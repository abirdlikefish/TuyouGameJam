# ADR-005：敌人类型、接近攻击流程与 EnemyManager

## 状态

Accepted（`AttackType` 配置方式由 ADR-020 收窄；敌人根脚本与 Prefab 划分由 ADR-031 修订；攻击距离、TargetSensor 移除和 Army 重合边界由 ADR-037 修订）

## 日期

2026-09-13

## 背景

首版需要普通、精英和 Boss 三种敌人。三种敌人共享移动和普通攻击流程，但攻击类型、数值和 Prefab 碰撞体不同。敌人从道路上方出现，向道路偏下的接近线移动，随后向最近的有士兵槽位接近，进入攻击起始范围后停止移动。普通敌人使用单体攻击，精英和 Boss 使用前方范围攻击碰撞体。

敌人数量可能较多，需要统一管理生成、对象池、存活数量和回收。子弹通过碰撞命中敌人，伤害反馈需要知道子弹、武器和元素来源，但不需要新增 `DamageType`。

## 决策

- 使用 `EnemyType` 区分 `Normal`、`Elite`、`Boss`。本 ADR 原定三种类型共享一个轻量敌人行为控制器；ADR-031 已将实现边界修订为 `NormalMonster`、`EliteMonster`、`BossMonster` 三个具体池化根脚本和三个规范 Prefab，公共行为继续复用。
- `AttackType` 保留为运行时概念，但不作为 `TbEnemy` 字段：`Normal` 派生为 `SingleTarget`，`Elite` 和 `Boss` 派生为 `Area`。Boss 首版没有额外阶段或特殊技能。
- 敌人先向下移动至关卡配置的道路接近线，再向最近的有效士兵槽位移动。只按怪物与槽位目标位置的 XY 距离判断攻击起始范围；进入范围后停止移动，不再继续向道路底部移动。
- 敌人的最近目标由 ArmyController 提供。目标只包括 `RepresentedCount > 0` 的士兵槽位，距离相同时按 `SlotIndex` 从小到大选择。
- 单体攻击锁定一个槽位。判定帧前敌人死亡或目标槽位变为空时，取消当前攻击并重新选择目标、重新开始攻击动画。
- 范围攻击使用 Prefab 前方的 `AttackCollider`。碰撞体只在攻击判定帧执行一次显式重叠查询，对范围内每个有效槽位造成一次相同的 `AttackPower`。
- 攻击判定、子弹命中和敌人之间的实体碰撞使用独立碰撞体和 Layer 规则，避免同一事件重复结算。
- ADR-037 已移除 `TargetSensor`，并明确 Enemy `BodyCollider` 与 Army `SlotCollider` 不承担移动阻挡；Army 与敌人允许重合，重合状态仍按单体锁定或范围 `AttackCollider` 正常攻击。
- `EnemyManager` 负责敌人生成入口、注册、对象池回收、活跃列表和存活数量；Spawn 负责根据 `LevelConfig` 提供生成时机和位置。
- `AliveEnemyCount` 只统计未死亡敌人；敌人死亡后立即从存活数量移除，死亡动画完成后再回收到对象池。
- 子弹伤害上下文使用 `BulletId`、`WeaponId`、发射瞬间的 `ElementMask`、最终伤害和命中位置/方向，不新增 `DamageType`。
- 敌人受击与击杀事件携带伤害上下文，使 Animator、AudioVFX 等表现层可以按子弹来源选择反馈。

## 不采用

- 不为三种敌人复制三套相同状态机或提前建立复杂 Boss 框架；ADR-031 允许三个具体池化根脚本承载真实差异，公共规则仍通过组合、接口、基类或纯 C# 逻辑复用。
- 不保留首版的“到达底部造成 `ContactDamage`”流程，不使用 `MonsterReachedBase` 作为首版事件。
- 不让每个敌人每帧扫描所有 Army 槽位；目标只在进入攻击、攻击结束或目标失效时重新选择。
- 不新增独立 `DamageType` 枚举。

## 影响

- `TbEnemy` 需要增加敌人类型、攻击力、攻击起始范围和攻击冷却等字段，并移除或重新定义 `ContactDamage`；攻击类型由敌人类型派生，攻击判定时刻由 Animator 判定帧提供。
- ArmyController 需要提供有效槽位的只读位置查询；Monster 通过 ArmyController 查询目标并通过 `ApplySlotDamage` 结算。
- 敌人的子弹受击适配需要接收带子弹来源的伤害上下文；保留通用 `IDamageable` 的整数伤害语义，避免影响 Army 等其他受伤目标。`MonsterKilled` 需要携带造成击杀的最后一击上下文。
- `MonsterReachedBase` 从首版事件目录移除；新增 `MonsterDamaged` 和 `MonsterAttackLanded` 供表现和关卡统计使用。
- 敌人身体、范围攻击碰撞体和子弹碰撞体必须使用明确的碰撞层。移动由自定义时间驱动；使用 Collider2D 查询时按 `../03_SharedContracts/CollisionRules.md` 在查询阶段显式同步 Physics2D Transform。
- 轻量 `enum + switch` 状态更新不会构成主要性能瓶颈；对象池、减少目标查询、攻击窗口碰撞查询和物理接触数量是首要优化点。

## 关联文档

- `../02_Modules/Monster/README.md`
- `../02_Modules/Spawn/README.md`
- `../02_Modules/Army/README.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/EventCatalog.md`
- `../07_Changes/ChangeLog.md`
- `ADR-020-MinimalMvpConfigurationSurface.md`
- `ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`
- `ADR-037-MonsterDistanceTargetingAndCollisionLayers.md`
