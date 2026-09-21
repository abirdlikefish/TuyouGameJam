# 应用流程

## 状态

Integration（批次 7.5C 已完成实际场景、Build Settings、Inspector 装配和多轮流程冒烟；专项手工验收待 7.6）

## 目标与边界

本文件是 `GameStateService`、`SceneService` 与三个应用场景入口的应用流程活文档：

- `GameStateService` 拥有 `AppFlowState`、运行期解锁集合、关卡选择、`LevelRunId`、待切换目标和结果推进。
- `SceneService` 协调 MainMenu、LevelSelect、Gameplay 三个应用场景的异步 Additive 加载、固定入口绑定和异步卸载，并发布场景事实。
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

未来正式程序集阶段：
Assets/Tests/
├── EditMode/Application/
│   ├── GameStateServiceTests.cs
│   └── SceneServiceTests.cs
└── PlayMode/Application/
    └── ApplicationFlowPlayModeTests.cs
```

当前不创建任何 `.asmdef`、测试程序集或自动测试代码。首轮按结构化日志和 `05_Testing` 清单手工验证；正式程序集阶段再创建上述测试。实现时每个新增脚本和场景都必须包含 Unity 对应 `.meta`。

## 服务依赖

- `GameStateService` 通过 `IConfigService` 校验选关，通过 `ISceneService` 发出类型化切换命令，并通过 `IEventBus` 发布应用状态和结果事实。
- GameStateService 在创建 Gameplay 会话时保留本次已校验 `LevelConfigSnapshot` 的只读结果数据；Victory 的 `UnlockedLevelIds` 从这里防御性复制，LevelManager 只提交精简 LevelCompletion。该集合已由 ConfigService 按 ADR-044 过滤当前目录中不存在的未来关卡 ID，应用流程不回读原始 LevelConfig，也不重复警告或过滤。
- `SceneService` 依赖 `IEventBus` 和 Unity 场景适配能力；它持有当前场景、待切换目标和内部操作状态，不读取配置目录，不决定下一状态。
- `GameStateService` 订阅 `AppSceneReady`、`LevelIntroFinished`、`AppSceneUnloaded`、`AppSceneLoadFailed`、`AppSceneUnloadFailed`，并校验 `AppSceneId`、`LevelId`、`LevelRunId` 和内部 pending target；Unloaded 只用于确认旧场景事实，不直接推进稳定状态。
- `LevelManager` 只依赖 `IGameStateService` 提交终局。`GameplaySceneEntry` 向 LevelManager 注入已校验的 `LevelConfigSnapshot`、`LevelId`、`LevelRunId` 和最小服务接口。

## 应用状态

| 状态 | 所有者 | 进入条件 | 离开条件 |
|---|---|---|---|
| `Initializing` | GameStateService | 应用启动 | ConfigService Ready，MainMenuScene 的 Entry 完成初始化并收到 `AppSceneReady(MainMenu)` |
| `MainMenu` | GameStateService | MainMenuScene Ready | 玩家点击开始，`TryEnterLevelSelect()` 接受请求并切换 LevelSelect |
| `LevelSelect` | GameStateService | LevelSelectScene Ready | 玩家点击已解锁节点，选择有效关卡并请求开始新会话 |
| `GameplayLoading` | GameStateService | 创建新 `LevelRunId` 并请求切换 Gameplay | 场景 Ready 后收到匹配的 `LevelIntroFinished`，或收到场景失败事实 |
| `Gameplay` | GameStateService | GameplayScene Ready 且开场门禁结束，随后发布 `LevelRunStarted` | 接受当前会话的 Victory 或 GameOver 后进入 `GameplayResult`，或玩家主动放弃并请求返回 LevelSelect |
| `GameplayResult` | GameStateService | 当前 Gameplay 结果被接受、解锁处理完成 | 玩家返回选关，或点击失败重试/胜利下一关并进入新的 `GameplayLoading` |

`Initializing` 和 `GameplayLoading` 是流程状态，不对应同名场景。MainMenu 到 LevelSelect 的切换期间公开状态保持 `MainMenu`；Gameplay Ready 后在开场视频终止前仍保持 `GameplayLoading`；Gameplay 主动退出期间保持 `Gameplay`，结算返回选关期间保持 `GameplayResult`，重试或下一关请求被接受后立即进入新的 `GameplayLoading`。

`Victory` 和 `GameOver` 仍是结果事实；`GameplayResult` 只表达结果界面正在等待玩家返回。Gameplay 单局内部的 `Preparing`、`Playing`、`Completed` 继续由 `LevelManager` 拥有。

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
- GameplaySceneEntry 只在匹配的 Ready 后开始开场视频；视频正常完成或安全终止后发布 `LevelIntroFinished`，不直接启动 LevelManager。
- 卸载前先调用 Entry 清理入口，取消订阅和场景定时任务；异步卸载完成后发布 `AppSceneUnloaded`。

## 加载与卸载策略

- `BootstrapScene` 始终保留；MainMenu、LevelSelect、Gameplay 使用 Additive 模式。
- 目标场景使用 `LoadSceneAsync` Additive 加载；只有 AsyncOperation 完成、固定根解析和 Entry 初始化全部成功后才发布 Ready。
- 从已有应用场景切换时，先清理并异步卸载旧场景，卸载完成后再异步加载目标场景；不得同时保留两个 Ready 的应用场景。
- SceneService 内部使用 `Idle`、`Unloading`、`Loading`、`Ready`、`CleaningFailedLoad`、`Faulted` 拒绝冲突请求；这些状态不进入公共 `AppFlowState`。
- 目标加载或 Entry 初始化失败时先异步清理失败场景，再发布 `AppSceneLoadFailed`。卸载失败发布 `AppSceneUnloadFailed`，不得继续加载目标场景。
- 目标 Entry Ready 后可把目标场景设为 Active Scene；卸载当前应用场景前先把 Active Scene 恢复为 BootstrapScene。

## 启动、主界面与选关

```text
GlobalBootstrap 完成 Create 与 Connect
→ Start 阶段先注册应用级事件
→ ConfigService.Initialize
→ ConfigService Ready
→ GameStateService.NotifyInitializationReady()
→ JsonPlayerProgressStore 读取存档，GameStateService 合并默认/已完成/已解锁集合
→ SceneService.SwitchToMainMenu()
→ 异步加载 MainMenuScene，完成后初始化 MainMenuSceneEntry
→ AppSceneReady(MainMenu)
→ GameStateService 进入 MainMenu 并等待玩家输入
→ MainMenuView 点击开始并调用 TryEnterLevelSelect()
→ SceneService 异步卸载 MainMenuScene
→ 异步加载 LevelSelectScene，完成后初始化 LevelSelectSceneEntry
→ AppSceneReady(LevelSelect)
→ GameStateService 进入 LevelSelect 并等待节点点击
```

MainMenu 使用场景内序列化 Button 和 `MainMenuView` 提交同步应用命令，不直接调用 SceneManager；退出按钮在 Editor 停止 Play，在 Player 请求退出。LevelSelectView 初始化 Inspector 中与目录 LevelId 一一对应的预放节点，查询 GameStateService 的运行期完成/解锁状态并等待玩家点击；节点位置由各自 RectTransform 决定。

## 进入 Gameplay

```text
玩家点击已解锁关卡节点
→ LevelSelectView 请求选择并开始该关卡
→ GameStateService 再次校验解锁状态与配置并创建新 LevelRunId
→ GameplayLoading
→ SceneService.SwitchToGameplay(levelId, levelConfigSnapshot, levelRunId)
→ 异步卸载 LevelSelectScene
→ 异步加载 GameplayScene
→ GameplaySceneEntry 注入依赖并完成 LevelManager.Preparing
→ AppSceneReady(Gameplay, levelId, levelRunId)
→ GameStateService 校验 pending target 和 LevelRunId
→ GameplaySceneEntry 播放当前 LevelId 绑定的开场视频；未绑定、失败或准备超时则安全终止
→ LevelIntroFinished(levelId, levelRunId, reason)
→ GameStateService 再次校验当前场景和会话，进入 Gameplay 并发布 LevelRunStarted
→ LevelManager 进入 Playing
```

`SceneService` 不选择关卡、不读取配置目录。它只接收已校验的 `LevelConfigSnapshot`、`LevelId` 和 `LevelRunId`。

## 终局与回到选关

```text
LevelManager 完成当前会话并停止玩法逻辑
→ GameStateService.CompleteGameplay(completion)
→ 校验 LevelRunId 且只接受一次
→ 胜利时记录当前关卡完成、更新运行期解锁集合并同步保存变化后的进度
→ 进入 GameplayResult 并发布 Victory 或 GameOver
→ BattleResult 根据结果和 UnlockedLevelIds 显示唯一匹配的结算根节点
→ 玩家选择返回选关、失败重试或挑战下一关
→ GameStateService 校验命令并请求对应场景切换
→ GameplaySceneEntry 清理当前会话
→ 返回路径异步加载 LevelSelectScene；重试/下一关路径使用新 LevelRunId 异步重新加载 GameplayScene
```

战斗中 HUD 也可通过 `TryReturnToLevelSelect()` 主动放弃本局；该路径不提交 `LevelCompletion`、不发布胜负且不解锁。结算后的 `TryRetryCurrentGameplay()` 只在失败结果有效，目标仍为当前关；`TryStartNextGameplay()` 只在胜利且当前快照存在有效 `UnlockedLevelIds[0]` 时有效。两者都创建新的 `LevelRunId`、进入 `GameplayLoading` 并重新加载 GameplayScene。`LevelManager` 不直接切换或卸载场景，`SceneService` 不决定重试或下一关目标。

## 失败、重复与过期处理

- Luban 表、LevelCatalog 或 LevelConfig 数据初始化失败时，ConfigService 按 ADR-041 记录首个错误并立即退出应用，不得请求 MainMenuScene。ADR-044 允许的 `unlockedLevelIds` 目录缺失 ID 只警告并过滤，不进入失败流程。其他 Bootstrap/场景装配失败不得伪装初始化成功。
- 重复的初始化通知、切换请求、节点点击和终局提交必须幂等，不能创建第二个会话或重复结果。
- MainMenuScene 或 LevelSelectScene 加载失败时不进入目标稳定状态，也不自动无限重试。
- GameplayScene 加载失败时不得发布 `LevelRunStarted`；GameStateService 清除待启动会话并请求恢复 LevelSelectScene，只有 `AppSceneReady(LevelSelect)` 后才进入 `LevelSelect`。
- 开场视频未绑定、运行时解码失败或准备超时不得永久阻塞；GameplaySceneEntry 记录原因并发布一次 `LevelIntroFinished`。过期或重复完成事实不得启动当前或下一局。
- `AppSceneUnloadFailed` 会停止当前切换，保留可诊断状态；不得在旧场景仍存在时加载目标场景。
- 所有场景事实必须匹配当前 pending target；Gameplay 事实还必须匹配当前 `LevelRunId`。过期事实不能影响新会话。
- 每个 UI 命令执行前再次检查当前状态和 pending target；过期或重复点击不得推进流程。
- 全局服务不得长期持有已卸载场景对象的具体引用。
- 关卡进度不存在或不可读时使用 `initiallyUnlocked` 默认集合继续启动；保存失败只记录错误并保留当前运行期状态，不阻断结果展示。

## 首轮日志验收

场景骨架阶段允许各入口和服务使用结构化 `Debug.Log` 记录请求、Entry 初始化、Ready、Entry 清理、Unloaded 和 `AppFlowChanged`。Gameplay 日志必须包含 `LevelId` 与 `LevelRunId`。日志只用于观察，不能作为流程信号，也不因此引入 DebugService。

一次场景加载必须恰好产生一次 Entry 初始化和一次 Ready；一次卸载必须恰好产生一次 Entry 清理和一次 Unloaded。当前以结构化日志和完整手工流程验收；正式程序集阶段再补 EditMode/PlayMode 回归测试，见 ADR-045。

## 公共契约与验收

- 接口和载荷：`../03_SharedContracts/PublicInterfaces.md`
- 事实事件：`../03_SharedContracts/EventCatalog.md`
- 场景层级：`SceneStructure.md`
- 验收清单：`../05_Testing/IntegrationTests.md` 的“应用流程”部分
- 决策依据：`../06_Decisions/ADR-019-ApplicationFlowContract.md`、`../06_Decisions/ADR-027-MvpGlobalServiceScope.md`、`../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`、`../06_Decisions/ADR-050-UnitySceneLoadCompletionBoundary.md`、`../06_Decisions/ADR-053-InteractiveLevelSelectFlow.md`
- 当前测试策略：`../05_Testing/TestingStrategy.md`、`../06_Decisions/ADR-045-DeferAutomatedTestsUntilAssemblyDefinitions.md`
