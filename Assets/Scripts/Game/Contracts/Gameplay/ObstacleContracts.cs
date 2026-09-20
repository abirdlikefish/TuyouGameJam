using System.Collections.Generic;
using UnityEngine;

namespace Game.Contracts
{
    public interface IObstacleRegistry
    {
        IReadOnlyList<RoadObjectSnapshot> GetActiveObjects();
        IReadOnlyList<RoadObjectSnapshot> GetActiveObjects(ObstacleKind kind);
        bool TryGetObject(int runtimeInstanceId, out RoadObjectSnapshot snapshot);
    }

    public readonly struct RoadObjectSnapshot
    {
        public RoadObjectSnapshot(
            int runtimeInstanceId,
            int spawnEntryIndex,
            int? configId,
            ObstacleKind kind,
            Vector2 worldPosition,
            bool isOnRoad,
            ObstacleState state)
        {
            RuntimeInstanceId = runtimeInstanceId;
            SpawnEntryIndex = spawnEntryIndex;
            ConfigId = configId;
            Kind = kind;
            WorldPosition = worldPosition;
            IsOnRoad = isOnRoad;
            State = state;
        }

        public int RuntimeInstanceId { get; }
        public int SpawnEntryIndex { get; }
        public int? ConfigId { get; }
        public ObstacleKind Kind { get; }
        public Vector2 WorldPosition { get; }
        public bool IsOnRoad { get; }
        public ObstacleState State { get; }
    }
}
