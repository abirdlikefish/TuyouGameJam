using System;
using System.Collections.Generic;
using Game.Contracts;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class AdditiveGate : MonoBehaviour, IRuntimeBulletTarget, IRoadObstacleRuntime
    {
        private static readonly int AdditiveLoopState = Animator.StringToHash("Base Layer.Additive_Loop");

        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BulletHitProxy bulletHitProxy;
        [SerializeField, Min(0f)] private float moveSpeed;
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
        private int gateValue;
        private GateContactState contactState;
        private bool runtimeActive;
        private bool animationPrepared;

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
        bool IBulletHittable.CanReceiveBulletHit => runtimeActive && contactState == GateContactState.Pending;

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

            if (bulletHitProxy.gameObject != bodyCollider.gameObject ||
                bulletHitProxy.TargetBehaviour != this ||
                !bulletHitProxy.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? $"{name}.bodyCollider requires a same-node BulletHitProxy bound to this gate."
                    : error;
                return false;
            }

            if (!BulletTargetPhysicsAdapter.TryValidate(bodyCollider, gameObject, out error))
            {
                return false;
            }

            if (!IsFinite(moveSpeed) || moveSpeed < 0f)
            {
                error = $"{name}.moveSpeed must be finite and non-negative.";
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
            if (request.GateType != GateType.Additive || request.InitialValue == int.MinValue)
            {
                throw new ArgumentException("Additive gate spawn data is invalid.", nameof(request));
            }

            army = armyController ?? throw new ArgumentNullException(nameof(armyController));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            recycleCallback = onRecycleRequested ?? throw new ArgumentNullException(nameof(onRecycleRequested));
            levelRunId = request.LevelRunId;
            runtimeInstanceId = initializedRuntimeInstanceId;
            spawnEntryIndex = request.SpawnEntryIndex;
            gateValue = request.InitialValue;
            contactState = GateContactState.Pending;
            runtimeActive = true;
            consumedBulletIds.Clear();
            transform.SetParent(parent, false);
            transform.position = request.WorldPosition;
            transform.rotation = Quaternion.identity;
            bodyCollider.enabled = true;
            RefreshText();
            PrepareAnimation();
        }

        private void OnEnable()
        {
            if (runtimeActive && animationPrepared)
            {
                PlayAnimation();
            }
        }

        public void ReceiveBulletHit(BulletDamageContext damage)
        {
            if (!runtimeActive || contactState != GateContactState.Pending || damage.Damage <= 0 ||
                !consumedBulletIds.Add(damage.BulletInstanceId))
            {
                return;
            }

            var previous = gateValue;
            gateValue = (int)Math.Min(int.MaxValue, (long)gateValue + damage.Damage);
            RefreshText();
            eventBus.Publish(
                new GateValueChanged(
                    levelRunId,
                    runtimeInstanceId,
                    damage,
                    previous,
                    gateValue,
                    gateValue - previous));
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

            ArmyAdditionResult? addition = null;
            ArmyRemovalResult? removal = null;
            var succeeded = gateValue >= 0;
            if (succeeded)
            {
                addition = army.AddArmy(gateValue);
                contactState = GateContactState.Succeeded;
            }
            else
            {
                removal = army.RemoveArmy(-gateValue);
                contactState = GateContactState.Failed;
            }

            eventBus.Publish(
                new GateContactResolved(
                    levelRunId,
                    runtimeInstanceId,
                    armyId,
                    GateType.Additive,
                    succeeded,
                    addition,
                    removal,
                    ElementType.None,
                    0,
                    0f,
                    0f,
                    null,
                    false,
                    false));
            recycleCallback(this, ObstacleRecycleReason.ContactResolved);
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
            if (animator.gameObject.activeInHierarchy)
            {
                animator.Rebind();
            }

            bodyCollider.enabled = false;
            army = null;
            eventBus = null;
            recycleCallback = null;
            levelRunId = 0;
            runtimeInstanceId = -1;
            spawnEntryIndex = -1;
            gateValue = 0;
            consumedBulletIds.Clear();
            gameObject.SetActive(false);
        }

        private void PrepareAnimation()
        {
            animationPrepared = true;
            if (gameObject.activeInHierarchy)
            {
                PlayAnimation();
            }
        }

        private void PlayAnimation()
        {
            animator.Rebind();
            animator.Play(AdditiveLoopState, 0, 0f);
            animator.Update(0f);
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
                stateText.text = gateValue.ToString();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
