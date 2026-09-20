# UI 模块

## 模块信息

- ID：`MOD-UI`
- 层级：Presentation
- 状态：MainMenu `InTest`；LevelSelect `InTest`；HUD `Deferred`；Input UI `ContractReady`
- 依赖：EventBus、GameStateService

## 首轮工程切片

首轮只创建 Input 模块所需的 Gameplay Canvas、GraphicRaycaster、EventSystem 和 `PF_UI_TouchDragArea`。不创建 Army 人数、关卡计时、生成进度、胜负或重开 HUD；玩法状态先通过 Gate 调试文本、结构化日志、Inspector 和测试清单手工验证。自动化测试按 ADR-045 延后到正式程序集阶段。

## 后续 HUD 职责

- 显示军队总人数、激活槽位数（可选显示各槽位代表人数）、加法门数字、元素门 HP 与 HP 清空后的可兑换额外伤害/持续时间、敌人生成/击杀进度和关卡计时。
- 显示胜利、失败和重开界面；暂停界面属于后续扩展。
- 监听状态和数值事件。

MainMenu 已接入开始与退出按钮。LevelSelect 按 `LevelDescriptor` 动态生成 `LevelSelectNodeView`，显示运行期解锁状态，并通过 GameStateService 的选择和开始命令进入关卡。两类页面代码都不控制视觉样式。HUD 和结果界面仍未实现；`Victory`、`GameOver` 事件保留给后续结果反馈使用。

## 约束

UI 不直接修改 Army、Gate 或 Monster 的内部字段。

UI 只监听 `ArmyCountChanged`、`ArmyFormationChanged`、`SoldierHit`、`GateValueChanged`、`ElementGateDamageChanged`、`GateContactResolved` 等事实事件，不把槽位 GameObject 数量当作逻辑总人数，也不自行累计元素门额外伤害或换算持续时间。

Gameplay Canvas 中的 `PF_UI_TouchDragArea` 是 Input 模块的拖拽采集 Prefab，不属于只读 HUD。它使用同一套 UGUI Pointer 回调接受设备触屏和 Editor 左键，把相对拖动值交给 Input Adapter，再由 Input Adapter 通过 `IHorizontalInputReceiver.SetHorizontalInput` 提交同步命令；它不得直接修改 Army，也不通过项目 `EventBus` 广播连续输入。当前不读取键盘或手柄。详细边界见 [Input 模块](../Input/README.md)、[ADR-028](../../06_Decisions/ADR-028-MvpRelativeDragInput.md) 和 [ADR-036](../../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md)。
