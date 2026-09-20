using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(fileName = "UnityResourceRegistry", menuName = "Game/Configuration/Unity Resource Registry")]
    public sealed class UnityResourceRegistry : ScriptableObject, IResourceRegistry
    {
        [SerializeField] private ResourceBinding[] bindings = new ResourceBinding[0];

        private Dictionary<string, UnityEngine.Object> assetsByKey;

        public bool TryInitialize(out string error)
        {
            var newAssetsByKey = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            if (bindings == null)
            {
                error = $"{name}.bindings cannot be null.";
                return false;
            }

            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                if (binding == null)
                {
                    error = $"{name}.bindings[{index}] is null.";
                    return false;
                }

                if (!IsValidKey(binding.Key))
                {
                    error =
                        $"{name}.bindings[{index}].key must use case-sensitive ASCII Category/Identity format.";
                    return false;
                }

                if (binding.Asset == null)
                {
                    error = $"{name}.bindings[{index}].asset is not assigned for key '{binding.Key}'.";
                    return false;
                }

                if (!newAssetsByKey.TryAdd(binding.Key, binding.Asset))
                {
                    error = $"{name}.bindings contains duplicate key '{binding.Key}'.";
                    return false;
                }
            }

            assetsByKey = newAssetsByKey;
            error = string.Empty;
            return true;
        }

        public bool TryGet<T>(string key, out T asset) where T : UnityEngine.Object
        {
            if (assetsByKey == null)
            {
                throw new InvalidOperationException("UnityResourceRegistry must be initialized before queries.");
            }

            if (key != null && assetsByKey.TryGetValue(key, out var untypedAsset))
            {
                asset = untypedAsset as T;
                return asset != null;
            }

            asset = null;
            return false;
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key[0] == '/' || key[key.Length - 1] == '/')
            {
                return false;
            }

            var slashCount = 0;
            for (var index = 0; index < key.Length; index++)
            {
                var character = key[index];
                if (character > 127 || char.IsWhiteSpace(character) || character == '\\')
                {
                    return false;
                }

                if (character == '/')
                {
                    slashCount++;
                    if (index > 0 && key[index - 1] == '/')
                    {
                        return false;
                    }
                }
            }

            return slashCount >= 1;
        }

        [Serializable]
        private sealed class ResourceBinding
        {
            [SerializeField] private string key = string.Empty;
            [SerializeField] private UnityEngine.Object asset;

            public string Key => key;
            public UnityEngine.Object Asset => asset;
        }
    }
}
