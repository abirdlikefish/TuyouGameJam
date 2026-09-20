using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    public sealed class SceneService : ISceneService, IDisposable
    {
        private readonly IEventBus eventBus;
        private readonly ISceneRuntime runtime;

        private SceneOperationState operationState = SceneOperationState.Idle;
        private SceneRequest currentScene;
        private SceneRequest pendingScene;
        private bool hasCurrentScene;
        private bool disposed;

        public SceneService(IEventBus eventBus, ISceneRuntime runtime)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public void SwitchToMainMenu()
        {
            RequestSwitch(new SceneRequest(AppSceneId.MainMenu, 0, 0, null));
        }

        public void SwitchToLevelSelect()
        {
            RequestSwitch(new SceneRequest(AppSceneId.LevelSelect, 0, 0, null));
        }

        public void SwitchToGameplay(
            int levelId,
            LevelConfigSnapshot levelConfig,
            int levelRunId)
        {
            if (levelConfig == null || levelId < 0 || levelRunId <= 0 || levelConfig.LevelId != levelId)
            {
                PublishLoadFailed(
                    new SceneRequest(AppSceneId.Gameplay, levelId, levelRunId, levelConfig),
                    SceneLoadErrorCode.InvalidRequest);
                return;
            }

            RequestSwitch(new SceneRequest(AppSceneId.Gameplay, levelId, levelRunId, levelConfig));
        }

        public void Dispose()
        {
            disposed = true;
            pendingScene = default(SceneRequest);
            operationState = SceneOperationState.Faulted;
        }

        private void RequestSwitch(SceneRequest request)
        {
            if (disposed || operationState == SceneOperationState.Faulted ||
                operationState == SceneOperationState.Unloading ||
                operationState == SceneOperationState.Loading ||
                operationState == SceneOperationState.CleaningFailedLoad ||
                (hasCurrentScene && operationState == SceneOperationState.Ready &&
                 currentScene.Matches(request)))
            {
                PublishLoadFailed(request, SceneLoadErrorCode.InvalidRequest);
                return;
            }

            pendingScene = request;
            Debug.Log(
                $"[SceneService] SwitchRequested={request.SceneId}; " +
                $"LevelId={request.LevelId}; LevelRunId={request.LevelRunId}");

            if (!hasCurrentScene)
            {
                LoadPendingScene();
                return;
            }

            operationState = SceneOperationState.Unloading;
            var unloadingScene = currentScene;
            runtime.UnloadCurrent(succeeded => OnCurrentSceneUnloaded(unloadingScene, succeeded));
        }

        private void OnCurrentSceneUnloaded(SceneRequest unloadedScene, bool succeeded)
        {
            if (disposed || operationState != SceneOperationState.Unloading)
            {
                return;
            }

            if (!succeeded)
            {
                operationState = SceneOperationState.Faulted;
                eventBus.Publish(
                    new AppSceneUnloadFailed(
                        unloadedScene.SceneId,
                        unloadedScene.LevelId,
                        unloadedScene.LevelRunId,
                        SceneUnloadErrorCode.SceneUnloadFailed));
                return;
            }

            hasCurrentScene = false;
            currentScene = default(SceneRequest);
            eventBus.Publish(
                new AppSceneUnloaded(
                    unloadedScene.SceneId,
                    unloadedScene.LevelId,
                    unloadedScene.LevelRunId));
            Debug.Log(
                $"[SceneService] Unloaded={unloadedScene.SceneId}; " +
                $"LevelId={unloadedScene.LevelId}; LevelRunId={unloadedScene.LevelRunId}");
            LoadPendingScene();
        }

        private void LoadPendingScene()
        {
            var request = pendingScene;
            operationState = SceneOperationState.Loading;
            try
            {
                runtime.Load(
                    new SceneRuntimeLoadRequest(
                        request.SceneId,
                        request.LevelId,
                        request.LevelRunId,
                        request.LevelConfig),
                    result => OnSceneLoaded(request, result));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnSceneLoaded(
                    request,
                    SceneRuntimeLoadResult.Failure(SceneLoadErrorCode.SceneLoadFailed, false));
            }
        }

        private void OnSceneLoaded(SceneRequest request, SceneRuntimeLoadResult result)
        {
            if (disposed || operationState != SceneOperationState.Loading)
            {
                return;
            }

            if (result.Succeeded)
            {
                currentScene = request;
                pendingScene = default(SceneRequest);
                hasCurrentScene = true;
                operationState = SceneOperationState.Ready;
                eventBus.Publish(new AppSceneReady(request.SceneId, request.LevelId, request.LevelRunId));
                Debug.Log(
                    $"[SceneService] Ready={request.SceneId}; " +
                    $"LevelId={request.LevelId}; LevelRunId={request.LevelRunId}");
                return;
            }

            if (!result.RequiresCleanup)
            {
                pendingScene = default(SceneRequest);
                operationState = SceneOperationState.Idle;
                PublishLoadFailed(request, result.ErrorCode);
                return;
            }

            operationState = SceneOperationState.CleaningFailedLoad;
            runtime.UnloadCurrent(succeeded => OnFailedSceneCleaned(request, result.ErrorCode, succeeded));
        }

        private void OnFailedSceneCleaned(
            SceneRequest request,
            SceneLoadErrorCode loadError,
            bool succeeded)
        {
            if (disposed || operationState != SceneOperationState.CleaningFailedLoad)
            {
                return;
            }

            pendingScene = default(SceneRequest);
            if (!succeeded)
            {
                operationState = SceneOperationState.Faulted;
                eventBus.Publish(
                    new AppSceneUnloadFailed(
                        request.SceneId,
                        request.LevelId,
                        request.LevelRunId,
                        SceneUnloadErrorCode.SceneUnloadFailed));
                return;
            }

            operationState = SceneOperationState.Idle;
            PublishLoadFailed(request, loadError);
        }

        private void PublishLoadFailed(SceneRequest request, SceneLoadErrorCode errorCode)
        {
            Debug.LogError(
                $"[SceneService] LoadFailed={request.SceneId}; Code={errorCode}; " +
                $"LevelId={request.LevelId}; LevelRunId={request.LevelRunId}");
            eventBus.Publish(
                new AppSceneLoadFailed(
                    request.SceneId,
                    request.LevelId,
                    request.LevelRunId,
                    errorCode));
        }

        private enum SceneOperationState
        {
            Idle = 0,
            Unloading = 1,
            Loading = 2,
            Ready = 3,
            CleaningFailedLoad = 4,
            Faulted = 5
        }

        private readonly struct SceneRequest
        {
            public SceneRequest(
                AppSceneId sceneId,
                int levelId,
                int levelRunId,
                LevelConfigSnapshot levelConfig)
            {
                SceneId = sceneId;
                LevelId = levelId;
                LevelRunId = levelRunId;
                LevelConfig = levelConfig;
            }

            public AppSceneId SceneId { get; }
            public int LevelId { get; }
            public int LevelRunId { get; }
            public LevelConfigSnapshot LevelConfig { get; }

            public bool Matches(SceneRequest other)
            {
                return SceneId == other.SceneId && LevelId == other.LevelId && LevelRunId == other.LevelRunId;
            }
        }
    }
}
