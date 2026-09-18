# 应用流程

## 目标与边界

本文件是 `GameStateService` 与 `SceneService` 的应用流程活文档。两个服务共同完成初始化后跳转、Gameplay 加载、终局卸载和重新开始，但保持独立职责：

- `GameStateService` 拥有应用状态、关卡选择、`LevelRunId`、流程定时器和结果推进。
- `SceneService` 是薄 Unity 场景适配器，负责 Gameplay 场景加载、入口绑定、卸载和场景结果事实发布。
- `GlobalBootstrap` 只创建、注入和初始化服务；`LevelManager` 只管理当前 Gameplay 会话。

二者由同一个 Composition 边界组装，可以记录在同一份流程文档中，但不合并为一个同时承担状态机与 Unity 场景操作的服务。

## 服务依赖

- `GameStateService` 通过注入的 `IConfigService` 查询并校验选定关卡，通过 `ITimeService` 创建 RealTime 流程定时器，通过 `ISceneService` 发出加载/卸载命令，并通过 `IEventBus` 发布应用状态和结果事实。
- `SceneService` 依赖 `IEventBus` 和 Unity 场景适配能力；它不持有具体 `GameStateService`，也不反向调用其实现类。
- `GameStateService` 订阅 SceneService 发布的 Ready、LoadFailed、Unloaded 事实，并校验其中的 `LevelRunId`。异步完成通知使用事件，但开始加载和卸载仍使用同步命令接口。
- `LevelManager` 只依赖 `IGameStateService` 提交终局，不直接依赖 `ISceneService`。场景入口向 LevelManager 注入已校验的 `LevelConfig` 和本局 ID，不要求 LevelManager 持有 ConfigService 或 SceneService 实现。

## 应用状态

| 状态 | 所有者 | 进入条件 | 离开条件 |
|---|---|---|---|
| `Initializing` | GameStateService | 应用启动 | ConfigService 为 `Ready`，收到一次有效初始化完成通知 |
| `MainMenu` | GameStateService | 初始化成功 | RealTime 1 秒定时器到期且状态仍匹配 |
| `LevelSelect` | GameStateService | MainMenu 跳转、加载失败或 Gameplay 卸载完成 | 已选择有效关卡并请求开始新会话 |
| `GameplayLoading` | GameStateService | 创建新 `LevelRunId` 并调用 SceneService | 收到匹配的 Ready 或 LoadFailed |
| `Gameplay` | GameStateService | 收到匹配的 `GameplaySceneReady` | 接受当前会话的 Victory 或 GameOver 结果 |

`Victory` 和 `GameOver` 是结果事实，不是额外的应用状态。Gameplay 单局内部的 `Preparing`、`Playing`、`Completed` 由 `LevelManager` 拥有。

## 启动与进入 Gameplay

```text
GlobalBootstrap 初始化 ConfigService
→ ConfigService Ready
→ GameStateService.NotifyInitializationReady()
→ MainMenu（RealTime 1 秒）
→ LevelSelect（选择唯一关卡，RealTime 1 秒）
→ GameStateService 创建新的 LevelRunId
→ GameplayLoading
→ SceneService.LoadGameplay(levelId, levelConfig, levelRunId)
→ 场景加载并完成 LevelManager.Preparing
→ GameplaySceneReady
→ GameStateService 进入 Gameplay 并发布 LevelRunStarted
```

`SceneService` 不选择关卡、不读取配置目录，也不推进 `AppFlowState`。它只接收已校验的 `LevelConfig`、`LevelId` 和 `LevelRunId`。

## 终局与卸载

```text
LevelManager 完成当前会话并停止玩法逻辑
→ GameStateService.CompleteGameplay(completion)
→ 校验 LevelRunId 且只接受一次
→ 发布 Victory 或 GameOver
→ SceneService.UnloadGameplay(levelRunId)
→ GameplaySceneUnloaded
→ GameStateService 清除当前会话并回到 LevelSelect
```

只有匹配当前会话的卸载完成事实才能推进流程。`LevelManager` 不直接卸载场景，`SceneService` 也不自行决定回到 LevelSelect。

## 失败、重复与过期处理

- 初始化失败时保持 `Initializing`，不得启动 MainMenu 定时器。
- 重复的 `NotifyInitializationReady`、开始请求和终局提交必须幂等，不能创建第二个会话或重复结果。
- 场景加载失败时发布 `GameplaySceneLoadFailed`，清除待启动会话并返回 `LevelSelect`；不得发布 `LevelRunStarted`。
- 所有场景成功、失败和卸载事实都必须校验 `LevelRunId`；过期回调不能影响新会话。
- 离开 MainMenu/LevelSelect、加载失败或终局时取消旧 `TimerHandle`；定时器回调执行前再次检查当前状态和会话上下文。
- 全局服务不得长期持有已卸载 Gameplay 场景对象的具体引用。

## 公共契约与验收

- 接口和载荷：`../03_SharedContracts/PublicInterfaces.md`
- 事实事件：`../03_SharedContracts/EventCatalog.md`
- 场景层级：`SceneStructure.md`
- 验收清单：`../05_Testing/IntegrationTests.md` 的“应用流程”部分
- 决策依据：`../06_Decisions/ADR-019-ApplicationFlowContract.md`、`../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
