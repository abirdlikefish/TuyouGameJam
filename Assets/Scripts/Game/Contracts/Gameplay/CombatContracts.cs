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

    public readonly struct EnemyDamageContext
    {
        public EnemyDamageContext(
            BulletDamageContext originBullet,
            ElementComboKind comboKind,
            int directDamage,
            int effectDamage,
            Vector2 hitPosition,
            Vector2 hitDirection)
        {
            OriginBullet = originBullet;
            ComboKind = comboKind;
            DirectDamage = directDamage;
            EffectDamage = effectDamage;
            Damage = SaturatingAdd(directDamage, effectDamage);
            HitPosition = hitPosition;
            HitDirection = hitDirection;
        }

        public BulletDamageContext OriginBullet { get; }
        public ElementComboKind ComboKind { get; }
        public ElementMask ActiveElements => OriginBullet.ActiveElements;
        public int DirectDamage { get; }
        public int EffectDamage { get; }
        public int Damage { get; }
        public Vector2 HitPosition { get; }
        public Vector2 HitDirection { get; }

        public static EnemyDamageContext FromDirectBullet(BulletDamageContext damage)
        {
            return new EnemyDamageContext(
                damage,
                ElementComboKind.None,
                damage.Damage,
                0,
                damage.HitPosition,
                damage.HitDirection);
        }

        private static int SaturatingAdd(int left, int right)
        {
            var result = (long)left + right;
            if (result > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (result < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)result;
        }
    }
}
