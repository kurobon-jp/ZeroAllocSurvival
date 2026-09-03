using System;
using System.Collections.Generic;
using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class DeathSystem : QueryActionSystem<Dead>, IDisposable
    {
        private readonly CharacterSlotRegistry _slots;
        private readonly GameOverPanelPresenter _gameOverPanel;
        private readonly List<Entity> _drops;
        private readonly int _dropCapacity;

        private int _pendingDropCount;
        private EntityCollector _dropCollector;

        public DeathSystem(World world, CharacterSlotRegistry slots, GameOverPanelPresenter gameOverPanel,
            int dropCapacity) : base(world)
        {
            _slots = slots;
            _gameOverPanel = gameOverPanel;
            _drops = new List<Entity>(dropCapacity);
            _dropCapacity = dropCapacity;
        }

        protected override void OnPostInitialize()
        {
            _dropCollector = World
                .Observe<ExperienceDrop>(ComponentEvent.KeyAdded | ComponentEvent.KeyRemoved)
                .EnsureCapacity(_dropCapacity);
        }

        protected override bool OnPreTick()
        {
            foreach (var entity in _dropCollector)
            {
                if (!entity.IsAlive)
                {
                    _drops.Remove(entity);
                }
            }

            _dropCollector.Clear();
            return true;
        }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
            foreach (var entity in _dropCollector)
            {
                if (entity.IsAlive)
                {
                    _drops.Add(entity);
                }
            }

            _dropCollector.Clear();
            _pendingDropCount = 0;
        }

        protected override void ForEach(in Entity entity, ref Dead dead)
        {
            if (dead.Initialized != 0)
            {
                dead.RemainingFade -= DeltaTime;
                if (dead.RemainingFade > 0f) return;
                FinishDeath(entity);
                return;
            }

            dead.Initialized = 1;
            var animation = entity.Get<CharacterAnimationState>();
            dead.FadeDuration = animation.DeadCount /
                                Mathf.Max(.01f, animation.DeadFps * Mathf.Max(.01f, animation.PlaybackSpeed));
            dead.RemainingFade = dead.FadeDuration;
            CommandBuffer.RemoveComponent<CrowdAgent>(entity);
            if (entity.Has<SpatialGroup>())
                CommandBuffer.RemoveComponent<SpatialGroup>(entity);

            if (entity.TryGet<ExperienceReward>(out var reward))
            {
                var position = entity.Get<PhysicsPosition>().Value;
                var value = Mathf.Max(1, reward.Value);

                if (_drops.Count + _pendingDropCount >= _dropCapacity)
                {
                    CommandBuffer.Despawn(_drops[0]);
                    _drops.RemoveAt(0);
                }

                var drop = CommandBuffer.Spawn();
                CommandBuffer.AddComponent(drop, new ExperienceDrop
                {
                    Position = position,
                    Value = value
                });

                _pendingDropCount++;
            }
        }

        private void FinishDeath(in Entity entity)
        {
            if (entity.TryGet<CharacterSlot>(out var index))
                _slots.Release(index.Value);

            if (entity.Has<PlayerTag>())
            {
                var pause = CommandBuffer.Spawn();
                CommandBuffer.AddComponent(pause, default(GamePause));
                CommandBuffer.RemoveComponent<Dead>(entity);
                _gameOverPanel.SetVisible(true);
                return;
            }

            foreach (var target in World.GetEntitiesWithTarget<Owner>(entity))
                CommandBuffer.Despawn(target);

            CommandBuffer.Despawn(entity);
        }

        public void Dispose()
        {
            _dropCollector?.Dispose();
        }
    }
}