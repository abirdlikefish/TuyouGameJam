# 全局服务

本文只记录由 `GlobalRoot` 在应用生命周期内持有、并由启动装配初始化的当前 MVP 全局服务。`GlobalBootstrap` 是 Composition Root，不是服务；场景级 Controller、Manager、表现适配器以及尚未启用的未来服务候选不进入本页服务清单，各自由启动装配、模块文档或 ADR 维护。

## 当前 MVP 全局服务

| 服务 | 作用 | 当前状态 | 详细入口 |
|---|---|---|---|
| `TimeService` | 固定倍率时间读取和 RealTime 定时器 | 必需（精简） | `TimeSystem.md` |
| `GameStateService` | 应用流程状态、游玩会话进入/退出和结果分发 | 必需 | `ApplicationFlow.md` |
| `EventBus` | 跨模块事实事件通信 | 必需 | `EventSystem.md` |
| `SceneService` | Gameplay 场景加载/卸载和入口绑定 | 必需（薄适配器） | `ApplicationFlow.md`、`SceneStructure.md` |
| `ConfigService` | 读取 `LevelCatalog`、选定的 `LevelConfig`、唯一的 Luban `cfg.Tables` 实例和资源注册表 | 必需 | `ConfigurationSystem.md` |
| `PoolService` | 高频对象复用 | 必需；部分语义 InDesign | `PoolSystem.md` |

## 实例所有权与访问方式

- MVP 必需服务在应用生命周期内各有一个活动实例，由唯一的 `GlobalBootstrap` / Composition Root 创建和持有。“唯一实例”是生命周期约束，不等同于每个类型实现静态 `Instance` 单例。
- 不要求每个服务对应一个 MonoBehaviour 或 GameObject。纯 C# 服务由 Composition Root 直接持有；确实需要 Unity 生命周期或场景 API 的适配器可以挂在 `ServiceHost`，但仍通过接口交给消费者。
- 服务和 Gameplay 模块不通过静态 `XxxService.Instance`、运行时 `Find` 或通用 Service Locator 隐式取得依赖。Composition Root 或 Gameplay 场景装配入口只在初始化时取得服务，并把最小接口注入实际消费者。
- 纯 C# 对象优先使用构造注入；Unity 场景组件使用明确的 `Initialize(...)` 或 Inspector 引用。配置、`LevelRunId`、生成位置等一次性上下文作为方法参数或初始化快照传递，不注册成全局服务。
- Bullet、Monster、Gate、Prop 等池对象由对应 Manager 初始化；池对象不得自行访问全局服务集合。Manager 负责传入本次会话所需的配置快照、ID 和回调。
- 现有静态 `LubanTables.Instance` 只可作为 Bootstrap 创建唯一 `cfg.Tables` 的过渡加载入口，Gameplay 模块不得直接访问它；运行时配置统一经 `IConfigService` 查询。

## 服务依赖与调用

`GlobalBootstrap` / Composition Root 是以下服务图的创建者，不是图中的服务节点：

```text
GlobalBootstrap / Composition Root
├── 创建 EventBus
├── 创建 TimeService
├── 创建 ConfigService(EventBus)
├── 创建 SceneService(EventBus)
├── 创建 GameStateService(ConfigService, SceneService, TimeService, EventBus)
└── 创建 PoolService(ResourceRegistry / 实例工厂，具体失败语义仍待定)
```

| 服务 | 直接依赖 | 原因 |
|---|---|---|
| `GameStateService` | `IConfigService`、`ISceneService`、`ITimeService`、`IEventBus` | 选关校验、场景命令、流程定时和事实发布 |
| `SceneService` | `IEventBus`、Unity 场景适配能力 | 加载/卸载后发布 Ready、Failed、Unloaded 事实；不回调具体 GameState 实现 |
| `ConfigService` | `IEventBus`、`LevelCatalog`、`cfg.Tables`、`IResourceRegistry` | 校验配置并发布失败事实；不依赖具体 Gameplay 模块 |
| `PoolService` | 资源注册表或实例工厂 | 只负责实例复用；不依赖 SpawnManager 或玩法状态 |
| `TimeService`、`EventBus` | 无业务服务依赖 | 作为基础叶节点，不反向依赖玩法或表现层 |

需要立即执行、返回值、失败结果或确定顺序的操作使用注入的类型化接口；已经发生且允许零个监听者的通知使用 `EventBus`。不得用事件伪装必须执行的命令，也不得为省略注入而引入静态单例。

## 生命周期

```text
Bootstrap → 创建服务 → 注册事件 → 加载场景 → 场景注册玩法对象
→ 场景卸载 → 清理场景对象 → 保留全局服务
```

## 约束

- 只有 `GlobalRoot`、Composition Root 和服务实例具有应用级生命周期；Gameplay Controller、Manager 和池对象均不得做成跨场景单例。
- MVP 服务初始化顺序必须固定：事件 → 时间 → 创建配置/场景服务 → 创建并注入状态服务 → 对象池 → 注册应用流程事件 → 初始化共享配置（LevelCatalog、Luban Tables + 资源注册表）→ 应用流程；具体 `LevelConfig` 在选关确定后交给 Gameplay。
- MVP 不创建或初始化 `AudioService`、`SaveService`、`DebugService`，也不创建音频根节点、音量设置或调试命令入口。
- 场景重载时不得创建重复的 `GlobalRoot`。
- MainMenu 和 LevelSelect 当前可以由 `GameStateService` 表示，不要求创建实际场景；离开这两个状态的自动跳过计时使用 RealTime。
- `ConfigService` 初始化失败时保持应用在 `Initializing`，并发布带错误码的 `InitializationFailed`；未成功初始化不得进入 MainMenu。
- `GlobalBootstrap` 在确认 `ConfigService.GetConfigLoadState() == Ready` 后调用 `GameStateService.NotifyInitializationReady()`；`GameStateService` 负责后续 `MainMenu`、`LevelSelect` 和 Gameplay 流程推进。
- MainMenu/LevelSelect 的 RealTime 定时器由 `GameStateService` 持有并在离开状态、加载失败或终局时取消；UI 不直接创建流程定时器。
- `SceneService` 只执行 `LoadGameplay` / `UnloadGameplay`，通过 `GameplaySceneReady`、`GameplaySceneLoadFailed` 和 `GameplaySceneUnloaded` 与 `GameStateService` 握手，不直接修改应用状态。
- `GameStateService` 与 `SceneService` 共同记录在 `ApplicationFlow.md`，但保持独立实现职责；前者拥有状态机，后者只适配 Unity 场景操作。

## 关联决策

- `../06_Decisions/ADR-017-DeferScoreAndSave.md`
- `../06_Decisions/ADR-019-ApplicationFlowContract.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`

## 关联文档

- `BootstrapAndComposition.md`
- `ApplicationFlow.md`
- `AssemblyBoundaries.md`
- `SceneStructure.md`
- `../03_SharedContracts/PublicInterfaces.md`
