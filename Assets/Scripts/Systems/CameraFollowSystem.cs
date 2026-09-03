using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class CameraFollowSystem : BaseSystem, ITickable
    {
        private readonly Camera _camera;
        private readonly Vector3 _offset;
        private readonly EntityQuery<EnemyTag> _enemyQuery;
        private readonly float _minimumOrthographicSize;
        private readonly float _maximumOrthographicSize;
        private readonly int _maximumEnemyCount;
        private readonly float _zoomSharpness;

        public CameraFollowSystem(World world, Camera camera, Vector3 offset,
            float minimumOrthographicSize, float maximumOrthographicSize,
            int maximumEnemyCount, float zoomSharpness) : base(world)
        {
            _camera = camera;
            _offset = offset;
            _enemyQuery = world.Query().With<EnemyTag>();
            _minimumOrthographicSize = Mathf.Max(.01f, minimumOrthographicSize);
            _maximumOrthographicSize = Mathf.Max(_minimumOrthographicSize, maximumOrthographicSize);
            _maximumEnemyCount = Mathf.Max(1, maximumEnemyCount);
            _zoomSharpness = Mathf.Max(0f, zoomSharpness);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!World.TryGetSingleton<PlayerTag>(out var player) || !player.TryGet<PhysicsPosition>(out var position)) return;
            _camera.transform.position = position.Value + _offset;
            _camera.transform.LookAt(position.Value);

            if (!_camera.orthographic) return;
            var ratio = Mathf.Clamp01(_enemyQuery.Count / (float)_maximumEnemyCount);
            // SmoothStep keeps the camera stable while the first few enemies appear and avoids
            // an abrupt acceleration as the final wave approaches its population cap.
            ratio = ratio * ratio * (3f - 2f * ratio);
            var targetSize = Mathf.Lerp(_minimumOrthographicSize, _maximumOrthographicSize, ratio);
            var blend = _zoomSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-_zoomSharpness * Mathf.Max(0f, deltaTime));
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, targetSize, blend);
        }
    }
}
