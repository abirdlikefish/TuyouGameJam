# UI 模块

## 模块信息

- ID：`MOD-UI`
- 层级：Presentation
- 状态：`Planned`
- 依赖：EventBus、GameStateService

## 职责

- 显示军队总人数、激活槽位数（可选显示各槽位代表人数）、门数字、敌人生成/击杀进度和关卡计时。
- 显示胜利、失败和重开界面；暂停界面属于后续扩展。
- 监听状态和数值事件。

当前 MVP 不要求实现 MainMenu、LevelSelect 或结果界面的实际交互；这些界面先由应用流程状态和 RealTime 自动跳过计时表示。`Victory`、`GameOver` 事件保留给后续结果反馈使用。

## 约束

UI 不直接修改 Army、Gate 或 Monster 的内部字段。

UI 只监听 `ArmyCountChanged`、`ArmyFormationChanged`、`SoldierHit` 等事件，不把槽位 GameObject 数量当作逻辑总人数。

Gameplay Canvas 中的 `TouchDragArea` 是 Input 模块的触屏采集组件，不属于只读 HUD。它可以使用 UGUI Pointer 回调把相对拖动值交给 Input Adapter，再由 Input Adapter 通过 `IArmyController.SetHorizontalInput` 提交同步命令；它不得直接修改 Army，也不通过项目 `EventBus` 广播连续输入。详细边界见 [Input 模块](../Input/README.md) 和 [ADR-028](../../06_Decisions/ADR-028-MvpRelativeDragInput.md)。
