using System.Collections.Generic;
using LitheEcs;

namespace ZeroAllocSurvival.Services
{
    /// <summary>Tracks the last target consumed by each projectile ray without storing gameplay state in AttackInstance.</summary>
    internal sealed class ProjectileHitTracker
    {
        private readonly Dictionary<EntityId, EntityId> _lastTargets;

        public ProjectileHitTracker(int capacity = 4096) =>
            _lastTargets = new Dictionary<EntityId, EntityId>(capacity);

        public bool WasLastTarget(EntityId rayId, EntityId targetId) =>
            _lastTargets.TryGetValue(rayId, out var last) && last == targetId;

        public void Record(EntityId rayId, EntityId targetId) => _lastTargets[rayId] = targetId;

        public void Remove(EntityId rayId) => _lastTargets.Remove(rayId);
    }
}
