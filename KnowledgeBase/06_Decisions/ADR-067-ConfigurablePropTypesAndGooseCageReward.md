# ADR-067：可配置道具类型与鹅笼增员奖励

## 状态

Accepted

## 日期

2026-09-21

## 背景

现有 `TbProp` 只能表达武器箱，篮球数值来自 Prefab，导致两类可击破道路道具存在两套配置来源。新增鹅笼要求可配置生命值、接触伤害和士兵奖励数量，并与篮球、武器箱一样进入关卡 `propSpawns` 时间轴和序列帧表现管线。

## 决策

1. 新增 `PropType`：`WeaponBox = 0`、`Basketball = 1`、`GooseCage = 2`。`TbProp` 增加 `propType` 与 `armyAddition`，保留 `weaponId`、`maxHp`、`contactDamage`、`moveSpeed`。
2. 三类道具统一由 `LevelConfig.propSpawns[].configId` 引用 `TbProp`。现有配置 ID `0~2` 保持为三种武器箱；新增篮球 ID `3`、鹅笼 ID `4`。
3. 条件字段规则：武器箱要求有效 `weaponId` 且 `armyAddition = 0`；篮球要求 `weaponId = 0`、`armyAddition = 0`；鹅笼要求 `weaponId = 0`、`armyAddition > 0`。所有类型都要求正数 HP/接触伤害和有限非负移动速度。
4. `ObstacleManager` 按 `PropType` 从 `WeaponProp`、`BasketballProp`、`GooseCageProp` 三个具体类型池借出实例。三类均复用 `BreakablePropBase` 的受击去重、接触失败、Failed 锁血、移动、离场与回池规则。
5. 鹅笼仅在 `Pending` 状态被击破时同步调用 `IArmyController.AddArmy(armyAddition)`，随后发布包含请求量、实际增员量和剩余人数的事实事件。Army 人数上限仍是最终权威。
6. 接触 Army 时若尚未击破，鹅笼与篮球、武器箱一致：对本次重叠的每个有效槽位造成一次配置伤害，进入 `Failed`，之后不再可能击破或增员。
7. 篮球的 HP、接触伤害和移动速度从 Prefab 迁入 `TbProp`。关卡直接生成的篮球使用普通 `PropSpawnRequest`；ikun 生成篮球时使用关卡顶层 `ikunBasketballConfigId`，该 ID 必须引用 `PropType.Basketball`。
8. 没有 ikun 的关卡使用 `ikunBasketballConfigId = 0` 作为被忽略的中性值；包含 ikun 的关卡必须配置有效篮球 ID。运行时篮球请求保留来源敌人运行时 ID，不伪造关卡生成项索引。
9. 鹅笼使用独立规范 Prefab、类型池和单一 Loop 状态。当前击破仍立即回收，不增加 Breaking 状态；空 Clip 不清空占位 Sprite，正式序列帧导入后由 Prop 专用动画工具重建。

## 不采用

- 不把鹅笼伪装成特殊 `weaponId`，也不使用负数或魔法值区分道具类型。
- 不通过事件监听器执行增员；事件只描述同步命令已经完成的事实。
- 不在 `LevelConfig` 重复保存 HP、伤害或奖励数值；关卡只选择 `TbProp.Id`。
- 不把三种具体根脚本合成一个依赖运行时 `AddComponent` 或资源路径搜索的通用 Prefab。

## 验收

- 配置加载能拒绝未知 `PropType`、错误条件字段、无效武器引用和错误的 ikun 篮球配置引用。
- 关卡 `propSpawns` 可分别生成武器箱、篮球和鹅笼，并保持配置 ID、生成项索引、位置与池类型正确。
- 鹅笼 HP 首次归零只增员一次；上限截断时事件记录请求量与实际量；回池复用不残留 HP、子弹去重、接触状态或动画帧。
- 鹅笼未击破接触 Army 时逐槽伤害一次，进入 Failed，后续伤害锁 HP 至少为 1 且永不增员。
- ikun 使用关卡配置的篮球 ID；篮球直接关卡生成和 ikun 生成均消费同一 `TbProp` 数值。
- 鹅笼 Loop、篮球 Loop 与三种武器箱 Loop 均从第 0 帧开始，且动画不拥有玩法结算时机。

## 关联文档

- `ADR-022-PropBreakEffectBoundary.md`
- `ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`
- `ADR-064-IkunBasketballEnemyAbility.md`
- `../02_Modules/Prop/README.md`
- `../02_Modules/Obstacle/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../04_Assets/AnimationPipeline.md`

