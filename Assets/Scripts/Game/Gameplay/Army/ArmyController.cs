using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ArmyController : MonoBehaviour, IArmyRunController
    {
        private const int MvpInitialArmyCount = 1;
        private const int InitialWeaponId = 0;
        private const int MvpWeaponCount = 3;

        [Serializable]
        private struct WeaponAnimatorControllerBinding
        {
            [SerializeField, Min(0)] private int weaponId;
            [SerializeField] private AnimatorOverrideController controller;

            public int WeaponId => weaponId;
            public AnimatorOverrideController Controller => controller;
        }

        [SerializeField] private ArmySlotView[] slots = new ArmySlotView[0];
        [SerializeField]
        private WeaponAnimatorControllerBinding[] weaponAnimatorControllers =
            new WeaponAnimatorControllerBinding[0];

        private IWeaponConfigProvider weaponConfigProvider;
        private IBulletManager bulletManager;
        private IEventBus eventBus;
        private ArmyConfigSnapshot armyConfig;
        private RoadLayoutSnapshot roadLayout;
        private int armyId;
        private int levelRunId;
        private int armyCount;
        private int currentWeaponId;
        private float horizontalInput;
        private float fireRemainingDuration;
        private float iceRemainingDuration;
        private float lightningRemainingDuration;
        private ArmyAnimationState currentAnimationState = ArmyAnimationState.Idle;
        private bool initialized;
        private bool running;
        private bool victoryPresentation;

        public void Initialize(
            int initializedArmyId,
            ArmyConfigSnapshot config,
            IWeaponConfigProvider initializedWeaponConfigProvider,
            IBulletManager initializedBulletManager,
            IEventBus initializedEventBus)
        {
            if (initializedArmyId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initializedArmyId));
            }

            if (config.Id != initializedArmyId)
            {
                throw new ArgumentException("Army config ID must match the initialized Army ID.", nameof(config));
            }

            if (initializedWeaponConfigProvider == null)
            {
                throw new ArgumentNullException(nameof(initializedWeaponConfigProvider));
            }

            if (initializedBulletManager == null)
            {
                throw new ArgumentNullException(nameof(initializedBulletManager));
            }

            if (initializedEventBus == null)
            {
                throw new ArgumentNullException(nameof(initializedEventBus));
            }

            if (initialized)
            {
                if (armyId == initializedArmyId &&
                    armyConfig.Equals(config) &&
                    ReferenceEquals(weaponConfigProvider, initializedWeaponConfigProvider) &&
                    ReferenceEquals(bulletManager, initializedBulletManager) &&
                    ReferenceEquals(eventBus, initializedEventBus))
                {
                    return;
                }

                throw new InvalidOperationException("ArmyController cannot be reinitialized with different dependencies.");
            }

            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            armyId = initializedArmyId;
            armyConfig = config;
            weaponConfigProvider = initializedWeaponConfigProvider;
            bulletManager = initializedBulletManager;
            eventBus = initializedEventBus;
            initialized = true;
        }

        public bool TryValidate(out string error)
        {
            if (slots == null || slots.Length == 0)
            {
                error = $"{name}.slots must contain at least one slot.";
                return false;
            }

            var uniqueSlots = new HashSet<ArmySlotView>();
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (slot == null)
                {
                    error = $"{name}.slots[{index}] is not assigned.";
                    return false;
                }

                if (!uniqueSlots.Add(slot))
                {
                    error = $"{name}.slots[{index}] is a duplicate reference.";
                    return false;
                }

                if (!slot.TryValidate(out error))
                {
                    return false;
                }
            }

            if (!TryValidateWeaponAnimatorControllers(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void StartRun(int startedLevelRunId, RoadLayoutSnapshot layout)
        {
            EnsureInitialized();
            if (running)
            {
                throw new InvalidOperationException("ArmyController already has an active run.");
            }

            if (startedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startedLevelRunId));
            }

            ValidateRoadLayout(layout);
            levelRunId = startedLevelRunId;
            roadLayout = layout;
            transform.position = Vector3.zero;
            horizontalInput = 0f;
            currentWeaponId = InitialWeaponId;
            fireRemainingDuration = 0f;
            iceRemainingDuration = 0f;
            lightningRemainingDuration = 0f;
            currentAnimationState = ArmyAnimationState.Idle;
            victoryPresentation = false;
            running = true;

            var weapon = weaponConfigProvider.GetWeaponConfig(currentWeaponId);
            ApplyWeaponAnimatorController(currentWeaponId);
            InitializeSlots(MvpInitialArmyCount, weapon.FireInterval);
            ClampRootToRoad();
            PublishCountChanged(MvpInitialArmyCount, ArmyCountChangeReason.Addition);
            PublishFormationChanged();
        }

        public void TickMovementAndFire(int tickingLevelRunId, float gameplayDeltaTime)
        {
            if (!IsCurrentRun(tickingLevelRunId))
            {
                return;
            }

            if (victoryPresentation)
            {
                return;
            }

            if (!IsFinite(gameplayDeltaTime) || gameplayDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gameplayDeltaTime));
            }

            TickElementDuration(ref fireRemainingDuration, ElementType.Fire, gameplayDeltaTime);
            TickElementDuration(ref iceRemainingDuration, ElementType.Ice, gameplayDeltaTime);
            TickElementDuration(ref lightningRemainingDuration, ElementType.Lightning, gameplayDeltaTime);
            SetAnimationState(MoveArmy(gameplayDeltaTime));
            TickFire(gameplayDeltaTime);
        }

        public void EnterVictoryPresentation(int completedLevelRunId)
        {
            if (!IsCurrentRun(completedLevelRunId) || victoryPresentation)
            {
                return;
            }

            victoryPresentation = true;
            horizontalInput = 0f;
            SetAnimationState(ArmyAnimationState.Victory);
        }

        public void StopRun(int stoppedLevelRunId)
        {
            if (!IsCurrentRun(stoppedLevelRunId))
            {
                return;
            }

            running = false;
            levelRunId = 0;
            armyCount = 0;
            horizontalInput = 0f;
            fireRemainingDuration = 0f;
            iceRemainingDuration = 0f;
            lightningRemainingDuration = 0f;
            currentAnimationState = ArmyAnimationState.Idle;
            victoryPresentation = false;
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].PrepareForRunStop();
                slots[index].SlotHitProxy.ResetRuntimeIdentity();
            }
        }

        public void SetHorizontalInput(float value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            horizontalInput = running && !victoryPresentation ? value : 0f;
        }

        public int GetArmyCount()
        {
            return armyCount;
        }

        public int GetActiveSlotCount()
        {
            var count = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                if (slots[index].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetSlotCapacity()
        {
            return slots != null ? slots.Length : 0;
        }

        public int GetCurrentWeaponId()
        {
            return currentWeaponId;
        }

        public ArmyAdditionResult AddArmy(int amount)
        {
            EnsureRunning();
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount == 0)
            {
                return new ArmyAdditionResult(0, 0, armyCount);
            }

            var allowed = amount;
            if (armyConfig.ArmyCountLimit > 0)
            {
                allowed = Mathf.Min(allowed, Mathf.Max(0, armyConfig.ArmyCountLimit - armyCount));
            }

            allowed = Mathf.Min(allowed, int.MaxValue - armyCount);
            var weapon = weaponConfigProvider.GetWeaponConfig(currentWeaponId);
            DistributeAddition(allowed, weapon.FireInterval);

            armyCount = SaturatingAdd(armyCount, allowed);
            if (allowed > 0)
            {
                ClampRootToRoad();
                PublishCountChanged(allowed, ArmyCountChangeReason.Addition);
                PublishFormationChanged();
            }

            return new ArmyAdditionResult(amount, allowed, armyCount);
        }

        public ArmyRemovalResult RemoveArmy(int amount)
        {
            EnsureRunning();
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            var requestedDamage = SaturatingMultiplyLong(amount, armyConfig.HpPerSoldier);
            if (requestedDamage == 0 || armyCount == 0)
            {
                return new ArmyRemovalResult(amount, requestedDamage, 0, 0, armyCount);
            }

            var candidates = new List<ArmySlotView>(slots.Length);
            for (var index = 0; index < slots.Length; index++)
            {
                if (slots[index].IsActive && slots[index].CurrentHp > 0)
                {
                    candidates.Add(slots[index]);
                }
            }

            candidates.Sort(CompareSlotsForRemoval);
            var remainingDamage = requestedDamage;
            long appliedDamage = 0;
            var totalArmyLoss = 0;
            for (var index = 0; index < candidates.Count && remainingDamage > 0; index++)
            {
                var slot = candidates[index];
                var requestedForSlot = (int)Math.Min(remainingDamage, slot.CurrentHp);
                var result = ApplyDamageToSlot(slot, requestedForSlot);
                appliedDamage += result.ActualHpDamage;
                totalArmyLoss = SaturatingAdd(totalArmyLoss, result.ArmyCountLoss);
                remainingDamage -= result.ActualHpDamage;
            }

            armyCount = SumRepresentedCount();
            if (appliedDamage > 0)
            {
                PublishCountChanged(-totalArmyLoss, ArmyCountChangeReason.RemovalRequest);
                PublishFormationChanged();
                PublishZeroIfNeeded(totalArmyLoss, ArmyReachedZeroReason.RemovalRequest);
            }

            return new ArmyRemovalResult(
                amount,
                requestedDamage,
                appliedDamage,
                totalArmyLoss,
                armyCount);
        }

        public void ApplySlotDamage(int slotIndex, int damage)
        {
            EnsureRunning();
            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            if (!TryGetSlot(slotIndex, out var slot) || !slot.IsActive)
            {
                return;
            }

            var result = ApplyDamageToSlot(slot, damage);
            armyCount = SumRepresentedCount();
            if (result.ActualHpDamage <= 0)
            {
                return;
            }

            if (result.ArmyCountLoss > 0)
            {
                PublishCountChanged(-result.ArmyCountLoss, ArmyCountChangeReason.Damage);
            }

            PublishFormationChanged();
            PublishZeroIfNeeded(result.ArmyCountLoss, ArmyReachedZeroReason.Damage);
        }

        public bool TryGetNearestActiveSlot(Vector2 origin, out ArmySlotTarget target)
        {
            var found = false;
            var bestDistance = float.MaxValue;
            var bestIndex = int.MaxValue;
            target = default(ArmySlotTarget);

            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (!slot.IsActive)
                {
                    continue;
                }

                var distance = (slot.WorldPosition - origin).sqrMagnitude;
                if (!found || distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) && slot.SlotIndex < bestIndex))
                {
                    found = true;
                    bestDistance = distance;
                    bestIndex = slot.SlotIndex;
                    target = CreateTarget(slot);
                }
            }

            return found;
        }

        public bool TryGetSlotTarget(int slotIndex, out ArmySlotTarget target)
        {
            if (TryGetSlot(slotIndex, out var slot) && slot.IsActive)
            {
                target = CreateTarget(slot);
                return true;
            }

            target = default(ArmySlotTarget);
            return false;
        }

        public void ApplyWeaponPickup(int weaponId, int sourceRuntimeInstanceId)
        {
            EnsureRunning();
            if (weaponId < 0 || sourceRuntimeInstanceId < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            var nextWeapon = weaponConfigProvider.GetWeaponConfig(weaponId);
            if (weaponId == currentWeaponId)
            {
                return;
            }

            var previousWeaponId = currentWeaponId;
            currentWeaponId = weaponId;
            ApplyWeaponAnimatorController(currentWeaponId);
            for (var index = 0; index < slots.Length; index++)
            {
                if (slots[index].IsActive)
                {
                    slots[index].SetFireCooldown(nextWeapon.FireInterval);
                }
            }

            eventBus.Publish(
                new ArmyWeaponChanged(
                    levelRunId,
                    armyId,
                    previousWeaponId,
                    currentWeaponId,
                    sourceRuntimeInstanceId));
        }

        public ElementDurationChangeResult AddElementDuration(
            ElementType elementType,
            float duration,
            int sourceRuntimeInstanceId)
        {
            EnsureRunning();
            if (elementType == ElementType.None || !Enum.IsDefined(typeof(ElementType), elementType))
            {
                throw new ArgumentOutOfRangeException(nameof(elementType));
            }

            if (!IsFinite(duration) || duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            if (sourceRuntimeInstanceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceRuntimeInstanceId));
            }

            var previous = GetElementDuration(elementType);
            var current = SaturatingAdd(previous, duration);
            SetElementDuration(elementType, current);
            var result = new ElementDurationChangeResult(elementType, previous, duration, current);
            eventBus.Publish(
                new ArmyElementDurationChanged(
                    levelRunId,
                    armyId,
                    elementType,
                    previous,
                    duration,
                    current,
                    sourceRuntimeInstanceId));
            return result;
        }

        public ArmyFormationSnapshot GetFormationSnapshot()
        {
            var snapshots = new ArmySlotSnapshot[slots.Length];
            for (var index = 0; index < slots.Length; index++)
            {
                snapshots[index] = slots[index].CreateSnapshot();
            }

            return new ArmyFormationSnapshot(armyCount, GetActiveSlotCount(), snapshots);
        }

        public ArmyElementStateSnapshot GetElementStateSnapshot()
        {
            return new ArmyElementStateSnapshot(
                fireRemainingDuration,
                iceRemainingDuration,
                lightningRemainingDuration,
                GetActiveElements());
        }

        private bool TryValidateWeaponAnimatorControllers(out string error)
        {
            if (weaponAnimatorControllers == null ||
                weaponAnimatorControllers.Length != MvpWeaponCount)
            {
                error = $"{name}.weaponAnimatorControllers must contain exactly " +
                        $"{MvpWeaponCount} bindings for WeaponId 0, 1 and 2.";
                return false;
            }

            var foundIds = new bool[MvpWeaponCount];
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            for (var index = 0; index < weaponAnimatorControllers.Length; index++)
            {
                var binding = weaponAnimatorControllers[index];
                if (binding.WeaponId < 0 || binding.WeaponId >= MvpWeaponCount)
                {
                    error = $"{name}.weaponAnimatorControllers[{index}] has unsupported " +
                            $"WeaponId {binding.WeaponId}.";
                    return false;
                }

                if (foundIds[binding.WeaponId])
                {
                    error = $"{name}.weaponAnimatorControllers contains duplicate " +
                            $"WeaponId {binding.WeaponId}.";
                    return false;
                }

                var controller = binding.Controller;
                if (controller == null || controller.runtimeAnimatorController == null)
                {
                    error = $"{name}.weaponAnimatorControllers[{index}] requires a valid " +
                            "AnimatorOverrideController.";
                    return false;
                }

                overrides.Clear();
                controller.GetOverrides(overrides);
                if (overrides.Count != 5)
                {
                    error = $"{name}.weaponAnimatorControllers[{index}] must override " +
                            "exactly five Army clips.";
                    return false;
                }

                for (var overrideIndex = 0; overrideIndex < overrides.Count; overrideIndex++)
                {
                    if (overrides[overrideIndex].Key == null || overrides[overrideIndex].Value == null)
                    {
                        error = $"{name}.weaponAnimatorControllers[{index}] contains a missing " +
                                "Army clip override.";
                        return false;
                    }
                }

                foundIds[binding.WeaponId] = true;
            }

            for (var weaponId = 0; weaponId < foundIds.Length; weaponId++)
            {
                if (!foundIds[weaponId])
                {
                    error = $"{name}.weaponAnimatorControllers is missing WeaponId {weaponId}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void ApplyWeaponAnimatorController(int weaponId)
        {
            AnimatorOverrideController controller = null;
            for (var index = 0; index < weaponAnimatorControllers.Length; index++)
            {
                if (weaponAnimatorControllers[index].WeaponId == weaponId)
                {
                    controller = weaponAnimatorControllers[index].Controller;
                    break;
                }
            }

            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"No Army AnimatorOverrideController is bound for WeaponId {weaponId}.");
            }

            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].ApplyAnimatorController(controller, currentAnimationState);
            }
        }

        private void SetAnimationState(ArmyAnimationState animationState)
        {
            if (currentAnimationState == animationState)
            {
                return;
            }

            currentAnimationState = animationState;
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].SetAnimationState(animationState);
            }
        }

        private void InitializeSlots(int initialCount, float fireInterval)
        {
            armyCount = initialCount;
            var activeCount = Mathf.Min(initialCount, slots.Length);
            var baseCount = activeCount > 0 ? initialCount / activeCount : 0;
            var remainder = activeCount > 0 ? initialCount % activeCount : 0;

            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].Initialize(armyId, index);
                var represented = index < activeCount ? baseCount + (index < remainder ? 1 : 0) : 0;
                var hp = SaturatingMultiply(represented, armyConfig.HpPerSoldier);
                slots[index].SetState(represented, hp, hp, represented > 0 ? fireInterval : 0f);
            }
        }

        private void TickElementDuration(ref float remainingDuration, ElementType elementType, float deltaTime)
        {
            if (remainingDuration <= 0f)
            {
                remainingDuration = 0f;
                return;
            }

            var previous = remainingDuration;
            remainingDuration = Mathf.Max(0f, remainingDuration - deltaTime);
            if (previous > 0f && remainingDuration <= 0f)
            {
                eventBus.Publish(new ArmyElementExpired(levelRunId, armyId, elementType));
            }
        }

        private ArmyAnimationState MoveArmy(float deltaTime)
        {
            var displacement = horizontalInput * armyConfig.MoveSpeed * deltaTime;
            if (!IsFinite(displacement))
            {
                throw new InvalidOperationException("Army movement produced a non-finite displacement.");
            }

            var previousX = transform.position.x;
            var position = transform.position;
            position.x += displacement;
            position.y = 0f;
            position.z = 0f;
            transform.position = position;
            ClampRootToRoad();

            var actualDisplacement = transform.position.x - previousX;
            if (actualDisplacement < 0f)
            {
                return ArmyAnimationState.MoveLeft;
            }

            return actualDisplacement > 0f
                ? ArmyAnimationState.MoveRight
                : ArmyAnimationState.Attack;
        }

        private void ClampRootToRoad()
        {
            if (!TryGetActiveBounds(out var bounds))
            {
                return;
            }

            var rootX = transform.position.x;
            var leftInset = rootX - bounds.min.x;
            var rightInset = bounds.max.x - rootX;
            var minimumRootX = roadLayout.LeftBoundary + leftInset;
            var maximumRootX = roadLayout.RightBoundary - rightInset;
            if (minimumRootX > maximumRootX)
            {
                throw new InvalidOperationException("The active Army formation is wider than the configured road.");
            }

            var position = transform.position;
            position.x = Mathf.Clamp(position.x, minimumRootX, maximumRootX);
            position.y = 0f;
            position.z = 0f;
            transform.position = position;
        }

        private bool TryGetActiveBounds(out Bounds bounds)
        {
            bounds = default(Bounds);
            var found = false;
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (!slot.IsActive || slot.SlotCollider == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = slot.SlotCollider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(slot.SlotCollider.bounds);
                }
            }

            return found;
        }

        private void TickFire(float deltaTime)
        {
            if (armyCount <= 0)
            {
                return;
            }

            var weapon = weaponConfigProvider.GetWeaponConfig(currentWeaponId);
            var activeElements = GetActiveElements();
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (!slot.IsActive)
                {
                    continue;
                }

                var cooldown = Mathf.Max(0f, slot.FireCooldownRemaining - deltaTime);
                if (cooldown > 0f)
                {
                    slot.SetFireCooldown(cooldown);
                    continue;
                }

                bulletManager.Spawn(
                    new BulletSpawnRequest(
                        levelRunId,
                        armyId,
                        slot.SlotIndex,
                        weapon.BulletId,
                        currentWeaponId,
                        activeElements,
                        slot.FirePosition,
                        Vector2.up));
                slot.SetFireCooldown(weapon.FireInterval);
            }
        }

        private SlotDamageResult ApplyDamageToSlot(ArmySlotView slot, int damage)
        {
            var oldCount = slot.RepresentedCount;
            var actualHpDamage = Mathf.Min(damage, slot.CurrentHp);
            var nextHp = slot.CurrentHp - actualHpDamage;
            var nextCount = nextHp <= 0
                ? 0
                : (int)Math.Min(int.MaxValue, ((long)nextHp + armyConfig.HpPerSoldier - 1) / armyConfig.HpPerSoldier);
            var nextMaxHp = SaturatingMultiply(nextCount, armyConfig.HpPerSoldier);
            var countLoss = oldCount - nextCount;
            slot.SetState(nextCount, nextHp, nextMaxHp, nextCount > 0 ? slot.FireCooldownRemaining : 0f);

            eventBus.Publish(
                new SoldierHit(
                    levelRunId,
                    armyId,
                    slot.SlotIndex,
                    damage,
                    actualHpDamage,
                    countLoss));
            return new SlotDamageResult(actualHpDamage, countLoss);
        }

        private void DistributeAddition(int amount, float fireInterval)
        {
            if (amount <= 0)
            {
                return;
            }

            var ordered = new List<ArmySlotView>(slots);
            ordered.Sort((left, right) =>
            {
                var countComparison = left.RepresentedCount.CompareTo(right.RepresentedCount);
                return countComparison != 0
                    ? countComparison
                    : left.SlotIndex.CompareTo(right.SlotIndex);
            });

            long remaining = amount;
            var groupSize = 1;
            var currentLevel = ordered[0].RepresentedCount;
            while (remaining > 0)
            {
                if (groupSize < ordered.Count)
                {
                    var nextLevel = ordered[groupSize].RepresentedCount;
                    var levelDelta = nextLevel - currentLevel;
                    var cost = (long)levelDelta * groupSize;
                    if (cost <= remaining)
                    {
                        for (var index = 0; index < groupSize; index++)
                        {
                            ApplySlotAddition(ordered[index], levelDelta, fireInterval);
                        }

                        remaining -= cost;
                        currentLevel = nextLevel;
                        groupSize++;
                        continue;
                    }
                }

                var completeRounds = (int)(remaining / groupSize);
                var extraSlots = (int)(remaining % groupSize);
                var tiedGroup = ordered.GetRange(0, groupSize);
                tiedGroup.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
                for (var index = 0; index < tiedGroup.Count; index++)
                {
                    var addition = completeRounds + (index < extraSlots ? 1 : 0);
                    ApplySlotAddition(tiedGroup[index], addition, fireInterval);
                }

                remaining = 0;
            }
        }

        private void ApplySlotAddition(ArmySlotView slot, int addition, float fireInterval)
        {
            if (addition <= 0)
            {
                return;
            }

            var wasInactive = !slot.IsActive;
            var newCount = SaturatingAdd(slot.RepresentedCount, addition);
            var addedHp = SaturatingMultiply(addition, armyConfig.HpPerSoldier);
            var newHp = SaturatingAdd(slot.CurrentHp, addedHp);
            var maxHp = SaturatingMultiply(newCount, armyConfig.HpPerSoldier);
            var cooldown = wasInactive ? fireInterval : slot.FireCooldownRemaining;
            slot.SetState(newCount, Mathf.Min(newHp, maxHp), maxHp, cooldown);
        }

        private int SumRepresentedCount()
        {
            var total = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                total = SaturatingAdd(total, slots[index].RepresentedCount);
            }

            return total;
        }

        private bool TryGetSlot(int slotIndex, out ArmySlotView slot)
        {
            if (slots != null && slotIndex >= 0 && slotIndex < slots.Length)
            {
                slot = slots[slotIndex];
                return slot != null;
            }

            slot = null;
            return false;
        }

        private static ArmySlotTarget CreateTarget(ArmySlotView slot)
        {
            return new ArmySlotTarget(
                slot.SlotIndex,
                slot.WorldPosition,
                slot.RepresentedCount,
                slot.IsActive);
        }

        private ElementMask GetActiveElements()
        {
            var result = ElementMask.None;
            if (fireRemainingDuration > 0f)
            {
                result |= ElementMask.Fire;
            }

            if (iceRemainingDuration > 0f)
            {
                result |= ElementMask.Ice;
            }

            if (lightningRemainingDuration > 0f)
            {
                result |= ElementMask.Lightning;
            }

            return result;
        }

        private float GetElementDuration(ElementType elementType)
        {
            switch (elementType)
            {
                case ElementType.Fire:
                    return fireRemainingDuration;
                case ElementType.Ice:
                    return iceRemainingDuration;
                case ElementType.Lightning:
                    return lightningRemainingDuration;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementType));
            }
        }

        private void SetElementDuration(ElementType elementType, float value)
        {
            switch (elementType)
            {
                case ElementType.Fire:
                    fireRemainingDuration = value;
                    break;
                case ElementType.Ice:
                    iceRemainingDuration = value;
                    break;
                case ElementType.Lightning:
                    lightningRemainingDuration = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementType));
            }
        }

        private void PublishCountChanged(int changeAmount, ArmyCountChangeReason reason)
        {
            eventBus.Publish(new ArmyCountChanged(levelRunId, armyCount, changeAmount, reason));
        }

        private void PublishFormationChanged()
        {
            eventBus.Publish(new ArmyFormationChanged(levelRunId, GetFormationSnapshot()));
        }

        private void PublishZeroIfNeeded(int armyCountLoss, ArmyReachedZeroReason reason)
        {
            if (armyCountLoss > 0 && armyCount == 0)
            {
                eventBus.Publish(new ArmyReachedZero(levelRunId, reason));
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
                throw new InvalidOperationException("ArmyController has not been initialized.");
            }
        }

        private void EnsureRunning()
        {
            EnsureInitialized();
            if (!running || victoryPresentation)
            {
                throw new InvalidOperationException(
                    "ArmyController does not have an active gameplay run.");
            }
        }

        private static void ValidateRoadLayout(RoadLayoutSnapshot layout)
        {
            if (!IsFinite(layout.LeftBoundary) || !IsFinite(layout.RightBoundary) ||
                layout.LeftBoundary >= layout.RightBoundary)
            {
                throw new ArgumentException("Road layout horizontal bounds are invalid.", nameof(layout));
            }
        }

        private static int CompareSlotsForRemoval(ArmySlotView left, ArmySlotView right)
        {
            var hpComparison = left.CurrentHp.CompareTo(right.CurrentHp);
            return hpComparison != 0 ? hpComparison : left.SlotIndex.CompareTo(right.SlotIndex);
        }

        private static int SaturatingAdd(int left, int right)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(0L, (long)left + right));
        }

        private static float SaturatingAdd(float left, float right)
        {
            var value = (double)left + right;
            return value >= float.MaxValue ? float.MaxValue : (float)value;
        }

        private static int SaturatingMultiply(int left, int right)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(0L, (long)left * right));
        }

        private static long SaturatingMultiplyLong(int left, int right)
        {
            if (left == 0 || right == 0)
            {
                return 0;
            }

            if (left > long.MaxValue / right)
            {
                return long.MaxValue;
            }

            return (long)left * right;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct SlotDamageResult
        {
            public SlotDamageResult(int actualHpDamage, int armyCountLoss)
            {
                ActualHpDamage = actualHpDamage;
                ArmyCountLoss = armyCountLoss;
            }

            public int ActualHpDamage { get; }
            public int ArmyCountLoss { get; }
        }
    }
}
