namespace Game.Contracts
{
    public interface IGameStateService
    {
        AppFlowState GetAppFlowState();
        int GetSelectedLevelId();
        int GetCurrentLevelRunId();
        void NotifyInitializationReady();
        bool TryEnterLevelSelect();
        bool IsLevelUnlocked(int levelId);
        bool IsLevelCompleted(int levelId);
        bool TrySelectLevel(int levelId);
        bool TryStartSelectedGameplay();
        bool TryReturnToLevelSelect();
        bool TryRetryCurrentGameplay();
        bool TryStartNextGameplay();
        void CompleteGameplay(LevelCompletion completion);
    }
}
