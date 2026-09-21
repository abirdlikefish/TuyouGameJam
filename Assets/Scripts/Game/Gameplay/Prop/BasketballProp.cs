using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class BasketballProp : BreakablePropBase
    {
        private static readonly int BasketballLoopState =
            Animator.StringToHash("Base Layer.Basketball_Loop");

        private PropConfigSnapshot config;
        private int sourceEnemyRuntimeInstanceId = -1;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal void InitializeRuntime(
            PropSpawnRequest request,
            PropConfigSnapshot propConfig,
            int initializedRuntimeInstanceId,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (request.ConfigId != propConfig.Id)
            {
                throw new ArgumentException("Prop config ID does not match its spawn request.");
            }

            InitializeRuntime(
                request.LevelRunId,
                request.SpawnEntryIndex,
                request.WorldPosition,
                -1,
                propConfig,
                initializedRuntimeInstanceId,
                armyController,
                initializedEventBus,
                onRecycleRequested,
                parent);
        }

        internal void InitializeRuntime(
            BasketballSpawnRequest request,
            PropConfigSnapshot propConfig,
            int initializedRuntimeInstanceId,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (request.SourceEnemyRuntimeInstanceId < 0 || request.ConfigId != propConfig.Id)
            {
                throw new ArgumentException("Basketball request source or config ID is invalid.");
            }

            InitializeRuntime(
                request.LevelRunId,
                -1,
                request.WorldPosition,
                request.SourceEnemyRuntimeInstanceId,
                propConfig,
                initializedRuntimeInstanceId,
                armyController,
                initializedEventBus,
                onRecycleRequested,
                parent);
        }

        private void InitializeRuntime(
            int initializedLevelRunId,
            int initializedSpawnEntryIndex,
            Vector2 worldPosition,
            int initializedSourceEnemyRuntimeInstanceId,
            PropConfigSnapshot propConfig,
            int initializedRuntimeInstanceId,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (propConfig.PropType != PropType.Basketball)
            {
                throw new ArgumentException("BasketballProp requires PropType.Basketball.");
            }

            config = propConfig;
            sourceEnemyRuntimeInstanceId = initializedSourceEnemyRuntimeInstanceId;
            InitializeBreakableRuntime(
                initializedLevelRunId,
                initializedRuntimeInstanceId,
                initializedSpawnEntryIndex,
                config.Id,
                config.MaxHp,
                config.ContactDamage,
                config.MoveSpeed,
                worldPosition,
                armyController,
                initializedEventBus,
                onRecycleRequested,
                parent);
            PrepareAnimation(BasketballLoopState);
        }

        protected override void OnBroken(BulletDamageContext damage)
        {
            EventBus.Publish(
                new BasketballBroken(
                    LevelRunId,
                    RuntimeInstanceId,
                    sourceEnemyRuntimeInstanceId,
                    config.Id,
                    damage));
        }

        protected override void OnPrepareForPool()
        {
            sourceEnemyRuntimeInstanceId = -1;
            config = default(PropConfigSnapshot);
        }
    }
}
