using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public abstract class ElementComboEffectBase : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float visualDuration = 0.2f;

        private float visualElapsed;
        private bool resolved;
        private bool visualActive;

        internal bool ResolveOnce(
            ElementComboHitRequest request,
            IEnemyEffectService enemyService,
            Transform visualRoot)
        {
            if (resolved)
            {
                throw new InvalidOperationException($"{name} has already resolved its current effect.");
            }

            if (gameObject.activeSelf)
            {
                throw new InvalidOperationException($"{name} must remain inactive until gameplay resolution finishes.");
            }

            if (enemyService == null)
            {
                throw new ArgumentNullException(nameof(enemyService));
            }

            if (visualRoot == null)
            {
                throw new ArgumentNullException(nameof(visualRoot));
            }

            transform.SetParent(visualRoot, false);
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (!ResolveGameplay(request, enemyService))
            {
                return false;
            }

            resolved = true;
            visualElapsed = 0f;
            PrepareVisual();
            return true;
        }

        internal void ActivateVisual()
        {
            if (!resolved || gameObject.activeSelf)
            {
                throw new InvalidOperationException($"{name} is not ready for visual activation.");
            }

            gameObject.SetActive(true);
        }

        internal bool TickVisual(float deltaTime)
        {
            if (!resolved || !visualActive)
            {
                return false;
            }

            visualElapsed = Mathf.Min(visualDuration, visualElapsed + deltaTime);
            var normalizedTime = visualDuration <= 0f ? 1f : visualElapsed / visualDuration;
            UpdateVisual(normalizedTime, deltaTime);
            return visualElapsed >= visualDuration;
        }

        internal void PrepareForPool()
        {
            HideVisual();
            visualActive = false;
            resolved = false;
            visualElapsed = 0f;
            ResetRuntime();
            gameObject.SetActive(false);
        }

        public virtual bool TryValidate(out string error)
        {
            if (!IsFinite(visualDuration) || visualDuration <= 0f)
            {
                error = $"{name}.visualDuration must be finite and positive.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        protected abstract bool ResolveGameplay(
            ElementComboHitRequest request,
            IEnemyEffectService enemyService);

        protected abstract void PrepareVisual();
        protected abstract void UpdateVisual(float normalizedTime, float deltaTime);
        protected abstract void HideVisual();
        protected abstract void ResetRuntime();

        protected static Color WithAlpha(Color color, float alphaMultiplier)
        {
            color.a *= Mathf.Clamp01(alphaMultiplier);
            return color;
        }

        protected static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        protected static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        protected static bool ValidateLine(
            LineRenderer line,
            Transform expectedRoot,
            string fieldName,
            out string error)
        {
            if (line == null || !line.transform.IsChildOf(expectedRoot))
            {
                error = $"{expectedRoot.name}.{fieldName} must reference a child LineRenderer.";
                return false;
            }

            if (!line.useWorldSpace || line.sharedMaterial == null)
            {
                error = $"{expectedRoot.name}.{fieldName} requires world-space rendering and a material.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if (!resolved)
            {
                return;
            }

            visualActive = true;
            ShowVisual();
            UpdateVisual(0f, 0f);
        }

        protected virtual void ShowVisual()
        {
        }
    }
}
