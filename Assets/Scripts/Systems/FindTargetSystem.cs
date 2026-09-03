using System;
using LitheEcs;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Services;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class FindTargetSystem : QueryActionSystem<WeaponRuntime>
    {
        private readonly CharacterSpatialHash _characterSpatialHash;
        private readonly float _searchRadius;

        public FindTargetSystem(World world, CharacterSpatialHash characterSpatialHash, float searchRadius) :
            base(world)
        {
            _characterSpatialHash = characterSpatialHash;
            _searchRadius = searchRadius;
        }

        protected override void ForEach(in Entity weapon, ref WeaponRuntime state)
        {
            var owner = weapon.GetRelation<Owner>();
            var origin = owner.Get<PhysicsPosition>().Value;
            var findEnemies = !owner.Has<EnemyTag>();
            var targetGroupId = findEnemies
                ? CharacterSpatialHash.EnemyGroupId
                : CharacterSpatialHash.NonEnemyGroupId;
            var radiusSqr = _searchRadius * _searchRadius;
            var currentTargets = weapon.GetRelations<Target>();
            var target = default(Entity);

            for (var i = 0; i < currentTargets.Length; i++)
            {
                var current = currentTargets[i];
                if (!current.IsAlive || current.Has<EnemyTag>() != findEnemies ||
                    !current.TryGet<PhysicsPosition>(out var targetPosition) ||
                    !current.TryGet<CharacterState>(out var targetState) || targetState.Health <= 0f) continue;
                var delta = targetPosition.Value - origin;
                if (delta.x * delta.x + delta.y * delta.y >= radiusSqr) continue;
                target = current;
                break;
            }

            if (!target.IsAlive)
            {
                Span<EntityId> candidates = stackalloc EntityId[1];
                var candidateCount = _characterSpatialHash.FindNearestIds(origin, _searchRadius, owner.Id,
                    targetGroupId, candidates);
                if (candidateCount > 0) World.TryGetEntity(candidates[0], out target);
            }

            var unchanged = target.IsAlive
                ? currentTargets.Length == 1 && currentTargets[0] == target
                : currentTargets.Length == 0;
            if (unchanged) return;

            CommandBuffer.RemoveRelation<Target>(weapon);
            if (target.IsAlive)
            {
                CommandBuffer.AddRelation<Target>(weapon, target);
            }
        }
    }
}

