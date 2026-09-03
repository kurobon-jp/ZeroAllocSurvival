using LitheEcs;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    /// <summary>Releases transient contact events after downstream gameplay systems have observed them.</summary>
    internal sealed class ContactEventCleanupSystem : QueryActionSystem<ContactExited>
    {
        internal ContactEventCleanupSystem(World world) : base(world)
        {
        }

        protected override void OnPostTick() => CommandBuffer.Playback();

        protected override void ForEach(in Entity entity, ref ContactExited contact)
        {
            if (contact.Other.IsAlive && contact.Other.Has<ContactDamage>())
                CommandBuffer.RemoveComponent<ContactDamage>(contact.Other);
            CommandBuffer.Despawn(entity);
        }
    }
}