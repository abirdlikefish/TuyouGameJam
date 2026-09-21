using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay
{
    public sealed class EnemyManager : MonoBehaviour, IEnemyManager, IEnemyEffectService
    {
        [FormerlySerializedAs("normalPrefab")]
        [SerializeField] private ChickMonster chickPrefab;
        [FormerlySerializedAs("elitePrefab")]
        [SerializeField] private HenMonster henPrefab;
        [FormerlySerializedAs("bossPrefab")]
        [SerializeField] private RoosterMonster roosterPrefab;
        [SerializeField] private IkunMonster ikunPrefab;
        [SerializeField] private LayerMask enemyBodyLayers;
        [SerializeField] private LayerMask armySlotLayers;

        private readonly List<MonsterBase> activeMonsters = new List<MonsterBase>();
        private readonly HashSet<MonsterBase> pendingRecycles = new HashSet<MonsterBase>();
        private readonly HashSet<int> reportedDeaths = new HashSet<int>();
        private readonly Dictionary<int, MonsterBase> monstersByRuntimeId =
            new Dictionary<int, MonsterBase>();
        private readonly Dictionary<int, Vector2> pendingDisplacements =
            new Dictionary<int, Vector2>();
        private readonly Collider2D[] overlapResults = new Collider2D[64];
        private readonly HashSet<int> hitSlotIndices = new HashSet<int>();

        private IComponentPool<ChickMonster> chickPool;
        private IComponentPool<HenMonster> henPool;
        private IComponentPool<RoosterMonster> roosterPool;
        private IComponentPool<IkunMonster> ikunPool;
        private IEnemyConfigProvider configProvider;
        private IBasketballSpawner basketballSpawner;
        private IArmyController army;
        private IEventBus eventBus;
        private int levelRunId;
        private int nextRuntimeInstanceId;
        private int aliveEnemyCount;
        private int ikunBasketballConfigId;
        private float enemyApproachY;
        private bool initialized;
        private bool running;

        public void Initialize(
            IPoolService poolService,
            IEnemyConfigProvider enemyConfigProvider,
            IArmyController armyController,
            IEventBus initializedEventBus,
            IBasketballSpawner initializedBasketballSpawner)
        {
            if (poolService == null)
            {
                throw new ArgumentNullException(nameof(poolService));
            }

            configProvider = enemyConfigProvider ?? throw new ArgumentNullException(nameof(enemyConfigProvider));
            army = armyController ?? throw new ArgumentNullException(nameof(armyController));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            basketballSpawner = initializedBasketballSpawner ??
                                throw new ArgumentNullException(nameof(initializedBasketballSpawner));
            if (initialized)
            {
                throw new InvalidOperationException("EnemyManager is already initialized.");
            }

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            chickPool = poolService.GetOrCreatePool(chickPrefab);
            henPool = poolService.GetOrCreatePool(henPrefab);
            roosterPool = poolService.GetOrCreatePool(roosterPrefab);
            ikunPool = poolService.GetOrCreatePool(ikunPrefab);
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (chickPrefab == null || henPrefab == null || roosterPrefab == null || ikunPrefab == null)
            {
                error = $"{name} requires Chick, Hen, Rooster and Ikun prefab bindings.";
                return false;
            }

            if (enemyBodyLayers.value == 0)
            {
                error = $"{name}.enemyBodyLayers is empty.";
                return false;
            }

            if (armySlotLayers.value == 0)
            {
                error = $"{name}.armySlotLayers is empty.";
                return false;
            }

            return chickPrefab.TryValidate(out error) &&
                   henPrefab.TryValidate(out error) &&
                   roosterPrefab.TryValidate(out error) &&
                   ikunPrefab.TryValidate(out error);
        }

        public void StartRun(
            int startedLevelRunId,
            RoadLayoutSnapshot roadLayout,
            int initializedIkunBasketballConfigId)
        {
            EnsureInitialized();
            if (running)
            {
                throw new InvalidOperationException("EnemyManager already has an active run.");
            }

            if (startedLevelRunId <= 0 || initializedIkunBasketballConfigId < 0 ||
                !IsFinite(roadLayout.EnemyApproachY))
            {
                throw new ArgumentException("Enemy run configuration is invalid.");
            }

            levelRunId = startedLevelRunId;
            ikunBasketballConfigId = initializedIkunBasketballConfigId;
            enemyApproachY = roadLayout.EnemyApproachY;
            nextRuntimeInstanceId = 0;
            aliveEnemyCount = 0;
            activeMonsters.Clear();
            monstersByRuntimeId.Clear();
            pendingDisplacements.Clear();
            pendingRecycles.Clear();
            reportedDeaths.Clear();
            running = true;
        }

        public void Spawn(EnemySpawnRequest request)
        {
            if (!IsCurrentRun(request.LevelRunId))
            {
                return;
            }

            if (request.SpawnEntryIndex < 0 || request.ConfigId < 0 ||
                !IsFinite(request.WorldPosition) || !IsFinite(request.SpawnPosition))
            {
                throw new ArgumentException("Enemy spawn request contains invalid values.", nameof(request));
            }

            var config = configProvider.GetEnemyConfig(request.ConfigId);
            var monster = Rent(config.EnemyType);
            var runtimeId = nextRuntimeInstanceId++;
            if (monster is IkunMonster ikun)
            {
                ikun.ConfigureBasketballSpawner(OnBasketballSpawnRequested);
            }

            monster.InitializeRuntime(
                request,
                config,
                runtimeId,
                enemyApproachY,
                army,
                OnMonsterDamaged,
                OnMonsterDied,
                OnMonsterRecycleRequested,
                transform);
            activeMonsters.Add(monster);
            monstersByRuntimeId.Add(runtimeId, monster);
            aliveEnemyCount++;
            monster.gameObject.SetActive(true);

            eventBus.Publish(
                new MonsterSpawned(
                    levelRunId,
                    runtimeId,
                    request.SpawnEntryIndex,
                    request.ConfigId,
                    config.EnemyType,
                    request.SpawnPosition,
                    request.WorldPosition));
        }

        public void TickMovement(int tickingLevelRunId, float monsterDeltaTime)
        {
            if (!IsCurrentRun(tickingLevelRunId))
            {
                return;
            }

            if (!IsFinite(monsterDeltaTime) || monsterDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(monsterDeltaTime));
            }

            var filter = new ContactFilter2D();
            filter.SetLayerMask(enemyBodyLayers);
            filter.useTriggers = true;
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                activeMonsters[index]?.TickMovement(monsterDeltaTime, filter);
            }
        }

        public void ResolveAttacks(int resolvingLevelRunId, float monsterDeltaTime)
        {
            if (!IsCurrentRun(resolvingLevelRunId))
            {
                return;
            }

            if (!IsFinite(monsterDeltaTime) || monsterDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(monsterDeltaTime));
            }

            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var monster = activeMonsters[index];
                if (monster == null || !monster.IsAlive ||
                    !monster.TryConsumeAttackRequest(out var request))
                {
                    continue;
                }

                if (monster.AttackType == AttackType.SingleTarget)
                {
                    ResolveSingleTargetAttack(monster, request.TargetSlotIndex);
                }
                else
                {
                    ResolveAreaAttack(monster);
                }
            }
        }

        public void ApplyPendingDisplacements(int applyingLevelRunId)
        {
            if (!IsCurrentRun(applyingLevelRunId))
            {
                return;
            }

            foreach (var pair in pendingDisplacements)
            {
                if (monstersByRuntimeId.TryGetValue(pair.Key, out var monster) &&
                    monster != null && monster.IsRuntimeActive)
                {
                    monster.ApplyDisplacement(pair.Value);
                }
            }

            pendingDisplacements.Clear();
        }

        public void FlushPendingRecycles(int flushingLevelRunId)
        {
            if (!IsCurrentRun(flushingLevelRunId))
            {
                return;
            }

            for (var index = activeMonsters.Count - 1; index >= 0; index--)
            {
                var monster = activeMonsters[index];
                if (monster == null || !pendingRecycles.Contains(monster))
                {
                    continue;
                }

                pendingRecycles.Remove(monster);
                activeMonsters.RemoveAt(index);
                monstersByRuntimeId.Remove(monster.RuntimeInstanceId);
                pendingDisplacements.Remove(monster.RuntimeInstanceId);
                Return(monster);
            }
        }

        public int GetAliveEnemyCount()
        {
            return aliveEnemyCount;
        }

        public int GetActiveEnemyCount()
        {
            return activeMonsters.Count;
        }

        public void StopRun(int stoppedLevelRunId)
        {
            if (!IsCurrentRun(stoppedLevelRunId))
            {
                return;
            }

            for (var index = activeMonsters.Count - 1; index >= 0; index--)
            {
                if (activeMonsters[index] != null)
                {
                    Return(activeMonsters[index]);
                }
            }

            activeMonsters.Clear();
            monstersByRuntimeId.Clear();
            pendingDisplacements.Clear();
            pendingRecycles.Clear();
            reportedDeaths.Clear();
            aliveEnemyCount = 0;
            running = false;
            levelRunId = 0;
            ikunBasketballConfigId = 0;
        }

        private void ResolveSingleTargetAttack(MonsterBase monster, int slotIndex)
        {
            if (!army.TryGetSlotTarget(slotIndex, out var target) || !target.IsActive)
            {
                return;
            }

            army.ApplySlotDamage(slotIndex, monster.AttackPower);
            eventBus.Publish(
                new MonsterAttackLanded(
                    levelRunId,
                    monster.RuntimeInstanceId,
                    monster.AttackType,
                    slotIndex,
                    monster.AttackPower));
        }

        private void ResolveAreaAttack(MonsterBase monster)
        {
            var attackCollider = monster.AttackCollider;
            if (attackCollider == null)
            {
                return;
            }

            var filter = new ContactFilter2D();
            filter.SetLayerMask(armySlotLayers);
            filter.useTriggers = true;
            var hitCount = attackCollider.OverlapCollider(filter, overlapResults);
            hitSlotIndices.Clear();
            for (var index = 0; index < hitCount; index++)
            {
                var collider = overlapResults[index];
                if (collider == null ||
                    !collider.TryGetComponent<ArmySlotHitProxy>(out var proxy) ||
                    !proxy.IsInitialized)
                {
                    continue;
                }

                hitSlotIndices.Add(proxy.SlotIndex);
            }

            foreach (var slotIndex in hitSlotIndices)
            {
                if (!army.TryGetSlotTarget(slotIndex, out var target) || !target.IsActive)
                {
                    continue;
                }

                army.ApplySlotDamage(slotIndex, monster.AttackPower);
                eventBus.Publish(
                    new MonsterAttackLanded(
                        levelRunId,
                        monster.RuntimeInstanceId,
                        monster.AttackType,
                        slotIndex,
                        monster.AttackPower));
            }
        }

        private void OnMonsterDamaged(
            MonsterBase monster,
            EnemyDamageContext damage,
            int remainingHp,
            bool fatal)
        {
            if (!IsOwnedCurrentMonster(monster))
            {
                return;
            }

            eventBus.Publish(
                new MonsterDamaged(levelRunId, monster.RuntimeInstanceId, damage, remainingHp, fatal));
        }

        private void OnMonsterDied(MonsterBase monster, EnemyDamageContext damage)
        {
            if (!IsOwnedCurrentMonster(monster) || !reportedDeaths.Add(monster.RuntimeInstanceId))
            {
                return;
            }

            aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
            eventBus.Publish(new MonsterKilled(levelRunId, monster.RuntimeInstanceId, damage));
        }

        private void OnMonsterRecycleRequested(MonsterBase monster)
        {
            if (IsOwnedCurrentMonster(monster))
            {
                pendingRecycles.Add(monster);
            }
        }

        private void OnBasketballSpawnRequested(IkunMonster ikun, Vector2 worldPosition)
        {
            if (!IsOwnedCurrentMonster(ikun) || !ikun.IsAlive || !IsFinite(worldPosition))
            {
                return;
            }

            basketballSpawner.SpawnBasketball(
                new BasketballSpawnRequest(
                    levelRunId,
                    ikun.RuntimeInstanceId,
                    ikunBasketballConfigId,
                    worldPosition));
        }

        private bool IsOwnedCurrentMonster(MonsterBase monster)
        {
            return monster != null && running && monster.LevelRunId == levelRunId &&
                   activeMonsters.Contains(monster);
        }

        bool IEnemyEffectService.TryGetAliveEffectTarget(
            int queriedLevelRunId,
            int runtimeInstanceId,
            out EnemyEffectTargetSnapshot target)
        {
            if (IsCurrentRun(queriedLevelRunId) &&
                monstersByRuntimeId.TryGetValue(runtimeInstanceId, out var monster) &&
                monster != null && monster.IsAlive)
            {
                target = new EnemyEffectTargetSnapshot(runtimeInstanceId, monster.EffectCenter);
                return true;
            }

            target = default(EnemyEffectTargetSnapshot);
            return false;
        }

        void IEnemyEffectService.CollectAliveEffectTargets(
            int queriedLevelRunId,
            Vector2 center,
            float radius,
            List<EnemyEffectTargetSnapshot> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (!IsCurrentRun(queriedLevelRunId) || !IsFinite(center) ||
                !IsFinite(radius) || radius < 0f)
            {
                return;
            }

            var radiusSquared = radius * radius;
            foreach (var pair in monstersByRuntimeId)
            {
                var monster = pair.Value;
                if (monster == null || !monster.IsAlive)
                {
                    continue;
                }

                var targetCenter = monster.EffectCenter;
                if ((targetCenter - center).sqrMagnitude <= radiusSquared)
                {
                    results.Add(new EnemyEffectTargetSnapshot(pair.Key, targetCenter));
                }
            }

            results.Sort(CompareEffectTargets);
        }

        bool IEnemyEffectService.ApplyEnemyDamage(
            int queriedLevelRunId,
            int runtimeInstanceId,
            EnemyDamageContext damage)
        {
            return IsCurrentRun(queriedLevelRunId) &&
                   monstersByRuntimeId.TryGetValue(runtimeInstanceId, out var monster) &&
                   monster != null && monster.ApplyEnemyDamage(damage);
        }

        void IEnemyEffectService.QueueEnemyDisplacement(
            int queriedLevelRunId,
            int runtimeInstanceId,
            Vector2 displacement)
        {
            if (!IsCurrentRun(queriedLevelRunId) || !IsFinite(displacement) ||
                !monstersByRuntimeId.TryGetValue(runtimeInstanceId, out var monster) ||
                monster == null || !monster.IsRuntimeActive)
            {
                return;
            }

            pendingDisplacements.TryGetValue(runtimeInstanceId, out var accumulated);
            var combined = accumulated + displacement;
            if (!IsFinite(combined))
            {
                throw new InvalidOperationException("Accumulated enemy displacement is not finite.");
            }

            pendingDisplacements[runtimeInstanceId] = combined;
        }

        bool IEnemyEffectService.TryGetRecentElementMask(
            int queriedLevelRunId,
            int runtimeInstanceId,
            out ElementMask elements)
        {
            if (IsCurrentRun(queriedLevelRunId) &&
                monstersByRuntimeId.TryGetValue(runtimeInstanceId, out var monster) &&
                monster != null && monster.IsAlive)
            {
                elements = monster.GetRecentElementMask();
                return true;
            }

            elements = ElementMask.None;
            return false;
        }

        private MonsterBase Rent(EnemyType enemyType)
        {
            switch (enemyType)
            {
                case EnemyType.Chick:
                    return chickPool.RentInactive();
                case EnemyType.Hen:
                    return henPool.RentInactive();
                case EnemyType.Rooster:
                    return roosterPool.RentInactive();
                case EnemyType.Ikun:
                    return ikunPool.RentInactive();
                default:
                    throw new ArgumentOutOfRangeException(nameof(enemyType));
            }
        }

        private void Return(MonsterBase monster)
        {
            monster.PrepareForPool();
            if (monster is ChickMonster chick)
            {
                chickPool.Return(chick);
            }
            else if (monster is HenMonster hen)
            {
                henPool.Return(hen);
            }
            else if (monster is RoosterMonster rooster)
            {
                roosterPool.Return(rooster);
            }
            else if (monster is IkunMonster ikun)
            {
                ikunPool.Return(ikun);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported monster pool type '{monster.GetType().FullName}'.");
            }
        }

        private bool IsCurrentRun(int queriedLevelRunId)
        {
            return running && queriedLevelRunId > 0 && queriedLevelRunId == levelRunId;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("EnemyManager has not been initialized.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static int CompareEffectTargets(
            EnemyEffectTargetSnapshot left,
            EnemyEffectTargetSnapshot right)
        {
            return left.RuntimeInstanceId.CompareTo(right.RuntimeInstanceId);
        }
    }
}
