using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Definitions;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class WeaponInitializeSystem : BaseSystem, IInitializable
    {
        private readonly WeaponDefinition[] _startingWeapons;
        private readonly WeaponRegistry _registry;

        public WeaponInitializeSystem(World world, WeaponDefinition[] startingWeapons,
            WeaponRegistry registry) : base(world)
        {
            _startingWeapons = startingWeapons;
            _registry = registry;
        }

        void IInitializable.Initialize()
        {
            var player = World.Singleton<PlayerTag>();
            var primaryAssigned = false;
            using (World.BeginStructuralBatch())
            {
                for (var i = 0; i < _startingWeapons.Length; i++)
                {
                    var definition = _startingWeapons[i];
                    if (definition == null) continue;
                    var definitionIndex = _registry.IndexOf(definition);
                    if (definitionIndex < 0) continue;
                    var isPrimary = !primaryAssigned && definition.MotionType != ProjectileMotionType.Orbit;
                    var weapon = _registry.Create(player, definitionIndex, isPrimary);
                    if (!isPrimary || !weapon.IsAlive) continue;
                    weapon.Add(default(PrimaryWeaponTag));
                    primaryAssigned = true;
                }
            }
        }
    }
    
    internal sealed class WeaponSystem : QueryActionSystem<WeaponRuntime>
    {
        private const float GoldenAngle = 137.50776f;

        public WeaponSystem(World world) : base(world) { }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override void ForEach(in Entity weaponEntity, ref WeaponRuntime weapon)
        {
            // A period without a target must not accumulate negative cooldown and turn into
            // catch-up shots when a new target appears.
            weapon.Cooldown = Mathf.Max(0f, weapon.Cooldown - DeltaTime);
            if (weapon.MotionType == ProjectileMotionType.Orbit)
            {
                UpdateOrbitingWeapon(weaponEntity, ref weapon);
                return;
            }
            var owner = weaponEntity.GetRelation<Owner>();
            var origin = owner.Get<PhysicsPosition>().Value;
            var primary = weaponEntity.Has<PrimaryWeaponTag>();
            Vector3 direction;
            var target = Vector3.zero;
            if (primary)
            {
                if (!owner.TryGet<PrimaryFireDirection>(out var fireDirection) ||
                    fireDirection.Value.sqrMagnitude <= .0001f) return;
                direction = new Vector3(fireDirection.Value.x, fireDirection.Value.y, 0f).normalized;
            }
            else
            {
                var targets = weaponEntity.GetRelations<Target>();
                if (targets.Length == 0 || !targets[0].IsAlive) return;
                target = targets[0].Get<PhysicsPosition>().Value;
                direction = target - origin;
                direction.z = 0f;
                if (direction.sqrMagnitude <= .0001f) return;
                direction.Normalize();
            }

            if (weaponEntity.TryGet<Link<Weapon>>(out var view))
            {
                var transform = view.Value.transform;
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
                if (weaponEntity.TryGet<WeaponViewState>(out var viewState))
                {
                    var scale = viewState.BaseScale;
                    if (direction.x < -.0001f) scale.y = -scale.y;
                    transform.localScale = scale;
                }
                var muzzle = view.Value.Muzzle;
                if (muzzle != null)
                {
                    origin = muzzle.position;
                    if (!primary)
                    {
                        direction = target - origin;
                        direction.z = 0f;
                        if (direction.sqrMagnitude <= .0001f) return;
                        direction.Normalize();
                    }
                }
            }

            if (weapon.Cooldown > 0f) return;

            weapon.Cooldown = Mathf.Max(.01f, weapon.Interval);
            var count = weapon.FirePattern == FirePattern.Spread ? Mathf.Max(1, weapon.ProjectileCount) : 1;

            if (weapon.FirePattern == FirePattern.Spread && weapon.DeliveryType == DeliveryType.Hitscan)
            {
                RecordAttack(weaponEntity, owner, weapon, origin, direction, count, weapon.Spread);
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var shotDirection = direction;
                if (count > 1)
                {
                    // A full-circle spread must not include both -180 and +180 because they
                    // represent the same direction. Divide the circle by the projectile count.
                    var angle = weapon.Spread >= 359.9f
                        ? weapon.SpreadPhase + i * (360f / count)
                        : Mathf.Lerp(-weapon.Spread * .5f, weapon.Spread * .5f,
                            i / (float)(count - 1));
                    shotDirection = Quaternion.AngleAxis(angle, Vector3.forward) * direction;
                }

                RecordAttack(weaponEntity, owner, weapon, origin, shotDirection, 1, 0f);
            }

            if (count > 1 && weapon.Spread >= 359.9f)
                weapon.SpreadPhase = Mathf.Repeat(weapon.SpreadPhase + GoldenAngle, 360f);
        }

        private void UpdateOrbitingWeapon(in Entity weaponEntity, ref WeaponRuntime weapon)
        {
            weapon.OrbitPhase = Mathf.Repeat(
                weapon.OrbitPhase + weapon.OrbitAngularSpeed * DeltaTime, 360f);
            if (weapon.ActiveProjectileCount > 0)
            {
                weapon.OrbitRemainingLifetime =
                    Mathf.Max(0f, weapon.OrbitRemainingLifetime - DeltaTime);
            }
            else
            {
                if (weapon.Cooldown > 0f) return;
                weapon.OrbitRemainingLifetime = Mathf.Max(.01f, weapon.ProjectileLifetime);
            }

            var desiredCount = Mathf.Max(1, weapon.ProjectileCount);
            if (weapon.ActiveProjectileCount >= desiredCount || weapon.OrbitRemainingLifetime <= 0f) return;
            var owner = weaponEntity.GetRelation<Owner>();
            var origin = owner.Get<PhysicsPosition>().Value;
            for (var slot = weapon.ActiveProjectileCount; slot < desiredCount; slot++)
            {
                var angle = weapon.OrbitPhase + slot * (360f / desiredCount);
                var direction = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.right;
                RecordAttack(weaponEntity, owner, weapon, origin, direction, 1, 0f, slot,
                    weapon.OrbitRemainingLifetime);
            }

            weapon.ActiveProjectileCount = desiredCount;
        }

        private void RecordAttack(Entity weaponEntity, Entity owner, in WeaponRuntime weapon,
            Vector3 origin, Vector3 direction,
            int projectileCount, float spread, int orbitSlot = 0,
            float lifetimeOverride = -1f)
        {
            var id = CommandBuffer.Spawn();
            CommandBuffer.AddComponent(id, new AttackRequest
            {
                Owner = owner,
                Weapon = weaponEntity,
                DeliveryType = weapon.DeliveryType,
                ImpactType = weapon.ImpactType,
                MotionType = weapon.MotionType,
                ProjectileHitsEnemies = weapon.ProjectileHitsEnemies,
                Origin = origin,
                Direction = direction,
                Damage = weapon.Damage,
                Knockback = weapon.Knockback,
                Speed = weapon.ProjectileSpeed,
                Lifetime = lifetimeOverride >= 0f ? lifetimeOverride : weapon.ProjectileLifetime,
                ProjectileAngularSpeed = weapon.ProjectileAngularSpeed,
                Range = weapon.Range,
                ProjectileRadius = weapon.ProjectileRadius,
                ImpactRadius = weapon.ImpactRadius,
                TickInterval = weapon.TickInterval,
                Penetration = Mathf.Max(1, weapon.Penetration),
                Reflections = Mathf.Max(0, weapon.Reflections),
                OrbitRadius = weapon.OrbitRadius,
                RepeatHitInterval = weapon.RepeatHitInterval,
                OrbitSlot = orbitSlot,
                ArcAngle = weapon.FirePattern == FirePattern.Arc ? weapon.Spread : 0f,
                ProjectileCount = projectileCount,
                Spread = spread,
                ProjectilePoolIndex = weapon.ProjectilePoolIndex,
                ImpactPoolIndex = weapon.ImpactPoolIndex,
                ImpactDuration = weapon.ImpactDuration,
                ImpactDamageDelay = weapon.ImpactDamageDelay
            });
        }
    }
}
