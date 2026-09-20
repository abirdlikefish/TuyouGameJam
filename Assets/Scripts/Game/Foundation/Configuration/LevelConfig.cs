using System.Collections.Generic;
using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Game/Configuration/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [SerializeField]
        private int levelId;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private int[] unlockedLevelIds = new int[0];

        [SerializeField]
        private Rect roadBounds;

        [SerializeField]
        private float spawnY;

        [SerializeField]
        private float enemyApproachY;

        [SerializeField]
        private float despawnY;

        [SerializeField]
        private float elementDurationSecondsPerDamage;

        [SerializeField]
        private EnemySpawnEntry[] enemySpawns = new EnemySpawnEntry[0];

        [SerializeField]
        private GateSpawnEntry[] gateSpawns = new GateSpawnEntry[0];

        [SerializeField]
        private PropSpawnEntry[] propSpawns = new PropSpawnEntry[0];

        public int LevelId => levelId;
        public string DisplayName => displayName;
        public IReadOnlyList<int> UnlockedLevelIds => unlockedLevelIds;
        public Rect RoadBounds => roadBounds;
        public float SpawnY => spawnY;
        public float EnemyApproachY => enemyApproachY;
        public float DespawnY => despawnY;
        public float ElementDurationSecondsPerDamage => elementDurationSecondsPerDamage;
        public IReadOnlyList<EnemySpawnEntry> EnemySpawns => enemySpawns;
        public IReadOnlyList<GateSpawnEntry> GateSpawns => gateSpawns;
        public IReadOnlyList<PropSpawnEntry> PropSpawns => propSpawns;
    }
}
