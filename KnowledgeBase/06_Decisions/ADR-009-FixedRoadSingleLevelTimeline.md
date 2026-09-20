# ADR-009：固定道路、单关卡时间轴与终局重开

## 状态

Accepted（生成位置部分由 ADR-023 替代）

## 日期

2026-09-14

## 后续修订

本 ADR 原定的左、中、右三个生成点、`spawnPoint` 编号和初始车道语义已由 ADR-023 替代。现行规则是在固定 `SpawnY` 横线上使用 `[0,1]` 的 `spawnPosition` 计算对象中心点；下文对应内容仅保留为历史决策背景。固定道路、无波次三类时间轴、敌人接近逻辑和终局判定仍然有效。

道路字段已由 ADR-052 收敛为原点居中的 `roadWidth`、`roadHeight`，四边由半宽和半高派生；ArmyRoot 每局从 `armySpawnPosition` 开始，道路不设置玩法 Collider。下文重复宽高/边界的表述按该后续决策理解。

## 背景

首版需要一个可运行的最小游戏闭环：道路宽高固定，军队位于道路下方并左右移动、自动射击；敌人、Gate 和 Prop 从道路顶部出现。当前设计曾使用波次，但首版实际需求是通过一组按时间排列的生成项控制单关卡，不引入波次状态。

## 决策

### 固定道路与三路生成

- 每个 `LevelConfig` 定义固定道路宽度、高度、左右边界、道路空间参数和三个生成点。
- 生成点按左、中、右编号 `0 / 1 / 2`。
- `spawnPoint` 只决定对象的初始位置，不作为生成后的移动约束。
- Army 的移动边界由道路左右边界和当前激活槽位合并 AABB 共同计算；任何激活槽位不得越过道路边界。
- 镜头和道路不持续向上推进；对象在固定道路空间中移动。

### 敌人移动

- 敌人从指定车道生成后，先沿道路向下移动。
- 到达 LevelConfig 提供的 `enemyApproachY` 后，查询最近的有效士兵槽位并向其移动。
- 进入攻击范围后按 ADR-005 执行单体或范围攻击。
- 进入接近阶段后不保留初始车道限制，目标失效时重新选择最近有效槽位。

### 时间轴生成

- `LevelConfig` 保存 `enemySpawns`、`gateSpawns`、`propSpawns` 三个列表。
- 每条生成项包含相对本局开始的 `spawnTime`、稳定配置 ID 和 `spawnPoint`。
- 列表按 `spawnTime` 非递减排序，同一时间按列表顺序处理。
- SpawnManager 为三类列表分别维护游标；每条生成项只处理一次。
- 终局后停止消费尚未到时的生成项，Gate/Prop 不参与敌人胜利条件。
- MVP 的 `enemySpawns` 必须至少包含一个条目；空敌人列表属于无效 `LevelConfig`，在进入 Gameplay 前以 `InvalidLevelConfig` 阻断，而不是进入后立即判定胜利。

### 胜负与同帧处理

- LevelManager 在一帧内的生成、伤害和死亡事件处理完成后统一判断终局。
- 失败条件：`ArmyCount <= 0`。
- 胜利条件：所有敌人生成项已处理，且 `EnemyManager.AliveEnemyCount == 0`。
- Army 归零优先于胜利；如果同一帧最后一只敌人死亡且 Army 归零，结果为失败。
- 终局转换幂等，只能触发一次 `Victory` 或 `GameOver`。

### 与应用流程的衔接

- 当前只支持一个关卡，`unlockedLevelIds` 只记录通关后的解锁结果，不执行下一关跳转。
- Victory 或 GameOver 后停止本局输入、射击、攻击和未来生成。
- 本 ADR 只定义 Gameplay 内的终局判定，不再规定终局后的页面跳转或等待时间。
- 终局后的应用流程、会话清理和重新进入同一关由 `ADR-011-ApplicationFlowAndGameplaySession.md` 定义。
- 进入新的 Gameplay 会话时，必须重置关卡计时、三类生成游标、EnemyManager、ObstacleManager、Army 和子弹状态。

## 不采用

- 首版不使用波次、波次间隔或下一波状态。
- 不让敌人永远锁定在初始生成车道。
- 不以敌人到达道路底部扣除 Army 人数；敌人按 ADR-005 在接近线后攻击 Army。
- 不因 Gate/Prop 尚未离场而延迟敌人胜利。
- 不把空敌人生成列表解释为可游玩的“立即胜利”关卡。

## 影响

- `LevelConfig` 和共享配置契约需要增加固定道路、三路生成点和三类时间轴字段。
- Level、Spawn、Monster、Army、Input、UI 和集成测试需要使用新的无波次规则。
- `EventCatalog` 不再提供 `WaveStarted`、`WaveCompleted`，`MonsterSpawned` 改为携带初始生成点编号，`Victory` 携带 `unlockedLevelIds`。
- 终局后回到 LevelSelect、等待 1 秒并重新开始同一关是当前临时应用流程；未来引入结果界面、下一关或存档时应重新评估。
- 配置校验和集成测试必须覆盖空 `enemySpawns` 被拒绝的情况。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../00_Project/Roadmap.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Spawn/README.md`
- `../02_Modules/Monster/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `ADR-013-SpawnCursorOwnershipAndDispatch.md`
