using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class Bullet : MonoBehaviour
    {
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SpriteRenderer visual;

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

            error = string.Empty;
            return true;
        }

        internal void InitializeRuntime(
            BulletSpawnRequest request,
            BulletConfigSnapshot config,
            int runtimeInstanceId,
            Transform parent)
        {
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
            transform.rotation = Quaternion.identity;
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
    }
}
