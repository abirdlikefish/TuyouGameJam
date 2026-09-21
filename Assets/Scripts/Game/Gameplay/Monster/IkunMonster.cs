using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class IkunMonster : MonsterBase
    {
        [SerializeField] private Collider2D attackCollider;
        [SerializeField] private Transform basketballSpawnPoint;
        [SerializeField, Min(0.01f)] private float basketballSpawnInterval = 2f;

        private float basketballSpawnRemaining;
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

            if (!HasAnimatorTrigger(RangedAttackTrigger))
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

            rangedAttackSequenceId = rangedAttackSequenceId == int.MaxValue
                ? 1
                : rangedAttackSequenceId + 1;
            pendingBasketballSequenceId = -1;
            basketballReleaseFrameRegistered = false;
            return true;
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
            rangedAttackSequenceId = 0;
            pendingBasketballSequenceId = -1;
            pendingBasketballWorldPosition = default(Vector2);
            basketballReleaseFrameRegistered = false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
