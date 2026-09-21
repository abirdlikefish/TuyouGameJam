using System;
using System.Collections.Generic;

namespace Game.Contracts
{
    public sealed class LevelConfigSnapshot
    {
        public LevelConfigSnapshot(
            int levelId,
            string displayName,
            IReadOnlyList<int> unlockedLevelIds,
            float spawnY,
            float enemyApproachY,
            float despawnY,
            IReadOnlyList<EnemySpawnEntrySnapshot> enemySpawns,
            IReadOnlyList<GateSpawnEntrySnapshot> gateSpawns,
            IReadOnlyList<PropSpawnEntrySnapshot> propSpawns,
            int ikunBasketballConfigId,
            float elementDurationSecondsPerDamage)
        {
            LevelId = levelId;
            DisplayName = displayName;
            UnlockedLevelIds = Copy(unlockedLevelIds, nameof(unlockedLevelIds));
            SpawnY = spawnY;
            EnemyApproachY = enemyApproachY;
            DespawnY = despawnY;
            EnemySpawns = Copy(enemySpawns, nameof(enemySpawns));
            GateSpawns = Copy(gateSpawns, nameof(gateSpawns));
            PropSpawns = Copy(propSpawns, nameof(propSpawns));
            IkunBasketballConfigId = ikunBasketballConfigId;
            ElementDurationSecondsPerDamage = elementDurationSecondsPerDamage;
        }

        public int LevelId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<int> UnlockedLevelIds { get; }
        public float SpawnY { get; }
        public float EnemyApproachY { get; }
        public float DespawnY { get; }
        public IReadOnlyList<EnemySpawnEntrySnapshot> EnemySpawns { get; }
        public IReadOnlyList<GateSpawnEntrySnapshot> GateSpawns { get; }
        public IReadOnlyList<PropSpawnEntrySnapshot> PropSpawns { get; }
        public int IkunBasketballConfigId { get; }
        public float ElementDurationSecondsPerDamage { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public readonly struct EnemySpawnEntrySnapshot
    {
        public EnemySpawnEntrySnapshot(float spawnTime, float spawnPosition, int configId)
        {
            SpawnTime = spawnTime;
            SpawnPosition = spawnPosition;
            ConfigId = configId;
        }

        public float SpawnTime { get; }
        public float SpawnPosition { get; }
        public int ConfigId { get; }
    }

    public readonly struct GateSpawnEntrySnapshot
    {
        public GateSpawnEntrySnapshot(
            float spawnTime,
            float spawnPosition,
            GateType gateType,
            int initialValue,
            ElementType elementType,
            int maxHp)
        {
            SpawnTime = spawnTime;
            SpawnPosition = spawnPosition;
            GateType = gateType;
            InitialValue = initialValue;
            ElementType = elementType;
            MaxHp = maxHp;
        }

        public float SpawnTime { get; }
        public float SpawnPosition { get; }
        public GateType GateType { get; }
        public int InitialValue { get; }
        public ElementType ElementType { get; }
        public int MaxHp { get; }
    }

    public readonly struct PropSpawnEntrySnapshot
    {
        public PropSpawnEntrySnapshot(float spawnTime, float spawnPosition, int configId)
        {
            SpawnTime = spawnTime;
            SpawnPosition = spawnPosition;
            ConfigId = configId;
        }

        public float SpawnTime { get; }
        public float SpawnPosition { get; }
        public int ConfigId { get; }
    }
}
