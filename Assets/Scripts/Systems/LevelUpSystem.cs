using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    public sealed class LevelUpSystem : QueryActionSystem<LevelUp>
    {
        private const byte MaximumUpgradeLevel = 5;
        private readonly UpgradePanelPresenter _presenter;
        private readonly WeaponRegistry _weapons;
        private readonly UpgradeChoice[] _eligible;
        private Entity _player;

        internal LevelUpSystem(World world, UpgradePanelPresenter presenter, WeaponRegistry weapons) : base(world)
        {
            _presenter = presenter;
            _weapons = weapons;
            _eligible = new UpgradeChoice[weapons.Count + 5];
        }

        protected override void OnPostInitialize()
        {
            _player = World.Singleton<PlayerTag>();
        }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override bool OnPreTick()
        {
            if (_presenter == null || _presenter.IsVisible) return false;
            foreach (ref var pending in World.Query<LevelUp>())
                if (pending.ChoicesGenerated != 0)
                    return false;
            return true;
        }

        protected override void ForEach(in Entity entity, ref LevelUp levelUp)
        {
            if (levelUp.ChoicesGenerated != 0 || _presenter == null || _presenter.IsVisible) return;
            var count = BuildEligibleChoices();
            if (count == 0)
            {
                CommandBuffer.Despawn(entity);
                return;
            }

            ShuffleFirst(count, Mathf.Min(3, count));
            levelUp.First = _eligible[0];
            levelUp.Second = _eligible[Mathf.Min(1, count - 1)];
            levelUp.Third = _eligible[Mathf.Min(2, count - 1)];
            levelUp.ChoiceCount = (byte)Mathf.Min(3, count);
            levelUp.ChoicesGenerated = 1;
            _presenter.Show(entity, _player.Get<PlayerProgress>().Level,
                levelUp.First, levelUp.Second, levelUp.Third, levelUp.ChoiceCount);
            CommandBuffer.AddComponent<GamePause>(entity);
        }

        private int BuildEligibleChoices()
        {
            var count = 0;
            for (var definitionIndex = 0; definitionIndex < _weapons.Count; definitionIndex++)
            {
                if (_weapons.Definition(definitionIndex) == null) continue;
                var found = false;
                foreach (ref var weapon in World.Query<WeaponRuntime>())
                {
                    if (weapon.DefinitionIndex != definitionIndex) continue;
                    found = true;
                    if (weapon.Level < MaximumUpgradeLevel)
                        _eligible[count++] = new UpgradeChoice
                        {
                            Kind = UpgradeKind.WeaponLevel,
                            Index = definitionIndex,
                            NextLevel = (byte)(weapon.Level + 1)
                        };
                    break;
                }

                if (!found)
                    _eligible[count++] = new UpgradeChoice
                    {
                        Kind = UpgradeKind.UnlockWeapon,
                        Index = definitionIndex,
                        NextLevel = 1
                    };
            }

            var levels = _player.Get<PlayerUpgradeLevels>();
            AddStat(ref count, PlayerStatKind.MoveSpeed, levels.MoveSpeed);
            AddStat(ref count, PlayerStatKind.WeaponInterval, levels.WeaponInterval);
            AddStat(ref count, PlayerStatKind.MaxHealth, levels.MaxHealth);
            AddStat(ref count, PlayerStatKind.AttackPower, levels.AttackPower);
            AddStat(ref count, PlayerStatKind.ExperienceGain, levels.ExperienceGain);
            return count;
        }

        private void AddStat(ref int count, PlayerStatKind kind, byte level)
        {
            if (level >= MaximumUpgradeLevel) return;
            _eligible[count++] = new UpgradeChoice
            {
                Kind = UpgradeKind.PlayerStat,
                Index = (int)kind,
                NextLevel = (byte)(level + 1)
            };
        }

        private void ShuffleFirst(int count, int take)
        {
            for (var i = 0; i < take; i++)
            {
                var other = Random.Range(i, count);
                (_eligible[i], _eligible[other]) = (_eligible[other], _eligible[i]);
            }
        }
    }
}