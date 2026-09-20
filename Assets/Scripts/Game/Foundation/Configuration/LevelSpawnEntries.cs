using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [SerializeField]
        [Tooltip("从本局开始计时的生成时刻，单位为秒且不能为负。同一列表必须按时间非递减排列；相同时间按列表顺序生成。")]
        private float spawnTime;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("道路横向归一化位置：0 为最左边界，1 为最右边界，中间值线性插值。使用对象根节点中心，不按对象尺寸内缩。")]
        private float spawnPosition;

        [SerializeField]
        [Tooltip("引用 Luban TbEnemy 的非负 ID，决定生成敌人的基础属性。")]
        private int configId;

        public float SpawnTime => spawnTime;
        public float SpawnPosition => spawnPosition;
        public int ConfigId => configId;
    }

    [Serializable]
    public sealed class GateSpawnEntry
    {
        [SerializeField]
        [Tooltip("从本局开始计时的生成时刻，单位为秒且不能为负。同一列表必须按时间非递减排列；相同时间按列表顺序生成。")]
        private float spawnTime;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("道路横向归一化位置：0 为最左边界，1 为最右边界，中间值线性插值。使用对象根节点中心，不按对象尺寸内缩。")]
        private float spawnPosition;

        [SerializeField]
        [Tooltip("门类型。Additive 使用 Initial Value；Element 使用 Element Type 和 Max Hp。未使用的条件字段必须填写中性值。")]
        private GateType gateType;

        [SerializeField]
        [Tooltip("加法门的初始数字，可为正数、0 或负数，但不能为 Int32.MinValue；元素门必须填写 0。子弹命中会按伤害增加该数字。")]
        private int initialValue;

        [SerializeField]
        [Tooltip("元素门奖励的元素类型，必须为 Fire、Ice 或 Lightning；加法门必须选择 None。")]
        private ElementType elementType;

        [SerializeField]
        [Tooltip("元素门的初始最大 HP，必须大于 0；加法门必须填写 0。HP 清空后的额外伤害用于兑换元素持续时间。")]
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
        [Tooltip("从本局开始计时的生成时刻，单位为秒且不能为负。同一列表必须按时间非递减排列；相同时间按列表顺序生成。")]
        private float spawnTime;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("道路横向归一化位置：0 为最左边界，1 为最右边界，中间值线性插值。使用对象根节点中心，不按对象尺寸内缩。")]
        private float spawnPosition;

        [SerializeField]
        [Tooltip("引用 Luban TbProp 的非负 ID，决定道具的生命值和武器奖励。")]
        private int configId;

        public float SpawnTime => spawnTime;
        public float SpawnPosition => spawnPosition;
        public int ConfigId => configId;
    }
}
