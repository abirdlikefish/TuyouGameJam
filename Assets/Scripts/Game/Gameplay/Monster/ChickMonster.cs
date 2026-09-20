using Game.Contracts;
using UnityEngine.Scripting.APIUpdating;

namespace Game.Gameplay
{
    [MovedFrom(true, sourceNamespace: "Game.Gameplay", sourceAssembly: "Assembly-CSharp", sourceClassName: "NormalMonster")]
    public sealed class ChickMonster : MonsterBase
    {
        public override EnemyType EnemyType => EnemyType.Chick;
        public override AttackType AttackType => AttackType.SingleTarget;
    }
}
