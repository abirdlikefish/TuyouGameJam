# 启动与装配

## 状态

ContractReady（设计状态，不代表已有 Unity 实现）

## 职责

`GlobalBootstrap` 是应用入口和全局服务的 Composition Root，只负责：

- 在 `BootstrapScene` 中确认唯一 `GlobalRoot`，并使其跨场景保留。
- 按 Create、Connect、Start 三个阶段创建服务、连接明确类型依赖并启动应用；创建 EventBus 时注入异常报告委托，不为此增加 DebugService。
- 绑定 Inspector 提供的 `LevelCatalog`、资源注册表和唯一的 Luban `cfg.Tables` 实例。
- 通过 UnitySceneRuntime 的 Inspector 字段绑定并校验 `BootstrapScene`、`MainMenuScene`、`LevelSelectScene`、`GameplayScene` 的稳定场景标识；确认目标场景已加入 Build Settings。场景标识不直接引用尚未加载场景中的 Entry 组件。
- 绑定唯一的 `PersistentPoolRoot`，创建全局 PoolService；具体池化 Prefab 由 Gameplay Manager 在 Inspector 绑定并在场景装配时注册为类型池。
- 在 ConfigService 为 `Ready` 后，调用一次 `GameStateService.NotifyInitializationReady()`。
- 在应用退出或入口销毁时取消全局订阅和定时任务，并按初始化逆序释放已创建服务。

`GlobalBootstrap` 不选择关卡、不推进 MainMenu/LevelSelect/Gameplay 状态，也不直接操作具体 SceneEntry 或承担玩法规则。它只创建并连接 SceneService 使用的 Unity 场景适配器。

## MVP 装配范围

```text
GlobalRoot
├── GlobalBootstrap [Composition Root]
└── ServiceHost
    ├── EventBus
    ├── TimeService
    ├── GameStateService
    ├── SceneService
    ├── ConfigService
    └── PoolService
```

Gameplay 场景模块不进入 `ServiceHost`。MVP 也不创建 `AudioService`、`SaveService` 或 `DebugService`。

服务可以是纯 C# 对象，不要求每个服务对应一个 GameObject。`ServiceHost` 表示所有权和注册位置，不是新的通用 `GameManager`。

## 实例与注入规则

- Composition Root 为每种 MVP 服务创建一个应用级实例，并持有其完整生命周期；服务唯一性不通过各类型的静态 `Instance` 实现。
- 纯 C# 服务优先在构造时取得最小接口。需要 Unity 场景 API 的组件由 `GlobalBootstrap` 或场景装配入口通过明确的 `Initialize(...)` 连接，不使用运行时 `Find`。
- 不提供可被任意业务对象访问的通用 Service Locator。Gameplay 场景装配入口可以读取一次全局服务接口并向场景 Manager 分发，但不能把完整服务集合继续传给 Bullet、Monster、Gate 或 Prop。
- 场景内固定组件与资源使用 Inspector 引用；应用级服务使用接口注入；单次会话数据使用参数或初始化快照。这三种依赖不得混成静态全局状态。
- 当前 `LubanTables.Instance` 是已有加载适配器，不是其他服务采用静态单例的先例。Bootstrap 只读取一次并把同一 `cfg.Tables` 交给 ConfigService；其他模块不直接访问该静态入口。
- 服务 Connect 阶段只注入明确类型依赖，不使用 `IServiceProvider`、服务字典或其他 Service Locator。所有服务完成 Connect 前不得进入 Start。

## 三阶段初始化顺序

```text
Create
├── 确认唯一 GlobalRoot
├── 创建 EventBus，并注入异常报告委托
├── 创建 TimeService、ConfigService 与 UnitySceneRuntime
├── 创建 SceneService，并注入 IEventBus 与 UnitySceneRuntime
├── 创建 GameStateService，并注入 IConfigService、ISceneService、ITimeService 与 IEventBus
└── 创建 PoolService，并注入 PersistentPoolRoot 与诊断委托

Connect
├── 向 UnitySceneRuntime 提供各 SceneEntry 初始化所需的最小服务和延后可得依赖
└── 校验场景标识、Build Settings、LevelCatalog、资源注册表和其他序列化引用

Start
├── 应用级服务注册事件并保存 SubscriptionToken
├── ConfigService.Initialize(LevelCatalog, Tables, ResourceRegistry)
├── 确认 ConfigService Ready
└── GameStateService.NotifyInitializationReady()，由它请求切换 MainMenuScene
```

具体 `LevelConfig` 不在全局初始化时绑定；它在 LevelSelect 确定关卡后由 ConfigService 返回，再经 SceneService 注入 Gameplay。

纯 C# 服务仍优先在 Create 阶段使用构造注入；Connect 只处理 SceneEntry 装配桥接等构造时尚不可闭合的跨模块依赖，不把所有服务强制改为可变的 Setter 注入。不是每个服务都必须实现统一的三阶段生命周期接口。EventBus 等没有跨服务连接需求的对象可以在构造后使用；三阶段是 Composition Root 的装配屏障。清理按 Start、Connect、Create 的逆序进行，并且只回滚本入口已完成的阶段。

## 失败与幂等

- 如果发现已有有效 `GlobalRoot`，新入口不得再创建第二套服务；重复入口应停止自身初始化。
- 任一必需 Inspector 引用、场景标识、Build Settings、配置表或资源注册项无效时，启动失败并保持应用在 `Initializing`。
- 初始化失败后不得调用 `NotifyInitializationReady`，不得加载任何应用场景，也不得使用缺省配置继续运行。
- 重复成功通知由 GameStateService 幂等拒绝，不能重复请求 MainMenuScene 或创建 MainMenu 定时器。
- 已完成初始化的服务在清理时按逆序释放；只清理本入口实际创建的对象和订阅。
- 全局服务不得长期持有已卸载 MainMenu、LevelSelect 或 Gameplay 场景对象的具体引用。
- 相同池化根类型只能注册一个规范 Prefab；Gameplay 重开时以相同 Prefab 请求已有类型池，不创建第二套空闲实例所有权。

## 非职责

- 不使用运行时搜索作为缺失 Inspector 引用的静默兜底。
- 不在 Bootstrap 中实现应用状态机、关卡终局、生成、对象重置或表现逻辑。
- 不为了统一访问而把 Gameplay Controller/Manager 注册为跨场景单例。
- 不为减少参数传递而暴露 `XxxService.Instance` 或全局可变服务集合。
- 不初始化 Deferred 服务或创建空占位实现。

## 验收标准

- 冷启动只存在一个 `GlobalRoot` 和一套全局服务。
- ConfigService 未 Ready 时应用始终处于 `Initializing`。
- 初始化成功只通知 GameStateService 一次。
- MainMenuScene 只能在全部服务完成 Connect、应用级事件订阅完成且 ConfigService Ready 后请求加载。
- 每个应用场景只存在一个规范根 SceneEntry；SceneEntry Ready 前不得推进对应稳定状态。
- Gameplay 重开不会创建重复服务，也不会遗留对已卸载场景对象的引用。
- 冷启动只绑定一个 `PersistentPoolRoot`；Gameplay 重开以相同具体类型和 Prefab 取得已有类型池，不创建第二个类型池或空闲 Root。
- MVP 启动层级中不存在 AudioRoot、SaveService 或 DebugService。
- 服务初始化与清理顺序可通过 EditMode 测试或测试替代实现复现。
- GameStateService 可以注入假的 Config、Scene、Time 和 EventBus 实现进行纯流程测试，不依赖静态全局状态。

## 关联文档

- `GlobalServices.md`
- `ApplicationFlow.md`
- `ConfigurationSystem.md`
- `SceneStructure.md`
- `AssemblyBoundaries.md`
- `../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
- `../06_Decisions/ADR-029-EventBusImplementationAndPayloads.md`
- `../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`
- `../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`
