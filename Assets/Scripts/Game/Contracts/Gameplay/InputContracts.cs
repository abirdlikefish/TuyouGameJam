namespace Game.Contracts
{
    public interface IGameplayInputGate
    {
        void SetGameplayEnabled(bool enabled);
    }

    public interface IGameplayInputController : IGameplayInputGate
    {
        void TickInput(float unscaledDeltaTime);
    }
}
