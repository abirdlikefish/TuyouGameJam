using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class GooseCageProp : BreakablePropBase
    {
        private static readonly int GooseCageLoopState =
            Animator.StringToHash("Base Layer.GooseCage_Loop");

        private PropConfigSnapshot config;

        internal void InitializeRuntime(
            PropSpawnRequest request,
            PropConfigSnapshot propConfig,
            int initializedRuntimeInstanceId,
            IArmyController armyController,
            IEventBus initializedEventBus,
            Action<IRoadObstacleRuntime, ObstacleRecycleReason> onRecycleRequested,
            Transform parent)
        {
            if (request.ConfigId != propConfig.Id || propConfig.PropType != PropType.GooseCage ||
                propConfig.ArmyAddition <= 0)
            {
                throw new ArgumentException("GooseCageProp requires a matching GooseCage config.");
            }

            config = propConfig;
            InitializeBreakableRuntime(
                request.LevelRunId,
                initializedRuntimeInstanceId,
                request.SpawnEntryIndex,
                config.Id,
                config.MaxHp,
                config.ContactDamage,
                config.MoveSpeed,
                request.WorldPosition,
                armyController,
                initializedEventBus,
                onRecycleRequested,
                parent);
            PrepareAnimation(GooseCageLoopState);
        }

        protected override void OnBroken(BulletDamageContext damage)
        {
            var addition = Army.AddArmy(config.ArmyAddition);
            EventBus.Publish(
                new GooseCageBroken(
                    LevelRunId,
                    RuntimeInstanceId,
                    config.Id,
                    damage,
                    addition));
        }

        protected override void OnPrepareForPool()
        {
            config = default(PropConfigSnapshot);
        }
    }
}
