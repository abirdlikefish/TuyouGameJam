# ADR-014：跨模块运行时契约基线

## 状态

Accepted（Pool 公共契约由 ADR-031 修订为具体组件类型池；Gate/Prop 生成请求由 ADR-038 修订）

> ADR-027 已从当前 MVP 公共契约移除 `PauseToken` 和倍率修改能力；本 ADR 对其他接口、请求、事件和会话 ID 的决策继续有效。

> ADR-033 已新增 Army 本局接口、BulletManager、InputGate 和 Manager 阶段方法；ADR-034 已把 RoadLayoutSnapshot 扩展为从唯一 `roadBounds` 派生的完整四边。

> ADR-038 已用 `GateSpawnRequest` 与 `PropSpawnRequest` 取代统一 `ObstacleSpawnRequest`，并以 `IBulletHittable` 表达无 HP 或 HP 归零后仍可接收子弹的目标。下文旧名称只保留决策演进背景。

## 日期

2026-09-14

## 背景

公共接口已经覆盖部分 Army、伤害、道路快照和 Level 查询，但 ConfigService、SceneService、SpawnManager、EnemyManager、EventBus、PoolService 和完整 TimeService 契约尚未统一记录。模块文档因此只能依赖具体控制器名称或未定义的方法。

## 决策

- `../03_SharedContracts/PublicInterfaces.md` 记录跨模块依赖的最小能力，不暴露完整控制器、可变集合或内部 Unity 层级。
- Config、场景、生成、敌人、道路对象、事件、时间和对象池分别拥有独立接口；模块通过接口依赖，不直接持有其他模块的具体控制器。
- `CompleteGameplay(LevelCompletion)` 是 GameStateService 接收当前会话结果的命令；`AppFlowChanged`、`Victory`、`GameOver` 和其他事件只描述已经发生的事实。
- Gate 和 Prop 通过 `IArmyController` 的同步方法提交人数、槽位伤害、元素或武器变更；状态变更完成后再发布接触/击破事实事件。`ArmyController` 不通过订阅这些事实事件执行同一状态变更。
- 所有 Gameplay 生成请求和结果事件必须携带 `LevelRunId`，接收者拒绝过期会话的数据。
- `AreAllEnemySpawnsDispatched` 属于 SpawnManager，不属于 LevelManager 的生成状态所有权。
- 生成实例的取得和归还属于 PoolService 类型池与对应 Manager 的协作；SpawnManager 只提交业务生成请求。ADR-031 已将原始 `GameObject + string key` 入口修订为具体 `MonoBehaviour` 类型池。
- `TimeService` 当前只定义固定倍率时间域读取、定时器句柄和取消语义；暂停与倍率能力按 ADR-027 延后。Gameplay 核心各域 delta 由 LevelManager 按 ADR-033 集中读取并传给对应阶段。
- 玩法碰撞对象使用 Collider2D 与显式 Cast/Overlap 查询的统一规则由 ADR-016 和 `CollisionRules.md` 维护。
- 事件 payload、错误码、生成请求和目录描述必须使用明确的数据结构，不使用无意义字符串。
- `EventCatalog.md` 中所有 Gameplay 对象事件统一携带 `LevelRunId`；涉及具体道路对象或敌人实例时使用明确的运行时实例 ID。初始化、应用流程全局事件可不携带会话 ID，但跨场景或延迟处理的事件不得例外。
- `RoadLayoutSnapshot` 必须包含所有由 LevelConfig 提供、且玩法模块需要读取的道路空间边界，包括 `DespawnY`。
- Gate/Prop 的离场和回收事实由 `ObstacleManager` 统一发布，避免对象和管理器重复发布同一事件。

## 必备接口组

```text
IConfigService
IResourceRegistry
IGameStateService
ISceneService
ILevelRuntime
IHorizontalInputReceiver
IArmyController
IArmyRunController
IGameplayInputGate
IGameplayInputController
ISpawnManager
IBulletManager
IEnemyManager
IObstacleRegistry
IObstacleManager
IEventBus
ITimeService
IPoolService
IComponentPool<T>
```

## 初始化失败

`AppFlowState` 保持 `Initializing`，同时由 ConfigService 发布 `InitializationFailed`，携带稳定错误码和来源。未收到成功初始化结果前，GameStateService 不得进入 MainMenu。

## 影响

- 共享契约增加 ConfigLoadState、ConfigErrorCode、LevelDescriptor、EnemySpawnRequest、ObstacleSpawnRequest、TimeDomain、TimerHandle、SubscriptionToken、失败事件 payload 和场景加载错误码。原决策中的 `PauseToken` 已由 ADR-027 从当前 MVP 契约移除。
- 事件目录需要记录初始化失败和应用场景加载/卸载失败事件；具体应用场景载荷已由 ADR-032 统一为 `AppScene...` 事件。
- Gate/Prop 事实事件的监听者只保留表现、音频、调试和统计消费者；玩法状态变更由类型化接口完成。
- 模块状态只有在接口名称、参数和所有权与本 ADR 一致后，才可提升到 `ContractReady`。

## 关联文档

- `../01_Architecture/ConfigurationSystem.md`
- `../01_Architecture/EventSystem.md`
- `../01_Architecture/GlobalServices.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Spawn/README.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `ADR-019-ApplicationFlowContract.md`
