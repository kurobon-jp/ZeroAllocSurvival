using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class ExperiencePickupSystem : QueryActionSystem<ExperienceDrop>
    {
        private const float AttractionRadius = 4f;
        private const float CollectionRadius = .65f;
        private const float Acceleration = 30f;
        private const float MaximumSpeed = 18f;

        private Entity _player;

        public ExperiencePickupSystem(World world) : base(world)
        {
        }

        protected override void OnPostInitialize()
        {
            _player = World.Singleton<PlayerTag>();
        }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override void ForEach(in Entity entity, ref ExperienceDrop pickup)
        {
            if (!_player.TryGet<PhysicsPosition>(out var playerPosition)) return;
            var delta = playerPosition.Value - pickup.Position;
            delta.z = 0f;
            var sqrDistance = delta.sqrMagnitude;
            if (sqrDistance <= CollectionRadius * CollectionRadius)
            {
                var collected = CommandBuffer.Spawn();
                CommandBuffer.AddComponent(collected, new ExperienceCollected { Value = pickup.Value });
                CommandBuffer.Despawn(entity);
                return;
            }

            if (sqrDistance <= AttractionRadius * AttractionRadius)
            {
                pickup.Speed = Mathf.Min(MaximumSpeed, pickup.Speed + Acceleration * DeltaTime);
                pickup.Position = Vector3.MoveTowards(
                    pickup.Position, playerPosition.Value, pickup.Speed * DeltaTime);
            }
        }
    }
}