using System;
using UnityEngine;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Services
{
    internal sealed class CharacterSpatialHash
    {
        public const int NonEnemyGroupId = 0;
        public const int EnemyGroupId = 1;

        private readonly float _cellSize;
        private readonly int[] _bucketHeads;
        private readonly int _bucketMask;
        private readonly EntityId[] _entities;
        private readonly Cell[] _cells;
        private readonly Vector3[] _positions;
        private readonly int[] _next;
        private readonly int[] _groupIds;
        private int _count;

        public CharacterSpatialHash(float cellSize, int capacity)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _cellSize = cellSize;
            var bucketCount = 1;
            while (bucketCount < capacity) bucketCount <<= 1;
            _bucketHeads = new int[bucketCount];
            _bucketMask = bucketCount - 1;
            _entities = new EntityId[capacity];
            _cells = new Cell[capacity];
            _positions = new Vector3[capacity];
            _next = new int[capacity];
            _groupIds = new int[capacity];
        }

        public void Clear()
        {
            Array.Fill(_bucketHeads, -1);
            _count = 0;
        }

        public void Add(EntityId entityId, Vector3 position, int groupId)
        {
            var index = _count++;
            if (index >= _entities.Length)
                throw new InvalidOperationException("CharacterSpatialHash capacity was exceeded.");

            var cell = GetCell(position);
            _entities[index] = entityId;
            _cells[index] = cell;
            _positions[index] = position;
            _groupIds[index] = groupId;

            var bucket = cell.GetHashCode() & _bucketMask;
            _next[index] = _bucketHeads[bucket];
            _bucketHeads[bucket] = index;
        }

        public int FindNearestIds(Vector3 origin, float radius, EntityId excluded, int groupId,
            Span<EntityId> results)
        {
            if (radius <= 0f || results.IsEmpty) return 0;
            ;
            var radiusSqr = radius * radius;
            var min = GetCell(origin - new Vector3(radius, radius, 0f));
            var max = GetCell(origin + new Vector3(radius, radius, 0f));
            Span<float> distances = stackalloc float[results.Length];
            var resultCount = 0;

            for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
            {
                var cell = new Cell(x, y);
                var index = _bucketHeads[cell.GetHashCode() & _bucketMask];
                while (index >= 0)
                {
                    if (!_cells[index].Equals(cell))
                    {
                        index = _next[index];
                        continue;
                    }

                    var entityId = _entities[index];
                    var next = _next[index];
                    if (entityId == excluded || _groupIds[index] != groupId)
                    {
                        index = next;
                        continue;
                    }

                    var delta = _positions[index] - origin;
                    var sqrDistance = delta.x * delta.x + delta.y * delta.y;
                    if (sqrDistance >= radiusSqr)
                    {
                        index = next;
                        continue;
                    }

                    var insertIndex = resultCount;
                    while (insertIndex > 0 && distances[insertIndex - 1] > sqrDistance) insertIndex--;
                    if (insertIndex >= results.Length)
                    {
                        index = next;
                        continue;
                    }

                    var lastIndex = Mathf.Min(resultCount, results.Length - 1);
                    for (var i = lastIndex; i > insertIndex; i--)
                    {
                        results[i] = results[i - 1];
                        distances[i] = distances[i - 1];
                    }

                    results[insertIndex] = entityId;
                    distances[insertIndex] = sqrDistance;
                    if (resultCount < results.Length) resultCount++;
                    index = next;
                }
            }

            return resultCount;
        }

        /// <summary>
        /// Finds character centres inside a hitscan corridor and returns them in travel order.
        /// Only cells overlapped by the segment bounds are visited, so unrelated nearby characters
        /// cannot consume the result capacity before the ray test is applied.
        /// </summary>
        public int FindAlongRayIds(Vector3 origin, Vector3 direction, float range, float hitRadius,
            EntityId excluded, int groupId, Span<EntityId> results)
        {
            if (range <= 0f || hitRadius < 0f || results.IsEmpty) return 0;

            direction.z = 0f;
            var directionLengthSqr = direction.x * direction.x + direction.y * direction.y;
            if (directionLengthSqr <= .0001f) return 0;
            direction /= Mathf.Sqrt(directionLengthSqr);

            var end = origin + direction * range;
            var expansion = new Vector3(hitRadius, hitRadius, 0f);
            var minPoint = Vector3.Min(origin, end) - expansion;
            var maxPoint = Vector3.Max(origin, end) + expansion;
            var min = GetCell(minPoint);
            var max = GetCell(maxPoint);
            Span<float> distances = stackalloc float[results.Length];
            var resultCount = 0;

            for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
            {
                var cell = new Cell(x, y);
                var index = _bucketHeads[cell.GetHashCode() & _bucketMask];
                while (index >= 0)
                {
                    if (!_cells[index].Equals(cell))
                    {
                        index = _next[index];
                        continue;
                    }

                    var entityId = _entities[index];
                    var next = _next[index];
                    if (entityId == excluded || _groupIds[index] != groupId)
                    {
                        index = next;
                        continue;
                    }

                    var relative = _positions[index] - origin;
                    relative.z = 0f;
                    var forward = Vector3.Dot(relative, direction);
                    if (forward < -hitRadius || forward > range + hitRadius)
                    {
                        index = next;
                        continue;
                    }

                    var closestForward = Mathf.Clamp(forward, 0f, range);
                    var closestDelta = relative - direction * closestForward;
                    var closestDistanceSqr = closestDelta.x * closestDelta.x + closestDelta.y * closestDelta.y;
                    if (closestDistanceSqr > hitRadius * hitRadius)
                    {
                        index = next;
                        continue;
                    }

                    var insertIndex = resultCount;
                    var contactDistance = Mathf.Max(0f, forward - hitRadius);
                    while (insertIndex > 0 && distances[insertIndex - 1] > contactDistance) insertIndex--;
                    if (insertIndex >= results.Length)
                    {
                        index = next;
                        continue;
                    }

                    var lastIndex = Mathf.Min(resultCount, results.Length - 1);
                    for (var i = lastIndex; i > insertIndex; i--)
                    {
                        results[i] = results[i - 1];
                        distances[i] = distances[i - 1];
                    }

                    results[insertIndex] = entityId;
                    distances[insertIndex] = contactDistance;
                    if (resultCount < results.Length) resultCount++;
                    index = next;
                }
            }

            return resultCount;
        }

        public int FindNearestBySectorIds(Vector3 origin, float radius, EntityId excluded, int groupId,
            Span<EntityId> results)
        {
            if (radius <= 0f || results.IsEmpty) return 0;

            results.Clear();
            Span<float> distances = stackalloc float[results.Length];
            distances.Fill(float.PositiveInfinity);
            var radiusSqr = radius * radius;
            var min = GetCell(origin - new Vector3(radius, radius, 0f));
            var max = GetCell(origin + new Vector3(radius, radius, 0f));

            for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
            {
                var cell = new Cell(x, y);
                var index = _bucketHeads[cell.GetHashCode() & _bucketMask];
                while (index >= 0)
                {
                    if (!_cells[index].Equals(cell))
                    {
                        index = _next[index];
                        continue;
                    }

                    var entityId = _entities[index];
                    var next = _next[index];
                    if (entityId == excluded || _groupIds[index] != groupId)
                    {
                        index = next;
                        continue;
                    }

                    var delta = _positions[index] - origin;
                    var sqrDistance = delta.x * delta.x + delta.y * delta.y;
                    if (sqrDistance >= radiusSqr)
                    {
                        index = next;
                        continue;
                    }

                    var normalizedAngle = (Mathf.Atan2(delta.y, delta.x) + Mathf.PI) / (Mathf.PI * 2f);
                    var sector = Mathf.Min(results.Length - 1, (int)(normalizedAngle * results.Length));
                    if (sqrDistance < distances[sector])
                    {
                        results[sector] = entityId;
                        distances[sector] = sqrDistance;
                    }

                    index = next;
                }
            }

            var resultCount = 0;
            for (var i = 0; i < results.Length; i++)
            {
                if (float.IsPositiveInfinity(distances[i])) continue;
                results[resultCount++] = results[i];
            }

            return resultCount;
        }

        private Cell GetCell(Vector3 position) => new(
            (int)(position.x / _cellSize),
            (int)(position.y / _cellSize));

        private readonly struct Cell : IEquatable<Cell>
        {
            public readonly int X;
            public readonly int Y;

            public Cell(int x, int y)
            {
                X = x;
                Y = y;
            }

            public bool Equals(Cell other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is Cell other && Equals(other);
            public override int GetHashCode() => unchecked((X * 397) ^ Y);
        }
    }
}