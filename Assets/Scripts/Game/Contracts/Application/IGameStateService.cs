namespace Game.Contracts
{
    public interface IGameStateService
    {
        AppFlowState GetAppFlowState();
        int GetSelectedLevelId();
        int GetCurrentLevelRunId();
        void NotifyInitializationReady();
        bool TrySelectLevel(int levelId);
        bool TryStartSelectedGameplay();
        void CompleteGameplay(LevelCompletion completion);
    }
}
