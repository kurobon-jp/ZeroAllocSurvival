using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;

namespace ZeroAllocSurvival.Definitions
{
    [CreateAssetMenu(menuName = "Zero Alloc Survival/Weapon", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private Weapon weaponPrefab;
        [SerializeField] private Transform bulletPrefab;
        [SerializeField, Min(0)] private int bulletPrewarmCount = 64;

        [SerializeField] private Transform impactPrefab;
        [SerializeField, Min(0)] private int impactPrewarmCount = 16;
        [SerializeField, Min(0f)] private float impactRadius = .5f;
        [SerializeField, Min(.01f)] private float impactDuration = .25f;
        [SerializeField, Min(0f)] private float impactDamageDelay;
        [SerializeField] private FirePattern firePattern;
        [SerializeField] private DeliveryType deliveryType;
        [SerializeField] private ImpactType impactType;
        [SerializeField] private ProjectileMotionType motionType;
        [SerializeField] private bool projectileHitsEnemies = true;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float knockback;
        [SerializeField, Min(.01f)] private float interval = .5f;
        [SerializeField, Min(.01f)] private float range = 20f;
        [SerializeField, Min(1)] private int projectileCount = 1;
        [SerializeField, Range(0f, 360f)] private float spread;
        [SerializeField, Min(.01f)] private float projectileSpeed = 20f;
        [SerializeField, Min(.01f)] private float projectileLifetime = 2f;
        [SerializeField] private float projectileAngularSpeed;
        [SerializeField, Min(0f)] private float projectileRadius = .1f;
        [SerializeField, Min(0f)] private float tickInterval = .5f;
        [SerializeField, Min(1)] private int penetration = 1;
        [SerializeField, Min(0)] private int reflections;
        [SerializeField, Min(0f)] private float orbitRadius = 3f;
        [SerializeField] private float orbitAngularSpeed = 180f;
        [SerializeField, Min(.01f)] private float repeatHitInterval = .25f;

        public Weapon WeaponPrefab => weaponPrefab;
        public Transform BulletPrefab => bulletPrefab;
        public int BulletPrewarmCount => bulletPrewarmCount;
        public Transform ImpactPrefab => impactPrefab;
        public int ImpactPrewarmCount => impactPrewarmCount;
        internal ProjectileMotionType MotionType => motionType;
        public string DisplayName => name;

        internal WeaponRuntime CreateRuntime(int projectilePoolIndex, int impactPoolIndex) => new()
        {
            FirePattern = projectileCount > 1 && firePattern == FirePattern.Single
                ? FirePattern.Spread
                : firePattern,
            Level = 1,
            BaseProjectileCount = projectileCount,
            BaseInterval = interval,
            BaseDamage = damage,
            DeliveryType = deliveryType,
            ImpactType = impactType,
            MotionType = motionType,
            ProjectileHitsEnemies = projectileHitsEnemies ? (byte)1 : (byte)0,
            Damage = damage,
            Knockback = knockback,
            Interval = interval,
            Range = range,
            ProjectileCount = projectileCount,
            Spread = spread,
            ProjectileSpeed = projectileSpeed,
            ProjectileLifetime = projectileLifetime,
            ProjectileAngularSpeed = projectileAngularSpeed,
            ProjectileRadius = projectileRadius,
            ImpactRadius = impactRadius,
            TickInterval = tickInterval,
            Penetration = penetration,
            Reflections = reflections,
            OrbitRadius = orbitRadius,
            OrbitAngularSpeed = orbitAngularSpeed,
            RepeatHitInterval = repeatHitInterval,
            ProjectilePoolIndex = projectilePoolIndex,
            ImpactPoolIndex = impactPoolIndex,
            ImpactDuration = impactDuration,
            ImpactDamageDelay = impactDamageDelay
        };
    }
}
