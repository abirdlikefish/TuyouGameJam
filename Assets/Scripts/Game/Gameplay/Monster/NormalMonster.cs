using Game.Contracts;

namespace Game.Gameplay
{
    public sealed class NormalMonster : MonsterBase
    {
        public override EnemyType EnemyType => EnemyType.Normal;
        public override AttackType AttackType => AttackType.SingleTarget;
    }
}
