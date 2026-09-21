# UI 模块

## 模块信息

- ID：`MOD-UI`
- 层级：Presentation
- 状态：MainMenu `InTest`；LevelSelect `InTest`；Gameplay HUD/Result `InTest`；Level Intro Video `InTest`；Input UI `ContractReady`
- 依赖：EventBus、GameStateService

## Gameplay HUD 与结算

Gameplay Canvas 在 `PF_UI_TouchDragArea` 上方依次放置常驻 `BattleHud` 与初始隐藏的 `BattleResult`。HUD 显示纯数字关卡 ID、玩法耗时、当前击杀数和火/冰/雷剩余时间，并用 `Image.Type.Filled` 进度条展示已击杀敌人数占总敌人数的比例。关卡 ID、`mm:ss` 耗时和击杀数统一使用 `PF_UI_ImageNumberText`；该 Prefab 在 `ImageNumberText` 上集中绑定 `0`～`9` 与冒号共 11 张 Sprite，运行时复用子 `Image` 排版。结果控制器保存失败、有下一关的胜利、无下一关的胜利三个互斥根节点，并用同一图片数字组件显示冻结的总耗时和击杀数。

失败根节点提供再次挑战当前关和返回选关按钮；两个胜利根节点都提供返回选关按钮，有下一关的胜利根节点另外提供挑战下一关按钮。胜利事件的 `UnlockedLevelIds` 为空时显示无下一关根节点，非空时显示有下一关根节点，并把列表首项作为挑战目标。UI 只提交 GameStateService 命令，不直接加载场景。

战斗中退出使用 HUD 内的二次确认层。确认层显示期间玩法继续推进和计时，但全屏射线遮挡阻止拖拽；取消恢复操作，确认则主动放弃本局且不发布胜负或解锁。终局发生时确认层必须关闭，结果面板获得最高 UI 层级。

## 关卡开场视频

Gameplay Canvas 在 BattleHud 上方、BattleResult 下方放置全屏 `LevelIntroVideo`。`LevelIntroVideoView` 使用黑色 Raycast Target 遮挡输入，VideoPlayer 以 API Only 输出到保持宽高比并覆盖全屏的 RawImage；播放使用未缩放时间且当前不输出音频。GameplaySceneEntry 按 LevelId 解析 Inspector 中的 VideoClip 绑定，并在匹配的 `AppSceneReady(Gameplay)` 后启动。

视频结束前应用保持 `GameplayLoading`、LevelManager 保持 `Preparing`。正常结束、当前关卡未绑定视频、播放失败或准备超时都会结束门禁并发布一次 `LevelIntroFinished`；只有 GameStateService 校验该事实后才发布 `LevelRunStarted`。View 不直接修改玩法状态。视频结束前重置拖拽输入，场景清理时停止播放器并移除回调。

## 后续 HUD 职责

- 显示军队总人数、激活槽位数（可选显示各槽位代表人数）、加法门数字、元素门 HP 与 HP 清空后的可兑换额外伤害/持续时间、敌人生成/击杀进度和关卡计时。
- 显示胜利、失败和重开界面；暂停界面属于后续扩展。
- 监听状态和数值事件。

MainMenu 已接入开始与退出按钮。LevelSelect 使用场景/界面 Prefab 中预放的 `LevelSelectNodeView`；`LevelSelectView.LevelNodeBinding[]` 在 Inspector 中把每个节点显式映射到唯一 LevelId，各节点 RectTransform 可自由布局。节点读取 GameStateService 的运行期完成与解锁状态：已通关只显示 CompletedState，仅解锁只显示 UnlockedState，未解锁时两个状态根都隐藏且按钮禁用。点击可选节点后通过 GameStateService 的选择和开始命令进入对应关卡。页面代码都不控制最终视觉样式。Gameplay HUD 从 `IGameplayHudSource` 读取只读快照；结果界面监听当前会话的 `Victory`、`GameOver` 并读取终局冻结快照。

## 约束

UI 不直接修改 Army、Gate 或 Monster 的内部字段。

UI 只读取 Gameplay 提供的 HUD 快照或监听 `ArmyCountChanged`、`ArmyFormationChanged`、`SoldierHit`、`GateValueChanged`、`ElementGateDamageChanged`、`GateContactResolved` 等事实事件，不把槽位 GameObject 数量当作逻辑总人数，也不自行累计元素、击杀或换算持续时间。

Gameplay Canvas 中的 `PF_UI_TouchDragArea` 是 Input 模块的拖拽采集 Prefab，不属于只读 HUD。它使用同一套 UGUI Pointer 回调接受设备触屏和 Editor 左键，把相对拖动值交给 Input Adapter，再由 Input Adapter 通过 `IHorizontalInputReceiver.SetHorizontalInput` 提交同步命令；它不得直接修改 Army，也不通过项目 `EventBus` 广播连续输入。当前不读取键盘或手柄。详细边界见 [Input 模块](../Input/README.md)、[ADR-028](../../06_Decisions/ADR-028-MvpRelativeDragInput.md) 和 [ADR-036](../../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md)。
