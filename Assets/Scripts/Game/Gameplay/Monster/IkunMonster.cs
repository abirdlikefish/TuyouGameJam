using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class IkunMonster : MonsterBase
    {
        [SerializeField] private Collider2D attackCollider;
        [SerializeField] private Transform basketballSpawnPoint;
        [SerializeField, Min(0.01f)] private float basketballSpawnInterval = 2f;

        [Header("追踪线前水平游走")]
        [SerializeField, Min(0.01f)] private float maxHorizontalSpeed = 0.75f;
        [SerializeField, Min(0.01f)] private float horizontalAcceleration = 2.5f;
        [SerializeField, Min(0.01f)] private float horizontalVelocityStep = 0.35f;
        [SerializeField, Min(0.01f)] private float horizontalVelocityChangeInterval = 0.5f;

        private float basketballSpawnRemaining;
        private float horizontalVelocity;
        private float targetHorizontalVelocity;
        private float horizontalVelocityChangeRemaining;
        private uint horizontalRandomState;
        private int rangedAttackSequenceId;
        private int pendingBasketballSequenceId;
        private Vector2 pendingBasketballWorldPosition;
        private bool basketballReleaseFrameRegistered;

        public override EnemyType EnemyType => EnemyType.Ikun;
        public override AttackType AttackType => AttackType.Area;
        public override Collider2D AttackCollider => attackCollider;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (attackCollider == null)
            {
                error = $"{name}.attackCollider is not assigned.";
                return false;
            }

            if (basketballSpawnPoint == null || basketballSpawnPoint.parent != transform)
            {
                error = $"{name}.basketballSpawnPoint must be assigned to a direct child.";
                return false;
            }

            if (!IsFinite(basketballSpawnInterval) || basketballSpawnInterval <= 0f)
            {
                error = $"{name}.basketballSpawnInterval must be finite and greater than zero.";
                return false;
            }

            if (!IsFinite(maxHorizontalSpeed) || maxHorizontalSpeed <= 0f ||
                !IsFinite(horizontalAcceleration) || horizontalAcceleration <= 0f ||
                !IsFinite(horizontalVelocityStep) || horizontalVelocityStep <= 0f ||
                horizontalVelocityStep > maxHorizontalSpeed ||
                !IsFinite(horizontalVelocityChangeInterval) ||
                horizontalVelocityChangeInterval <= 0f)
            {
                error = $"{name} requires finite positive horizontal wander values, and " +
                        "horizontalVelocityStep must not exceed maxHorizontalSpeed.";
                return false;
            }

            if (CanValidateAnimatorParameters && !HasAnimatorTrigger(RangedAttackTrigger))
            {
                error = $"{name}.animator controller requires a RangedAttack Trigger.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        protected override void OnRuntimeInitialized()
        {
            basketballSpawnRemaining = basketballSpawnInterval;
            horizontalRandomState = CreateHorizontalRandomSeed();
            ResetHorizontalWander(true);
            rangedAttackSequenceId = 0;
            pendingBasketballSequenceId = -1;
            pendingBasketballWorldPosition = default(Vector2);
            basketballReleaseFrameRegistered = false;
        }

        protected override bool TickBeforeApproach(float deltaTime)
        {
            basketballSpawnRemaining -= deltaTime;
            if (basketballSpawnRemaining > 0f)
            {
                return false;
            }

            // 不追赶掉帧期间错过的周期；每次远程攻击结束后重新等待完整间隔。
            basketballSpawnRemaining = basketballSpawnInterval;
            if (!TryBeginRangedAttack())
            {
                return false;
            }

            // 远程攻击期间完全停止横移；结束后的首个移动帧重新选择随机目标速度。
            ResetHorizontalWander(false);
            rangedAttackSequenceId = rangedAttackSequenceId == int.MaxValue
                ? 1
                : rangedAttackSequenceId + 1;
            pendingBasketballSequenceId = -1;
            basketballReleaseFrameRegistered = false;
            return true;
        }

        protected override Vector2 CalculateMovingDownMovement(
            float deltaTime,
            float downwardDistance)
        {
            if (deltaTime <= 0f)
            {
                return Vector2.down * downwardDistance;
            }

            horizontalVelocityChangeRemaining -= deltaTime;
            if (horizontalVelocityChangeRemaining <= 0f)
            {
                horizontalVelocityChangeRemaining = horizontalVelocityChangeInterval;
                var velocityDelta = NextHorizontalStepIsPositive()
                    ? horizontalVelocityStep
                    : -horizontalVelocityStep;
                targetHorizontalVelocity = Mathf.Clamp(
                    targetHorizontalVelocity + velocityDelta,
                    -maxHorizontalSpeed,
                    maxHorizontalSpeed);
            }

            var position = (Vector2)transform.position;
            if (position.x <= RoadLeftBoundary)
            {
                ForceHorizontalDirection(1f);
            }
            else if (position.x >= RoadRightBoundary)
            {
                ForceHorizontalDirection(-1f);
            }

            horizontalVelocity = Mathf.MoveTowards(
                horizontalVelocity,
                targetHorizontalVelocity,
                horizontalAcceleration * deltaTime);

            var nextX = position.x + horizontalVelocity * deltaTime;
            if (nextX < RoadLeftBoundary)
            {
                nextX = RoadLeftBoundary;
                ForceHorizontalDirection(1f);
            }
            else if (nextX > RoadRightBoundary)
            {
                nextX = RoadRightBoundary;
                ForceHorizontalDirection(-1f);
            }

            return new Vector2(nextX - position.x, -downwardDistance);
        }

        public void OnBasketballReleaseFrame()
        {
            if (!IsRangedAttacking || basketballReleaseFrameRegistered)
            {
                return;
            }

            basketballReleaseFrameRegistered = true;
            pendingBasketballSequenceId = rangedAttackSequenceId;
            pendingBasketballWorldPosition = basketballSpawnPoint.position;
        }

        public void OnRangedAttackAnimationFinished()
        {
            if (!TryFinishRangedAttack())
            {
                return;
            }

            basketballReleaseFrameRegistered = false;
            ResetHorizontalWander(true);
        }

        internal bool TryConsumeBasketballSpawnRequest(out Vector2 worldPosition)
        {
            if (IsAlive && pendingBasketballSequenceId >= 0 &&
                pendingBasketballSequenceId == rangedAttackSequenceId)
            {
                worldPosition = pendingBasketballWorldPosition;
                pendingBasketballSequenceId = -1;
                return true;
            }

            worldPosition = default(Vector2);
            return false;
        }

        protected override void OnPrepareForPool()
        {
            basketballSpawnRemaining = 0f;
            horizontalVelocity = 0f;
            targetHorizontalVelocity = 0f;
            horizontalVelocityChangeRemaining = 0f;
            horizontalRandomState = 0u;
            rangedAttackSequenceId = 0;
            pendingBasketballSequenceId = -1;
            pendingBasketballWorldPosition = default(Vector2);
            basketballReleaseFrameRegistered = false;
        }

        private void ResetHorizontalWander(bool restartImmediately)
        {
            horizontalVelocity = 0f;
            targetHorizontalVelocity = 0f;
            horizontalVelocityChangeRemaining = restartImmediately
                ? 0f
                : horizontalVelocityChangeInterval;
        }

        private void ForceHorizontalDirection(float direction)
        {
            if (horizontalVelocity * direction < 0f)
            {
                horizontalVelocity = 0f;
            }

            var inwardSpeed = Mathf.Max(horizontalVelocityStep, Mathf.Abs(targetHorizontalVelocity));
            targetHorizontalVelocity = direction * Mathf.Min(maxHorizontalSpeed, inwardSpeed);
        }

        private uint CreateHorizontalRandomSeed()
        {
            var seed = unchecked(
                (uint)LevelRunId * 73856093u ^
                (uint)(RuntimeInstanceId + 1) * 19349663u ^
                (uint)(SpawnEntryIndex + 1) * 83492791u);
            return seed == 0u ? 0x6D2B79F5u : seed;
        }

        private bool NextHorizontalStepIsPositive()
        {
            var state = horizontalRandomState;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            horizontalRandomState = state;
            return (state & 1u) != 0u;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
