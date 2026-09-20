using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class EliteMonster : MonsterBase
    {
        [SerializeField] private Collider2D attackCollider;

        public override EnemyType EnemyType => EnemyType.Elite;
        public override AttackType AttackType => AttackType.Area;
        public override Collider2D AttackCollider => attackCollider;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (attackCollider == null)
            {
                error = $"{name}.attackCollider is not assigned.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
