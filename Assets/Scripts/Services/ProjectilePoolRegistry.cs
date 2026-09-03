using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeroAllocSurvival.Services
{
    internal sealed class ProjectilePoolRegistry : IDisposable
    {
        private readonly Dictionary<Transform, int> _indices = new();
        private readonly List<GameObjectPool<Transform>> _pools = new();

        public int Register(Transform prefab, int prewarmCount)
        {
            if (prefab == null) return -1;
            if (_indices.TryGetValue(prefab, out var index)) return index;

            index = _pools.Count;
            _indices.Add(prefab, index);
            _pools.Add(new GameObjectPool<Transform>(prefab, prewarmCount, $"Projectile Pool - {prefab.name}"));
            return index;
        }

        public Transform Get(int index, Vector3 position, Quaternion rotation) =>
            _pools[index].Get(position, rotation);

        public void Release(int index, Transform instance) => _pools[index].Release(instance);

        public void Dispose()
        {
            for (var i = 0; i < _pools.Count; i++) _pools[i].Dispose();
            _pools.Clear();
            _indices.Clear();
        }
    }
}
