# ADR-037：怪物距离索敌、Army 重合与 Gameplay Layer 矩阵

## 状态

Accepted

> ADR-046 补充敌人间阻挡：BodyCollider Cast 只查询上一轮 `Physics2D.SyncTransforms` 后的姿态，在 MVP 参数下尽量减少穿透和重叠，但不保证同帧绝对不重叠，也不执行事后分离。

## 日期

2026-09-19

## 背景

原碰撞契约为怪物 Prefab 预留了 `TargetSensor`，同时又使用 `TbEnemy.AttackStartRange` 表达进入攻击的距离，形成了两套可能不一致的攻击起始范围。此前 Layer Collision Matrix 也只给出候选方案，尚未确认哪些关系属于显式查询、哪些关系需要自动物理接触。

MVP 的 Army 会整体横向移动，敌人在到达接近线后追踪最近有效槽位。当前不需要处理 Army 与敌人之间的实体阻挡、推挤或同帧相向高速移动；允许 Army 移动后士兵槽位与敌人身体重合。玩法只需要保证敌人即使与槽位重合，仍能按攻击起始距离、攻击判定帧和攻击类型正常造成伤害。

## 决策

### 攻击起始距离

- 从所有怪物 Prefab 和碰撞契约中移除 `TargetSensor`。攻击起始条件只使用当前怪物世界位置与已锁定 `ArmySlotTarget.WorldPosition` 的 XY 欧氏距离。
- 当 `distanceSquared <= AttackStartRange * AttackStartRange` 时，敌人停止主动接近并进入攻击；距离为 `0` 的完全重合情况同样满足条件。
- 目标位置随 Army 移动更新。攻击判定帧只登记待结算攻击；`EnemyManager.ResolveAttacks` 重新验证会话、敌人、目标和攻击序号后才造成伤害。
- 普通敌人对锁定的有效 `SlotIndex` 结算一次单体伤害。精英和 Boss 仍以 `AttackCollider` 对 `ArmySlot` 执行一次显式范围重叠查询；`AttackStartRange` 决定何时开始攻击，`AttackCollider` 决定范围攻击实际命中哪些槽位。
- 数值与 Prefab 应调到大多数情况下敌人在明显重合前开始攻击，但“不重合”不是玩法不变量，也不作为配置加载成功条件。

### 移动重合边界

- Monster `BodyCollider` 只参与子弹受击和存活敌人之间的移动阻挡，不查询或阻挡 `ArmySlot`。
- Army 横向移动只受道路数值边界和激活槽位合并 AABB 约束，不对 `EnemyBody` 执行 Cast，也不因敌人位置截断输入位移。
- Army 槽位与敌人身体允许部分或完全重合。重合不产生接触伤害、推挤、自动攻击或额外事件。
- 不处理 Army 与 Enemy 在同一帧相向快速移动的问题；当前帧阶段和唯一一次 `Physics2D.SyncTransforms` 顺序保持不变。
- 敌人之间仍使用 `EnemyBody` Cast 尝试保持安全间距；查询只看到上一同步姿态，因此后方敌人在 MVP 参数下尽量不穿过存活的前方敌人，但不存在同帧绝对不重叠保证。

### Gameplay Layer 与查询关系

MVP 固定使用以下六个玩法 Layer：

```text
Bullet
EnemyBody
EnemyAttack
ArmySlot
Gate
Prop
```

道路左右边界、生成线、接近线和离场线全部使用数值，不建立 Collider 或 Layer。显式查询关系固定为：

| 查询者 | 允许查询目标 | 用途 |
|---|---|---|
| Bullet `BodyCollider` | `EnemyBody`、`Gate`、`Prop` | 子弹扫掠命中 |
| Monster `BodyCollider` | 其他存活 `EnemyBody` | 敌人内部移动阻挡 |
| Monster `AttackCollider` | `ArmySlot` | 精英/Boss 范围攻击 |
| Gate `BodyCollider` | `ArmySlot` | Gate 接触 |
| Prop `BodyCollider` | `ArmySlot` | Prop 接触 |

`EnemyBody` 与 `ArmySlot` 之间没有移动查询关系。普通敌人的单体攻击通过已锁定 `SlotIndex` 调用 `IArmyController.ApplySlotDamage`，不增加单体攻击 Collider。

上述 Gameplay Layer 之间的 Unity 自动 Layer Collision Matrix 默认全部关闭。核心规则不依赖 `OnTriggerEnter2D`、`OnCollisionEnter2D` 或 Rigidbody2D 自动响应；显式查询始终使用明确的 `ContactFilter2D` 或 LayerMask。工程实现时必须在目标 Unity 版本验证矩阵关闭后这些显式查询仍能返回指定目标；如果具体 API 必须开启 Layer 对，只开启对应查询所需的最小关系，仍不得引入自动玩法结算。

## 不采用

- 不保留 `TargetSensor` 作为攻击距离的第二权威来源。
- 不让 `EnemyBody` 与 `ArmySlot` 自动推挤、阻挡或触发接触伤害。
- 不为道路边界建立碰撞层或 Collider。
- 不因允许 Army/Enemy 重合而把 `EnemyAttack` 合并进 `EnemyBody`；范围攻击仍需独立职责 Layer，避免被子弹或敌人阻挡查询命中。
- 不在 MVP 中解决 Army 与 Enemy 的相对运动扫掠、同帧高速交叉或重合分离。

## 影响

- Monster Prefab 只需要 `BodyCollider`；Elite/Boss 额外需要 `AttackCollider`，三类 Prefab 都不再包含 `TargetSensor`。
- Army 移动实现不增加 Enemy 查询或位移截断逻辑，LevelManager 帧管线和 SyncTransforms 次数不变。
- `AttackStartRange` 的配置与范围攻击 Collider 需要通过可玩性调参覆盖重合和近距离攻击场景，但不承担实体分离职责。
- DES-030 的设计结论已确定；Project Settings 与显式查询行为仍需作为工程验证项执行。

## 关联文档

- `../00_Project/DesignBacklog.md`
- `../02_Modules/Army/README.md`
- `../02_Modules/Monster/README.md`
- `../03_SharedContracts/CollisionRules.md`
- `../03_SharedContracts/DataDictionary.md`
- `../04_Assets/ArtList.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-005-MonsterCombatAndManager.md`
- `ADR-016-Collider2DCollisionQueries.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
