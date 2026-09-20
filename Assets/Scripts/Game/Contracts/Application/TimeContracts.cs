using System;

namespace Game.Contracts
{
    public enum TimeDomain
    {
        RealTime = 0,
        Gameplay = 1,
        Bullet = 2,
        Gate = 3,
        Monster = 4,
        VFX = 5
    }

    public readonly struct TimerHandle
    {
        private readonly CancellationState state;

        internal TimerHandle(Action cancel)
        {
            state = cancel == null ? null : new CancellationState(cancel);
        }

        public bool IsValid => state != null && state.IsValid;

        public void Cancel()
        {
            state?.Cancel();
        }

        private sealed class CancellationState
        {
            private Action cancel;

            public CancellationState(Action cancel)
            {
                this.cancel = cancel;
            }

            public bool IsValid => cancel != null;

            public void Cancel()
            {
                var callback = cancel;
                if (callback == null)
                {
                    return;
                }

                cancel = null;
                callback();
            }
        }
    }

    public interface ITimeService
    {
        float GetDeltaTime(TimeDomain domain);
        TimerHandle Schedule(float seconds, Action callback, TimeDomain domain);
    }
}
