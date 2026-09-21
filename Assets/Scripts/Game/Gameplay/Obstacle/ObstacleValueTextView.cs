using System;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ObstacleValueTextView : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField, Min(1.01f)] private float pulseScaleMultiplier = 1.25f;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.08f;

        private Vector3 restLocalScale;
        private float pulseElapsed;
        private bool restScaleCaptured;
        private bool pulsePlaying;

        public bool TryValidate(out string error)
        {
            if (targetText == null || targetText.gameObject != gameObject)
            {
                error = $"{name} requires a same-node TMP_Text binding.";
                return false;
            }

            if (!IsFinite(pulseScaleMultiplier) || pulseScaleMultiplier <= 1f)
            {
                error = $"{name}.pulseScaleMultiplier must be finite and greater than one.";
                return false;
            }

            if (!IsFinite(pulseDuration) || pulseDuration <= 0f)
            {
                error = $"{name}.pulseDuration must be finite and greater than zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Initialize(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            CaptureRestScale();
            StopPulse();
            targetText.enabled = true;
            targetText.text = value;
        }

        public void SetTextAndTryPulse(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            CaptureRestScale();
            targetText.text = value;
            if (pulsePlaying)
            {
                return;
            }

            pulsePlaying = true;
            pulseElapsed = 0f;
            ApplyScale(pulseScaleMultiplier);
        }

        public void Hide()
        {
            StopPulse();
            targetText.enabled = false;
        }

        public void Tick(float deltaTime)
        {
            if (!pulsePlaying)
            {
                return;
            }

            pulseElapsed += Mathf.Max(0f, deltaTime);
            var normalizedTime = Mathf.Clamp01(pulseElapsed / pulseDuration);
            ApplyScale(Mathf.Lerp(pulseScaleMultiplier, 1f, normalizedTime));
            if (normalizedTime >= 1f)
            {
                StopPulse();
            }
        }

        public void ResetForPool()
        {
            CaptureRestScale();
            StopPulse();
            targetText.enabled = true;
            targetText.text = string.Empty;
        }

        private void OnDisable()
        {
            StopPulse();
        }

        private void OnValidate()
        {
            pulseScaleMultiplier = Mathf.Max(1.01f, pulseScaleMultiplier);
            pulseDuration = Mathf.Max(0.01f, pulseDuration);
        }

        private void CaptureRestScale()
        {
            if (restScaleCaptured)
            {
                return;
            }

            restLocalScale = transform.localScale;
            restScaleCaptured = true;
        }

        private void ApplyScale(float multiplier)
        {
            transform.localScale = new Vector3(
                restLocalScale.x * multiplier,
                restLocalScale.y * multiplier,
                restLocalScale.z);
        }

        private void StopPulse()
        {
            pulsePlaying = false;
            pulseElapsed = 0f;
            if (restScaleCaptured)
            {
                transform.localScale = restLocalScale;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
