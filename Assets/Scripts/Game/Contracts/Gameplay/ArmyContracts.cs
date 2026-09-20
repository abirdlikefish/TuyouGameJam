using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Contracts
{
    public interface IHorizontalInputReceiver
    {
        void SetHorizontalInput(float value);
    }

    public interface IArmyController : IHorizontalInputReceiver
    {
        int GetArmyCount();
        int GetActiveSlotCount();
        int GetSlotCapacity();
        int GetCurrentWeaponId();
        ArmyAdditionResult AddArmy(int amount);
        ArmyRemovalResult RemoveArmy(int amount);
        void ApplySlotDamage(int slotIndex, int damage);
        bool TryGetNearestActiveSlot(Vector2 origin, out ArmySlotTarget target);
        bool TryGetSlotTarget(int slotIndex, out ArmySlotTarget target);
        void ApplyWeaponPickup(int weaponId, int sourceRuntimeInstanceId);
        ElementDurationChangeResult AddElementDuration(
            ElementType elementType,
            float duration,
            int sourceRuntimeInstanceId);
        ArmyFormationSnapshot GetFormationSnapshot();
        ArmyElementStateSnapshot GetElementStateSnapshot();
    }

    public interface IArmyRunController : IArmyController
    {
        void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
        void TickMovementAndFire(int levelRunId, float gameplayDeltaTime);
        void EnterVictoryPresentation(int levelRunId);
        void StopRun(int levelRunId);
    }

    public readonly struct ArmyAdditionResult
    {
        public ArmyAdditionResult(int requestedAddition, int actualAddition, int remainingArmyCount)
        {
            RequestedAddition = requestedAddition;
            ActualAddition = actualAddition;
            RemainingArmyCount = remainingArmyCount;
        }

        public int RequestedAddition { get; }
        public int ActualAddition { get; }
        public int RemainingArmyCount { get; }
    }

    public readonly struct ArmyRemovalResult
    {
        public ArmyRemovalResult(
            int requestedRemoval,
            long requestedDamage,
            long appliedDamage,
            int actualArmyCountLoss,
            int remainingArmyCount)
        {
            RequestedRemoval = requestedRemoval;
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            ActualArmyCountLoss = actualArmyCountLoss;
            RemainingArmyCount = remainingArmyCount;
        }

        public int RequestedRemoval { get; }
        public long RequestedDamage { get; }
        public long AppliedDamage { get; }
        public int ActualArmyCountLoss { get; }
        public int RemainingArmyCount { get; }
    }

    public readonly struct ElementDurationChangeResult
    {
        public ElementDurationChangeResult(
            ElementType elementType,
            float previousDuration,
            float addedDuration,
            float currentDuration)
        {
            ElementType = elementType;
            PreviousDuration = previousDuration;
            AddedDuration = addedDuration;
            CurrentDuration = currentDuration;
        }

        public ElementType ElementType { get; }
        public float PreviousDuration { get; }
        public float AddedDuration { get; }
        public float CurrentDuration { get; }
    }

    public readonly struct ArmyElementStateSnapshot
    {
        public ArmyElementStateSnapshot(
            float fireRemainingDuration,
            float iceRemainingDuration,
            float lightningRemainingDuration,
            ElementMask activeElements)
        {
            FireRemainingDuration = fireRemainingDuration;
            IceRemainingDuration = iceRemainingDuration;
            LightningRemainingDuration = lightningRemainingDuration;
            ActiveElements = activeElements;
        }

        public float FireRemainingDuration { get; }
        public float IceRemainingDuration { get; }
        public float LightningRemainingDuration { get; }
        public ElementMask ActiveElements { get; }
    }

    public readonly struct ArmyFormationSnapshot
    {
        public ArmyFormationSnapshot(
            int armyCount,
            int activeSlotCount,
            IReadOnlyList<ArmySlotSnapshot> slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            ArmyCount = armyCount;
            ActiveSlotCount = activeSlotCount;

            var copy = new ArmySlotSnapshot[slots.Count];
            for (var index = 0; index < slots.Count; index++)
            {
                copy[index] = slots[index];
            }

            Slots = Array.AsReadOnly(copy);
        }

        public int ArmyCount { get; }
        public int ActiveSlotCount { get; }
        public IReadOnlyList<ArmySlotSnapshot> Slots { get; }
    }

    public readonly struct ArmySlotSnapshot
    {
        public ArmySlotSnapshot(
            int slotIndex,
            int representedCount,
            int currentHp,
            int maxHp,
            bool isActive)
        {
            SlotIndex = slotIndex;
            RepresentedCount = representedCount;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            IsActive = isActive;
        }

        public int SlotIndex { get; }
        public int RepresentedCount { get; }
        public int CurrentHp { get; }
        public int MaxHp { get; }
        public bool IsActive { get; }
    }

    public readonly struct ArmySlotTarget
    {
        public ArmySlotTarget(
            int slotIndex,
            Vector2 worldPosition,
            int representedCount,
            bool isActive)
        {
            SlotIndex = slotIndex;
            WorldPosition = worldPosition;
            RepresentedCount = representedCount;
            IsActive = isActive;
        }

        public int SlotIndex { get; }
        public Vector2 WorldPosition { get; }
        public int RepresentedCount { get; }
        public bool IsActive { get; }
    }
}
