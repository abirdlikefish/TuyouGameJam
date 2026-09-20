# ADR-032：MVP 应用场景入口、切换协议与分阶段初始化

## 状态

Accepted

## 日期

2026-09-18

> 后续决策：ADR-051 已用场景内 MainMenuView 和显式应用命令取代 MainMenu 的 1 秒自动推进；LevelSelect 临时自动推进及本 ADR 的场景入口、切换和初始化边界保持有效。

## 背景

ADR-011 和 ADR-019 将 MainMenu、LevelSelect 作为无实际 Unity 场景的临时流程状态，并只定义了 Gameplay 场景握手。进入工程实现前，需要先验证 `GlobalRoot` 常驻、场景入口解析、服务注入、同步加载、异步卸载、重复切换拒绝和场景监听清理。只加载 Gameplay 无法覆盖完整的页面切换链路，也没有给出各场景入口、文件布局和服务连接时机。

## 决策

### MainMenu、LevelSelect 和 Gameplay 都使用实际场景

MVP 创建并加入 Build Settings：

```text
BootstrapScene
MainMenuScene
LevelSelectScene
GameplayScene
```

`BootstrapScene` 常驻并持有唯一 `GlobalRoot`。其他三个应用场景使用 Additive 模式切换；同一时刻至多有一个非 Bootstrap 应用场景处于 Ready。`Initializing` 和 `GameplayLoading` 是流程状态，不创建同名场景。

`UnitySceneRuntime` 通过 Inspector 序列化 MainMenu、LevelSelect、Gameplay 的稳定场景标识并在 Connect 阶段验证 Build Settings；这些引用只标识要加载的场景，不尝试序列化尚未加载场景中的 SceneEntry 或其他组件。

MainMenu 和 LevelSelect 暂时不实现正式 UI，场景 Ready 后仍由 `GameStateService` 使用 RealTime 等待 1 秒自动推进。空页面场景的目的，是先验证场景生命周期和装配边界，不表示主界面或选关交互已经完成。

### 每个应用场景拥有固定根入口

三个可切换场景分别使用以下唯一根节点和入口组件：

```text
MainMenuScene/MainMenuRoot       [MainMenuSceneEntry]
LevelSelectScene/LevelSelectRoot [LevelSelectSceneEntry]
GameplayScene/GameplayRoot       [GameplaySceneEntry]
```

Unity 场景适配器只在本次加载返回的 `Scene` 根对象中按固定根名称解析入口，不使用跨场景 `GameObject.Find`、静态注册表或通用 Service Locator。根对象缺失、重名、入口组件缺失、入口类型不匹配或入口初始化失败都属于场景加载失败。

SceneEntry 只负责场景内装配：保存 Inspector 引用、接收本场景需要的最小服务和运行时上下文、初始化场景组件、建立订阅、报告 Ready，并在卸载前清理。SceneEntry 不选择下一场景、不推进 `AppFlowState`、不持有完整全局服务集合。`Awake`、`OnEnable` 和 `Start` 不得自行开始页面流程或 Gameplay；只有显式初始化成功后才可报告 Ready，Gameplay 仍需等待 `LevelRunStarted` 才进入 `Playing`。

### 控制方向

固定控制链为：

```text
GameStateService
→ ISceneService 的类型化切换命令
→ SceneService / Unity 场景适配器
→ 目标 SceneEntry
→ 场景内部组件
```

`GameStateService` 不持有或调用具体 SceneEntry。`SceneService` 不回调具体 `GameStateService`，而是在操作完成后发布 `AppSceneReady`、`AppSceneUnloaded`、`AppSceneLoadFailed` 或 `AppSceneUnloadFailed` 事实。`GameStateService` 只在收到与待处理目标匹配的 Ready 后推进稳定状态。

### 同步加载、异步卸载

- 加载使用同步 Additive 场景加载。同步 API 返回只表示 Unity 场景已加载，不表示 SceneEntry 已初始化。
- 旧应用场景先执行 Entry 清理，再使用异步卸载；只有收到卸载完成后才同步加载目标场景。
- 切换期间 SceneService 维护内部 `Idle`、`Unloading`、`Loading`、`InitializingEntry`、`Ready`、`CleaningFailedLoad` 状态并拒绝冲突请求；这些不是新的 `AppFlowState`。
- 加载后必须解析并初始化目标 SceneEntry；成功后发布 `AppSceneReady`。失败场景必须先完成异步清理，再发布 `AppSceneLoadFailed`，避免自动跳转在旧场景仍存在时发起下一次切换。
- 异步卸载失败时发布 `AppSceneUnloadFailed`，不得继续加载目标场景或伪造 Ready。
- 卸载 Gameplay 前将 Active Scene 恢复为 `BootstrapScene`；加载目标场景且入口准备成功后，可将目标场景设为 Active Scene。

### 场景切换接口与事件

MVP 使用三个明确的类型化命令，不引入字符串场景命令或 `object` 上下文：

```csharp
void SwitchToMainMenu();
void SwitchToLevelSelect();
void SwitchToGameplay(int levelId, LevelConfigSnapshot levelConfig, int levelRunId);
```

SceneService 内部持有当前应用场景和待切换目标。MainMenu、LevelSelect 的事件使用 `LevelId = 0`、`LevelRunId = 0`；Gameplay 事件必须携带当前值。应用场景事件统一携带 `AppSceneId`，取代 Gameplay 专用的 Ready、LoadFailed、Unloaded 载荷。

### 应用状态推进

- ConfigService Ready 后，`NotifyInitializationReady()` 请求切换 MainMenu；收到 `AppSceneReady(MainMenu)` 后才进入 `MainMenu` 并启动 1 秒定时器。
- MainMenu 定时器到期后请求切换 LevelSelect；收到 `AppSceneReady(LevelSelect)` 后才进入 `LevelSelect`、选择唯一关卡并启动 1 秒定时器。
- LevelSelect 定时器到期后校验配置、创建新 `LevelRunId`、进入 `GameplayLoading` 并请求切换 Gameplay；收到匹配的 `AppSceneReady(Gameplay)` 后才进入 `Gameplay` 并发布 `LevelRunStarted`。
- 终局结果只接受一次。GameStateService 发布 Victory/GameOver 后请求切换 LevelSelect；收到 `AppSceneReady(LevelSelect)` 后清除当前会话并进入 `LevelSelect`。
- MainMenu 到 LevelSelect 的切换期间公开状态保持 `MainMenu`；Gameplay 终局回选关期间公开状态保持 `Gameplay`，并用内部 pending target 与完成标记拒绝重复请求。只有 Gameplay 启动继续使用公开的 `GameplayLoading` 过渡状态。

目标 MainMenu 或 LevelSelect 加载失败时不进入目标状态、不创建自动跳转定时器，也不自动无限重试。Gameplay 加载失败时清除待启动会话并请求恢复 LevelSelect；只有 LevelSelect 再次 Ready 后才进入 `LevelSelect`。过期或目标不匹配的场景事实不得推进状态。

### 服务分三个阶段初始化

`GlobalBootstrap` 按以下屏障装配服务：

1. `Create`：按依赖顺序创建服务实例；纯 C# 服务优先通过构造注入取得已存在的最小接口，不发布业务事件、不启动定时器、不加载场景。
2. `Connect`：只连接 SceneEntry 装配桥接等构造时尚不可闭合的明确类型依赖，并绑定、校验场景名/Build Settings/序列化引用；不把所有服务改成可变 Setter 注入，所有 Connect 完成前不得启动任一服务。
3. `Start`：应用级服务先完成事件订阅，再初始化 ConfigService；ConfigService Ready 后才通知 GameStateService 请求 MainMenu。

不是每个服务都必须实现相同生命周期接口。无跨服务连接需求的 EventBus 等对象可以在构造后可用；阶段屏障由 Composition Root 维护。禁止用通用 `IServiceProvider`、服务字典或 Service Locator 代替 Connect。清理按 Start、Connect、Create 的逆序执行，并且只清理本入口实际完成的阶段。

### 首轮日志验收

场景骨架的首轮手动验收使用结构化 `Debug.Log`，至少记录场景切换请求、Entry 初始化、Ready、Entry 清理、Unloaded、应用状态变化和 Gameplay 的 `LevelId`/`LevelRunId`。日志只用于观察，不作为状态推进信号，也不因此创建 DebugService。

每个场景的一次加载必须恰好产生一次 Entry 初始化和一次 Ready；卸载必须恰好产生一次 Entry 清理和一次 Unloaded。后续仍需用 EditMode/PlayMode 测试验证幂等、失败和过期回调，日志不能替代自动化验收。

## MVP 文件与资源规划

```text
Assets/Scenes/
├── BootstrapScene.unity
├── MainMenuScene.unity
├── LevelSelectScene.unity
└── GameplayScene.unity

Assets/Scripts/Game/
├── Contracts/Application/
│   ├── IGameStateService.cs
│   ├── ISceneService.cs
│   └── ApplicationFlowTypes.cs
├── Contracts/Events/ApplicationEvents.cs
├── Services/Application/
│   ├── GameStateService.cs
│   └── SceneService.cs
└── Composition/
    ├── GlobalBootstrap.cs
    └── Scenes/
        ├── UnitySceneRuntime.cs
        ├── MainMenuSceneEntry.cs
        ├── LevelSelectSceneEntry.cs
        └── GameplaySceneEntry.cs

Assets/Tests/
├── EditMode/Application/
│   ├── GameStateServiceTests.cs
│   └── SceneServiceTests.cs
└── PlayMode/Application/
    └── ApplicationFlowPlayModeTests.cs
```

当前不创建 `.asmdef`。实际新增 `.cs`、场景和其他 Unity 资源时必须一并纳入对应 `.meta`，并在 Unity Editor 中验证序列化引用和 Build Settings。

## 不采用

- 不让 `GameStateService` 直接持有或控制 SceneEntry。
- 不让每个 `AppFlowState` 强制对应场景；`Initializing`、`GameplayLoading` 仍是流程状态。
- 不用跨场景 `GameObject.Find`、静态入口注册或 Service Locator 取得 SceneEntry。
- 不在 `LoadScene` 返回后、Entry 尚未准备完成时发布 Ready。
- 不在旧场景异步卸载完成前加载下一个应用场景。
- 不以 Debug.Log 作为流程控制、自动化验收或 DebugService 的替代实现。
- 不在当前 MVP 使用异步场景加载、进度条或可取消加载。

## 影响

- 本 ADR 取代 ADR-011 中“MainMenu 和 LevelSelect 不要求实际场景”的决定。
- 本 ADR 扩展并取代 ADR-019 中只覆盖 Gameplay 的场景接口和握手事件；`GameStateService` 的状态所有权、`LevelRunId` 校验和结果幂等规则继续有效。
- ApplicationFlow、SceneStructure、BootstrapAndComposition、GlobalServices、PublicInterfaces、EventCatalog、DataDictionary、IntegrationTests 和 Roadmap 需要同步。
- MVP 新增两个页面场景、三个 SceneEntry、Build Settings 配置和场景流程日志验收，但不代表正式 MainMenu/LevelSelect UI 已实现。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../00_Project/Roadmap.md`
- `../01_Architecture/ApplicationFlow.md`
- `../01_Architecture/BootstrapAndComposition.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/SceneStructure.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-011-ApplicationFlowAndGameplaySession.md`
- `ADR-019-ApplicationFlowContract.md`
- `ADR-027-MvpGlobalServiceScope.md`
