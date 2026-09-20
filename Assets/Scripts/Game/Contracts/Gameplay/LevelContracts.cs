using UnityEngine;

namespace Game.Contracts
{
    public interface ILevelRuntime
    {
        LevelRunState GetRunState();
        int GetLevelRunId();
        float GetElapsedTime();
        RoadLayoutSnapshot GetRoadLayout();
    }

    public readonly struct RoadLayoutSnapshot
    {
        public RoadLayoutSnapshot(
            float width,
            float height,
            Vector2 armySpawnPosition,
            float spawnY,
            float enemyApproachY,
            float despawnY)
        {
            Width = width;
            Height = height;
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            LeftBoundary = -halfWidth;
            RightBoundary = halfWidth;
            BottomBoundary = -halfHeight;
            TopBoundary = halfHeight;
            ArmySpawnPosition = armySpawnPosition;
            SpawnY = spawnY;
            EnemyApproachY = enemyApproachY;
            DespawnY = despawnY;
        }

        public float Width { get; }
        public float Height { get; }
        public float LeftBoundary { get; }
        public float RightBoundary { get; }
        public float BottomBoundary { get; }
        public float TopBoundary { get; }
        public Vector2 ArmySpawnPosition { get; }
        public float SpawnY { get; }
        public float EnemyApproachY { get; }
        public float DespawnY { get; }
    }
}
