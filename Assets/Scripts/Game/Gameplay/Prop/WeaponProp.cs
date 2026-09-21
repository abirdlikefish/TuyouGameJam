using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class WeaponProp : BreakablePropBase
    {
        private static readonly int WeaponIdParameter = Animator.StringToHash("WeaponId");
        private static readonly int Weapon000LoopState =
            Animator.StringToHash("Base Layer.Weapon_000_Loop");
        private static readonly int Weapon001LoopState =
            Animator.StringToHash("Base Layer.Weapon_001_Loop");
        private static readonly int Weapon002LoopState =
            Animator.StringToHash("Base Layer.Weapon_002_Loop");

        private PropConfigSnapshot config;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (gameObject.activeInHierarchy &&
                !HasAnimatorParameter(WeaponIdParameter, AnimatorControllerParameterType.Int))
            {
                error = $"{name}.animator controller requires an Int parameter named WeaponId.";
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
            if (request.ConfigId != propConfig.Id || propConfig.PropType != PropType.WeaponBox)
            {
                throw new ArgumentException("WeaponProp requires a matching WeaponBox config.");
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
            PrepareAnimation(GetAnimationState(config.WeaponId));
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

        protected override void ConfigureAnimatorForPlayback(Animator targetAnimator)
        {
            RequireAnimatorParameter();
            targetAnimator.SetInteger(WeaponIdParameter, config.WeaponId);
        }

        protected override void ResetAnimatorForPool(Animator targetAnimator)
        {
            targetAnimator.SetInteger(WeaponIdParameter, 0);
        }

        private static int GetAnimationState(int weaponId)
        {
            switch (weaponId)
            {
                case 0:
                    return Weapon000LoopState;
                case 1:
                    return Weapon001LoopState;
                case 2:
                    return Weapon002LoopState;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(weaponId),
                        weaponId,
                        "Only WeaponId 0 through 2 have weapon prop animation states.");
            }
        }

        private void RequireAnimatorParameter()
        {
            if (!HasAnimatorParameter(WeaponIdParameter, AnimatorControllerParameterType.Int))
            {
                throw new InvalidOperationException(
                    $"{name}.animator controller requires an Int parameter named WeaponId.");
            }
        }
    }
}
