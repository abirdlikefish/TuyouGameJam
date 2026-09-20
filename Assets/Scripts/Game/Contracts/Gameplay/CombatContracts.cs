using UnityEngine;

namespace Game.Contracts
{
    public interface IDamageable
    {
        void TakeDamage(int damage);
        bool IsAlive { get; }
    }

    public interface IBulletHittable
    {
        bool CanReceiveBulletHit { get; }
        void ReceiveBulletHit(BulletDamageContext damage);
    }

    public readonly struct BulletDamageContext
    {
        public BulletDamageContext(
            int bulletInstanceId,
            int bulletId,
            int weaponId,
            ElementMask activeElements,
            int damage,
            Vector2 hitPosition,
            Vector2 hitDirection)
        {
            BulletInstanceId = bulletInstanceId;
            BulletId = bulletId;
            WeaponId = weaponId;
            ActiveElements = activeElements;
            Damage = damage;
            HitPosition = hitPosition;
            HitDirection = hitDirection;
        }

        public int BulletInstanceId { get; }
        public int BulletId { get; }
        public int WeaponId { get; }
        public ElementMask ActiveElements { get; }
        public int Damage { get; }
        public Vector2 HitPosition { get; }
        public Vector2 HitDirection { get; }
    }
}
