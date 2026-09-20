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
            float leftBoundary,
            float rightBoundary,
            float bottomBoundary,
            float topBoundary,
            float spawnY,
            float enemyApproachY,
            float despawnY)
        {
            Width = width;
            Height = height;
            LeftBoundary = leftBoundary;
            RightBoundary = rightBoundary;
            BottomBoundary = bottomBoundary;
            TopBoundary = topBoundary;
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
        public float SpawnY { get; }
        public float EnemyApproachY { get; }
        public float DespawnY { get; }
    }
}
