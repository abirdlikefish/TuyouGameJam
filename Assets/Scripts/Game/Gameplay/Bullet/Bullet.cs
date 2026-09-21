using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class Bullet : MonoBehaviour
    {
        private static readonly int BulletIdParameter = Animator.StringToHash("BulletId");
        private static readonly int Bullet000LoopState = Animator.StringToHash("Base Layer.Bullet_000_Loop");
        private static readonly int Bullet001LoopState = Animator.StringToHash("Base Layer.Bullet_001_Loop");
        private static readonly int Bullet002LoopState = Animator.StringToHash("Base Layer.Bullet_002_Loop");
        private static readonly int Bullet003LoopState = Animator.StringToHash("Base Layer.Bullet_003_Loop");
        private static readonly int Bullet004LoopState = Animator.StringToHash("Base Layer.Bullet_004_Loop");
        private static readonly int Bullet005LoopState = Animator.StringToHash("Base Layer.Bullet_005_Loop");
        private static readonly int Bullet006LoopState = Animator.StringToHash("Base Layer.Bullet_006_Loop");
        private static readonly int Bullet007LoopState = Animator.StringToHash("Base Layer.Bullet_007_Loop");
        private static readonly int Bullet008LoopState = Animator.StringToHash("Base Layer.Bullet_008_Loop");
        private static readonly int Bullet009LoopState = Animator.StringToHash("Base Layer.Bullet_009_Loop");

        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Animator animator;

        private int levelRunId;
        private int bulletInstanceId;
        private int bulletId;
        private int weaponId;
        private int sourceArmyId;
        private int sourceSlotIndex;
        private int damage;
        private float moveSpeed;
        private ElementMask activeElements;
        private Vector2 direction;
        private Vector2 previousPosition;
        private bool active;
        private bool animationPrepared;
        private int preparedAnimationState;

        public int LevelRunId => levelRunId;
        public int BulletInstanceId => bulletInstanceId;
        public Collider2D BodyCollider => bodyCollider;
        public bool IsRuntimeActive => active;

        public bool TryValidate(out string error)
        {
            if (bodyCollider == null)
            {
                error = $"{name}.bodyCollider is not assigned.";
                return false;
            }

            if (visual == null)
            {
                error = $"{name}.visual is not assigned.";
                return false;
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                error = $"{name}.animator and its controller are required.";
                return false;
            }

            if (!animator.enabled)
            {
                error = $"{name}.animator must be enabled.";
                return false;
            }

            if (animator.gameObject.activeInHierarchy &&
                !HasAnimatorParameter(animator, BulletIdParameter, AnimatorControllerParameterType.Int))
            {
                error = $"{name}.animator controller requires an Int parameter named BulletId.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal void InitializeRuntime(
            BulletSpawnRequest request,
            BulletConfigSnapshot config,
            int runtimeInstanceId,
            Transform parent)
        {
            var animationState = GetAnimationState(config.Id);
            levelRunId = request.LevelRunId;
            bulletInstanceId = runtimeInstanceId;
            bulletId = config.Id;
            weaponId = request.WeaponId;
            sourceArmyId = request.SourceArmyId;
            sourceSlotIndex = request.SourceSlotIndex;
            damage = config.Damage;
            moveSpeed = config.MoveSpeed;
            activeElements = request.ActiveElements;
            direction = request.Direction.normalized;
            previousPosition = request.WorldPosition;
            active = true;

            transform.SetParent(parent, false);
            transform.position = request.WorldPosition;
            // 子弹美术默认朝上，根节点对齐飞行方向可同时旋转视觉和碰撞体。
            transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            PrepareAnimation(animationState);
        }

        private void OnEnable()
        {
            if (active && animationPrepared)
            {
                PlayPreparedAnimation();
            }
        }

        internal Vector2 GetDesiredPosition(float deltaTime)
        {
            return previousPosition + direction * (moveSpeed * deltaTime);
        }

        internal float GetTravelDistance(Vector2 desiredPosition)
        {
            return Vector2.Distance(previousPosition, desiredPosition);
        }

        internal Vector2 Direction => direction;

        internal void ApplyPosition(Vector2 position)
        {
            transform.position = position;
            previousPosition = position;
        }

        internal BulletDamageContext CreateDamageContext(Vector2 hitPosition)
        {
            return new BulletDamageContext(
                bulletInstanceId,
                bulletId,
                weaponId,
                activeElements,
                damage,
                hitPosition,
                direction);
        }

        internal void PrepareForPool()
        {
            active = false;
            animationPrepared = false;
            preparedAnimationState = 0;
            if (animator.gameObject.activeInHierarchy)
            {
                animator.Rebind();
                animator.SetInteger(BulletIdParameter, 0);
            }

            levelRunId = 0;
            bulletInstanceId = -1;
            bulletId = -1;
            weaponId = -1;
            sourceArmyId = -1;
            sourceSlotIndex = -1;
            damage = 0;
            moveSpeed = 0f;
            activeElements = ElementMask.None;
            direction = Vector2.zero;
            previousPosition = Vector2.zero;
            gameObject.SetActive(false);
        }

        private void PrepareAnimation(int animationState)
        {
            preparedAnimationState = animationState;
            animationPrepared = true;
            if (gameObject.activeInHierarchy)
            {
                PlayPreparedAnimation();
            }
        }

        private void PlayPreparedAnimation()
        {
            animator.Rebind();
            RequireAnimatorParameter();
            animator.SetInteger(BulletIdParameter, bulletId);
            animator.Play(preparedAnimationState, 0, 0f);
            animator.Update(0f);
        }

        private static int GetAnimationState(int initializedBulletId)
        {
            switch (initializedBulletId)
            {
                case 0:
                    return Bullet000LoopState;
                case 1:
                    return Bullet001LoopState;
                case 2:
                    return Bullet002LoopState;
                case 3:
                    return Bullet003LoopState;
                case 4:
                    return Bullet004LoopState;
                case 5:
                    return Bullet005LoopState;
                case 6:
                    return Bullet006LoopState;
                case 7:
                    return Bullet007LoopState;
                case 8:
                    return Bullet008LoopState;
                case 9:
                    return Bullet009LoopState;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(initializedBulletId),
                        initializedBulletId,
                        "Only BulletId 0 through 9 have bound animation states.");
            }
        }

        private static bool HasAnimatorParameter(
            Animator targetAnimator,
            int parameterNameHash,
            AnimatorControllerParameterType parameterType)
        {
            var parameters = targetAnimator.parameters;
            for (var index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].nameHash == parameterNameHash && parameters[index].type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private void RequireAnimatorParameter()
        {
            if (!HasAnimatorParameter(animator, BulletIdParameter, AnimatorControllerParameterType.Int))
            {
                throw new System.InvalidOperationException(
                    $"{name}.animator controller requires an Int parameter named BulletId.");
            }
        }
    }
}
