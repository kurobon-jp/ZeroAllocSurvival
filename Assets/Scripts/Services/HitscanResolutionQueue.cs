using System;
using System.IO;
using System.Text;
using UnityEngine;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Services
{
    internal sealed class ProjectileDiagnosticLog : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly int _shotLimit;

        internal ProjectileDiagnosticLog(bool enabled, int shotLimit)
        {
            _shotLimit = Mathf.Max(1, shotLimit);
            if (!enabled) return;
            try
            {
                var fileName = $"projectile-{DateTime.Now:yyyyMMdd-HHmmss}.log";
                var path = Path.Combine(Application.persistentDataPath, fileName);
                _writer = new StreamWriter(path, false, new UTF8Encoding(false), 64 * 1024)
                {
                    AutoFlush = true
                };
                _writer.WriteLine($"Projectile diagnostics started: {DateTime.Now:O}");
                Debug.Log($"[Projectile.Diagnostics] Log output: {path}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Projectile.Diagnostics] Failed to create log: {exception.Message}");
            }
        }

        internal bool Includes(int shotId) => _writer != null && shotId <= _shotLimit;

        internal void Write(string message) => _writer?.WriteLine(message);

        public void Dispose() => _writer?.Dispose();
    }

    /// <summary>
    /// Stores distance-ordered hitscan candidates until DamageSystem resolves them.
    /// The backing arrays are fixed so firing does not allocate managed memory.
    /// </summary>
    internal sealed class HitscanResolutionQueue
    {
        internal readonly struct Pellet
        {
            public readonly int CandidateStart;
            public readonly int CandidateCount;
            public readonly int ShotId;
            public readonly int PelletIndex;
            public readonly int Penetration;
            public readonly float Damage;
            public readonly float Knockback;
            public readonly Vector3 Origin;
            public readonly Vector3 Direction;

            public Pellet(int candidateStart, int candidateCount, int shotId, int pelletIndex, int penetration,
                float damage, float knockback, Vector3 origin, Vector3 direction)
            {
                CandidateStart = candidateStart;
                CandidateCount = candidateCount;
                ShotId = shotId;
                PelletIndex = pelletIndex;
                Penetration = penetration;
                Damage = damage;
                Knockback = knockback;
                Origin = origin;
                Direction = direction;
            }
        }

        private readonly Pellet[] _pellets;
        private readonly EntityId[] _candidates;
        private int _pelletCount;
        private int _candidateCount;

        public HitscanResolutionQueue(int pelletCapacity = 1024, int candidateCapacity = 1024)
        {
            if (pelletCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(pelletCapacity));
            if (candidateCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(candidateCapacity));
            _pellets = new Pellet[pelletCapacity];
            _candidates = new EntityId[candidateCapacity];
        }

        public int PelletCount => _pelletCount;

        public ref readonly Pellet GetPellet(int index) => ref _pellets[index];

        public EntityId GetCandidate(int index) => _candidates[index];

        public void EnqueuePellet(ReadOnlySpan<EntityId> candidates, int shotId, int pelletIndex, int penetration,
            float damage, float knockback, Vector3 origin, Vector3 direction)
        {
            if (_pelletCount >= _pellets.Length)
                throw new InvalidOperationException("Hitscan pellet queue capacity was exceeded.");
            if (_candidateCount + candidates.Length > _candidates.Length)
                throw new InvalidOperationException("Hitscan candidate queue capacity was exceeded.");

            var start = _candidateCount;
            candidates.CopyTo(_candidates.AsSpan(start));
            _candidateCount += candidates.Length;
            _pellets[_pelletCount++] = new Pellet(start, candidates.Length, shotId, pelletIndex,
                Math.Max(1, penetration), damage, knockback, origin, direction);
        }

        public void Clear()
        {
            _pelletCount = 0;
            _candidateCount = 0;
        }
    }

}
