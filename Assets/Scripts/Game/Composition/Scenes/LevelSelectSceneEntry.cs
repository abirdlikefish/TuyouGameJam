using System;
using Game.Contracts;
using Game.Foundation;
using Game.Presentation;
using UnityEngine;

namespace Game.Composition
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectSceneEntry : MonoBehaviour, IAppSceneEntry
    {
        [SerializeField] private LevelSelectView levelSelectView;

        private bool initialized;

        AppSceneId IAppSceneEntry.SceneId => AppSceneId.LevelSelect;

        void IAppSceneEntry.Initialize(
            SceneRuntimeLoadRequest request,
            SceneRuntimeDependencies dependencies)
        {
            if (initialized)
            {
                throw new InvalidOperationException("LevelSelectSceneEntry is already initialized.");
            }

            if (request.SceneId != AppSceneId.LevelSelect || request.LevelId != 0 || request.LevelRunId != 0)
            {
                throw new ArgumentException("LevelSelect scene context is invalid.", nameof(request));
            }

            ValidateSceneBindings();
            try
            {
                levelSelectView.Initialize(
                    dependencies.ConfigService.GetLevelDescriptors(),
                    dependencies.GameStateService);
                initialized = true;
                Debug.Log("[LevelSelectSceneEntry] Initialized");
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
            if (levelSelectView != null)
            {
                levelSelectView.Cleanup();
            }

            if (!initialized)
            {
                return;
            }

            initialized = false;
            Debug.Log("[LevelSelectSceneEntry] Cleaned");
        }

        private void ValidateSceneBindings()
        {
            if (levelSelectView == null)
            {
                throw new InvalidOperationException("LevelSelectSceneEntry.levelSelectView is not assigned.");
            }

            if (levelSelectView.gameObject.scene != gameObject.scene ||
                !levelSelectView.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    "LevelSelectSceneEntry.levelSelectView must belong to this LevelSelectRoot hierarchy.");
            }
        }

        private void OnDestroy()
        {
            CleanupInternal();
        }
    }
}
