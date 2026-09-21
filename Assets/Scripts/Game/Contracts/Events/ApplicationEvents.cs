using System;
using System.Collections.Generic;

namespace Game.Contracts
{
    public readonly struct AppFlowChanged
    {
        public AppFlowChanged(
            AppFlowState previousState,
            AppFlowState currentState,
            int currentLevelId,
            int levelRunId)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            CurrentLevelId = currentLevelId;
            LevelRunId = levelRunId;
        }

        public AppFlowState PreviousState { get; }
        public AppFlowState CurrentState { get; }
        public int CurrentLevelId { get; }
        public int LevelRunId { get; }
    }

    public readonly struct LevelRunStarted
    {
        public LevelRunStarted(int levelId, int levelRunId)
        {
            LevelId = levelId;
            LevelRunId = levelRunId;
        }

        public int LevelId { get; }
        public int LevelRunId { get; }
    }

    public readonly struct LevelIntroFinished
    {
        public LevelIntroFinished(int levelId, int levelRunId, LevelIntroEndReason reason)
        {
            LevelId = levelId;
            LevelRunId = levelRunId;
            Reason = reason;
        }

        public int LevelId { get; }
        public int LevelRunId { get; }
        public LevelIntroEndReason Reason { get; }
    }

    public readonly struct AppSceneReady
    {
        public AppSceneReady(AppSceneId sceneId, int levelId, int levelRunId)
        {
            SceneId = sceneId;
            LevelId = levelId;
            LevelRunId = levelRunId;
        }

        public AppSceneId SceneId { get; }
        public int LevelId { get; }
        public int LevelRunId { get; }
    }

    public readonly struct AppSceneUnloaded
    {
        public AppSceneUnloaded(AppSceneId sceneId, int levelId, int levelRunId)
        {
            SceneId = sceneId;
            LevelId = levelId;
            LevelRunId = levelRunId;
        }

        public AppSceneId SceneId { get; }
        public int LevelId { get; }
        public int LevelRunId { get; }
    }

    public readonly struct AppSceneLoadFailed
    {
        public AppSceneLoadFailed(
            AppSceneId sceneId,
            int levelId,
            int levelRunId,
            SceneLoadErrorCode errorCode)
        {
            SceneId = sceneId;
            LevelId = levelId;
            LevelRunId = levelRunId;
            ErrorCode = errorCode;
        }

        public AppSceneId SceneId { get; }
        public int LevelId { get; }
        public int LevelRunId { get; }
        public SceneLoadErrorCode ErrorCode { get; }
    }

    public readonly struct AppSceneUnloadFailed
    {
        public AppSceneUnloadFailed(
            AppSceneId sceneId,
            int levelId,
            int levelRunId,
            SceneUnloadErrorCode errorCode)
        {
            SceneId = sceneId;
            LevelId = levelId;
            LevelRunId = levelRunId;
            ErrorCode = errorCode;
        }

        public AppSceneId SceneId { get; }
        public int LevelId { get; }
        public int LevelRunId { get; }
        public SceneUnloadErrorCode ErrorCode { get; }
    }

    public sealed class Victory
    {
        public Victory(int levelId, int levelRunId, IReadOnlyList<int> unlockedLevelIds)
        {
            if (unlockedLevelIds == null)
            {
                throw new ArgumentNullException(nameof(unlockedLevelIds));
            }

            LevelId = levelId;
            LevelRunId = levelRunId;

            var copy = new int[unlockedLevelIds.Count];
            for (var index = 0; index < unlockedLevelIds.Count; index++)
            {
                copy[index] = unlockedLevelIds[index];
            }

            UnlockedLevelIds = Array.AsReadOnly(copy);
        }

        public int LevelId { get; }
        public int LevelRunId { get; }
        public IReadOnlyList<int> UnlockedLevelIds { get; }
    }

    public readonly struct GameOver
    {
        public GameOver(int levelId, int levelRunId, GameOverReason reason)
        {
            LevelId = levelId;
            LevelRunId = levelRunId;
            Reason = reason;
        }

        public int LevelId { get; }
        public int LevelRunId { get; }
        public GameOverReason Reason { get; }
    }
}
