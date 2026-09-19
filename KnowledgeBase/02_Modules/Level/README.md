# Level 关卡模块

## 模块信息

- ID：`MOD-LEVEL`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：IGameStateService、ITimeService、IEventBus、ISpawnManager、IArmyRunController、IBulletManager、IEnemyManager、IObstacleManager、IGameplayInputController
- 被依赖模块：Army、Spawn、Monster、GameplaySceneEntry、Input、UI
- 决策：`../../06_Decisions/ADR-009-FixedRoadSingleLevelTimeline.md`、`../../06_Decisions/ADR-013-SpawnCursorOwnershipAndDispatch.md`、`../../06_Decisions/ADR-021-MvpRuntimeDeterminismAndBindings.md`、`../../06_Decisions/ADR-023-NormalizedSpawnPosition.md`、`../../06_Decisions/ADR-033-LevelManagerFramePipeline.md`、`../../06_Decisions/ADR-034-NumericRoadBoundsAndArmyOrigin.md`、`../../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md`、`../../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`、`../../06_Decisions/ADR-042-LevelConfigSnapshotAssemblyBoundary.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`

## 模块目标

管理一次 Gameplay 会话的准备、开始、确定性逐帧阶段、终局判定和停止，并向其他模块提供只读道路与单局状态。Level 不拥有其他模块的活动实例集合，也不替代 GameStateService 管理应用页面流程。

## 职责范围

- 接收由 ConfigService 从关卡资产校验并复制、经 SceneService 和 GameplaySceneEntry 注入的 `LevelConfigSnapshot`、`LevelId`、`LevelRunId`。
- 从唯一 `roadBounds` 派生道路宽高与上下左右边界，构建只读 `RoadLayoutSnapshot`。
- 记录本局 `LevelRunState`、`elapsedTime` 和唯一终局结果。
- 在 `Preparing` 阶段按顺序启动 Army、BulletManager、EnemyManager、ObstacleManager 和 SpawnManager；全部就绪后才允许 GameplaySceneEntry 报告 Ready。
- 订阅匹配的 `LevelRunStarted`，从 `Preparing` 进入 `Playing`。启动事件不直接驱动其他 Manager 的逐帧逻辑。
- 在 `Playing` 中集中读取时间域 delta，并按权威顺序同步驱动拖拽输入、生成、移动、子弹命中、道路对象接触、敌人攻击、回收和终局判断。
- 在帧末查询 Army、SpawnManager 和 EnemyManager 的权威状态；Army 归零优先于胜利。
- 终局时先切换为 `Completed`，停止输入、未来生成和全部玩法阶段，幂等清理当前会话，再向 GameStateService 提交一次 `LevelCompletion`。Victory 不等待 Gate/Prop 时间轴或活动道路对象完成。

## 非职责范围

- 不加载、卸载或选择 Unity 场景，不直接调用 SceneService。
- 不重新查询或校验 LevelCatalog，不创建第二个 ConfigService 或 Luban Tables。
- 不计算敌人、Prop、Weapon、Element 和 Bullet 的配置数值，也不执行 Gate 伤害或接触规则；LevelConfig 只保存 Gate 的关卡编排输入。
- 不维护敌人、Gate、Prop 或 Bullet 的活动实例集合。
- 不直接解析 Prefab。池化规范 Prefab 由对应 Manager 的 Inspector 引用；数值配置由对应 Manager 通过注入的 Bullet、Enemy、Prop 最小类型化 Provider 获取。
- 不让事件监听者参与必须执行的帧阶段、伤害、死亡计数或终局结果。
- 不实现下一关跳转；`unlockedLevelIds` 只作为 GameStateService 发布 Victory 时使用的本局结果数据。

## 输入

### 会话输入

- `LevelConfigSnapshot`：已由 ConfigService 校验并防御性复制的不可变运行时快照。
- `LevelId`：必须与 `LevelConfigSnapshot.LevelId` 一致。
- `LevelRunId`：本次 Gameplay 会话的非零稳定 ID。
- 最小服务接口：`IGameStateService`、`ITimeService`、`IEventBus`。
- 场景模块接口：Army、Bullet、Enemy、Obstacle、Spawn 和 `IGameplayInputController`。

### LevelConfig 资产与运行时快照字段

```text
levelId
displayName
unlockedLevelIds
roadBounds: Rect
spawnY
enemyApproachY
despawnY
enemySpawns
gateSpawns
propSpawns
elementDurationSecondsPerDamage
```

`roadBounds` 是道路空间的唯一序列化边界。宽度、高度、左右边界和上下边界全部派生，不重复保存。ArmyRoot 每局开始时重置到世界坐标 `(0,0,0)`，世界 y 保持为 `0`。

所有生成列表每项包含有限且非负的 `spawnTime` 和闭区间 `[0,1]` 的 `spawnPosition`。Enemy/Prop 条目另外包含对应表的稳定 `configId`；Gate 条目不使用配置 ID，而是包含 `GateType`、Additive 的 `InitialValue`，或 Element 的 `ElementType + MaxHp`。`enemySpawns` 不得为空，Gate/Prop 列表可以为空。

这些字段的编辑源是 Foundation 中的 `LevelConfig` ScriptableObject；ConfigService 校验后把值和三个生成列表复制到 `LevelConfigSnapshot`，Level/Spawn 只读取快照。敌人、Prop 和 Bullet 的生命、速度、伤害等可复用数值来自 Luban，由 ConfigService 在应用启动时完整校验并复制为不可变快照；对应 Manager 分别通过 `IEnemyConfigProvider`、`IPropConfigProvider`、`IBulletConfigProvider` 必得快照，不接收完整 IConfigService。Gate 不使用 TbGate：每条门的类型、加法门的初始数字、元素门的 ElementType 与 MaxHp 来自关卡快照；本关元素门统一使用 `ElementDurationSecondsPerDamage`；加法门统一速度来自 AdditiveGate Prefab，元素门统一速度和接触伤害来自 ElementGate Prefab。规范 Prefab、Collider、Sprite、Animator 和场景固定引用来自 Unity Inspector 或 Unity 资源注册表。

## 输出

- `ILevelRuntime`：当前状态、LevelRunId、冻结或运行中的 elapsedTime、RoadLayoutSnapshot。
- 对各 Manager 的同步生命周期与阶段命令。
- 一次 `IGameStateService.CompleteGameplay(LevelCompletion)` 调用。
- 不直接发布 Victory/GameOver；GameStateService 接受结果后发布应用级结果事实。

## 文件与资源规划

### Level 直接拥有的脚本

```text
Assets/Scripts/Game/Gameplay/Level/
├── LevelManager.cs
└── RoadView.cs
```

- `LevelManager.cs`：MonoBehaviour，实现 ILevelRuntime 和本局协调逻辑。
- `RoadView.cs`：根据 RoadLayoutSnapshot 调整道路视觉，不持有玩法状态、不添加道路 Collider。

`LevelConfig.cs`、`LevelCatalog.cs`、`LevelSpawnEntries.cs` 与具体 ConfigService 放入 `Assets/Scripts/Game/Foundation/Configuration`；`LevelConfigSnapshot.cs` 与生成条目快照放入 `Assets/Scripts/Game/Contracts/Configuration`。`GameplaySceneEntry.cs` 属于 Composition；LevelConfig 的跨表验证与资产到快照转换不放入 LevelManager。

### 场景与配置资产

```text
Assets/Scenes/GameplayScene.unity
Assets/Prefabs/Level/PF_Road_Default.prefab
Assets/Configs/Levels/CFG_LevelCatalog.asset
Assets/Configs/Levels/CFG_Level_001.asset
```

Level 不创建 `PF_LevelManager`。LevelManager 和 SpawnManager 是 GameplayScene 中 `LevelSystems` 下的固定场景组件。`PF_Road_Default` 只包含道路视觉与 RoadView，不包含玩法 Collider。

完整 Gameplay 闭环还依赖 Army 模块的 `PF_Army_001`、三种 Monster、两种 Gate、`PF_Prop_Weapon`、`PF_Bullet` 和 Gameplay UI/TouchDragArea；这些 Prefab 不由 LevelConfig 引用。GameplaySceneEntry 使用序列化 ArmyPrefabBinding 以 ArmyId 选择 Army Prefab。

## 场景装配

GameplaySceneEntry 通过 Inspector 持有 LevelManager、SpawnManager、ArmyPrefabBinding、BulletManager、EnemyManager、ObstacleManager、GameplayInputAdapter、TouchDragInput、Gameplay Canvas、GraphicRaycaster、EventSystem、StandaloneInputModule、RoadView 和相应 Root。ArmyController 是由 ArmyPrefabBinding 选中并运行时实例化的本局对象，不是场景预先绑定的固定实例。Entry 取得应用服务后只把每个消费者需要的最小接口注入，不使用 Find、运行时 AddComponent、静态服务定位器或缺失引用兜底。

```text
GameplayRoot [GameplaySceneEntry]
├── Road [RoadView；PF_Road_Default 实例]
├── ArmyContainer
│   └── ArmyRoot [ArmyController；PF_Army_001 运行时实例；开始时世界原点]
├── LevelSystems
│   ├── LevelManager
│   └── SpawnManager
├── MonsterRoot [EnemyManager]
├── ObstacleRoot [ObstacleManager]
├── BulletRoot [BulletManager]
├── InputAdapter [GameplayInputAdapter；实现 IGameplayInputController]
├── UI [Gameplay Canvas；GraphicRaycaster]
│   └── TouchDragArea [PF_UI_TouchDragArea；TouchDragInput]
└── EventSystem [EventSystem；StandaloneInputModule]
```

## 公共接口与阶段契约

Level 对外只暴露 ILevelRuntime。帧协调使用职责明确的同步接口，不增加通用 `ITickable`。

```csharp
public interface ILevelRuntime
{
    LevelRunState GetRunState();
    int GetLevelRunId();
    float GetElapsedTime();
    RoadLayoutSnapshot GetRoadLayout();
}
```

Army、Bullet、Enemy 和 Obstacle 的具体阶段方法以 `../../03_SharedContracts/PublicInterfaces.md` 为准。所有方法必须拒绝过期 LevelRunId；Preparing 和 Completed 不接受新的玩法阶段。

Input 使用独立同步阶段：LevelManager 在每个 Playing 帧通过 `ITimeService.GetDeltaTime(TimeDomain.RealTime)` 取得一次有限且大于 `0` 的 `unscaledDeltaTime`，先调用 `IGameplayInputController.TickInput(unscaledDeltaTime)`，再调用 Army 的移动与发射阶段。Input 本身不依赖 TimeService，也不直接读取 Unity `Time`；输入阶段不依赖 Unity 同类脚本的隐式 `Update` 顺序，无效未缩放 delta 只使本帧输入为 `0`。

## 生命周期

### Preparing

```text
GameplaySceneEntry 注入 LevelConfigSnapshot、LevelId、LevelRunId 和接口
→ 读取 TbArmy.Id=1 与固定 TbWeapon 快照
→ 从序列化 ArmyPrefabBinding 取得 ArmyId=1 Prefab，在 ArmyContainer 下实例化并校验槽位数组
→ LevelManager 校验会话参数与固定引用
→ 从 roadBounds 构建 RoadLayoutSnapshot，RoadView 应用视觉
→ 集中校验本局配置、Manager Prefab、Collider、Layer、Input 和全部必需 Inspector 引用
→ BulletManager.StartRun
→ ArmyRunController.StartRun（重置 ArmyRoot 到原点）
→ EnemyManager.StartRun
→ ObstacleManager.StartRun
→ SpawnManager.StartRun（最后启动，确保生成接收者就绪）
→ InputGate 保持禁用并清零
→ LevelManager 订阅 LevelRunStarted
→ GameplaySceneEntry 才允许报告 AppSceneReady(Gameplay)
```

Luban 表、LevelCatalog 和目录内 LevelConfig 已由 ConfigService 在应用启动时完整校验；其错误会在进入场景前直接退出应用。Gameplay Preparing 只复核本次会话参数以及 Prefab、Collider、Layer、Input 和 Inspector 绑定；失败时直接 `Debug.LogError` 输出稳定字段或对象路径，不调用任何 Manager StartRun，也不发布 Ready 或 LevelRunStarted。StartRun 接口保持 `void`，不增加通用 Result 或错误恢复状态机。若启动过程中仍发生意外异常，SceneEntry 记录异常，只对已经启动的模块执行必要的幂等 StopRun 以释放本局实例，并保持不可游玩状态。

### Playing

收到 LevelId、LevelRunId 均匹配的 `LevelRunStarted` 后：

```text
runState = Playing
elapsedTime = 0
SpawnManager.Tick(LevelRunId, 0)
InputGate.SetGameplayEnabled(true)
```

每帧固定执行：

```text
读取 RealTime/Gameplay/Bullet/Gate/Monster delta
→ GameplayInputAdapter.TickInput
→ elapsedTime += Gameplay delta
→ SpawnManager.Tick
→ ArmyController.TickMovementAndFire
→ EnemyManager.TickMovement
→ ObstacleManager.TickMovement
→ Physics2D.SyncTransforms（每帧移动完成后准确一次）
→ BulletManager.TickMovementAndHits
→ ObstacleManager.ResolveContacts
→ EnemyManager.ResolveAttacks
→ 各 Manager FlushPendingRecycles
→ EvaluateCompletion
```

池对象不使用独立 Update 推进上述核心阶段。新生成的敌人、道路对象和 Army 本帧发射的子弹参与本帧后续阶段。

### Completed 与清理

```text
if ArmyCount <= 0:
    GameOver
else if SpawnManager.AreAllEnemySpawnsDispatched(LevelRunId)
     && EnemyManager.GetAliveEnemyCount() == 0:
    Victory
```

命中终局后先设置 `runState = Completed` 和唯一结果，再禁用 Input，并按启动逆序调用 Spawn、Obstacle、Enemy、Army、Bullet 的 `StopRun`；任何 StopRun 期间都不再执行玩法阶段。全部完成后调用一次 `CompleteGameplay`。SceneEntry 卸载时再次调用清理只能作为幂等兜底，不得重复提交结果。

Victory 是立即截断点：不检查 Gate/Prop 时间轴是否派发完毕，也不等待活动 Gate/Prop 接触或离场。SpawnManager 停止未来 Gate/Prop 生成，ObstacleManager 在 StopRun 中归还活动道路对象；这些未生成或未结算内容不发放补偿效果。

GameStateService 保留本次已校验 `LevelConfigSnapshot` 的结果数据；Victory 的 `UnlockedLevelIds` 由它防御性复制并发布，不加入 LevelCompletion。

## 配置验证

ConfigService 在应用启动初始化中验证目录内全部关卡：

```text
levelId 有效且与目录唯一条目一致
roadBounds 及三条 Y 线均为有限值
roadBounds.width > 0 且 height > 0
roadBounds 包含世界原点
roadBounds.yMin <= despawnY < 0
0 < enemyApproachY < spawnY <= roadBounds.yMax
enemySpawns 非空
三个生成列表按 spawnTime 非递减
spawnTime >= 0 且为有限值
spawnPosition 位于 [0,1] 且为有限值
Enemy/Prop 的所有 configId 存在于对应 Luban 表
Additive Gate 使用有效 InitialValue，且 ElementType=None、MaxHp=0
Element Gate 使用 Fire/Ice/Lightning 和 MaxHp>0，且 InitialValue=0
存在元素门时 elementDurationSecondsPerDamage 有限且 >0；不存在时为 0
unlockedLevelIds 符合目录引用规则
```

场景资源引用、ArmyId 到 Prefab 的唯一绑定、Army 槽位数组、Manager 规范 Prefab、Collider、Layer 和发射点由 GameplaySceneEntry 在报告 Ready 前验证，不属于 LevelConfig 数值校验。

## 测试标准

- RoadLayoutSnapshot 的宽高与四条边界只由 roadBounds 派生，永远互相一致。
- 道路没有玩法 Collider 时，Army 限位、归一化生成、接近和离场仍正常。
- ArmyRoot 每个新会话从 `(0,0,0)` 开始并保持 y=0，激活槽位合并 AABB 不越过左右边界。
- ArmyId=1 的配置或 Prefab 绑定缺失、重复、Prefab 根类型错误或槽位数组无效时 Preparing 失败且不发布 Ready。
- `LevelRunStarted` 只让匹配会话进入 Playing；重复、过期或参数不匹配的事件无副作用。
- `spawnTime == 0` 的条目在首帧移动前只生成一次。
- 每帧阶段顺序与 ADR-033 一致，Manager 独立 Update 不执行核心玩法结算。
- 当前 Auto Sync Transforms 关闭；每个 Playing 帧在全部移动后、首次显式查询前准确调用一次 Physics2D.SyncTransforms，各 Manager 不重复同步。
- elapsedTime 只累计本帧 Gameplay delta，其他模块分别只消费传入的对应域 delta。
- Army 归零、敌人生成完成且全部死亡、同帧双条件分别得到 GameOver、Victory、GameOver。
- ArmyReachedZero 和 MonsterKilled 的监听者数量或顺序不改变终局结果。
- 终局只提交一次；Input 清零、未来生成停止、活动对象归还，重复清理安全。
- Victory 即使仍有未来或活动 Gate/Prop 也立即完成；这些对象停止生成或被清理，不产生接触、击破或补偿效果。
- 新 LevelRunId 不继承上局时间、游标、实例、输入、回调或终局状态。
- 配置表或关卡数据错误在加载应用场景前退出；Gameplay 会话参数或绑定校验失败时输出可定位错误、不得调用 Manager StartRun 且不发布 AppSceneReady(Gameplay)；意外启动异常只清理已经启动的模块，不尝试降级继续运行。

## 已知问题与进入代码前仍需解决

- Layer Collision Matrix 已由 ADR-037 定案为默认关闭自动物理关系；工程实现时仍需在目标 Unity 版本验证显式 Cast/Overlap 按 ContactFilter2D/LayerMask 返回指定目标。
- `DES-045`：`unlockedLevelIds` 的重复、自引用和目录缺失 ID 校验规则仍未确认。

## 变更记录

| 日期 | 变更 | 记录人 |
|---|---|---|
| 2026-09-19 | 补齐帧阶段所有权、数值道路边界、脚本与资源规划、场景装配、初始化/清理顺序、配置校验和剩余问题 | Codex |
