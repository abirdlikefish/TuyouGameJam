using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class BasketballProp : BreakablePropBase
    {
        [SerializeField, Min(1)] private int maxHp = 10;
        [SerializeField, Min(1)] private int contactDamage = 10;
        [SerializeField, Min(0f)] private float moveSpeed = 1f;

        private int sourceEnemyRuntimeInstanceId = -1;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (maxHp <= 0 || contactDamage <= 0 || !IsFinite(moveSpeed) || moveSpeed < 0f)
            {
                error = $"{name} requires positive maxHp/contactDamage and finite non-negative moveSpeed.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal void InitializeRuntime(
            BasketballSpawnRequest request,
            int initializedRuntimeInstanceId,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (request.SourceEnemyRuntimeInstanceId < 0)
            {
                throw new ArgumentException("Basketball source enemy ID must be non-negative.");
            }

            sourceEnemyRuntimeInstanceId = request.SourceEnemyRuntimeInstanceId;
            InitializeBreakableRuntime(
                request.LevelRunId,
                initializedRuntimeInstanceId,
                -1,
                null,
                maxHp,
                contactDamage,
                moveSpeed,
                request.WorldPosition,
                armyController,
                initializedEventBus,
                onRecycleRequested,
                parent);
        }

        protected override void OnBroken(BulletDamageContext damage)
        {
            EventBus.Publish(
                new BasketballBroken(
                    LevelRunId,
                    RuntimeInstanceId,
                    sourceEnemyRuntimeInstanceId,
                    damage));
        }

        protected override string BuildDebugText()
        {
            return $"Basketball  HP {CurrentHp}/{MaxHp}";
        }

        protected override void OnPrepareForPool()
        {
            sourceEnemyRuntimeInstanceId = -1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
