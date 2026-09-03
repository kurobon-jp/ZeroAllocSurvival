using System;
using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class ProjectileSystem : QueryActionSystem<AttackInstance, Link<Transform>>
    {
        private const int MaxTargets = 128;
        private readonly CharacterSpatialHash _spatialHash;
        private readonly ProjectilePoolRegistry _viewPools;
        private readonly ProjectileDiagnosticLog _diagnosticLog;
        private readonly ProjectileHitTracker _hitTracker = new();

        public ProjectileSystem(World world, CharacterSpatialHash spatialHash, ProjectilePoolRegistry viewPools,
            ProjectileDiagnosticLog diagnosticLog) : base(world)
        {
            _spatialHash = spatialHash;
            _viewPools = viewPools;
#if ENABLE_DIAGNOSTICS_LOG
            _diagnosticLog = diagnosticLog;
#endif
        }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override void ForEach(in Entity entity, ref AttackInstance attack, ref Link<Transform> link)
        {
            attack.RemainingLifetime -= DeltaTime;
            if (attack.MotionType == ProjectileMotionType.Stationary)
            {
                if (attack.RemainingLifetime <= 0f)
                {
                    Release(entity, ref attack, link.Value, entity.Id);
                    return;
                }
                TickStationary(ref attack);
                return;
            }
            UpdateHitCooldowns(ref attack);
            var orbiting = attack.MotionType == ProjectileMotionType.Orbit;
            var nextPosition = attack.Position;
            float distance;
            if (orbiting)
            {
                if (!attack.Owner.IsAlive || !attack.Weapon.IsAlive ||
                    !attack.Weapon.TryGet<WeaponRuntime>(out var weapon))
                {
                    Release(entity, ref attack, link.Value, entity.Id);
                    return;
                }

                var count = Mathf.Max(1, weapon.ProjectileCount);
                var angle = weapon.OrbitPhase + attack.OrbitSlot * (360f / count);
                var radians = angle * Mathf.Deg2Rad;
                var center = attack.Owner.Get<PhysicsPosition>().Value;
                nextPosition = center + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) *
                    attack.OrbitRadius;
                distance = Vector3.Distance(attack.Position, nextPosition);
                if (attack.MotionInitialized == 0)
                {
                    attack.MotionInitialized = 1;
                    attack.Position = nextPosition;
                    ApplyProjectileOrientation(link.Value, nextPosition,
                        new Vector3(-Mathf.Sin(radians), Mathf.Cos(radians), 0f));
                    return;
                }
            }
            else
            {
                // Keep a newly spawned projectile at the muzzle for its first rendered frame.
                // AttackRequestSystem and ProjectileSystem run in the same game update; moving
                // immediately here otherwise makes the projectile appear ahead of the flash.
                if (attack.MotionInitialized == 0)
                {
                    attack.MotionInitialized = 1;
                    link.Value.position = WithDepth(attack.Position, VisualDepth.Projectile);
                    return;
                }

                distance = Mathf.Min(attack.Speed * DeltaTime, Mathf.Max(0f, attack.Range - attack.Travelled));
                nextPosition = attack.Position + attack.Direction * distance;
            }

            attack.Travelled += distance;
            var rayId = entity.Id;
            var log = false;
#if ENABLE_DIAGNOSTICS_LOG
            log = _diagnosticLog.Includes(attack.DiagnosticShotId);
            if (log)
                _diagnosticLog.Write($"[Projectile.Step] shot={attack.DiagnosticShotId} frame={Time.frameCount} " +
                                     $"from={attack.Position:F3} to={nextPosition:F3} distance={distance:F4} " +
                                     $"travelled={attack.Travelled:F3}");
#endif
            var hit = FindSegmentHit(rayId, ref attack, nextPosition, log, out var hitPosition, out var reflected);
            var expired = attack.RemainingLifetime <= 0f || !orbiting && attack.Travelled >= attack.Range;
            if (hit || expired)
            {
#if ENABLE_DIAGNOSTICS_LOG
                if (log)
                    _diagnosticLog.Write($"[Projectile.End] shot={attack.DiagnosticShotId} frame={Time.frameCount} " +
                                         $"reason={(hit ? "hit" : attack.RemainingLifetime <= 0f ? "lifetime" : "range")} " +
                                         $"position={(hit ? hitPosition : nextPosition):F3}");
#endif
                Impact(attack, hit ? hitPosition : nextPosition);
                Release(entity, ref attack, link.Value, rayId);
                return;
            }

            if (reflected)
            {
                // Move just outside the contacted agent so the next segment starts on the
                // reflected side of its surface. The hit tracker provides an additional guard.
                attack.Position = hitPosition + attack.Direction * Mathf.Max(.001f, attack.ProjectileRadius * .05f);
                ApplyProjectileOrientation(link.Value, attack.Position, attack.Direction);
                return;
            }

            attack.Position = nextPosition;
            if (orbiting)
            {
                var tangent = nextPosition - link.Value.position;
                if (tangent.sqrMagnitude > .0001f)
                    ApplyProjectileOrientation(link.Value, nextPosition, tangent);
                else
                    link.Value.position = nextPosition;
                return;
            }

            var renderPosition = WithDepth(nextPosition, VisualDepth.Projectile);
            if (attack is { DeliveryType: DeliveryType.LobbedProjectile, Range: > .001f })
            {
                renderPosition.z -= Mathf.Sin(Mathf.Clamp01(attack.Travelled / attack.Range) * Mathf.PI) * 2f;
                link.Value.Rotate(0f, 0f, attack.ProjectileAngularSpeed * DeltaTime, Space.Self);
            }

            link.Value.position = renderPosition;
        }

        private bool FindSegmentHit(EntityId rayId, ref AttackInstance attack, Vector3 end, bool log,
            out Vector3 hitPosition, out bool reflected)
        {
            Span<EntityId> hitCandidates = stackalloc EntityId[MaxTargets];
            var segment = end - attack.Position;
            var length = segment.magnitude;
            hitPosition = end;
            reflected = false;
            if (attack.ProjectileHitsEnemies == 0) return false;
            if (length <= .0001f) return false;
            var direction = segment / length;
            var hitRadius = Mathf.Max(.2f, attack.ProjectileRadius + .5f);
            var count = _spatialHash.FindAlongRayIds(attack.Position, direction, length, hitRadius,
                attack.Owner.Id, CharacterSpatialHash.EnemyGroupId, hitCandidates);
#if ENABLE_DIAGNOSTICS_LOG
            if (log)
                _diagnosticLog.Write(
                    $"[Projectile.Candidates] shot={attack.DiagnosticShotId} frame={Time.frameCount} " +
                    $"count={count} length={length:F4} hitRadius={hitRadius:F3}");
#endif
            for (var i = 0; i < count; i++)
            {
                var targetId = hitCandidates[i];
                var orbiting = attack.MotionType == ProjectileMotionType.Orbit;
                if (orbiting && IsOnHitCooldown(ref attack, targetId))
                {
#if ENABLE_DIAGNOSTICS_LOG
                    if (log)
                        _diagnosticLog.Write(
                            $"[Projectile.Skip] shot={attack.DiagnosticShotId} target={targetId} reason=hit-cooldown");
#endif
                    continue;
                }

                if (!orbiting && _hitTracker.WasLastTarget(rayId, targetId))
                {
#if ENABLE_DIAGNOSTICS_LOG
                    if (log)
                        _diagnosticLog.Write(
                            $"[Projectile.Skip] shot={attack.DiagnosticShotId} target={targetId} reason=last-target");
#endif
                    continue;
                }

                if (!World.TryGetEntity(targetId, out var target))
                {
#if ENABLE_DIAGNOSTICS_LOG
                    if (log)
                        _diagnosticLog.Write(
                            $"[Projectile.Skip] shot={attack.DiagnosticShotId} target={targetId} reason=not-alive");
#endif
                    continue;
                }

                // Pending damage is resolved later in the frame. Skipping a target that is only
                // scheduled to die makes this projectile visibly pass through a still-rendered
                // agent, so collision must remain authoritative here even for overkill damage.
                var targetPosition = target.Get<PhysicsPosition>().Value;
                var relative = targetPosition - attack.Position;
                relative.z = 0f;
                var forward = Vector3.Dot(relative, direction);
#if ENABLE_DIAGNOSTICS_LOG
                if (log)
                {
                    var lateral = Mathf.Abs(relative.x * direction.y - relative.y * direction.x);
                    var health = target.TryGet<CharacterState>(out var targetState)
                        ? targetState.Health
                        : float.NaN;
                    _diagnosticLog.Write($"[Projectile.Candidate] shot={attack.DiagnosticShotId} target={targetId} " +
                                         $"targetPosition={targetPosition:F3} forward={forward:F4} lateral={lateral:F4} " +
                                         $"health={health:F3}");
                }
#endif
                if (attack.ImpactType == ImpactType.DirectDamage)
                {
                    var knockbackOrigin = attack.Origin;
                    if (orbiting && attack.Owner.IsAlive &&
                        attack.Owner.TryGet<PhysicsPosition>(out var ownerPosition))
                        knockbackOrigin = ownerPosition.Value;
                    var knockbackDirection = targetPosition - knockbackOrigin;
                    knockbackDirection.z = 0f;
                    if (knockbackDirection.sqrMagnitude > .0001f)
                        knockbackDirection.Normalize();
                    else
                        knockbackDirection = attack.Direction;
                    DealDamage(target, attack.Damage, knockbackDirection * attack.Knockback);
#if ENABLE_DIAGNOSTICS_LOG
                    if (log)
                        _diagnosticLog.Write($"[Projectile.Damage] shot={attack.DiagnosticShotId} target={targetId} " +
                                             $"damage={attack.Damage:F3}");
#endif
                }

                var contactDistance = Mathf.Clamp(forward - hitRadius, 0f, length);
                hitPosition = attack.Position + direction * contactDistance;
                if (orbiting)
                {
                    RecordHitCooldown(ref attack, targetId,
                        Mathf.Max(.01f, attack.RepeatHitInterval));
                    continue;
                }

                _hitTracker.Record(rayId, targetId);
                if (attack.RemainingReflections > 0)
                {
                    var normal = hitPosition - targetPosition;
                    normal.z = 0f;
                    if (normal.sqrMagnitude <= .0001f) normal = -direction;
                    else normal.Normalize();
                    attack.Direction = Vector3.Reflect(direction, normal).normalized;
                    attack.RemainingReflections--;
                    reflected = true;
                    return false;
                }

                attack.RemainingHits--;
                if (attack.RemainingHits > 0) continue;
                return true;
            }

            return false;
        }

        private void Release(in Entity entity, ref AttackInstance attack, Transform view, EntityId rayId)
        {
            if (attack.MotionType == ProjectileMotionType.Orbit && attack.Weapon.IsAlive &&
                attack.Weapon.TryGetRef<WeaponRuntime>(out var weapon))
            {
                weapon.Value.ActiveProjectileCount = Mathf.Max(0, weapon.Value.ActiveProjectileCount - 1);
                if (weapon.Value.ActiveProjectileCount == 0)
                    weapon.Value.Cooldown = Mathf.Max(.01f, weapon.Value.Interval);
            }

            _viewPools.Release(attack.ProjectilePoolIndex, view);
            _hitTracker.Remove(rayId);
            CommandBuffer.Despawn(entity);
        }

        private static void UpdateHitCooldowns(ref AttackInstance attack)
        {
            for (var i = attack.HitCooldowns.Length - 1; i >= 0; i--)
            {
                var cooldown = attack.HitCooldowns[i];
                cooldown.Remaining -= Time.deltaTime;
                if (cooldown.Remaining <= 0f)
                    attack.HitCooldowns.RemoveAtSwapBack(i);
                else
                    attack.HitCooldowns[i] = cooldown;
            }
        }

        private static bool IsOnHitCooldown(ref AttackInstance attack, EntityId target)
        {
            for (var i = 0; i < attack.HitCooldowns.Length; i++)
                if (attack.HitCooldowns[i].Target == target)
                    return true;
            return false;
        }

        private static void RecordHitCooldown(ref AttackInstance attack, EntityId target, float duration)
        {
            var cooldown = new ProjectileHitCooldown { Target = target, Remaining = duration };
            if (attack.HitCooldowns.Length < attack.HitCooldowns.Capacity)
            {
                attack.HitCooldowns.Add(cooldown);
                return;
            }

            var shortestIndex = 0;
            for (var i = 1; i < attack.HitCooldowns.Length; i++)
                if (attack.HitCooldowns[i].Remaining < attack.HitCooldowns[shortestIndex].Remaining)
                    shortestIndex = i;
            attack.HitCooldowns[shortestIndex] = cooldown;
        }

        private void TickStationary(ref AttackInstance attack)
        {
            if (attack.ImpactDamageDelay > 0f)
            {
                attack.ImpactDamageDelay -= DeltaTime;
                if (attack.ImpactDamageDelay > 0f) return;
            }

            if (attack.TickInterval <= 0f)
            {
                if (attack.ImpactApplied == 0) ApplySingleAreaImpact(ref attack);
                return;
            }

            for (var i = 0; i < attack.HitCooldowns.Length; i++)
            {
                var contact = attack.HitCooldowns[i];
                contact.Remaining -= DeltaTime;
                contact.Seen = 0;
                attack.HitCooldowns[i] = contact;
            }

            Span<EntityId> targets = stackalloc EntityId[MaxTargets];
            var count = _spatialHash.FindNearestIds(attack.Position, attack.ImpactRadius, attack.Owner.Id,
                CharacterSpatialHash.EnemyGroupId, targets);
            for (var i = 0; i < count; i++)
            {
                var targetId = targets[i];
                if (!World.TryGetEntity(targetId, out var target)) continue;
                var contactIndex = FindContact(ref attack, targetId);
                if (contactIndex < 0)
                {
                    DealDamage(target, attack.Damage, Vector3.zero);
                    RecordStationaryContact(ref attack, targetId);
                    continue;
                }

                var contact = attack.HitCooldowns[contactIndex];
                contact.Seen = 1;
                if (contact.Remaining <= 0f)
                {
                    DealDamage(target, attack.Damage, Vector3.zero);
                    contact.Remaining = Mathf.Max(.01f, attack.TickInterval);
                }
                attack.HitCooldowns[contactIndex] = contact;
            }

            for (var i = attack.HitCooldowns.Length - 1; i >= 0; i--)
                if (attack.HitCooldowns[i].Seen == 0) attack.HitCooldowns.RemoveAtSwapBack(i);
        }

        private void ApplySingleAreaImpact(ref AttackInstance attack)
        {
            attack.ImpactApplied = 1;
            Span<EntityId> targets = stackalloc EntityId[MaxTargets];
            var count = _spatialHash.FindNearestIds(attack.Position, attack.ImpactRadius, attack.Owner.Id,
                CharacterSpatialHash.EnemyGroupId, targets);
            for (var i = 0; i < count; i++)
            {
                if (!World.TryGetEntity(targets[i], out var target)) continue;
                var targetPosition = target.Get<PhysicsPosition>().Value;
                var distance = Vector3.Distance(attack.Position, targetPosition);
                var scale = Mathf.Lerp(1f, .35f,
                    Mathf.Clamp01(distance / Mathf.Max(.001f, attack.ImpactRadius)));
                var direction = targetPosition - attack.Position;
                direction.z = 0f;
                if (direction.sqrMagnitude > .0001f) direction.Normalize();
                DealDamage(target, attack.Damage * scale, direction * (attack.Knockback * scale));
            }
        }

        private static int FindContact(ref AttackInstance attack, EntityId target)
        {
            for (var i = 0; i < attack.HitCooldowns.Length; i++)
                if (attack.HitCooldowns[i].Target == target) return i;
            return -1;
        }

        private static void RecordStationaryContact(ref AttackInstance attack, EntityId target)
        {
            if (attack.HitCooldowns.Length >= attack.HitCooldowns.Capacity) return;
            attack.HitCooldowns.Add(new ProjectileHitCooldown
            {
                Target = target,
                Remaining = Mathf.Max(.01f, attack.TickInterval),
                Seen = 1
            });
        }

        private static Quaternion ProjectileRotation(Vector3 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        private static void ApplyProjectileOrientation(Transform view, Vector3 position, Vector3 direction)
        {
            position.z = VisualDepth.Projectile;
            view.SetPositionAndRotation(position, ProjectileRotation(direction));
            var scale = view.localScale;
            scale.y = Mathf.Abs(scale.y) * (direction.x < -.0001f ? -1f : 1f);
            view.localScale = scale;
        }

        private void Impact(in AttackInstance attack, Vector3 position)
        {
            if (attack.ImpactType == ImpactType.Area)
            {
                SpawnStationaryImpact(attack, position);
                return;
            }

            if (attack.ImpactPoolIndex >= 0)
            {
                var renderPosition = WithDepth(position, VisualDepth.Explosion);
                var view = _viewPools.Get(attack.ImpactPoolIndex, renderPosition, Quaternion.identity);
                var impactDiameter = Mathf.Max(.01f, attack.ImpactRadius * 2f);
                view.localScale = new Vector3(impactDiameter, impactDiameter, 1f);
                CommandBuffer.Spawn(Link.With(view), new ImpactVisual
                {
                    PoolIndex = attack.ImpactPoolIndex,
                    RemainingDuration = Mathf.Max(.01f, attack.ImpactDuration)
                });
            }
        }

        private void SpawnStationaryImpact(in AttackInstance source, Vector3 position)
        {
            if (source.ImpactPoolIndex < 0) return;
            var renderPosition = WithDepth(position, VisualDepth.Explosion);
            var view = _viewPools.Get(source.ImpactPoolIndex, renderPosition, Quaternion.identity);
            var diameter = Mathf.Max(.01f, source.ImpactRadius * 2f);
            view.localScale = new Vector3(diameter, diameter, 1f);
            var id = CommandBuffer.Spawn();
            CommandBuffer.AddComponent(id, Link.With(view));
            CommandBuffer.AddComponent(id, new AttackInstance
            {
                Owner = source.Owner,
                Weapon = source.Weapon,
                DeliveryType = DeliveryType.Projectile,
                ImpactType = ImpactType.DirectDamage,
                MotionType = ProjectileMotionType.Stationary,
                ProjectileHitsEnemies = 1,
                Position = position,
                Damage = source.Damage,
                RemainingLifetime = Mathf.Max(.01f, source.ImpactDuration),
                ImpactRadius = source.ImpactRadius,
                TickInterval = Mathf.Max(0f, source.TickInterval),
                Knockback = source.Knockback,
                ProjectilePoolIndex = source.ImpactPoolIndex,
                ImpactPoolIndex = -1,
                ImpactDamageDelay = Mathf.Max(0f, source.ImpactDamageDelay),
                MotionInitialized = 1
            });
        }

        private static Vector3 WithDepth(Vector3 position, float depth)
        {
            position.z = depth;
            return position;
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

    /// <summary>Updates and releases pooled impact visuals for every weapon type.</summary>
    internal sealed class ImpactVisualSystem : QueryActionSystem<ImpactVisual, Link<Transform>>
    {
        private readonly ProjectilePoolRegistry _pools;

        internal ImpactVisualSystem(World world, ProjectilePoolRegistry pools) : base(world) => _pools = pools;

        protected override void ForEach(in Entity entity, ref ImpactVisual effect, ref Link<Transform> link)
        {
            effect.RemainingDuration -= DeltaTime;
            if (effect.RemainingDuration <= 0f)
            {
                _pools.Release(effect.PoolIndex, link.Value);
                CommandBuffer.Despawn(entity);
            }
        }

        protected override void OnPostTick() => CommandBuffer.Playback();
    }
}
