using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class IkunMonster : MonsterBase
    {
        [SerializeField] private Collider2D attackCollider;
        [SerializeField] private Transform basketballSpawnPoint;
        [SerializeField, Min(0.01f)] private float basketballSpawnInterval = 2f;

        private Action<IkunMonster, Vector2> basketballSpawnCallback;
        private float basketballSpawnRemaining;

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

            error = string.Empty;
            return true;
        }

        internal void ConfigureBasketballSpawner(Action<IkunMonster, Vector2> spawnCallback)
        {
            basketballSpawnCallback = spawnCallback ??
                                      throw new ArgumentNullException(nameof(spawnCallback));
        }

        protected override void OnRuntimeInitialized()
        {
            if (basketballSpawnCallback == null)
            {
                throw new InvalidOperationException("Ikun basketball spawner is not configured.");
            }

            basketballSpawnRemaining = basketballSpawnInterval;
        }

        protected override void TickBeforeApproach(float deltaTime)
        {
            basketballSpawnRemaining -= deltaTime;
            if (basketballSpawnRemaining > 0f)
            {
                return;
            }

            basketballSpawnCallback(this, basketballSpawnPoint.position);
            basketballSpawnRemaining += basketballSpawnInterval;
        }

        protected override void OnPrepareForPool()
        {
            basketballSpawnCallback = null;
            basketballSpawnRemaining = 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
