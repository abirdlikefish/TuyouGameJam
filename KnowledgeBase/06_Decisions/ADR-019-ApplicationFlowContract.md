# ADR-019：应用流程公共接口与场景握手

## 状态

Accepted（Gameplay 专用场景接口与事件由 ADR-032 取代）

## 日期

2026-09-14

> 后续决策：ADR-032 将场景握手扩展到 MainMenu、LevelSelect、Gameplay，并以 `SwitchTo...` 命令及 `AppScene...` 事件取代本文的 Gameplay 专用命令和载荷；GameStateService 的唯一状态所有权、`GameplayLoading`、`LevelRunId` 校验和结果幂等规则继续有效。

## 背景

ADR-011 已确定初始化、主界面、选关、Gameplay、终局回选关的流程，但原有公共接口只有状态查询、终局提交和场景加载命令，缺少初始化成功通知、关卡选择、开始会话、场景就绪和场景卸载完成的边界。若直接实现，`GlobalBootstrap`、`GameStateService`、`SceneService` 和 `LevelManager` 可能各自推进状态，导致重复启动、过期会话或在场景尚未初始化时进入 `Gameplay`。

## 决策

### 流程所有权

- `GameStateService` 是应用级流程的唯一状态推进者，负责 `Initializing`、`MainMenu`、`LevelSelect`、`GameplayLoading` 和 `Gameplay` 的转换。
- `LevelManager` 只负责当前 Gameplay 会话的 `Preparing`、`Playing`、`Completed`，不得直接修改 `AppFlowState`。
- `GlobalBootstrap` 只负责创建服务、调用配置初始化，并在 `ConfigService` 状态为 `Ready` 后通知 `GameStateService`；不得直接跳过 MainMenu 或 LevelSelect。
- `SceneService` 只负责加载/卸载 Gameplay 场景和传递已校验的 `LevelConfig`、`LevelId`、`LevelRunId`，不得自行选择关卡或推进应用状态。

### 公共命令

`IGameStateService` 至少提供以下命令和查询：

```csharp
void NotifyInitializationReady();
bool TrySelectLevel(int levelId);
bool TryStartSelectedGameplay();
int GetCurrentLevelRunId();
void CompleteGameplay(LevelCompletion completion);
```

- `NotifyInitializationReady` 只接受 `ConfigService` 已为 `Ready` 的情况；重复通知幂等。
- `TrySelectLevel` 只允许在 `LevelSelect` 调用，并且只能选择 `LevelCatalog` 中有效且当前可选的关卡。
- `TryStartSelectedGameplay` 只允许在 `LevelSelect` 调用；它校验配置、创建递增的 `LevelRunId`，切换到 `GameplayLoading`，然后调用 `SceneService.LoadGameplay`。
- `GetCurrentLevelRunId` 返回当前加载中或 Gameplay 会话 ID；没有活动会话时返回无效值 `0`。
- `CompleteGameplay` 只接受当前 `LevelRunId` 且只处理一次；重复或过期结果必须忽略。

### 场景加载握手

`SceneService.LoadGameplay` 是异步边界命令。它必须发布以下二者之一：

```text
GameplaySceneReady
GameplaySceneLoadFailed
```

`GameplaySceneReady` 只能在 Gameplay 场景加载完成、入口 `LevelManager` 已收到相同的 `LevelId`、`LevelConfig`、`LevelRunId` 并完成 `Preparing` 初始化后发布。收到该事件后，`GameStateService` 才能从 `GameplayLoading` 切换到 `Gameplay`，并发布 `LevelRunStarted`。

加载失败时，`GameStateService` 必须清除待启动会话，保持或返回 `LevelSelect`，不得发布 `LevelRunStarted`，也不得创建 `LevelManager.Playing` 会话。

### 终局与卸载握手

1. `LevelManager` 在 `Completed` 状态停止本局逻辑、清理玩法对象，然后调用 `CompleteGameplay`。
2. `GameStateService` 校验结果后发布一次 `Victory` 或 `GameOver`，并调用 `SceneService.UnloadGameplay(LevelRunId)`。
3. `SceneService` 完成场景清理后发布 `GameplaySceneUnloaded`。
4. `GameStateService` 收到匹配的 `GameplaySceneUnloaded` 后清除当前会话，切换到 `LevelSelect`，并取消旧的自动跳转定时器。
5. `LevelSelect` 的 1 秒等待结束后再次调用 `TryStartSelectedGameplay`，生成新的 `LevelRunId`。

`Victory` / `GameOver` 是结果事实，不是应用状态；场景卸载和回到 `LevelSelect` 只能由 `GameStateService` 按上述顺序推进。

### 定时器

MainMenu 和 LevelSelect 的 RealTime 定时器由 `GameStateService` 持有 `TimerHandle`。状态离开、加载失败、终局或重复请求时必须取消旧句柄；定时器回调必须检查当前状态和会话 ID后再执行。

## 不采用

- 不允许 `SceneService`、`LevelManager` 或 UI 直接修改 `AppFlowState`。
- 不把场景加载中的 `Gameplay` 作为已可游玩的状态。
- 不让 `LevelManager` 直接调用 `UnloadGameplay` 或自行回到 LevelSelect。
- 不通过轮询场景对象是否存在代替 `GameplaySceneReady` / `GameplaySceneUnloaded` 握手。

## 影响

- `AppFlowState` 增加仅供流程内部使用的 `GameplayLoading`；它不是用户可见页面。
- `PublicInterfaces.md` 需要记录应用流程命令、当前会话查询和场景握手事件。
- `EventCatalog.md` 需要明确应用流程事件的发布时机、成功/失败事件和 `LevelRunId` 关联。
- 集成测试需要覆盖重复启动、场景加载失败、场景就绪后才进入 Gameplay、终局卸载完成后回选关和旧定时器失效。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/EventSystem.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-011-ApplicationFlowAndGameplaySession.md`
- `ADR-012-LevelCatalogConfigurationBootstrap.md`
