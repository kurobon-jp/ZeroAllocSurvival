using System;
using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Definitions;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal abstract class CharacterSpawnSystem : BaseSystem
    {
        private readonly CharacterSlotRegistry _slots;
        private readonly CharacterVisualRegistry _visuals;

        protected CharacterSpawnSystem(World world, CharacterSlotRegistry slots, CharacterVisualRegistry visuals)
            : base(world)
        {
            _slots = slots;
            _visuals = visuals;
        }

        protected Entity Spawn(Vector3 position, bool isEnemy, CharacterDefinition definition)
        {
            var parameters = definition.Parameters;
            var visual = definition != null ? definition.Visual : null;
            var scale = parameters.collisionRadius * 2f;
            Transform viewTransform = null;
            if (!isEnemy)
            {
                viewTransform = CreatePlayerRoot(position, scale, visual);
            }

            var entity = World.Spawn();
            using (World.BeginStructuralBatch())
            {
                entity.Add(
                    new PhysicsPosition { Value = position },
                    new CollisionRadius { Value = Mathf.Max(.01f, parameters.collisionRadius) },
                    new CharacterState
                    {
                        Health = parameters.health,
                        MaxHealth = parameters.health,
                        AttackPower = parameters.attackPower,
                        MoveSpeed = parameters.moveSpeed
                    }
                );
                entity.Add(default(CharacterVisualFeedback));
                if (viewTransform != null)
                {
                    entity.Add(Link.With(viewTransform));
                    entity.Bind(viewTransform);
                }

                entity.Add(new BatchVisual
                {
                    BatchId = _visuals.IdOf(visual),
                    Depth = isEnemy ? VisualDepth.Enemy : VisualDepth.Player,
                    SortByY = isEnemy ? (byte)1 : (byte)0
                });

                entity.Add(new SpatialGroup
                {
                    Value = isEnemy
                        ? CharacterSpatialHash.EnemyGroupId
                        : CharacterSpatialHash.NonEnemyGroupId
                });
                entity.Add(new CrowdAgent
                {
                    EntityId = entity.Id,
                    Radius = Mathf.Max(.01f, parameters.collisionRadius),
                    Mass = isEnemy ? 1f : 0.5f,
                    AvoidanceWeight = isEnemy ? .6f : 1f,
                    CorrectionVelocityWeight = isEnemy ? 1f : 0f,
                    MaximumCorrectionSpeed = isEnemy ? 2f : 4f,
                    AvoidancePriority = isEnemy
                        ? (byte)Mathf.Min(byte.MaxValue, definition.AvoidancePriority + 1)
                        : (byte)0,
                    Layer = isEnemy ? 1u << 0 : 1u << 1,
                    CollisionMask = (1u << 0) | (1u << 1),
                    ContactEventMask = isEnemy ? 1u << 1 : 1u << 0,
                    StableContactResolution = isEnemy ? (byte)0 : (byte)1
                });

                var index = _slots.Allocate();
                entity.Add(new CharacterSlot { Value = index });
                if (visual != null)
                {
                    var playbackSpeed = isEnemy ? .9f + Hash01(entity.Id.GetHashCode()) * .2f : 1f;
                    var phase = isEnemy
                        ? Hash01(entity.Id.GetHashCode() * 397 ^ 0x5f3759df) * .8f
                        : 0f;
                    entity.Add(visual.CreateAnimationState(playbackSpeed, phase));
                }

                if (isEnemy)
                {
                    entity.Add(
                        default(EnemyTag),
                        new ContactAttack { Interval = Mathf.Max(.01f, parameters.contactDamageInterval) },
                        new ExperienceReward { Value = definition != null ? definition.ExperienceReward : 1 },
                        default(EnemySteering)
                    );
                }
                else
                {
                    entity.Add(default(PlayerTag));
                }
            }

            return entity;
        }

        private static Transform CreatePlayerRoot(Vector3 position, float scale, CharacterVisualDefinition visual)
        {
            if (visual == null || visual.AtlasTexture == null)
                throw new InvalidOperationException("Player character requires a visual with an atlas texture.");
            var rootObject = new GameObject("Player");
            rootObject.layer = 9;
            var root = rootObject.transform;
            root.SetPositionAndRotation(position, Quaternion.identity);
            root.localScale = new Vector3(scale, scale, 1f);
            return root;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                var hash = (uint)value;
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                hash *= 0x846ca68b;
                hash ^= hash >> 16;
                return (hash & 0x00ffffff) / 16777216f;
            }
        }
    }
}
