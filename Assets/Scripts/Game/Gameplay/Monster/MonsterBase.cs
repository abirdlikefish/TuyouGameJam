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
        private static readonly int MoveState = Animator.StringToHash("Base Layer.Move");

        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float blockingGap;

        private readonly RaycastHit2D[] blockingResults = new RaycastHit2D[32];

        private IArmyController army;
        private Action<MonsterBase, BulletDamageContext, int, bool> damagedCallback;
        private Action<MonsterBase, BulletDamageContext> deathCallback;
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
        internal Collider2D BodyCollider => bodyCollider;

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
                 !HasAnimatorParameter(animator, DeathTrigger, AnimatorControllerParameterType.Trigger)))
            {
                error = $"{name}.animator controller requires Attack and Death Trigger parameters.";
                return false;
            }

            if (!IsFinite(blockingGap) || blockingGap < 0f)
            {
                error = $"{name}.blockingGap must be finite and non-negative.";
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
            Action<MonsterBase, BulletDamageContext, int, bool> onDamaged,
            Action<MonsterBase, BulletDamageContext> onDeath,
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
            attackFrameRegistered = false;
            deathAnimationFinished = false;
            runtimeActive = true;

            transform.SetParent(parent, false);
            transform.position = request.WorldPosition;
            transform.rotation = Quaternion.identity;
            bodyCollider.enabled = true;
            if (AttackCollider != null)
            {
                AttackCollider.enabled = true;
            }

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
            if (!runtimeActive || state == MonsterRuntimeState.Dead)
            {
                return;
            }

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
            if (!IsAlive || damage.Damage <= 0)
            {
                return;
            }

            currentHp = (int)Math.Max(0L, (long)currentHp - damage.Damage);
            var fatal = currentHp == 0;
            damagedCallback(this, damage, currentHp, fatal);
            if (!fatal)
            {
                return;
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

            animator.SetTrigger(DeathTrigger);
            deathCallback(this, damage);
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
            if (animator.gameObject.activeInHierarchy)
            {
                animator.ResetTrigger(AttackTrigger);
                animator.ResetTrigger(DeathTrigger);
                animator.Rebind();
            }

            bodyCollider.enabled = false;
            if (AttackCollider != null)
            {
                AttackCollider.enabled = false;
            }

            army = null;
            damagedCallback = null;
            deathCallback = null;
            recycleCallback = null;
            levelRunId = 0;
            runtimeInstanceId = -1;
            spawnEntryIndex = -1;
            currentHp = 0;
            targetSlotIndex = -1;
            pendingAttackSequenceId = -1;
            gameObject.SetActive(false);
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
            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(DeathTrigger);
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
                !HasAnimatorParameter(animator, DeathTrigger, AnimatorControllerParameterType.Trigger))
            {
                throw new InvalidOperationException(
                    $"{name}.animator controller requires Attack and Death Trigger parameters.");
            }
        }
    }
}
