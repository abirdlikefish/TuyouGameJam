# Obstacle 道路对象管理模块

## 模块信息

- ID：`MOD-OBSTACLE`
- 层级：Gameplay / Infrastructure
- 状态：`InDesign`
- 依赖：ConfigService（仅 Prop）、PoolService、Gate、Prop、EventBus、Level
- 决策：`../../06_Decisions/ADR-008-ObstacleManager.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`

## 目标

提供类似 `EnemyManager` 的道路对象管理能力，统一维护当前道路上的 Gate 和 Prop 实例，供 Spawn、UI、调试和后续关卡逻辑查询。

## 职责

- 通过 Inspector 绑定 `AdditiveGate`、`ElementGate` 和 `WeaponProp` 规范 Prefab，从 PoolService 取得对应具体类型池；接收 Spawn 请求后从正确类型池取得未激活对象。
- 为每个实例分配唯一的 `RuntimeInstanceId`。
- 设置 `ObstacleRoot`、世界位置和旋转；Gate 直接消费 `GateSpawnRequest` 的 LevelConfig 内联数据，Prop 按 `PropSpawnRequest.ConfigId` 查询已校验配置；随后注入会话 ID、实例 ID 和回调，登记活动集合后再激活对象。
- 登记、查询和注销活动 Gate/Prop。
- 提供按对象类型、运行时实例 ID 和道路位置的只读快照查询。
- 接收对象成功回收或下方离场请求，幂等注销、清理并主动失活后归还对应类型池；类型池再执行防御性失活。
- 发布对象生成、回收和离场事实事件。
- 实现 `TickMovement`、`ResolveContacts` 和 `FlushPendingRecycles`；使用 LevelManager 传入的 Gate 时间域 delta，并只按 ADR-033 的阶段顺序执行，不使用独立 Update 推进核心玩法。

## 非职责

- 不计算加法门数字、元素门 HP 或道具 HP。
- 不执行 Army 人数变化、元素切换或武器切换。
- 不替代 Gate/Prop 自身的接触去重和状态机。
- 不决定敌人生成完成和关卡胜负条件。

## 查询模型

生成来源、配置表 ID 和运行时实例 ID 必须分离：

```text
SpawnEntryIndex：LevelConfig 对应生成列表中的本局来源索引
ConfigId：Gate 不使用；Prop 使用 TbProp.Id
RuntimeInstanceId：本次生成的具体对象
```

对外提供 `IObstacleRegistry` 定义的只读快照查询：

```csharp
IReadOnlyList<RoadObjectSnapshot> GetActiveObjects();
IReadOnlyList<RoadObjectSnapshot> GetActiveObjects(ObstacleKind kind);
bool TryGetObject(int runtimeInstanceId, out RoadObjectSnapshot snapshot);
```

快照至少包含运行时实例 ID、生成项索引、可空配置 ID、对象类别、世界位置、是否仍在道路上和当前交互状态。Gate 的配置 ID 必须为 `null`，Prop 的配置 ID 为 `TbProp.Id`；调用方不得取得管理器内部可变集合。

统一快照状态按 ADR-021 映射为 `MovingDown`、`ContactPending`、`ContactSucceeded`、`ContactFailed`、`ExitedUncontacted`、`Broken`、`Recycled`；Gate/Prop 自身仍维护专用接触状态。

## 测试标准

- 相同内联参数生成多个 Gate 或同一配置生成多个 Prop 时，每个实例拥有不同运行时 ID。
- 注册、查询、注销和对象池回收保持一致。
- 成功回收、失败后离场和未接触离场都只注销一次。
- 查询结果不包含已注销或已归还对象。
- Manager 不改变 Gate/Prop 的数值规则和 Army 状态。
- 三种具体池化根类型各自只绑定一个规范 Prefab；同类型不同 Prefab 注册失败。
- Gate/Prop 不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身。
- Preparing、Completed 或过期 LevelRunId 的阶段调用不移动、接触或回收当前会话之外的对象。
