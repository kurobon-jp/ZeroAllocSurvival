using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    /// <summary>The player is the only character with a Transform; update it directly.</summary>
    internal sealed class PlayerTransformSystem : BaseSystem, IInitializable, ITickable
    {
        private Entity _player;
        
        public PlayerTransformSystem(World world) : base(world) { }

        public void Initialize()
        {
            _player = World.Singleton<PlayerTag>();
        }

        public void Tick(float deltaTime)
        {
            if (_player.TryGet<PhysicsPosition>(out var position) && _player.TryGet<Link<Transform>>(out var transform))
                transform.Value.position = position.Value;
        }
    }
}
