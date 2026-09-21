using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Foundation;
using Game.Gameplay;
using Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplaySceneEntry : MonoBehaviour, IAppSceneEntry
    {
        private const int MvpArmyId = 0;

        [Header("关卡协调")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField] private RoadView roadView;

        [Header("玩法模块")]
        [SerializeField] private BulletManager bulletManager;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private ObstacleManager obstacleManager;
        [SerializeField] private GameplayInputAdapter inputAdapter;
        [SerializeField] private TouchDragInput touchDragInput;

        [Header("固定容器")]
        [SerializeField] private Transform armySpawnPoint;
        [SerializeField] private Transform armyContainer;
        [SerializeField] private Transform bulletRoot;
        [SerializeField] private Transform monsterRoot;
        [SerializeField] private Transform obstacleRoot;

        [Header("Army Prefab 绑定")]
        [SerializeField] private ArmyPrefabBinding[] armyPrefabBindings = new ArmyPrefabBinding[0];

        [Header("输入 UI")]
        [SerializeField] private Canvas gameplayCanvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private StandaloneInputModule standaloneInputModule;

        [Header("战斗 UI")]
        [SerializeField] private BattleHudView battleHudView;
        [SerializeField] private BattleResultView battleResultView;

        [Header("关卡开场视频")]
        [SerializeField] private LevelIntroVideoView levelIntroVideoView;
        [SerializeField] private LevelIntroVideoBinding[] levelIntroVideoBindings =
            new LevelIntroVideoBinding[0];

        private ArmyController armyInstance;
        private IEventBus eventBus;
        private SubscriptionToken sceneReadySubscription;
        private int levelId;
        private int levelRunId;
        private bool introStarted;
        private bool levelInitialized;
        private bool initialized;

        AppSceneId IAppSceneEntry.SceneId => AppSceneId.Gameplay;

        void IAppSceneEntry.Initialize(
            SceneRuntimeLoadRequest request,
            SceneRuntimeDependencies dependencies)
        {
            if (initialized || armyInstance != null)
            {
                throw new InvalidOperationException("GameplaySceneEntry is already initialized.");
            }

            ValidateContext(request);
            ValidateSceneBindings();
            var armyPrefab = ResolveArmyPrefab(MvpArmyId);
            var introVideoClip = ResolveLevelIntroVideoClip(request.LevelId);
            var armyConfig = dependencies.ConfigService.GetArmyConfig(MvpArmyId);
            eventBus = dependencies.EventBus;
            levelId = request.LevelId;
            levelRunId = request.LevelRunId;
            introStarted = false;

            try
            {
                armyInstance = Instantiate(armyPrefab, armyContainer, false);
                armyInstance.name = armyPrefab.name;
                armyInstance.transform.rotation = Quaternion.identity;

                if (!armyInstance.TryValidate(out var armyError))
                {
                    throw new InvalidOperationException(armyError);
                }

                // Manager 初始化顺序固定；LevelManager.Initialize 是唯一开始 Preparing/StartRun 的入口。
                bulletManager.Initialize(dependencies.PoolService, dependencies.ConfigService);
                armyInstance.Initialize(
                    MvpArmyId,
                    armyConfig,
                    dependencies.ConfigService,
                    bulletManager,
                    dependencies.EventBus);
                inputAdapter.Initialize(armyInstance);
                enemyManager.Initialize(
                    dependencies.PoolService,
                    dependencies.ConfigService,
                    armyInstance,
                    dependencies.EventBus);
                obstacleManager.Initialize(
                    dependencies.PoolService,
                    dependencies.ConfigService,
                    armyInstance,
                    dependencies.EventBus);
                spawnManager.Initialize(enemyManager, obstacleManager);
                levelManager.Initialize(
                    request.LevelConfig,
                    request.LevelId,
                    request.LevelRunId,
                    dependencies.GameStateService,
                    dependencies.TimeService,
                    dependencies.EventBus,
                    spawnManager,
                    armyInstance,
                    bulletManager,
                    enemyManager,
                    obstacleManager,
                    inputAdapter,
                    roadView,
                    armySpawnPoint.position);
                levelInitialized = true;
                battleHudView.Initialize(levelManager, dependencies.GameStateService);
                battleResultView.Initialize(
                    request.LevelRunId,
                    levelManager,
                    dependencies.GameStateService,
                    dependencies.EventBus);
                levelIntroVideoView.Initialize(introVideoClip, OnLevelIntroFinished);
                sceneReadySubscription = eventBus.Subscribe<AppSceneReady>(OnAppSceneReady);
                initialized = true;
                Debug.Log(
                    $"[GameplaySceneEntry] Initialized; LevelId={request.LevelId}; " +
                    $"LevelRunId={request.LevelRunId}");
            }
            catch
            {
                CleanupInternal();
                throw;
            }
        }

        void IAppSceneEntry.Cleanup()
        {
            CleanupInternal();
        }

        private void ValidateSceneBindings()
        {
            RequireSceneComponent(levelManager, nameof(levelManager));
            RequireSceneComponent(spawnManager, nameof(spawnManager));
            RequireSceneComponent(roadView, nameof(roadView));
            RequireSceneComponent(bulletManager, nameof(bulletManager));
            RequireSceneComponent(enemyManager, nameof(enemyManager));
            RequireSceneComponent(obstacleManager, nameof(obstacleManager));
            RequireSceneComponent(inputAdapter, nameof(inputAdapter));
            RequireSceneComponent(touchDragInput, nameof(touchDragInput));
            RequireSceneComponent(gameplayCanvas, nameof(gameplayCanvas));
            RequireSceneComponent(graphicRaycaster, nameof(graphicRaycaster));
            RequireSceneComponent(eventSystem, nameof(eventSystem));
            RequireSceneComponent(standaloneInputModule, nameof(standaloneInputModule));
            RequireSceneComponent(battleHudView, nameof(battleHudView));
            RequireSceneComponent(battleResultView, nameof(battleResultView));
            RequireSceneComponent(levelIntroVideoView, nameof(levelIntroVideoView));
            RequireSceneTransform(armySpawnPoint, nameof(armySpawnPoint));
            RequireSceneTransform(armyContainer, nameof(armyContainer));
            RequireSceneTransform(bulletRoot, nameof(bulletRoot));
            RequireSceneTransform(monsterRoot, nameof(monsterRoot));
            RequireSceneTransform(obstacleRoot, nameof(obstacleRoot));

            if (bulletManager.transform != bulletRoot || enemyManager.transform != monsterRoot ||
                obstacleManager.transform != obstacleRoot)
            {
                throw new InvalidOperationException(
                    "BulletManager, EnemyManager and ObstacleManager must be attached to their bound fixed roots.");
            }

            if (gameplayCanvas.gameObject != graphicRaycaster.gameObject)
            {
                throw new InvalidOperationException("Gameplay Canvas and GraphicRaycaster must share one GameObject.");
            }

            if (eventSystem.gameObject != standaloneInputModule.gameObject)
            {
                throw new InvalidOperationException("EventSystem and StandaloneInputModule must share one GameObject.");
            }

            if (inputAdapter.TouchDragInput != touchDragInput)
            {
                throw new InvalidOperationException(
                    "GameplayInputAdapter must reference the TouchDragInput bound by GameplaySceneEntry.");
            }

            if (touchDragInput.transform.parent != gameplayCanvas.transform ||
                battleHudView.transform.parent != gameplayCanvas.transform ||
                levelIntroVideoView.transform.parent != gameplayCanvas.transform ||
                battleResultView.transform.parent != gameplayCanvas.transform)
            {
                throw new InvalidOperationException(
                    "TouchDragInput, BattleHudView, LevelIntroVideoView and BattleResultView must be " +
                    "direct Gameplay Canvas children.");
            }

            if (touchDragInput.transform.GetSiblingIndex() >= battleHudView.transform.GetSiblingIndex() ||
                battleHudView.transform.GetSiblingIndex() >= levelIntroVideoView.transform.GetSiblingIndex() ||
                levelIntroVideoView.transform.GetSiblingIndex() >= battleResultView.transform.GetSiblingIndex())
            {
                throw new InvalidOperationException(
                    "Gameplay Canvas order must be TouchDragArea, BattleHud, LevelIntroVideo, then BattleResult.");
            }

            if (armyContainer.childCount != 0)
            {
                throw new InvalidOperationException("ArmyContainer must be empty before scene entry initialization.");
            }

            if (!roadView.TryValidate(out var roadError))
            {
                throw new InvalidOperationException(roadError);
            }

            if (!bulletManager.TryValidate(out var bulletError))
            {
                throw new InvalidOperationException(bulletError);
            }

            if (!enemyManager.TryValidate(out var enemyError))
            {
                throw new InvalidOperationException(enemyError);
            }

            if (!obstacleManager.TryValidate(out var obstacleError))
            {
                throw new InvalidOperationException(obstacleError);
            }

            if (!inputAdapter.TryValidate(out var inputError))
            {
                throw new InvalidOperationException(inputError);
            }

            if (!touchDragInput.TryValidate(out var touchError))
            {
                throw new InvalidOperationException(touchError);
            }

            if (!battleHudView.TryValidate(out var battleHudError))
            {
                throw new InvalidOperationException(battleHudError);
            }

            if (!battleResultView.TryValidate(out var battleResultError))
            {
                throw new InvalidOperationException(battleResultError);
            }

            if (!levelIntroVideoView.TryValidate(out var introVideoError))
            {
                throw new InvalidOperationException(introVideoError);
            }
        }

        private ArmyController ResolveArmyPrefab(int armyId)
        {
            if (armyPrefabBindings == null)
            {
                throw new InvalidOperationException("Army prefab bindings cannot be null.");
            }

            var seenIds = new HashSet<int>();
            ArmyController selectedPrefab = null;
            for (var index = 0; index < armyPrefabBindings.Length; index++)
            {
                var binding = armyPrefabBindings[index];
                if (binding == null)
                {
                    throw new InvalidOperationException($"Army prefab binding at index {index} is null.");
                }

                if (binding.ArmyId < 0 || !seenIds.Add(binding.ArmyId))
                {
                    throw new InvalidOperationException(
                        $"Army prefab binding ID {binding.ArmyId} is invalid or duplicated.");
                }

                var prefab = binding.Prefab;
                if (prefab == null || prefab.gameObject.scene.IsValid() || prefab.transform.parent != null)
                {
                    throw new InvalidOperationException(
                        $"Army prefab binding {binding.ArmyId} must reference a prefab root asset.");
                }

                if (!prefab.TryValidate(out var prefabError))
                {
                    throw new InvalidOperationException(prefabError);
                }

                if (binding.ArmyId == armyId)
                {
                    selectedPrefab = prefab;
                }
            }

            if (selectedPrefab == null)
            {
                throw new InvalidOperationException($"Army prefab binding for ArmyId {armyId} is missing.");
            }

            return selectedPrefab;
        }

        private VideoClip ResolveLevelIntroVideoClip(int requestedLevelId)
        {
            if (levelIntroVideoBindings == null)
            {
                throw new InvalidOperationException("Level intro video bindings cannot be null.");
            }

            var seenIds = new HashSet<int>();
            VideoClip selectedClip = null;
            for (var index = 0; index < levelIntroVideoBindings.Length; index++)
            {
                var binding = levelIntroVideoBindings[index];
                if (binding == null)
                {
                    throw new InvalidOperationException(
                        $"Level intro video binding at index {index} is null.");
                }

                if (binding.LevelId < 0 || !seenIds.Add(binding.LevelId))
                {
                    throw new InvalidOperationException(
                        $"Level intro video binding ID {binding.LevelId} is invalid or duplicated.");
                }

                if (binding.VideoClip == null)
                {
                    throw new InvalidOperationException(
                        $"Level intro video binding {binding.LevelId} has no VideoClip.");
                }

                if (binding.LevelId == requestedLevelId)
                {
                    selectedClip = binding.VideoClip;
                }
            }

            return selectedClip;
        }

        private void OnAppSceneReady(AppSceneReady ready)
        {
            if (!initialized || introStarted || ready.SceneId != AppSceneId.Gameplay ||
                ready.LevelId != levelId || ready.LevelRunId != levelRunId)
            {
                return;
            }

            introStarted = true;
            UnsubscribeFromSceneReady();
            levelIntroVideoView.BeginPlayback();
        }

        private void OnLevelIntroFinished(LevelIntroEndReason reason)
        {
            if (!initialized || eventBus == null)
            {
                return;
            }

            touchDragInput.ResetInput();
            eventBus.Publish(new LevelIntroFinished(levelId, levelRunId, reason));
        }

        private void CleanupInternal()
        {
            UnsubscribeFromSceneReady();
            if (levelIntroVideoView != null)
            {
                levelIntroVideoView.Cleanup();
            }

            if (battleResultView != null)
            {
                battleResultView.Cleanup();
            }

            if (battleHudView != null)
            {
                battleHudView.Cleanup();
            }

            if (levelInitialized && levelManager != null)
            {
                levelManager.StopRun();
            }

            levelInitialized = false;
            if (inputAdapter != null)
            {
                inputAdapter.SetGameplayEnabled(false);
            }

            if (armyInstance != null)
            {
                Destroy(armyInstance.gameObject);
                armyInstance = null;
            }

            if (initialized)
            {
                Debug.Log("[GameplaySceneEntry] Cleaned");
            }

            eventBus = null;
            levelId = 0;
            levelRunId = 0;
            introStarted = false;
            initialized = false;
        }

        private void UnsubscribeFromSceneReady()
        {
            if (eventBus == null || !sceneReadySubscription.IsValid)
            {
                return;
            }

            eventBus.Unsubscribe(sceneReadySubscription);
            sceneReadySubscription = default(SubscriptionToken);
        }

        private void RequireSceneComponent(Component component, string fieldName)
        {
            if (component == null)
            {
                throw new InvalidOperationException($"GameplaySceneEntry.{fieldName} is not assigned.");
            }

            if (component.gameObject.scene != gameObject.scene || !component.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    $"GameplaySceneEntry.{fieldName} must belong to this GameplayRoot hierarchy.");
            }
        }

        private void RequireSceneTransform(Transform target, string fieldName)
        {
            if (target == null)
            {
                throw new InvalidOperationException($"GameplaySceneEntry.{fieldName} is not assigned.");
            }

            if (target.gameObject.scene != gameObject.scene || !target.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    $"GameplaySceneEntry.{fieldName} must belong to this GameplayRoot hierarchy.");
            }
        }

        private static void ValidateContext(SceneRuntimeLoadRequest request)
        {
            if (request.SceneId != AppSceneId.Gameplay || request.LevelConfig == null ||
                request.LevelId < 0 || request.LevelRunId <= 0 ||
                request.LevelConfig.LevelId != request.LevelId)
            {
                throw new ArgumentException("Gameplay scene context is invalid.", nameof(request));
            }
        }

        private void OnDestroy()
        {
            CleanupInternal();
        }
    }

    [Serializable]
    public sealed class ArmyPrefabBinding
    {
        [SerializeField] private int armyId;
        [SerializeField] private ArmyController prefab;

        public int ArmyId => armyId;
        public ArmyController Prefab => prefab;
    }

    [Serializable]
    public sealed class LevelIntroVideoBinding
    {
        [SerializeField] private int levelId;
        [SerializeField] private VideoClip videoClip;

        public int LevelId => levelId;
        public VideoClip VideoClip => videoClip;
    }
}
