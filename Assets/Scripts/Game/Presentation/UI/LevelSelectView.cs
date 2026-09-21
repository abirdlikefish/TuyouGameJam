using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectView : MonoBehaviour
    {
        [Serializable]
        private sealed class LevelNodeBinding
        {
            [SerializeField] private LevelSelectNodeView node;
            [SerializeField] private int levelId;

            public LevelSelectNodeView Node => node;
            public int LevelId => levelId;
        }

        [Header("关卡节点绑定")]
        [SerializeField] private LevelNodeBinding[] nodeBindings = new LevelNodeBinding[0];

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
            try
            {
                var descriptorsById = ValidateBindings(descriptors);
                for (var index = 0; index < nodeBindings.Length; index++)
                {
                    var binding = nodeBindings[index];
                    var node = binding.Node;
                    var descriptor = descriptorsById[binding.LevelId];
                    nodes.Add(node);
                    node.Initialize(
                        descriptor,
                        gameStateService.IsLevelUnlocked(descriptor.LevelId),
                        gameStateService.IsLevelCompleted(descriptor.LevelId),
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
            }

            nodes.Clear();
            gameStateService = null;
            actionRequested = false;
            initialized = false;
        }

        private Dictionary<int, LevelDescriptor> ValidateBindings(
            IReadOnlyList<LevelDescriptor> descriptors)
        {
            if (nodeBindings == null || nodeBindings.Length == 0)
            {
                throw new InvalidOperationException(
                    "LevelSelectView requires at least one Inspector node binding.");
            }

            var descriptorsById = new Dictionary<int, LevelDescriptor>(descriptors.Count);
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (!descriptorsById.TryAdd(descriptor.LevelId, descriptor))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView received duplicate descriptor LevelId {descriptor.LevelId}.");
                }
            }

            var boundNodes = new HashSet<LevelSelectNodeView>();
            var boundLevelIds = new HashSet<int>();
            for (var index = 0; index < nodeBindings.Length; index++)
            {
                var binding = nodeBindings[index];
                if (binding == null)
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}] is null.");
                }

                var node = binding.Node;
                if (node == null)
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}].node is not assigned.");
                }

                if (node.gameObject.scene != gameObject.scene || !node.transform.IsChildOf(transform))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}].node must belong to the " +
                        "LevelSelectView hierarchy.");
                }

                if (!boundNodes.Add(node))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}] repeats node '{node.name}'.");
                }

                if (!boundLevelIds.Add(binding.LevelId))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}] repeats LevelId {binding.LevelId}.");
                }

                if (!descriptorsById.ContainsKey(binding.LevelId))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}] references unknown LevelId " +
                        $"{binding.LevelId}.");
                }

                if (!node.TryValidate(out var nodeError))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView.nodeBindings[{index}] is invalid: {nodeError}");
                }
            }

            foreach (var pair in descriptorsById)
            {
                if (!boundLevelIds.Contains(pair.Key))
                {
                    throw new InvalidOperationException(
                        $"LevelSelectView has no node binding for LevelId {pair.Key}.");
                }
            }

            return descriptorsById;
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
