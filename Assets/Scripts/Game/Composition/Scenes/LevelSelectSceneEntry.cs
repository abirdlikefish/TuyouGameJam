using System;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;

namespace Game.Composition
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectSceneEntry : MonoBehaviour, IAppSceneEntry
    {
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

            initialized = true;
            Debug.Log("[LevelSelectSceneEntry] Initialized");
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
            Debug.Log("[LevelSelectSceneEntry] Cleaned");
        }

        private void OnDestroy()
        {
            CleanupInternal();
        }
    }
}
