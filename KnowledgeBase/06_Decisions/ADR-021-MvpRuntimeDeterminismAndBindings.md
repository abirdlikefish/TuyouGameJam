# ADR-021：MVP 运行时确定性与绑定约束

## 状态

Accepted（对象池不再使用资源键选择 Prefab，见 ADR-031）

> ADR-046 将所有表 ID 统一为从 `0` 开始，因而把 MVP 唯一 Army 的 `ArmyId` 从 `1` 修订为 `0`；本 ADR 的其他确定性与绑定规则不变。

> ADR-027 已进一步移除当前 `ITimeService` 中的倍率与暂停接口；本 ADR 的固定倍率结论继续有效。

> ADR-029 已进一步补充 EventBus 的精确类型匹配、嵌套发布、Token 身份、异常报告、载荷类型和订阅生命周期；本 ADR 的同步快照分发基础语义继续有效。

> ADR-033 已将本 ADR 的同帧顺序落实为 LevelManager 持有的同步阶段管线；ADR-034 已把道路空间收敛为唯一 `roadBounds` 并固定 ArmyRoot 世界原点。

## 日期

2026-09-14

## 背景

核心玩法规则已经收敛，但 Army 身份、MVP 时间倍率、同帧碰撞处理、道路坐标、Army 移动速度、道路对象状态快照、事件分发和资源键仍存在实现层面的多种解释。若不先统一，多个模块可能使用不同的字段、时间或处理顺序。

## 决策

### Army 身份

- MVP 只有一个 Army，`ArmyId` 固定为首行 ID `0`。
- 不引入 Army ID 分配器；所有包含 Army 身份的事件和去重上下文使用 `ArmyId = 0`。
- 将来支持多个 Army 时，必须新增或更新 ADR，不能把固定值直接扩展为隐式分配。

### MVP 时间范围

- MVP 中所有时间域倍率和对象局部倍率固定为 `1`，不调用运行时倍率调整接口。
- 暂停、减速、加速和局部时停的完整组合规则延后；按 ADR-027，相关倍率与暂停方法不保留在当前 `ITimeService` 契约中。
- MVP 的关卡计时和玩法逻辑使用未缩放的正常步进；主界面和选关定时器继续使用 `RealTime`。

### 同帧处理与同距离命中

每个逻辑帧的权威处理顺序固定为：

```text
读取有效 delta
→ 移动与敌人身体阻挡
→ 子弹 Cast 命中与伤害
→ Gate/Prop 接触查询与结算
→ Monster AttackCollider 攻击查询与结算
→ 发布本阶段事实并回收对象
→ LevelManager 执行终局判断
```

该顺序由 LevelManager 通过类型化同步阶段接口执行；`LevelRunStarted` 只启动会话，Manager 和池对象不使用独立 Update 绕过阶段顺序。

同一次 Bullet Cast 返回多个不同目标且距离相同时，按以下顺序选择：

1. Cast 命中距离更近者优先。
2. 距离相同按目标类别优先级：`Enemy` > `Gate` > `Prop`。
3. 类别和距离仍相同，按 `RuntimeInstanceId` 升序。

该类别优先级只用于完全相同距离的稳定决胜；多个子 Collider 属于同一运行时实例时先按实例去重。

### 道路坐标

- 道路和所有玩法位置使用世界坐标的 XY 平面，运行时 `z = 0`。
- 世界右方为 `+x`，世界上方为 `+y`；`SpawnY`、`EnemyApproachY`、`DespawnY` 和道路边界都在同一世界坐标系中。横向出生位置由 ADR-023 的 `[0,1]` 归一化规则解析。
- LevelConfig 的唯一世界坐标 `roadBounds` 提供实际边界，宽高和四边由它派生；ArmyRoot 每局从世界原点 `(0,0,0)` 开始且 y 保持为 0。道路 Prefab 只负责视觉，不提供玩法 Collider。

### Army 移动速度

- Army 横向移动速度由 Luban `TbArmy.MoveSpeed` 提供。
- Input 只传入归一化方向，不保存或决定速度；Army 使用该配置值结合输入和道路边界计算移动。

> 后续变更：ADR-028 将相对拖动纳入 MVP；ADR-036 又将首个工程切片收窄为设备触屏与 Editor 左键共用的单一拖拽输入，键盘/手柄延后。拖拽先将原始归一化滑动速度限制到 `[-1,1]`，再乘 Inspector 系数，最终输入可以超过该范围。`TbArmy.MoveSpeed` 保持为基础速度来源。

### Gate/Prop 状态快照

`RoadObjectSnapshot.State` 使用统一生命周期状态映射：

| 对象状态 | 快照状态 |
|---|---|
| 正常移动且尚未进入接触结算 | `MovingDown` |
| 本帧已经检测到接触候选、尚未完成结算 | `ContactPending` |
| 接触成功 | `ContactSucceeded` |
| 接触失败 | `ContactFailed` |
| 未接触离场 | `ExitedUncontacted` |
| Prop 已击破但尚未回收 | `Broken` |
| Manager 已完成注销并归还池 | `Recycled` |

Gate/Prop 的专用接触枚举仍负责本对象的细节状态；快照只暴露上述统一观察状态。`Recycled` 只用于回收事实或注销前的瞬时快照，已从 `IObstacleRegistry` 注销的对象不能再出现在活动查询结果中。

### EventBus 语义

- `Publish` 同步执行，并在发布开始时复制当前订阅者列表。
- 订阅者按注册顺序调用；发布期间新增或取消订阅只影响下一次发布。
- 同一处理器重复订阅视为不同订阅，每个订阅拥有独立 `SubscriptionToken`。
- 单个处理器抛出异常时记录错误并继续调用其他处理器。
- `Unsubscribe` 幂等；无效或已使用的 Token 不产生副作用。

### Unity 资源键

- 资源键只属于 Unity 资源注册表，不写入 Luban，也不使用绝对路径或 `AssetDatabase` 路径。
- 键使用大小写敏感的 ASCII `类别/身份` 格式：`Enemy/Normal`、`Enemy/Elite`、`Enemy/Boss`、`Gate/Additive`、`Gate/Element`、`Prop/Weapon/{WeaponId}`、`Bullet/{BulletId}`。
- 同一键只能注册一个兼容类型的资源；缺失或类型不匹配在进入 Gameplay 前报告 `ResourceMissing`。
- ADR-031 已明确资源键不作为对象池身份或 PoolService 的 Prefab 选择入口。池化规范 Prefab 由对应 Manager 的 Inspector 引用提供，PoolService 使用准确的具体根组件类型区分类型池；本节继续约束其他运行时资源绑定。

## 后续定案

Layer Collision Matrix 的最终允许/禁止关系已由 ADR-037 定案；本 ADR 继续只负责查询顺序和目标类别优先级。

## 影响

- 公共契约和数据字典需要记录固定 `ArmyId`、MVP 时间范围、确定性排序、世界坐标和状态映射。
- `TbArmy` 增加 `MoveSpeed`；资源清单和配置校验使用统一资源键格式。
- EventBus 和碰撞测试需要覆盖注册顺序、异常隔离、重复订阅、同帧阶段顺序和同距离决胜。

## 关联文档

- `../00_Project/DesignBacklog.md`
- `../01_Architecture/TimeSystem.md`
- `../01_Architecture/EventSystem.md`
- `../02_Modules/Army/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/CollisionRules.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../04_Assets/ArtList.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-029-EventBusImplementationAndPayloads.md`
- `ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`
- `ADR-037-MonsterDistanceTargetingAndCollisionLayers.md`
