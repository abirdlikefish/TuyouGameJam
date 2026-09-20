using UnityEngine;

namespace Game.Contracts
{
    public readonly struct BulletSpawnRequest
    {
        public BulletSpawnRequest(
            int levelRunId,
            int sourceArmyId,
            int sourceSlotIndex,
            int bulletId,
            int weaponId,
            ElementMask activeElements,
            Vector2 worldPosition,
            Vector2 direction)
        {
            LevelRunId = levelRunId;
            SourceArmyId = sourceArmyId;
            SourceSlotIndex = sourceSlotIndex;
            BulletId = bulletId;
            WeaponId = weaponId;
            ActiveElements = activeElements;
            WorldPosition = worldPosition;
            Direction = direction;
        }

        public int LevelRunId { get; }
        public int SourceArmyId { get; }
        public int SourceSlotIndex { get; }
        public int BulletId { get; }
        public int WeaponId { get; }
        public ElementMask ActiveElements { get; }
        public Vector2 WorldPosition { get; }
        public Vector2 Direction { get; }
    }

    public interface IBulletManager
    {
        void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
        void Spawn(BulletSpawnRequest request);
        void TickMovementAndHits(int levelRunId, float bulletDeltaTime);
        void FlushPendingRecycles(int levelRunId);
        void StopRun(int levelRunId);
    }

    public interface IEnemyManager
    {
        void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
        void Spawn(EnemySpawnRequest request);
        void TickMovement(int levelRunId, float monsterDeltaTime);
        void ResolveAttacks(int levelRunId, float monsterDeltaTime);
        void FlushPendingRecycles(int levelRunId);
        int GetAliveEnemyCount();
        int GetActiveEnemyCount();
        void StopRun(int levelRunId);
    }

    public interface IObstacleManager : IObstacleRegistry
    {
        void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
        void Spawn(GateSpawnRequest request);
        void Spawn(PropSpawnRequest request);
        void TickMovement(int levelRunId, float gateDeltaTime);
        void ResolveContacts(int levelRunId);
        void FlushPendingRecycles(int levelRunId);
        void StopRun(int levelRunId);
    }
}
