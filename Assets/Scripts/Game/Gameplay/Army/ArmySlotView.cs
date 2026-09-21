using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    internal enum ArmyAnimationState
    {
        Idle,
        Attack,
        MoveLeft,
        MoveRight,
        Victory,
        Death
    }

    internal enum ArmySlotLifeState
    {
        Empty,
        Alive,
        Dying
    }

    public sealed class ArmySlotView : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");
        private static readonly int MoveLeftState = Animator.StringToHash("Base Layer.MoveLeft");
        private static readonly int MoveRightState = Animator.StringToHash("Base Layer.MoveRight");
        private static readonly int VictoryState = Animator.StringToHash("Base Layer.Victory");
        private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");

        [SerializeField] private GameObject soldierVisual;
        [SerializeField] private Animator soldierAnimator;
        [SerializeField] private ArmySlotAnimationEventProxy animationEventProxy;
        [SerializeField] private Collider2D slotCollider;
        [SerializeField] private ArmySlotHitProxy slotHitProxy;
        [SerializeField] private Transform firePoint;

        private int slotIndex = -1;
        private int representedCount;
        private int currentHp;
        private int maxHp;
        private float fireCooldownRemaining;
        private ArmyAnimationState desiredAnimationState = ArmyAnimationState.Idle;
        private ArmyAnimationState postDeathAnimationState = ArmyAnimationState.Idle;
        private ArmySlotLifeState lifeState = ArmySlotLifeState.Empty;
        private AnimatorOverrideController pendingAnimatorController;
        private bool animationStateInitialized;

        public int SlotIndex => slotIndex;
        public int RepresentedCount => representedCount;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public bool IsActive => lifeState == ArmySlotLifeState.Alive;
        public bool IsDeathAnimating => lifeState == ArmySlotLifeState.Dying;
        public Vector2 WorldPosition => transform.position;
        public Vector2 FirePosition => firePoint != null ? firePoint.position : transform.position;
        public Collider2D SlotCollider => slotCollider;
        public ArmySlotHitProxy SlotHitProxy => slotHitProxy;
        public float FireCooldownRemaining => fireCooldownRemaining;

        public bool TryValidate(out string error)
        {
            if (soldierVisual == null)
            {
                error = $"{name}.soldierVisual is not assigned.";
                return false;
            }

            if (soldierAnimator == null || soldierAnimator.runtimeAnimatorController == null)
            {
                error = $"{name}.soldierAnimator and its controller are required.";
                return false;
            }

            if (soldierAnimator.gameObject != soldierVisual)
            {
                error = $"{name}.soldierAnimator must be on the soldierVisual GameObject.";
                return false;
            }

            if (animationEventProxy == null || animationEventProxy.gameObject != soldierVisual)
            {
                error = $"{name}.animationEventProxy must be assigned on soldierVisual.";
                return false;
            }

            if (animationEventProxy.Target != this)
            {
                error = $"{name}.animationEventProxy must explicitly reference this ArmySlotView.";
                return false;
            }

            if (!soldierVisual.TryGetComponent<SpriteRenderer>(out _))
            {
                error = $"{name}.soldierVisual requires a same-node SpriteRenderer.";
                return false;
            }

            if (slotCollider == null)
            {
                error = $"{name}.slotCollider is not assigned.";
                return false;
            }

            if (slotHitProxy == null)
            {
                error = $"{name}.slotHitProxy is not assigned.";
                return false;
            }

            if (firePoint == null)
            {
                error = $"{name}.firePoint is not assigned.";
                return false;
            }

            if (slotHitProxy.gameObject != slotCollider.gameObject)
            {
                error = $"{name}.slotHitProxy must be on the slotCollider GameObject.";
                return false;
            }

            if (slotHitProxy.Target != this)
            {
                error = $"{name}.slotHitProxy must explicitly reference this ArmySlotView.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal void Initialize(int armyId, int index)
        {
            slotIndex = index;
            slotHitProxy.Initialize(armyId, index);
            // Army 玩法周期使用 unscaledDeltaTime，Animator 必须使用同一时间域才不会漂移。
            soldierAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            SetState(0, 0, 0, 0f);
        }

        internal void SetState(int count, int hp, int slotMaxHp, float cooldown)
        {
            if (lifeState == ArmySlotLifeState.Dying)
            {
                throw new System.InvalidOperationException(
                    $"{name} cannot change represented soldiers while its death animation is playing.");
            }

            representedCount = Mathf.Max(0, count);
            currentHp = Mathf.Max(0, hp);
            maxHp = Mathf.Max(0, slotMaxHp);
            fireCooldownRemaining = Mathf.Max(0f, cooldown);

            var active = representedCount > 0;
            lifeState = active ? ArmySlotLifeState.Alive : ArmySlotLifeState.Empty;
            var wasActive = soldierVisual.activeSelf;
            soldierVisual.SetActive(active);
            slotCollider.enabled = active;
            if (active && (!wasActive || !animationStateInitialized))
            {
                animationStateInitialized = false;
                PlayDesiredAnimation(0f);
            }
        }

        internal void ApplyAnimatorController(
            AnimatorOverrideController controller,
            ArmyAnimationState animationState,
            float normalizedTime)
        {
            if (controller == null)
            {
                throw new System.ArgumentNullException(nameof(controller));
            }

            if (lifeState == ArmySlotLifeState.Dying)
            {
                pendingAnimatorController = controller;
                postDeathAnimationState = animationState;
                return;
            }

            desiredAnimationState = animationState;
            soldierAnimator.runtimeAnimatorController = controller;
            soldierAnimator.Rebind();
            animationStateInitialized = false;
            PlayDesiredAnimation(normalizedTime);
        }

        internal void SetAnimationState(ArmyAnimationState animationState, float normalizedTime)
        {
            if (lifeState == ArmySlotLifeState.Dying && animationState != ArmyAnimationState.Death)
            {
                postDeathAnimationState = animationState;
                return;
            }

            if (desiredAnimationState == animationState && animationStateInitialized)
            {
                return;
            }

            desiredAnimationState = animationState;
            animationStateInitialized = false;
            PlayDesiredAnimation(normalizedTime);
        }

        internal void BeginDeathAnimation()
        {
            if (lifeState != ArmySlotLifeState.Alive)
            {
                return;
            }

            representedCount = 0;
            currentHp = 0;
            maxHp = 0;
            fireCooldownRemaining = 0f;
            lifeState = ArmySlotLifeState.Dying;
            postDeathAnimationState = desiredAnimationState == ArmyAnimationState.Death
                ? ArmyAnimationState.Idle
                : desiredAnimationState;
            desiredAnimationState = ArmyAnimationState.Death;
            animationStateInitialized = false;
            soldierVisual.SetActive(true);
            slotCollider.enabled = false;
            PlayDesiredAnimation(0f);
        }

        internal void HandleDeathAnimationFinished()
        {
            if (lifeState != ArmySlotLifeState.Dying)
            {
                return;
            }

            lifeState = ArmySlotLifeState.Empty;
            desiredAnimationState = postDeathAnimationState;
            animationStateInitialized = false;
            if (pendingAnimatorController != null)
            {
                soldierAnimator.runtimeAnimatorController = pendingAnimatorController;
                soldierAnimator.Rebind();
                pendingAnimatorController = null;
            }

            soldierVisual.SetActive(false);
            slotCollider.enabled = false;
        }

        internal void SetFireCooldown(float value)
        {
            fireCooldownRemaining = Mathf.Max(0f, value);
        }

        internal ArmySlotSnapshot CreateSnapshot()
        {
            return new ArmySlotSnapshot(
                slotIndex,
                representedCount,
                currentHp,
                maxHp,
                IsActive);
        }

        internal void PrepareForRunStop()
        {
            representedCount = 0;
            currentHp = 0;
            maxHp = 0;
            fireCooldownRemaining = 0f;
            desiredAnimationState = ArmyAnimationState.Idle;
            postDeathAnimationState = ArmyAnimationState.Idle;
            lifeState = ArmySlotLifeState.Empty;
            pendingAnimatorController = null;
            animationStateInitialized = false;
            if (soldierVisual != null)
            {
                soldierVisual.SetActive(false);
            }

            if (slotCollider != null)
            {
                slotCollider.enabled = false;
            }
        }

        private void PlayDesiredAnimation(float normalizedTime)
        {
            if (!soldierVisual.activeInHierarchy || !soldierAnimator.isActiveAndEnabled)
            {
                animationStateInitialized = false;
                return;
            }

            if (animationStateInitialized)
            {
                return;
            }

            soldierAnimator.Play(
                GetStateHash(desiredAnimationState),
                0,
                Mathf.Repeat(normalizedTime, 1f));
            animationStateInitialized = true;
        }

        private static int GetStateHash(ArmyAnimationState animationState)
        {
            switch (animationState)
            {
                case ArmyAnimationState.Idle:
                    return IdleState;
                case ArmyAnimationState.Attack:
                    return AttackState;
                case ArmyAnimationState.MoveLeft:
                    return MoveLeftState;
                case ArmyAnimationState.MoveRight:
                    return MoveRightState;
                case ArmyAnimationState.Victory:
                    return VictoryState;
                case ArmyAnimationState.Death:
                    return DeathState;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(animationState));
            }
        }
    }
}
