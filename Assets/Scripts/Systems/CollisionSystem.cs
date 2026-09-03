using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class CollisionSystem : QueryActionSystem<ContactEntered>
    {
        public CollisionSystem(World world) : base(world) { }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override void ForEach(in Entity entity, ref ContactEntered contact)
        {
            if (contact.Source.IsAlive && contact.Other.TryGet<CharacterState>(out var otherState) &&
                contact.Other.TryGet<ContactAttack>(out var contactAttack))
            {
                var id = CommandBuffer.Spawn();
                CommandBuffer.AddComponent(id, new Damage
                {
                    Value = otherState.AttackPower,
                    Target = contact.Source
                });

                if (!contact.Other.Has<ContactDamage>())
                    CommandBuffer.AddComponent(contact.Other, new ContactDamage
                    {
                        Target = contact.Source,
                        Interval = contactAttack.Interval,
                        Timer = contactAttack.Interval
                    });
            }

            // Contact events are transient even when either endpoint is no longer valid.
            CommandBuffer.Despawn(entity);
        }
    }

    /// <summary>Produces damage events at a fixed cadence while an enemy contact is active.</summary>
    internal sealed class ContactDamageSystem : QueryActionSystem<ContactDamage>
    {
        internal ContactDamageSystem(World world) : base(world) { }

        protected override void ForEach(in Entity entity, ref ContactDamage contact)
        {
            if (!contact.Target.IsAlive || entity.Has<Dead>() ||
                !entity.TryGet<CharacterState>(out var sourceState) || sourceState.Health <= 0f)
            {
                CommandBuffer.RemoveComponent<ContactDamage>(entity);
                return;
            }

            contact.Timer -= DeltaTime;
            if (contact.Timer > 0f) return;

            // Preserve a stable cadence without creating an unbounded catch-up burst after a
            // long frame. At most one damage event is produced per game update.
            contact.Timer = Mathf.Max(.01f, contact.Interval);
            var damage = CommandBuffer.Spawn();
            CommandBuffer.AddComponent(damage, new Damage
            {
                Value = sourceState.AttackPower,
                Target = contact.Target
            });
        }

        protected override void OnPostTick() => CommandBuffer.Playback();
    }
}
