using System;
using System.Collections.Generic;
using Game.Contracts;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    public abstract class BreakablePropBase : MonoBehaviour, IRuntimeBulletTarget, IRoadObstacleRuntime
    {
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BulletHitProxy bulletHitProxy;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private TMP_Text debugText;

        private readonly HashSet<int> consumedBulletIds = new HashSet<int>();

        private IArmyController army;
        private IEventBus eventBus;
        private Action<IRoadObstacleRuntime, ObstacleRecycleReason> recycleCallback;
        private int levelRunId;
        private int runtimeInstanceId = -1;
        private int spawnEntryIndex = -1;
        private int? configId;
        private int maxHp;
        private int contactDamage;
        private float moveSpeed;
        private int currentHp;
        private PropContactState contactState;
        private bool runtimeActive;

        public int RuntimeInstanceId => runtimeInstanceId;
        protected int LevelRunId => levelRunId;
        protected int CurrentHp => currentHp;
        protected int MaxHp => maxHp;
        protected IArmyController Army => army;
        protected IEventBus EventBus => eventBus;

        int IRoadObstacleRuntime.LevelRunId => levelRunId;
        int IRoadObstacleRuntime.SpawnEntryIndex => spawnEntryIndex;
        int? IRoadObstacleRuntime.ConfigId => configId;
        ObstacleKind IRoadObstacleRuntime.Kind => ObstacleKind.Prop;
        ObstacleState IRoadObstacleRuntime.State => ToObstacleState();
        bool IRoadObstacleRuntime.IsRuntimeActive => runtimeActive;
        bool IRoadObstacleRuntime.CanResolveContact => runtimeActive && contactState == PropContactState.Pending;
        Collider2D IRoadObstacleRuntime.BodyCollider => bodyCollider;
        Vector2 IRoadObstacleRuntime.WorldPosition => transform.position;
        BulletTargetKind IRuntimeBulletTarget.BulletTargetKind => BulletTargetKind.Prop;
        bool IBulletHittable.CanReceiveBulletHit => runtimeActive &&
            (contactState == PropContactState.Pending || contactState == PropContactState.Failed);

        public virtual bool TryValidate(out string error)
        {
            error = string.Empty;
            if (bodyCollider == null || bulletHitProxy == null || visual == null)
            {
                error = $"{name} requires bodyCollider, bulletHitProxy and visual bindings.";
                return false;
            }

            if (bulletHitProxy.gameObject != bodyCollider.gameObject ||
                bulletHitProxy.TargetBehaviour != this ||
                !bulletHitProxy.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? $"{name}.bodyCollider requires a same-node BulletHitProxy bound to this prop."
                    : error;
                return false;
            }

            return BulletTargetPhysicsAdapter.TryValidate(bodyCollider, gameObject, out error);
        }

        private protected void InitializeBreakableRuntime(
            int initializedLevelRunId,
            int initializedRuntimeInstanceId,
            int initializedSpawnEntryIndex,
            int? initializedConfigId,
            int initializedMaxHp,
            int initializedContactDamage,
            float initializedMoveSpeed,
            Vector2 worldPosition,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (initializedLevelRunId <= 0 || initializedRuntimeInstanceId < 0 ||
                initializedSpawnEntryIndex < -1 || initializedMaxHp <= 0 ||
                initializedContactDamage <= 0 || !IsFinite(initializedMoveSpeed) ||
                initializedMoveSpeed < 0f || !IsFinite(worldPosition))
            {
                throw new ArgumentException("Breakable prop runtime configuration is invalid.");
            }

            army = armyController ?? throw new ArgumentNullException(nameof(armyController));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            recycleCallback = onRecycleRequested ?? throw new ArgumentNullException(nameof(onRecycleRequested));
            levelRunId = initializedLevelRunId;
            runtimeInstanceId = initializedRuntimeInstanceId;
            spawnEntryIndex = initializedSpawnEntryIndex;
            configId = initializedConfigId;
            maxHp = initializedMaxHp;
            contactDamage = initializedContactDamage;
            moveSpeed = initializedMoveSpeed;
            currentHp = maxHp;
            contactState = PropContactState.Pending;
            runtimeActive = true;
            consumedBulletIds.Clear();
            transform.SetParent(parent, false);
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            bodyCollider.enabled = true;
            RefreshDebugText();
        }

        public void ReceiveBulletHit(BulletDamageContext damage)
        {
            if (!runtimeActive || damage.Damage <= 0 ||
                (contactState != PropContactState.Pending && contactState != PropContactState.Failed) ||
                !consumedBulletIds.Add(damage.BulletInstanceId))
            {
                return;
            }

            if (contactState == PropContactState.Failed)
            {
                currentHp = (int)Math.Max(1L, (long)currentHp - damage.Damage);
                RefreshDebugText();
                return;
            }

            currentHp = (int)Math.Max(0L, (long)currentHp - damage.Damage);
            RefreshDebugText();
            if (currentHp > 0)
            {
                return;
            }

            contactState = PropContactState.Succeeded;
            OnBroken(damage);
            recycleCallback(this, ObstacleRecycleReason.Broken);
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
            if (!runtimeActive || contactState != PropContactState.Pending ||
                slotIndices == null || slotIndices.Count == 0)
            {
                return;
            }

            contactState = PropContactState.Failed;
            for (var index = 0; index < slotIndices.Count; index++)
            {
                var slotIndex = slotIndices[index];
                if (!army.TryGetSlotTarget(slotIndex, out var target) || !target.IsActive)
                {
                    continue;
                }

                army.ApplySlotDamage(slotIndex, contactDamage);
                eventBus.Publish(
                    new PropContactDamage(
                        levelRunId,
                        runtimeInstanceId,
                        armyId,
                        slotIndex,
                        contactDamage));
            }

            RefreshDebugText();
        }

        void IRoadObstacleRuntime.ExitRoad()
        {
            if (!runtimeActive)
            {
                return;
            }

            var contacted = contactState != PropContactState.Pending;
            if (!contacted)
            {
                contactState = PropContactState.ExitedUncontacted;
            }

            eventBus.Publish(new PropExitedRoad(levelRunId, runtimeInstanceId, contacted));
            recycleCallback(this, ObstacleRecycleReason.ExitedRoad);
        }

        void IRoadObstacleRuntime.PrepareForPool()
        {
            runtimeActive = false;
            bodyCollider.enabled = false;
            OnPrepareForPool();
            army = null;
            eventBus = null;
            recycleCallback = null;
            levelRunId = 0;
            runtimeInstanceId = -1;
            spawnEntryIndex = -1;
            configId = null;
            maxHp = 0;
            contactDamage = 0;
            moveSpeed = 0f;
            currentHp = 0;
            consumedBulletIds.Clear();
            gameObject.SetActive(false);
        }

        protected abstract void OnBroken(BulletDamageContext damage);
        protected abstract string BuildDebugText();
        protected virtual void OnPrepareForPool() { }

        private ObstacleState ToObstacleState()
        {
            switch (contactState)
            {
                case PropContactState.Succeeded:
                    return ObstacleState.Broken;
                case PropContactState.Failed:
                    return ObstacleState.ContactFailed;
                case PropContactState.ExitedUncontacted:
                    return ObstacleState.ExitedUncontacted;
                default:
                    return ObstacleState.ContactPending;
            }
        }

        private void RefreshDebugText()
        {
            if (debugText != null)
            {
                debugText.text = BuildDebugText();
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
