# ADR-046：首轮玩法实现契约收口

## 状态

Accepted

## 日期

2026-09-20

## 背景

首轮代码生成前，Army、Gate、Prop、Obstacle、Monster、Bullet 和 Spawn 的主体规则已经确定，但仍存在碰撞查询结果如何映射到玩法身份、槽位伤害命令是否返回结果、失败道路对象的后续受击、配置 ID 范围、敌人阻挡保证以及少量生成请求和事件所有权不一致。若直接实现，不同模块会各自补充不兼容的约定。

## 决策

### 配置与运行时 ID

- `LevelId` 以及所有 Luban 表主键和外键都是非负整数，`0` 是合法 ID；每张 Luban 表都从首行 `Id = 0` 开始编号，不把 `0` 当作缺失值。除已明确固定的 ID 外，不要求后续编号连续。
- `WeaponId = 0/1/2` 继续分别表示 Slingshot、Bow、Staff。
- 活动 Gameplay 会话的 `LevelRunId` 必须大于 `0`；`0` 只表示当前没有活动会话。MVP 唯一 Army 使用首行配置，`ArmyId = TbArmy.Id = 0`。
- `RuntimeInstanceId` 和 `SpawnEntryIndex` 为非负整数；在各自约定作用域内唯一或稳定，不使用负数或配置 ID 充当缺失哨兵。Gate 没有 ConfigId，仍通过可空字段表达不存在。

### 槽位伤害命令与事件

- `IArmyController.ApplySlotDamage(int slotIndex, int damage)` 保持 `void`，不新增 `SlotDamageResult`。
- `damage` 必须大于 `0`。调用方在提交命令前使用 `TryGetSlotTarget` 重新确认槽位仍有效；无效或空槽位不提交伤害。
- 命令同步返回后，Army 已经完成该槽位的权威 HP、人数和事件更新。Army 是 `SoldierHit`、人数、阵型和归零事实的唯一发布者。
- Monster、Gate 和 Prop 的攻击/接触事实只记录已向当时有效槽位提交的配置伤害，字段语义为 `RequestedDamage`、`AttackPower` 或 `ContactDamage`，不宣称等于实际 HP 扣除或实际人数损失。
- Monster 在当前攻击动画结束后的下一次 `TickMovement` 重新验证原目标或选择新目标；调用方不依赖伤害返回值推进状态。

### Collider 身份代理

- Physics2D 查询返回的 `Collider2D` 必须在同一 GameObject 上具有明确的身份代理组件，不使用向父级搜索作为缺失引用兜底。
- Enemy、Gate 和 Prop 的受击 Collider 节点使用 `BulletHitProxy`，由 Inspector 显式绑定实现 `IBulletHittable` 的根玩法组件，并提供对象类别和运行时实例 ID。
- Army 的 `SlotCollider` 节点使用 `ArmySlotHitProxy`，由 Inspector 显式绑定对应 `ArmySlotView`；Army 初始化时注入固定 `ArmyId` 和数组下标 `SlotIndex`。
- Gameplay Preparing 必须校验 Collider、代理和目标绑定一致。运行时只在命中的同一节点读取已经验证的代理；找不到代理表示装配不变量被破坏，不尝试 `GetComponentInParent`、`Find` 或默认目标。
- 同一查询先按代理提供的运行时实例 ID 或 SlotIndex 去重，再执行伤害、接触或同距离优先级。

### Army 增员与数值边界

- Army 不保存 `NeedsRefill`。初始创建仍使用整数平均和按索引分配余数；运行时每增加一人，都从全部槽位中选择 `RepresentedCount` 最少者，相同时选择 `SlotIndex` 最小者。空槽位因此自然优先重新启用。
- `AddArmy(0)` 合法但不改变状态，也不发布 Army 数值变化事件；Gate 接触事实仍可报告零值成功。
- 重复获得当前 WeaponId 不发布 `ArmyWeaponChanged`，不重置射击冷却；Prop 的击破效果仍视为已提交。
- 人数、HP、门值和持续时间计算使用足够宽的中间类型。配置禁止 `InitialValue == int.MinValue`；`GateValue` 正向累加、无上限 Army 人数、槽位聚合 HP 和元素持续时间在超过公开存储类型时饱和到该类型最大有限值，不允许回绕、变负、NaN 或无穷值。

### Failed 元素门和 Prop

- 元素门在 `Pending` 时可以正常降到 `0` 并累计额外伤害。接触失败进入 `Failed` 时，奖励永久锁定，若当前 HP 大于 `1` 则保留原值；其后每次有效伤害都正常消费子弹并发布受击事实，但 `CurrentHp = Max(1, CurrentHp - Damage)`，永远不会降到 `0`，也不增加 `PostDepletionDamage`。
- Prop 接触失败进入 `Failed` 后采用相同的 `1 HP` 锁血规则：继续接收并消费子弹、播放受击反馈，但 HP 永远不会降到 `0`，不发布 `PropBroken`、不触发击破效果，也不因伤害请求回收。
- Failed 元素门和 Prop 只继续向下移动，并在根中心到达 `DespawnY` 时按失败离场流程回收；StopRun 仍可直接清理。

### 敌人阻挡、攻击和生成诊断

- EnemyManager 的 BodyCollider Cast 只查询上一轮 `Physics2D.SyncTransforms` 后的物理姿态；本帧不为逐个敌人移动增加额外同步，也不维护预测碰撞解算器。
- 命中上一同步姿态中的前方存活敌人时，后方敌人按 Cast 距离和自身 `blockingGap` 截断位移。该规则用于在 MVP 速度、Collider 尺寸和帧率下减少穿透与重叠，但不承诺同帧移动后的绝对不重叠，也不执行事后分离或侧向绕行。
- Monster 从 `MovingDown` 到达 `EnemyApproachY` 时把本帧位移截断在接近线，下一次 `TickMovement` 再开始向槽位接近。
- 普通敌人一旦开始攻击，命中帧只重新校验会话、敌人、攻击序号和目标槽位仍有效，不因目标随后移出 `AttackStartRange` 取消；精英/Boss 仍由命中帧的 `AttackCollider` Overlap 决定实际命中槽位。
- `MonsterAttackLanded` 由完成权威结算的 EnemyManager 发布；范围攻击对每个实际提交伤害的有效槽位各发布一条。
- `EnemySpawnRequest` 增加 `SpawnEntryIndex`；三类生成请求都携带各自列表内的来源索引，`MonsterSpawned` 同步携带该字段。

### 配置单位与枚举

- 所有表主键和外键非负且允许 `0`，每张表的首行 ID 为 `0`。所有配置 float 必须有限。
- `MoveSpeed` 统一使用世界单位/秒；`FireInterval`、`AttackCooldown` 和元素持续时间统一使用秒；伤害、HP 和人数使用整数。
- 进入代码前必须在 Contracts 中定义 `EnemyType`、`AttackType`、`GateType`、`ObstacleKind`、`ObstacleState`、接触状态和各事件原因枚举的稳定成员，不依赖实现模块自行补枚举。

## 后果

- 不增加由攻击者读取 Army 私有结算结果的耦合；实际伤害继续由 Army 事实事件表达。
- Prefab 需要新增两个显式身份代理组件及绑定，但 Physics 查询结果不再依赖父级扫描或隐式命名。
- Failed 元素门和 Prop 的受击表现、奖励锁定和离场生命周期完全确定。
- 敌人排队是受 MVP 参数约束的近似阻挡，不再将“同帧绝对不重叠”作为验收不变量。
- Army、Gate、Prop、Obstacle、Monster、Bullet、Spawn 与 Level 的记录契约闭合后可以进入 `ContractReady`；工程实现和资源验证仍按路线图保持未完成。

## 验收标准

- 表 ID 为 `0` 时可以完成配置加载和跨表引用；负 ID 被拒绝。
- `ApplySlotDamage` 保持 void，攻击结束后重新索敌，Army 事件仍报告实际损失。
- Physics 查询命中的 Collider 能通过同节点代理稳定取得受击目标或 SlotIndex；缺失代理阻止 Gameplay Ready。
- Failed 元素门和 Prop 在任意正伤害后 HP 最低为 `1`，继续消耗子弹但永不成功、击破或发奖，最终只因离场或 StopRun 回收。
- 多敌人只基于上一同步姿态 Cast；测试验证目标 MVP 参数下排队效果，同时允许记录并接受少量同帧重叠风险。
- 三类生成请求都能追溯到 `SpawnEntryIndex`；EnemyManager 是 `MonsterAttackLanded` 的唯一发布者。

## 关联文档

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Formation.md`
- `../02_Modules/Gate/README.md`
- `../02_Modules/Prop/README.md`
- `../02_Modules/Obstacle/README.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Bullet/README.md`
- `../02_Modules/Spawn/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/CollisionRules.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/EventCatalog.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
- `../05_Testing/ObstacleTests.md`
