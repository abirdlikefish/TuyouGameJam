using System;
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
            return TryGetWorldSize(out _, out error);
        }

        public Vector2 GetWorldSize()
        {
            if (!TryGetWorldSize(out var worldSize, out var error))
            {
                throw new InvalidOperationException(error);
            }

            return worldSize;
        }

        private bool TryGetWorldSize(out Vector2 worldSize, out string error)
        {
            worldSize = default(Vector2);
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

            // Renderer bounds 已包含 visualRoot 及父级缩放，是道路实际世界尺寸的唯一来源。
            var visualBounds = visualRenderer.bounds;
            var visualSize = visualBounds.size;
            if (!IsFinite(visualSize.x) || !IsFinite(visualSize.y) ||
                visualSize.x <= 0f || visualSize.y <= 0f)
            {
                error = $"{name}.visualRoot must have positive finite world dimensions.";
                return false;
            }

            var visualCenter = visualBounds.center;
            if (!IsFinite(visualCenter.x) || !IsFinite(visualCenter.y) ||
                !Mathf.Approximately(visualCenter.x, 0f) ||
                !Mathf.Approximately(visualCenter.y, 0f))
            {
                error = $"{name}.visualRoot visual bounds must be centered at the world origin.";
                return false;
            }

            worldSize = new Vector2(visualSize.x, visualSize.y);
            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
