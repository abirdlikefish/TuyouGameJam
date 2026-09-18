# 事件目录

| 事件 | 发布者 | 主要监听者 | 参数 |
|---|---|---|---|
| `AppFlowChanged` | GameStateService | UI | 旧状态、新状态、当前关卡 ID、当前 `LevelRunId` |
| `LevelRunStarted` | GameStateService | LevelManager、UI | `LevelId`、`LevelRunId`；仅在匹配的 `AppSceneReady(Gameplay)` 后发布；LevelManager 收到后从 `Preparing` 进入 `Playing` |
| `InitializationFailed` | ConfigService | GameStateService、UI | `ConfigErrorCode`、稳定来源 |
| `LevelConfigLoadFailed` | ConfigService | GameStateService、UI | 关卡编号、`ConfigErrorCode`、稳定来源 |
| `AppSceneReady` | SceneService | GameStateService、UI | `AppSceneId`、`LevelId`、`LevelRunId`；规范 SceneEntry 已初始化，Gameplay 的 `LevelManager.Preparing` 已完成 |
| `AppSceneUnloaded` | SceneService | GameStateService、UI | `AppSceneId`、`LevelId`、`LevelRunId`；旧场景 Entry 已清理且异步卸载完成 |
| `AppSceneLoadFailed` | SceneService | GameStateService、UI | 目标 `AppSceneId`、`LevelId`、`LevelRunId`、`SceneLoadErrorCode`；失败场景已清理 |
| `AppSceneUnloadFailed` | SceneService | GameStateService、UI | 当前 `AppSceneId`、`LevelId`、`LevelRunId`、`SceneUnloadErrorCode`；目标场景未继续加载 |
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
| `ObstacleRecycled` | ObstacleManager | —（当前无必需监听者） | `LevelRunId`、运行时实例 ID、对象类别、回收原因 |
| `MonsterSpawned` | EnemyManager | LevelManager、UI | `LevelRunId`、运行时敌人实例 ID、敌人配置 ID、敌人类型、`SpawnPosition`、初始世界坐标 |
| `MonsterDamaged` | Monster | UI、VFX | `LevelRunId`、运行时敌人实例 ID、`BulletDamageContext`、剩余生命值、是否致命 |
| `MonsterAttackLanded` | Monster | VFX | `LevelRunId`、运行时敌人实例 ID、攻击类型、命中槽位索引、每槽位伤害 |
| `MonsterKilled` | EnemyManager | LevelManager、VFX | `LevelRunId`、运行时敌人实例 ID、造成击杀的 `BulletDamageContext`；Monster 先通过必执行回调报告死亡，EnemyManager 完成死亡去重和存活计数后发布 |
| `Victory` | GameStateService | UI | `LevelId`、`LevelRunId`、`unlockedLevelIds`（仅本局结果数据）；场景卸载前发布 |
| `GameOver` | GameStateService | UI | `LevelId`、`LevelRunId`、失败原因；场景卸载前发布 |

## 事件约束

- 事件只描述已经发生的事实，不通过事件请求另一个模块执行私有逻辑。
- Gate/Prop 必须先通过 `IArmyController` 完成玩法状态变更，再发布接触、击破或伤害事件；ArmyController 不是这些事实事件的玩法监听者。
- 监听者在销毁时必须取消订阅。
- 事件参数版本变化需要在 `06_Decisions` 记录。
- 所有 Gameplay 生成请求、会话结果以及可能跨场景或延迟处理的事件必须携带 `LevelRunId`，或由统一的会话事件封装携带；接收者不得处理过期会话消息。
- `AppSceneReady` 是进入 MainMenu、LevelSelect 或 Gameplay 稳定状态的唯一场景成功信号；只有匹配的 `AppSceneReady(Gameplay)` 可以触发 `LevelRunStarted`。
- MainMenu、LevelSelect 场景事实使用 `LevelId = 0`、`LevelRunId = 0`；Gameplay 场景事实必须使用当前值。GameStateService 必须同时校验 `AppSceneId`、pending target 和会话 ID。
- `AppSceneUnloaded` 只确认旧场景清理完成，不直接推进下一个应用状态。SceneService 在此后同步加载并初始化目标场景，GameStateService 只在目标 `AppSceneReady` 后推进。
- `AppSceneLoadFailed` 只能在失败目标场景完成清理后发布；`AppSceneUnloadFailed` 会终止本次切换，且不得继续加载目标场景。
- Gate/Prop 报告离场或回收请求后，由 `ObstacleManager` 作为 `GateExitedRoad`、`PropExitedRoad` 和 `ObstacleRecycled` 的唯一事实发布者。
- Monster 生命值归零后先通过 EnemyManager 的必执行回调完成死亡去重和 `AliveEnemyCount` 更新，再由 EnemyManager 发布 `MonsterKilled`；监听者不承担敌人注销、计数或回收。
- MVP 所有包含 Army 身份的事件使用固定 `ArmyId = 1`；多 Army 扩展前必须更新契约。
- MVP 完全无声音且不初始化 DebugService；事件目录不把 Audio 或通用调试服务列为当前监听者。未来表现模块仍可在不改变核心结果的前提下订阅既有事实事件。
- 暂停、减速和局部时停延后，当前事件目录不定义 `GamePaused` 或 `GameResumed`。

## 事件类型与文件

- 每个事件拥有独立的消息类型，但不要求每个类型占用一个脚本。
- 应用流程事件集中在 `ApplicationEvents.cs`，Army 事件集中在 `ArmyEvents.cs`，Gate/Prop/道路对象事件集中在 `ObstacleEvents.cs`，敌人事件集中在 `MonsterEvents.cs`。
- 小型事件默认使用 `readonly struct`；包含大型快照或多个集合的事件使用不可变 `sealed class`。EventBus 不限制消息必须是值类型。
- 消息定义、订阅生命周期和具体文件路径见 `../01_Architecture/EventSystem.md`；本文件仍是事件名、发布者、监听者和参数语义的唯一目录。

## 新增或修改事件

1. 先确认它是已经发生的事实，不是命令、查询或必须执行的回调。
2. 明确唯一发布者、主要监听者，以及零监听者时核心结果仍然正确。
3. 定义不可变载荷；Gameplay 事件携带 `LevelRunId`，实例事件携带运行时实例 ID。
4. 修改跨模块事件、载荷或发布所有权前，先新增或更新 `../06_Decisions` 中的 ADR。
5. 更新本目录；新增公共类型或错误码时同步 `PublicInterfaces.md`，新增共享字段语义时同步 `DataDictionary.md`。
6. 同步发布/监听模块 README、`../05_Testing/IntegrationTests.md` 和 `../07_Changes/ChangeLog.md`，再修改 C# 消息类型。
