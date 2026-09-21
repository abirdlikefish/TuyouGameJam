using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour, ILevelRuntime, IGameplayHudSource
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
        private SubscriptionToken monsterKilledSubscription;
        private RoadLayoutSnapshot roadLayout;
        private GameplayHudSnapshot completedHudSnapshot;
        private LevelRunState runState;
        private string levelDisplayName;
        private int levelId;
        private int levelRunId;
        private int killedEnemyCount;
        private int totalEnemyCount;
        private float elapsedTime;
        private bool sessionInitialized;
        private bool completionSubmitted;
        private bool hasCompletedHudSnapshot;
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
            RoadView initializedRoadView,
            Vector3 initializedArmySpawnPosition)
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
            levelDisplayName = levelConfig.DisplayName;
            totalEnemyCount = levelConfig.EnemySpawns.Count;
            killedEnemyCount = 0;
            elapsedTime = 0f;
            runState = LevelRunState.Preparing;
            completedHudSnapshot = default(GameplayHudSnapshot);
            hasCompletedHudSnapshot = false;
            roadLayout = CreateRoadLayout(levelConfig, roadView, initializedArmySpawnPosition);
            sessionInitialized = true;

            try
            {
                inputController.SetGameplayEnabled(false);

                bulletStarted = true;
                bulletManager.StartRun(levelRunId, roadLayout);
                armyStarted = true;
                army.StartRun(levelRunId, roadLayout);
                enemyStarted = true;
                enemyManager.StartRun(levelRunId, roadLayout, levelConfig.IkunBasketballConfigId);
                obstacleStarted = true;
                obstacleManager.StartRun(levelRunId, roadLayout);
                spawnStarted = true;
                spawnManager.StartRun(levelConfig, roadLayout, levelRunId);

                levelRunStartedSubscription = eventBus.Subscribe<LevelRunStarted>(OnLevelRunStarted);
                monsterKilledSubscription = eventBus.Subscribe<MonsterKilled>(OnMonsterKilled);
            }
            catch
            {
                StopModules();
                UnsubscribeFromEvents();
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

        public GameplayHudSnapshot GetHudSnapshot()
        {
            if (!sessionInitialized)
            {
                throw new InvalidOperationException(
                    "LevelManager must be initialized before reading the Gameplay HUD snapshot.");
            }

            return hasCompletedHudSnapshot
                ? completedHudSnapshot
                : CreateHudSnapshot(false);
        }

        public void StopRun()
        {
            if (!sessionInitialized)
            {
                UnsubscribeFromEvents();
                return;
            }

            runState = LevelRunState.Completed;
            inputController.SetGameplayEnabled(false);
            StopModules();
            UnsubscribeFromEvents();
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
            enemyManager.ApplyPendingDisplacements(levelRunId);
            Physics2D.SyncTransforms();
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

        private void OnMonsterKilled(MonsterKilled killed)
        {
            if (!sessionInitialized || runState != LevelRunState.Playing ||
                killed.LevelRunId != levelRunId)
            {
                return;
            }

            killedEnemyCount = Mathf.Min(totalEnemyCount, killedEnemyCount + 1);
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
            completedHudSnapshot = CreateHudSnapshot(true);
            hasCompletedHudSnapshot = true;
            var preserveArmyVisuals = armyStarted;
            if (preserveArmyVisuals)
            {
                if (result == LevelResult.Victory)
                {
                    // 胜利表现保留到 Gameplay 场景卸载；OnDestroy/显式 StopRun 仍负责最终清理。
                    army.EnterVictoryPresentation(levelRunId);
                }
                else
                {
                    // GameOver 立即提交，但保留最后一批死亡动画，避免 StopRun 当帧清空表现。
                    army.EnterDefeatPresentation(levelRunId);
                }
            }

            StopModules(preserveArmyVisuals);
            UnsubscribeFromEvents();
            gameStateService.CompleteGameplay(new LevelCompletion(levelId, levelRunId, result));
        }

        private GameplayHudSnapshot CreateHudSnapshot(bool isCompleted)
        {
            var elements = army.GetElementStateSnapshot();
            return new GameplayHudSnapshot(
                levelId,
                levelRunId,
                levelDisplayName,
                elapsedTime,
                elements.FireRemainingDuration,
                elements.IceRemainingDuration,
                elements.LightningRemainingDuration,
                killedEnemyCount,
                totalEnemyCount,
                isCompleted);
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

        private void UnsubscribeFromEvents()
        {
            if (eventBus == null)
            {
                return;
            }

            if (monsterKilledSubscription.IsValid)
            {
                eventBus.Unsubscribe(monsterKilledSubscription);
                monsterKilledSubscription = default(SubscriptionToken);
            }

            if (levelRunStartedSubscription.IsValid)
            {
                eventBus.Unsubscribe(levelRunStartedSubscription);
                levelRunStartedSubscription = default(SubscriptionToken);
            }
        }

        private void OnDestroy()
        {
            StopRun();
        }

        private static RoadLayoutSnapshot CreateRoadLayout(
            LevelConfigSnapshot levelConfig,
            RoadView roadView,
            Vector3 armySpawnPosition)
        {
            if (!IsFinite(armySpawnPosition.x) || !IsFinite(armySpawnPosition.y))
            {
                throw new ArgumentException(
                    "Army spawn point must have finite world X and Y coordinates.",
                    nameof(armySpawnPosition));
            }

            // 道路尺寸和 Army 出生点属于场景装配数据，不再从 LevelConfig 读取。
            var roadSize = roadView.GetWorldSize();
            var layout = new RoadLayoutSnapshot(
                roadSize.x,
                roadSize.y,
                new Vector2(armySpawnPosition.x, armySpawnPosition.y),
                levelConfig.SpawnY,
                levelConfig.EnemyApproachY,
                levelConfig.DespawnY,
                levelConfig.BulletDespawnY);

            ValidateRoadLayout(layout);
            return layout;
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

            if (!IsFinite(levelConfig.SpawnY) ||
                !IsFinite(levelConfig.EnemyApproachY) ||
                !IsFinite(levelConfig.DespawnY) ||
                !IsFinite(levelConfig.BulletDespawnY))
            {
                throw new ArgumentException("Level config contains invalid vertical lines.", nameof(levelConfig));
            }
        }

        private static void ValidateRoadLayout(RoadLayoutSnapshot layout)
        {
            var armySpawnPosition = layout.ArmySpawnPosition;
            if (!IsFinite(layout.Width) || !IsFinite(layout.Height) ||
                layout.Width <= 0f || layout.Height <= 0f ||
                !IsFinite(armySpawnPosition.x) || !IsFinite(armySpawnPosition.y) ||
                armySpawnPosition.x < layout.LeftBoundary ||
                armySpawnPosition.x > layout.RightBoundary ||
                armySpawnPosition.y < layout.BottomBoundary ||
                armySpawnPosition.y > layout.TopBoundary ||
                layout.DespawnY < layout.BottomBoundary ||
                layout.DespawnY >= armySpawnPosition.y ||
                layout.EnemyApproachY <= armySpawnPosition.y ||
                layout.EnemyApproachY >= layout.SpawnY ||
                layout.SpawnY > layout.TopBoundary ||
                layout.BulletDespawnY <= armySpawnPosition.y ||
                layout.BulletDespawnY > layout.TopBoundary)
            {
                throw new ArgumentException(
                    "Scene layout must satisfy positive centered road dimensions, an Army spawn point inside " +
                    "the road, and BottomBoundary <= DespawnY < ArmySpawnPoint.y < EnemyApproachY < " +
                    "SpawnY <= TopBoundary plus ArmySpawnPoint.y < BulletDespawnY <= TopBoundary.",
                    nameof(layout));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
