using System;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;

namespace Game.Composition
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GlobalBootstrap : MonoBehaviour
    {
        [Header("已有场景组件")]
        [SerializeField] private TimeService timeService;
        [SerializeField] private UnitySceneRuntime sceneRuntime;

        [Header("配置资产")]
        [SerializeField] private LevelCatalog levelCatalog;
        [SerializeField] private UnityResourceRegistry resourceRegistry;

        [Header("常驻容器")]
        [SerializeField] private Transform persistentPoolRoot;

        private static GlobalBootstrap activeInstance;

        private EventBus eventBus;
        private ConfigService configService;
        private PoolService poolService;
        private SceneService sceneService;
        private GameStateService gameStateService;
        private bool created;
        private bool connected;
        private bool started;
        private bool ownsActiveInstance;
        private bool shuttingDown;

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                Debug.LogWarning(
                    $"[GlobalBootstrap] Duplicate bootstrap rejected on '{name}'. " +
                    $"Active='{activeInstance.name}'.");
                enabled = false;
                Destroy(gameObject);
                return;
            }

            activeInstance = this;
            ownsActiveInstance = true;

            try
            {
                ValidateSerializedBindings();
                CreateServices();
            }
            catch (Exception exception)
            {
                FailStartup(exception);
            }
        }

        private System.Collections.IEnumerator Start()
        {
            // 让 Unity 完成首场景的加载提交；首个 Start 帧内调用 LoadScene(Additive)
            // 仍可能把目标场景留到下一帧才标记为 isLoaded。
            yield return null;

            if (!ownsActiveInstance || shuttingDown || !created)
            {
                yield break;
            }

            try
            {
                // Unity 2022 在首场景 Awake 阶段尚未稳定报告 Scene.isLoaded；
                // Connect 与首次 Additive 加载延后到 Start，保留严格场景校验。
                ConnectServices();
                DontDestroyOnLoad(gameObject);
                StartServices();
            }
            catch (Exception exception)
            {
                FailStartup(exception);
            }
        }

        private void CreateServices()
        {
            eventBus = new EventBus(Debug.LogException);
            configService = new ConfigService();
            poolService = new PoolService(
                persistentPoolRoot,
                message => Debug.LogError($"[PoolService] {message}"));
            sceneService = new SceneService(eventBus, sceneRuntime);
            gameStateService = new GameStateService(
                configService,
                sceneService,
                timeService,
                eventBus);
            created = true;
            Debug.Log("[GlobalBootstrap] Phase=Create; Succeeded=True");
        }

        private void ConnectServices()
        {
            if (!resourceRegistry.TryInitialize(out var registryError))
            {
                throw new InvalidOperationException(registryError);
            }

            var dependencies = new SceneRuntimeDependencies(
                gameStateService,
                timeService,
                eventBus,
                configService,
                poolService);
            if (!sceneRuntime.TryConnect(dependencies, out var sceneError))
            {
                throw new InvalidOperationException(sceneError);
            }

            connected = true;
            Debug.Log("[GlobalBootstrap] Phase=Connect; Succeeded=True");
        }

        private void StartServices()
        {
            gameStateService.Start();
            started = true;

            configService.Initialize(levelCatalog, LubanTables.Instance, resourceRegistry);
            if (configService.GetConfigLoadState() != ConfigLoadState.Ready)
            {
                return;
            }

            Debug.Log("[GlobalBootstrap] Phase=Start; ConfigState=Ready");
            gameStateService.NotifyInitializationReady();
        }

        private void ValidateSerializedBindings()
        {
            if (transform.parent != null)
            {
                throw new InvalidOperationException("GlobalBootstrap must be attached to the GlobalRoot scene root.");
            }

            if (timeService == null)
            {
                throw new InvalidOperationException("GlobalBootstrap.timeService is not assigned.");
            }

            if (sceneRuntime == null)
            {
                throw new InvalidOperationException("GlobalBootstrap.sceneRuntime is not assigned.");
            }

            if (levelCatalog == null)
            {
                throw new InvalidOperationException("GlobalBootstrap.levelCatalog is not assigned.");
            }

            if (resourceRegistry == null)
            {
                throw new InvalidOperationException("GlobalBootstrap.resourceRegistry is not assigned.");
            }

            if (persistentPoolRoot == null)
            {
                throw new InvalidOperationException("GlobalBootstrap.persistentPoolRoot is not assigned.");
            }

            if (!timeService.transform.IsChildOf(transform) || !sceneRuntime.transform.IsChildOf(transform) ||
                !persistentPoolRoot.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    "TimeService, UnitySceneRuntime and PersistentPoolRoot must belong to GlobalRoot.");
            }
        }

        private void FailStartup(Exception exception)
        {
            Debug.LogError(
                $"[GlobalBootstrap] StartupFailed; Type={exception.GetType().Name}; " +
                $"Reason={exception.Message}");
            Cleanup();

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#else
            Application.Quit();
#endif
        }

        private void Cleanup()
        {
            if (shuttingDown)
            {
                return;
            }

            shuttingDown = true;
            if (started)
            {
                gameStateService.Dispose();
                started = false;
            }

            if (connected)
            {
                sceneRuntime.Disconnect();
                connected = false;
            }

            if (created)
            {
                sceneService.Dispose();
                poolService.Dispose();
                created = false;
            }
        }

        private void OnDestroy()
        {
            if (!ownsActiveInstance)
            {
                return;
            }

            Cleanup();
            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }
    }
}
