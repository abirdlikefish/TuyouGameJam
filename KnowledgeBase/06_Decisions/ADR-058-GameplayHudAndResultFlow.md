# ADR-058：Gameplay HUD、结算停留与显式返回

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-019、ADR-029、ADR-032、ADR-053

## 背景

现有 Gameplay 在胜利或失败后立即由 `GameStateService` 请求切回 `LevelSelectScene`，没有展示本局耗时和击杀统计的窗口。Gameplay Canvas 当前只有全屏拖拽输入，也没有关卡名、元素剩余时间或击杀进度 HUD。

## 决策

1. Gameplay 增加常驻 `BattleHud` 和初始隐藏的 `BattleResult`。终局时 HUD 保持显示并冻结，结果面板叠加在上方；结果面板本轮不显示胜利或失败标题。
2. `LevelManager` 提供只读 `GameplayHudSnapshot`，集中保存关卡名、玩法耗时、三元素剩余时间、击杀数和敌人总数。击杀数只累计匹配当前 `LevelRunId` 的 `MonsterKilled`；总数等于本关 `EnemySpawns.Count`。
3. 终局时 `LevelManager` 在清理 Manager 前冻结 HUD 快照。`GameStateService` 接受结果、处理胜利解锁、进入 `GameplayResult` 并发布 `Victory` 或 `GameOver`，但不自动切换场景。
4. `IGameStateService.TryReturnToLevelSelect()` 是 Gameplay 返回选关的唯一 UI 命令。战斗中调用表示主动放弃，不发布结果且不解锁；结算后调用只结束展示并返回选关。
5. 战斗中的退出按钮先显示二次确认层。确认层显示期间 Gameplay 继续计时和推进，确认层通过 UGUI 射线遮挡拖拽；取消后恢复拖拽。若确认期间自然终局，关闭确认层并显示结果面板。
6. 结算耗时只累计 `LevelRunState.Playing`，不包含加载、Preparing 或结果停留时间。结算返回不再二次确认。

## 后果

- `Victory`、`GameOver` 从“卸载前瞬时通知”变为 GameplayScene 内结果 UI 的展示触发事实。
- `GameplayResult` 是应用稳定状态；`LevelRunState.Completed` 仍是单局玩法状态，两者职责不合并。
- UI 不自行累计敌人或元素状态，也不要求每帧 EventBus 广播元素剩余时间。
- 本轮不增加重开、暂停、存档、动画、音效或最终视觉样式。

## 验收

- HUD 从 Gameplay Ready 后持续显示，胜负后数据冻结且结果面板出现，场景不会自动切换。
- 战斗中确认退出不发布胜负、不解锁；取消确认后继续本局。
- 胜负结果只接受一次，结算返回只发起一次 LevelSelect 切换。
- 连续两局之间不存在旧订阅、旧统计或旧 UI 状态。
