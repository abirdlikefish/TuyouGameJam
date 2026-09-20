using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectView : MonoBehaviour
    {
        [Header("节点生成")]
        [SerializeField] private RectTransform nodeContainer;
        [SerializeField] private LevelSelectNodeView nodePrefab;

        private readonly List<LevelSelectNodeView> nodes = new List<LevelSelectNodeView>();
        private IGameStateService gameStateService;
        private bool actionRequested;
        private bool initialized;

        public void Initialize(
            IReadOnlyList<LevelDescriptor> descriptors,
            IGameStateService service)
        {
            if (initialized || nodes.Count != 0)
            {
                throw new InvalidOperationException("LevelSelectView is already initialized.");
            }

            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            if (descriptors.Count == 0)
            {
                throw new InvalidOperationException("LevelSelectView requires at least one level descriptor.");
            }

            gameStateService = service ?? throw new ArgumentNullException(nameof(service));
            ValidateBindings();

            var levelIds = new HashSet<int>();
            try
            {
                for (var index = 0; index < descriptors.Count; index++)
                {
                    var descriptor = descriptors[index];
                    if (!levelIds.Add(descriptor.LevelId))
                    {
                        throw new InvalidOperationException(
                            $"LevelSelectView received duplicate LevelId {descriptor.LevelId}.");
                    }

                    var node = Instantiate(nodePrefab, nodeContainer, false);
                    node.name = $"LevelNode_{descriptor.LevelId}";
                    nodes.Add(node);
                    node.Initialize(
                        descriptor,
                        gameStateService.IsLevelUnlocked(descriptor.LevelId),
                        HandleLevelRequested);
                }

                actionRequested = false;
                initialized = true;
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        public void Cleanup()
        {
            for (var index = nodes.Count - 1; index >= 0; index--)
            {
                var node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                node.Cleanup();
                Destroy(node.gameObject);
            }

            nodes.Clear();
            gameStateService = null;
            actionRequested = false;
            initialized = false;
        }

        private void ValidateBindings()
        {
            if (nodeContainer == null)
            {
                throw new InvalidOperationException("LevelSelectView.nodeContainer is not assigned.");
            }

            if (nodeContainer.gameObject.scene != gameObject.scene ||
                !nodeContainer.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    "LevelSelectView.nodeContainer must belong to the LevelSelectView hierarchy.");
            }

            if (nodeContainer.childCount != 0)
            {
                throw new InvalidOperationException(
                    "LevelSelectView.nodeContainer must be empty before initialization.");
            }

            if (nodePrefab == null || nodePrefab.gameObject.scene.IsValid() ||
                nodePrefab.transform.parent != null)
            {
                throw new InvalidOperationException(
                    "LevelSelectView.nodePrefab must reference a prefab root asset.");
            }

            if (!nodePrefab.TryValidate(out var prefabError))
            {
                throw new InvalidOperationException(prefabError);
            }
        }

        private void HandleLevelRequested(int levelId)
        {
            if (!initialized || actionRequested)
            {
                return;
            }

            if (!gameStateService.TrySelectLevel(levelId))
            {
                Debug.LogWarning($"[LevelSelectView] Level selection rejected; LevelId={levelId}");
                return;
            }

            if (!gameStateService.TryStartSelectedGameplay())
            {
                Debug.LogError($"[LevelSelectView] Gameplay start rejected; LevelId={levelId}");
                return;
            }

            actionRequested = true;
            for (var index = 0; index < nodes.Count; index++)
            {
                nodes[index].SetInteractionEnabled(false);
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
