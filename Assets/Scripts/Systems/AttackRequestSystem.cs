using System;
using LitheEcs;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;
using UnityEngine;
using UnityEngine.Profiling;
using ZeroAllocSurvival.Components;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class AttackRequestSystem : QueryActionSystem<AttackRequest>
    {
        private const int MaxTargets = 128;
        private readonly CharacterSpatialHash _spatialHash;
        private readonly ProjectilePoolRegistry _viewPools;
        private readonly HitscanResolutionQueue _hitscanQueue;
        private int _nextHitscanShotId;
        private int _nextProjectileShotId;
#if ENABLE_DIAGNOSTICS_LOG
        private readonly bool _logHitscanDiagnostics;
        private readonly ProjectileDiagnosticLog _projectileDiagnosticLog;
#endif

        public AttackRequestSystem(World world, CharacterSpatialHash spatialHash, ProjectilePoolRegistry viewPools,
            HitscanResolutionQueue hitscanQueue, bool logHitscanDiagnostics,
            ProjectileDiagnosticLog projectileDiagnosticLog) :
            base(world)
        {
            _spatialHash = spatialHash;
            _viewPools = viewPools;
            _hitscanQueue = hitscanQueue;
#if ENABLE_DIAGNOSTICS_LOG
            _logHitscanDiagnostics = logHitscanDiagnostics;
            _projectileDiagnosticLog = projectileDiagnosticLog;
#endif
        }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override void ForEach(in Entity entity, ref AttackRequest request)
        {
            if (request.Weapon.TryGet<Link<Weapon>>(out var weapon))
            {
                weapon.Value.Fire();
            }
#if ENABLE_DIAGNOSTICS_LOG
            if (_logHitscanDiagnostics)
                Debug.Log($"[Attack.Request] delivery={request.DeliveryType} projectiles={request.ProjectileCount} " +
                          $"damage={request.Damage:F2} penetration={request.Penetration} range={request.Range:F2}");
#endif
            switch (request.DeliveryType)
            {
                case DeliveryType.Instant:
                    ApplyInstant(request);
                    break;
                case DeliveryType.Hitscan:
                    ApplyHitscan(request);
                    break;
                case DeliveryType.Projectile:
                case DeliveryType.LobbedProjectile:
                    SpawnProjectile(request);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            CommandBuffer.Despawn(entity);
        }

        private void ApplyInstant(in AttackRequest request)
        {
            Span<EntityId> targets = stackalloc EntityId[MaxTargets];
            var count = _spatialHash.FindNearestIds(request.Origin, request.Range, request.Owner.Id,
                CharacterSpatialHash.EnemyGroupId, targets);
            var minimumDot = Mathf.Cos(request.ArcAngle * .5f * Mathf.Deg2Rad);
            for (var i = 0; i < count; i++)
            {
                if (!World.TryGetEntity(targets[i], out var target)) continue;
                var delta = target.Get<PhysicsPosition>().Value - request.Origin;
                delta.z = 0f;
                if (delta.sqrMagnitude <= .0001f || Vector3.Dot(delta.normalized, request.Direction) < minimumDot)
                    continue;
                DealDamage(target, request.Damage, delta.normalized * request.Knockback);
            }
        }

        private void ApplyHitscan(in AttackRequest request)
        {
            Span<EntityId> hitCandidates = stackalloc EntityId[MaxTargets];
            var shotId = ++_nextHitscanShotId;
            var hitRadius = Mathf.Max(.15f, request.ProjectileRadius);
            var projectileCount = Mathf.Max(1, request.ProjectileCount);
#if ENABLE_DIAGNOSTICS_LOG
            if (_logHitscanDiagnostics)
                Debug.Log($"[Hitscan.Begin] shot={shotId} pellets={projectileCount} damage={request.Damage:F2} " +
                          $"penetration={request.Penetration} radius={hitRadius:F2}");
#endif
            for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                var direction = request.Direction;
                if (projectileCount > 1)
                {
                    var t = projectileIndex / (float)(projectileCount - 1);
                    var angle = Mathf.Lerp(-request.Spread * .5f, request.Spread * .5f, t);
                    direction = Quaternion.AngleAxis(angle, Vector3.forward) * request.Direction;
                }

                var candidateCount = _spatialHash.FindAlongRayIds(request.Origin, direction, request.Range,
                    hitRadius, request.Owner.Id, CharacterSpatialHash.EnemyGroupId, hitCandidates);
#if ENABLE_DIAGNOSTICS_LOG
                if (_logHitscanDiagnostics)
                    Debug.Log($"[Hitscan.Candidates] shot={shotId} pellet={projectileIndex} " +
                              $"candidates={candidateCount} direction=({direction.x:F3},{direction.y:F3})");
#endif
                _hitscanQueue.EnqueuePellet(hitCandidates[..candidateCount], shotId, projectileIndex,
                    request.Penetration, request.Damage, request.Knockback, request.Origin, direction);
            }
        }

        private void SpawnProjectile(in AttackRequest request)
        {
            var shotId = ++_nextProjectileShotId;
            var angle = Mathf.Atan2(request.Direction.y, request.Direction.x) * Mathf.Rad2Deg;
            var renderPosition = request.Origin;
            renderPosition.z = VisualDepth.Projectile;
            var view = _viewPools.Get(request.ProjectilePoolIndex, renderPosition,
                Quaternion.Euler(0f, 0f, angle));
            var projectileScale = Mathf.Max(.1f, request.ProjectileRadius * 2f);
            view.transform.localScale = new Vector3(
                projectileScale,
                request.Direction.x < -.0001f ? -projectileScale : projectileScale,
                projectileScale);

            CommandBuffer.Spawn(Link.With(view.transform), new AttackInstance
            {
                DiagnosticShotId = shotId,
                Owner = request.Owner,
                Weapon = request.Weapon,
                DeliveryType = request.DeliveryType,
                ImpactType = request.ImpactType,
                MotionType = request.MotionType,
                ProjectileHitsEnemies = request.ProjectileHitsEnemies,
                Origin = request.Origin,
                Position = request.Origin,
                Direction = request.Direction,
                Damage = request.Damage,
                Knockback = request.Knockback,
                Speed = request.Speed,
                RemainingLifetime = request.Lifetime,
                ProjectileAngularSpeed = request.ProjectileAngularSpeed,
                Range = request.Range,
                ProjectileRadius = request.ProjectileRadius,
                ImpactRadius = request.ImpactRadius,
                TickInterval = request.TickInterval,
                RemainingHits = request.Penetration,
                RemainingReflections = request.Reflections,
                OrbitRadius = request.OrbitRadius,
                RepeatHitInterval = request.RepeatHitInterval,
                OrbitSlot = request.OrbitSlot,
                ProjectilePoolIndex = request.ProjectilePoolIndex,
                ImpactPoolIndex = request.ImpactPoolIndex,
                ImpactDuration = request.ImpactDuration,
                ImpactDamageDelay = request.ImpactDamageDelay
            });
#if ENABLE_DIAGNOSTICS_LOG
            if (_projectileDiagnosticLog.Includes(shotId))
                _projectileDiagnosticLog.Write(
                    $"[Projectile.Spawn] shot={shotId} frame={Time.frameCount} " +
                    $"origin={request.Origin:F3} direction={request.Direction:F3} " +
                    $"speed={request.Speed:F3} radius={request.ProjectileRadius:F3} " +
                    $"range={request.Range:F3} lifetime={request.Lifetime:F3}");
#endif
        }

        private void DealDamage(Entity target, float damage, Vector3 knockback)
        {
            var entity = CommandBuffer.Spawn();
            CommandBuffer.AddComponent(entity, new Damage
            {
                Value = damage,
                Target = target,
                Knockback = new Vector2(knockback.x, knockback.y)
            });
        }
    }
}
