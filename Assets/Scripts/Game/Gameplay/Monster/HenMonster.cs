using Game.Contracts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Game.Gameplay
{
    [MovedFrom(true, sourceNamespace: "Game.Gameplay", sourceAssembly: "Assembly-CSharp", sourceClassName: "EliteMonster")]
    public sealed class HenMonster : MonsterBase
    {
        [SerializeField] private Collider2D attackCollider;

        public override EnemyType EnemyType => EnemyType.Hen;
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
