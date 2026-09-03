using LitheEcs;
using LitheEcs.Unity.Jobs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class EnemyFollowSystem : BaseSystem, IInitializable, ITickable
    {
        private BurstQuery<PhysicsPosition, CharacterState, CrowdAgent> _jobQuery;
        private Entity _player;
        private readonly int _capacity;

        // Paired with the shotgun's knockback of 18. A 3x initial velocity and 9x damping
        // preserves v^2 / (2a), so the shove is sharper without materially extending its distance.
        private const float KnockbackDamping = 180f;

        public EnemyFollowSystem(World world, int capacity) : base(world)
        {
            _capacity = capacity;
        }

        public void Initialize()
        {
            _jobQuery = World.Query<PhysicsPosition, CharacterState, CrowdAgent>()
                .With<EnemyTag>()
                .AsBurstQuery(4096);
            _jobQuery.Reserve(_capacity);
            _player = World.Singleton<PlayerTag>();
        }

        public void Tick(float deltaTime)
        {
            if (!_player.TryGet<PhysicsPosition>(out var position)) return;
            var action = new EnemyFollowAction { PlayerPosition = position.Value, DeltaTime = deltaTime };
            _jobQuery.RunUnsafe(ref action);
        }

        [Unity.Burst.BurstCompile]
        internal struct EnemyFollowAction : IBurstQueryAction<PhysicsPosition, CharacterState, CrowdAgent>
        {
            public Vector3 PlayerPosition;
            public float DeltaTime;

            public void Execute(int index, ref PhysicsPosition position, ref CharacterState state,
                ref CrowdAgent steering)
            {
                var delta = PlayerPosition - position.Value;
                delta.z = 0f;
                var direction = delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.zero;
                var knockback = state.KnockbackVelocity;
                steering.FacingVelocity = new Vector2(direction.x, direction.y) * state.MoveSpeed;
                steering.DesiredVelocity = steering.FacingVelocity + knockback;
                steering.RequiresImmediateApply = knockback.sqrMagnitude > .0001f ? (byte)1 : (byte)0;
                state.KnockbackVelocity = Vector2.MoveTowards(state.KnockbackVelocity, Vector2.zero,
                    KnockbackDamping * DeltaTime);
            }
        }
    }
}
