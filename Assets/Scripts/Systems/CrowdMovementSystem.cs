using System;
using System.IO;
using System.Text;
using LocalAvoidance2D;
using LitheEcs;
using LitheEcs.Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Services;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Systems
{
    /// <summary>LitheEcs adapter for the framework-independent LocalAvoidance2D package.</summary>
    internal sealed class CrowdMovementSystem : BaseSystem, IInitializable, ITickable, IDisposable
    {
        private readonly int _capacity;
        private readonly float _jitterThreshold;
        private BurstQuery<PhysicsPosition, CrowdAgent, CharacterSlot> _query;

        private readonly　LocalAvoidanceSimulation _simulation;
        private readonly LocalAvoidanceDiagnostics _diagnostics;
        private NativeArray<EntityId> _entitiesBySlot;
        private NativeArray<EntityId> _previousEntitiesBySlot;
        private Entity _player;
        private float2 _previousPlayerCorrection;
        private float2 _previousPlayerMotion;
        private float2 _previousPlayerDesiredVelocity;
        private bool _hasPreviousPlayerSample;
        private StreamWriter _jitterWriter;
        private CrowdTriggeredDiagnosticRecorder _triggeredRecorder;
        private int _jitterRecordCount;

        internal CrowdMovementSystem(World world, LocalAvoidanceSimulation simulation,
            LocalAvoidanceDiagnostics diagnostics, int capacity,
            float jitterThreshold = .005f) : base(world)
        {
            _simulation = simulation;
            _diagnostics = diagnostics;
            _capacity = Mathf.Max(1, capacity);
            _jitterThreshold = Mathf.Max(.0001f, jitterThreshold);
        }

        public void Initialize()
        {
            _query = World.Query<PhysicsPosition, CrowdAgent, CharacterSlot>().AsBurstQuery(1024);
            _query.Reserve(maximumEntityCount: _capacity);

            _player = World.Singleton<PlayerTag>();
#if ENABLE_DIAGNOSTICS_LOG
            InitializeJitterLog();
#endif
            _simulation.Settings = new LocalAvoidanceSettings
            {
                CellSize = 2.2f,
                NeighborDistance = 2.2f,
                MaximumNeighbors = 10,
                MaximumCandidateChecks = 64,
                VelocityResponse = 10f,
                SeparationSpeedRatio = .4f,
                LateralSpeedRatio = .15f,
                MinimumSpacingRatio = 1f,
                MaximumCorrectionRatio = .65f,
                ContactSlowdown = 1f,
                ContactsForMaximumSlowdown = 6f,
                ContactSkinRatio = .1f,
                PreferredSeparationMultiplier = 1.2f,
                ContactRetentionSkinMultiplier = 2f,
                DominantMassRatioThreshold = 4f,
                CorrectionVelocityInfluence = 0f,
                SolverIterations = 2,
                InnerLoopBatchCount = 128
            };
            _entitiesBySlot = new NativeArray<EntityId>(_capacity, Allocator.Persistent);
            _previousEntitiesBySlot = new NativeArray<EntityId>(_capacity, Allocator.Persistent);
#if ENABLE_DIAGNOSTICS_LOG
            _triggeredRecorder = new CrowdTriggeredDiagnosticRecorder(_capacity);
#endif
        }

        public void Tick(float deltaTime)
        {
            // Schedule performs no work for a zero delta time. Applying its output in that case
            // would copy the initially zeroed ResolvedPositions into every character on reload.
            if (deltaTime <= 0f) return;

            var clearActiveHandle = new ClearActiveJob { Active = _simulation.Active }
                .Schedule(_capacity, 256);
            clearActiveHandle.Complete();
            var gather = new GatherAction
            {
                Positions = _simulation.Positions,
                DesiredVelocities = _simulation.DesiredVelocities,
                CurrentVelocities = _simulation.CurrentVelocities,
                Radii = _simulation.Radii,
                Masses = _simulation.Masses,
                AvoidancePriorities = _simulation.AvoidancePriorities,
                AvoidanceWeights = _simulation.AvoidanceWeights,
                CorrectionVelocityWeights = _simulation.CorrectionVelocityWeights,
                MaximumCorrectionSpeeds = _simulation.MaximumCorrectionSpeeds,
                Layers = _simulation.Layers,
                CollisionMasks = _simulation.CollisionMasks,
                ContactEventMasks = _simulation.ContactEventMasks,
                ImmediateVelocity = _simulation.ImmediateVelocity,
                DirectControl = _simulation.DirectControl,
                StableContactResolution = _simulation.StableContactResolution,
                Active = _simulation.Active,
                EntitiesBySlot = _entitiesBySlot
            };

            _query.RunUnsafe(ref gather);
            var simulationHandle = _simulation.Schedule(
                Mathf.Max(0f, deltaTime), _capacity, 0, _diagnostics);

            // The worker-thread Timeline exposes the individual jobs as ClearGridJob,
            // ClearContactPairsJob, BuildGridJob, MoveJob, CopyPositionsJob,
            // ConstraintJob and FinalizeContactPairsJob. Keep the main-thread wait separate
            // so scheduling cost is not mistaken for simulation work.
            simulationHandle.Complete();

#if ENABLE_DIAGNOSTICS_LOG
            if (_player.IsAlive &&
                _player.TryGet<CharacterSlot>(out var diagnosticPlayerIndex))
                _triggeredRecorder.Capture(
                    _simulation, _diagnostics, diagnosticPlayerIndex.Value, deltaTime);
            DetectPlayerJitter(deltaTime);
#endif
            EmitEnteredContacts();
            EmitExitedContacts();

            NativeArray<EntityId>.Copy(_entitiesBySlot, _previousEntitiesBySlot, _capacity);
            var apply = new ApplyAction
            {
                Positions = _simulation.ResolvedPositions,
                Velocities = _simulation.ResolvedVelocities
            };
            
            _query.RunUnsafe(ref apply);
            CommandBuffer.Playback();
        }

#if ENABLE_DIAGNOSTICS_LOG
        private void DetectPlayerJitter(float deltaTime)
        {
            if (!_player.IsAlive ||
                !_player.TryGet<CharacterSlot>(out var transformIndex)) return;

            var slot = transformIndex.Value;
            if ((uint)slot >= (uint)_simulation.Capacity || _simulation.Active[slot] == 0) return;
            var before = _simulation.Positions[slot];
            var moved = _simulation.MovedPositions[slot];
            var after = _simulation.ResolvedPositions[slot];
            var desired = _simulation.DesiredVelocities[slot];
            var inputVelocity = _simulation.CurrentVelocities[slot];
            var outputVelocity = _simulation.ResolvedVelocities[slot];
            var motion = after - before;
            var correction = after - moved;
            var thresholdSqr = _jitterThreshold * _jitterThreshold;
            var correctionFlip = _hasPreviousPlayerSample &&
                                 math.lengthsq(correction) >= thresholdSqr &&
                                 math.lengthsq(_previousPlayerCorrection) >= thresholdSqr &&
                                 math.dot(correction, _previousPlayerCorrection) < 0f;
            var motionFlip = _hasPreviousPlayerSample &&
                             math.lengthsq(motion) >= thresholdSqr &&
                             math.lengthsq(_previousPlayerMotion) >= thresholdSqr &&
                             math.dot(motion, _previousPlayerMotion) < 0f &&
                             math.dot(desired, _previousPlayerDesiredVelocity) >= 0f;
            var contact = _simulation.Contacts[slot];
            var dominantTrace = contact.HasConstraint != 0 && contact.ConstraintIsDominant != 0;

            if (correctionFlip || motionFlip || dominantTrace)
            {
                var desiredBeforeConstraint = _diagnostics.DesiredBeforeConstraint[slot];
                var desiredAfterConstraint = _diagnostics.DesiredAfterConstraint[slot];
                var previousConstraintNormal = _diagnostics.PreviousConstraintNormal[slot];
                var previousAllowedNormalSpeed = _diagnostics.PreviousAllowedNormalSpeed[slot];
                var lastIteration = math.min(
                    _simulation.Settings.SolverIterations,
                    LocalAvoidanceDiagnostics.MaximumSolverIterations) - 1;
                var firstSolverCorrection = _diagnostics.GetSolverCorrection(slot, 0);
                var lastSolverCorrection = _diagnostics.GetSolverCorrection(slot, lastIteration);
                var constraintApplied = _diagnostics.ConstraintApplied[slot];
                var selected = contact.ConstraintAgentIndex;
                var centerDistance = (uint)selected < (uint)_simulation.Capacity &&
                                     _simulation.Active[selected] != 0
                    ? math.distance(after, _simulation.ResolvedPositions[selected])
                    : -1f;
                _jitterWriter?.WriteLine(FormattableString.Invariant(
                    $"{Time.frameCount},{Time.unscaledTime:F6},{deltaTime:F6},{slot},{(correctionFlip ? 1 : 0)},{(motionFlip ? 1 : 0)},{(dominantTrace ? 1 : 0)},{contact.AgentContactCount},{contact.ObstacleContactCount},{before.x:F6},{before.y:F6},{moved.x:F6},{moved.y:F6},{after.x:F6},{after.y:F6},{motion.x:F6},{motion.y:F6},{desired.x:F6},{desired.y:F6},{inputVelocity.x:F6},{inputVelocity.y:F6},{outputVelocity.x:F6},{outputVelocity.y:F6},{correction.x:F6},{correction.y:F6},{_previousPlayerCorrection.x:F6},{_previousPlayerCorrection.y:F6},{selected},{contact.ConstraintOtherMass:F6},{contact.ConstraintOtherRadius:F6},{contact.ConstraintIsDominant},{centerDistance:F6},{contact.ConstraintPenetration:F6},{contact.CorrectionLimit:F6},{previousConstraintNormal.x:F6},{previousConstraintNormal.y:F6},{previousAllowedNormalSpeed:F6},{desiredBeforeConstraint.x:F6},{desiredBeforeConstraint.y:F6},{desiredAfterConstraint.x:F6},{desiredAfterConstraint.y:F6},{constraintApplied},{firstSolverCorrection.x:F6},{firstSolverCorrection.y:F6},{lastSolverCorrection.x:F6},{lastSolverCorrection.y:F6}"));
                _jitterRecordCount++;
                if ((_jitterRecordCount & 31) == 0) _jitterWriter?.Flush();
            }

            _previousPlayerCorrection = correction;
            _previousPlayerMotion = motion;
            _previousPlayerDesiredVelocity = desired;
            _hasPreviousPlayerSample = true;
        }

        private void InitializeJitterLog()
        {
            try
            {
                var fileName = $"crowd-jitter-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
                var path = Path.Combine(Application.persistentDataPath, fileName);
                _jitterWriter = new StreamWriter(path, false, new UTF8Encoding(false), 64 * 1024);
                _jitterWriter.WriteLine(
                    "frame,unscaledTime,deltaTime,slot,correctionFlip,motionFlip,dominantTrace," +
                    "agentContacts,obstacleContacts,beforeX,beforeY,movedX,movedY,afterX,afterY," +
                    "motionX,motionY,desiredX,desiredY,inputVelocityX,inputVelocityY," +
                    "outputVelocityX,outputVelocityY,correctionX,correctionY," +
                    "previousCorrectionX,previousCorrectionY,selectedAgent,selectedMass,selectedRadius," +
                    "selectedIsDominant,centerDistance,penetration,correctionLimit," +
                    "previousConstraintNormalX,previousConstraintNormalY,previousAllowedNormalSpeed," +
                    "desiredBeforeConstraintX,desiredBeforeConstraintY,desiredAfterConstraintX," +
                    "desiredAfterConstraintY,constraintApplied,solver0CorrectionX,solver0CorrectionY," +
                    "solverLastCorrectionX,solverLastCorrectionY");
                _jitterWriter.Flush();
                Debug.Log($"[Crowd.Jitter] CSV output: {path}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Crowd.Jitter] Failed to create CSV: {exception.Message}");
                _jitterWriter?.Dispose();
                _jitterWriter = null;
            }
        }
#endif
        private void EmitEnteredContacts()
        {
            var contacts = _simulation.EnteredContacts;
            for (var i = 0; i < contacts.Length; i++)
            {
                var pair = contacts[i];
                if (!World.TryGetEntity(_entitiesBySlot[pair.AgentA], out var a) ||
                    !World.TryGetEntity(_entitiesBySlot[pair.AgentB], out var b)) continue;

                var aIsEnemy = a.Has<EnemyTag>();
                var bIsEnemy = b.Has<EnemyTag>();
                if (aIsEnemy == bIsEnemy) continue;

                var entered = CommandBuffer.Spawn();
                CommandBuffer.AddComponent(entered, new ContactEntered
                {
                    Source = aIsEnemy ? b : a,
                    Other = aIsEnemy ? a : b
                });
            }
        }

        private void EmitExitedContacts()
        {
            var contacts = _simulation.ExitedContacts;
            for (var i = 0; i < contacts.Length; i++)
            {
                var pair = contacts[i];
                if (!World.TryGetEntity(_previousEntitiesBySlot[pair.AgentA], out var a) ||
                    !World.TryGetEntity(_previousEntitiesBySlot[pair.AgentB], out var b)) continue;

                var aIsEnemy = a.Has<EnemyTag>();
                var bIsEnemy = b.Has<EnemyTag>();
                if (aIsEnemy == bIsEnemy) continue;

                var exited = CommandBuffer.Spawn();
                CommandBuffer.AddComponent(exited, new ContactExited
                {
                    Other = aIsEnemy ? a : b
                });
            }
        }

        public void Dispose()
        {
            _jitterWriter?.Flush();
            _jitterWriter?.Dispose();
            _jitterWriter = null;
            _triggeredRecorder?.Dispose();
            _triggeredRecorder = null;
            if (_previousEntitiesBySlot.IsCreated) _previousEntitiesBySlot.Dispose();
            if (_entitiesBySlot.IsCreated) _entitiesBySlot.Dispose();
        }

        [BurstCompile]
        private struct ClearActiveJob : IJobParallelFor
        {
            public NativeArray<byte> Active;
            public void Execute(int index) => Active[index] = 0;
        }

        [BurstCompile]
        internal struct GatherAction : IBurstQueryAction<PhysicsPosition, CrowdAgent, CharacterSlot>
        {
            [NativeDisableParallelForRestriction] public NativeArray<float2> Positions;
            [NativeDisableParallelForRestriction] public NativeArray<float2> DesiredVelocities;
            [NativeDisableParallelForRestriction] public NativeArray<float2> CurrentVelocities;
            [NativeDisableParallelForRestriction] public NativeArray<float> Radii;
            [NativeDisableParallelForRestriction] public NativeArray<float> Masses;
            [NativeDisableParallelForRestriction] public NativeArray<byte> AvoidancePriorities;
            [NativeDisableParallelForRestriction] public NativeArray<float> AvoidanceWeights;
            [NativeDisableParallelForRestriction] public NativeArray<float> CorrectionVelocityWeights;
            [NativeDisableParallelForRestriction] public NativeArray<float> MaximumCorrectionSpeeds;
            [NativeDisableParallelForRestriction] public NativeArray<uint> Layers;
            [NativeDisableParallelForRestriction] public NativeArray<uint> CollisionMasks;
            [NativeDisableParallelForRestriction] public NativeArray<uint> ContactEventMasks;
            [NativeDisableParallelForRestriction] public NativeArray<byte> ImmediateVelocity;
            [NativeDisableParallelForRestriction] public NativeArray<byte> DirectControl;
            [NativeDisableParallelForRestriction] public NativeArray<byte> StableContactResolution;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Active;
            [NativeDisableParallelForRestriction] public NativeArray<EntityId> EntitiesBySlot;

            public void Execute(int index, ref PhysicsPosition position, ref CrowdAgent steering,
                ref CharacterSlot transformIndex)
            {
                var slot = transformIndex.Value;
                Positions[slot] = new float2(position.Value.x, position.Value.y);
                DesiredVelocities[slot] = new float2(steering.DesiredVelocity.x, steering.DesiredVelocity.y);
                CurrentVelocities[slot] = new float2(steering.CurrentVelocity.x, steering.CurrentVelocity.y);
                Radii[slot] = steering.Radius;
                Masses[slot] = steering.Mass;
                AvoidancePriorities[slot] = steering.AvoidancePriority;
                AvoidanceWeights[slot] = steering.AvoidanceWeight;
                CorrectionVelocityWeights[slot] = steering.CorrectionVelocityWeight;
                MaximumCorrectionSpeeds[slot] = steering.MaximumCorrectionSpeed;
                Layers[slot] = steering.Layer;
                CollisionMasks[slot] = steering.CollisionMask;
                ContactEventMasks[slot] = steering.ContactEventMask;
                ImmediateVelocity[slot] = steering.RequiresImmediateApply;
                DirectControl[slot] = steering.DirectControl;
                StableContactResolution[slot] = steering.StableContactResolution;
                Active[slot] = 1;
                EntitiesBySlot[slot] = steering.EntityId;
            }
        }

        [BurstCompile]
        internal struct ApplyAction : IBurstQueryAction<PhysicsPosition, CrowdAgent, CharacterSlot>
        {
            [ReadOnly] public NativeArray<float2> Positions;
            [ReadOnly] public NativeArray<float2> Velocities;

            public void Execute(int index, ref PhysicsPosition position, ref CrowdAgent steering,
                ref CharacterSlot transformIndex)
            {
                var slot = transformIndex.Value;
                var resolvedPosition = Positions[slot];
                var resolvedVelocity = Velocities[slot];
                position.Value = new Vector3(resolvedPosition.x, resolvedPosition.y, position.Value.z);
                steering.CurrentVelocity = new Vector2(resolvedVelocity.x, resolvedVelocity.y);
            }
        }
    }
}
