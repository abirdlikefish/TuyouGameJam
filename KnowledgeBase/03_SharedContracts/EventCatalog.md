# 事件目录

| 事件 | 发布者 | 主要监听者 | 参数 |
|---|---|---|---|
| `AppFlowChanged` | GameStateService | UI | 旧状态、新状态、当前关卡 ID、当前 `LevelRunId` |
| `LevelIntroFinished` | GameplaySceneEntry | GameStateService | `LevelId`、`LevelRunId`、`LevelIntroEndReason`；匹配场景的开场视频正常结束、未配置、播放失败或准备超时后只发布一次 |
| `LevelRunStarted` | GameStateService | LevelManager、UI | `LevelId`、`LevelRunId`；仅在匹配的 `AppSceneReady(Gameplay)` 与 `LevelIntroFinished` 后发布；LevelManager 收到后从 `Preparing` 进入 `Playing` |
| `AppSceneReady` | SceneService | GameStateService、UI | `AppSceneId`、`LevelId`、`LevelRunId`；规范 SceneEntry 已初始化，Gameplay 的 `LevelManager.Preparing` 已完成 |
| `AppSceneUnloaded` | SceneService | GameStateService、UI | `AppSceneId`、`LevelId`、`LevelRunId`；旧场景 Entry 已清理且异步卸载完成 |
| `AppSceneLoadFailed` | SceneService | GameStateService、UI | 目标 `AppSceneId`、`LevelId`、`LevelRunId`、`SceneLoadErrorCode`；失败场景已清理 |
| `AppSceneUnloadFailed` | SceneService | GameStateService、UI | 当前 `AppSceneId`、`LevelId`、`LevelRunId`、`SceneUnloadErrorCode`；目标场景未继续加载 |
| `ArmyCountChanged` | ArmyController | UI、ArmyVisual | `LevelRunId`、新人数、变化量、`ArmyCountChangeReason` |
| `ArmyFormationChanged` | ArmyController | ArmyVisual、UI、VFX | `LevelRunId`、总人数、激活槽位数、槽位代表人数和 HP 快照 |
| `SoldierHit` | ArmyController | UI、VFX | `LevelRunId`、`ArmyId`、槽位 ID、请求伤害、实际扣除 HP、实际损失人数 |
| `ArmyReachedZero` | ArmyController | UI | `LevelRunId`、`ArmyReachedZeroReason`；LevelManager 在帧末同步查询 ArmyCount，不由该事件即时终局 |
| `ArmyWeaponChanged` | ArmyController | ArmyVisual、UI、VFX | `LevelRunId`、`ArmyId`、旧 Weapon ID、新 Weapon ID、`ArmyWeaponChangeReason`、可空来源运行时实例 ID；元素自然过期时来源为 null |
| `ArmyElementDurationChanged` | ArmyController | ArmyVisual、UI、VFX | `LevelRunId`、`ArmyId`、`ElementType`、旧剩余时间、本次增加时间、新剩余时间、来源运行时实例 ID |
| `ArmyElementExpired` | ArmyController | ArmyVisual、UI、VFX | `LevelRunId`、`ArmyId`、刚从有效变为 `0` 的 `ElementType`；同一次有效期只发布一次 |
| `GateSpawned` | ObstacleManager | UI、VFX | `LevelRunId`、运行时门实例 ID、`SpawnEntryIndex`、门类型、初始数字或元素类型与 MaxHp、位置；Gate 不携带配置表 ID |
| `GateValueChanged` | Gate | UI、VFX | `LevelRunId`、运行时门实例 ID、`BulletDamageContext`、旧数字、新数字、等于实际子弹伤害的变化量 |
| `ElementGateDamageChanged` | Gate | UI、VFX | `LevelRunId`、运行时门实例 ID、`BulletDamageContext`、旧/新 HP、本次 HP 伤害、本次额外伤害、累计 `PostDepletionDamage`、奖励是否已锁定 |
| `GateContactResolved` | Gate | UI、VFX | `LevelRunId`、运行时门实例 ID、`ArmyId`、门类型、是否成功；加法门携带请求/实际增员或 `ArmyRemovalResult`；元素门携带 `ElementType`、`PostDepletionDamage`、换算系数、计算持续时间、可选的 `ElementDurationChangeResult`、奖励是否锁定；另含是否继续移动 |
| `GateExitedRoad` | ObstacleManager | — | `LevelRunId`、运行时门实例 ID、是否接触过 Army |
| `PropSpawned` | ObstacleManager | UI、VFX | `LevelRunId`、运行时道具实例 ID、`SpawnEntryIndex`、配置 ID、`PropType`、位置 |
| `PropBroken` | Prop | UI、VFX | 当前 MVP：`LevelRunId`、运行时道具实例 ID、WeaponId、命中上下文、接触状态、是否已发放武器替换效果；只由 Pending 状态 HP 归零发布，Failed 后锁血不发布；未来效果载荷需按 ADR-022 另行定案 |
| `PropContactDamage` | Prop | UI、VFX | `LevelRunId`、运行时道具实例 ID、`ArmyId`、接触槽位索引、已提交的 `ContactDamage`；实际 HP 与人数损失见 `SoldierHit` |
| `PropExitedRoad` | ObstacleManager | — | `LevelRunId`、运行时道具实例 ID、是否接触过 Army |
| `GooseCageBroken` | GooseCageProp | UI、VFX | `LevelRunId`、运行时实例 ID、配置 ID、命中上下文与 `ArmyAdditionResult`；同步增员完成后发布 |
| `BasketballSpawned` | ObstacleManager | UI、VFX | `LevelRunId`、运行时实例 ID、来源敌人运行时 ID、篮球配置 ID、位置 |
| `BasketballBroken` | BasketballProp | UI、VFX | `LevelRunId`、运行时实例 ID、来源敌人运行时 ID（关卡直接生成时为 `-1`）、配置 ID与命中上下文 |
| `ObstacleRecycled` | ObstacleManager | —（当前无必需监听者） | `LevelRunId`、运行时实例 ID、对象类别、`ObstacleRecycleReason` |
| `MonsterSpawned` | EnemyManager | LevelManager、UI | `LevelRunId`、运行时敌人实例 ID、`SpawnEntryIndex`、敌人配置 ID、敌人类型、`SpawnPosition`、初始世界坐标 |
| `MonsterDamaged` | Monster | UI、VFX | `LevelRunId`、运行时敌人实例 ID、`EnemyDamageContext`（来源子弹、组合类型、直接/效果伤害、元素、命中位置与方向）、剩余生命值、是否致命 |
| `MonsterAttackLanded` | EnemyManager | VFX | `LevelRunId`、运行时敌人实例 ID、攻击类型、命中槽位索引、已提交的 `AttackPower`；范围攻击对每个有效槽位各发布一条，实际 HP 与人数损失见 `SoldierHit` |
| `MonsterKilled` | EnemyManager | LevelManager、VFX | `LevelRunId`、运行时敌人实例 ID、造成击杀的 `EnemyDamageContext`；Monster 先通过必执行回调报告死亡，EnemyManager 完成死亡去重和存活计数后发布；LevelManager 只累计当前会话的 HUD 击杀数，并在帧末同步查询 AliveEnemyCount |
| `Victory` | GameStateService | Gameplay 结果 UI | `LevelId`、`LevelRunId`、ConfigService 已按 ADR-044 过滤的 `unlockedLevelIds`（首项为下一关目标）；进入 `GameplayResult` 后发布并等待玩家选择返回或下一关 |
| `GameOver` | GameStateService | Gameplay 结果 UI | `LevelId`、`LevelRunId`、`GameOverReason`；进入 `GameplayResult` 后发布并等待玩家选择返回或重试 |

## 事件约束

- 事件只描述已经发生的事实，不通过事件请求另一个模块执行私有逻辑。
- Gate/Prop 必须先通过 `IArmyController` 完成玩法状态变更，再发布接触、击破或伤害事件；ArmyController 不是这些事实事件的玩法监听者。
- 元素门额外伤害为 0 时不调用 `AddElementDuration`，因此不发布 `ArmyElementDurationChanged`；`GateContactResolved` 仍必须报告成功和计算持续时间 0。元素门失败后若仍保留受击表现，可以继续发布 `ElementGateDamageChanged`，但载荷必须表明奖励已锁定且累计可兑换伤害未增加。
- `ApplySlotDamage` 不返回 Army 私有结算结果。Monster、Gate、Prop 只在提交伤害前重新确认槽位有效，并在提交后发布各自的攻击/接触事实；实际扣除 HP 和人数损失只由 Army 的 `SoldierHit`、人数与阵型事件报告。
- Failed 元素门和 Prop 后续受击时 HP 最低锁在 `1`。元素门可以继续发布奖励已锁定的 `ElementGateDamageChanged`；Prop 不发布 `PropBroken`，两者都不因后续伤害回收。
- 监听者在销毁时必须取消订阅。
- 事件参数版本变化需要在 `06_Decisions` 记录。
- 所有 Gameplay 生成请求、会话结果以及可能跨场景或延迟处理的事件必须携带 `LevelRunId`，或由统一的会话事件封装携带；接收者不得处理过期会话消息。
- `AppSceneReady` 是进入 MainMenu、LevelSelect 或 Gameplay 场景就绪边界的唯一成功信号。Gameplay Ready 后仍保持 `GameplayLoading`；只有同一场景和会话随后发布匹配的 `LevelIntroFinished`，GameStateService 才能进入 Gameplay 并发布 `LevelRunStarted`。
- `LevelIntroFinished` 的四种原因都表示本局开场门禁已经终止，不表示播放一定成功。重复、过期、场景不匹配或非 `GameplayLoading` 状态的消息必须忽略。
- MainMenu、LevelSelect 场景事实使用 `LevelId = 0`、`LevelRunId = 0`；Gameplay 场景事实必须使用当前值。GameStateService 必须同时校验 `AppSceneId`、pending target 和会话 ID。
- `AppSceneUnloaded` 只确认旧场景清理完成，不直接推进下一个应用状态。SceneService 在此后同步加载并初始化目标场景，GameStateService 只在目标 `AppSceneReady` 后推进。
- `AppSceneLoadFailed` 只能在失败目标场景完成清理后发布；`AppSceneUnloadFailed` 会终止本次切换，且不得继续加载目标场景。
- Gate/Prop 报告离场或回收请求后，由 `ObstacleManager` 作为 `GateExitedRoad`、`PropExitedRoad` 和 `ObstacleRecycled` 的唯一事实发布者。
- Monster 生命值归零后先通过 EnemyManager 的必执行回调完成死亡去重和 `AliveEnemyCount` 更新，再由 EnemyManager 发布 `MonsterKilled`；监听者不承担敌人注销、计数或回收。
- `OnDeathAnimationFinished()` 是 Monster Prefab 上的本地 AnimationEvent 方法，不是 EventBus 事件。它只登记延后回收，不能再次发布 `MonsterKilled` 或改变死亡计数。
- `ArmyReachedZero` 和 `MonsterKilled` 不驱动 LevelManager 即时终局；LevelManager 在 ADR-033 的攻击和回收阶段之后统一查询权威状态，保证同帧失败优先级。
- MVP 所有包含 Army 身份的事件使用固定 `ArmyId = 0`；多 Army 扩展前必须更新契约。
- 元素剩余时间不通过 EventBus 每帧广播；只在成功增加持续时间和从有效变为过期时发布事实。子弹、受击和击杀事实使用发射瞬间不可变的 `ElementMask`。
- 元素增加先发布 `ArmyElementDurationChanged`，再发布可能发生的 `ArmyWeaponChanged`；每帧全部元素过期事实发布完成后，至多发布一次最终法杖 `ArmyWeaponChanged`。
- 配置或必需资源校验失败不通过 EventBus 建立恢复流程。Luban 表、LevelCatalog 或 LevelConfig 数据错误由 ConfigService 输出首个稳定错误并立即退出应用；ADR-044 允许的 `unlockedLevelIds` 目录缺失 ID 只通过 `Debug.LogWarning` 报告并过滤，不发布事件。Prefab、Collider、Layer 或 Inspector 装配错误由 GlobalBootstrap 或 GameplaySceneEntry 输出错误并阻止对应 Ready。
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
