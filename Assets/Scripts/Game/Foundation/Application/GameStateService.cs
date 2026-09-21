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
        private readonly IPlayerProgressStore playerProgressStore;
        private readonly HashSet<int> completedLevelIds = new HashSet<int>();
        private readonly HashSet<int> unlockedLevelIds = new HashSet<int>();

        private SubscriptionToken sceneReadySubscription;
        private SubscriptionToken levelIntroFinishedSubscription;
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
        private LevelResult? completedResult;
        private bool disposed;

        public GameStateService(
            IConfigService configService,
            ISceneService sceneService,
            IEventBus eventBus,
            IPlayerProgressStore playerProgressStore)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.sceneService = sceneService ?? throw new ArgumentNullException(nameof(sceneService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.playerProgressStore = playerProgressStore ??
                throw new ArgumentNullException(nameof(playerProgressStore));
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (started)
            {
                return;
            }

            sceneReadySubscription = eventBus.Subscribe<AppSceneReady>(OnSceneReady);
            levelIntroFinishedSubscription = eventBus.Subscribe<LevelIntroFinished>(OnLevelIntroFinished);
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

            InitializePlayerProgress();
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

        public bool IsLevelCompleted(int levelId)
        {
            return !disposed && started && completedLevelIds.Contains(levelId);
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

            StartGameplay(selectedLevelId, levelConfig);
            return true;
        }

        public bool TryReturnToLevelSelect()
        {
            if (disposed || !started || pendingScene.IsValid ||
                (state != AppFlowState.Gameplay && state != AppFlowState.GameplayResult))
            {
                return false;
            }

            RequestScene(AppSceneId.LevelSelect, 0, 0, null);
            return true;
        }

        public bool TryRetryCurrentGameplay()
        {
            if (disposed || !started || state != AppFlowState.GameplayResult ||
                pendingScene.IsValid || currentLevelConfig == null ||
                completedResult != LevelResult.GameOver)
            {
                return false;
            }

            StartGameplay(currentLevelConfig.LevelId, currentLevelConfig);
            return true;
        }

        public bool TryStartNextGameplay()
        {
            if (disposed || !started || state != AppFlowState.GameplayResult ||
                pendingScene.IsValid || currentLevelConfig == null ||
                completedResult != LevelResult.Victory ||
                currentLevelConfig.UnlockedLevelIds.Count == 0)
            {
                return false;
            }

            var nextLevelId = currentLevelConfig.UnlockedLevelIds[0];
            if (!unlockedLevelIds.Contains(nextLevelId) ||
                !configService.TryGetLevelConfig(nextLevelId, out var nextLevelConfig))
            {
                return false;
            }

            StartGameplay(nextLevelId, nextLevelConfig);
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
            completedResult = completion.Result;
            if (completion.Result == LevelResult.Victory)
            {
                var unlockedCopy = CopyLevelIds(currentLevelConfig.UnlockedLevelIds);
                var progressChanged = completedLevelIds.Add(completion.LevelId);
                progressChanged |= unlockedLevelIds.Add(completion.LevelId);
                for (var index = 0; index < unlockedCopy.Length; index++)
                {
                    progressChanged |= unlockedLevelIds.Add(unlockedCopy[index]);
                }

                if (progressChanged)
                {
                    PersistPlayerProgress();
                }

                ChangeState(AppFlowState.GameplayResult);
                eventBus.Publish(new Victory(completion.LevelId, completion.LevelRunId, unlockedCopy));
            }
            else
            {
                ChangeState(AppFlowState.GameplayResult);
                eventBus.Publish(
                    new GameOver(
                        completion.LevelId,
                        completion.LevelRunId,
                        GameOverReason.ArmyReachedZero));
            }

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
            Unsubscribe(levelIntroFinishedSubscription);
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

                    Debug.Log(
                        $"[GameStateService] WaitingForLevelIntro; LevelId={message.LevelId}; " +
                        $"LevelRunId={message.LevelRunId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnLevelIntroFinished(LevelIntroFinished message)
        {
            if (disposed || state != AppFlowState.GameplayLoading || pendingScene.IsValid ||
                !hasCurrentScene || currentSceneId != AppSceneId.Gameplay ||
                currentSceneLevelId != message.LevelId ||
                currentSceneLevelRunId != message.LevelRunId ||
                currentLevelConfig == null || currentLevelConfig.LevelId != message.LevelId ||
                currentLevelRunId != message.LevelRunId)
            {
                return;
            }

            Debug.Log(
                $"[GameStateService] LevelIntroFinished={message.Reason}; " +
                $"LevelId={message.LevelId}; LevelRunId={message.LevelRunId}");
            ChangeState(AppFlowState.Gameplay);
            eventBus.Publish(new LevelRunStarted(message.LevelId, message.LevelRunId));
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

        private void InitializePlayerProgress()
        {
            completedLevelIds.Clear();
            unlockedLevelIds.Clear();
            var descriptors = configService.GetLevelDescriptors();
            var catalogLevelIds = new HashSet<int>();
            for (var index = 0; index < descriptors.Count; index++)
            {
                catalogLevelIds.Add(descriptors[index].LevelId);
                if (descriptors[index].InitiallyUnlocked)
                {
                    unlockedLevelIds.Add(descriptors[index].LevelId);
                }
            }

            PlayerProgressSnapshot progress;
            string diagnostic;
            try
            {
                if (!playerProgressStore.TryLoad(out progress, out diagnostic))
                {
                    Debug.LogWarning($"[GameStateService] PlayerProgressLoadFailed; Reason={diagnostic}");
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[GameStateService] PlayerProgressLoadFailed; Type={exception.GetType().Name}; " +
                    $"Reason={exception.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(diagnostic))
            {
                Debug.LogWarning($"[GameStateService] PlayerProgressRecovered; Detail={diagnostic}");
            }

            MergeProgressIds(
                progress.CompletedLevelIds,
                catalogLevelIds,
                completedLevelIds,
                "completedLevelIds");
            MergeProgressIds(
                progress.UnlockedLevelIds,
                catalogLevelIds,
                unlockedLevelIds,
                "unlockedLevelIds");

            foreach (var completedLevelId in completedLevelIds)
            {
                unlockedLevelIds.Add(completedLevelId);
            }
        }

        private void PersistPlayerProgress()
        {
            var snapshot = new PlayerProgressSnapshot(
                CopySortedLevelIds(completedLevelIds),
                CopySortedLevelIds(unlockedLevelIds));

            try
            {
                if (!playerProgressStore.TrySave(snapshot, out var diagnostic))
                {
                    Debug.LogError($"[GameStateService] PlayerProgressSaveFailed; Reason={diagnostic}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[GameStateService] PlayerProgressSaveFailed; Type={exception.GetType().Name}; " +
                    $"Reason={exception.Message}");
            }
        }

        private static void MergeProgressIds(
            IReadOnlyList<int> source,
            HashSet<int> catalogLevelIds,
            HashSet<int> destination,
            string fieldName)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var levelId = source[index];
                if (levelId < 0 || !catalogLevelIds.Contains(levelId))
                {
                    Debug.LogWarning(
                        $"[GameStateService] PlayerProgressLevelIgnored; " +
                        $"Field={fieldName}; Index={index}; LevelId={levelId}");
                    continue;
                }

                destination.Add(levelId);
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

        private void StartGameplay(int levelId, LevelConfigSnapshot levelConfig)
        {
            selectedLevelId = levelId;
            hasSelectedLevel = true;
            currentLevelRunId = AllocateLevelRunId();
            currentLevelConfig = levelConfig;
            gameplayCompleted = false;
            completedResult = null;
            ChangeState(AppFlowState.GameplayLoading);
            RequestScene(AppSceneId.Gameplay, levelId, currentLevelRunId, levelConfig);
        }

        private void ClearCurrentSession()
        {
            currentLevelConfig = null;
            currentLevelRunId = 0;
            gameplayCompleted = false;
            completedResult = null;
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

        private static int[] CopySortedLevelIds(HashSet<int> source)
        {
            var copy = new int[source.Count];
            source.CopyTo(copy);
            Array.Sort(copy);
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
