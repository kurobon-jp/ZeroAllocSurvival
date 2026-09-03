using System;
using System.Collections.Generic;
using LitheEcs;
using LocalAvoidance2D;
using Unity.Collections;
using UnityEngine;
using Unity.Profiling;
using ZeroAllocSurvival.Definitions;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;
using ZeroAllocSurvival.Systems;

namespace ZeroAllocSurvival
{
    [DefaultExecutionOrder(int.MaxValue)]
    public sealed class ZeroAllocSurvivalGame : MonoBehaviour
    {
        [Header("Scene references")] [SerializeField]
        private CharacterDefinition playerDefinition;

        [SerializeField] private EnemyWaveSequenceDefinition enemyWaveSequence;
        [SerializeField] private ExperienceVisualDefinition experienceVisual;
        [SerializeField] private WeaponDefinition[] startingWeapons;
        [SerializeField] private WeaponDefinition[] availableWeapons;

        [SerializeField] private Camera gameCamera;
        [SerializeField] private UpgradePanelPresenter upgradePanel;
        [SerializeField] private GameOverPanelPresenter gameOverPanel;
        [SerializeField] private GaugePresenter expGauge;
        [SerializeField] private GaugePresenter hpGauge;
        [SerializeField] private FpsCounter fps;

        [SerializeField, Min(0f)] private float minimumSpawnDistance = 12f;
        [SerializeField, Min(0f)] private float maximumSpawnDistance = 18f;

        [SerializeField] private int maxEnemies = 10000;

        [Header("Camera")] [SerializeField] private Vector3 cameraOffset = new(0f, 0f, -8f);
        [SerializeField, Min(0f)] private float cameraSharpness = 12f;
        [SerializeField, Min(.01f)] private float minimumCameraSize = 10f;
        [SerializeField, Min(.01f)] private float maximumCameraSize = 30f;
        [SerializeField, Min(1)] private int enemiesForMaximumCameraSize = 10000;
        [SerializeField, Min(0.01f)] private float targetSearchRadius = 30f;

        [Header("Autopilot")] [SerializeField] private bool autopilotEnabled = true;

        [SerializeField] private VirtualPadPresenter virtualPad;

        [Header("Gameplay")] [SerializeField] private bool disableFire;
        [SerializeField] private bool playerInvincible;

        [Header("Diagnostics")] [SerializeField]
        private bool logHitscanDiagnostics = true;

        [SerializeField] private bool logProjectileDiagnostics;
        [SerializeField, Min(1)] private int projectileDiagnosticShotLimit = 8;
        [SerializeField] private bool logPerformance = true;
        [SerializeField, Min(.1f)] private float performanceLogInterval = 5f;
        [SerializeField, Min(.0001f)] private float crowdJitterThreshold = .005f;

        [SerializeField] private bool drawCrowdAgentGizmos;
        [SerializeField] private bool drawCrowdVelocityGizmos;
        [SerializeField, Min(0)] private int crowdAgentGizmoLimit = 512;

        private World _world;
        private LocalAvoidanceSimulation _simulation;
        private LocalAvoidanceDiagnostics _crowdDiagnostics;
        private CharacterSlotRegistry _characterSlots;
        private CharacterSpatialHash _characterSpatialHash;
        private HitscanResolutionQueue _hitscanResolutionQueue;
        private ProjectilePoolRegistry _projectilePools;
        private WeaponRegistry _weaponRegistry;
        private CharacterVisualRegistry _characterVisuals;
        private VirtualStickInputState _virtualStickInput;
        private ProjectileDiagnosticLog _projectileDiagnosticLog;
        private PerformanceLogService _performanceLog;

        private readonly Systems _systems = new();

        private void Awake()
        {
            ValidateSetup();
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var capacity = maxEnemies + 1024;
            _world = new World();
            if (logPerformance)
                _performanceLog = new PerformanceLogService(_world, performanceLogInterval);
#if !DISABLE_LITHEECS_DIAGNOSTICS
            _world.ResetWarmupProfile();
            _world.AllocationDiagnosticsEnabled = true;
            _world.ArchetypeCreatedLogger = Log;
            _world.TransitionCreatedLogger = Log;
#endif

#if ENABLE_DIAGNOSTICS_LOG
            _projectileDiagnosticLog =
 new ProjectileDiagnosticLog(logProjectileDiagnostics, projectileDiagnosticShotLimit);
#endif
            _virtualStickInput = new VirtualStickInputState();
            _characterSlots = new CharacterSlotRegistry(capacity);
            _simulation = new LocalAvoidanceSimulation(capacity, 0);
#if ENABLE_DIAGNOSTICS_LOG
            _crowdDiagnostics = new LocalAvoidanceDiagnostics(capacity);
#endif
            _projectilePools = new ProjectilePoolRegistry();
            var weaponCatalog = availableWeapons != null && availableWeapons.Length > 0
                ? availableWeapons
                : startingWeapons;
            _weaponRegistry = new WeaponRegistry(_world, weaponCatalog, _projectilePools);
            _characterSpatialHash = new CharacterSpatialHash(4f, capacity);
            _hitscanResolutionQueue = new HitscanResolutionQueue();
            _characterVisuals = CreateCharacterVisualRegistry();

            _systems.Add(new WarmupSystem(_world, capacity));
            _systems.Add(new PlayerSpawnSystem(
                _world, _characterSlots, playerDefinition, _characterVisuals, playerInvincible));
            _systems.Add(new WeaponInitializeSystem(_world, startingWeapons, _weaponRegistry));
            _systems.Add(new EnemySpawnSystem(_world, _characterSlots, _characterVisuals, enemyWaveSequence,
                minimumSpawnDistance, maximumSpawnDistance, maxEnemies));

            _systems.Add(new PlayerMovementSystem(
                _world, autopilotEnabled, gameCamera, _virtualStickInput));
            _systems.Add(new EnemyFollowSystem(_world, maxEnemies));
            _systems.Add(new CrowdMovementSystem(
                _world, _simulation, _crowdDiagnostics, maxEnemies + 1,
                crowdJitterThreshold));

            _systems.Add(new CollisionSystem(_world));
            _systems.Add(new ContactEventCleanupSystem(_world));
            _systems.Add(new ContactDamageSystem(_world));
            _systems.Add(new CharacterSpatialQuerySystem(_world, _characterSpatialHash));
            if (autopilotEnabled)
                _systems.Add(new AutopilotSystem(_world, _characterSpatialHash));
            _systems.Add(new FindTargetSystem(_world, _characterSpatialHash, targetSearchRadius));

            if (!disableFire)
                _systems.Add(new WeaponSystem(_world));
            _systems.Add(new AttackRequestSystem(_world, _characterSpatialHash, _projectilePools,
                _hitscanResolutionQueue, logHitscanDiagnostics, _projectileDiagnosticLog));
            _systems.Add(new ProjectileSystem(_world, _characterSpatialHash, _projectilePools,
                _projectileDiagnosticLog));
            _systems.Add(new ImpactVisualSystem(_world, _projectilePools));
            _systems.Add(new DamageSystem(_world, _hitscanResolutionQueue, hpGauge,
                logHitscanDiagnostics));
            _systems.Add(new DeathSystem(_world, _characterSlots, gameOverPanel, maxEnemies));
            _systems.Add(new CharacterAnimationSystem(_world));
            _systems.Add(new CharacterVisualFeedbackSystem(_world));
            _systems.Add(new CharacterBatchRenderSystem(_world, _characterVisuals, capacity));
            _systems.Add(new ExperiencePickupSystem(_world));
            _systems.Add(new ExperienceBatchRenderSystem(_world, experienceVisual, maxEnemies));
            _systems.Add(new ExperienceCollectSystem(_world));
            _systems.Add(new ExperienceGaugeSystem(_world, expGauge));
            _systems.Add(new LevelUpSystem(_world, upgradePanel, _weaponRegistry));
            _systems.Add(new WeaponUpgradeSystem(_world, _weaponRegistry));
            _systems.Add(new PlayerTransformSystem(_world));
            _systems.Add(new GamePauseSystem(_world));
            _systems.Add(new CameraFollowSystem(_world, gameCamera, cameraOffset,
                minimumCameraSize, maximumCameraSize, enemiesForMaximumCameraSize, cameraSharpness));
        }

        private void Start()
        {
            fps.Initialize(_world);
            upgradePanel.Initialize(_world, _weaponRegistry);
            virtualPad.Initialize(_virtualStickInput);
            foreach (var entry in _systems)
            {
                if (entry.Initializable == null) continue;

                entry.InitializeMarker.Begin();
                try
                {
                    entry.Initializable.Initialize();
                }
                finally
                {
                    entry.InitializeMarker.End();
                }
            }

            _performanceLog?.Begin();
        }

        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
#if !DISABLE_LITHEECS_DIAGNOSTICS
            _world.ResetAllocationDiagnostics();
#endif
            foreach (var entry in _systems)
            {
                if (entry.Tickable == null) continue;

                entry.TickMarker.Begin();
                try
                {
                    entry.Tickable.Tick(deltaTime);
                }
                catch (Exception e)
                {
                    throw new SystemException($"{entry.Tickable}", e);
                }
                finally
                {
                    entry.TickMarker.End();
                }
            }

            var alloc = recorder.CurrentValue;
            recorder.Dispose();
            if (Time.frameCount > 1)
            {
                fps.AddAlloc(alloc);
                _performanceLog?.RecordFrame(Time.unscaledDeltaTime, alloc);
            }
#if !DISABLE_LITHEECS_DIAGNOSTICS
            var d = _world.GetAllocationDiagnostics();
            if (d.HasEvents)
            {
                var text = _world.FormatAllocationDiagnostics(d);
                Log(text);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawCrowdAgentGizmos || !Application.isPlaying) return;
            var settings = LocalAvoidanceGizmoSettings.Default;
            settings.MaximumAgents = crowdAgentGizmoLimit;
            settings.DrawVelocity = drawCrowdVelocityGizmos;
            LocalAvoidanceGizmos.Draw(_simulation, settings);
        }
#endif

        private readonly struct SystemExecutionEntry
        {
            public readonly IInitializable Initializable;
            public readonly ITickable Tickable;
            public readonly IDisposable Disposable;
            public readonly ProfilerMarker InitializeMarker;
            public readonly ProfilerMarker TickMarker;

            public SystemExecutionEntry(ISystem system)
            {
                Initializable = system as IInitializable;
                Tickable = system as ITickable;
                Disposable = system as IDisposable;
                var name = system.GetType().Name;
                InitializeMarker = new ProfilerMarker($"{name}.Initialize");
                TickMarker = new ProfilerMarker($"{name}.Tick");
            }
        }

        private sealed class Systems : List<SystemExecutionEntry>
        {
            public void Add(ISystem system) => base.Add(new SystemExecutionEntry(system));
        }

        private void OnDestroy()
        {
            foreach (var entry in _systems)
            {
                entry.Disposable?.Dispose();
            }

            _performanceLog?.Dispose();
            _performanceLog = null;
            if (_world == null) return;
            _projectileDiagnosticLog?.Dispose();
            _projectileDiagnosticLog = null;

            _world.Dispose();
            _crowdDiagnostics?.Dispose();
            _crowdDiagnostics = null;
            _simulation?.Dispose();
            _projectilePools?.Dispose();
        }

        private void ValidateSetup()
        {
            if (playerDefinition == null || playerDefinition.Visual == null)
                throw new InvalidOperationException("Player Character Definition or Visual is not assigned.");
            if (experienceVisual == null || experienceVisual.Sprite == null)
                throw new InvalidOperationException("Experience Visual is not assigned or has no Sprite.");
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null)
                throw new InvalidOperationException("Game Camera is not assigned and no MainCamera exists.");
            if (maximumSpawnDistance < minimumSpawnDistance) maximumSpawnDistance = minimumSpawnDistance;
            if (maximumCameraSize < minimumCameraSize) maximumCameraSize = minimumCameraSize;
            if (enemyWaveSequence == null || enemyWaveSequence.WaveCount == 0)
                throw new InvalidOperationException("Enemy Wave Sequence is not assigned or has no waves.");
            for (var i = 0; i < enemyWaveSequence.WaveCount; i++)
            {
                var wave = enemyWaveSequence.GetWave(i);
                if (wave == null || !HasCharacterDefinition(wave.Candidates))
                    throw new InvalidOperationException($"Enemy wave {i + 1} has no valid character definition.");
            }
        }

        private static bool HasCharacterDefinition(CharacterSpawnCandidate[] candidates)
        {
            if (candidates == null) return false;
            for (var i = 0; i < candidates.Length; i++)
                if (candidates[i].definition != null)
                    return true;
            return false;
        }

        private CharacterVisualRegistry CreateCharacterVisualRegistry()
        {
            var registry = new CharacterVisualRegistry();
            registry.Register(playerDefinition.Visual);
            for (var waveIndex = 0; waveIndex < enemyWaveSequence.WaveCount; waveIndex++)
            {
                var wave = enemyWaveSequence.GetWave(waveIndex);
                var candidates = wave?.Candidates;
                if (candidates == null) continue;
                for (var i = 0; i < candidates.Length; i++)
                {
                    var definition = candidates[i].definition;
                    if (definition != null && definition.Visual != null)
                        registry.Register(definition.Visual);
                }
            }

            return registry;
        }

        static void Log(string text)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0} {1}", Time.frameCount, text);
        }
    }
}
