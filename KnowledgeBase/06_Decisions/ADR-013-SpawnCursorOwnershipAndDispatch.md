# ADR-013：Spawn 游标所有权与时间轴调度

## 状态

Accepted

> ADR-038 已将 Gate/Prop 的统一 `ObstacleSpawnRequest` 拆分为 `GateSpawnRequest` 与 `PropSpawnRequest`；本 ADR 的时间轴游标所有权和调度顺序继续有效。

## 日期

2026-09-14

## 背景

LevelManager 负责本局运行时间和终局判断，SpawnManager 负责消费三类时间轴。此前两处文档同时描述了生成进度和游标查询，容易造成重复推进或两个模块各自保存游标。

## 决策

- `SpawnManager` 是 `enemySpawns`、`gateSpawns`、`propSpawns` 三个游标的唯一所有者。
- `LevelManager` 持有 `elapsedTime`，每个 Playing 帧调用 `SpawnManager.Tick(LevelRunId, elapsedTime)`；LevelManager 不直接遍历生成列表，也不修改游标。
- SpawnManager 为三类列表分别按 `spawnTime` 非递减顺序消费；同一列表内相同时间按列表顺序处理。
- 三类列表之间不定义额外的跨类型顺序；它们在同一帧各自消费到当前时间。未来如果跨类型顺序影响玩法，必须改为统一时间轴并新增决策。
- 每次生成请求携带当前 `LevelRunId`、配置 ID、`[0,1]` 归一化横向出生位置和由 SpawnManager 解析的初始世界坐标；位置规则见 ADR-023。
- SpawnManager 在 `StopRun` 后不再消费未来条目；每次新会话调用一次 `StartRun(LevelConfig, RoadLayoutSnapshot, LevelRunId)`，同时绑定本关配置与 LevelManager 派生的道路快照、切换会话并将三个游标归零。不再暴露语义重复的 `ResetRun`。
- `AreAllEnemySpawnsDispatched(LevelRunId)` 属于 SpawnManager 的查询；LevelManager 可以通过注入的 SpawnManager 查询该结果，但不拥有该状态。传入过期会话 ID 时必须拒绝查询或返回未完成。
- SpawnManager 不维护敌人、Gate 或 Prop 的活动实例、生命值、接触规则和回收集合。
- 子弹和特效的对象池入口属于 PoolService，不由 SpawnManager 代为提供。

## 调度流程

```text
LevelManager.LevelRunId + elapsedTime
→ SpawnManager.Tick(LevelRunId, elapsedTime)
→ EnemyManager.Spawn(EnemySpawnRequest)
→ ObstacleManager.Spawn(GateSpawnRequest) / ObstacleManager.Spawn(PropSpawnRequest)
→ LevelManager 查询 AreAllEnemySpawnsDispatched(LevelRunId)
```

按 ADR-033，进入 Playing 时先执行一次 `Tick(LevelRunId, 0)`；后续 Tick 位于每帧移动阶段之前，新生成对象参与本帧后续阶段。SpawnManager 不使用独立 Update。

## 不采用

- 不允许 LevelManager 和 SpawnManager 各自保存一份游标。
- 不因对象回收、死亡或碰撞事件推进生成游标。
- 不在 SpawnManager 中判断 Victory/GameOver。
- 不把 Gate/Prop 的活动集合复制到 SpawnManager。

## 影响

- 需要新增 SpawnManager、敌人生成请求和道路对象生成请求的公共契约。
- LevelManager 的胜利判断必须依赖 SpawnManager 的查询接口。
- 需要测试同一时间、重开、终局停止和过期 `LevelRunId` 请求。

## 关联文档

- `../02_Modules/Level/README.md`
- `../02_Modules/Spawn/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/DataDictionary.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-009-FixedRoadSingleLevelTimeline.md`
