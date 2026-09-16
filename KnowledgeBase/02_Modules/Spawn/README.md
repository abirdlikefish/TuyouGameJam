# Spawn 生成与对象池模块

## 模块信息

- ID：`MOD-SPAWN`
- 层级：Gameplay / Infrastructure
- 状态：`Planned`
- 依赖：ConfigService、PoolService、TimeService、EnemyManager、ObstacleManager、Level
- 决策：`../../06_Decisions/ADR-009-FixedRoadSingleLevelTimeline.md`、`../../06_Decisions/ADR-013-SpawnCursorOwnershipAndDispatch.md`

## 职责

- 按 `LevelConfig` 的时间轴安排敌人、Gate 和 Prop 的生成。
- 唯一维护 `enemySpawns`、`gateSpawns`、`propSpawns` 的游标。
- 接受 LevelManager 传入的当前 `LevelRunId` 和 `elapsedTime`，在每次 `Tick` 中消费到时条目；拒绝过期会话的 Tick。
- 将每条生成项的配置 ID 和三路生成点编号解析为生成请求。
- 向 EnemyManager 发送敌人生成请求，向 ObstacleManager 发送 Gate/Prop 生成请求。
- 在 LevelManager 进入终局后停止消费未来生成项，并在新会话开始时重置三个游标。

## 生成项契约

每条生成项至少包含：

```text
spawnTime  本局开始后的相对秒数
configId   对应 TbEnemy / TbGate / TbProp 的稳定 ID
spawnPoint 左、中、右三路编号
```

`spawnPoint` 只决定对象的初始位置，不约束生成后的路径。敌人生成后先由 Monster 沿道路向下移动，到达接近线后再向最近 Army 槽位移动。

生成项应按 `spawnTime` 非递减顺序配置；同一列表中相同时间的项按列表顺序处理。三个列表之间不定义额外的跨类型顺序。每个游标只递增一次，不能因为对象回收或重开前的重复更新而重复生成。

## 生命周期约束

- 每次生成必须获得新的 `RuntimeInstanceId`。
- 每次生成请求必须携带当前 `LevelRunId`。
- Spawn 只负责请求生成，不维护敌人、门或道具的活动列表、生命值和接触规则。
- `AreAllEnemySpawnsDispatched(LevelRunId)` 只表示指定会话的敌人时间轴已消费完成，不表示当前敌人已经死亡；过期会话不得返回当前会话结果。
- 对象成功回收或从道路下方离场后，由对应 Manager 注销并归还对象池。
- 终局后不再生成尚未到时的对象；新会话调用一次 `StartRun(LevelConfig, LevelRunId)`，绑定本关配置并将所有游标归零。

## 测试标准

- 三类生成列表在精确的 `spawnTime` 触发，时间相同的项保持列表顺序。
- LevelManager 每帧传入的 `elapsedTime` 不会导致同一条目重复生成。
- 每条生成项只生成一次，游标与配置列表长度一致。
- 三个生成点编号正确映射到道路左、中、右初始位置。
- 生成位置只影响初始位置，敌人进入接近线后可以跨越车道选择最近士兵。
- EnemyManager 收到敌人请求并正确注册敌人。
- ObstacleManager 收到 Gate/Prop 请求并正确注册运行时实例。
- 同一配置对象的多个实例可以同时存在且可独立回收。
- 对象再次使用前，数字、HP、接触状态、位置和运行时 ID 已重置。
- 过期 `LevelRunId` 的生成请求不会创建对象。
- 每次 Gameplay 会话只调用一次 `StartRun`，且不会残留上局游标。
