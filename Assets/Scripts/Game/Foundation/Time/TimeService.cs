using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    [DisallowMultipleComponent]
    public sealed class TimeService : MonoBehaviour, ITimeService
    {
        private readonly RealTimeTimerScheduler timerScheduler = new RealTimeTimerScheduler();

        public float GetDeltaTime(TimeDomain domain)
        {
            switch (domain)
            {
                case TimeDomain.RealTime:
                case TimeDomain.Gameplay:
                case TimeDomain.Bullet:
                case TimeDomain.Gate:
                case TimeDomain.Monster:
                case TimeDomain.VFX:
                    return UnityEngine.Time.unscaledDeltaTime;
                default:
                    throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown time domain.");
            }
        }

        public TimerHandle Schedule(float seconds, Action callback, TimeDomain domain)
        {
            if (domain != TimeDomain.RealTime)
            {
                throw new NotSupportedException("MVP timers only support the RealTime domain.");
            }

            return timerScheduler.Schedule(seconds, callback);
        }

        private void Update()
        {
            timerScheduler.Tick(UnityEngine.Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            timerScheduler.Dispose();
        }
    }
}
