using System;
using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Services;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class AutopilotSystem : BaseSystem, IInitializable, ITickable
    {
        private const int DirectionCount = 16;
        private const int SectorCount = 16;
        private const int MaxThreats = SectorCount;
        private const float ShortPrediction = .35f;
        private const float MediumPrediction = .8f;
        private const float LongPrediction = 1.5f;

        private Entity _player;
        private readonly CharacterSpatialHash _spatialHash;
        private readonly float _searchRadius;
        private readonly float _minimumRange;
        private readonly float _idealRange;
        private readonly Vector3[] _threatPositions = new Vector3[MaxThreats];
        private readonly bool[] _occupiedSectors = new bool[SectorCount];
        private QueryAction<ExperienceDrop> _pickupAction;
        private Query<ExperienceDrop> _pickupQuery;
        private Vector3 _pickupOrigin;
        private Vector3 _nearestSafePickup;
        private Entity _nearestSafePickupEntity;
        private float _pickupSafetyDistance;
        private float _nearestSafePickupSqr;
        private int _pickupThreatCount;
        private bool _hasSafePickup;
        private bool _hasLockedPickup;
        private Entity _pickupTarget;
        private Vector3 _lockedPickupPosition;
        private Vector3 _lastDirection = Vector3.up;

        public AutopilotSystem(World world, CharacterSpatialHash spatialHash, float weaponRange = 20) : base(world)
        {
            _spatialHash = spatialHash;
            _searchRadius = Mathf.Max(1f, weaponRange);
            _minimumRange = _searchRadius * .6f;
            _idealRange = _searchRadius * .78f;
        }

        void IInitializable.Initialize()
        {
            _player = World.Singleton<PlayerTag>();
            _pickupQuery = World.Query<ExperienceDrop>();
            _pickupAction = ConsiderPickup;
        }

        void ITickable.Tick(float _)
        {
            ref var movement = ref _player.Get<AutopilotMovement>();
            var position = _player.Get<PhysicsPosition>();
            movement.Direction = Evaluate(position.Value);
        }

        public void Update(Vector3 origin, bool enabled)
        {
            ref var movement = ref _player.Get<AutopilotMovement>();
            movement.Direction = enabled ? Evaluate(origin) : Vector3.zero;
        }

        private Vector3 Evaluate(Vector3 origin)
        {
            Span<EntityId> threats = stackalloc EntityId[MaxThreats];
            var threatCount = _spatialHash.FindNearestBySectorIds(origin, _searchRadius, _player.Id,
                CharacterSpatialHash.EnemyGroupId, threats);
            Array.Clear(_occupiedSectors, 0, _occupiedSectors.Length);
            var validCount = 0;
            var nearestDistance = float.PositiveInfinity;
            var targetPosition = origin;
            for (var i = 0; i < threatCount; i++)
            {
                if (!World.TryGetEntity(threats[i], out var threat) ||
                    !threat.TryGet<PhysicsPosition>(out var threatPosition)) continue;
                var position = threatPosition.Value;
                _threatPositions[validCount++] = position;
                var delta = position - origin;
                delta.z = 0f;
                var distance = delta.magnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    targetPosition = position;
                }

                if (distance <= _idealRange)
                {
                    var angle = Mathf.Atan2(delta.y, delta.x) + Mathf.PI;
                    var sector = Mathf.FloorToInt(angle * (SectorCount / (Mathf.PI * 2f))) % SectorCount;
                    _occupiedSectors[sector] = true;
                }
            }

            var occupiedSectorCount = 0;
            for (var i = 0; i < SectorCount; i++)
                if (_occupiedSectors[i])
                    occupiedSectorCount++;

            var moveSpeed = Mathf.Max(.01f, _player.Get<CharacterState>().MoveSpeed);
            var escapeDistance = Mathf.Min(8f, _searchRadius * .3f);
            var surrounded = occupiedSectorCount >= SectorCount * 5 / 8;
            var escaping = validCount > 0 && (nearestDistance < escapeDistance || surrounded);
            var pickupPosition = origin;
            var seekingPickup = !escaping && TryFindNearestSafePickup(origin, validCount, escapeDistance,
                out pickupPosition);
            if (validCount == 0 && !seekingPickup) return Vector3.zero;
            if (seekingPickup) targetPosition = pickupPosition;
            var bestDirection = seekingPickup
                ? HorizontalDirection(origin, pickupPosition)
                : _lastDirection;
            if (bestDirection.sqrMagnitude <= .0001f) bestDirection = _lastDirection;
            var bestScore = EvaluateDirection(
                origin, targetPosition, bestDirection, moveSpeed, validCount, escaping, seekingPickup);
            var switchThreshold = escaping ? .25f : seekingPickup ? .2f : 1.5f;

            for (var i = 0; i < DirectionCount; i++)
            {
                var angle = i * (Mathf.PI * 2f / DirectionCount);
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var score = EvaluateDirection(
                    origin, targetPosition, direction, moveSpeed, validCount, escaping, seekingPickup);
                if (score <= bestScore + switchThreshold) continue;
                bestScore = score;
                bestDirection = direction;
            }

            var turnSharpness = escaping ? .8f : seekingPickup ? .65f : .35f;
            _lastDirection = Vector3.Slerp(_lastDirection, bestDirection, turnSharpness).normalized;
            return _lastDirection;
        }

        private float EvaluateDirection(Vector3 origin, Vector3 targetPosition, Vector3 direction, float moveSpeed,
            int threatCount, bool escaping, bool seekingPickup)
        {
            var shortPosition = origin + direction * (moveSpeed * ShortPrediction);
            var mediumPosition = origin + direction * (moveSpeed * MediumPrediction);
            var longPosition = origin + direction * (moveSpeed * LongPrediction);
            var minimumClearance = _searchRadius;
            var crowdPenalty = 0f;
            var corridorPenalty = 0f;
            var safetyRadius = Mathf.Max(4f, _minimumRange * .5f);
            var corridorLength = moveSpeed * LongPrediction + safetyRadius;
            var corridorWidth = Mathf.Max(2f, safetyRadius * .45f);

            for (var i = 0; i < threatCount; i++)
            {
                var threatPosition = _threatPositions[i];
                var shortDistance = HorizontalDistance(shortPosition, threatPosition);
                var mediumDistance = HorizontalDistance(mediumPosition, threatPosition);
                var longDistance = HorizontalDistance(longPosition, threatPosition);
                var clearance = Mathf.Min(shortDistance, Mathf.Min(mediumDistance, longDistance));
                minimumClearance = Mathf.Min(minimumClearance, clearance);
                var proximity = Mathf.Max(0f, safetyRadius - clearance);
                crowdPenalty += proximity * proximity;

                var relative = threatPosition - origin;
                relative.z = 0f;
                var forward = Vector3.Dot(relative, direction);
                if (forward <= 0f || forward >= corridorLength) continue;
                var lateral = Mathf.Abs(relative.x * direction.y - relative.y * direction.x);
                corridorPenalty += Mathf.Max(0f, corridorWidth - lateral);
            }

            var targetPenalty = seekingPickup
                ? Mathf.Min(HorizontalDistance(shortPosition, targetPosition),
                    Mathf.Min(HorizontalDistance(mediumPosition, targetPosition),
                        HorizontalDistance(longPosition, targetPosition))) * 3f
                : Mathf.Abs(HorizontalDistance(longPosition, targetPosition) - _idealRange);
            if (!seekingPickup)
            {
                var predictedTargetDistance = HorizontalDistance(longPosition, targetPosition);
                if (predictedTargetDistance < _minimumRange)
                    targetPenalty += (_minimumRange - predictedTargetDistance) * 3f;
            }

            var score = minimumClearance * (escaping ? 12f : 5f)
                        - crowdPenalty * (escaping ? 4f : 2f)
                        - corridorPenalty * (escaping ? 5f : 2f)
                        - targetPenalty * (escaping ? .15f : 2f)
                        + Vector3.Dot(direction, _lastDirection) * (escaping ? .5f : 3f);
            return score;
        }

        private bool TryFindNearestSafePickup(Vector3 origin, int threatCount, float safetyDistance,
            out Vector3 position)
        {
            _pickupOrigin = origin;
            _pickupThreatCount = threatCount;
            _pickupSafetyDistance = safetyDistance;
            _nearestSafePickupSqr = _searchRadius * _searchRadius;
            _hasSafePickup = false;
            _hasLockedPickup = false;
            _pickupQuery.ForEach(_pickupAction);
            if (_hasLockedPickup)
            {
                position = _lockedPickupPosition;
                return true;
            }

            position = _nearestSafePickup;
            if (_hasSafePickup) _pickupTarget = _nearestSafePickupEntity;
            else _pickupTarget = default;
            return _hasSafePickup;
        }

        private void ConsiderPickup(in Entity entity, ref ExperienceDrop experienceDrop)
        {
            var position = experienceDrop.Position;
            if (entity == _pickupTarget && IsPickupSafe(position))
            {
                _lockedPickupPosition = position;
                _hasLockedPickup = true;
            }

            var delta = position - _pickupOrigin;
            delta.z = 0f;
            var sqrDistance = delta.sqrMagnitude;
            if (sqrDistance >= _nearestSafePickupSqr || !IsPickupSafe(position)) return;
            _nearestSafePickupSqr = sqrDistance;
            _nearestSafePickup = position;
            _nearestSafePickupEntity = entity;
            _hasSafePickup = true;
        }

        private bool IsPickupSafe(Vector3 pickupPosition)
        {
            var safetySqr = _pickupSafetyDistance * _pickupSafetyDistance;
            var route = pickupPosition - _pickupOrigin;
            route.z = 0f;
            var routeLengthSqr = route.sqrMagnitude;
            for (var i = 0; i < _pickupThreatCount; i++)
            {
                var threatPosition = _threatPositions[i];
                if ((threatPosition - pickupPosition).sqrMagnitude < safetySqr) return false;
                if (routeLengthSqr <= .0001f) continue;

                var relative = threatPosition - _pickupOrigin;
                relative.z = 0f;
                var progress = Mathf.Clamp01(Vector3.Dot(relative, route) / routeLengthSqr);
                var nearestOnRoute = _pickupOrigin + route * progress;
                if ((threatPosition - nearestOnRoute).sqrMagnitude < 6.25f) return false;
            }

            return true;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var x = a.x - b.x;
            var y = a.y - b.y;
            return Mathf.Sqrt(x * x + y * y);
        }

        private static Vector3 HorizontalDirection(Vector3 origin, Vector3 target)
        {
            var direction = target - origin;
            direction.z = 0f;
            return direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.zero;
        }
    }
}
