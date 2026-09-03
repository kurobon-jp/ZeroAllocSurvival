using LitheEcs;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class CharacterSpatialQuerySystem :
        QueryActionSystem<PhysicsPosition, SpatialGroup, CharacterState>
    {
        private readonly CharacterSpatialHash _characterSpatialHash;

        public CharacterSpatialQuerySystem(World world, CharacterSpatialHash characterSpatialHash) : base(world)
        {
            _characterSpatialHash = characterSpatialHash;
        }

        protected override bool OnPreTick()
        {
            _characterSpatialHash.Clear();
            return true;
        }

        protected override void ForEach(in Entity entity, ref PhysicsPosition position, ref SpatialGroup group,
            ref CharacterState state)
        {
            if (state.Health <= 0f) return;
            _characterSpatialHash.Add(entity.Id, position.Value, group.Value);
        }
    }
}

