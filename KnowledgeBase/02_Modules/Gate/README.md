# Gate 门模块

## 模块信息

- ID：`MOD-GATE`
- 层级：Gameplay
- 状态：`InProgress`（批次 4 玩法脚本与批次 7.4 Animator/池复用适配代码已实现并通过编译；Prefab 字段、Layer 与接触手测待完成）
- 依赖：EventBus、Army、Bullet、ObstacleManager、Level
- 决策：`../../06_Decisions/ADR-006-AdditiveGateAndContactResolution.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`、`../../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`、`../../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`、`../../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md`

## 职责

- 控制加法门和元素门向下移动；移动流程使用 LevelManager 传给 ObstacleManager 的 `Gate` 时间域 delta，一次更新不再读取或叠加 `Gameplay` delta。
- 使用 Prefab 上的 `BodyCollider` 参与子弹命中和 Army 接触；本帧移动与 Physics2D 同步完成后只对终点姿态执行一次 `OverlapCollider`，不做接触 Cast、扫掠或子步进，也不依赖自动碰撞回调。
- 保存门的运行时数字、HP、接触状态和运行时实例 ID。
- 接收 `BulletDamageContext`；加法门按实际伤害增加数字，元素门扣减 HP 并在 HP 归零后累计额外伤害。
- 在 Army 首次接触时执行一次判定。
- 通过 `IArmyController` 同步应用门效果或槽位伤害，完成状态变更后再发布接触结果事实事件。
- 成功、失败和下方离场后向 `ObstacleManager` 报告生命周期变化。

## 门类型

两类门共享“子弹先改变门状态、Army 接触时只判定一次”的交互骨架，但子弹命中与接触结果不同：

| 类型 | 子弹命中 | Army 接触成功 | Army 接触失败 |
|---|---|---|---|
| 加法门 | `GateValue += BulletDamageContext.Damage` | `GateValue >= 0`，请求 Army 增加当前数字 | `GateValue < 0`，向 Army 提交绝对值形式的请求减员人数 |
| 元素门 | 先扣减运行时 HP，HP 归零后的额外伤害累计到 `PostDepletionDamage` | `Hp <= 0`，按额外伤害和关卡系数计算元素持续时间 | `Hp > 0`，对每个接触槽位造成相同伤害并锁定元素奖励 |

子弹不会直接把门效果发给 Army；Army 接触才是 Gate 效果的结算点。加法门没有 HP，元素门也不维护可加减的门数字。

### 加法门

- `GateValue` 是可为负数的当前数字。
- 每次有效子弹命中后增加该次 `BulletDamageContext.Damage`；同一 `BulletInstanceId` 只能结算一次。
- Army 接触时按符号提交不同命令：

```text
if GateValue >= 0:
    additionResult = Army.AddArmy(GateValue)
else:
    removalResult = Army.RemoveArmy(Abs(GateValue))
```

- `GateValue >= 0` 标记为成功；Army 自行应用 ArmyCountLimit，并返回 `ArmyAdditionResult` 供 Gate 的接触事实记录请求与实际增加量。
- `GateValue < 0` 标记为失败。Gate 只传入请求减员人数，Army 将其乘以 HpPerSoldier 并按当前 HP 最少、槽位索引最小的顺序承担；请求减员和实际人数损失可以不同。该路径不使用元素门的逐接触槽位伤害，结算后回收。

### 元素门

- 每个生成项从 `LevelConfig` 提供 `MaxHp` 和 `ElementType`；初始化时 `CurrentHp = MaxHp`、`PostDepletionDamage = 0`，当前不使用 TbElement 或 TbGate。
- Pending 状态收到正数伤害时，先以 `Min(CurrentHp, Damage)` 扣减 HP，超过剩余 HP 的部分计入 `PostDepletionDamage`。使 HP 恰好归零的伤害没有额外部分；HP 已为零时，后续每次有效伤害全部计入额外伤害。
- HP 为零的 Pending 元素门仍是合法子弹目标并消耗子弹；子弹不会穿透该门继续命中后方目标。
- HP 清空后接触 Army 标记成功，并计算 `calculatedDuration = PostDepletionDamage × LevelConfig.elementDurationSecondsPerDamage`。额外伤害大于零时给对应元素增加该持续时间；同类型累加且不覆盖另外两种元素。
- `PostDepletionDamage == 0` 时接触仍标记成功，但计算持续时间为 `0`，不调用只接受正持续时间的 `AddElementDuration`，也不发布 `ArmyElementDurationChanged`。
- HP 未清空时接触失败，对每一个接触到的 Army 槽位造成相同的接触伤害。
- 失败后继续向下移动并离场，不再次进行成功判定。
- 接触失败后奖励永久锁定。后续子弹仍正常命中、消费并产生受击表现，但 HP 按 `Max(1, CurrentHp - Damage)` 锁在至少 `1`，不会归零，也不再增加可兑换的 `PostDepletionDamage` 或发放元素奖励；对象只继续向下移动至离场或由 StopRun 清理。

## 接触状态

```text
Pending
  -> Succeeded
  -> Failed
  -> ExitedUncontacted
```

实际运行时只有一条状态转换路径。`Succeeded` 和 `Failed` 都表示接触判定已经消费；同一门不能因为多个槽位或多帧碰撞重复结算。未接触直接离场使用 `ExitedUncontacted`，不产生成功或失败接触事件。

门的接触去重键使用 `ArmyId + RuntimeInstanceId`，不使用配置表中的 `GateId`。

## 配置输入

- 当前 MVP 不建立 `TbGate`，Gate 不读取 ConfigService 或配置 ID。
- `LevelConfig.gateSpawns` 的每条生成项提供 `GateType`、加法门 `InitialValue`、元素门 `ElementType` 和每个元素门独立的 `MaxHp`；关卡顶层提供本关统一且以“秒/伤害”为单位的 `elementDurationSecondsPerDamage`。
- 关卡出现顺序和每条生成项的 `[0,1]` 横向出生位置也由 `LevelConfig` 提供；SpawnManager 解析固定 `spawnY` 上的中心点世界坐标并构造 `GateSpawnRequest`。
- 加法门使用 `AdditiveGate` 根脚本和一个规范 Prefab，统一 `MoveSpeed` 由该 Prefab 的 Inspector 字段提供。
- 元素门使用 `ElementGate` 根脚本和一个规范 Prefab，统一 `MoveSpeed` 和 `ContactDamage` 由该 Prefab 的 Inspector 字段提供。
- ObstacleManager 通过 Inspector 绑定二者并按 `GateType` 选择具体类型池，不读取 Luban 或资源键选择 Prefab。
- 当前数字、当前 HP、`PostDepletionDamage`、接触状态和位置属于运行时状态，不回写 LevelConfig 或 Prefab。

## 计划脚本与 Prefab 结构

批次 4 已按以下最小结构创建脚本；对应 Prefab 仍待用户装配：

```text
Assets/Scripts/Game/Gameplay/Gate/
├── AdditiveGate.cs        无 HP 的伤害驱动数字门；处理数字命中与正负接触
└── ElementGate.cs         处理 HP、额外伤害、持续时间换算与失败锁定

Assets/Prefabs/Gate/
├── PF_Gate_Additive.prefab   根组件 AdditiveGate
└── PF_Gate_Element.prefab    根组件 ElementGate
```

`GateType`、`GateContactState` 等稳定枚举直接使用 Contracts 中的共享定义，不在 Gate 模块重复创建本地类型文件。

首轮 Prefab 使用最小占位层级：

```text
PF_Gate_Additive [AdditiveGate；Animator]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer；BulletHitProxy]
└── StateText [TMP_Text]

PF_Gate_Element [ElementGate；Animator]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer；BulletHitProxy]
└── StateText [TMP_Text]
```

加法门的单个 `stateText` 显示当前 GateValue；元素门的单个 `stateText` 至少显示 ElementType、`CurrentHp/MaxHp` 和 PostDepletionDamage。加法门绑定单循环 Clip；元素门保持一个规范 Prefab，并通过 Animator 整数参数 `ElementType` 选择 Fire、Ice、Lightning 循环 Clip。排版和最终文案不属于玩法契约，文本与动画都不得反向修改玩法状态。

两个根组件都由 `ObstacleManager` 驱动移动、接触和回收，不实现独立 `Update`，也不持有 ConfigService、PoolService 或 ArmyController 具体类型。最小序列化/运行时字段分工：

| 根组件 | Inspector 序列化 | 每次生成注入/重置 |
|---|---|---|
| `AdditiveGate` | `BodyCollider`、同节点 `BulletHitProxy`、`MoveSpeed`、`TMP_Text stateText`、视觉引用、Animator | `LevelRunId`、`RuntimeInstanceId`、`SpawnEntryIndex`、`InitialValue -> GateValue`、接触状态、位置、结束回调、Army 契约 |
| `ElementGate` | `BodyCollider`、同节点 `BulletHitProxy`、`MoveSpeed`、`ContactDamage`、`TMP_Text stateText`、视觉引用、Animator | `LevelRunId`、`RuntimeInstanceId`、`SpawnEntryIndex`、`ElementType` 与动画状态、`MaxHp -> CurrentHp`、`elementDurationSecondsPerDamage`、`PostDepletionDamage = 0`、奖励未锁定、接触状态、位置、结束回调、Army 契约 |

Prefab 不保存 `InitialValue`、`ElementType`、`MaxHp` 或持续时间系数；LevelConfig 也不保存两类门的统一速度和元素门统一接触伤害。`InitialValue` 不得为 `int.MinValue`；GateValue 正向累加超过 `int` 范围时饱和到 `int.MaxValue`。必需引用或同节点 BulletHitProxy 缺失、速度非有限/小于 0、元素门接触伤害小于等于 0 时，Gameplay 不得进入 Ready，不能运行时静默补组件或使用默认值。

表现组件只读取根组件已经结算出的快照或事实事件，不反向修改门值、HP、额外伤害或奖励锁定状态。若后续发现两类门有稳定且足够多的共同生命周期代码，可再提取内部基类；当前文档不要求为了复用少量字段预先建立通用 Gate 框架。

完整最小绑定见 [PrefabSpecifications](../../04_Assets/PrefabSpecifications.md)。

## 非职责

- 不直接修改 Army 槽位内部人数或 HP。
- 不依赖 ArmyController 订阅 `GateContactResolved` 来执行门效果；该事件只用于事后观察。
- 不负责 Spawn 时序和活动对象列表。
- 不决定敌人生成完成和关卡胜负条件。
- 不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身；只通过 ObstacleManager 注入的窄回调请求结束当前实例。
- 不使用独立 Update 推进移动或接触；由 ObstacleManager 在 LevelManager 指定阶段驱动。

## 测试标准

- 子弹命中一次只按该次实际伤害更新一次数字或元素门状态，同一子弹实例不得重复结算。
- 加法门对伤害分别为 `2` 和 `5` 的两次命中累计增加 `7`，不按命中次数增加固定值。
- 加法门的正数和零调用 AddArmy；负数接触标记失败并只调用 `RemoveArmy(Abs(GateValue))`，Gate 不选择槽位或直接设置 ArmyCount。
- 负数门得到的 ArmyRemovalResult 分别记录请求减员、实际伤害和实际人数损失；最低当前 HP 槽位优先，同 HP 时 SlotIndex 较小者优先。
- 同一门对同一 Army 只进行一次接触判定。
- Army 状态变更完成后才发布一次 `GateContactResolved`；增删其他事件监听者不会改变结算结果。
- 元素门 `MaxHp = 10` 依次受到 `6`、`6`、`5` 点伤害后，`CurrentHp = 0` 且 `PostDepletionDamage = 7`；HP 为零后仍可被命中并消费子弹。
- 元素门在接触前 HP 清空并接触 Army 时，按 `PostDepletionDamage × elementDurationSecondsPerDamage` 计算一次持续时间；额外伤害为零时成功但不调用 `AddElementDuration`。
- 元素门接触失败后不再累计可兑换额外伤害；后续命中正常消费子弹，但 HP 最低锁在 `1`，不会被打空，也不增加任何元素持续时间或触发伤害回收。
- 元素门未清空时，对每个接触槽位造成相同伤害，之后继续移动离场。
- 元素门接触失败后继续受击时 HP 最低锁在 `1`，不增加 Army 的元素持续时间或可兑换额外伤害，也不因伤害回收。
- 未接触的门离场时不产生接触成功或失败事件。
- Gate 根 GameObject 中心满足 `position.y <= RoadLayoutSnapshot.DespawnY` 时离场；不使用 Collider 或 Renderer 下边缘。
- 接触只使用移动终点的 `OverlapCollider` 结果；MVP 不验收路径中穿过但终点未重叠的接触。
- Gate 的 BodyCollider 使用 Gate Layer；同一查询返回多个子 Collider 时按运行时实例 ID 去重。
- 两种具体 Gate 类型各自只绑定一个规范 Prefab；从类型池取得时未激活，ObstacleManager 完成初始化和登记后才激活，归还前清理运行时状态并主动失活。
- 加法门持续播放本类型循环 Clip；Fire/Ice/Lightning 元素门按本次 ElementType 播放唯一对应循环 Clip。元素门从池中以不同类型复用时不得残留旧参数、状态、帧或 Sprite。
- 两种 Gate 在不依赖 HUD 或最终美术的情况下，单个 stateText 能随运行时状态刷新并用于验证结算结果。
