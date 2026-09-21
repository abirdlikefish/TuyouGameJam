using UnityEngine;

namespace Game.Contracts
{
    public interface ISpawnManager
    {
        void StartRun(
            LevelConfigSnapshot levelConfig,
            RoadLayoutSnapshot roadLayout,
            int levelRunId);
        void Tick(int levelRunId, float elapsedTime);
        bool AreAllEnemySpawnsDispatched(int levelRunId);
        void StopRun(int levelRunId);
    }

    public readonly struct EnemySpawnRequest
    {
        public EnemySpawnRequest(
            int levelRunId,
            int spawnEntryIndex,
            int configId,
            float spawnPosition,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            SpawnEntryIndex = spawnEntryIndex;
            ConfigId = configId;
            SpawnPosition = spawnPosition;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int SpawnEntryIndex { get; }
        public int ConfigId { get; }
        public float SpawnPosition { get; }
        public Vector2 WorldPosition { get; }
    }

    public readonly struct GateSpawnRequest
    {
        public GateSpawnRequest(
            int levelRunId,
            int spawnEntryIndex,
            GateType gateType,
            int initialValue,
            ElementType elementType,
            int maxHp,
            float elementDurationSecondsPerDamage,
            float spawnPosition,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            SpawnEntryIndex = spawnEntryIndex;
            GateType = gateType;
            InitialValue = initialValue;
            ElementType = elementType;
            MaxHp = maxHp;
            ElementDurationSecondsPerDamage = elementDurationSecondsPerDamage;
            SpawnPosition = spawnPosition;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int SpawnEntryIndex { get; }
        public GateType GateType { get; }
        public int InitialValue { get; }
        public ElementType ElementType { get; }
        public int MaxHp { get; }
        public float ElementDurationSecondsPerDamage { get; }
        public float SpawnPosition { get; }
        public Vector2 WorldPosition { get; }
    }

    public readonly struct PropSpawnRequest
    {
        public PropSpawnRequest(
            int levelRunId,
            int spawnEntryIndex,
            int configId,
            float spawnPosition,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            SpawnEntryIndex = spawnEntryIndex;
            ConfigId = configId;
            SpawnPosition = spawnPosition;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int SpawnEntryIndex { get; }
        public int ConfigId { get; }
        public float SpawnPosition { get; }
        public Vector2 WorldPosition { get; }
    }

    public readonly struct BasketballSpawnRequest
    {
        public BasketballSpawnRequest(
            int levelRunId,
            int sourceEnemyRuntimeInstanceId,
            int configId,
            Vector2 worldPosition)
        {
            LevelRunId = levelRunId;
            SourceEnemyRuntimeInstanceId = sourceEnemyRuntimeInstanceId;
            ConfigId = configId;
            WorldPosition = worldPosition;
        }

        public int LevelRunId { get; }
        public int SourceEnemyRuntimeInstanceId { get; }
        public int ConfigId { get; }
        public Vector2 WorldPosition { get; }
    }
}
