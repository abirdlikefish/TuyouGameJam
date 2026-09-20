using UnityEngine;

namespace Game.Contracts
{
    public readonly struct GateSpawned
    {
        public GateSpawned(
            int levelRunId,
            int runtimeInstanceId,
            int spawnEntryIndex,
            GateType gateType,
            int initialValue,
            ElementType elementType,
            int maxHp,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            SpawnEntryIndex = spawnEntryIndex;
            GateType = gateType;
            InitialValue = initialValue;
            ElementType = elementType;
            MaxHp = maxHp;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public int SpawnEntryIndex { get; }
        public GateType GateType { get; }
        public int InitialValue { get; }
        public ElementType ElementType { get; }
        public int MaxHp { get; }
        public Vector2 WorldPosition { get; }
    }

    public readonly struct GateValueChanged
    {
        public GateValueChanged(
            int levelRunId,
            int runtimeInstanceId,
            BulletDamageContext damageContext,
            int previousValue,
            int currentValue,
            int changeAmount)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            DamageContext = damageContext;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            ChangeAmount = changeAmount;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public BulletDamageContext DamageContext { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        public int ChangeAmount { get; }
    }

    public readonly struct ElementGateDamageChanged
    {
        public ElementGateDamageChanged(
            int levelRunId,
            int runtimeInstanceId,
            BulletDamageContext damageContext,
            int previousHp,
            int currentHp,
            int hpDamage,
            int extraDamage,
            long postDepletionDamage,
            bool rewardLocked)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            DamageContext = damageContext;
            PreviousHp = previousHp;
            CurrentHp = currentHp;
            HpDamage = hpDamage;
            ExtraDamage = extraDamage;
            PostDepletionDamage = postDepletionDamage;
            RewardLocked = rewardLocked;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public BulletDamageContext DamageContext { get; }
        public int PreviousHp { get; }
        public int CurrentHp { get; }
        public int HpDamage { get; }
        public int ExtraDamage { get; }
        public long PostDepletionDamage { get; }
        public bool RewardLocked { get; }
    }

    public sealed class GateContactResolved
    {
        public GateContactResolved(
            int levelRunId,
            int runtimeInstanceId,
            int armyId,
            GateType gateType,
            bool succeeded,
            ArmyAdditionResult? additionResult,
            ArmyRemovalResult? removalResult,
            ElementType elementType,
            long postDepletionDamage,
            float elementDurationSecondsPerDamage,
            float calculatedElementDuration,
            ElementDurationChangeResult? elementDurationChangeResult,
            bool rewardLocked,
            bool continuesMoving)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            ArmyId = armyId;
            GateType = gateType;
            Succeeded = succeeded;
            AdditionResult = additionResult;
            RemovalResult = removalResult;
            ElementType = elementType;
            PostDepletionDamage = postDepletionDamage;
            ElementDurationSecondsPerDamage = elementDurationSecondsPerDamage;
            CalculatedElementDuration = calculatedElementDuration;
            ElementDurationChangeResult = elementDurationChangeResult;
            RewardLocked = rewardLocked;
            ContinuesMoving = continuesMoving;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public int ArmyId { get; }
        public GateType GateType { get; }
        public bool Succeeded { get; }
        public ArmyAdditionResult? AdditionResult { get; }
        public ArmyRemovalResult? RemovalResult { get; }
        public ElementType ElementType { get; }
        public long PostDepletionDamage { get; }
        public float ElementDurationSecondsPerDamage { get; }
        public float CalculatedElementDuration { get; }
        public ElementDurationChangeResult? ElementDurationChangeResult { get; }
        public bool RewardLocked { get; }
        public bool ContinuesMoving { get; }
    }

    public readonly struct GateExitedRoad
    {
        public GateExitedRoad(int levelRunId, int runtimeInstanceId, bool contactedArmy)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            ContactedArmy = contactedArmy;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public bool ContactedArmy { get; }
    }

    public readonly struct PropSpawned
    {
        public PropSpawned(
            int levelRunId,
            int runtimeInstanceId,
            int configId,
            int weaponId,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            ConfigId = configId;
            WeaponId = weaponId;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public int ConfigId { get; }
        public int WeaponId { get; }
        public Vector2 WorldPosition { get; }
    }

    public readonly struct PropBroken
    {
        public PropBroken(
            int levelRunId,
            int runtimeInstanceId,
            int weaponId,
            BulletDamageContext damageContext,
            PropContactState contactState,
            bool effectApplied)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            WeaponId = weaponId;
            DamageContext = damageContext;
            ContactState = contactState;
            EffectApplied = effectApplied;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public int WeaponId { get; }
        public BulletDamageContext DamageContext { get; }
        public PropContactState ContactState { get; }
        public bool EffectApplied { get; }
    }

    public readonly struct PropContactDamage
    {
        public PropContactDamage(
            int levelRunId,
            int runtimeInstanceId,
            int armyId,
            int slotIndex,
            int contactDamage)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            ArmyId = armyId;
            SlotIndex = slotIndex;
            ContactDamage = contactDamage;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public int ArmyId { get; }
        public int SlotIndex { get; }
        public int ContactDamage { get; }
    }

    public readonly struct PropExitedRoad
    {
        public PropExitedRoad(int levelRunId, int runtimeInstanceId, bool contactedArmy)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            ContactedArmy = contactedArmy;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public bool ContactedArmy { get; }
    }

    public readonly struct ObstacleRecycled
    {
        public ObstacleRecycled(
            int levelRunId,
            int runtimeInstanceId,
            ObstacleKind kind,
            ObstacleRecycleReason reason)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            Kind = kind;
            Reason = reason;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public ObstacleKind Kind { get; }
        public ObstacleRecycleReason Reason { get; }
    }
}
