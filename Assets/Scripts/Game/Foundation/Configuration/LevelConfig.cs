using System.Collections.Generic;
using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Game/Configuration/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("关卡标识")]
        [SerializeField]
        [Tooltip("关卡的非负唯一 ID。LevelCatalog 中不能出现重复 ID，运行时通过该值选择关卡。")]
        private int levelId;

        [SerializeField]
        [Tooltip("关卡选择等界面使用的显示名称，不参与关卡 ID 查询。")]
        private string displayName = string.Empty;

        [SerializeField]
        [Tooltip("通关本关后解锁的关卡 ID。不能重复或填写本关 ID；尚未加入 LevelCatalog 的 ID 会被警告并从运行时结果中过滤。")]
        private int[] unlockedLevelIds = new int[0];

        [Header("纵向判定")]
        [SerializeField]
        [Tooltip("敌人、Gate 和 Prop 共用的世界坐标出生 Y；以生成对象根节点中心为准。必须位于场景道路上边界内。")]
        private float spawnY;

        [SerializeField]
        [Tooltip("敌人向下移动到达此世界 Y 后，开始转向最近的有效士兵槽位。必须位于场景 Army 出生点与 Spawn Y 之间。")]
        private float enemyApproachY;

        [SerializeField]
        [Tooltip("对象根节点中心到达或低于此世界 Y 时按离场处理。必须位于场景道路下边界与 Army 出生点之间。")]
        private float despawnY;

        [Header("元素门奖励")]
        [SerializeField]
        [Tooltip("元素门 HP 清空后的额外伤害每 1 点可兑换的元素持续秒数。关卡存在元素门时必须大于 0；没有元素门时必须为 0。")]
        private float elementDurationSecondsPerDamage;

        [Header("生成时间轴")]
        [SerializeField]
        [Tooltip("敌人生成列表，至少需要一项。按 Spawn Time 非递减排列，Config Id 引用 Luban 的 TbEnemy。")]
        private EnemySpawnEntry[] enemySpawns = new EnemySpawnEntry[0];

        [SerializeField]
        [Tooltip("Gate 生成列表，可为空。按 Spawn Time 非递减排列；每项直接配置加法门或元素门参数。")]
        private GateSpawnEntry[] gateSpawns = new GateSpawnEntry[0];

        [SerializeField]
        [Tooltip("道具生成列表，可为空。按 Spawn Time 非递减排列，Config Id 引用 Luban 的 TbProp。")]
        private PropSpawnEntry[] propSpawns = new PropSpawnEntry[0];

        public int LevelId => levelId;
        public string DisplayName => displayName;
        public IReadOnlyList<int> UnlockedLevelIds => unlockedLevelIds;
        public float SpawnY => spawnY;
        public float EnemyApproachY => enemyApproachY;
        public float DespawnY => despawnY;
        public float ElementDurationSecondsPerDamage => elementDurationSecondsPerDamage;
        public IReadOnlyList<EnemySpawnEntry> EnemySpawns => enemySpawns;
        public IReadOnlyList<GateSpawnEntry> GateSpawns => gateSpawns;
        public IReadOnlyList<PropSpawnEntry> PropSpawns => propSpawns;
    }
}
