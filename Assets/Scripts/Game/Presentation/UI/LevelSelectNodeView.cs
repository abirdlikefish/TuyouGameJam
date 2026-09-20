using System;
using Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectNodeView : MonoBehaviour
    {
        [Header("关卡信息")]
        [SerializeField] private TMP_Text levelNameText;

        [Header("解锁状态")]
        [SerializeField] private GameObject unlockedState;
        [SerializeField] private GameObject lockedState;
        [SerializeField] private Button startButton;

        private Action<int> levelRequested;
        private int levelId;
        private bool unlocked;
        private bool initialized;

        public void Initialize(
            LevelDescriptor descriptor,
            bool isUnlocked,
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
            unlocked = isUnlocked;
            levelRequested = onLevelRequested;
            levelNameText.text = descriptor.DisplayName;
            unlockedState.SetActive(isUnlocked);
            lockedState.SetActive(!isUnlocked);
            startButton.interactable = isUnlocked;
            startButton.onClick.AddListener(HandleStartClicked);
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (levelNameText == null)
            {
                error = "LevelSelectNodeView.levelNameText is not assigned.";
                return false;
            }

            if (unlockedState == null || lockedState == null)
            {
                error = "LevelSelectNodeView state roots are not fully assigned.";
                return false;
            }

            if (unlockedState == lockedState)
            {
                error = "LevelSelectNodeView state roots must reference different objects.";
                return false;
            }

            if (startButton == null)
            {
                error = "LevelSelectNodeView.startButton is not assigned.";
                return false;
            }

            if (!levelNameText.transform.IsChildOf(transform) ||
                !unlockedState.transform.IsChildOf(transform) ||
                !lockedState.transform.IsChildOf(transform) ||
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
                startButton.interactable = initialized && unlocked && enabled;
            }
        }

        public void Cleanup()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
                startButton.interactable = false;
            }

            levelRequested = null;
            levelId = 0;
            unlocked = false;
            initialized = false;
        }

        private void HandleStartClicked()
        {
            if (initialized && unlocked)
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
