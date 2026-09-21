# UI 模块

## 模块信息

- ID：`MOD-UI`
- 层级：Presentation
- 状态：MainMenu `InTest`；LevelSelect `InTest`；Gameplay HUD/Result `InTest`；Input UI `ContractReady`
- 依赖：EventBus、GameStateService

## Gameplay HUD 与结算

Gameplay Canvas 在 `PF_UI_TouchDragArea` 上方依次放置常驻 `BattleHud` 与初始隐藏的 `BattleResult`。HUD 显示关卡名、玩法耗时、火/冰/雷剩余时间与击杀进度；结果面板显示冻结的总耗时、击杀数和返回选关按钮，不显示额外胜负标题。终局后停留在 GameplayScene，玩家显式返回后才卸载场景。

战斗中退出使用 HUD 内的二次确认层。确认层显示期间玩法继续推进和计时，但全屏射线遮挡阻止拖拽；取消恢复操作，确认则主动放弃本局且不发布胜负或解锁。终局发生时确认层必须关闭，结果面板获得最高 UI 层级。

## 后续 HUD 职责

- 显示军队总人数、激活槽位数（可选显示各槽位代表人数）、加法门数字、元素门 HP 与 HP 清空后的可兑换额外伤害/持续时间、敌人生成/击杀进度和关卡计时。
- 显示胜利、失败和重开界面；暂停界面属于后续扩展。
- 监听状态和数值事件。

MainMenu 已接入开始与退出按钮。LevelSelect 按 `LevelDescriptor` 动态生成 `LevelSelectNodeView`，显示运行期解锁状态，并通过 GameStateService 的选择和开始命令进入关卡。页面代码都不控制最终视觉样式。Gameplay HUD 从 `IGameplayHudSource` 读取只读快照；结果界面监听当前会话的 `Victory`、`GameOver` 并读取终局冻结快照。

## 约束

UI 不直接修改 Army、Gate 或 Monster 的内部字段。

UI 只读取 Gameplay 提供的 HUD 快照或监听 `ArmyCountChanged`、`ArmyFormationChanged`、`SoldierHit`、`GateValueChanged`、`ElementGateDamageChanged`、`GateContactResolved` 等事实事件，不把槽位 GameObject 数量当作逻辑总人数，也不自行累计元素、击杀或换算持续时间。

Gameplay Canvas 中的 `PF_UI_TouchDragArea` 是 Input 模块的拖拽采集 Prefab，不属于只读 HUD。它使用同一套 UGUI Pointer 回调接受设备触屏和 Editor 左键，把相对拖动值交给 Input Adapter，再由 Input Adapter 通过 `IHorizontalInputReceiver.SetHorizontalInput` 提交同步命令；它不得直接修改 Army，也不通过项目 `EventBus` 广播连续输入。当前不读取键盘或手柄。详细边界见 [Input 模块](../Input/README.md)、[ADR-028](../../06_Decisions/ADR-028-MvpRelativeDragInput.md) 和 [ADR-036](../../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md)。
