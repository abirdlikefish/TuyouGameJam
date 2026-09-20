using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    public sealed class GameStateService : IGameStateService, IDisposable
    {
        private readonly IConfigService configService;
        private readonly ISceneService sceneService;
        private readonly IEventBus eventBus;
        private readonly HashSet<int> unlockedLevelIds = new HashSet<int>();

        private SubscriptionToken sceneReadySubscription;
        private SubscriptionToken sceneUnloadedSubscription;
        private SubscriptionToken sceneLoadFailedSubscription;
        private SubscriptionToken sceneUnloadFailedSubscription;
        private PendingScene pendingScene;
        private AppFlowState state = AppFlowState.Initializing;
        private AppSceneId currentSceneId;
        private LevelConfigSnapshot currentLevelConfig;
        private int selectedLevelId;
        private int currentLevelRunId;
        private int currentSceneLevelId;
        private int currentSceneLevelRunId;
        private int nextLevelRunId;
        private bool hasSelectedLevel;
        private bool hasCurrentScene;
        private bool initializationNotified;
        private bool started;
        private bool gameplayCompleted;
        private bool disposed;

        public GameStateService(
            IConfigService configService,
            ISceneService sceneService,
            IEventBus eventBus)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.sceneService = sceneService ?? throw new ArgumentNullException(nameof(sceneService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (started)
            {
                return;
            }

            sceneReadySubscription = eventBus.Subscribe<AppSceneReady>(OnSceneReady);
            sceneUnloadedSubscription = eventBus.Subscribe<AppSceneUnloaded>(OnSceneUnloaded);
            sceneLoadFailedSubscription = eventBus.Subscribe<AppSceneLoadFailed>(OnSceneLoadFailed);
            sceneUnloadFailedSubscription = eventBus.Subscribe<AppSceneUnloadFailed>(OnSceneUnloadFailed);
            started = true;
        }

        public AppFlowState GetAppFlowState()
        {
            return state;
        }

        public int GetSelectedLevelId()
        {
            return hasSelectedLevel ? selectedLevelId : 0;
        }

        public int GetCurrentLevelRunId()
        {
            return currentLevelRunId;
        }

        public void NotifyInitializationReady()
        {
            ThrowIfDisposed();
            if (!started)
            {
                throw new InvalidOperationException("GameStateService must be started before initialization is reported ready.");
            }

            if (initializationNotified)
            {
                return;
            }

            if (configService.GetConfigLoadState() != ConfigLoadState.Ready)
            {
                throw new InvalidOperationException("Configuration must be Ready before application initialization can complete.");
            }

            InitializeUnlockedLevels();
            initializationNotified = true;
            RequestScene(AppSceneId.MainMenu, 0, 0, null);
        }

        public bool TryEnterLevelSelect()
        {
            if (disposed || !started || state != AppFlowState.MainMenu || pendingScene.IsValid)
            {
                return false;
            }

            RequestScene(AppSceneId.LevelSelect, 0, 0, null);
            return true;
        }

        public bool IsLevelUnlocked(int levelId)
        {
            return !disposed && started && unlockedLevelIds.Contains(levelId);
        }

        public bool TrySelectLevel(int levelId)
        {
            if (disposed || !started || state != AppFlowState.LevelSelect || pendingScene.IsValid)
            {
                return false;
            }

            if (!unlockedLevelIds.Contains(levelId) ||
                !configService.TryGetLevelConfig(levelId, out _))
            {
                return false;
            }

            selectedLevelId = levelId;
            hasSelectedLevel = true;
            return true;
        }

        public bool TryStartSelectedGameplay()
        {
            if (disposed || !started || state != AppFlowState.LevelSelect ||
                pendingScene.IsValid || !hasSelectedLevel)
            {
                return false;
            }

            if (!configService.TryGetLevelConfig(selectedLevelId, out var levelConfig))
            {
                return false;
            }

            currentLevelRunId = AllocateLevelRunId();
            currentLevelConfig = levelConfig;
            gameplayCompleted = false;
            ChangeState(AppFlowState.GameplayLoading);
            RequestScene(AppSceneId.Gameplay, selectedLevelId, currentLevelRunId, levelConfig);
            return true;
        }

        public void CompleteGameplay(LevelCompletion completion)
        {
            if (disposed || state != AppFlowState.Gameplay || gameplayCompleted ||
                currentLevelConfig == null || completion.LevelId != currentLevelConfig.LevelId ||
                completion.LevelRunId != currentLevelRunId)
            {
                return;
            }

            gameplayCompleted = true;
            if (completion.Result == LevelResult.Victory)
            {
                var unlockedCopy = CopyLevelIds(currentLevelConfig.UnlockedLevelIds);
                for (var index = 0; index < unlockedCopy.Length; index++)
                {
                    unlockedLevelIds.Add(unlockedCopy[index]);
                }

                eventBus.Publish(new Victory(completion.LevelId, completion.LevelRunId, unlockedCopy));
            }
            else
            {
                eventBus.Publish(
                    new GameOver(
                        completion.LevelId,
                        completion.LevelRunId,
                        GameOverReason.ArmyReachedZero));
            }

            RequestScene(AppSceneId.LevelSelect, 0, 0, null);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Unsubscribe(sceneUnloadFailedSubscription);
            Unsubscribe(sceneLoadFailedSubscription);
            Unsubscribe(sceneUnloadedSubscription);
            Unsubscribe(sceneReadySubscription);
            pendingScene = default(PendingScene);
            ClearCurrentSession();
            started = false;
        }

        private void OnSceneReady(AppSceneReady message)
        {
            if (!pendingScene.Matches(message.SceneId, message.LevelId, message.LevelRunId))
            {
                return;
            }

            pendingScene = default(PendingScene);
            hasCurrentScene = true;
            currentSceneId = message.SceneId;
            currentSceneLevelId = message.LevelId;
            currentSceneLevelRunId = message.LevelRunId;

            switch (message.SceneId)
            {
                case AppSceneId.MainMenu:
                    ChangeState(AppFlowState.MainMenu);
                    break;
                case AppSceneId.LevelSelect:
                    ClearCurrentSession();
                    ChangeState(AppFlowState.LevelSelect);
                    break;
                case AppSceneId.Gameplay:
                    if (state != AppFlowState.GameplayLoading ||
                        currentLevelConfig == null ||
                        message.LevelId != currentLevelConfig.LevelId ||
                        message.LevelRunId != currentLevelRunId)
                    {
                        return;
                    }

                    ChangeState(AppFlowState.Gameplay);
                    eventBus.Publish(new LevelRunStarted(message.LevelId, message.LevelRunId));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnSceneUnloaded(AppSceneUnloaded message)
        {
            if (!hasCurrentScene || currentSceneId != message.SceneId ||
                currentSceneLevelId != message.LevelId ||
                currentSceneLevelRunId != message.LevelRunId)
            {
                return;
            }

            hasCurrentScene = false;
            currentSceneLevelId = 0;
            currentSceneLevelRunId = 0;
            Debug.Log(
                $"[GameStateService] SceneUnloaded={message.SceneId}; " +
                $"LevelId={message.LevelId}; LevelRunId={message.LevelRunId}");
        }

        private void OnSceneLoadFailed(AppSceneLoadFailed message)
        {
            if (!pendingScene.Matches(message.SceneId, message.LevelId, message.LevelRunId))
            {
                return;
            }

            var failedTarget = pendingScene.SceneId;
            pendingScene = default(PendingScene);
            Debug.LogError(
                $"[GameStateService] SceneLoadFailed={message.SceneId}; Code={message.ErrorCode}; " +
                $"LevelId={message.LevelId}; LevelRunId={message.LevelRunId}");

            if (failedTarget != AppSceneId.Gameplay)
            {
                return;
            }

            ClearCurrentSession();
            RequestScene(AppSceneId.LevelSelect, 0, 0, null);
        }

        private void OnSceneUnloadFailed(AppSceneUnloadFailed message)
        {
            if (!pendingScene.IsValid || !hasCurrentScene || currentSceneId != message.SceneId ||
                currentSceneLevelId != message.LevelId ||
                currentSceneLevelRunId != message.LevelRunId)
            {
                return;
            }

            Debug.LogError(
                $"[GameStateService] SceneUnloadFailed={message.SceneId}; Code={message.ErrorCode}; " +
                $"LevelId={message.LevelId}; LevelRunId={message.LevelRunId}");
            pendingScene = default(PendingScene);
        }

        private void RequestScene(
            AppSceneId sceneId,
            int levelId,
            int levelRunId,
            LevelConfigSnapshot levelConfig)
        {
            if (pendingScene.IsValid)
            {
                throw new InvalidOperationException("An application scene switch is already pending.");
            }

            pendingScene = new PendingScene(sceneId, levelId, levelRunId);
            Debug.Log(
                $"[GameStateService] SceneRequest={sceneId}; LevelId={levelId}; LevelRunId={levelRunId}");

            switch (sceneId)
            {
                case AppSceneId.MainMenu:
                    sceneService.SwitchToMainMenu();
                    break;
                case AppSceneId.LevelSelect:
                    sceneService.SwitchToLevelSelect();
                    break;
                case AppSceneId.Gameplay:
                    sceneService.SwitchToGameplay(levelId, levelConfig, levelRunId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sceneId), sceneId, "Unknown application scene.");
            }
        }

        private void ChangeState(AppFlowState newState)
        {
            if (state == newState)
            {
                return;
            }

            var previousState = state;
            state = newState;
            var levelId = currentLevelConfig != null
                ? currentLevelConfig.LevelId
                : hasSelectedLevel ? selectedLevelId : 0;
            Debug.Log(
                $"[GameStateService] State={previousState}->{newState}; " +
                $"LevelId={levelId}; LevelRunId={currentLevelRunId}");
            eventBus.Publish(new AppFlowChanged(previousState, newState, levelId, currentLevelRunId));
        }

        private void InitializeUnlockedLevels()
        {
            unlockedLevelIds.Clear();
            var descriptors = configService.GetLevelDescriptors();
            for (var index = 0; index < descriptors.Count; index++)
            {
                if (descriptors[index].InitiallyUnlocked)
                {
                    unlockedLevelIds.Add(descriptors[index].LevelId);
                }
            }
        }

        private int AllocateLevelRunId()
        {
            if (nextLevelRunId == int.MaxValue)
            {
                throw new InvalidOperationException("LevelRunId space is exhausted.");
            }

            nextLevelRunId++;
            return nextLevelRunId;
        }

        private void ClearCurrentSession()
        {
            currentLevelConfig = null;
            currentLevelRunId = 0;
            gameplayCompleted = false;
        }

        private void Unsubscribe(SubscriptionToken token)
        {
            if (token.IsValid)
            {
                eventBus.Unsubscribe(token);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GameStateService));
            }
        }

        private static int[] CopyLevelIds(IReadOnlyList<int> source)
        {
            var copy = new int[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }

        private readonly struct PendingScene
        {
            public PendingScene(AppSceneId sceneId, int levelId, int levelRunId)
            {
                SceneId = sceneId;
                LevelId = levelId;
                LevelRunId = levelRunId;
                IsValid = true;
            }

            public AppSceneId SceneId { get; }
            public int LevelId { get; }
            public int LevelRunId { get; }
            public bool IsValid { get; }

            public bool Matches(AppSceneId sceneId, int levelId, int levelRunId)
            {
                return IsValid && SceneId == sceneId && LevelId == levelId && LevelRunId == levelRunId;
            }
        }
    }
}
