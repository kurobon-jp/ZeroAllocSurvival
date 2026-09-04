using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Definitions;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class EnemySpawnSystem : CharacterSpawnSystem, ITickable
    {
        private readonly float _minimumDistance;
        private readonly float _maximumDistance;
        private readonly int _capacity;
        private readonly EntityQuery<EnemyTag> _enemyQuery;
        private readonly EnemyWaveSequenceDefinition _waveSequence;
        private int _waveIndex;
        private float _waveElapsed;
        private float _intermissionRemaining;
        private bool _isIntermission;
        private bool _isCompleted;
        private float _timer;

        public EnemySpawnSystem(World world, CharacterSlotRegistry slots,
            CharacterVisualRegistry visuals, EnemyWaveSequenceDefinition waveSequence,
            float minimumDistance, float maximumDistance, int capacity)
            : base(world, slots, visuals)
        {
            _waveSequence = waveSequence;
            _minimumDistance = minimumDistance;
            _maximumDistance = maximumDistance;
            _capacity = capacity;
            _enemyQuery = world.Query().With<EnemyTag>();
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isCompleted) return;
            if (_isIntermission)
            {
                _intermissionRemaining -= deltaTime;
                if (_intermissionRemaining <= 0f) BeginNextWave();
                return;
            }

            var wave = _waveSequence.GetWave(_waveIndex);
            if (wave == null)
            {
                CompleteOrLoop();
                return;
            }

            _waveElapsed += deltaTime;
            if (!wave.InfiniteDuration && _waveElapsed >= wave.Duration)
            {
                _isIntermission = true;
                _intermissionRemaining = _waveSequence.IntervalBetweenWaves;
                if (_intermissionRemaining <= 0f) BeginNextWave();
                return;
            }

            if (!World.TryGetSingleton<PlayerTag>(out var player) || !player.TryGet<PhysicsPosition>(out var position)) return;

            var aliveLimit = Mathf.Min(_capacity, wave.MaximumAliveEnemies);
            var enemyCount = _enemyQuery.Count;
            _timer -= deltaTime;
            while (_timer <= 0f && enemyCount < aliveLimit)
            {
                _timer += wave.SpawnInterval;
                var missingCount = aliveLimit - enemyCount;
                var catchUpCount = Mathf.CeilToInt(missingCount * wave.TargetAliveCatchUpRate);
                var requestedCount = Mathf.Clamp(Mathf.Max(wave.SpawnCount, catchUpCount),
                    wave.SpawnCount, wave.MaximumSpawnCount);
                var count = Mathf.Min(requestedCount, missingCount);
                for (var i = 0; i < count; i++)
                {
                    SpawnEnemy(CreateSpawnPosition(position.Value), wave.Candidates, wave);
                    enemyCount++;
                }
            }

            if (enemyCount >= aliveLimit) _timer = Mathf.Min(_timer, wave.SpawnInterval);
        }

        private void BeginNextWave()
        {
            _waveIndex++;
            _waveElapsed = 0f;
            _timer = 0f;
            _isIntermission = false;
            _intermissionRemaining = 0f;
            if (_waveIndex >= _waveSequence.WaveCount) CompleteOrLoop();
        }

        private void CompleteOrLoop()
        {
            if (_waveSequence.Loop)
            {
                _waveIndex = 0;
                _waveElapsed = 0f;
                _timer = 0f;
                _isIntermission = false;
                return;
            }
            _isCompleted = true;
        }

        private Vector3 CreateSpawnPosition(Vector3 center)
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var distance = Random.Range(_minimumDistance, _maximumDistance);
            return center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
        }

        private void SpawnEnemy(Vector3 position, CharacterSpawnCandidate[] candidates,
            EnemyWaveDefinition wave = null)
        {
            Entity entity;
            using (World.BeginStructuralBatch())
                entity = Spawn(position, true, SelectDefinition(candidates));
            if (wave != null) ApplyWaveScaling(entity, wave);
        }

        private static void ApplyWaveScaling(in Entity entity, EnemyWaveDefinition wave)
        {
            ref var state = ref entity.Get<CharacterState>();
            state.MaxHealth *= wave.HealthMultiplier;
            state.Health = state.MaxHealth;
            state.AttackPower *= wave.AttackMultiplier;
            state.MoveSpeed *= wave.MoveSpeedMultiplier;

            ref var reward = ref entity.Get<ExperienceReward>();
            reward.Value = Mathf.Max(1, Mathf.RoundToInt(reward.Value * wave.ExperienceMultiplier));
        }

        private static CharacterDefinition SelectDefinition(CharacterSpawnCandidate[] candidates)
        {
            if (candidates == null || candidates.Length == 0) return null;
            var totalWeight = 0f;
            for (var i = 0; i < candidates.Length; i++)
                if (candidates[i].definition != null)
                    totalWeight += Mathf.Max(0f, candidates[i].weight);
            if (totalWeight <= 0f)
            {
                for (var i = 0; i < candidates.Length; i++)
                    if (candidates[i].definition != null)
                        return candidates[i].definition;
                return null;
            }

            var selection = Random.value * totalWeight;
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate.definition == null) continue;
                selection -= Mathf.Max(0f, candidate.weight);
                if (selection <= 0f) return candidate.definition;
            }
            for (var i = candidates.Length - 1; i >= 0; i--)
                if (candidates[i].definition != null)
                    return candidates[i].definition;
            return null;
        }
    }
}
