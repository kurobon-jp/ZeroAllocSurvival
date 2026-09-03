using LitheEcs.Unity.Jobs;
using Unity.Jobs;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Systems;

[assembly: RegisterGenericJobType(typeof(
    BurstPointerBatchJob<
        EnemyFollowSystem.EnemyFollowAction,
        PhysicsPosition,
        CharacterState,
        CrowdAgent
    >))]

[assembly: RegisterGenericJobType(typeof(
    BurstPointerBatchJob<
        CrowdMovementSystem.GatherAction,
        PhysicsPosition,
        CrowdAgent,
        CharacterSlot
    >))]

[assembly: RegisterGenericJobType(typeof(
    BurstPointerBatchJob<
        CrowdMovementSystem.ApplyAction,
        PhysicsPosition,
        CrowdAgent,
        CharacterSlot>))]
