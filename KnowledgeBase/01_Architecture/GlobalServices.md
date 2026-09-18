# 全局服务

本文只记录由 `GlobalRoot` 在应用生命周期内持有、并由启动装配初始化的当前 MVP 全局服务。`GlobalBootstrap` 是 Composition Root，不是服务；场景级 Controller、Manager、表现适配器以及尚未启用的未来服务候选不进入本页服务清单，各自由启动装配、模块文档或 ADR 维护。

## 当前 MVP 全局服务

| 服务 | 作用 | 当前状态 | 详细入口 |
|---|---|---|---|
| `TimeService` | 固定倍率时间读取和 RealTime 定时器 | 必需（精简） | `TimeSystem.md` |
| `GameStateService` | 应用流程状态、游玩会话进入/退出和结果分发 | 必需 | `ApplicationFlow.md` |
| `EventBus` | 跨模块事实事件通信 | 必需 | `EventSystem.md` |
| `SceneService` | MainMenu、LevelSelect、Gameplay 场景切换、入口绑定和卸载协调 | 必需（薄协调器） | `ApplicationFlow.md`、`SceneStructure.md` |
| `ConfigService` | 读取 `LevelCatalog`、选定的 `LevelConfig`、唯一的 Luban `cfg.Tables` 实例和资源注册表 | 必需 | `ConfigurationSystem.md` |
| `PoolService` | 按具体 MonoBehaviour 类型持有和复用实例 | 必需（ContractReady） | `PoolSystem.md` |

## 实例所有权与访问方式

- MVP 必需服务在应用生命周期内各有一个活动实例，由唯一的 `GlobalBootstrap` / Composition Root 创建和持有。“唯一实例”是生命周期约束，不等同于每个类型实现静态 `Instance` 单例。
- 不要求每个服务对应一个 MonoBehaviour 或 GameObject。纯 C# 服务由 Composition Root 直接持有；确实需要 Unity 生命周期或场景 API 的适配器可以挂在 `ServiceHost`，但仍通过接口交给消费者。
- 服务和 Gameplay 模块不通过静态 `XxxService.Instance`、运行时 `Find` 或通用 Service Locator 隐式取得依赖。Composition Root 或 Gameplay 场景装配入口只在初始化时取得服务，并把最小接口注入实际消费者。
- 纯 C# 对象优先使用构造注入；Unity 场景组件使用明确的 `Initialize(...)` 或 Inspector 引用。配置、`LevelRunId`、生成位置等一次性上下文作为方法参数或初始化快照传递，不注册成全局服务。
- Bullet、Monster、Gate、Prop 等池对象由对应 Manager 初始化；池对象不得自行访问全局服务集合或持有类型池。Manager 负责传入本次会话所需的配置快照、ID 和窄回调，并在业务清理后归还类型池。
- 现有静态 `LubanTables.Instance` 只可作为 Bootstrap 创建唯一 `cfg.Tables` 的过渡加载入口，Gameplay 模块不得直接访问它；运行时配置统一经 `IConfigService` 查询。

## 服务依赖与调用

`GlobalBootstrap` / Composition Root 是以下服务图的创建者，不是图中的服务节点：

```text
GlobalBootstrap / Composition Root
├── 创建 EventBus
├── 创建 TimeService
├── 创建 ConfigService(EventBus)
├── 创建 SceneService(EventBus, UnitySceneRuntime)
├── 创建 GameStateService(ConfigService, SceneService, TimeService, EventBus)
└── 创建 PoolService(PersistentPoolRoot, 诊断委托)
```

| 服务 | 直接依赖 | 原因 |
|---|---|---|
| `GameStateService` | `IConfigService`、`ISceneService`、`ITimeService`、`IEventBus` | 选关校验、场景命令、流程定时和事实发布 |
| `SceneService` | `IEventBus`、UnitySceneRuntime | 同步加载、固定入口绑定和异步卸载后发布 Ready、Failed、Unloaded 事实；不回调具体 GameState 实现 |
| `ConfigService` | `IEventBus`、`LevelCatalog`、`cfg.Tables`、`IResourceRegistry` | 校验配置并发布失败事实；不依赖具体 Gameplay 模块 |
| `PoolService` | `PersistentPoolRoot`、可选诊断委托 | 按具体类型持有类型池和空闲实例；不依赖资源注册表、SpawnManager 或玩法状态 |
| `TimeService`、`EventBus` | 无业务服务依赖 | 作为基础叶节点，不反向依赖玩法或表现层 |

需要立即执行、返回值、失败结果或确定顺序的操作使用注入的类型化接口；已经发生且允许零个监听者的通知使用 `EventBus`。不得用事件伪装必须执行的命令，也不得为省略注入而引入静态单例。

## 生命周期

```text
Bootstrap → Create 服务 → Connect 明确依赖 → Start 并注册事件
→ MainMenu/LevelSelect/Gameplay 场景依次加载与入口初始化
→ 异步卸载并清理场景对象 → 保留全局服务
```

## 约束

- 只有 `GlobalRoot`、Composition Root 和服务实例具有应用级生命周期；Gameplay Controller、Manager 和池对象均不得做成跨场景单例。
- MVP 服务初始化使用固定屏障：Create 创建实例且不产生业务副作用；Connect 注入明确类型依赖并校验场景与序列化配置；Start 先注册应用流程事件，再初始化共享配置并启动应用流程。具体 `LevelConfig` 在选关确定后交给 Gameplay。
- MVP 不创建或初始化 `AudioService`、`SaveService`、`DebugService`，也不创建音频根节点、音量设置或调试命令入口。
- 场景重载时不得创建重复的 `GlobalRoot`。
- MainMenu、LevelSelect 和 Gameplay 都使用实际 Additive 场景及固定根 SceneEntry；`Initializing`、`GameplayLoading` 仍只是流程状态。MainMenu/LevelSelect 场景 Ready 后的自动跳过计时使用 RealTime。
- `ConfigService` 初始化失败时保持应用在 `Initializing`，并发布带错误码的 `InitializationFailed`；未成功初始化不得进入 MainMenu。
- `GlobalBootstrap` 在确认 `ConfigService.GetConfigLoadState() == Ready` 后调用 `GameStateService.NotifyInitializationReady()`；`GameStateService` 负责后续 `MainMenu`、`LevelSelect` 和 Gameplay 流程推进。
- MainMenu/LevelSelect 的 RealTime 定时器由 `GameStateService` 持有并在离开状态、加载失败或终局时取消；UI 不直接创建流程定时器。
- `SceneService` 执行 `SwitchToMainMenu`、`SwitchToLevelSelect`、`SwitchToGameplay`，使用同步 Additive 加载、固定根入口绑定和异步卸载，并通过 `AppSceneReady`、`AppSceneLoadFailed`、`AppSceneUnloaded`、`AppSceneUnloadFailed` 与 `GameStateService` 握手；它不直接修改应用状态。
- `GameStateService` 不持有具体 SceneEntry；SceneEntry 只负责场景内部装配，不能选择下一场景或推进 `AppFlowState`。
- `GameStateService` 与 `SceneService` 共同记录在 `ApplicationFlow.md`，但保持独立实现职责；前者拥有状态机，后者只适配 Unity 场景操作。
- PoolService 以具体池化根组件类型作为池身份；一个类型只绑定一个规范 Prefab。Manager 通过 Inspector 提供 Prefab 并取得类型池，池借出未激活实例，归还时采用防御性失活。

## 关联决策

- `../06_Decisions/ADR-017-DeferScoreAndSave.md`
- `../06_Decisions/ADR-019-ApplicationFlowContract.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
- `../06_Decisions/ADR-029-EventBusImplementationAndPayloads.md`
- `../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`
- `../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`

## 关联文档

- `BootstrapAndComposition.md`
- `ApplicationFlow.md`
- `AssemblyBoundaries.md`
- `SceneStructure.md`
- `../03_SharedContracts/PublicInterfaces.md`
