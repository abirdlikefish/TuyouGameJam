using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Foundation;
using Game.Gameplay;
using Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        private ArmyController armyInstance;
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
            var armyConfig = dependencies.ConfigService.GetArmyConfig(MvpArmyId);

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
                battleResultView.transform.parent != gameplayCanvas.transform)
            {
                throw new InvalidOperationException(
                    "TouchDragInput, BattleHudView and BattleResultView must be direct Gameplay Canvas children.");
            }

            if (touchDragInput.transform.GetSiblingIndex() >= battleHudView.transform.GetSiblingIndex() ||
                battleHudView.transform.GetSiblingIndex() >= battleResultView.transform.GetSiblingIndex())
            {
                throw new InvalidOperationException(
                    "Gameplay Canvas order must be TouchDragArea, BattleHud, then BattleResult.");
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

        private void CleanupInternal()
        {
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

            initialized = false;
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
}
