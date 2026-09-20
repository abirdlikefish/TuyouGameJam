using System;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Composition
{
    [DisallowMultipleComponent]
    public sealed class UnitySceneRuntime : MonoBehaviour, ISceneRuntime
    {
        private const string MainMenuRootName = "MainMenuRoot";
        private const string LevelSelectRootName = "LevelSelectRoot";
        private const string GameplayRootName = "GameplayRoot";

        [Header("Build Settings 场景路径")]
        [SerializeField] private string bootstrapScenePath = "Assets/Scenes/BootstrapScene.unity";
        [SerializeField] private string mainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        [SerializeField] private string levelSelectScenePath = "Assets/Scenes/LevelSelectScene.unity";
        [SerializeField] private string gameplayScenePath = "Assets/Scenes/GameplayScene.unity";

        private SceneRuntimeDependencies dependencies;
        private SceneRuntimeLoadRequest loadedRequest;
        private Scene bootstrapScene;
        private Scene loadedScene;
        private IAppSceneEntry loadedEntry;
        private int mainMenuBuildIndex = -1;
        private int levelSelectBuildIndex = -1;
        private int gameplayBuildIndex = -1;
        private bool connected;
        private bool loadInProgress;
        private bool unloadInProgress;

        public bool TryConnect(SceneRuntimeDependencies runtimeDependencies, out string error)
        {
            if (connected)
            {
                error = "UnitySceneRuntime is already connected.";
                return false;
            }

            if (!TryValidateScenePaths(out error))
            {
                return false;
            }

            // Connect 发生在 GlobalRoot 移入 DontDestroyOnLoad 场景之前；直接使用
            // SceneRuntime 自身所属场景，避免依赖全局路径查询的启动时序。
            bootstrapScene = gameObject.scene;
            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded ||
                bootstrapScene.path != bootstrapScenePath)
            {
                error =
                    $"UnitySceneRuntime must belong to Bootstrap scene '{bootstrapScenePath}'. " +
                    $"Actual scene: Name='{bootstrapScene.name}', Path='{bootstrapScene.path}', " +
                    $"IsValid={bootstrapScene.IsValid()}, IsLoaded={bootstrapScene.isLoaded}.";
                return false;
            }

            dependencies = runtimeDependencies;
            connected = true;
            error = string.Empty;
            return true;
        }

        public void Disconnect()
        {
            CleanupLoadedEntry();
            connected = false;
            dependencies = default(SceneRuntimeDependencies);
        }

        public void Load(
            SceneRuntimeLoadRequest request,
            Action<SceneRuntimeLoadResult> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            if (!connected || loadInProgress || unloadInProgress || loadedScene.IsValid())
            {
                completed(SceneRuntimeLoadResult.Failure(SceneLoadErrorCode.InvalidRequest, false));
                return;
            }

            if (!TryGetSceneDefinition(
                    request.SceneId,
                    out var buildIndex,
                    out var rootName,
                    out var expectedEntryType))
            {
                completed(SceneRuntimeLoadResult.Failure(SceneLoadErrorCode.SceneNotConfigured, false));
                return;
            }

            loadedRequest = request;
            loadInProgress = true;
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(
                    buildIndex,
                    new LoadSceneParameters(LoadSceneMode.Additive));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                loadInProgress = false;
                ClearLoadedState();
                completed(SceneRuntimeLoadResult.Failure(SceneLoadErrorCode.SceneLoadFailed, false));
                return;
            }

            if (operation == null)
            {
                loadInProgress = false;
                ClearLoadedState();
                completed(SceneRuntimeLoadResult.Failure(SceneLoadErrorCode.SceneLoadFailed, false));
                return;
            }

            operation.completed += _ => CompleteLoad(
                request,
                buildIndex,
                rootName,
                expectedEntryType,
                completed);
        }

        private void CompleteLoad(
            SceneRuntimeLoadRequest request,
            int buildIndex,
            string rootName,
            Type expectedEntryType,
            Action<SceneRuntimeLoadResult> completed)
        {
            SceneRuntimeLoadResult result;
            try
            {
                loadedScene = SceneManager.GetSceneByBuildIndex(buildIndex);
                if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                {
                    ClearLoadedState();
                    result = SceneRuntimeLoadResult.Failure(SceneLoadErrorCode.SceneLoadFailed, false);
                }
                else
                {
                    var rootResult = TryResolveRoot(loadedScene, rootName, out var root);
                    if (rootResult.HasValue)
                    {
                        result = SceneRuntimeLoadResult.Failure(rootResult.Value, true);
                    }
                    else
                    {
                        var entryResult = TryResolveEntry(root, expectedEntryType, out loadedEntry);
                        if (entryResult.HasValue)
                        {
                            result = SceneRuntimeLoadResult.Failure(entryResult.Value, true);
                        }
                        else
                        {
                            try
                            {
                                loadedEntry.Initialize(request, dependencies);
                                if (!SceneManager.SetActiveScene(loadedScene))
                                {
                                    result = SceneRuntimeLoadResult.Failure(
                                        SceneLoadErrorCode.SceneLoadFailed,
                                        true);
                                }
                                else
                                {
                                    Debug.Log(
                                        $"[UnitySceneRuntime] EntryInitialized={request.SceneId}; " +
                                        $"LevelId={request.LevelId}; LevelRunId={request.LevelRunId}");
                                    result = SceneRuntimeLoadResult.Success();
                                }
                            }
                            catch (Exception exception)
                            {
                                Debug.LogException(exception);
                                result = SceneRuntimeLoadResult.Failure(
                                    SceneLoadErrorCode.SceneEntryInitializationFailed,
                                    true);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                var requiresCleanup = loadedScene.IsValid() && loadedScene.isLoaded;
                if (!requiresCleanup)
                {
                    ClearLoadedState();
                }

                result = SceneRuntimeLoadResult.Failure(
                    SceneLoadErrorCode.SceneLoadFailed,
                    requiresCleanup);
            }

            loadInProgress = false;
            completed(result);
        }

        public void UnloadCurrent(Action<bool> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            if (!connected || loadInProgress || unloadInProgress ||
                !loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                completed(false);
                return;
            }

            unloadInProgress = true;
            var cleanupSucceeded = CleanupLoadedEntry();
            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded ||
                !SceneManager.SetActiveScene(bootstrapScene))
            {
                unloadInProgress = false;
                completed(false);
                return;
            }

            var sceneToUnload = loadedScene;
            var request = loadedRequest;
            AsyncOperation operation;
            try
            {
                operation = SceneManager.UnloadSceneAsync(sceneToUnload);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                unloadInProgress = false;
                completed(false);
                return;
            }

            if (operation == null)
            {
                unloadInProgress = false;
                completed(false);
                return;
            }

            operation.completed += _ =>
            {
                var unloadSucceeded = !sceneToUnload.IsValid() || !sceneToUnload.isLoaded;
                ClearLoadedState();
                unloadInProgress = false;
                Debug.Log(
                    $"[UnitySceneRuntime] EntryCleanupAndUnload={request.SceneId}; " +
                    $"LevelId={request.LevelId}; LevelRunId={request.LevelRunId}; " +
                    $"Succeeded={cleanupSucceeded && unloadSucceeded}");
                completed(cleanupSucceeded && unloadSucceeded);
            };
        }

        private bool TryValidateScenePaths(out string error)
        {
            if (!IsScenePath(bootstrapScenePath) || !IsScenePath(mainMenuScenePath) ||
                !IsScenePath(levelSelectScenePath) || !IsScenePath(gameplayScenePath))
            {
                error = "All UnitySceneRuntime scene paths must be non-empty Assets/*.unity paths.";
                return false;
            }

            if (bootstrapScenePath == mainMenuScenePath || bootstrapScenePath == levelSelectScenePath ||
                bootstrapScenePath == gameplayScenePath || mainMenuScenePath == levelSelectScenePath ||
                mainMenuScenePath == gameplayScenePath || levelSelectScenePath == gameplayScenePath)
            {
                error = "UnitySceneRuntime scene paths must be unique.";
                return false;
            }

            var bootstrapBuildIndex = SceneUtility.GetBuildIndexByScenePath(bootstrapScenePath);
            mainMenuBuildIndex = SceneUtility.GetBuildIndexByScenePath(mainMenuScenePath);
            levelSelectBuildIndex = SceneUtility.GetBuildIndexByScenePath(levelSelectScenePath);
            gameplayBuildIndex = SceneUtility.GetBuildIndexByScenePath(gameplayScenePath);
            if (bootstrapBuildIndex < 0 || mainMenuBuildIndex < 0 ||
                levelSelectBuildIndex < 0 || gameplayBuildIndex < 0)
            {
                error = "Bootstrap, MainMenu, LevelSelect and Gameplay scenes must all be enabled in Build Settings.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryGetSceneDefinition(
            AppSceneId sceneId,
            out int buildIndex,
            out string rootName,
            out Type expectedEntryType)
        {
            switch (sceneId)
            {
                case AppSceneId.MainMenu:
                    buildIndex = mainMenuBuildIndex;
                    rootName = MainMenuRootName;
                    expectedEntryType = typeof(MainMenuSceneEntry);
                    break;
                case AppSceneId.LevelSelect:
                    buildIndex = levelSelectBuildIndex;
                    rootName = LevelSelectRootName;
                    expectedEntryType = typeof(LevelSelectSceneEntry);
                    break;
                case AppSceneId.Gameplay:
                    buildIndex = gameplayBuildIndex;
                    rootName = GameplayRootName;
                    expectedEntryType = typeof(GameplaySceneEntry);
                    break;
                default:
                    buildIndex = -1;
                    rootName = string.Empty;
                    expectedEntryType = null;
                    return false;
            }

            return buildIndex >= 0;
        }

        private static SceneLoadErrorCode? TryResolveRoot(
            Scene scene,
            string rootName,
            out GameObject root)
        {
            root = null;
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                if (!string.Equals(roots[index].name, rootName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (root != null)
                {
                    root = null;
                    return SceneLoadErrorCode.SceneEntryAmbiguous;
                }

                root = roots[index];
            }

            return root == null ? SceneLoadErrorCode.SceneEntryMissing : (SceneLoadErrorCode?)null;
        }

        private static SceneLoadErrorCode? TryResolveEntry(
            GameObject root,
            Type expectedEntryType,
            out IAppSceneEntry entry)
        {
            entry = null;
            var behaviours = root.GetComponents<MonoBehaviour>();
            var compatibleCount = 0;
            var hasDifferentEntryType = false;
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (!(behaviours[index] is IAppSceneEntry candidate))
                {
                    continue;
                }

                if (behaviours[index].GetType() != expectedEntryType)
                {
                    hasDifferentEntryType = true;
                    continue;
                }

                compatibleCount++;
                entry = candidate;
            }

            if (compatibleCount > 1)
            {
                entry = null;
                return SceneLoadErrorCode.SceneEntryAmbiguous;
            }

            if (compatibleCount == 1)
            {
                return null;
            }

            return hasDifferentEntryType
                ? SceneLoadErrorCode.SceneEntryTypeMismatch
                : SceneLoadErrorCode.SceneEntryMissing;
        }

        private bool CleanupLoadedEntry()
        {
            if (loadedEntry == null)
            {
                return true;
            }

            try
            {
                loadedEntry.Cleanup();
                loadedEntry = null;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                loadedEntry = null;
                return false;
            }
        }

        private void ClearLoadedState()
        {
            loadedEntry = null;
            loadedScene = default(Scene);
            loadedRequest = default(SceneRuntimeLoadRequest);
        }

        private static bool IsScenePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                   path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private void OnDestroy()
        {
            CleanupLoadedEntry();
        }
    }
}
