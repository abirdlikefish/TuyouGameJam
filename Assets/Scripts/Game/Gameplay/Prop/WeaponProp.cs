using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class WeaponProp : BreakablePropBase
    {
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
            if (request.ConfigId != propConfig.Id)
            {
                throw new ArgumentException("Prop config ID does not match its spawn request.");
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
        }

        protected override void OnBroken(BulletDamageContext damage)
        {
            Army.ApplyWeaponPickup(config.WeaponId, RuntimeInstanceId);
            EventBus.Publish(
                new PropBroken(
                    LevelRunId,
                    RuntimeInstanceId,
                    config.WeaponId,
                    damage,
                    PropContactState.Succeeded,
                    true));
        }

        protected override string BuildDebugText()
        {
            return $"Weapon {config.WeaponId}  HP {CurrentHp}/{MaxHp}";
        }

        protected override void OnPrepareForPool()
        {
            config = default(PropConfigSnapshot);
        }
    }
}
