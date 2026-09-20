using System;
using System.Collections.Generic;
using Game.Contracts;

namespace Game.Foundation
{
    internal sealed class RealTimeTimerScheduler : IDisposable
    {
        private readonly List<ScheduledTimer> timers = new List<ScheduledTimer>();
        private readonly List<ScheduledTimer> dueTimers = new List<ScheduledTimer>();

        private bool isDisposed;

        public TimerHandle Schedule(float seconds, Action callback)
        {
            ThrowIfDisposed();

            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Timer duration must be finite and non-negative.");
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            ScheduledTimer timer = null;
            var handle = new TimerHandle(() => Cancel(timer));
            timer = new ScheduledTimer(seconds, callback, handle);
            timers.Add(timer);
            return handle;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (isDisposed)
            {
                return;
            }

            if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unscaledDeltaTime),
                    unscaledDeltaTime,
                    "Timer delta time must be finite and non-negative.");
            }

            dueTimers.Clear();
            for (var index = 0; index < timers.Count; index++)
            {
                var timer = timers[index];
                timer.RemainingSeconds -= unscaledDeltaTime;
                if (timer.RemainingSeconds <= 0f)
                {
                    dueTimers.Add(timer);
                }
            }

            for (var index = 0; index < dueTimers.Count; index++)
            {
                var timer = dueTimers[index];
                if (!timer.Handle.IsValid)
                {
                    continue;
                }

                // 先使 Handle 失效，回调中的重复取消或重入不会再次执行当前任务。
                timer.Handle.Cancel();
                timer.Callback();
            }

            dueTimers.Clear();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            while (timers.Count > 0)
            {
                timers[timers.Count - 1].Handle.Cancel();
            }

            dueTimers.Clear();
        }

        private void Cancel(ScheduledTimer timer)
        {
            if (timer == null)
            {
                return;
            }

            timers.Remove(timer);
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(RealTimeTimerScheduler));
            }
        }

        private sealed class ScheduledTimer
        {
            public ScheduledTimer(float remainingSeconds, Action callback, TimerHandle handle)
            {
                RemainingSeconds = remainingSeconds;
                Callback = callback;
                Handle = handle;
            }

            public float RemainingSeconds { get; set; }
            public Action Callback { get; }
            public TimerHandle Handle { get; }
        }
    }
}
