# Obstacle 道路对象管理模块

## 模块信息

- ID：`MOD-OBSTACLE`
- 层级：Gameplay
- 状态：`InProgress`（批次 4 脚本已实现并通过编译；Prefab/Layer 与对象池联调待完成）
- 依赖：IPropConfigProvider（仅 Prop）、PoolService、Gate、Prop、EventBus、Level
- 决策：`../../06_Decisions/ADR-008-ObstacleManager.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`、`../../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`、`../../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`

## 目标

提供类似 `EnemyManager` 的道路对象管理能力，统一维护当前道路上的 Gate 和 Prop 实例，供 Spawn、UI、调试和后续关卡逻辑查询。

## 职责

- 通过 Inspector 绑定 `AdditiveGate`、`ElementGate` 和 `WeaponProp` 规范 Prefab，从 PoolService 取得对应具体类型池；接收 Spawn 请求后从正确类型池取得未激活对象。
- 为每个实例分配唯一的 `RuntimeInstanceId`。
- 设置 `ObstacleRoot`、世界位置和旋转；Gate 直接消费 `GateSpawnRequest` 的 LevelConfig 内联数据，Prop 按 `PropSpawnRequest.ConfigId` 从 `IPropConfigProvider` 必得已校验快照；随后注入会话 ID、实例 ID 和回调，登记活动集合后再激活对象。配置缺失已在应用启动时作为致命错误处理，ObstacleManager 不重复降级或记录同一错误。
- 登记、查询和注销活动 Gate/Prop。
- 提供按对象类型、运行时实例 ID 和道路位置的只读快照查询。
- 接收对象成功回收或下方离场请求，幂等注销、清理并主动失活后归还对应类型池；类型池再执行防御性失活。
- 发布对象生成、回收和离场事实事件；`ObstacleRecycled` 使用稳定的 `ObstacleRecycleReason` 区分接触完成、击破、下方离场和 StopRun 清理。
- 实现 `TickMovement`、`ResolveContacts` 和 `FlushPendingRecycles`；使用 LevelManager 传入的 Gate 时间域 delta，并只按 ADR-033 的阶段顺序执行，不使用独立 Update 推进核心玩法。
- `ResolveContacts` 只在本帧移动应用并完成一次 Physics2D 同步后，对各 Gate/Prop 的终点姿态执行 `OverlapCollider`；不执行移动路径 Cast。
- 以实例根 GameObject 中心判定离场：`position.y <= RoadLayoutSnapshot.DespawnY` 即登记回收，不按 Collider/Renderer 边缘修正。

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

每次查询返回防御性复制的只读快照列表，不直接包装或暴露 Manager 内部活动 List。`RuntimeInstanceId`、`SpawnEntryIndex` 和 Prop ConfigId 均为非负整数，`0` 合法；Gate 缺少 ConfigId 必须用 `null` 表达，不能用 `0` 伪装。

统一快照状态按 ADR-021 映射为 `MovingDown`、`ContactPending`、`ContactSucceeded`、`ContactFailed`、`ExitedUncontacted`、`Broken`、`Recycled`；Gate/Prop 自身仍维护专用接触状态。

## 测试标准

- 相同内联参数生成多个 Gate 或同一配置生成多个 Prop 时，每个实例拥有不同运行时 ID。
- 注册、查询、注销和对象池回收保持一致。
- 成功回收、失败后离场和未接触离场都只注销一次。
- Gate/Prop 终点 Overlap 和根中心 DespawnY 判定使用相同的本帧最终 Transform；离场对象不得再进行后续接触结算。
- 接触查询通过 SlotCollider 同节点的 `ArmySlotHitProxy` 取得 ArmyId 与 SlotIndex；先按 SlotIndex 去重并重新确认槽位有效，再调用 `ApplySlotDamage`。缺失代理不使用父级搜索兜底。
- 查询结果不包含已注销或已归还对象。
- Manager 不改变 Gate/Prop 的数值规则和 Army 状态。
- 三种具体池化根类型各自只绑定一个规范 Prefab；同类型不同 Prefab 注册失败。
- Gate/Prop 不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身。
- Preparing、Completed 或过期 LevelRunId 的阶段调用不移动、接触或回收当前会话之外的对象。
- Failed 元素门和 Prop 后续受击时 HP 最低锁在 `1`，不会因伤害进入击破/成功回收；它们只在 DespawnY 离场或 StopRun 时回收。
