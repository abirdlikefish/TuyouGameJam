using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SpawnManager : MonoBehaviour, ISpawnManager
    {
        private IEnemyManager enemyManager;
        private IObstacleManager obstacleManager;
        private LevelConfigSnapshot levelConfig;
        private RoadLayoutSnapshot roadLayout;
        private int levelRunId;
        private int enemySpawnCursor;
        private int gateSpawnCursor;
        private int propSpawnCursor;
        private bool initialized;
        private bool running;

        public void Initialize(IEnemyManager initializedEnemyManager, IObstacleManager initializedObstacleManager)
        {
            if (initializedEnemyManager == null)
            {
                throw new ArgumentNullException(nameof(initializedEnemyManager));
            }

            if (initializedObstacleManager == null)
            {
                throw new ArgumentNullException(nameof(initializedObstacleManager));
            }

            if (initialized)
            {
                if (ReferenceEquals(enemyManager, initializedEnemyManager) &&
                    ReferenceEquals(obstacleManager, initializedObstacleManager))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "SpawnManager cannot be reinitialized with different dependencies.");
            }

            enemyManager = initializedEnemyManager;
            obstacleManager = initializedObstacleManager;
            initialized = true;
        }

        public void StartRun(
            LevelConfigSnapshot startedLevelConfig,
            RoadLayoutSnapshot startedRoadLayout,
            int startedLevelRunId)
        {
            EnsureInitialized();
            if (running)
            {
                throw new InvalidOperationException("SpawnManager already has an active run.");
            }

            if (startedLevelConfig == null)
            {
                throw new ArgumentNullException(nameof(startedLevelConfig));
            }

            if (startedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startedLevelRunId));
            }

            ValidateRoadLayout(startedRoadLayout);

            levelConfig = startedLevelConfig;
            roadLayout = startedRoadLayout;
            levelRunId = startedLevelRunId;
            enemySpawnCursor = 0;
            gateSpawnCursor = 0;
            propSpawnCursor = 0;
            running = true;
        }

        public void Tick(int tickingLevelRunId, float elapsedTime)
        {
            if (!IsCurrentRun(tickingLevelRunId))
            {
                return;
            }

            if (!IsFinite(elapsedTime) || elapsedTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));
            }

            DispatchEnemySpawns(elapsedTime);
            DispatchGateSpawns(elapsedTime);
            DispatchPropSpawns(elapsedTime);
        }

        public bool AreAllEnemySpawnsDispatched(int queriedLevelRunId)
        {
            return IsCurrentRun(queriedLevelRunId) &&
                   enemySpawnCursor >= levelConfig.EnemySpawns.Count;
        }

        public void StopRun(int stoppedLevelRunId)
        {
            if (!IsCurrentRun(stoppedLevelRunId))
            {
                return;
            }

            running = false;
            levelRunId = 0;
            levelConfig = null;
            roadLayout = default(RoadLayoutSnapshot);
            enemySpawnCursor = 0;
            gateSpawnCursor = 0;
            propSpawnCursor = 0;
        }

        private void DispatchEnemySpawns(float elapsedTime)
        {
            var entries = levelConfig.EnemySpawns;
            while (enemySpawnCursor < entries.Count &&
                   entries[enemySpawnCursor].SpawnTime <= elapsedTime)
            {
                var entryIndex = enemySpawnCursor;
                var entry = entries[entryIndex];
                enemyManager.Spawn(new EnemySpawnRequest(
                    levelRunId,
                    entryIndex,
                    entry.ConfigId,
                    entry.SpawnPosition,
                    ResolveWorldPosition(entry.SpawnPosition)));
                enemySpawnCursor++;
            }
        }

        private void DispatchGateSpawns(float elapsedTime)
        {
            var entries = levelConfig.GateSpawns;
            while (gateSpawnCursor < entries.Count &&
                   entries[gateSpawnCursor].SpawnTime <= elapsedTime)
            {
                var entryIndex = gateSpawnCursor;
                var entry = entries[entryIndex];
                obstacleManager.Spawn(new GateSpawnRequest(
                    levelRunId,
                    entryIndex,
                    entry.GateType,
                    entry.InitialValue,
                    entry.ElementType,
                    entry.MaxHp,
                    levelConfig.ElementDurationSecondsPerDamage,
                    entry.SpawnPosition,
                    ResolveWorldPosition(entry.SpawnPosition)));
                gateSpawnCursor++;
            }
        }

        private void DispatchPropSpawns(float elapsedTime)
        {
            var entries = levelConfig.PropSpawns;
            while (propSpawnCursor < entries.Count &&
                   entries[propSpawnCursor].SpawnTime <= elapsedTime)
            {
                var entryIndex = propSpawnCursor;
                var entry = entries[entryIndex];
                obstacleManager.Spawn(new PropSpawnRequest(
                    levelRunId,
                    entryIndex,
                    entry.ConfigId,
                    entry.SpawnPosition,
                    ResolveWorldPosition(entry.SpawnPosition)));
                propSpawnCursor++;
            }
        }

        private Vector2 ResolveWorldPosition(float normalizedPosition)
        {
            return new Vector2(
                Mathf.Lerp(roadLayout.LeftBoundary, roadLayout.RightBoundary, normalizedPosition),
                roadLayout.SpawnY);
        }

        private bool IsCurrentRun(int queriedLevelRunId)
        {
            return running && queriedLevelRunId > 0 && queriedLevelRunId == levelRunId;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("SpawnManager must be initialized before starting a run.");
            }
        }

        private static void ValidateRoadLayout(RoadLayoutSnapshot layout)
        {
            if (!IsFinite(layout.LeftBoundary) ||
                !IsFinite(layout.RightBoundary) ||
                !IsFinite(layout.SpawnY) ||
                layout.RightBoundary <= layout.LeftBoundary)
            {
                throw new ArgumentException("Road layout contains invalid spawn boundaries.", nameof(layout));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
