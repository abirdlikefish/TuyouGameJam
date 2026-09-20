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
        Victory
    }

    public sealed class ArmySlotView : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");
        private static readonly int MoveLeftState = Animator.StringToHash("Base Layer.MoveLeft");
        private static readonly int MoveRightState = Animator.StringToHash("Base Layer.MoveRight");
        private static readonly int VictoryState = Animator.StringToHash("Base Layer.Victory");

        [SerializeField] private GameObject soldierVisual;
        [SerializeField] private Animator soldierAnimator;
        [SerializeField] private Collider2D slotCollider;
        [SerializeField] private ArmySlotHitProxy slotHitProxy;
        [SerializeField] private Transform firePoint;

        private int slotIndex = -1;
        private int representedCount;
        private int currentHp;
        private int maxHp;
        private float fireCooldownRemaining;
        private ArmyAnimationState desiredAnimationState = ArmyAnimationState.Idle;
        private bool animationStateInitialized;

        public int SlotIndex => slotIndex;
        public int RepresentedCount => representedCount;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public bool IsActive => representedCount > 0;
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
            SetState(0, 0, 0, 0f);
        }

        internal void SetState(int count, int hp, int slotMaxHp, float cooldown)
        {
            representedCount = Mathf.Max(0, count);
            currentHp = Mathf.Max(0, hp);
            maxHp = Mathf.Max(0, slotMaxHp);
            fireCooldownRemaining = Mathf.Max(0f, cooldown);

            var wasActive = soldierVisual.activeSelf;
            var active = representedCount > 0;
            soldierVisual.SetActive(active);
            slotCollider.enabled = active;
            if (active && (!wasActive || !animationStateInitialized))
            {
                PlayDesiredAnimation(true);
            }
        }

        internal void ApplyAnimatorController(
            AnimatorOverrideController controller,
            ArmyAnimationState animationState)
        {
            if (controller == null)
            {
                throw new System.ArgumentNullException(nameof(controller));
            }

            desiredAnimationState = animationState;
            soldierAnimator.runtimeAnimatorController = controller;
            soldierAnimator.Rebind();
            animationStateInitialized = false;
            PlayDesiredAnimation(true);
        }

        internal void SetAnimationState(ArmyAnimationState animationState)
        {
            if (desiredAnimationState == animationState && animationStateInitialized)
            {
                return;
            }

            desiredAnimationState = animationState;
            animationStateInitialized = false;
            PlayDesiredAnimation(false);
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

        private void PlayDesiredAnimation(bool restart)
        {
            if (!soldierVisual.activeInHierarchy || !soldierAnimator.isActiveAndEnabled)
            {
                animationStateInitialized = false;
                return;
            }

            if (!restart && animationStateInitialized)
            {
                return;
            }

            soldierAnimator.Play(GetStateHash(desiredAnimationState), 0, 0f);
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
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(animationState));
            }
        }
    }
}
