# Level 关卡模块

## 模块信息

- ID：`MOD-LEVEL`
- 层级：Gameplay
- 状态：`Planned`
- 依赖：GameStateService、TimeService、Spawn、Monster、Army、ObstacleManager、EventBus
- 决策：`../../06_Decisions/ADR-009-FixedRoadSingleLevelTimeline.md`、`../../06_Decisions/ADR-013-SpawnCursorOwnershipAndDispatch.md`

## 职责

- 接收由 ConfigService 校验、经 SceneService 注入的当前 `LevelConfig`。
- 提供固定道路尺寸、左右边界、出生横线 `spawnY` 和敌人接近线。
- 记录当前关卡 ID、本局运行时间和终局状态；三类生成游标由 SpawnManager 持有，LevelManager 只通过查询接口判断敌人生成是否完成。
- 驱动 Spawn 消费按时间排序的敌人、Gate、Prop 生成列表。
- 通过 EnemyManager 统计已生成敌人的 `AliveEnemyCount`，并通过 SpawnManager 查询敌人时间轴是否已消费完成。
- 在帧末判断胜利或失败，并保证终局只触发一次。
- 终局后停止本局逻辑并清理当前会话，向 GameStateService 提交本局结果；GameStateService 在场景卸载完成后回到 LevelSelect。

`LevelManager` 接收 SceneService 已注入的配置和会话数据，但不直接依赖或调用 SceneService。它也不重新查询 ConfigService；配置来源校验和场景交接属于应用流程边界。

## 配置输入

从 `LevelConfig` ScriptableObject 读取：

- `levelId` 和 `unlockedLevelIds`。
- 固定道路宽度、高度、道路坐标、左右边界和出生横线 `spawnY`。
- 所有道路位置使用世界 XY 坐标，运行时 `z = 0`，右方为 `+x`、上方为 `+y`；世界原点由道路 Prefab/场景决定。
- `spawnY`、`enemyApproachY`、`despawnY` 等关卡空间参数。
- `enemySpawns`、`gateSpawns`、`propSpawns` 三个按时间编排的列表。

`enemySpawns` 在 MVP 中不得为空；ConfigService 必须在场景加载前拒绝空列表并报告 `InvalidLevelConfig`。`gateSpawns` 和 `propSpawns` 可以为空。

敌人、Gate、Prop 的生命值、速度、伤害和 Prefab 等可复用数值由 `ConfigService` 查询 Luban 表；Level 不复制这些数值。

## 运行时流程

```text
Preparing
  -> Playing
  -> Completed（Victory / GameOver）
```

`Preparing` 状态完成 Army、EnemyManager、ObstacleManager、SpawnManager 和子弹状态初始化后，向 GameplaySceneEntry 报告就绪；只有 SceneService 发布匹配的 `AppSceneReady(Gameplay)`、GameStateService 进入应用级 `Gameplay` 并发布 `LevelRunStarted` 后，LevelManager 才进入 `Playing`。`Playing` 状态下，LevelManager 每帧使用 `timeService.GetDeltaTime(TimeDomain.Gameplay)` 累计本局 `elapsedTime`，并将当前 `LevelRunId` 和该时间传给 SpawnManager；SpawnManager 不直接读取 TimeService，自行消费所有 `spawnTime <= elapsedTime` 的条目并推进对应游标。

终局判断在一帧内的生成、伤害和死亡事件处理完成后执行：

```text
if ArmyCount <= 0:
    GameOver
else if SpawnManager.AreAllEnemySpawnsDispatched(LevelRunId)
     && EnemyManager.GetAliveEnemyCount() == 0:
    Victory
```

Army 归零优先于胜利，因此同一帧最后一只敌人死亡且 Army 归零时结果为失败。终局后不再消费未来的生成项；当前敌人、Gate、Prop 和子弹应在离开 Gameplay 前清理。LevelManager 不直接决定应用页面跳转。

## 非职责

- 不计算敌人、Gate、Prop 和子弹的属性数值。
- 不维护敌人、Gate、Prop 的活动实例集合。
- 不把归一化横向出生位置当作敌人的后续移动约束；敌人路径由 Monster 模块处理。
- 不实现下一关跳转；`unlockedLevelIds` 仅作为当前关卡的解锁结果数据。
- 不加载或卸载 Unity 场景，不访问静态全局服务；终局只通过注入的 `IGameStateService` 提交一次结果。

## 测试标准

- 当前场景可以接收选关阶段已校验的唯一 `LevelConfig`，并正确提供道路宽高、边界、`spawnY`、接近线和 `despawnY`。
- 每条敌人、Gate、Prop 生成项的 `spawnPosition` 均位于 `[0,1]`；越界或非有限值在场景加载前被拒绝。
- 三类生成列表按 `spawnTime` 消费，每个生成项只处理一次。
- 本局 `elapsedTime` 只累计 `Gameplay` 时间域 delta；一次更新不叠加其他时间域 delta，SpawnManager 不直接读取 TimeService。
- 所有敌人生成项处理完且 `AliveEnemyCount == 0` 后只触发一次胜利。
- 胜利事件携带当前 `levelId` 和配置中的 `unlockedLevelIds`。
- Army 总人数小于等于 0 后只触发一次失败。
- 同一帧敌人清零和 Army 归零时结果为失败。
- 终局后停止输入、射击、攻击和未来生成，向 GameStateService 提交一次 `Victory` 或 `GameOver` 结果；GameStateService 请求切换 LevelSelectScene，并在匹配的 `AppSceneReady(LevelSelect)` 后清除会话并回到 LevelSelect。
- LevelSelect 使用 RealTime 等待 1 秒后开始新的同一关会话；新会话的运行时间、生成游标、EnemyManager、ObstacleManager、Army 和子弹状态恢复为初始值。
- 初始化或配置加载失败时不得自动进入 Playing。
- `enemySpawns` 为空时配置校验失败，不创建 Gameplay 会话，也不触发立即胜利。
