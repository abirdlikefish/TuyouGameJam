using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    internal sealed class ComponentPool<T> : IComponentPool<T>, IDisposable where T : MonoBehaviour
    {
        private readonly T prefab;
        private readonly Transform poolRoot;
        private readonly Action<string> reportDiagnostic;
        private readonly Stack<T> availableInstances = new Stack<T>();
        private readonly HashSet<T> allInstances = new HashSet<T>();
        private readonly HashSet<T> rentedInstances = new HashSet<T>();

        private bool isDisposed;

        public ComponentPool(T prefab, Transform poolRoot, Action<string> reportDiagnostic)
        {
            this.prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            this.poolRoot = poolRoot != null ? poolRoot : throw new ArgumentNullException(nameof(poolRoot));
            this.reportDiagnostic = reportDiagnostic;
        }

        public T RentInactive()
        {
            ThrowIfDisposed();

            var instance = TakeAvailableInstance();
            if (instance == null)
            {
                instance = CreateInactiveInstance();
            }

            instance.gameObject.SetActive(false);
            if (!rentedInstances.Add(instance))
            {
                throw new InvalidOperationException(
                    $"Pool type '{typeof(T).FullName}' attempted to rent an instance that is already marked as rented.");
            }

            return instance;
        }

        public bool Return(T instance)
        {
            if (isDisposed)
            {
                ReportDiagnostic($"Pool type '{typeof(T).FullName}' rejected a return after disposal.");
                return false;
            }

            if (instance == null)
            {
                ReportDiagnostic($"Pool type '{typeof(T).FullName}' rejected a null instance return.");
                return false;
            }

            if (!allInstances.Contains(instance))
            {
                ReportDiagnostic(
                    $"Pool type '{typeof(T).FullName}' rejected unknown or cross-pool instance '{instance.name}'.");
                return false;
            }

            if (!rentedInstances.Remove(instance))
            {
                ReportDiagnostic(
                    $"Pool type '{typeof(T).FullName}' rejected duplicate return for instance '{instance.name}'.");
                return false;
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(poolRoot, false);
            availableInstances.Push(instance);
            return true;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            foreach (var instance in allInstances)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance.gameObject);
                }
            }

            availableInstances.Clear();
            rentedInstances.Clear();
            allInstances.Clear();

            if (poolRoot != null)
            {
                UnityEngine.Object.Destroy(poolRoot.gameObject);
            }
        }

        private T TakeAvailableInstance()
        {
            while (availableInstances.Count > 0)
            {
                var instance = availableInstances.Pop();
                if (instance != null)
                {
                    return instance;
                }

                allInstances.Remove(instance);
            }

            return null;
        }

        private T CreateInactiveInstance()
        {
            try
            {
                // 非激活父节点确保首次实例化不会在 Manager 注入租用上下文前触发有效 OnEnable。
                var instance = UnityEngine.Object.Instantiate(prefab, poolRoot, false);
                if (instance == null)
                {
                    throw new InvalidOperationException("Unity returned a null component instance.");
                }

                instance.gameObject.SetActive(false);
                allInstances.Add(instance);
                return instance;
            }
            catch (Exception exception)
            {
                var message = $"Pool type '{typeof(T).FullName}' failed to instantiate its canonical prefab.";
                ReportDiagnostic(message);
                throw new InvalidOperationException(message, exception);
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
                // 诊断失败不能改变借出或归还的权威结果。
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException($"ComponentPool<{typeof(T).FullName}>");
            }
        }
    }
}
