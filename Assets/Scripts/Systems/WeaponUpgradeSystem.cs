using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class WeaponUpgradeSystem : QueryActionSystem<UpgradeSelected>
    {
        private const int MaxLevel = 5;

        private readonly WeaponRegistry _weapons;
        private readonly GaugePresenter _hpGauge;
        private Entity _player;

        public WeaponUpgradeSystem(World world, WeaponRegistry weapons, GaugePresenter hpGauge) : base(world)
        {
            _weapons = weapons;
            _hpGauge = hpGauge;
        }

        protected override void OnPostInitialize() => _player = World.Singleton<PlayerTag>();
        protected override void OnPostTick() => CommandBuffer.Playback();

        protected override void ForEach(in Entity entity, ref UpgradeSelected selected)
        {
            switch (selected.Choice.Kind)
            {
                case UpgradeKind.UnlockWeapon:
                    _weapons.Create(_player, selected.Choice.Index);
                    break;
                case UpgradeKind.WeaponLevel:
                    UpgradeWeapon(selected.Choice.Index);
                    break;
                case UpgradeKind.PlayerStat:
                    UpgradeStat((PlayerStatKind)selected.Choice.Index);
                    break;
            }

            CommandBuffer.Despawn(entity);
        }

        private void UpgradeWeapon(int definitionIndex)
        {
            foreach (var weaponEntity in World.GetEntitiesWithTarget<Owner>(_player))
            {
                if (!weaponEntity.TryGetRef<WeaponRuntime>(out var weaponReference)) continue;
                ref var weapon = ref weaponReference.Value;
                if (weapon.DefinitionIndex != definitionIndex || weapon.Level >= 5) continue;
                weapon.Level++;
                weapon.ProjectileCount = weapon.BaseProjectileCount + weapon.Level - 1;
                if (weapon.MotionType == ProjectileMotionType.Orbit)
                {
                    // An orbit projectile added near the end of the current cycle would inherit
                    // only that cycle's tiny remaining lifetime and disappear immediately.
                    // Start the upgraded formation as one cycle and keep its existing members in
                    // sync with the newly added projectile.
                    weapon.OrbitRemainingLifetime = Mathf.Max(.01f, weapon.ProjectileLifetime);
                    foreach (ref var attack in World.Query<AttackInstance>())
                    {
                        if (attack.MotionType != ProjectileMotionType.Orbit ||
                            attack.Weapon.Id != weaponEntity.Id) continue;
                        attack.RemainingLifetime = weapon.OrbitRemainingLifetime;
                    }
                }

                // A weapon-level upgrade grants an immediate shot with the upgraded values.
                // Reset only this weapon; the global interval upgrade keeps existing cooldowns.
                weapon.Cooldown = 0f;
                if (weapon.ProjectileCount > 1 && weapon.FirePattern == FirePattern.Single)
                    weapon.FirePattern = FirePattern.Spread;
                return;
            }
        }

        private void UpgradeStat(PlayerStatKind kind)
        {
            ref var levels = ref _player.Get<PlayerUpgradeLevels>();
            ref var state = ref _player.Get<CharacterState>();
            switch (kind)
            {
                case PlayerStatKind.MoveSpeed when levels.MoveSpeed < MaxLevel:
                    levels.MoveSpeed++;
                    state.MoveSpeed = levels.BaseMoveSpeed * (1f + levels.MoveSpeed * .1f);
                    break;
                case PlayerStatKind.WeaponInterval when levels.WeaponInterval < MaxLevel:
                    levels.WeaponInterval++;
                    foreach (var entity in World.GetEntitiesWithTarget<Owner>(_player))
                    {
                        if (entity.TryGetRef(out Ref<WeaponRuntime> weapon))
                            weapon.Value.Interval = Mathf.Max(.05f,
                                weapon.Value.BaseInterval * (1f - levels.WeaponInterval * .1f));
                    }

                    break;
                case PlayerStatKind.MaxHealth when levels.MaxHealth < MaxLevel:
                    levels.MaxHealth++;
                    state.MaxHealth = levels.BaseMaxHealth * (1f + levels.MaxHealth * .2f);
                    state.Health = state.MaxHealth;
                    _hpGauge.SetProgress(state.Health, state.MaxHealth);
                    break;
                case PlayerStatKind.AttackPower when levels.AttackPower < MaxLevel:
                    levels.AttackPower++;
                    foreach (var entity in World.GetEntitiesWithTarget<Owner>(_player))
                    {
                        if (entity.TryGetRef(out Ref<WeaponRuntime> weapon))
                            weapon.Value.Damage = weapon.Value.BaseDamage * (1f + levels.AttackPower * .1f);
                    }

                    foreach (ref var weapon in World.Query<WeaponRuntime>())
                        weapon.Damage = weapon.BaseDamage * (1f + levels.AttackPower * .1f);
                    break;
                case PlayerStatKind.ExperienceGain when levels.ExperienceGain < MaxLevel:
                    levels.ExperienceGain++;
                    break;
            }
        }
    }
}