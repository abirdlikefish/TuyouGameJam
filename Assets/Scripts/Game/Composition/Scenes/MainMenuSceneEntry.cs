using System;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;

namespace Game.Composition
{
    [DisallowMultipleComponent]
    public sealed class MainMenuSceneEntry : MonoBehaviour, IAppSceneEntry
    {
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

            initialized = true;
            Debug.Log("[MainMenuSceneEntry] Initialized");
        }

        void IAppSceneEntry.Cleanup()
        {
            CleanupInternal();
        }

        private void CleanupInternal()
        {
            if (!initialized)
            {
                return;
            }

            initialized = false;
            Debug.Log("[MainMenuSceneEntry] Cleaned");
        }

        private void OnDestroy()
        {
            CleanupInternal();
        }
    }
}
