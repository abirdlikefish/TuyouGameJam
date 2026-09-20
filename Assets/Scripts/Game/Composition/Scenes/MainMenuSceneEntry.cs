using System;
using Game.Contracts;
using Game.Foundation;
using Game.Presentation;
using UnityEngine;

namespace Game.Composition
{
    [DisallowMultipleComponent]
    public sealed class MainMenuSceneEntry : MonoBehaviour, IAppSceneEntry
    {
        [SerializeField] private MainMenuView mainMenuView;

        private bool initialized;

        AppSceneId IAppSceneEntry.SceneId => AppSceneId.MainMenu;

        void IAppSceneEntry.Initialize(
            SceneRuntimeLoadRequest request,
            SceneRuntimeDependencies dependencies)
        {
            if (initialized)
            {
                throw new InvalidOperationException("MainMenuSceneEntry is already initialized.");
            }

            if (request.SceneId != AppSceneId.MainMenu || request.LevelId != 0 || request.LevelRunId != 0)
            {
                throw new ArgumentException("MainMenu scene context is invalid.", nameof(request));
            }

            ValidateSceneBindings();
            try
            {
                mainMenuView.Initialize(dependencies.GameStateService);
                initialized = true;
                Debug.Log("[MainMenuSceneEntry] Initialized");
            }
            catch
            {
                CleanupInternal();
                throw;
            }
        }

        void IAppSceneEntry.Cleanup()
        {
            CleanupInternal();
        }

        private void CleanupInternal()
        {
            if (mainMenuView != null)
            {
                mainMenuView.Cleanup();
            }

            if (!initialized)
            {
                return;
            }

            initialized = false;
            Debug.Log("[MainMenuSceneEntry] Cleaned");
        }

        private void ValidateSceneBindings()
        {
            if (mainMenuView == null)
            {
                throw new InvalidOperationException("MainMenuSceneEntry.mainMenuView is not assigned.");
            }

            if (mainMenuView.gameObject.scene != gameObject.scene ||
                !mainMenuView.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    "MainMenuSceneEntry.mainMenuView must belong to this MainMenuRoot hierarchy.");
            }
        }

        private void OnDestroy()
        {
            CleanupInternal();
        }
    }
}
