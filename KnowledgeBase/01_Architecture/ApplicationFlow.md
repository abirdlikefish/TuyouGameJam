# 应用流程

## 状态

ContractReady（设计状态，不代表已有 Unity 实现）

## 目标与边界

本文件是 `GameStateService`、`SceneService` 与三个应用场景入口的应用流程活文档：

- `GameStateService` 拥有 `AppFlowState`、关卡选择、`LevelRunId`、流程定时器、待切换目标和结果推进。
- `SceneService` 协调 MainMenu、LevelSelect、Gameplay 三个应用场景的同步 Additive 加载、固定入口绑定和异步卸载，并发布场景事实。
- `MainMenuSceneEntry`、`LevelSelectSceneEntry`、`GameplaySceneEntry` 只负责各自场景内装配和清理，不选择下一场景或推进应用状态。
- `GlobalBootstrap` 只按 Create、Connect、Start 三阶段创建并连接服务；`LevelManager` 只管理当前 Gameplay 会话。

固定控制方向为：

```text
GameStateService
→ ISceneService 类型化切换命令
→ SceneService / UnitySceneRuntime
→ 目标 SceneEntry
→ 场景内部组件
```

`GameStateService` 不持有具体 SceneEntry，`SceneService` 也不回调具体 GameState 实现。异步完成通知通过事实事件返回。

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

当前不创建 `.asmdef`。实现时每个新增脚本和场景都必须包含 Unity 对应 `.meta`。

## 服务依赖

- `GameStateService` 通过 `IConfigService` 校验选关，通过 `ITimeService` 创建 RealTime 流程定时器，通过 `ISceneService` 发出类型化切换命令，并通过 `IEventBus` 发布应用状态和结果事实。
- GameStateService 在创建 Gameplay 会话时保留本次已校验 `LevelConfigSnapshot` 的只读结果数据；Victory 的 `UnlockedLevelIds` 从这里防御性复制，LevelManager 只提交精简 LevelCompletion。
- `SceneService` 依赖 `IEventBus` 和 Unity 场景适配能力；它持有当前场景、待切换目标和内部操作状态，不读取配置目录，不决定下一状态。
- `GameStateService` 订阅 `AppSceneReady`、`AppSceneUnloaded`、`AppSceneLoadFailed`、`AppSceneUnloadFailed`，并校验 `AppSceneId`、`LevelId`、`LevelRunId` 和内部 pending target；Unloaded 只用于确认旧场景事实，不直接推进稳定状态。
- `LevelManager` 只依赖 `IGameStateService` 提交终局。`GameplaySceneEntry` 向 LevelManager 注入已校验的 `LevelConfigSnapshot`、`LevelId`、`LevelRunId` 和最小服务接口。

## 应用状态

| 状态 | 所有者 | 进入条件 | 离开条件 |
|---|---|---|---|
| `Initializing` | GameStateService | 应用启动 | ConfigService Ready，MainMenuScene 的 Entry 完成初始化并收到 `AppSceneReady(MainMenu)` |
| `MainMenu` | GameStateService | MainMenuScene Ready | RealTime 1 秒定时器到期并请求切换 LevelSelect |
| `LevelSelect` | GameStateService | LevelSelectScene Ready | 已选择有效关卡并请求开始新会话 |
| `GameplayLoading` | GameStateService | 创建新 `LevelRunId` 并请求切换 Gameplay | 收到匹配的 `AppSceneReady(Gameplay)` 或场景失败事实 |
| `Gameplay` | GameStateService | GameplayScene Ready，随后发布 `LevelRunStarted` | 接受当前会话的 Victory 或 GameOver，并请求切换 LevelSelect |

`Initializing` 和 `GameplayLoading` 是流程状态，不对应同名场景。MainMenu 到 LevelSelect 的切换期间公开状态保持 `MainMenu`；终局回选关期间公开状态保持 `Gameplay`，并用内部完成标记拒绝重复结果。只有目标 SceneEntry Ready 后才进入目标稳定状态。

`Victory` 和 `GameOver` 是结果事实，不是额外应用状态。Gameplay 单局内部的 `Preparing`、`Playing`、`Completed` 由 `LevelManager` 拥有。

## 场景入口协议

```text
MainMenuScene/MainMenuRoot       [MainMenuSceneEntry]
LevelSelectScene/LevelSelectRoot [LevelSelectSceneEntry]
GameplayScene/GameplayRoot       [GameplaySceneEntry]
```

- UnitySceneRuntime 只在本次加载的 `Scene.GetRootGameObjects()` 中按固定根名称解析唯一入口，不执行跨场景 `GameObject.Find`。
- 根缺失、重名、组件缺失、入口类型错误或 Entry 初始化失败都属于加载失败。
- SceneEntry 通过 Inspector 保存本场景固定引用；运行时服务和会话数据通过显式初始化传入。
- SceneEntry 的 `Awake`、`OnEnable`、`Start` 不得自行推进应用状态。Gameplay 对象在 Entry 初始化后仍保持 `Preparing`，只在 `LevelRunStarted` 后进入 `Playing`。
- Entry 初始化成功且场景监听者完成订阅后，SceneService 才能发布 `AppSceneReady`。
- 卸载前先调用 Entry 清理入口，取消订阅和场景定时任务；异步卸载完成后发布 `AppSceneUnloaded`。

## 加载与卸载策略

- `BootstrapScene` 始终保留；MainMenu、LevelSelect、Gameplay 使用 Additive 模式。
- 目标场景使用同步加载。`LoadScene` 返回只说明 Unity 已加载场景，不代表 Entry Ready。
- 从已有应用场景切换时，先清理并异步卸载旧场景，卸载完成后再同步加载目标场景；不得同时保留两个 Ready 的应用场景。
- SceneService 内部使用 `Idle`、`Unloading`、`Loading`、`InitializingEntry`、`Ready`、`CleaningFailedLoad` 拒绝冲突请求；这些状态不进入公共 `AppFlowState`。
- 目标加载或 Entry 初始化失败时先异步清理失败场景，再发布 `AppSceneLoadFailed`。卸载失败发布 `AppSceneUnloadFailed`，不得继续加载目标场景。
- 目标 Entry Ready 后可把目标场景设为 Active Scene；卸载当前应用场景前先把 Active Scene 恢复为 BootstrapScene。

## 启动、主界面与选关

```text
GlobalBootstrap 完成 Create 与 Connect
→ Start 阶段先注册应用级事件
→ ConfigService.Initialize
→ ConfigService Ready
→ GameStateService.NotifyInitializationReady()
→ SceneService.SwitchToMainMenu()
→ 同步加载 MainMenuScene，初始化 MainMenuSceneEntry
→ AppSceneReady(MainMenu)
→ GameStateService 进入 MainMenu，启动 RealTime 1 秒定时器
→ SceneService 异步卸载 MainMenuScene
→ 同步加载 LevelSelectScene，初始化 LevelSelectSceneEntry
→ AppSceneReady(LevelSelect)
→ GameStateService 进入 LevelSelect、选择唯一关卡并启动 RealTime 1 秒定时器
```

MainMenu 和 LevelSelect 当前仍自动跳过，没有正式 UI。计时从对应 Scene Ready 后开始，而不是从加载请求时开始。

## 进入 Gameplay

```text
LevelSelect 定时器到期
→ GameStateService 校验选中关卡并创建新 LevelRunId
→ GameplayLoading
→ SceneService.SwitchToGameplay(levelId, levelConfigSnapshot, levelRunId)
→ 异步卸载 LevelSelectScene
→ 同步加载 GameplayScene
→ GameplaySceneEntry 注入依赖并完成 LevelManager.Preparing
→ AppSceneReady(Gameplay, levelId, levelRunId)
→ GameStateService 校验 pending target 和 LevelRunId
→ 进入 Gameplay 并发布 LevelRunStarted
→ LevelManager 进入 Playing
```

`SceneService` 不选择关卡、不读取配置目录。它只接收已校验的 `LevelConfigSnapshot`、`LevelId` 和 `LevelRunId`。

## 终局与回到选关

```text
LevelManager 完成当前会话并停止玩法逻辑
→ GameStateService.CompleteGameplay(completion)
→ 校验 LevelRunId 且只接受一次
→ 发布 Victory 或 GameOver
→ SceneService.SwitchToLevelSelect()
→ GameplaySceneEntry 清理当前会话
→ 异步卸载 GameplayScene
→ 同步加载并初始化 LevelSelectSceneEntry
→ AppSceneReady(LevelSelect)
→ GameStateService 清除当前会话并进入 LevelSelect
→ 启动新的 RealTime 1 秒定时器
```

`LevelManager` 不直接切换或卸载场景，`SceneService` 不自行决定回到 LevelSelect。

## 失败、重复与过期处理

- Luban 表、LevelCatalog 或 LevelConfig 数据初始化失败时，ConfigService 按 ADR-041 记录首个错误并立即退出应用，不得请求 MainMenuScene。其他 Bootstrap/场景装配失败不得伪装初始化成功。
- 重复的初始化通知、切换请求、定时器回调和终局提交必须幂等，不能创建第二个会话或重复结果。
- MainMenuScene 加载失败时保持 `Initializing`；LevelSelectScene 加载失败时不进入 `LevelSelect`，也不启动选关定时器。二者都不自动无限重试。
- GameplayScene 加载失败时不得发布 `LevelRunStarted`；GameStateService 清除待启动会话并请求恢复 LevelSelectScene，只有 `AppSceneReady(LevelSelect)` 后才进入 `LevelSelect`。
- `AppSceneUnloadFailed` 会停止当前切换，保留可诊断状态；不得在旧场景仍存在时加载目标场景。
- 所有场景事实必须匹配当前 pending target；Gameplay 事实还必须匹配当前 `LevelRunId`。过期事实不能影响新会话。
- 离开稳定状态、目标加载失败、终局或会话失效时取消旧 `TimerHandle`；回调执行前再次检查当前状态和 pending target。
- 全局服务不得长期持有已卸载场景对象的具体引用。

## 首轮日志验收

场景骨架阶段允许各入口和服务使用结构化 `Debug.Log` 记录请求、Entry 初始化、Ready、Entry 清理、Unloaded 和 `AppFlowChanged`。Gameplay 日志必须包含 `LevelId` 与 `LevelRunId`。日志只用于观察，不能作为流程信号，也不因此引入 DebugService。

一次场景加载必须恰好产生一次 Entry 初始化和一次 Ready；一次卸载必须恰好产生一次 Entry 清理和一次 Unloaded。完整验收仍以 EditMode/PlayMode 测试为准。

## 公共契约与验收

- 接口和载荷：`../03_SharedContracts/PublicInterfaces.md`
- 事实事件：`../03_SharedContracts/EventCatalog.md`
- 场景层级：`SceneStructure.md`
- 验收清单：`../05_Testing/IntegrationTests.md` 的“应用流程”部分
- 决策依据：`../06_Decisions/ADR-019-ApplicationFlowContract.md`、`../06_Decisions/ADR-027-MvpGlobalServiceScope.md`、`../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`
