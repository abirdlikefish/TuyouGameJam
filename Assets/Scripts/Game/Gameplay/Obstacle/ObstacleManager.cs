using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ObstacleManager : MonoBehaviour, IObstacleManager
    {
        [SerializeField] private AdditiveGate additiveGatePrefab;
        [SerializeField] private ElementGate elementGatePrefab;
        [SerializeField] private WeaponProp weaponPropPrefab;
        [SerializeField] private BasketballProp basketballPropPrefab;
        [SerializeField] private GooseCageProp gooseCagePropPrefab;
        [SerializeField] private LayerMask armySlotLayers;

        private readonly List<IRoadObstacleRuntime> activeObjects =
            new List<IRoadObstacleRuntime>();
        private readonly Dictionary<IRoadObstacleRuntime, ObstacleRecycleReason> pendingRecycles =
            new Dictionary<IRoadObstacleRuntime, ObstacleRecycleReason>();
        private readonly Collider2D[] overlapResults = new Collider2D[64];
        private readonly SortedSet<int> contactedSlots = new SortedSet<int>();

        private IComponentPool<AdditiveGate> additiveGatePool;
        private IComponentPool<ElementGate> elementGatePool;
        private IComponentPool<WeaponProp> weaponPropPool;
        private IComponentPool<BasketballProp> basketballPropPool;
        private IComponentPool<GooseCageProp> gooseCagePropPool;
        private IPropConfigProvider propConfigProvider;
        private IArmyController army;
        private IEventBus eventBus;
        private int levelRunId;
        private int nextRuntimeInstanceId;
        private float despawnY;
        private bool initialized;
        private bool running;

        public void Initialize(
            IPoolService poolService,
            IPropConfigProvider initializedPropConfigProvider,
            IArmyController armyController,
            IEventBus initializedEventBus)
        {
            if (poolService == null)
            {
                throw new ArgumentNullException(nameof(poolService));
            }

            propConfigProvider = initializedPropConfigProvider ??
                                 throw new ArgumentNullException(nameof(initializedPropConfigProvider));
            army = armyController ?? throw new ArgumentNullException(nameof(armyController));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            if (initialized)
            {
                throw new InvalidOperationException("ObstacleManager is already initialized.");
            }

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            additiveGatePool = poolService.GetOrCreatePool(additiveGatePrefab);
            elementGatePool = poolService.GetOrCreatePool(elementGatePrefab);
            weaponPropPool = poolService.GetOrCreatePool(weaponPropPrefab);
            basketballPropPool = poolService.GetOrCreatePool(basketballPropPrefab);
            gooseCagePropPool = poolService.GetOrCreatePool(gooseCagePropPrefab);
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (additiveGatePrefab == null || elementGatePrefab == null || weaponPropPrefab == null ||
                basketballPropPrefab == null || gooseCagePropPrefab == null)
            {
                error = $"{name} requires AdditiveGate, ElementGate, WeaponProp, BasketballProp and GooseCageProp prefab bindings.";
                return false;
            }

            if (armySlotLayers.value == 0)
            {
                error = $"{name}.armySlotLayers is empty.";
                return false;
            }

            return additiveGatePrefab.TryValidate(out error) &&
                   elementGatePrefab.TryValidate(out error) &&
                   weaponPropPrefab.TryValidate(out error) &&
                   basketballPropPrefab.TryValidate(out error) &&
                   gooseCagePropPrefab.TryValidate(out error);
        }

        public void StartRun(int startedLevelRunId, RoadLayoutSnapshot roadLayout)
        {
            EnsureInitialized();
            if (running)
            {
                throw new InvalidOperationException("ObstacleManager already has an active run.");
            }

            if (startedLevelRunId <= 0 || !IsFinite(roadLayout.DespawnY))
            {
                throw new ArgumentException("Obstacle run configuration is invalid.");
            }

            levelRunId = startedLevelRunId;
            despawnY = roadLayout.DespawnY;
            nextRuntimeInstanceId = 0;
            activeObjects.Clear();
            pendingRecycles.Clear();
            running = true;
        }

        public void Spawn(GateSpawnRequest request)
        {
            if (!IsCurrentRun(request.LevelRunId))
            {
                return;
            }

            ValidateGateRequest(request);
            var runtimeId = nextRuntimeInstanceId++;
            IRoadObstacleRuntime gate;
            if (request.GateType == GateType.Additive)
            {
                var additive = additiveGatePool.RentInactive();
                additive.InitializeRuntime(
                    request,
                    runtimeId,
                    army,
                    eventBus,
                    OnRecycleRequested,
                    transform);
                gate = additive;
                activeObjects.Add(gate);
                additive.gameObject.SetActive(true);
            }
            else
            {
                var element = elementGatePool.RentInactive();
                element.InitializeRuntime(
                    request,
                    runtimeId,
                    army,
                    eventBus,
                    OnRecycleRequested,
                    transform);
                gate = element;
                activeObjects.Add(gate);
                element.gameObject.SetActive(true);
            }

            eventBus.Publish(
                new GateSpawned(
                    levelRunId,
                    runtimeId,
                    request.SpawnEntryIndex,
                    request.GateType,
                    request.InitialValue,
                    request.ElementType,
                    request.MaxHp,
                    request.WorldPosition));
        }

        public void Spawn(PropSpawnRequest request)
        {
            if (!IsCurrentRun(request.LevelRunId))
            {
                return;
            }

            if (request.SpawnEntryIndex < 0 || request.ConfigId < 0 ||
                !IsFinite(request.SpawnPosition) || !IsFinite(request.WorldPosition))
            {
                throw new ArgumentException("Prop spawn request contains invalid values.", nameof(request));
            }

            var config = propConfigProvider.GetPropConfig(request.ConfigId);
            var runtimeId = nextRuntimeInstanceId++;
            IRoadObstacleRuntime prop;
            GameObject propObject;
            switch (config.PropType)
            {
                case PropType.WeaponBox:
                    var weapon = weaponPropPool.RentInactive();
                    weapon.InitializeRuntime(
                        request,
                        config,
                        runtimeId,
                        army,
                        eventBus,
                        OnRecycleRequested,
                        transform);
                    prop = weapon;
                    propObject = weapon.gameObject;
                    break;
                case PropType.Basketball:
                    var basketball = basketballPropPool.RentInactive();
                    basketball.InitializeRuntime(
                        request,
                        config,
                        runtimeId,
                        army,
                        eventBus,
                        OnRecycleRequested,
                        transform);
                    prop = basketball;
                    propObject = basketball.gameObject;
                    break;
                case PropType.GooseCage:
                    var gooseCage = gooseCagePropPool.RentInactive();
                    gooseCage.InitializeRuntime(
                        request,
                        config,
                        runtimeId,
                        army,
                        eventBus,
                        OnRecycleRequested,
                        transform);
                    prop = gooseCage;
                    propObject = gooseCage.gameObject;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.PropType));
            }

            activeObjects.Add(prop);
            propObject.SetActive(true);
            eventBus.Publish(
                new PropSpawned(
                    levelRunId,
                    runtimeId,
                    request.SpawnEntryIndex,
                    config.Id,
                    config.PropType,
                    request.WorldPosition));
        }

        public void SpawnBasketball(BasketballSpawnRequest request)
        {
            if (!IsCurrentRun(request.LevelRunId))
            {
                return;
            }

            if (request.SourceEnemyRuntimeInstanceId < 0 || request.ConfigId < 0 ||
                !IsFinite(request.WorldPosition))
            {
                throw new ArgumentException(
                    "Basketball spawn request contains invalid values.",
                    nameof(request));
            }

            var config = propConfigProvider.GetPropConfig(request.ConfigId);
            if (config.PropType != PropType.Basketball)
            {
                throw new ArgumentException(
                    "Basketball spawn request must reference PropType.Basketball.",
                    nameof(request));
            }

            var runtimeId = nextRuntimeInstanceId++;
            var basketball = basketballPropPool.RentInactive();
            basketball.InitializeRuntime(
                request,
                config,
                runtimeId,
                army,
                eventBus,
                OnRecycleRequested,
                transform);
            activeObjects.Add(basketball);
            basketball.gameObject.SetActive(true);
            eventBus.Publish(
                new BasketballSpawned(
                    levelRunId,
                    runtimeId,
                    request.SourceEnemyRuntimeInstanceId,
                    request.ConfigId,
                    request.WorldPosition));
        }

        public void TickMovement(int tickingLevelRunId, float gateDeltaTime)
        {
            if (!IsCurrentRun(tickingLevelRunId))
            {
                return;
            }

            if (!IsFinite(gateDeltaTime) || gateDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gateDeltaTime));
            }

            for (var index = 0; index < activeObjects.Count; index++)
            {
                var roadObject = activeObjects[index];
                if (roadObject == null || !roadObject.IsRuntimeActive ||
                    pendingRecycles.ContainsKey(roadObject))
                {
                    continue;
                }

                roadObject.TickMovement(gateDeltaTime);
                if (roadObject.WorldPosition.y <= despawnY)
                {
                    roadObject.ExitRoad();
                }
            }
        }

        public void ResolveContacts(int resolvingLevelRunId)
        {
            if (!IsCurrentRun(resolvingLevelRunId))
            {
                return;
            }

            var filter = new ContactFilter2D();
            filter.SetLayerMask(armySlotLayers);
            filter.useTriggers = true;
            for (var index = 0; index < activeObjects.Count; index++)
            {
                var roadObject = activeObjects[index];
                if (roadObject == null || !roadObject.CanResolveContact ||
                    pendingRecycles.ContainsKey(roadObject))
                {
                    continue;
                }

                var hitCount = roadObject.BodyCollider.OverlapCollider(filter, overlapResults);
                contactedSlots.Clear();
                var armyId = -1;
                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    var collider = overlapResults[hitIndex];
                    if (collider == null ||
                        !collider.TryGetComponent<ArmySlotHitProxy>(out var proxy) ||
                        !proxy.IsInitialized)
                    {
                        continue;
                    }

                    if (armyId < 0)
                    {
                        armyId = proxy.ArmyId;
                    }

                    if (proxy.ArmyId == armyId)
                    {
                        contactedSlots.Add(proxy.SlotIndex);
                    }
                }

                if (armyId >= 0 && contactedSlots.Count > 0)
                {
                    roadObject.ResolveContact(armyId, new List<int>(contactedSlots));
                }
            }
        }

        public void FlushPendingRecycles(int flushingLevelRunId)
        {
            if (!IsCurrentRun(flushingLevelRunId))
            {
                return;
            }

            for (var index = activeObjects.Count - 1; index >= 0; index--)
            {
                var roadObject = activeObjects[index];
                if (roadObject == null || !pendingRecycles.TryGetValue(roadObject, out var reason))
                {
                    continue;
                }

                activeObjects.RemoveAt(index);
                pendingRecycles.Remove(roadObject);
                Recycle(roadObject, reason);
            }
        }

        public IReadOnlyList<RoadObjectSnapshot> GetActiveObjects()
        {
            return CreateSnapshots(null);
        }

        public IReadOnlyList<RoadObjectSnapshot> GetActiveObjects(ObstacleKind kind)
        {
            return CreateSnapshots(kind);
        }

        public bool TryGetObject(int runtimeInstanceId, out RoadObjectSnapshot snapshot)
        {
            for (var index = 0; index < activeObjects.Count; index++)
            {
                var roadObject = activeObjects[index];
                if (roadObject != null && roadObject.RuntimeInstanceId == runtimeInstanceId)
                {
                    snapshot = CreateSnapshot(roadObject);
                    return true;
                }
            }

            snapshot = default(RoadObjectSnapshot);
            return false;
        }

        public void StopRun(int stoppedLevelRunId)
        {
            if (!IsCurrentRun(stoppedLevelRunId))
            {
                return;
            }

            for (var index = activeObjects.Count - 1; index >= 0; index--)
            {
                if (activeObjects[index] != null)
                {
                    Recycle(activeObjects[index], ObstacleRecycleReason.StopRun);
                }
            }

            activeObjects.Clear();
            pendingRecycles.Clear();
            running = false;
            levelRunId = 0;
        }

        private IReadOnlyList<RoadObjectSnapshot> CreateSnapshots(ObstacleKind? kind)
        {
            var snapshots = new List<RoadObjectSnapshot>(activeObjects.Count);
            for (var index = 0; index < activeObjects.Count; index++)
            {
                var roadObject = activeObjects[index];
                if (roadObject != null && (!kind.HasValue || roadObject.Kind == kind.Value))
                {
                    snapshots.Add(CreateSnapshot(roadObject));
                }
            }

            return Array.AsReadOnly(snapshots.ToArray());
        }

        private static RoadObjectSnapshot CreateSnapshot(IRoadObstacleRuntime roadObject)
        {
            var state = roadObject.State;
            var isOnRoad = state != ObstacleState.ExitedUncontacted && state != ObstacleState.Recycled;
            return new RoadObjectSnapshot(
                roadObject.RuntimeInstanceId,
                roadObject.SpawnEntryIndex,
                roadObject.ConfigId,
                roadObject.Kind,
                roadObject.WorldPosition,
                isOnRoad,
                state);
        }

        private void OnRecycleRequested(
            IRoadObstacleRuntime roadObject,
            ObstacleRecycleReason reason)
        {
            if (roadObject == null || !running || roadObject.LevelRunId != levelRunId ||
                pendingRecycles.ContainsKey(roadObject))
            {
                return;
            }

            pendingRecycles.Add(roadObject, reason);
        }

        private void Recycle(IRoadObstacleRuntime roadObject, ObstacleRecycleReason reason)
        {
            var runtimeId = roadObject.RuntimeInstanceId;
            var kind = roadObject.Kind;
            roadObject.PrepareForPool();
            if (roadObject is AdditiveGate additive)
            {
                additiveGatePool.Return(additive);
            }
            else if (roadObject is ElementGate element)
            {
                elementGatePool.Return(element);
            }
            else if (roadObject is WeaponProp prop)
            {
                weaponPropPool.Return(prop);
            }
            else if (roadObject is BasketballProp basketball)
            {
                basketballPropPool.Return(basketball);
            }
            else if (roadObject is GooseCageProp gooseCage)
            {
                gooseCagePropPool.Return(gooseCage);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported obstacle pool type '{roadObject.GetType().FullName}'.");
            }

            eventBus.Publish(new ObstacleRecycled(levelRunId, runtimeId, kind, reason));
        }

        private static void ValidateGateRequest(GateSpawnRequest request)
        {
            if (request.SpawnEntryIndex < 0 || !IsFinite(request.SpawnPosition) ||
                !IsFinite(request.WorldPosition) || !Enum.IsDefined(typeof(GateType), request.GateType))
            {
                throw new ArgumentException("Gate spawn request contains invalid values.", nameof(request));
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
                throw new InvalidOperationException("ObstacleManager has not been initialized.");
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
    }
}
