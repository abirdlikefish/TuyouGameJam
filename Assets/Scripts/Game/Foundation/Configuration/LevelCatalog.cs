using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Configuration/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Header("关卡目录")]
        [SerializeField]
        [Tooltip("可用关卡的有序目录。顺序用于生成关卡描述列表；Level Id 必须唯一，第一项必须设置为初始解锁。")]
        private LevelCatalogEntry[] entries = new LevelCatalogEntry[0];

        public IReadOnlyList<LevelCatalogEntry> Entries => entries;
    }

    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField]
        [Tooltip("该目录项对应的 LevelConfig 资产，不能为空。关卡 ID 从该资产读取，不在目录中重复填写。")]
        private LevelConfig levelConfig;

        [SerializeField]
        [Tooltip("游戏初始化时该关卡是否默认可选。MVP 目录中的第一项必须勾选。")]
        private bool initiallyUnlocked;

        public LevelConfig LevelConfig => levelConfig;
        public bool InitiallyUnlocked => initiallyUnlocked;
    }
}
