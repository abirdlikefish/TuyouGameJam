using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    public sealed class PoolService : IPoolService, IDisposable
    {
        private readonly Transform persistentPoolRoot;
        private readonly Action<string> reportDiagnostic;
        private readonly Dictionary<Type, PoolRegistration> registrations =
            new Dictionary<Type, PoolRegistration>();

        private bool isDisposed;

        public PoolService(Transform persistentPoolRoot, Action<string> reportDiagnostic = null)
        {
            this.persistentPoolRoot = persistentPoolRoot != null
                ? persistentPoolRoot
                : throw new ArgumentNullException(nameof(persistentPoolRoot));
            this.reportDiagnostic = reportDiagnostic;
        }

        public IComponentPool<T> GetOrCreatePool<T>(T prefab) where T : MonoBehaviour
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(PoolService));
            }

            ValidatePrefab(prefab);

            var componentType = typeof(T);
            if (registrations.TryGetValue(componentType, out var existingRegistration))
            {
                var existingPrefab = (T)existingRegistration.Prefab;
                if (existingPrefab != prefab)
                {
                    var message =
                        $"Pool type '{componentType.FullName}' is already bound to prefab '{existingPrefab.name}' " +
                        $"and cannot be rebound to '{prefab.name}'.";
                    ReportDiagnostic(message);
                    throw new InvalidOperationException(message);
                }

                return (IComponentPool<T>)existingRegistration.Pool;
            }

            var poolRootObject = new GameObject($"{componentType.Name}Pool");
            poolRootObject.SetActive(false);
            poolRootObject.transform.SetParent(persistentPoolRoot, false);

            var pool = new ComponentPool<T>(prefab, poolRootObject.transform, ReportDiagnostic);
            registrations.Add(componentType, new PoolRegistration(prefab, pool, pool));
            return pool;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            foreach (var registration in registrations.Values)
            {
                registration.Lifetime.Dispose();
            }

            registrations.Clear();
        }

        private void ValidatePrefab<T>(T prefab) where T : MonoBehaviour
        {
            if (prefab == null)
            {
                const string message = "Pool prefab cannot be null.";
                ReportDiagnostic(message);
                throw new ArgumentNullException(nameof(prefab), message);
            }

            if (prefab.gameObject.scene.IsValid())
            {
                var message =
                    $"Pool type '{typeof(T).FullName}' requires a prefab asset, but '{prefab.name}' is a scene instance.";
                ReportDiagnostic(message);
                throw new ArgumentException(message, nameof(prefab));
            }

            if (prefab.transform.parent != null)
            {
                var message =
                    $"Pool component '{typeof(T).FullName}' must be attached to the prefab root GameObject.";
                ReportDiagnostic(message);
                throw new ArgumentException(message, nameof(prefab));
            }
        }

        private void ReportDiagnostic(string message)
        {
            if (reportDiagnostic == null)
            {
                return;
            }

            try
            {
                reportDiagnostic(message);
            }
            catch
            {
                // 诊断失败不能改变注册、借出或归还的权威结果。
            }
        }

        private sealed class PoolRegistration
        {
            public PoolRegistration(UnityEngine.Object prefab, object pool, IDisposable lifetime)
            {
                Prefab = prefab;
                Pool = pool;
                Lifetime = lifetime;
            }

            public UnityEngine.Object Prefab { get; }
            public object Pool { get; }
            public IDisposable Lifetime { get; }
        }
    }
}
