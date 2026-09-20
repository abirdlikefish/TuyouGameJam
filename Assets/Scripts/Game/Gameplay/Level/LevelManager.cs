using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour, ILevelRuntime
    {
        private IGameStateService gameStateService;
        private ITimeService timeService;
        private IEventBus eventBus;
        private ISpawnManager spawnManager;
        private IArmyRunController army;
        private IBulletManager bulletManager;
        private IEnemyManager enemyManager;
        private IObstacleManager obstacleManager;
        private IGameplayInputController inputController;
        private RoadView roadView;
        private SubscriptionToken levelRunStartedSubscription;
        private RoadLayoutSnapshot roadLayout;
        private LevelRunState runState;
        private int levelId;
        private int levelRunId;
        private float elapsedTime;
        private bool sessionInitialized;
        private bool completionSubmitted;
        private bool bulletStarted;
        private bool armyStarted;
        private bool enemyStarted;
        private bool obstacleStarted;
        private bool spawnStarted;

        public void Initialize(
            LevelConfigSnapshot levelConfig,
            int initializedLevelId,
            int initializedLevelRunId,
            IGameStateService initializedGameStateService,
            ITimeService initializedTimeService,
            IEventBus initializedEventBus,
            ISpawnManager initializedSpawnManager,
            IArmyRunController initializedArmy,
            IBulletManager initializedBulletManager,
            IEnemyManager initializedEnemyManager,
            IObstacleManager initializedObstacleManager,
            IGameplayInputController initializedInputController,
            RoadView initializedRoadView)
        {
            if (sessionInitialized)
            {
                throw new InvalidOperationException("LevelManager is already initialized for a run.");
            }

            ValidateSession(levelConfig, initializedLevelId, initializedLevelRunId);
            gameStateService = initializedGameStateService ??
                throw new ArgumentNullException(nameof(initializedGameStateService));
            timeService = initializedTimeService ??
                throw new ArgumentNullException(nameof(initializedTimeService));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            spawnManager = initializedSpawnManager ??
                throw new ArgumentNullException(nameof(initializedSpawnManager));
            army = initializedArmy ?? throw new ArgumentNullException(nameof(initializedArmy));
            bulletManager = initializedBulletManager ??
                throw new ArgumentNullException(nameof(initializedBulletManager));
            enemyManager = initializedEnemyManager ??
                throw new ArgumentNullException(nameof(initializedEnemyManager));
            obstacleManager = initializedObstacleManager ??
                throw new ArgumentNullException(nameof(initializedObstacleManager));
            inputController = initializedInputController ??
                throw new ArgumentNullException(nameof(initializedInputController));
            roadView = initializedRoadView ?? throw new ArgumentNullException(nameof(initializedRoadView));

            if (!roadView.TryValidate(out var roadError))
            {
                throw new InvalidOperationException(roadError);
            }

            levelId = initializedLevelId;
            levelRunId = initializedLevelRunId;
            elapsedTime = 0f;
            runState = LevelRunState.Preparing;
            roadLayout = CreateRoadLayout(levelConfig);
            sessionInitialized = true;

            try
            {
                inputController.SetGameplayEnabled(false);
                roadView.ApplyLayout(roadLayout);

                bulletStarted = true;
                bulletManager.StartRun(levelRunId, roadLayout);
                armyStarted = true;
                army.StartRun(levelRunId, roadLayout);
                enemyStarted = true;
                enemyManager.StartRun(levelRunId, roadLayout);
                obstacleStarted = true;
                obstacleManager.StartRun(levelRunId, roadLayout);
                spawnStarted = true;
                spawnManager.StartRun(levelConfig, roadLayout, levelRunId);

                levelRunStartedSubscription = eventBus.Subscribe<LevelRunStarted>(OnLevelRunStarted);
            }
            catch
            {
                StopModules();
                UnsubscribeFromRunStart();
                sessionInitialized = false;
                levelRunId = 0;
                throw;
            }
        }

        public LevelRunState GetRunState()
        {
            return runState;
        }

        public int GetLevelRunId()
        {
            return levelRunId;
        }

        public float GetElapsedTime()
        {
            return elapsedTime;
        }

        public RoadLayoutSnapshot GetRoadLayout()
        {
            return roadLayout;
        }

        public void StopRun()
        {
            if (!sessionInitialized)
            {
                UnsubscribeFromRunStart();
                return;
            }

            runState = LevelRunState.Completed;
            inputController.SetGameplayEnabled(false);
            StopModules();
            UnsubscribeFromRunStart();
        }

        private void Update()
        {
            if (!sessionInitialized || runState != LevelRunState.Playing)
            {
                return;
            }

            var realTimeDelta = timeService.GetDeltaTime(TimeDomain.RealTime);
            var gameplayDelta = timeService.GetDeltaTime(TimeDomain.Gameplay);
            var bulletDelta = timeService.GetDeltaTime(TimeDomain.Bullet);
            var gateDelta = timeService.GetDeltaTime(TimeDomain.Gate);
            var monsterDelta = timeService.GetDeltaTime(TimeDomain.Monster);

            // 该顺序是玩法确定性边界，不能拆到各 Manager 的独立 Update。
            inputController.TickInput(realTimeDelta);
            elapsedTime += gameplayDelta;
            spawnManager.Tick(levelRunId, elapsedTime);
            army.TickMovementAndFire(levelRunId, gameplayDelta);
            enemyManager.TickMovement(levelRunId, monsterDelta);
            obstacleManager.TickMovement(levelRunId, gateDelta);
            Physics2D.SyncTransforms();
            bulletManager.TickMovementAndHits(levelRunId, bulletDelta);
            obstacleManager.ResolveContacts(levelRunId);
            enemyManager.ResolveAttacks(levelRunId, monsterDelta);
            bulletManager.FlushPendingRecycles(levelRunId);
            obstacleManager.FlushPendingRecycles(levelRunId);
            enemyManager.FlushPendingRecycles(levelRunId);
            EvaluateCompletion();
        }

        private void OnLevelRunStarted(LevelRunStarted started)
        {
            if (!sessionInitialized ||
                runState != LevelRunState.Preparing ||
                started.LevelId != levelId ||
                started.LevelRunId != levelRunId)
            {
                return;
            }

            runState = LevelRunState.Playing;
            elapsedTime = 0f;
            spawnManager.Tick(levelRunId, 0f);
            inputController.SetGameplayEnabled(true);
        }

        private void EvaluateCompletion()
        {
            if (army.GetArmyCount() <= 0)
            {
                Complete(LevelResult.GameOver);
                return;
            }

            if (spawnManager.AreAllEnemySpawnsDispatched(levelRunId) &&
                enemyManager.GetAliveEnemyCount() == 0)
            {
                Complete(LevelResult.Victory);
            }
        }

        private void Complete(LevelResult result)
        {
            if (runState == LevelRunState.Completed || completionSubmitted)
            {
                return;
            }

            runState = LevelRunState.Completed;
            completionSubmitted = true;
            inputController.SetGameplayEnabled(false);
            var preserveArmyVisuals = result == LevelResult.Victory && armyStarted;
            if (preserveArmyVisuals)
            {
                // 胜利表现保留到 Gameplay 场景卸载；OnDestroy/显式 StopRun 仍负责最终清理。
                army.EnterVictoryPresentation(levelRunId);
            }

            StopModules(preserveArmyVisuals);
            UnsubscribeFromRunStart();
            gameStateService.CompleteGameplay(new LevelCompletion(levelId, levelRunId, result));
        }

        private void StopModules(bool preserveArmyVisuals = false)
        {
            if (spawnStarted)
            {
                spawnManager.StopRun(levelRunId);
                spawnStarted = false;
            }

            if (obstacleStarted)
            {
                obstacleManager.StopRun(levelRunId);
                obstacleStarted = false;
            }

            if (enemyStarted)
            {
                enemyManager.StopRun(levelRunId);
                enemyStarted = false;
            }

            if (armyStarted && !preserveArmyVisuals)
            {
                army.StopRun(levelRunId);
                armyStarted = false;
            }

            if (bulletStarted)
            {
                bulletManager.StopRun(levelRunId);
                bulletStarted = false;
            }
        }

        private void UnsubscribeFromRunStart()
        {
            if (!levelRunStartedSubscription.IsValid || eventBus == null)
            {
                return;
            }

            eventBus.Unsubscribe(levelRunStartedSubscription);
            levelRunStartedSubscription = default(SubscriptionToken);
        }

        private void OnDestroy()
        {
            StopRun();
        }

        private static RoadLayoutSnapshot CreateRoadLayout(LevelConfigSnapshot levelConfig)
        {
            var bounds = levelConfig.RoadBounds;
            return new RoadLayoutSnapshot(
                bounds.width,
                bounds.height,
                bounds.xMin,
                bounds.xMax,
                bounds.yMin,
                bounds.yMax,
                levelConfig.SpawnY,
                levelConfig.EnemyApproachY,
                levelConfig.DespawnY);
        }

        private static void ValidateSession(
            LevelConfigSnapshot levelConfig,
            int initializedLevelId,
            int initializedLevelRunId)
        {
            if (levelConfig == null)
            {
                throw new ArgumentNullException(nameof(levelConfig));
            }

            if (initializedLevelId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initializedLevelId));
            }

            if (initializedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initializedLevelRunId));
            }

            if (levelConfig.LevelId != initializedLevelId)
            {
                throw new ArgumentException(
                    "Level config ID must match the initialized Level ID.",
                    nameof(levelConfig));
            }

            var bounds = levelConfig.RoadBounds;
            if (!IsFinite(bounds.x) || !IsFinite(bounds.y) ||
                !IsFinite(bounds.width) || !IsFinite(bounds.height) ||
                bounds.width <= 0f || bounds.height <= 0f)
            {
                throw new ArgumentException("Level config contains invalid road bounds.", nameof(levelConfig));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
