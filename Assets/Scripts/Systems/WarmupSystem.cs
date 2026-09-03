using LitheEcs;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    /// <summary>
    /// Prepares runtime resources to avoid allocations during gameplay. This warmup is optional
    /// and is not a recommendation to pursue zero-allocation design in general.
    /// </summary>
    public class WarmupSystem : BaseSystem, IInitializable
    {
        private readonly int _capacity;
        
        public WarmupSystem(World world, int capacity) : base(world)
        {
            _capacity = capacity;
        }

        void IInitializable.Initialize()
        {
            World.WarmParallelQueryWorkers();
            World.ReserveEntities(_capacity * 3);
            World.ReserveArchetypeGroup(_capacity, static group => group
                .Common(static archetype => archetype
                    .Add<EnemyTag>()
                    .Add<PhysicsPosition>()
                    .Add<CollisionRadius>()
                    .Add<CharacterState>()
                    .Add<CharacterVisualFeedback>()
                    .Add<CharacterSlot>()
                    .Add<CharacterAnimationState>()
                    .Add<BatchVisual>()
                    .Add<ContactAttack>()
                    .Add<ExperienceReward>()
                    .Add<EnemySteering>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<ContactDamage>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<ContactDamage>()
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<ContactDamage>()
                    .Add<Dead>()
                )
            );

            World.ReserveArchetypeGroup(1, static group => group
                .Common(static archetype => archetype
                    .Add<PhysicsPosition>()
                    .Add<CollisionRadius>()
                    .Add<CharacterState>()
                    .Add<CharacterVisualFeedback>()
                    .Add<Link<UnityEngine.Transform>>()
                    .Add<BatchVisual>()
                    .Add<CharacterSlot>()
                    .Add<CharacterAnimationState>()
                    .Add<PlayerTag>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                    .Add<PlayerProgress>()
                    .Add<PlayerUpgradeLevels>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                    .Add<PlayerProgress>()
                    .Add<PlayerUpgradeLevels>()
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Invincible>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Invincible>()
                    .Add<AutopilotMovement>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Invincible>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Invincible>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                    .Add<PlayerProgress>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Invincible>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                    .Add<PlayerProgress>()
                    .Add<PlayerUpgradeLevels>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<CrowdAgent>()
                    .Add<Invincible>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                    .Add<PlayerProgress>()
                    .Add<PlayerUpgradeLevels>()
                    .Add<Dead>()
                )
            );

            World.ReserveArchetypeGroup(1, static group => group
                .Common(static archetype => archetype
                    .Add<PhysicsPosition>()
                    .Add<CollisionRadius>()
                    .Add<CharacterState>()
                    .Add<CharacterVisualFeedback>()
                    .Add<Link<UnityEngine.Transform>>()
                    .Add<BatchVisual>()
                    .Add<CharacterSlot>()
                    .Add<CharacterAnimationState>()
                    .Add<PlayerTag>()
                    .Add<AutopilotMovement>()
                    .Add<PrimaryFireDirection>()
                    .Add<PlayerProgress>()
                    .Add<PlayerUpgradeLevels>()
                )
                .Add(static archetype => archetype
                    .Add<SpatialGroup>()
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<Dead>()
                )
                .Add()
                .Add(static archetype => archetype
                    .Add<Invincible>()
                    .Add<SpatialGroup>()
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<Invincible>()
                    .Add<Dead>()
                )
                .Add(static archetype => archetype
                    .Add<Invincible>()
                )
            );

            World.ReserveArchetypeGroup(1024, static group =>
            {
                group.Add(static archetype => archetype
                    .Add<Link<UnityEngine.Transform>>()
                    .Add<AttackInstance>()
                );
                group.Add(static archetype => archetype
                    .Add<Link<UnityEngine.Transform>>()
                    .Add<ImpactVisual>()
                );
            });

            World.ReserveArchetypeGroup(8, static group =>
            {
                group.Add(static archetype => archetype
                    .Add<LevelUp>()
                );
                group.Add(static archetype => archetype
                    .Add<GamePause>()
                );
                group.Add(static archetype => archetype
                    .Add<LevelUp>()
                    .Add<GamePause>()
                );
                group.Add(static archetype => archetype
                    .Add<LevelUp>()
                    .Add<GamePause>()
                    .Add<UpgradeSelected>()
                );
            });
            World.ReserveArchetype(5, static archetype => archetype
                .Add<WeaponRuntime>()
                .Add<PrimaryWeaponTag>()
            );
            World.ReserveArchetype(5, static archetype => archetype
                .Add<WeaponRuntime>()
            );
            World.ReserveArchetype(_capacity, static archetype => archetype
                .Add<Damage>()
            );
            World.ReserveArchetype(_capacity, static archetype => archetype
                .Add<ExperienceDrop>()
            );
            World.ReserveArchetype(_capacity, static archetype => archetype
                .Add<ExperienceCollected>()
            );
            World.ReserveArchetype(128, static archetype => archetype
                .Add<ContactEntered>()
            );
            World.ReserveArchetype(128, static archetype => archetype
                .Add<ContactExited>()
            );
            World.ReserveArchetype(30, static archetype => archetype
                .Add<UpgradeSelected>()
            );
            World.ReserveArchetype(10, static archetype => archetype
                .Add<AttackRequest>()
            );
            World.CommandBuffer.Reserve(_capacity, _capacity, 0);
            World.CommandBuffer.ReservePayload<PlayerTag>(1);
            World.CommandBuffer.ReservePayload<Target>(1);
            World.CommandBuffer.ReservePayload<WeaponRuntime>(5);
            World.CommandBuffer.ReservePayload<PrimaryWeaponTag>(5);
            World.CommandBuffer.ReservePayload<LevelUp>(5);
            World.CommandBuffer.ReservePayload<GamePause>(5);
            World.CommandBuffer.ReservePayload<BatchVisual>(128);
            World.CommandBuffer.ReservePayload<ContactAttack>(128);
            World.CommandBuffer.ReservePayload<ContactEntered>(128);
            World.CommandBuffer.ReservePayload<ContactExited>(128);
            World.CommandBuffer.ReservePayload<ContactDamage>(128);
            World.CommandBuffer.ReservePayload<AttackRequest>(128);
            World.CommandBuffer.ReservePayload<AttackInstance>(128);
            World.CommandBuffer.ReservePayload<ImpactVisual>(128);
            World.CommandBuffer.ReservePayload<EnemyTag>(128);
            World.CommandBuffer.ReservePayload<PhysicsPosition>(128);
            World.CommandBuffer.ReservePayload<CollisionRadius>(128);
            World.CommandBuffer.ReservePayload<CharacterState>(128);
            World.CommandBuffer.ReservePayload<CharacterVisualFeedback>(128);
            World.CommandBuffer.ReservePayload<Link<UnityEngine.Transform>>(128);
            World.CommandBuffer.ReservePayload<SpatialGroup>(128);
            World.CommandBuffer.ReservePayload<CrowdAgent>(128);
            World.CommandBuffer.ReservePayload<CharacterSlot>(128);
            World.CommandBuffer.ReservePayload<CharacterAnimationState>(128);
            World.CommandBuffer.ReservePayload<Damage>(1024);
            World.CommandBuffer.ReservePayload<Dead>(1024);
            World.CommandBuffer.ReservePayload<ExperienceDrop>(1024);
            World.CommandBuffer.ReservePayload<ExperienceCollected>(1024);
            World.CommandBuffer.ReservePayload<ExperienceReward>(1024);
            World.CommandBuffer.ReservePayload<EnemySteering>(1024);

            World.ReserveRelation<Target>(5);
            World.ReserveRelation<Owner>(5);

            World.Query().With<EnemyTag>().Warmup();
            World.Query().With<GamePause>().Warmup();
            World.Query().With<ExperienceDrop>().Warmup();
            World.Query<WeaponRuntime>().Warmup();
            World.Query<AttackInstance>().Warmup();
        }
    }
}
