using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Configuration/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField]
        private LevelCatalogEntry[] entries = new LevelCatalogEntry[0];

        public IReadOnlyList<LevelCatalogEntry> Entries => entries;
    }

    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField]
        private LevelConfig levelConfig;

        [SerializeField]
        private bool initiallyUnlocked;

        public LevelConfig LevelConfig => levelConfig;
        public bool InitiallyUnlocked => initiallyUnlocked;
    }
}
