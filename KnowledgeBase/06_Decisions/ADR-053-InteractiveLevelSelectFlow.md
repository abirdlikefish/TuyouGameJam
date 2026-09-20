# ADR-053：选关界面改为动态节点与显式关卡启动

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-011、ADR-032、ADR-044、ADR-051

## 背景

LevelSelectScene 当前只有固定根和 SceneEntry。`GameStateService` 在收到 `AppSceneReady(LevelSelect)` 后自动选择唯一关卡，使用 RealTime 等待 1 秒并开始 Gameplay。该临时流程无法显示关卡目录、区分锁定状态或让玩家主动选择关卡。

当前 LevelCatalog 仍只有关卡 `0`，本轮不增加或修改关卡配置，但选关 UI 必须按目录动态生成，以便后续增加关卡时不修改列表代码。

## 决策

1. LevelSelect Ready 后只进入 `AppFlowState.LevelSelect`，不再自动选择或启动关卡；GameStateService 移除应用页面计时器及其 ITimeService 依赖。
2. `IGameStateService` 增加 `IsLevelUnlocked(levelId)` 查询。LevelDescriptor 继续描述静态目录和初始解锁，GameStateService 的运行期集合是当前解锁状态权威来源。
3. LevelSelectView 按 ConfigService 返回的 LevelDescriptor 顺序，从单一节点 Prefab 动态创建 LevelSelectNodeView。节点不使用对象池，随场景初始化和清理。
4. 每个节点显示配置中的 DisplayName，依据当前解锁状态互斥显示 UnlockedState 与 LockedState；锁定节点按钮不可交互。
5. 节点只向 LevelSelectView 提交 LevelId。View 同步调用 `TrySelectLevel(levelId)` 和 `TryStartSelectedGameplay()`；GameStateService 在命令边界再次校验状态、解锁集合和配置。
6. 成功请求 Gameplay 后立即禁用全部节点，防止重复点击。失败时保持可交互并记录包含 LevelId 的诊断日志。
7. 节点图片、颜色、字体、布局和动画只由 Prefab、场景与 Inspector 控制，运行时代码不设置视觉样式。

## 后果

- 当前目录只生成一个“第 1 关”节点，并因初始解锁而可点击。
- 胜利解锁仍只保存在本次应用运行期；返回 LevelSelect 时重建节点并读取最新集合。退出应用后恢复为目录初始状态。
- 后续增加 LevelConfig 和 LevelCatalog 条目时，选关列表自动增加节点；本决策不引入存档、分页、星级或滚动虚拟化。
- MainMenu、SceneService、UnitySceneRuntime 和 Gameplay 加载协议不变。

## 验收

- LevelSelect 停留超过 1 秒不会自动进入 Gameplay。
- 当前配置只生成一个名称为“第 1 关”的已解锁节点，两个状态根互斥且按钮可交互。
- 点击节点只创建一个 LevelRunId 并加载关卡 `0`；重复点击不产生重复请求。
- 返回 LevelSelect 时节点重新创建一次，不残留旧监听或旧实例。
- 本轮无锁定关卡配置；锁定状态只做结构、绑定和逻辑验证。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../01_Architecture/ApplicationFlow.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/UI/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `../07_Changes/ChangeLog.md`
