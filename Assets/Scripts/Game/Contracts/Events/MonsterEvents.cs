using UnityEngine;

namespace Game.Contracts
{
    public readonly struct MonsterSpawned
    {
        public MonsterSpawned(
            int levelRunId,
            int runtimeInstanceId,
            int spawnEntryIndex,
            int configId,
            EnemyType enemyType,
            float spawnPosition,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            SpawnEntryIndex = spawnEntryIndex;
            ConfigId = configId;
            EnemyType = enemyType;
            SpawnPosition = spawnPosition;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public int SpawnEntryIndex { get; }
        public int ConfigId { get; }
        public EnemyType EnemyType { get; }
        public float SpawnPosition { get; }
        public Vector2 WorldPosition { get; }
    }

    public readonly struct MonsterDamaged
    {
        public MonsterDamaged(
            int levelRunId,
            int runtimeInstanceId,
            BulletDamageContext damageContext,
            int remainingHp,
            bool isFatal)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            DamageContext = damageContext;
            RemainingHp = remainingHp;
            IsFatal = isFatal;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public BulletDamageContext DamageContext { get; }
        public int RemainingHp { get; }
        public bool IsFatal { get; }
    }

    public readonly struct MonsterAttackLanded
    {
        public MonsterAttackLanded(
            int levelRunId,
            int runtimeInstanceId,
            AttackType attackType,
            int slotIndex,
            int attackPower)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            AttackType = attackType;
            SlotIndex = slotIndex;
            AttackPower = attackPower;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public AttackType AttackType { get; }
        public int SlotIndex { get; }
        public int AttackPower { get; }
    }

    public readonly struct MonsterKilled
    {
        public MonsterKilled(
            int levelRunId,
            int runtimeInstanceId,
            BulletDamageContext damageContext)
        {
            LevelRunId = levelRunId;
            RuntimeInstanceId = runtimeInstanceId;
            DamageContext = damageContext;
        }

        public int LevelRunId { get; }
        public int RuntimeInstanceId { get; }
        public BulletDamageContext DamageContext { get; }
    }
}
