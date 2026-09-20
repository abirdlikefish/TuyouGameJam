using System;
using System.Collections.Generic;
using Game.Contracts;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ElementGate : MonoBehaviour, IRuntimeBulletTarget, IRoadObstacleRuntime
    {
        private static readonly int ElementTypeParameter = Animator.StringToHash("ElementType");
        private static readonly int FireLoopState = Animator.StringToHash("Base Layer.Fire_Loop");
        private static readonly int IceLoopState = Animator.StringToHash("Base Layer.Ice_Loop");
        private static readonly int LightningLoopState = Animator.StringToHash("Base Layer.Lightning_Loop");

        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BulletHitProxy bulletHitProxy;
        [SerializeField, Min(0f)] private float moveSpeed;
        [SerializeField, Min(1)] private int contactDamage = 1;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Animator animator;

        private readonly HashSet<int> consumedBulletIds = new HashSet<int>();

        private IArmyController army;
        private IEventBus eventBus;
        private Action<IRoadObstacleRuntime, ObstacleRecycleReason> recycleCallback;
        private int levelRunId;
        private int runtimeInstanceId = -1;
        private int spawnEntryIndex = -1;
        private int maxHp;
        private int currentHp;
        private long postDepletionDamage;
        private float durationSecondsPerDamage;
        private ElementType elementType;
        private GateContactState contactState;
        private bool rewardLocked;
        private bool runtimeActive;
        private bool animationPrepared;
        private int preparedAnimationState;

        public int RuntimeInstanceId => runtimeInstanceId;
        int IRoadObstacleRuntime.LevelRunId => levelRunId;
        int IRoadObstacleRuntime.SpawnEntryIndex => spawnEntryIndex;
        int? IRoadObstacleRuntime.ConfigId => null;
        ObstacleKind IRoadObstacleRuntime.Kind => ObstacleKind.Gate;
        ObstacleState IRoadObstacleRuntime.State => ToObstacleState();
        bool IRoadObstacleRuntime.IsRuntimeActive => runtimeActive;
        bool IRoadObstacleRuntime.CanResolveContact => runtimeActive && contactState == GateContactState.Pending;
        Collider2D IRoadObstacleRuntime.BodyCollider => bodyCollider;
        Vector2 IRoadObstacleRuntime.WorldPosition => transform.position;
        BulletTargetKind IRuntimeBulletTarget.BulletTargetKind => BulletTargetKind.Gate;
        bool IBulletHittable.CanReceiveBulletHit => runtimeActive &&
            (contactState == GateContactState.Pending || contactState == GateContactState.Failed);

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (bodyCollider == null || bulletHitProxy == null || stateText == null || visual == null ||
                animator == null || animator.runtimeAnimatorController == null)
            {
                error = $"{name} requires bodyCollider, bulletHitProxy, stateText, visual and Animator bindings.";
                return false;
            }

            if (!animator.enabled)
            {
                error = $"{name}.animator must be enabled.";
                return false;
            }

            if (animator.gameObject.activeInHierarchy &&
                !HasAnimatorParameter(animator, ElementTypeParameter, AnimatorControllerParameterType.Int))
            {
                error = $"{name}.animator controller requires an Int parameter named ElementType.";
                return false;
            }

            if (bulletHitProxy.gameObject != bodyCollider.gameObject ||
                bulletHitProxy.TargetBehaviour != this ||
                !bulletHitProxy.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? $"{name}.bodyCollider requires a same-node BulletHitProxy bound to this gate."
                    : error;
                return false;
            }

            if (!IsFinite(moveSpeed) || moveSpeed < 0f)
            {
                error = $"{name}.moveSpeed must be finite and non-negative.";
                return false;
            }

            if (contactDamage <= 0)
            {
                error = $"{name}.contactDamage must be greater than zero.";
                return false;
            }

            return true;
        }

        internal void InitializeRuntime(
            GateSpawnRequest request,
            int initializedRuntimeInstanceId,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (request.GateType != GateType.Element || request.MaxHp <= 0 ||
                request.ElementType == ElementType.None ||
                !IsFinite(request.ElementDurationSecondsPerDamage) ||
                request.ElementDurationSecondsPerDamage <= 0f)
            {
                throw new ArgumentException("Element gate spawn data is invalid.", nameof(request));
            }

            var animationState = GetAnimationState(request.ElementType);

            army = armyController ?? throw new ArgumentNullException(nameof(armyController));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            recycleCallback = onRecycleRequested ?? throw new ArgumentNullException(nameof(onRecycleRequested));
            levelRunId = request.LevelRunId;
            runtimeInstanceId = initializedRuntimeInstanceId;
            spawnEntryIndex = request.SpawnEntryIndex;
            maxHp = request.MaxHp;
            currentHp = maxHp;
            postDepletionDamage = 0;
            durationSecondsPerDamage = request.ElementDurationSecondsPerDamage;
            elementType = request.ElementType;
            contactState = GateContactState.Pending;
            rewardLocked = false;
            runtimeActive = true;
            consumedBulletIds.Clear();
            transform.SetParent(parent, false);
            transform.position = request.WorldPosition;
            transform.rotation = Quaternion.identity;
            bodyCollider.enabled = true;
            RefreshText();
            PrepareAnimation(animationState);
        }

        private void OnEnable()
        {
            if (runtimeActive && animationPrepared)
            {
                PlayPreparedAnimation();
            }
        }

        public void ReceiveBulletHit(BulletDamageContext damage)
        {
            if (!runtimeActive || damage.Damage <= 0 ||
                (contactState != GateContactState.Pending && contactState != GateContactState.Failed) ||
                !consumedBulletIds.Add(damage.BulletInstanceId))
            {
                return;
            }

            var previousHp = currentHp;
            var hpDamage = 0;
            var extraDamage = 0;
            if (contactState == GateContactState.Failed)
            {
                currentHp = (int)Math.Max(1L, (long)currentHp - damage.Damage);
                hpDamage = previousHp - currentHp;
            }
            else
            {
                hpDamage = Mathf.Min(currentHp, damage.Damage);
                currentHp -= hpDamage;
                extraDamage = damage.Damage - hpDamage;
                postDepletionDamage = SaturatingAdd(postDepletionDamage, extraDamage);
            }

            RefreshText();
            eventBus.Publish(
                new ElementGateDamageChanged(
                    levelRunId,
                    runtimeInstanceId,
                    damage,
                    previousHp,
                    currentHp,
                    hpDamage,
                    extraDamage,
                    postDepletionDamage,
                    rewardLocked));
        }

        void IRoadObstacleRuntime.TickMovement(float deltaTime)
        {
            if (runtimeActive)
            {
                transform.position += Vector3.down * (moveSpeed * deltaTime);
            }
        }

        void IRoadObstacleRuntime.ResolveContact(int armyId, IReadOnlyList<int> slotIndices)
        {
            if (!runtimeActive || contactState != GateContactState.Pending ||
                slotIndices == null || slotIndices.Count == 0)
            {
                return;
            }

            if (currentHp <= 0)
            {
                contactState = GateContactState.Succeeded;
                var calculatedDuration = SaturatingMultiply(postDepletionDamage, durationSecondsPerDamage);
                ElementDurationChangeResult? changeResult = null;
                if (calculatedDuration > 0f)
                {
                    changeResult = army.AddElementDuration(
                        elementType,
                        calculatedDuration,
                        runtimeInstanceId);
                }

                eventBus.Publish(
                    new GateContactResolved(
                        levelRunId,
                        runtimeInstanceId,
                        armyId,
                        GateType.Element,
                        true,
                        null,
                        null,
                        elementType,
                        postDepletionDamage,
                        durationSecondsPerDamage,
                        calculatedDuration,
                        changeResult,
                        false,
                        false));
                recycleCallback(this, ObstacleRecycleReason.ContactResolved);
                return;
            }

            contactState = GateContactState.Failed;
            rewardLocked = true;
            for (var index = 0; index < slotIndices.Count; index++)
            {
                var slotIndex = slotIndices[index];
                if (army.TryGetSlotTarget(slotIndex, out var target) && target.IsActive)
                {
                    army.ApplySlotDamage(slotIndex, contactDamage);
                }
            }

            RefreshText();
            eventBus.Publish(
                new GateContactResolved(
                    levelRunId,
                    runtimeInstanceId,
                    armyId,
                    GateType.Element,
                    false,
                    null,
                    null,
                    elementType,
                    postDepletionDamage,
                    durationSecondsPerDamage,
                    0f,
                    null,
                    true,
                    true));
        }

        void IRoadObstacleRuntime.ExitRoad()
        {
            if (!runtimeActive)
            {
                return;
            }

            var contacted = contactState != GateContactState.Pending;
            if (!contacted)
            {
                contactState = GateContactState.ExitedUncontacted;
            }

            eventBus.Publish(new GateExitedRoad(levelRunId, runtimeInstanceId, contacted));
            recycleCallback(this, ObstacleRecycleReason.ExitedRoad);
        }

        void IRoadObstacleRuntime.PrepareForPool()
        {
            runtimeActive = false;
            animationPrepared = false;
            preparedAnimationState = 0;
            if (animator.gameObject.activeInHierarchy)
            {
                animator.Rebind();
                animator.SetInteger(ElementTypeParameter, (int)ElementType.None);
            }

            bodyCollider.enabled = false;
            army = null;
            eventBus = null;
            recycleCallback = null;
            levelRunId = 0;
            runtimeInstanceId = -1;
            spawnEntryIndex = -1;
            maxHp = 0;
            currentHp = 0;
            postDepletionDamage = 0;
            durationSecondsPerDamage = 0f;
            elementType = ElementType.None;
            rewardLocked = false;
            consumedBulletIds.Clear();
            gameObject.SetActive(false);
        }

        private void PrepareAnimation(int animationState)
        {
            preparedAnimationState = animationState;
            animationPrepared = true;
            if (gameObject.activeInHierarchy)
            {
                PlayPreparedAnimation();
            }
        }

        private void PlayPreparedAnimation()
        {
            animator.Rebind();
            RequireAnimatorParameter();
            animator.SetInteger(ElementTypeParameter, (int)elementType);
            animator.Play(preparedAnimationState, 0, 0f);
            animator.Update(0f);
        }

        private static int GetAnimationState(ElementType initializedElementType)
        {
            switch (initializedElementType)
            {
                case ElementType.Fire:
                    return FireLoopState;
                case ElementType.Ice:
                    return IceLoopState;
                case ElementType.Lightning:
                    return LightningLoopState;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(initializedElementType),
                        initializedElementType,
                        "Only Fire, Ice and Lightning have bound animation states.");
            }
        }

        private static bool HasAnimatorParameter(
            Animator targetAnimator,
            int parameterNameHash,
            AnimatorControllerParameterType parameterType)
        {
            var parameters = targetAnimator.parameters;
            for (var index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].nameHash == parameterNameHash && parameters[index].type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private void RequireAnimatorParameter()
        {
            if (!HasAnimatorParameter(animator, ElementTypeParameter, AnimatorControllerParameterType.Int))
            {
                throw new InvalidOperationException(
                    $"{name}.animator controller requires an Int parameter named ElementType.");
            }
        }

        private ObstacleState ToObstacleState()
        {
            switch (contactState)
            {
                case GateContactState.Succeeded:
                    return ObstacleState.ContactSucceeded;
                case GateContactState.Failed:
                    return ObstacleState.ContactFailed;
                case GateContactState.ExitedUncontacted:
                    return ObstacleState.ExitedUncontacted;
                default:
                    return ObstacleState.ContactPending;
            }
        }

        private void RefreshText()
        {
            if (stateText != null)
            {
                stateText.text = $"{elementType} {currentHp}/{maxHp} +{postDepletionDamage}";
            }
        }

        private static long SaturatingAdd(long left, int right)
        {
            return right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;
        }

        private static float SaturatingMultiply(long left, float right)
        {
            var result = left * (double)right;
            return result >= float.MaxValue ? float.MaxValue : (float)result;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
