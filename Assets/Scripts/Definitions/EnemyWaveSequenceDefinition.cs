using System;
using UnityEngine;

namespace ZeroAllocSurvival.Definitions
{
    [Serializable]
    public sealed class EnemyWaveDefinition
    {
        [SerializeField] private string name = "Wave";
        [SerializeField] private bool infiniteDuration;
        [SerializeField, Min(.01f)] private float duration = 30f;
        [SerializeField, Min(.001f)] private float spawnInterval = 1f;
        [SerializeField, Min(1)] private int spawnCount = 1;
        [SerializeField, Min(1)] private int maximumAliveEnemies = 100;
        [Header("Enemy stat multipliers")]
        [SerializeField, Min(.01f)] private float healthMultiplier = 1f;
        [SerializeField, Min(.01f)] private float attackMultiplier = 1f;
        [SerializeField, Range(.01f, 1.2f)] private float moveSpeedMultiplier = 1f;
        [SerializeField, Min(.01f)] private float experienceMultiplier = 1f;
        [Header("Alive-count catch-up (0 disables)")]
        [SerializeField, Range(0f, 1f)] private float targetAliveCatchUpRate;
        [SerializeField, Min(1)] private int maximumSpawnCount = 1;
        [SerializeField] private CharacterSpawnCandidate[] candidates;

        public string Name => name;
        public bool InfiniteDuration => infiniteDuration;
        public float Duration => Mathf.Max(.01f, duration);
        public float SpawnInterval => Mathf.Max(.001f, spawnInterval);
        public int SpawnCount => Mathf.Max(1, spawnCount);
        public int MaximumAliveEnemies => Mathf.Max(1, maximumAliveEnemies);
        public float HealthMultiplier => healthMultiplier > 0f ? healthMultiplier : 1f;
        public float AttackMultiplier => attackMultiplier > 0f ? attackMultiplier : 1f;
        public float MoveSpeedMultiplier => moveSpeedMultiplier > 0f
            ? Mathf.Min(1.2f, moveSpeedMultiplier)
            : 1f;
        public float ExperienceMultiplier => experienceMultiplier > 0f ? experienceMultiplier : 1f;
        public float TargetAliveCatchUpRate => Mathf.Clamp01(targetAliveCatchUpRate);
        public int MaximumSpawnCount => Mathf.Max(SpawnCount, maximumSpawnCount);
        public CharacterSpawnCandidate[] Candidates => candidates;
    }

    [CreateAssetMenu(menuName = "Zero Alloc Survival/Enemy Wave Sequence", fileName = "EnemyWaveSequence")]
    public sealed class EnemyWaveSequenceDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float intervalBetweenWaves = 5f;
        [SerializeField] private bool loop;
        [SerializeField] private EnemyWaveDefinition[] waves;

        public float IntervalBetweenWaves => Mathf.Max(0f, intervalBetweenWaves);
        public bool Loop => loop;
        public int WaveCount => waves?.Length ?? 0;
        public EnemyWaveDefinition GetWave(int index) =>
            waves != null && (uint)index < (uint)waves.Length ? waves[index] : null;
    }
}
