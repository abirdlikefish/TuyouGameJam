# 事件目录

| 事件 | 发布者 | 主要监听者 | 参数 |
|---|---|---|---|
| `AppFlowChanged` | GameStateService | UI | 旧状态、新状态、当前关卡 ID、当前 `LevelRunId` |
| `LevelRunStarted` | GameStateService | LevelManager、UI | `LevelId`、`LevelRunId`；仅在 `GameplaySceneReady` 后发布；LevelManager 收到后从 `Preparing` 进入 `Playing` |
| `InitializationFailed` | ConfigService | GameStateService、UI | `ConfigErrorCode`、稳定来源 |
| `LevelConfigLoadFailed` | ConfigService | GameStateService、UI | 关卡编号、`ConfigErrorCode`、稳定来源 |
| `GameplaySceneLoadFailed` | SceneService | GameStateService、UI | `LevelId`、`LevelRunId`、`SceneLoadErrorCode` |
| `GameplaySceneReady` | SceneService | GameStateService | `LevelId`、`LevelRunId`；场景入口和 `LevelManager.Preparing` 已完成 |
| `GameplaySceneUnloaded` | SceneService | GameStateService | `LevelId`、`LevelRunId`；场景对象已清理 |
| `ArmyCountChanged` | ArmyController | UI、ArmyVisual | `LevelRunId`、新人数、变化量、变化原因 |
| `ArmyFormationChanged` | ArmyController | ArmyVisual、UI、VFX | `LevelRunId`、总人数、激活槽位数、槽位代表人数和 HP 快照 |
| `SoldierHit` | ArmyController | UI、VFX | `LevelRunId`、`ArmyId`、槽位 ID、伤害、实际损失人数 |
| `ArmyReachedZero` | ArmyController | LevelManager、UI | `LevelRunId`、原因 |
| `ArmyLoadoutChanged` | ArmyController | ArmyVisual、UI、VFX | `LevelRunId`、旧 Weapon ID、新 Weapon ID、当前 Element ID、来源运行时实例 ID |
| `GateSpawned` | ObstacleManager | UI、VFX | `LevelRunId`、运行时门实例 ID、配置 ID、门类型、位置 |
| `GateValueChanged` | Gate | UI、VFX | `LevelRunId`、运行时门实例 ID、新数字、变化量 |
| `GateContactResolved` | Gate | UI、VFX | `LevelRunId`、运行时门实例 ID、`ArmyId`、门类型、是否成功、已应用的 `GateEffect`、是否继续移动 |
| `GateExitedRoad` | ObstacleManager | — | `LevelRunId`、运行时门实例 ID、是否接触过 Army |
| `PropSpawned` | ObstacleManager | UI、VFX | `LevelRunId`、运行时道具实例 ID、配置 ID、WeaponId、位置 |
| `PropBroken` | Prop | UI、VFX | 当前 MVP：`LevelRunId`、运行时道具实例 ID、WeaponId、命中上下文、接触状态、是否已发放武器替换效果；未来效果载荷需按 ADR-022 另行定案 |
| `PropContactDamage` | Prop | UI、VFX | `LevelRunId`、运行时道具实例 ID、`ArmyId`、接触槽位索引、已应用伤害 |
| `PropExitedRoad` | ObstacleManager | — | `LevelRunId`、运行时道具实例 ID、是否接触过 Army |
| `ObstacleRecycled` | ObstacleManager | 对象池统计 | `LevelRunId`、运行时实例 ID、对象类别、回收原因 |
| `MonsterSpawned` | EnemyManager | LevelManager、UI | `LevelRunId`、运行时敌人实例 ID、敌人配置 ID、敌人类型、`SpawnPosition`、初始世界坐标 |
| `MonsterDamaged` | Monster | UI、VFX | `LevelRunId`、运行时敌人实例 ID、`BulletDamageContext`、剩余生命值、是否致命 |
| `MonsterAttackLanded` | Monster | VFX | `LevelRunId`、运行时敌人实例 ID、攻击类型、命中槽位索引、每槽位伤害 |
| `MonsterKilled` | Monster | EnemyManager、LevelManager、VFX | `LevelRunId`、运行时敌人实例 ID、造成击杀的 `BulletDamageContext` |
| `Victory` | GameStateService | UI | `LevelId`、`LevelRunId`、`unlockedLevelIds`（仅本局结果数据）；场景卸载前发布 |
| `GameOver` | GameStateService | UI | `LevelId`、`LevelRunId`、失败原因；场景卸载前发布 |

## 事件约束

- 事件只描述已经发生的事实，不通过事件请求另一个模块执行私有逻辑。
- Gate/Prop 必须先通过 `IArmyController` 完成玩法状态变更，再发布接触、击破或伤害事件；ArmyController 不是这些事实事件的玩法监听者。
- 监听者在销毁时必须取消订阅。
- 事件参数版本变化需要在 `06_Decisions` 记录。
- 所有 Gameplay 生成请求、会话结果以及可能跨场景或延迟处理的事件必须携带 `LevelRunId`，或由统一的会话事件封装携带；接收者不得处理过期会话消息。
- `GameplaySceneReady` 是进入应用级 `Gameplay` 的唯一成功信号；`GameplaySceneLoadFailed` 不得触发 `LevelRunStarted`。
- `GameplaySceneUnloaded` 只确认场景清理完成，不重新触发结果事件；`GameStateService` 需校验其 `LevelRunId` 后才回到 `LevelSelect`。
- Gate/Prop 报告离场或回收请求后，由 `ObstacleManager` 作为 `GateExitedRoad`、`PropExitedRoad` 和 `ObstacleRecycled` 的唯一事实发布者。
- MVP 所有包含 Army 身份的事件使用固定 `ArmyId = 1`；多 Army 扩展前必须更新契约。
- MVP 完全无声音且不初始化 DebugService；事件目录不把 Audio 或通用调试服务列为当前监听者。未来表现模块仍可在不改变核心结果的前提下订阅既有事实事件。
- 暂停、减速和局部时停延后，当前事件目录不定义 `GamePaused` 或 `GameResumed`。
