using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ElementComboManager : MonoBehaviour, IElementComboResolver
    {
        [Header("Gameplay 效果 Prefab")]
        [SerializeField] private FireLightningExplosionEffect fireLightningPrefab;
        [SerializeField] private IceLightningChainEffect iceLightningPrefab;
        [SerializeField] private FireIceSteamEffect fireIcePrefab;

        private readonly List<ElementComboEffectBase> activeEffects =
            new List<ElementComboEffectBase>();
        private IComponentPool<FireLightningExplosionEffect> fireLightningPool;
        private IComponentPool<IceLightningChainEffect> iceLightningPool;
        private IComponentPool<FireIceSteamEffect> fireIcePool;
        private IEnemyEffectService enemyService;
        private ITimeService timeService;
        private Transform visualRoot;
        private int levelRunId;
        private bool initialized;
        private bool resolving;

        public void Initialize(
            IPoolService poolService,
            IEnemyEffectService initializedEnemyService,
            ITimeService initializedTimeService,
            Transform initializedVisualRoot)
        {
            if (initialized)
            {
                throw new InvalidOperationException("ElementComboManager is already initialized.");
            }

            if (poolService == null)
            {
                throw new ArgumentNullException(nameof(poolService));
            }

            enemyService = initializedEnemyService ??
                           throw new ArgumentNullException(nameof(initializedEnemyService));
            timeService = initializedTimeService ?? throw new ArgumentNullException(nameof(initializedTimeService));
            visualRoot = initializedVisualRoot ?? throw new ArgumentNullException(nameof(initializedVisualRoot));
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            fireLightningPool = poolService.GetOrCreatePool(fireLightningPrefab);
            iceLightningPool = poolService.GetOrCreatePool(iceLightningPrefab);
            fireIcePool = poolService.GetOrCreatePool(fireIcePrefab);
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (fireLightningPrefab == null || iceLightningPrefab == null || fireIcePrefab == null)
            {
                error = $"{name} requires all three double-element effect prefabs.";
                return false;
            }

            if (!IsPrefabRoot(fireLightningPrefab) ||
                !IsPrefabRoot(iceLightningPrefab) ||
                !IsPrefabRoot(fireIcePrefab))
            {
                error = $"{name} effect bindings must reference prefab root assets.";
                return false;
            }

            return fireLightningPrefab.TryValidate(out error) &&
                   iceLightningPrefab.TryValidate(out error) &&
                   fireIcePrefab.TryValidate(out error);
        }

        public void Cleanup()
        {
            if (!initialized)
            {
                return;
            }

            for (var index = activeEffects.Count - 1; index >= 0; index--)
            {
                Return(activeEffects[index]);
            }

            activeEffects.Clear();
            resolving = false;
            levelRunId = 0;
            enemyService = null;
            timeService = null;
            visualRoot = null;
            initialized = false;
        }

        void IElementComboResolver.StartRun(int startedLevelRunId)
        {
            EnsureInitialized();
            if (resolving)
            {
                throw new InvalidOperationException("ElementComboManager already has an active run.");
            }

            if (startedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startedLevelRunId));
            }

            levelRunId = startedLevelRunId;
            resolving = true;
        }

        bool IElementComboResolver.TryResolveEnemyHit(ElementComboHitRequest request)
        {
            if (!resolving || request.LevelRunId != levelRunId)
            {
                return false;
            }

            var comboKind = ResolveExactCombo(request.DirectDamage.ActiveElements);
            if (comboKind == ElementComboKind.None)
            {
                return false;
            }

            var effect = Rent(comboKind);
            try
            {
                if (!effect.ResolveOnce(request, enemyService, visualRoot))
                {
                    Return(effect);
                    return false;
                }

                activeEffects.Add(effect);
                effect.ActivateVisual();
                return true;
            }
            catch
            {
                activeEffects.Remove(effect);
                Return(effect);
                throw;
            }
        }

        void IElementComboResolver.StopRun(int stoppedLevelRunId)
        {
            if (resolving && stoppedLevelRunId == levelRunId)
            {
                resolving = false;
                levelRunId = 0;
            }
        }

        private void Update()
        {
            if (!initialized || activeEffects.Count == 0)
            {
                return;
            }

            var deltaTime = timeService.GetDeltaTime(TimeDomain.VFX);
            if (!IsFinite(deltaTime) || deltaTime < 0f)
            {
                throw new InvalidOperationException("VFX delta time must be finite and non-negative.");
            }

            for (var index = activeEffects.Count - 1; index >= 0; index--)
            {
                var effect = activeEffects[index];
                if (effect == null)
                {
                    activeEffects.RemoveAt(index);
                    continue;
                }

                if (!effect.TickVisual(deltaTime))
                {
                    continue;
                }

                activeEffects.RemoveAt(index);
                Return(effect);
            }
        }

        private ElementComboEffectBase Rent(ElementComboKind comboKind)
        {
            switch (comboKind)
            {
                case ElementComboKind.FireLightningExplosion:
                    return fireLightningPool.RentInactive();
                case ElementComboKind.IceLightningChain:
                    return iceLightningPool.RentInactive();
                case ElementComboKind.FireIceSteam:
                    return fireIcePool.RentInactive();
                default:
                    throw new ArgumentOutOfRangeException(nameof(comboKind));
            }
        }

        private void Return(ElementComboEffectBase effect)
        {
            if (effect == null)
            {
                return;
            }

            effect.PrepareForPool();
            if (effect is FireLightningExplosionEffect fireLightning)
            {
                fireLightningPool.Return(fireLightning);
            }
            else if (effect is IceLightningChainEffect iceLightning)
            {
                iceLightningPool.Return(iceLightning);
            }
            else if (effect is FireIceSteamEffect fireIce)
            {
                fireIcePool.Return(fireIce);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported element combo effect type '{effect.GetType().FullName}'.");
            }
        }

        private static ElementComboKind ResolveExactCombo(ElementMask elements)
        {
            // 完整掩码匹配，避免三元素子弹回退触发任一二元素效果。
            switch (elements)
            {
                case ElementMask.Fire | ElementMask.Ice:
                    return ElementComboKind.FireIceSteam;
                case ElementMask.Fire | ElementMask.Lightning:
                    return ElementComboKind.FireLightningExplosion;
                case ElementMask.Ice | ElementMask.Lightning:
                    return ElementComboKind.IceLightningChain;
                default:
                    return ElementComboKind.None;
            }
        }

        private static bool IsPrefabRoot(Component prefab)
        {
            return prefab != null && !prefab.gameObject.scene.IsValid() && prefab.transform.parent == null;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("ElementComboManager has not been initialized.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
