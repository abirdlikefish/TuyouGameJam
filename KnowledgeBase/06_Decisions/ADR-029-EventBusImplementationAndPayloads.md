# ADR-029：EventBus 实现、事件载荷与订阅生命周期

## 状态

Accepted

## 日期

2026-09-18

## 背景

ADR-014 已确定 `IEventBus` 公共接口，ADR-021 已确定同步发布、注册顺序、订阅快照、异常隔离、重复订阅和幂等取消，但仍缺少可以直接指导工程实现的文件布局、Token 身份、嵌套发布、类型匹配、异常报告、载荷值/引用类型选择和订阅清理规则。

事件目录目前包含 27 个事件。若把每个事件都拆为脚本会增加 Game Jam 工程维护成本；若统一使用字符串和 `object` 又会失去类型安全。Monster 死亡链路还需要区分 EnemyManager 必须完成的存活计数与允许零监听者的击杀事实。

## 决策

### EventBus 内核

- `EventBus` 是由 `GlobalBootstrap` 创建和持有的纯 C# 应用级服务，不继承 `MonoBehaviour`，不暴露静态 `Instance`，也不创建独立 GameObject。
- MVP 仅在 Unity 主线程同步使用 EventBus，不实现后台线程、锁、异步队列或延迟派发。
- `Publish<T>` 只匹配准确的消息类型，不向基类或接口订阅者做多态广播。
- 允许同步嵌套发布；每层发布在开始时复制自己的处理器快照。
- `SubscriptionToken` 是不透明值，内部同时关联 EventBus 身份和单次订阅 ID。默认、未知、已使用或其他 Bus 的 Token 取消时无副作用。
- EventBus 通过构造参数接收 `Action<Exception>` 异常报告委托；Composition 可以传入 `Debug.LogException`，不为此引入 DebugService。
- 分发使用强类型 `Action<T>` 调用，不使用字符串事件名、`object Data`、`Delegate.DynamicInvoke` 或反射扫描注册。

### 文件与载荷组织

- 当前不创建 `.asmdef`。事件契约放入 `Assets/Scripts/Game/Contracts/Events`，实现放入 `Assets/Scripts/Game/Services/Events`，目录先表达职责。
- 内核由 `IEventBus.cs`、`SubscriptionToken.cs` 和 `EventBus.cs` 构成。
- 业务事件按 `ApplicationEvents.cs`、`ArmyEvents.cs`、`ObstacleEvents.cs`、`MonsterEvents.cs` 四个领域文件集中，不要求每个事件一个脚本。
- 小型、字段固定或高频事件默认使用 `readonly struct`；包含大型快照、多个集合或明显复制成本的事件使用不可变 `sealed class`。
- `IEventBus` 不对消息类型增加 `struct` 或 `class` 泛型约束。无论采用哪种类型，事件发布后都不可变；所有可变集合必须复制为快照。

### 订阅生命周期

- 应用级服务保存自己的 Token，并在释放时取消订阅；应用流程订阅必须在配置初始化前完成。
- 各 SceneEntry 必须先注入依赖并完成场景订阅，再允许 SceneService 发布对应 `AppSceneReady`；Gameplay 还必须完成 LevelManager `Preparing`。
- Gameplay 处理器即使已正确管理订阅，也必须校验 `LevelRunId`。
- 池对象不查找或长期持有全局 EventBus，不直接订阅全局事件；Manager 在复用时传入最小类型化回调，并在回收前清理回调与会话状态。

### Monster 死亡边界

- Monster 判定生命值归零后，通过 EnemyManager 提供的必执行回调报告死亡。
- EnemyManager 负责死亡去重、立即减少 `AliveEnemyCount`，然后发布 `MonsterKilled` 事实；注销活动实例和动画结束后的回收仍由 EnemyManager 管理。
- `MonsterKilled` 的监听者不得承担 EnemyManager 的存活计数、注销或回收职责。移除全部事件监听者不能改变敌人死亡状态和存活计数。

### 事件登记

- 新增或修改跨模块事件、载荷或发布所有权前，先新增或更新 ADR。
- `EventCatalog.md` 是事件名称、发布者、主要监听者和载荷语义的唯一目录。
- `PublicInterfaces.md` 维护公共接口、公共消息类型和错误码；模块 README 只描述本模块如何使用契约。
- 共享字段语义同步到 `DataDictionary.md`，验收同步到 `IntegrationTests.md`，变更摘要同步到 `ChangeLog.md`。

## 不采用

- 不为每个事件默认创建一个脚本或 MonoBehaviour。
- 不使用单个 `GameEvent` 加字符串名称和 `object` 载荷。
- 不使用 UnityEvent、ScriptableObject Event Channel 或静态全局事件替代当前 EventBus。
- 不使用弱引用自动清理监听者；订阅所有者必须显式取消。
- 不让必须完成的业务步骤依赖允许零监听者的事实事件。

## 影响

- `EventSystem.md` 从概念说明扩展为可执行的实现与生命周期文档。
- `PublicInterfaces.md` 补充 Token 身份、精确类型、嵌套发布和消息类型规则。
- `EventCatalog.md` 将 `MonsterKilled` 的发布者改为 EnemyManager，并增加事件登记流程。
- Monster 模块需要通过必执行回调把死亡交给 EnemyManager，再发布击杀事实。
- EventBus 的 EditMode 测试需要覆盖精确类型、嵌套发布、默认/跨 Bus Token 和异常报告委托。
- 程序集工程化仍按 ADR-026 延后，本 ADR 不授权创建 `.asmdef` 或 Unity 工程脚本。

## 关联文档

- `../01_Architecture/EventSystem.md`
- `../01_Architecture/BootstrapAndComposition.md`
- `../02_Modules/Monster/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/EventCatalog.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-014-SharedRuntimeContractBaseline.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `ADR-026-AssemblyBoundariesAndCommunication.md`
