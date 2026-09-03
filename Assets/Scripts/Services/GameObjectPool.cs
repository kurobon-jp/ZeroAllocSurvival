using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZeroAllocSurvival.Services
{
    internal sealed class GameObjectPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _root;
        private readonly Stack<T> _available;
        private readonly HashSet<T> _all;

        public GameObjectPool(T prefab, int prewarmCount, string rootName)
        {
            _prefab = prefab;
            _root = new GameObject(rootName).transform;
            prewarmCount = Mathf.Max(0, prewarmCount);
            _available = new Stack<T>(prewarmCount);
            _all = new HashSet<T>(prewarmCount);
            for (var i = 0; i < prewarmCount; i++)
            {
                var instance = Create();
                instance.gameObject.SetActive(false);
                _available.Push(instance);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            var instance = _available.Count > 0 ? _available.Pop() : Create();
            instance.transform.SetParent(null);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public bool Release(T instance)
        {
            if (instance == null || !_all.Contains(instance) || !instance.gameObject.activeSelf) return false;
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_root, false);
            _available.Push(instance);
            return true;
        }

        public void Dispose()
        {
            foreach (var instance in _all)
                if (instance != null)
                    Object.Destroy(instance);
            _all.Clear();
            _available.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private T Create()
        {
            var instance = Object.Instantiate(_prefab, _root);
            // 初回Activate時のalloc回避
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _all.Add(instance);
            return instance;
        }
    }
}
