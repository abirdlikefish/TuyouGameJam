# ADR-021：MVP 运行时确定性与绑定约束

## 状态

Accepted

## 日期

2026-09-14

## 背景

核心玩法规则已经收敛，但 Army 身份、MVP 时间倍率、同帧碰撞处理、道路坐标、Army 移动速度、道路对象状态快照、事件分发和资源键仍存在实现层面的多种解释。若不先统一，多个模块可能使用不同的字段、时间或处理顺序。

## 决策

### Army 身份

- MVP 只有一个 Army，`ArmyId` 固定为 `1`。
- 不引入 Army ID 分配器；所有包含 Army 身份的事件和去重上下文使用 `ArmyId = 1`。
- 将来支持多个 Army 时，必须新增或更新 ADR，不能把固定值直接扩展为隐式分配。

### MVP 时间范围

- MVP 中所有时间域倍率和对象局部倍率固定为 `1`，不调用运行时倍率调整接口。
- 暂停、减速、加速和局部时停的完整组合规则延后；`ITimeService` 中保留的倍率与暂停接口属于后续扩展契约。
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

同一次 Bullet Cast 返回多个不同目标且距离相同时，按以下顺序选择：

1. Cast 命中距离更近者优先。
2. 距离相同按目标类别优先级：`Enemy` > `Gate` > `Prop`。
3. 类别和距离仍相同，按 `RuntimeInstanceId` 升序。

该类别优先级只用于完全相同距离的稳定决胜；多个子 Collider 属于同一运行时实例时先按实例去重。

### 道路坐标

- 道路和所有玩法位置使用世界坐标的 XY 平面，运行时 `z = 0`。
- 世界右方为 `+x`，世界上方为 `+y`；`SpawnY`、`EnemyApproachY`、`DespawnY`、边界和三路生成点都在同一世界坐标系中。
- 道路 Prefab/场景提供实际坐标和边界；世界原点可以按场景摆放，不是公共契约约束，但所有相关对象必须使用同一坐标系。

### Army 移动速度

- Army 横向移动速度由 Luban `TbArmy.MoveSpeed` 提供。
- Input 只传入归一化方向，不保存或决定速度；Army 使用该配置值结合输入和道路边界计算移动。

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

## 未在本 ADR 定案

Layer Collision Matrix 的最终允许/禁止关系仍需根据 Unity 工程中的查询方式评审后单独定案；本 ADR 只确定查询顺序和目标类别优先级。

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
