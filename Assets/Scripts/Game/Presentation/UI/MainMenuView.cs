using System;
using Game.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("主界面按钮")]
        [SerializeField] private Button enterLevelSelectButton;
        [SerializeField] private Button quitButton;

        private IGameStateService gameStateService;
        private bool initialized;
        private bool actionRequested;

        public void Initialize(IGameStateService service)
        {
            if (initialized)
            {
                throw new InvalidOperationException("MainMenuView is already initialized.");
            }

            gameStateService = service ?? throw new ArgumentNullException(nameof(service));
            ValidateBindings();

            enterLevelSelectButton.onClick.AddListener(HandleEnterLevelSelectClicked);
            quitButton.onClick.AddListener(HandleQuitClicked);
            SetButtonsInteractable(true);
            actionRequested = false;
            initialized = true;
        }

        public void Cleanup()
        {
            if (enterLevelSelectButton != null)
            {
                enterLevelSelectButton.onClick.RemoveListener(HandleEnterLevelSelectClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(HandleQuitClicked);
            }

            gameStateService = null;
            actionRequested = false;
            initialized = false;
        }

        private void ValidateBindings()
        {
            RequireButton(enterLevelSelectButton, nameof(enterLevelSelectButton));
            RequireButton(quitButton, nameof(quitButton));

            if (enterLevelSelectButton == quitButton)
            {
                throw new InvalidOperationException("MainMenuView buttons must reference different objects.");
            }
        }

        private void RequireButton(Button button, string fieldName)
        {
            if (button == null)
            {
                throw new InvalidOperationException($"MainMenuView.{fieldName} is not assigned.");
            }

            if (button.gameObject.scene != gameObject.scene || !button.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    $"MainMenuView.{fieldName} must belong to this MainMenuView hierarchy.");
            }
        }

        private void HandleEnterLevelSelectClicked()
        {
            if (!initialized || actionRequested || !gameStateService.TryEnterLevelSelect())
            {
                return;
            }

            actionRequested = true;
            SetButtonsInteractable(false);
        }

        private void HandleQuitClicked()
        {
            if (!initialized || actionRequested)
            {
                return;
            }

            actionRequested = true;
            SetButtonsInteractable(false);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetButtonsInteractable(bool interactable)
        {
            enterLevelSelectButton.interactable = interactable;
            quitButton.interactable = interactable;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
