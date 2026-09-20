using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [SerializeField]
        private float spawnTime;

        [SerializeField]
        [Range(0f, 1f)]
        private float spawnPosition;

        [SerializeField]
        private int configId;

        public float SpawnTime => spawnTime;
        public float SpawnPosition => spawnPosition;
        public int ConfigId => configId;
    }

    [Serializable]
    public sealed class GateSpawnEntry
    {
        [SerializeField]
        private float spawnTime;

        [SerializeField]
        [Range(0f, 1f)]
        private float spawnPosition;

        [SerializeField]
        private GateType gateType;

        [SerializeField]
        private int initialValue;

        [SerializeField]
        private ElementType elementType;

        [SerializeField]
        private int maxHp;

        public float SpawnTime => spawnTime;
        public float SpawnPosition => spawnPosition;
        public GateType GateType => gateType;
        public int InitialValue => initialValue;
        public ElementType ElementType => elementType;
        public int MaxHp => maxHp;
    }

    [Serializable]
    public sealed class PropSpawnEntry
    {
        [SerializeField]
        private float spawnTime;

        [SerializeField]
        [Range(0f, 1f)]
        private float spawnPosition;

        [SerializeField]
        private int configId;

        public float SpawnTime => spawnTime;
        public float SpawnPosition => spawnPosition;
        public int ConfigId => configId;
    }
}
