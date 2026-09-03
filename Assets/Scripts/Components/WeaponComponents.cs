using LitheEcs;
using Unity.Collections;
using UnityEngine;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Components
{
    internal enum FirePattern : byte { Single, Spread, Arc }
    internal enum DeliveryType : byte { Instant, Hitscan, Projectile, LobbedProjectile }
    internal enum ImpactType : byte { DirectDamage, Area }
    internal enum ProjectileMotionType : byte { Linear, Orbit, Stationary }

    internal struct PrimaryWeaponTag
    {
    }

    internal struct WeaponViewState
    {
        public Vector3 BaseScale;
    }

    internal struct WeaponRuntime
    {
        public int DefinitionIndex;
        public byte Level;
        public int BaseProjectileCount;
        public float BaseInterval;
        public float BaseDamage;
        public FirePattern FirePattern;
        public DeliveryType DeliveryType;
        public ImpactType ImpactType;
        public ProjectileMotionType MotionType;
        public byte ProjectileHitsEnemies;
        public float Damage;
        public float Knockback;
        public float Interval;
        public float Range;
        public float Cooldown;
        public int ProjectileCount;
        public float Spread;
        public float SpreadPhase;
        public float ProjectileSpeed;
        public float ProjectileLifetime;
        public float ProjectileAngularSpeed;
        public float ProjectileRadius;
        public float ImpactRadius;
        public float TickInterval;
        public int Penetration;
        public int Reflections;
        public float OrbitRadius;
        public float OrbitAngularSpeed;
        public float RepeatHitInterval;
        public float OrbitPhase;
        public float OrbitRemainingLifetime;
        public int ActiveProjectileCount;
        public int ProjectilePoolIndex;
        public int ImpactPoolIndex;
        public float ImpactDuration;
        public float ImpactDamageDelay;
    }

    internal struct AttackRequest
    {
        public Entity Owner;
        public Entity Weapon;
        public DeliveryType DeliveryType;
        public ImpactType ImpactType;
        public ProjectileMotionType MotionType;
        public byte ProjectileHitsEnemies;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Damage;
        public float Knockback;
        public float Speed;
        public float Lifetime;
        public float ProjectileAngularSpeed;
        public float Range;
        public float ProjectileRadius;
        public float ImpactRadius;
        public float TickInterval;
        public int Penetration;
        public int Reflections;
        public float OrbitRadius;
        public float RepeatHitInterval;
        public int OrbitSlot;
        public int ProjectilePoolIndex;
        public int ImpactPoolIndex;
        public float ImpactDuration;
        public float ImpactDamageDelay;
        public float ArcAngle;
        public int ProjectileCount;
        public float Spread;
    }

    internal struct AttackInstance
    {
        public int DiagnosticShotId;
        public Entity Owner;
        public Entity Weapon;
        public DeliveryType DeliveryType;
        public ImpactType ImpactType;
        public ProjectileMotionType MotionType;
        public byte ProjectileHitsEnemies;
        public Vector3 Origin;
        public Vector3 Position;
        public Vector3 Direction;
        public float Damage;
        public float Knockback;
        public float Speed;
        public float RemainingLifetime;
        public float ProjectileAngularSpeed;
        public float Range;
        public float Travelled;
        public float ProjectileRadius;
        public float ImpactRadius;
        public float TickInterval;
        public int RemainingHits;
        public int RemainingReflections;
        public float OrbitRadius;
        public float RepeatHitInterval;
        public FixedList4096Bytes<ProjectileHitCooldown> HitCooldowns;
        public int OrbitSlot;
        public byte MotionInitialized;
        public int ProjectilePoolIndex;
        public int ImpactPoolIndex;
        public float ImpactDuration;
        public float ImpactDamageDelay;
        public byte ImpactApplied;
    }

    internal struct ProjectileHitCooldown
    {
        public EntityId Target;
        public float Remaining;
        public byte Seen;
    }

    internal struct ImpactVisual
    {
        public int PoolIndex;
        public float RemainingDuration;
    }
}
