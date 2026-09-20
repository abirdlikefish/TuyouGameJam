using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RoadView : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer visualRenderer;

        public bool TryValidate(out string error)
        {
            if (visualRoot == null)
            {
                error = $"{name}.visualRoot is not assigned.";
                return false;
            }

            if (visualRenderer == null)
            {
                error = $"{name}.visualRenderer is not assigned.";
                return false;
            }

            if (visualRenderer.transform != visualRoot)
            {
                error = $"{name}.visualRenderer must be on visualRoot.";
                return false;
            }

            if (visualRenderer.sprite == null)
            {
                error = $"{name}.visualRenderer.sprite is not assigned.";
                return false;
            }

            var spriteSize = visualRenderer.sprite.bounds.size;
            if (!IsFinite(spriteSize.x) || !IsFinite(spriteSize.y) ||
                spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                error = $"{name}.visualRenderer.sprite must have positive finite dimensions.";
                return false;
            }

            var parentScale = visualRoot.parent != null
                ? visualRoot.parent.lossyScale
                : Vector3.one;
            if (!IsFinite(parentScale.x) || !IsFinite(parentScale.y) ||
                Mathf.Approximately(parentScale.x, 0f) || Mathf.Approximately(parentScale.y, 0f))
            {
                error = $"{name}.visualRoot parent scale must be finite and non-zero on X and Y.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void ApplyLayout(RoadLayoutSnapshot layout)
        {
            ValidateLayout(layout);
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            var centerX = (layout.LeftBoundary + layout.RightBoundary) * 0.5f;
            var centerY = (layout.BottomBoundary + layout.TopBoundary) * 0.5f;
            var position = visualRoot.position;
            visualRoot.position = new Vector3(centerX, centerY, position.z);

            var spriteSize = visualRenderer.sprite.bounds.size;
            var parentScale = visualRoot.parent != null
                ? visualRoot.parent.lossyScale
                : Vector3.one;
            var localScale = visualRoot.localScale;
            var signX = localScale.x < 0f ? -1f : 1f;
            var signY = localScale.y < 0f ? -1f : 1f;
            visualRoot.localScale = new Vector3(
                signX * layout.Width / (spriteSize.x * Mathf.Abs(parentScale.x)),
                signY * layout.Height / (spriteSize.y * Mathf.Abs(parentScale.y)),
                localScale.z);
        }

        private static void ValidateLayout(RoadLayoutSnapshot layout)
        {
            if (!IsFinite(layout.Width) || !IsFinite(layout.Height) ||
                !IsFinite(layout.LeftBoundary) || !IsFinite(layout.RightBoundary) ||
                !IsFinite(layout.BottomBoundary) || !IsFinite(layout.TopBoundary) ||
                layout.Width <= 0f || layout.Height <= 0f ||
                layout.RightBoundary <= layout.LeftBoundary ||
                layout.TopBoundary <= layout.BottomBoundary)
            {
                throw new ArgumentException("Road layout contains invalid visual bounds.", nameof(layout));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
