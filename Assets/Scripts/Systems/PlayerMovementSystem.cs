using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class PlayerMovementSystem : BaseSystem, IInitializable, ITickable
    {
        private Entity _player;
        private readonly bool _autopilotEnabled;
        private readonly Camera _camera;
        private readonly VirtualStickInputState _virtualStick;

        public PlayerMovementSystem(World world, bool autopilotEnabled, Camera camera,
            VirtualStickInputState virtualStick) : base(world)
        {
            _autopilotEnabled = autopilotEnabled;
            _camera = camera;
            _virtualStick = virtualStick;
        }

        void IInitializable.Initialize()
        {
            _player = World.Singleton<PlayerTag>();
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_player.IsAlive || !_player.TryGetRef<CrowdAgent>(out var agent) ||
                !_player.TryGetRef<CharacterState>(out var state) || state.Value.Health <= 0f) return;

            var direction = Vector3.zero;
            var hasVirtualStickInput = _virtualStick is { IsActive: true };
            var up = Input.GetKey(KeyCode.W);
            var down = Input.GetKey(KeyCode.S);
            var right = Input.GetKey(KeyCode.D);
            var left = Input.GetKey(KeyCode.A);
            var hasKeyboardInput = up || down || right || left;
            if (up) direction.y += 1f;
            if (down) direction.y -= 1f;
            if (right) direction.x += 1f;
            if (left) direction.x -= 1f;

            if (hasVirtualStickInput)
            {
                var stickDirection = _virtualStick.Direction;
                direction = new Vector3(stickDirection.x, stickDirection.y, 0f);
            }
            else if (hasKeyboardInput)
            {
                if (direction.sqrMagnitude > .0001f)
                {
                    direction.Normalize();
                }
            }
            else if (_autopilotEnabled)
            {
                direction = _player.Get<AutopilotMovement>().Direction;
            }

            agent.Value.DesiredVelocity = new Vector2(direction.x, direction.y) * state.Value.MoveSpeed;
            agent.Value.FacingVelocity = agent.Value.DesiredVelocity;
            if (hasVirtualStickInput)
            {
                if (direction.sqrMagnitude > .0001f)
                    _player.Get<PrimaryFireDirection>().Value = new Vector2(direction.x, direction.y).normalized;
            }
            else if (hasKeyboardInput)
            {
                if (!TryUpdateMouseAim() && direction.sqrMagnitude > .0001f)
                    _player.Get<PrimaryFireDirection>().Value = new Vector2(direction.x, direction.y).normalized;
            }
            else if (_virtualStick is not { HasBeenUsed: true } &&
                     !TryUpdateMouseAim() && direction.sqrMagnitude > .0001f)
            {
                _player.Get<PrimaryFireDirection>().Value = new Vector2(direction.x, direction.y).normalized;
            }
            var hasDirectInput = hasKeyboardInput || hasVirtualStickInput;
            agent.Value.RequiresImmediateApply = hasDirectInput ? (byte)1 : (byte)0;
            agent.Value.DirectControl = hasDirectInput ? (byte)1 : (byte)0;
        }

        private bool TryUpdateMouseAim()
        {
            if (_camera == null || !Input.mousePresent) return false;

            var screenPosition = Input.mousePosition;
            var playerPosition = _player.Get<PhysicsPosition>().Value;
            var distanceFromCamera = playerPosition.z - _camera.transform.position.z;
            var worldPosition = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
            var direction = worldPosition - playerPosition;
            direction.z = 0f;
            if (direction.sqrMagnitude <= .0001f) return true;
            direction.Normalize();
            _player.Get<PrimaryFireDirection>().Value = new Vector2(direction.x, direction.y);
            return true;
        }
    }
}
