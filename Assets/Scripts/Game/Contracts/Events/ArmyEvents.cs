namespace Game.Contracts
{
    public readonly struct ArmyCountChanged
    {
        public ArmyCountChanged(
            int levelRunId,
            int newArmyCount,
            int changeAmount,
            ArmyCountChangeReason reason)
        {
            LevelRunId = levelRunId;
            NewArmyCount = newArmyCount;
            ChangeAmount = changeAmount;
            Reason = reason;
        }

        public int LevelRunId { get; }
        public int NewArmyCount { get; }
        public int ChangeAmount { get; }
        public ArmyCountChangeReason Reason { get; }
    }

    public sealed class ArmyFormationChanged
    {
        public ArmyFormationChanged(int levelRunId, ArmyFormationSnapshot formation)
        {
            LevelRunId = levelRunId;
            Formation = formation;
        }

        public int LevelRunId { get; }
        public ArmyFormationSnapshot Formation { get; }
    }

    public readonly struct SoldierHit
    {
        public SoldierHit(
            int levelRunId,
            int armyId,
            int slotIndex,
            int requestedDamage,
            int actualHpDamage,
            int actualArmyCountLoss)
        {
            LevelRunId = levelRunId;
            ArmyId = armyId;
            SlotIndex = slotIndex;
            RequestedDamage = requestedDamage;
            ActualHpDamage = actualHpDamage;
            ActualArmyCountLoss = actualArmyCountLoss;
        }

        public int LevelRunId { get; }
        public int ArmyId { get; }
        public int SlotIndex { get; }
        public int RequestedDamage { get; }
        public int ActualHpDamage { get; }
        public int ActualArmyCountLoss { get; }
    }

    public readonly struct ArmyReachedZero
    {
        public ArmyReachedZero(int levelRunId, ArmyReachedZeroReason reason)
        {
            LevelRunId = levelRunId;
            Reason = reason;
        }

        public int LevelRunId { get; }
        public ArmyReachedZeroReason Reason { get; }
    }

    public readonly struct ArmyWeaponChanged
    {
        public ArmyWeaponChanged(
            int levelRunId,
            int armyId,
            int previousWeaponId,
            int currentWeaponId,
            int sourceRuntimeInstanceId)
        {
            LevelRunId = levelRunId;
            ArmyId = armyId;
            PreviousWeaponId = previousWeaponId;
            CurrentWeaponId = currentWeaponId;
            SourceRuntimeInstanceId = sourceRuntimeInstanceId;
        }

        public int LevelRunId { get; }
        public int ArmyId { get; }
        public int PreviousWeaponId { get; }
        public int CurrentWeaponId { get; }
        public int SourceRuntimeInstanceId { get; }
    }

    public readonly struct ArmyElementDurationChanged
    {
        public ArmyElementDurationChanged(
            int levelRunId,
            int armyId,
            ElementType elementType,
            float previousDuration,
            float addedDuration,
            float currentDuration,
            int sourceRuntimeInstanceId)
        {
            LevelRunId = levelRunId;
            ArmyId = armyId;
            ElementType = elementType;
            PreviousDuration = previousDuration;
            AddedDuration = addedDuration;
            CurrentDuration = currentDuration;
            SourceRuntimeInstanceId = sourceRuntimeInstanceId;
        }

        public int LevelRunId { get; }
        public int ArmyId { get; }
        public ElementType ElementType { get; }
        public float PreviousDuration { get; }
        public float AddedDuration { get; }
        public float CurrentDuration { get; }
        public int SourceRuntimeInstanceId { get; }
    }

    public readonly struct ArmyElementExpired
    {
        public ArmyElementExpired(int levelRunId, int armyId, ElementType elementType)
        {
            LevelRunId = levelRunId;
            ArmyId = armyId;
            ElementType = elementType;
        }

        public int LevelRunId { get; }
        public int ArmyId { get; }
        public ElementType ElementType { get; }
    }
}
