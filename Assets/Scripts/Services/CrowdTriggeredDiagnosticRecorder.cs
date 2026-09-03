using System;
using System.Globalization;
using System.IO;
using System.Text;
using LocalAvoidance2D;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ZeroAllocSurvival.Services
{
    /// <summary>Triggered, fixed-slot crowd capture. Disabled builds pay no per-frame cost.</summary>
    internal sealed class CrowdTriggeredDiagnosticRecorder : IDisposable
    {
        private const int PreFrames = 60;
        private const int PostFrames = 240;
        private const int TrackedCount = 12;
        private const int LoggedNeighborCount = 16;
        private const int TriggerContacts = 6;
        private const float TriggerPenetration = .15f;
        private const float TriggerRadius = 6f;
        private const float JitterCorrectionThreshold = .005f;
        private const float PlayerMovingPenetrationThreshold = .1f;
        private const int CenterResidenceTriggerFrames = 30;
        private const float StuckOutputSpeed = .5f;

        private readonly int _capacity;
        private NativeArray<AgentFrame> _history;
        private NativeArray<GlobalFrame> _globalHistory;
        private NativeArray<float2> _previousCorrections;
        private NativeArray<byte> _hasPreviousCorrection;
        private NativeArray<ushort> _centerResidenceFrames;
        private readonly int[] _tracked = new int[TrackedCount];
        private readonly float[] _trackedDistances = new float[TrackedCount];
        private readonly int[] _neighborSlots = new int[LoggedNeighborCount];
        private readonly float[] _neighborDistances = new float[LoggedNeighborCount];
        private readonly int[] _cellCounts = new int[9];
        private int _historyCursor;
        private int _capturedFrames;
        private int _postFramesRemaining;
        private bool _triggered;
        private bool _completed;
        private StreamWriter _agents;
        private StreamWriter _solver;
        private StreamWriter _neighbors;
        private StreamWriter _cachedNeighbors;
        private StreamWriter _candidateCells;
        private StreamWriter _global;

        internal CrowdTriggeredDiagnosticRecorder(int capacity)
        {
            _capacity = capacity;
            _history = new NativeArray<AgentFrame>(capacity * PreFrames, Allocator.Persistent);
            _globalHistory = new NativeArray<GlobalFrame>(PreFrames, Allocator.Persistent);
            _previousCorrections = new NativeArray<float2>(capacity, Allocator.Persistent);
            _hasPreviousCorrection = new NativeArray<byte>(capacity, Allocator.Persistent);
            _centerResidenceFrames = new NativeArray<ushort>(capacity, Allocator.Persistent);
        }

        internal void Capture(LocalAvoidanceSimulation simulation,
            LocalAvoidanceDiagnostics diagnostics, int playerSlot, float deltaTime)
        {
            if (_completed) return;
            if ((uint)playerSlot >= (uint)_capacity || simulation.Active[playerSlot] == 0) return;
            var historyOffset = _historyCursor * _capacity;
            var activeCount = 0;
            var saturatedCount = 0;
            var maximumPenetration = 0f;
            var penetrationTriggerSlot = -1;
            var jitterTriggerSlot = -1;
            var stuckNormalTriggerSlot = -1;
            var playerJitter = false;
            var playerMovingPenetration = false;
            var priorityAgentNearCenter = false;
            var playerPosition = simulation.ResolvedPositions[playerSlot];
            for (var slot = 0; slot < _capacity; slot++)
            {
                var active = simulation.Active[slot];
                var contact = simulation.Contacts[slot];
                if (active != 0)
                {
                    activeCount++;
                    if (contact.AgentContactCount >= 10) saturatedCount++;
                    maximumPenetration = math.max(maximumPenetration, contact.ConstraintPenetration);
                    var correction = simulation.ResolvedPositions[slot] - simulation.MovedPositions[slot];
                    if (!_triggered && slot == playerSlot)
                    {
                        var previousCorrection = _previousCorrections[slot];
                        playerJitter = _hasPreviousCorrection[slot] != 0 &&
                                       math.lengthsq(correction) >=
                                       JitterCorrectionThreshold * JitterCorrectionThreshold &&
                                       math.lengthsq(previousCorrection) >=
                                       JitterCorrectionThreshold * JitterCorrectionThreshold &&
                                       math.dot(correction, previousCorrection) < 0f;
                        playerMovingPenetration = simulation.DirectControl[slot] != 0 &&
                                                  contact.ConstraintPenetration >=
                                                  PlayerMovingPenetrationThreshold;
                    }
                    var nearCenter = slot != playerSlot &&
                                     math.distancesq(playerPosition, simulation.ResolvedPositions[slot]) <=
                                     TriggerRadius * TriggerRadius;
                    if (nearCenter && simulation.AvoidancePriorities[slot] == 1)
                    {
                        priorityAgentNearCenter = true;
                        var previousCorrection = _previousCorrections[slot];
                        if (!_triggered && jitterTriggerSlot < 0 && _hasPreviousCorrection[slot] != 0 &&
                            math.lengthsq(correction) >= JitterCorrectionThreshold * JitterCorrectionThreshold &&
                            math.lengthsq(previousCorrection) >= JitterCorrectionThreshold * JitterCorrectionThreshold &&
                            math.dot(correction, previousCorrection) < 0f)
                            jitterTriggerSlot = slot;
                    }
                    var desiredSpeedSq = math.lengthsq(simulation.DesiredVelocities[slot]);
                    var outputSpeedSq = math.lengthsq(simulation.ResolvedVelocities[slot]);
                    // A regular agent stopped at the player's collision boundary is expected.
                    // Only regard it as center-stuck after it has penetrated materially inside
                    // that boundary.
                    var centerResidenceRadius = math.max(0f,
                        (simulation.Radii[playerSlot] + simulation.Radii[slot]) *
                        simulation.Settings.MinimumSpacingRatio - TriggerPenetration);
                    var isStuckNormal = slot != playerSlot && simulation.AvoidancePriorities[slot] == 0 &&
                                        math.distancesq(playerPosition, simulation.ResolvedPositions[slot]) <=
                                        centerResidenceRadius * centerResidenceRadius &&
                                        desiredSpeedSq >= 1f &&
                                        outputSpeedSq <= StuckOutputSpeed * StuckOutputSpeed;
                    if (isStuckNormal)
                    {
                        _centerResidenceFrames[slot] = (ushort)math.min(ushort.MaxValue,
                            _centerResidenceFrames[slot] + 1);
                        if (!_triggered && stuckNormalTriggerSlot < 0 &&
                            _centerResidenceFrames[slot] >= CenterResidenceTriggerFrames)
                            stuckNormalTriggerSlot = slot;
                    }
                    else _centerResidenceFrames[slot] = 0;
                    if (!_triggered && penetrationTriggerSlot < 0 && nearCenter &&
                        contact.AgentContactCount >= TriggerContacts &&
                        contact.ConstraintPenetration >= TriggerPenetration)
                        penetrationTriggerSlot = slot;
                    _previousCorrections[slot] = correction;
                    _hasPreviousCorrection[slot] = 1;
                }
                else
                {
                    _hasPreviousCorrection[slot] = 0;
                    _centerResidenceFrames[slot] = 0;
                }
                _history[historyOffset + slot] = new AgentFrame
                {
                    Frame = Time.frameCount,
                    Time = Time.unscaledTime,
                    DeltaTime = deltaTime,
                    Before = simulation.Positions[slot],
                    Moved = simulation.MovedPositions[slot],
                    After = simulation.ResolvedPositions[slot],
                    Desired = simulation.DesiredVelocities[slot],
                    InputVelocity = simulation.CurrentVelocities[slot],
                    OutputVelocity = simulation.ResolvedVelocities[slot],
                    Contacts = contact.AgentContactCount,
                    BlockingContacts = contact.BlockingAgentContactCount,
                    Priority0Contacts = contact.Priority0ContactCount,
                    Priority1Contacts = contact.Priority1ContactCount,
                    Priority2Contacts = contact.Priority2ContactCount,
                    CenterResidenceFrames = _centerResidenceFrames[slot],
                    PlayerDistance = math.distance(playerPosition, simulation.ResolvedPositions[slot]),
                    SelectedAgent = contact.ConstraintAgentIndex,
                    Penetration = contact.ConstraintPenetration,
                    CandidateChecks = diagnostics.CandidateChecks[slot],
                    CandidateLimitReached = diagnostics.CandidateLimitReached[slot],
                    RetainedNeighbors = diagnostics.RetainedNeighborCounts[slot],
                    SameCellCandidateChecks = diagnostics.SameCellCandidateChecks[slot],
                    AvoidancePriority = simulation.AvoidancePriorities[slot],
                    Active = active
                };
            }
            _globalHistory[_historyCursor] = new GlobalFrame
            {
                Frame = Time.frameCount, Time = Time.unscaledTime, DeltaTime = deltaTime,
                ActiveCount = activeCount, SaturatedCount = saturatedCount,
                MaximumPenetration = maximumPenetration
            };
            _historyCursor = (_historyCursor + 1) % PreFrames;
            _capturedFrames = math.min(PreFrames, _capturedFrames + 1);
            var playerCandidateLimit = diagnostics.CandidateLimitReached[playerSlot] != 0;

            // When breakthrough agents have reached the center, wait for their actual correction
            // reversal instead of consuming the one-shot capture on an unrelated penetration.
            // This recorder is currently dedicated to diagnosing regular enemies remaining in
            // the center. Do not consume its one-shot capture on the already-investigated
            // breakthrough correction flip while priority agents are present.
            var triggerSlot = playerJitter || playerMovingPenetration || playerCandidateLimit
                ? playerSlot
                : stuckNormalTriggerSlot >= 0
                    ? stuckNormalTriggerSlot
                    : priorityAgentNearCenter ? -1 : penetrationTriggerSlot;
            if (!_triggered && triggerSlot >= 0)
            {
                var reason = playerJitter
                    ? "player-jitter"
                    : playerMovingPenetration
                        ? "player-moving-penetration"
                        : playerCandidateLimit
                            ? "player-candidate-limit"
                        : stuckNormalTriggerSlot >= 0
                            ? "priority0-center-stuck"
                            : "penetration";
                Trigger(simulation, diagnostics, playerSlot, triggerSlot, reason);
                return;
            }
            if (!_triggered) return;
            WriteAgentFrame(simulation, diagnostics, Time.frameCount, false);
            WriteSolverAndNeighbors(simulation, diagnostics, Time.frameCount);
            WriteGlobal(_globalHistory[(_historyCursor + PreFrames - 1) % PreFrames], false);
            if (--_postFramesRemaining <= 0)
            {
                _completed = true;
                CloseWriters();
            }
        }

        private void Trigger(LocalAvoidanceSimulation simulation, LocalAvoidanceDiagnostics diagnostics,
            int playerSlot, int triggerSlot, string reason)
        {
            _triggered = true;
            _postFramesRemaining = PostFrames;
            SelectTracked(simulation, playerSlot, triggerSlot);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var prefix = Path.Combine(Application.persistentDataPath, $"crowd-trigger-{stamp}");
            _agents = Writer(prefix + "-agents.csv");
            _solver = Writer(prefix + "-solver.csv");
            _neighbors = Writer(prefix + "-neighbors.csv");
            _cachedNeighbors = Writer(prefix + "-cached-neighbors.csv");
            _candidateCells = Writer(prefix + "-candidate-cells.csv");
            _global = Writer(prefix + "-global.csv");
            WriteSettings(prefix + "-settings.txt", simulation.Settings);
            using (var triggerWriter = Writer(prefix + "-trigger.txt"))
            {
                triggerWriter.WriteLine($"Reason={reason}");
                triggerWriter.WriteLine($"Frame={Time.frameCount}");
                triggerWriter.WriteLine($"PlayerSlot={playerSlot}");
                triggerWriter.WriteLine($"TriggerSlot={triggerSlot}");
                triggerWriter.WriteLine($"TriggerAvoidancePriority={simulation.AvoidancePriorities[triggerSlot]}");
            }
            _agents.WriteLine("phase,frame,time,deltaTime,slot,active,avoidancePriority,beforeX,beforeY,movedX,movedY,afterX,afterY,desiredX,desiredY,inputVX,inputVY,outputVX,outputVY,contacts,blockingContacts,priority0Contacts,priority1Contacts,priority2Contacts,centerResidenceFrames,playerDistance,selectedAgent,penetration,candidateChecks,candidateLimitReached,retainedNeighbors,sameCellCandidateChecks");
            _solver.WriteLine("frame,slot,iteration,positionX,positionY,correctionX,correctionY");
            _neighbors.WriteLine("frame,slot,neighborSlot,distance,surfaceDistance,penetration,normalX,normalY,neighborRadius,neighborMass,neighborAvoidancePriority");
            _cachedNeighbors.WriteLine("frame,slot,cacheIndex,neighborSlot,distance,surfaceDistance,penetration,neighborAvoidancePriority");
            _candidateCells.WriteLine("frame,slot,offsetX,offsetY,activeAgents");
            _global.WriteLine("phase,frame,time,deltaTime,activeCount,neighborCapacityCount,maxPenetration");
            var oldest = (_historyCursor + PreFrames - _capturedFrames) % PreFrames;
            for (var i = 0; i < _capturedFrames; i++)
            {
                var ring = (oldest + i) % PreFrames;
                WriteHistoryFrame(ring);
                WriteGlobal(_globalHistory[ring], true);
            }
            WriteSolverAndNeighbors(simulation, diagnostics, Time.frameCount);
            Flush();
            Debug.Log($"[Crowd.Trigger] {reason}, slot={triggerSlot}, output: {prefix}-*");
        }

        private void SelectTracked(LocalAvoidanceSimulation simulation, int playerSlot, int triggerSlot)
        {
            for (var i = 0; i < TrackedCount; i++) { _tracked[i] = -1; _trackedDistances[i] = float.PositiveInfinity; }
            _tracked[0] = playerSlot;
            _trackedDistances[0] = 0f;
            var insertBegin = 1;
            if (triggerSlot != playerSlot)
            {
                _tracked[1] = triggerSlot;
                _trackedDistances[1] = 0f;
                insertBegin = 2;
            }
            var center = simulation.ResolvedPositions[triggerSlot];
            for (var slot = 0; slot < _capacity; slot++)
            {
                if (simulation.Active[slot] == 0 || slot == playerSlot || slot == triggerSlot) continue;
                Insert(slot, math.distance(center, simulation.ResolvedPositions[slot]),
                    _tracked, _trackedDistances, insertBegin, TrackedCount);
            }
        }

        private void WriteHistoryFrame(int ring)
        {
            var offset = ring * _capacity;
            for (var i = 0; i < TrackedCount && _tracked[i] >= 0; i++)
                WriteAgent(_history[offset + _tracked[i]], _tracked[i], true);
        }

        private void WriteAgentFrame(LocalAvoidanceSimulation simulation,
            LocalAvoidanceDiagnostics diagnostics, int frame, bool pre)
        {
            var playerPosition = simulation.ResolvedPositions[_tracked[0]];
            for (var i = 0; i < TrackedCount && _tracked[i] >= 0; i++)
            {
                var slot = _tracked[i];
                var c = simulation.Contacts[slot];
                WriteAgent(new AgentFrame
                {
                    Frame = frame, Time = Time.unscaledTime, DeltaTime = Time.deltaTime,
                    Before = simulation.Positions[slot], Moved = simulation.MovedPositions[slot],
                    After = simulation.ResolvedPositions[slot], Desired = simulation.DesiredVelocities[slot],
                    InputVelocity = simulation.CurrentVelocities[slot],
                    OutputVelocity = simulation.ResolvedVelocities[slot], Contacts = c.AgentContactCount,
                    BlockingContacts = c.BlockingAgentContactCount,
                    Priority0Contacts = c.Priority0ContactCount,
                    Priority1Contacts = c.Priority1ContactCount,
                    Priority2Contacts = c.Priority2ContactCount,
                    CenterResidenceFrames = _centerResidenceFrames[slot],
                    PlayerDistance = math.distance(playerPosition, simulation.ResolvedPositions[slot]),
                    SelectedAgent = c.ConstraintAgentIndex, Penetration = c.ConstraintPenetration,
                    CandidateChecks = diagnostics.CandidateChecks[slot],
                    CandidateLimitReached = diagnostics.CandidateLimitReached[slot],
                    RetainedNeighbors = diagnostics.RetainedNeighborCounts[slot],
                    SameCellCandidateChecks = diagnostics.SameCellCandidateChecks[slot],
                    AvoidancePriority = simulation.AvoidancePriorities[slot],
                    Active = simulation.Active[slot]
                }, slot, pre);
            }
        }

        private void WriteAgent(AgentFrame s, int slot, bool pre) => _agents.WriteLine(FormattableString.Invariant(
            $"{(pre ? "pre" : "post")},{s.Frame},{s.Time:F6},{s.DeltaTime:F6},{slot},{s.Active},{s.AvoidancePriority},{s.Before.x:F6},{s.Before.y:F6},{s.Moved.x:F6},{s.Moved.y:F6},{s.After.x:F6},{s.After.y:F6},{s.Desired.x:F6},{s.Desired.y:F6},{s.InputVelocity.x:F6},{s.InputVelocity.y:F6},{s.OutputVelocity.x:F6},{s.OutputVelocity.y:F6},{s.Contacts},{s.BlockingContacts},{s.Priority0Contacts},{s.Priority1Contacts},{s.Priority2Contacts},{s.CenterResidenceFrames},{s.PlayerDistance:F6},{s.SelectedAgent},{s.Penetration:F6},{s.CandidateChecks},{s.CandidateLimitReached},{s.RetainedNeighbors},{s.SameCellCandidateChecks}"));

        private void WriteSolverAndNeighbors(LocalAvoidanceSimulation simulation,
            LocalAvoidanceDiagnostics diagnostics, int frame)
        {
            var iterations = math.min(math.max(1, simulation.Settings.SolverIterations),
                LocalAvoidanceDiagnostics.MaximumSolverIterations);
            for (var trackedIndex = 0; trackedIndex < TrackedCount && _tracked[trackedIndex] >= 0; trackedIndex++)
            {
                var slot = _tracked[trackedIndex];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    var p = diagnostics.GetSolverPosition(slot, iteration);
                    var c = diagnostics.GetSolverCorrection(slot, iteration);
                    _solver.WriteLine(FormattableString.Invariant($"{frame},{slot},{iteration},{p.x:F6},{p.y:F6},{c.x:F6},{c.y:F6}"));
                }
                WriteNeighbors(simulation, diagnostics, frame, slot);
            }
        }

        private void WriteNeighbors(LocalAvoidanceSimulation simulation,
            LocalAvoidanceDiagnostics diagnostics, int frame, int slot)
        {
            for (var i = 0; i < LoggedNeighborCount; i++) { _neighborSlots[i] = -1; _neighborDistances[i] = float.PositiveInfinity; }
            for (var i = 0; i < _cellCounts.Length; i++) _cellCounts[i] = 0;
            var position = simulation.ResolvedPositions[slot];
            var inverseCellSize = 1f / simulation.Settings.CellSize;
            var centerCell = (int2)math.floor(position * inverseCellSize);
            for (var other = 0; other < _capacity; other++)
            {
                if (other == slot || simulation.Active[other] == 0) continue;
                var otherCell = (int2)math.floor(simulation.ResolvedPositions[other] * inverseCellSize);
                var offset = otherCell - centerCell;
                if (math.abs(offset.x) <= 1 && math.abs(offset.y) <= 1)
                    _cellCounts[(offset.x + 1) * 3 + offset.y + 1]++;
                Insert(other, math.distance(position, simulation.ResolvedPositions[other]),
                    _neighborSlots, _neighborDistances, 0, LoggedNeighborCount);
            }
            for (var i = 0; i < LoggedNeighborCount && _neighborSlots[i] >= 0; i++)
            {
                var other = _neighborSlots[i];
                var delta = position - simulation.ResolvedPositions[other];
                var distance = _neighborDistances[i];
                var combinedRadius = simulation.Radii[slot] + simulation.Radii[other];
                var normal = math.normalizesafe(delta);
                _neighbors.WriteLine(FormattableString.Invariant($"{frame},{slot},{other},{distance:F6},{distance - combinedRadius:F6},{math.max(0f, combinedRadius - distance):F6},{normal.x:F6},{normal.y:F6},{simulation.Radii[other]:F6},{simulation.Masses[other]:F6},{simulation.AvoidancePriorities[other]}"));
            }
            var cachedCount = diagnostics.GetCachedNeighborCount(slot);
            for (var i = 0; i < cachedCount; i++)
            {
                var other = diagnostics.GetCachedNeighbor(slot, i);
                var distance = math.distance(position, simulation.ResolvedPositions[other]);
                var combinedRadius = simulation.Radii[slot] + simulation.Radii[other];
                _cachedNeighbors.WriteLine(FormattableString.Invariant(
                    $"{frame},{slot},{i},{other},{distance:F6},{distance - combinedRadius:F6},{math.max(0f, combinedRadius - distance):F6},{simulation.AvoidancePriorities[other]}"));
            }
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            for (var offsetY = -1; offsetY <= 1; offsetY++)
                _candidateCells.WriteLine($"{frame},{slot},{offsetX},{offsetY},{_cellCounts[(offsetX + 1) * 3 + offsetY + 1]}");
        }

        private static void Insert(int slot, float distance, int[] slots, float[] distances, int begin, int end)
        {
            for (var i = begin; i < end; i++)
            {
                if (distance >= distances[i]) continue;
                for (var j = end - 1; j > i; j--) { slots[j] = slots[j - 1]; distances[j] = distances[j - 1]; }
                slots[i] = slot; distances[i] = distance; return;
            }
        }

        private void WriteGlobal(GlobalFrame frame, bool pre) => _global.WriteLine(FormattableString.Invariant(
            $"{(pre ? "pre" : "post")},{frame.Frame},{frame.Time:F6},{frame.DeltaTime:F6},{frame.ActiveCount},{frame.SaturatedCount},{frame.MaximumPenetration:F6}"));
        private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false), 64 * 1024);
        private static void WriteSettings(string path, LocalAvoidanceSettings settings)
        {
            using var writer = Writer(path);
            writer.WriteLine($"CellSize={settings.CellSize.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"NeighborDistance={settings.NeighborDistance.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"MaximumCandidateChecks={settings.MaximumCandidateChecks}");
            writer.WriteLine($"VelocityResponse={settings.VelocityResponse.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"SeparationSpeedRatio={settings.SeparationSpeedRatio.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"LateralSpeedRatio={settings.LateralSpeedRatio.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"MinimumSpacingRatio={settings.MinimumSpacingRatio.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"MaximumCorrectionRatio={settings.MaximumCorrectionRatio.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"ContactSlowdown={settings.ContactSlowdown.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"ContactsForMaximumSlowdown={settings.ContactsForMaximumSlowdown}");
            writer.WriteLine($"ContactSkinRatio={settings.ContactSkinRatio.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"PreferredSeparationMultiplier={settings.PreferredSeparationMultiplier.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"ContactRetentionSkinMultiplier={settings.ContactRetentionSkinMultiplier.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"DominantMassRatioThreshold={settings.DominantMassRatioThreshold.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"CorrectionVelocityInfluence={settings.CorrectionVelocityInfluence.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"SolverIterations={settings.SolverIterations}");
            writer.WriteLine($"InnerLoopBatchCount={settings.InnerLoopBatchCount}");
        }
        private void Flush() { _agents?.Flush(); _solver?.Flush(); _neighbors?.Flush(); _cachedNeighbors?.Flush(); _candidateCells?.Flush(); _global?.Flush(); }
        private void CloseWriters() { Flush(); _agents?.Dispose(); _solver?.Dispose(); _neighbors?.Dispose(); _cachedNeighbors?.Dispose(); _candidateCells?.Dispose(); _global?.Dispose(); _agents = _solver = _neighbors = _cachedNeighbors = _candidateCells = _global = null; }
        public void Dispose()
        {
            CloseWriters();
            if (_history.IsCreated) _history.Dispose();
            if (_globalHistory.IsCreated) _globalHistory.Dispose();
            if (_previousCorrections.IsCreated) _previousCorrections.Dispose();
            if (_hasPreviousCorrection.IsCreated) _hasPreviousCorrection.Dispose();
            if (_centerResidenceFrames.IsCreated) _centerResidenceFrames.Dispose();
        }

        private struct AgentFrame
        {
            public int Frame, Contacts, BlockingContacts, Priority0Contacts, Priority1Contacts,
                Priority2Contacts, CenterResidenceFrames, SelectedAgent, CandidateChecks,
                RetainedNeighbors, SameCellCandidateChecks;
            public float Time, DeltaTime, Penetration, PlayerDistance;
            public byte AvoidancePriority;
            public byte CandidateLimitReached;
            public float2 Before, Moved, After, Desired, InputVelocity, OutputVelocity; public byte Active;
        }
        private struct GlobalFrame
        {
            public int Frame, ActiveCount, SaturatedCount; public float Time, DeltaTime, MaximumPenetration;
        }
    }
}
