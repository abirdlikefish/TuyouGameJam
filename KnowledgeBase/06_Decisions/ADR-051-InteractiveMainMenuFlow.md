# ADR-051：主界面改为显式按钮驱动

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-011、ADR-019、ADR-032、ADR-050

> 后续决策：ADR-053 已将 LevelSelect 的临时自动流程替换为动态关卡节点和显式点击启动；本文关于 MainMenu 的决策保持有效。

## 背景

MainMenuScene 当前只有固定根和 SceneEntry。`GameStateService` 在收到 `AppSceneReady(MainMenu)` 后使用 RealTime 计时器等待 1 秒，再自动切换到 LevelSelectScene。该行为只用于正式 UI 接入前验证场景骨架，无法支持玩家停留在主界面、主动进入选关或退出应用。

本轮只接入主界面交互；LevelSelectScene 的 UI 和自动开始唯一关卡的临时流程不在本轮范围内。

## 决策

1. MainMenuScene Ready 后只进入 `AppFlowState.MainMenu`，不再创建自动跳转计时器。
2. `IGameStateService` 增加 `TryEnterLevelSelect()` 同步命令。仅当应用处于 MainMenu 且没有待处理场景切换时接受命令，并沿用现有 SceneService 切换链路。
3. MainMenu 使用场景内 `MainMenuView` 持有两个序列化 Button。开始按钮提交 `TryEnterLevelSelect()`；退出按钮在 Editor 停止 Play、在 Player 请求退出应用。
4. `MainMenuSceneEntry` 负责校验、初始化和清理 MainMenuView；View 在初始化和清理边界注册、注销按钮监听，不通过 Inspector 持久化事件、场景搜索或 Service Locator 取得服务。
5. UI 代码不设置图片、字体、颜色、锚点、尺寸和排版。视觉样式继续由 MainMenuScene 的 Inspector 资产承担。
6. LevelSelect Ready 后选择唯一关卡并等待 1 秒开始 Gameplay 的临时逻辑保持不变。

## 后果

- 应用会停留在主界面，直到玩家点击开始或退出。
- 开始按钮仍通过 GameStateService 的 pending target、失败事实和场景卸载边界切换，不直接调用 SceneManager。
- 本轮不会形成可交互选关；进入 LevelSelect 后仍沿用现有临时自动流程。
- MainMenu UI 引用缺失属于 SceneEntry 初始化失败，不使用运行时补组件或默认绑定。

## 验收

- 静态检查确认 MainMenu Ready 路径不再创建自动跳转计时器。
- MainMenuView 的两个 Button 和 MainMenuSceneEntry 引用均为显式序列化绑定。
- 编译通过，MainMenuView 不包含视觉样式赋值、场景搜索或直接场景加载。
- 本轮不进入 Play Mode；按钮点击、场景切换和 Player 退出行为保留为后续运行验收项。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../01_Architecture/ApplicationFlow.md`
- `../02_Modules/UI/README.md`
- `../07_Changes/ChangeLog.md`
