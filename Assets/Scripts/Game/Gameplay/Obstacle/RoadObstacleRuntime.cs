using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    internal interface IRoadObstacleRuntime
    {
        int LevelRunId { get; }
        int RuntimeInstanceId { get; }
        int SpawnEntryIndex { get; }
        int? ConfigId { get; }
        ObstacleKind Kind { get; }
        ObstacleState State { get; }
        bool IsRuntimeActive { get; }
        bool CanResolveContact { get; }
        Collider2D BodyCollider { get; }
        Vector2 WorldPosition { get; }

        void TickMovement(float deltaTime);
        void ResolveContact(int armyId, IReadOnlyList<int> slotIndices);
        void ExitRoad();
        void PrepareForPool();
    }
}
