namespace Game.Contracts
{
    public interface ISceneService
    {
        void SwitchToMainMenu();
        void SwitchToLevelSelect();
        void SwitchToGameplay(
            int levelId,
            LevelConfigSnapshot levelConfig,
            int levelRunId);
    }
}
