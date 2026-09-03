using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class ExperienceCollectSystem : QueryActionSystem<ExperienceCollected>
    {
        private Entity _player;

        public ExperienceCollectSystem(World world) : base(world)
        {
        }

        protected override void OnPostInitialize()
        {
            _player = World.Singleton<PlayerTag>();
        }

        protected override bool OnPreTick()
        {
            CommandBuffer.Playback();
            return true;
        }

        protected override void ForEach(in Entity entity, ref ExperienceCollected collected)
        {
            ref var progress = ref _player.Get<PlayerProgress>();
            var experienceLevel = _player.Get<PlayerUpgradeLevels>().ExperienceGain;
            var multiplier = 1f + experienceLevel * .1f;
            progress.Experience += Mathf.Max(0, Mathf.RoundToInt(collected.Value * multiplier));
            while (progress.Experience >= progress.RequiredExperience)
            {
                progress.Experience -= progress.RequiredExperience;
                progress.Level++;
                progress.RequiredExperience = Mathf.Max(1,
                    Mathf.CeilToInt(5f * Mathf.Pow(1.25f, progress.Level - 1)));

                var id = CommandBuffer.Spawn();
                CommandBuffer.AddComponent<LevelUp>(id);
            }

            CommandBuffer.Despawn(entity);
        }
    }

    internal sealed class ExperienceGaugeSystem : QueryActionSystem<PlayerProgress>
    {
        private readonly GaugePresenter _presenter;

        internal ExperienceGaugeSystem(World world, GaugePresenter presenter) : base(world)
        {
            _presenter = presenter;
        }

        protected override void ForEach(in Entity entity, ref PlayerProgress progress)
        {
            _presenter.SetProgress(progress.Experience, progress.RequiredExperience);
        }
    }
}

