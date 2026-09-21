namespace Game.Contracts
{
    public enum AppFlowState
    {
        Initializing = 0,
        MainMenu = 1,
        LevelSelect = 2,
        GameplayLoading = 3,
        Gameplay = 4,
        GameplayResult = 5
    }

    public enum AppSceneId
    {
        MainMenu = 0,
        LevelSelect = 1,
        Gameplay = 2
    }

    public enum LevelResult
    {
        Victory = 0,
        GameOver = 1
    }

    public enum GameOverReason
    {
        ArmyReachedZero = 0
    }

    public enum LevelIntroEndReason
    {
        Completed = 0,
        NoVideoConfigured = 1,
        PlaybackFailed = 2,
        PreparationTimedOut = 3
    }

    public enum SceneLoadErrorCode
    {
        InvalidRequest = 0,
        SceneNotConfigured = 1,
        SceneLoadFailed = 2,
        SceneEntryMissing = 3,
        SceneEntryAmbiguous = 4,
        SceneEntryTypeMismatch = 5,
        SceneEntryInitializationFailed = 6
    }

    public enum SceneUnloadErrorCode
    {
        InvalidRequest = 0,
        SceneUnloadFailed = 1
    }

    public readonly struct LevelCompletion
    {
        public LevelCompletion(int levelId, int levelRunId, LevelResult result)
        {
            LevelId = levelId;
            LevelRunId = levelRunId;
            Result = result;
        }

        public int LevelId { get; }
        public int LevelRunId { get; }
        public LevelResult Result { get; }
    }
}
