# ADR-011：应用流程与游玩会话生命周期

## 状态

Accepted（MainMenu/LevelSelect 无实际场景的部分由 ADR-032 取代）

## 日期

2026-09-14

> 后续决策：ADR-032 已将 MainMenu、LevelSelect 改为实际 Additive 场景并增加固定 SceneEntry；ADR-051 又以显式按钮命令取代 MainMenu 的 1 秒自动推进。应用状态所有权、LevelSelect 临时自动推进、单局状态和终局回选关继续有效。

## 背景

当前需要先实现一个最小但可扩展的应用流程：初始化、主界面、选关、实际游玩，以及游玩结束后回到选关。主界面和选关界面暂时没有交互内容，只等待 1 秒后自动进入下一阶段；当前只有一个关卡。

此前 ADR-009 规定终局后在游玩场景内等待 3 秒并直接重开当前关卡。该流程无法表达“游玩结束返回选关”，也会让应用级页面状态和单局终局状态重复。

## 决策

### 应用级流程

`GameStateService` 只管理应用流程状态：

```text
Initializing
  -> MainMenu
  -> LevelSelect
  -> GameplayLoading
  -> Gameplay
  -> LevelSelect
  -> Gameplay ...
```

- `Initializing` 只在全局服务、共享 Luban 数据和资源注册表成功后离开；初始化失败时保持错误状态并报告原因，不按计时器跳过。
- 首次进入 `MainMenu` 后使用 `RealTime` 等待 1 秒，再进入 `LevelSelect`。
- 当前只有一个关卡，进入 `LevelSelect` 时自动选择该关卡；使用 `RealTime` 等待 1 秒后请求进入 Gameplay。
- 进入 Gameplay 前必须完成游玩场景加载和本关 `LevelConfig` 初始化；场景加载中的状态仅作为内部过渡，不作为用户可见页面。
- 游玩结束后不返回 MainMenu，而是清理本局并回到 `LevelSelect`；选关等待 1 秒后再次开始同一关。
- MainMenu 和 LevelSelect 当前可以只作为流程状态存在，不要求创建实际 Unity 场景或交互 UI。
- 上述 1 秒自动跳转、终局回到 LevelSelect 并重开同一关属于临时应用串联流程，不定义一局内反复发生的核心玩法循环；核心玩法循环以 `ProjectOverview.md` 为准。
- 应用流程由 `GameStateService` 唯一推进；场景加载期间使用内部 `GameplayLoading` 状态，收到 `GameplaySceneReady` 后才进入 `Gameplay`。

`Victory` 和 `GameOver` 是游玩结果事实，不再作为 `GameStateService` 的应用状态。

### 单局游玩状态

`LevelManager` 只管理当前游玩会话：

```text
Preparing -> Playing -> Completed
```

`Completed` 携带 `LevelResult`（`Victory` 或 `GameOver`）。LevelManager 负责本局生成、玩法运行、同帧胜负优先级和结果幂等；结果交给 `GameStateService` 后，由应用流程负责离开 Gameplay。

### 会话与清理

- 每次进入 Gameplay 都创建新的 `LevelRunId`。
- 终局后停止输入、射击、攻击和未来生成，清理敌人、门、道具、子弹、生成游标和关卡计时。
- 全局服务和 `GlobalRoot` 保留；上一局的延迟回调或事件不得影响新的 `LevelRunId`。
- 不再使用游玩场景内固定 3 秒直接重开。

### 配置选择

初始化阶段加载共享 Luban Tables、资源注册表和关卡可选项；具体 `LevelConfig` 在 LevelSelect 确定后交给 Gameplay。当前只有一个关卡，因此默认选择唯一的 `LevelConfig`。

## 不采用

- 不把 `Victory`、`GameOver` 同时建模为应用状态和单局结果。
- 不在终局后留在 Gameplay 等待 3 秒并直接重置当前关卡。
- 不为暂时没有内容的 MainMenu、LevelSelect 强制创建独立场景。
- 不让初始化失败的流程通过固定 1 秒计时器继续推进。
- 不让场景加载失败或场景入口缺失的请求发布 `LevelRunStarted`。

## 影响

- `GameStateService`、`LevelManager`、`SceneService` 和 `ConfigService` 需要明确应用状态、单局状态、场景加载和配置选择的边界。
- `LevelRunState` 应使用 `Preparing`、`Playing`、`Completed`；`Victory`、`GameOver` 作为结果事件保留。
- MainMenu/LevelSelect 的 1 秒自动跳过必须使用 RealTime，并且离开状态时取消定时器。
- 集成测试需要覆盖首次进入、终局回选关、同一关新会话、清理和初始化失败不跳过。
- 公共流程命令、场景就绪/卸载握手和 `GameplayLoading` 状态见 ADR-019。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-009-FixedRoadSingleLevelTimeline.md`
- `ADR-012-LevelCatalogConfigurationBootstrap.md`
- `ADR-013-SpawnCursorOwnershipAndDispatch.md`
- `ADR-014-SharedRuntimeContractBaseline.md`
