using System;
using Game.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectNodeView : MonoBehaviour
    {
        [Header("关卡状态")]
        [SerializeField] private GameObject lockedState;
        [SerializeField] private GameObject unlockedState;
        [SerializeField] private Button startButton;

        private Action<int> levelRequested;
        private int levelId;
        private bool selectable;
        private bool initialized;

        public void Initialize(
            LevelDescriptor descriptor,
            bool isUnlocked,
            bool isCompleted,
            Action<int> onLevelRequested)
        {
            if (initialized)
            {
                throw new InvalidOperationException("LevelSelectNodeView is already initialized.");
            }

            if (onLevelRequested == null)
            {
                throw new ArgumentNullException(nameof(onLevelRequested));
            }

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            levelId = descriptor.LevelId;
            selectable = isUnlocked || isCompleted;
            levelRequested = onLevelRequested;
            lockedState.SetActive(!isUnlocked);
            unlockedState.SetActive(isUnlocked && !isCompleted);
            startButton.interactable = selectable;
            startButton.onClick.AddListener(HandleStartClicked);
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (lockedState == null || unlockedState == null)
            {
                error = "LevelSelectNodeView state roots are not fully assigned.";
                return false;
            }

            if (lockedState == unlockedState)
            {
                error = "LevelSelectNodeView state roots must reference different objects.";
                return false;
            }

            if (startButton == null)
            {
                error = "LevelSelectNodeView.startButton is not assigned.";
                return false;
            }

            if (!lockedState.transform.IsChildOf(transform) ||
                !unlockedState.transform.IsChildOf(transform) ||
                !startButton.transform.IsChildOf(transform))
            {
                error = "LevelSelectNodeView bindings must belong to the node hierarchy.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (startButton != null)
            {
                startButton.interactable = initialized && selectable && enabled;
            }
        }

        public void Cleanup()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
                startButton.interactable = false;
            }

            if (lockedState != null)
            {
                lockedState.SetActive(false);
            }

            if (unlockedState != null)
            {
                unlockedState.SetActive(false);
            }

            levelRequested = null;
            levelId = 0;
            selectable = false;
            initialized = false;
        }

        private void HandleStartClicked()
        {
            if (initialized && selectable)
            {
                levelRequested(levelId);
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
