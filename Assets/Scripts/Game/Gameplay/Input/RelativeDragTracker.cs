using System;

namespace Game.Gameplay
{
    public sealed class RelativeDragTracker
    {
        private const int NoPointer = int.MinValue;

        private int activePointerId = NoPointer;
        private float previousLocalX;
        private float accumulatedDeltaX;

        public bool HasActivePointer => activePointerId != NoPointer;

        public bool TryBegin(int pointerId, float localX)
        {
            if (HasActivePointer || !IsFinite(localX))
            {
                return false;
            }

            activePointerId = pointerId;
            previousLocalX = localX;
            accumulatedDeltaX = 0f;
            return true;
        }

        public bool TryAddSample(int pointerId, float localX)
        {
            if (activePointerId != pointerId || !IsFinite(localX))
            {
                return false;
            }

            var delta = localX - previousLocalX;
            previousLocalX = localX;
            if (!IsFinite(delta))
            {
                Reset();
                return false;
            }

            accumulatedDeltaX = SaturatingAdd(accumulatedDeltaX, delta);
            return true;
        }

        public bool TryEnd(int pointerId)
        {
            if (activePointerId != pointerId)
            {
                return false;
            }

            Reset();
            return true;
        }

        public float ConsumeAccumulatedDeltaX()
        {
            var result = accumulatedDeltaX;
            accumulatedDeltaX = 0f;
            return result;
        }

        public void Reset()
        {
            activePointerId = NoPointer;
            previousLocalX = 0f;
            accumulatedDeltaX = 0f;
        }

        private static float SaturatingAdd(float left, float right)
        {
            var result = (double)left + right;
            if (result >= float.MaxValue)
            {
                return float.MaxValue;
            }

            if (result <= -float.MaxValue)
            {
                return -float.MaxValue;
            }

            return (float)result;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
