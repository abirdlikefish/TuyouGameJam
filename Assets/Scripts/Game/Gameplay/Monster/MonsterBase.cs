using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    internal enum MonsterRuntimeState
    {
        MovingDown,
        ApproachingTarget,
        Blocked,
        Attacking,
        Dead
    }

    internal enum MonsterDeathVariant
    {
        Normal = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3
    }

    internal readonly struct MonsterAttackRequest
    {
        public MonsterAttackRequest(int attackSequenceId, int targetSlotIndex)
        {
            AttackSequenceId = attackSequenceId;
            TargetSlotIndex = targetSlotIndex;
        }

        public int AttackSequenceId { get; }
        public int TargetSlotIndex { get; }
    }

    public abstract class MonsterBase : MonoBehaviour, IRuntimeBulletTarget
    {
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int DeathVariantParameter = Animator.StringToHash("DeathVariant");
        private static readonly int MoveState = Animator.StringToHash("Base Layer.Move");

        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float blockingGap;

        [Header("元素效果节点")]
        [SerializeField] private GameObject fireEffectRoot;
        [SerializeField] private GameObject iceEffectRoot;
        [SerializeField] private GameObject lightningEffectRoot;

        private readonly RaycastHit2D[] blockingResults = new RaycastHit2D[32];

        private IArmyController army;
        private Action<MonsterBase, EnemyDamageContext, int, bool> damagedCallback;
        private Action<MonsterBase, EnemyDamageContext> deathCallback;
        private Action<MonsterBase> recycleCallback;
        private EnemyConfigSnapshot config;
        private MonsterRuntimeState state;
        private int levelRunId;
        private int runtimeInstanceId = -1;
        private int spawnEntryIndex;
        private int currentHp;
        private int targetSlotIndex = -1;
        private int attackSequenceId;
        private int pendingAttackSequenceId = -1;
        private float attackCooldownRemaining;
        private float enemyApproachY;
        private float recentFireRemaining;
        private float recentIceRemaining;
        private float recentLightningRemaining;
        private MonsterDeathVariant deathVariant;
        private bool runtimeActive;
        private bool attackFrameRegistered;
        private bool deathAnimationFinished;
        private bool animationPrepared;

        public abstract EnemyType EnemyType { get; }
        public abstract AttackType AttackType { get; }
        public virtual Collider2D AttackCollider => null;
        public int RuntimeInstanceId => runtimeInstanceId;
        internal int LevelRunId => levelRunId;
        internal int SpawnEntryIndex => spawnEntryIndex;
        internal int ConfigId => config.Id;
        internal int AttackPower => config.AttackPower;
        internal bool IsAlive => runtimeActive && state != MonsterRuntimeState.Dead && currentHp > 0;
        internal bool IsRuntimeActive => runtimeActive;
        internal Collider2D BodyCollider => bodyCollider;
        internal Vector2 EffectCenter => bodyCollider.bounds.center;
        internal MonsterDeathVariant DeathVariant => deathVariant;

        BulletTargetKind IRuntimeBulletTarget.BulletTargetKind => BulletTargetKind.Enemy;
        bool IBulletHittable.CanReceiveBulletHit => IsAlive;

        public virtual bool TryValidate(out string error)
        {
            error = string.Empty;
            if (bodyCollider == null)
            {
                error = $"{name}.bodyCollider is not assigned.";
                return false;
            }

            if (visual == null)
            {
                error = $"{name}.visual is not assigned.";
                return false;
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                error = $"{name}.animator and its controller are required.";
                return false;
            }

            if (!animator.enabled)
            {
                error = $"{name}.animator must be enabled.";
                return false;
            }

            if (animator.gameObject.activeInHierarchy &&
                (!HasAnimatorParameter(animator, AttackTrigger, AnimatorControllerParameterType.Trigger) ||
                 !HasAnimatorParameter(animator, DeathTrigger, AnimatorControllerParameterType.Trigger) ||
                 !HasAnimatorParameter(
                     animator,
                     DeathVariantParameter,
                     AnimatorControllerParameterType.Int)))
            {
                error = $"{name}.animator controller requires Attack/Death Triggers and an Int DeathVariant parameter.";
                return false;
            }

            if (!IsFinite(blockingGap) || blockingGap < 0f)
            {
                error = $"{name}.blockingGap must be finite and non-negative.";
                return false;
            }

            if (!TryValidateElementEffectRoot(fireEffectRoot, nameof(fireEffectRoot), out error) ||
                !TryValidateElementEffectRoot(iceEffectRoot, nameof(iceEffectRoot), out error) ||
                !TryValidateElementEffectRoot(
                    lightningEffectRoot,
                    nameof(lightningEffectRoot),
                    out error))
            {
                return false;
            }

            if (fireEffectRoot == iceEffectRoot || fireEffectRoot == lightningEffectRoot ||
                iceEffectRoot == lightningEffectRoot)
            {
                error = $"{name} requires three distinct element effect roots.";
                return false;
            }

            if (!bodyCollider.TryGetComponent<BulletHitProxy>(out var proxy) ||
                proxy.TargetBehaviour != this ||
                !proxy.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? $"{name}.bodyCollider requires a same-node BulletHitProxy bound to this monster."
                    : error;
                return false;
            }

            if (!BulletTargetPhysicsAdapter.TryValidate(bodyCollider, gameObject, out error))
            {
                return false;
            }

            return true;
        }

        internal void InitializeRuntime(
            EnemySpawnRequest request,
            EnemyConfigSnapshot enemyConfig,
            int initializedRuntimeInstanceId,
            float approachY,
            IArmyController armyController,
            Action<MonsterBase, EnemyDamageContext, int, bool> onDamaged,
            Action<MonsterBase, EnemyDamageContext> onDeath,
            Action<MonsterBase> onRecycleRequested,
            Transform parent)
        {
            if (enemyConfig.EnemyType != EnemyType)
            {
                throw new ArgumentException("Enemy config type does not match the concrete monster type.");
            }

            army = armyController ?? throw new ArgumentNullException(nameof(armyController));
            damagedCallback = onDamaged ?? throw new ArgumentNullException(nameof(onDamaged));
            deathCallback = onDeath ?? throw new ArgumentNullException(nameof(onDeath));
            recycleCallback = onRecycleRequested ?? throw new ArgumentNullException(nameof(onRecycleRequested));
            config = enemyConfig;
            levelRunId = request.LevelRunId;
            runtimeInstanceId = initializedRuntimeInstanceId;
            spawnEntryIndex = request.SpawnEntryIndex;
            enemyApproachY = approachY;
            currentHp = config.MaxHp;
            state = MonsterRuntimeState.MovingDown;
            targetSlotIndex = -1;
            attackSequenceId = 0;
            pendingAttackSequenceId = -1;
            attackCooldownRemaining = 0f;
            recentFireRemaining = 0f;
            recentIceRemaining = 0f;
            recentLightningRemaining = 0f;
            deathVariant = MonsterDeathVariant.Normal;
            attackFrameRegistered = false;
            deathAnimationFinished = false;
            runtimeActive = true;
            SyncElementEffectNodes();

            transform.SetParent(parent, false);
            transform.position = request.WorldPosition;
            transform.rotation = Quaternion.identity;
            bodyCollider.enabled = true;
            if (AttackCollider != null)
            {
                AttackCollider.enabled = true;
            }

            OnRuntimeInitialized();
            PrepareMoveAnimation();
        }

        private void OnEnable()
        {
            if (runtimeActive && animationPrepared)
            {
                PlayMoveAnimation();
            }
        }

        internal void TickMovement(float deltaTime, ContactFilter2D enemyBodyFilter)
        {
            if (!runtimeActive)
            {
                return;
            }

            if (state == MonsterRuntimeState.Dead)
            {
                return;
            }

            recentFireRemaining = Mathf.Max(0f, recentFireRemaining - deltaTime);
            recentIceRemaining = Mathf.Max(0f, recentIceRemaining - deltaTime);
            recentLightningRemaining = Mathf.Max(0f, recentLightningRemaining - deltaTime);
            SyncElementEffectNodes();

            attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - deltaTime);
            if (state == MonsterRuntimeState.Attacking)
            {
                return;
            }

            if (state == MonsterRuntimeState.MovingDown)
            {
                TickMovingDown(deltaTime, enemyBodyFilter);
                return;
            }

            TickApproaching(deltaTime, enemyBodyFilter);
        }

        public void ReceiveBulletHit(BulletDamageContext damage)
        {
            ApplyEnemyDamage(EnemyDamageContext.FromDirectBullet(damage));
        }

        internal bool ApplyEnemyDamage(EnemyDamageContext damage)
        {
            if (!IsAlive || damage.Damage <= 0)
            {
                return false;
            }

            RefreshRecentElements(damage.ActiveElements);
            currentHp = (int)Math.Max(0L, (long)currentHp - damage.Damage);
            var fatal = currentHp == 0;
            if (fatal)
            {
                deathVariant = ResolveDeathVariant(
                    damage.ActiveElements,
                    recentFireRemaining,
                    recentIceRemaining,
                    recentLightningRemaining);
            }

            SyncElementEffectNodes();
            damagedCallback(this, damage, currentHp, fatal);
            if (!fatal)
            {
                return true;
            }

            state = MonsterRuntimeState.Dead;
            targetSlotIndex = -1;
            pendingAttackSequenceId = -1;
            attackFrameRegistered = false;
            bodyCollider.enabled = false;
            if (AttackCollider != null)
            {
                AttackCollider.enabled = false;
            }

            animator.SetInteger(DeathVariantParameter, (int)deathVariant);
            animator.SetTrigger(DeathTrigger);
            deathCallback(this, damage);
            return true;
        }

        internal ElementMask GetRecentElementMask()
        {
            if (!IsAlive)
            {
                return ElementMask.None;
            }

            var result = ElementMask.None;
            if (recentFireRemaining > 0f)
            {
                result |= ElementMask.Fire;
            }

            if (recentIceRemaining > 0f)
            {
                result |= ElementMask.Ice;
            }

            if (recentLightningRemaining > 0f)
            {
                result |= ElementMask.Lightning;
            }

            return result;
        }

        internal void ApplyDisplacement(Vector2 displacement)
        {
            if (!runtimeActive || !IsFinite(displacement.x) || !IsFinite(displacement.y))
            {
                return;
            }

            transform.position = (Vector2)transform.position + displacement;
        }

        public void OnAttackFrame()
        {
            if (!runtimeActive || state != MonsterRuntimeState.Attacking || attackFrameRegistered)
            {
                return;
            }

            attackFrameRegistered = true;
            pendingAttackSequenceId = attackSequenceId;
        }

        public void OnAttackAnimationFinished()
        {
            if (!runtimeActive || state != MonsterRuntimeState.Attacking)
            {
                return;
            }

            state = MonsterRuntimeState.ApproachingTarget;
            targetSlotIndex = -1;
            attackFrameRegistered = false;
        }

        public void OnDeathAnimationFinished()
        {
            if (!runtimeActive || state != MonsterRuntimeState.Dead || deathAnimationFinished)
            {
                return;
            }

            deathAnimationFinished = true;
            recycleCallback(this);
        }

        internal bool TryConsumeAttackRequest(out MonsterAttackRequest request)
        {
            if (runtimeActive && state == MonsterRuntimeState.Attacking &&
                pendingAttackSequenceId == attackSequenceId && pendingAttackSequenceId >= 0)
            {
                request = new MonsterAttackRequest(pendingAttackSequenceId, targetSlotIndex);
                pendingAttackSequenceId = -1;
                return true;
            }

            request = default(MonsterAttackRequest);
            return false;
        }

        internal void PrepareForPool()
        {
            runtimeActive = false;
            animationPrepared = false;
            deathVariant = MonsterDeathVariant.Normal;
            if (animator.gameObject.activeInHierarchy)
            {
                animator.ResetTrigger(AttackTrigger);
                animator.ResetTrigger(DeathTrigger);
                animator.Rebind();
                animator.SetInteger(DeathVariantParameter, (int)deathVariant);
            }

            bodyCollider.enabled = false;
            if (AttackCollider != null)
            {
                AttackCollider.enabled = false;
            }

            OnPrepareForPool();
            army = null;
            damagedCallback = null;
            deathCallback = null;
            recycleCallback = null;
            levelRunId = 0;
            runtimeInstanceId = -1;
            spawnEntryIndex = -1;
            currentHp = 0;
            recentFireRemaining = 0f;
            recentIceRemaining = 0f;
            recentLightningRemaining = 0f;
            SyncElementEffectNodes();
            targetSlotIndex = -1;
            pendingAttackSequenceId = -1;
            gameObject.SetActive(false);
        }

        private void RefreshRecentElements(ElementMask elements)
        {
            if ((elements & ElementMask.Fire) != 0)
            {
                recentFireRemaining = 1f;
            }

            if ((elements & ElementMask.Ice) != 0)
            {
                recentIceRemaining = 1f;
            }

            if ((elements & ElementMask.Lightning) != 0)
            {
                recentLightningRemaining = 1f;
            }
        }

        private static MonsterDeathVariant ResolveDeathVariant(
            ElementMask lethalElements,
            float fireRemaining,
            float iceRemaining,
            float lightningRemaining)
        {
            // 致命命中的元素必然比历史记录更新；同一次多元素命中按固定优先级选择。
            if ((lethalElements & ElementMask.Fire) != 0)
            {
                return MonsterDeathVariant.Fire;
            }

            if ((lethalElements & ElementMask.Ice) != 0)
            {
                return MonsterDeathVariant.Ice;
            }

            if ((lethalElements & ElementMask.Lightning) != 0)
            {
                return MonsterDeathVariant.Lightning;
            }

            var selected = MonsterDeathVariant.Normal;
            var selectedRemaining = 0f;
            if (fireRemaining > selectedRemaining)
            {
                selected = MonsterDeathVariant.Fire;
                selectedRemaining = fireRemaining;
            }

            if (iceRemaining > selectedRemaining)
            {
                selected = MonsterDeathVariant.Ice;
                selectedRemaining = iceRemaining;
            }

            if (lightningRemaining > selectedRemaining)
            {
                selected = MonsterDeathVariant.Lightning;
            }

            return selected;
        }

        private void SyncElementEffectNodes()
        {
            var elements = GetRecentElementMask();
            SetEffectRootActive(fireEffectRoot, (elements & ElementMask.Fire) != 0);
            SetEffectRootActive(iceEffectRoot, (elements & ElementMask.Ice) != 0);
            SetEffectRootActive(lightningEffectRoot, (elements & ElementMask.Lightning) != 0);
        }

        private bool TryValidateElementEffectRoot(
            GameObject effectRoot,
            string fieldName,
            out string error)
        {
            if (effectRoot == null || effectRoot.transform.parent != transform)
            {
                error = $"{name}.{fieldName} must reference a direct child GameObject.";
                return false;
            }

            if (effectRoot.activeSelf)
            {
                error = $"{name}.{fieldName} must be inactive by default.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void SetEffectRootActive(GameObject effectRoot, bool active)
        {
            if (effectRoot.activeSelf != active)
            {
                effectRoot.SetActive(active);
            }
        }

        private void PrepareMoveAnimation()
        {
            animationPrepared = true;
            if (gameObject.activeInHierarchy)
            {
                PlayMoveAnimation();
            }
        }

        private void PlayMoveAnimation()
        {
            animator.Rebind();
            RequireAnimatorParameters();
            deathVariant = MonsterDeathVariant.Normal;
            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(DeathTrigger);
            animator.SetInteger(DeathVariantParameter, (int)deathVariant);
            animator.Play(MoveState, 0, 0f);
            animator.Update(0f);
        }

        private void TickMovingDown(float deltaTime, ContactFilter2D enemyBodyFilter)
        {
            var position = (Vector2)transform.position;
            if (position.y <= enemyApproachY)
            {
                position.y = enemyApproachY;
                transform.position = position;
                state = MonsterRuntimeState.ApproachingTarget;
                return;
            }

            TickBeforeApproach(deltaTime);
            var distance = config.MoveSpeed * deltaTime;
            var maximumDistance = position.y - enemyApproachY;
            var movement = Vector2.down * Mathf.Min(distance, maximumDistance);
            ApplyBlockedMovement(movement, enemyBodyFilter);
            if (transform.position.y <= enemyApproachY)
            {
                var clamped = transform.position;
                clamped.y = enemyApproachY;
                transform.position = clamped;
                state = MonsterRuntimeState.ApproachingTarget;
            }
        }

        private void TickApproaching(float deltaTime, ContactFilter2D enemyBodyFilter)
        {
            if (!army.TryGetNearestActiveSlot(transform.position, out var target) || !target.IsActive)
            {
                targetSlotIndex = -1;
                return;
            }

            targetSlotIndex = target.SlotIndex;
            var offset = target.WorldPosition - (Vector2)transform.position;
            var rangeSquared = config.AttackStartRange * config.AttackStartRange;
            if (offset.sqrMagnitude <= rangeSquared)
            {
                if (attackCooldownRemaining <= 0f)
                {
                    BeginAttack();
                }

                return;
            }

            var maxDistance = config.MoveSpeed * deltaTime;
            var movement = offset.normalized * Mathf.Min(maxDistance, offset.magnitude);
            var actualDistance = ApplyBlockedMovement(movement, enemyBodyFilter);
            state = actualDistance + 0.0001f < movement.magnitude
                ? MonsterRuntimeState.Blocked
                : MonsterRuntimeState.ApproachingTarget;
        }

        private float ApplyBlockedMovement(Vector2 movement, ContactFilter2D enemyBodyFilter)
        {
            var distance = movement.magnitude;
            if (distance <= 0f)
            {
                return 0f;
            }

            var direction = movement / distance;
            var allowedDistance = distance;
            var hitCount = bodyCollider.Cast(direction, enemyBodyFilter, blockingResults, distance);
            for (var index = 0; index < hitCount; index++)
            {
                var collider = blockingResults[index].collider;
                if (collider == null ||
                    !collider.TryGetComponent<BulletHitProxy>(out var proxy) ||
                    !proxy.TryGetTarget(out var target) ||
                    target.BulletTargetKind != BulletTargetKind.Enemy ||
                    target.RuntimeInstanceId == runtimeInstanceId ||
                    !target.CanReceiveBulletHit)
                {
                    continue;
                }

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, blockingResults[index].distance - blockingGap));
            }

            transform.position = (Vector2)transform.position + direction * allowedDistance;
            return allowedDistance;
        }

        private void BeginAttack()
        {
            state = MonsterRuntimeState.Attacking;
            attackSequenceId = attackSequenceId == int.MaxValue ? 1 : attackSequenceId + 1;
            pendingAttackSequenceId = -1;
            attackFrameRegistered = false;
            attackCooldownRemaining = config.AttackCooldown;
            animator.SetTrigger(AttackTrigger);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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

        private void RequireAnimatorParameters()
        {
            if (!HasAnimatorParameter(animator, AttackTrigger, AnimatorControllerParameterType.Trigger) ||
                !HasAnimatorParameter(animator, DeathTrigger, AnimatorControllerParameterType.Trigger) ||
                !HasAnimatorParameter(
                    animator,
                    DeathVariantParameter,
                    AnimatorControllerParameterType.Int))
            {
                throw new InvalidOperationException(
                    $"{name}.animator controller requires Attack/Death Triggers and an Int DeathVariant parameter.");
            }
        }

        protected virtual void OnRuntimeInitialized() { }

        protected virtual void TickBeforeApproach(float deltaTime) { }

        protected virtual void OnPrepareForPool() { }
    }
}
