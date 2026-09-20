using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    public sealed class TouchDragInput : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler,
        IEndDragHandler,
        ICancelHandler
    {
        [SerializeField] private RectTransform touchArea;
        [SerializeField] private Graphic raycastGraphic;
        [SerializeField, Min(0f)] private float horizontalMultiplier = 1f;

        private readonly RelativeDragTracker tracker = new RelativeDragTracker();

        public bool HasActivePointer => tracker.HasActivePointer;

        public bool TryValidate(out string error)
        {
            if (touchArea == null)
            {
                error = $"{name}.touchArea is not assigned.";
                return false;
            }

            if (raycastGraphic == null)
            {
                error = $"{name}.raycastGraphic is not assigned.";
                return false;
            }

            if (raycastGraphic.gameObject != touchArea.gameObject)
            {
                error = $"{name}.raycastGraphic must be on the touchArea GameObject.";
                return false;
            }

            if (!raycastGraphic.raycastTarget)
            {
                error = $"{name}.raycastGraphic must have Raycast Target enabled.";
                return false;
            }

            if (raycastGraphic.color.a > 0.001f)
            {
                error = $"{name}.raycastGraphic must be transparent (alpha 0).";
                return false;
            }

            if (!IsFinite(horizontalMultiplier) || horizontalMultiplier < 0f)
            {
                error = $"{name}.horizontalMultiplier must be finite and non-negative.";
                return false;
            }

            var width = touchArea.rect.width;
            if (!IsFinite(width) || width <= 0f)
            {
                error = $"{name}.touchArea width must be finite and greater than zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public float ConsumeHorizontalInput(float unscaledDeltaTime)
        {
            var accumulatedDeltaX = tracker.ConsumeAccumulatedDeltaX();
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime <= 0f || touchArea == null)
            {
                return 0f;
            }

            var width = touchArea.rect.width;
            if (!IsFinite(width) || width <= 0f || !IsFinite(horizontalMultiplier))
            {
                return 0f;
            }

            var normalizedVelocity = (accumulatedDeltaX / width) / unscaledDeltaTime;
            if (!IsFinite(normalizedVelocity))
            {
                return 0f;
            }

            return Mathf.Clamp(normalizedVelocity, -1f, 1f) * horizontalMultiplier;
        }

        public void ResetInput()
        {
            tracker.Reset();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (TryGetClampedLocalX(eventData, out var localX))
            {
                tracker.TryBegin(eventData.pointerId, localX);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || !tracker.HasActivePointer)
            {
                return;
            }

            if (TryGetClampedLocalX(eventData, out var localX))
            {
                tracker.TryAddSample(eventData.pointerId, localX);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null)
            {
                tracker.TryEnd(eventData.pointerId);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData != null)
            {
                tracker.TryEnd(eventData.pointerId);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            ResetInput();
        }

        private void OnDisable()
        {
            ResetInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetInput();
            }
        }

        private bool TryGetClampedLocalX(PointerEventData eventData, out float localX)
        {
            localX = 0f;
            if (touchArea == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    touchArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint) ||
                !IsFinite(localPoint.x))
            {
                return false;
            }

            var rect = touchArea.rect;
            localX = Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax);
            return IsFinite(localX);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
