# ADR-023：固定出生横线与归一化横向出生位置

## 状态

Accepted

## 日期

2026-09-18

## 背景

原方案在 `LevelConfig` 中配置左、中、右三个生成点，每条敌人、Gate 和 Prop 生成项通过 `spawnPoint` 编号选择其中一个位置。该方案把关卡编排限制为三路，不能连续调整对象在道路宽度上的出生位置。

## 决策

- 移除三路生成点和生成点 ID。敌人、Gate、Prop 共用一条由 `LevelConfig.spawnY` 定义的固定出生横线；左右边界由 ADR-034 的唯一 `roadBounds` 派生。
- 每条 `enemySpawns`、`gateSpawns`、`propSpawns` 生成项使用 `spawnPosition`，类型为 `float`，有效范围为闭区间 `[0, 1]`。
- `spawnPosition = 0` 表示道路最左边界，`spawnPosition = 1` 表示道路最右边界，中间值按道路宽度线性插值：

```text
WorldX = LeftBoundary + (RightBoundary - LeftBoundary) × spawnPosition
WorldY = SpawnY
```

- 出生坐标以对象 Transform 中心点为基准，不读取或补偿敌人、Gate、Prop 的 Collider、Renderer、Prefab 尺寸或半宽。位于 `0` 或 `1` 时允许对象视觉或碰撞形状越过道路边界。
- ConfigService 在关卡加载期校验每条生成项的 `spawnPosition`。小于 `0`、大于 `1`、NaN 或无穷值均返回 `InvalidLevelConfig`，不得静默 Clamp。
- SpawnManager 将生成项的 `spawnPosition` 解析为 `WorldPosition`。生成请求同时携带 `SpawnPosition` 和解析后的 `WorldPosition`，EnemyManager 与 ObstacleManager 不重复计算坐标。
- 归一化出生位置只决定初始中心点，不约束对象生成后的移动路径。

## 替代关系

本 ADR 替代 ADR-003、ADR-009、ADR-013 和 ADR-021 中关于“三路生成点”“生成点编号”及 `LaneSpawnPoints` 的规则；固定道路、世界 XY 坐标、三类独立时间轴和敌人接近逻辑继续有效。

## 影响

- `RoadLayoutSnapshot` 保留 `SpawnY`、`LeftBoundary` 和 `RightBoundary`，移除 `LaneSpawnPoints`；后续 ADR-034 又加入从 `roadBounds` 派生的 BottomBoundary、TopBoundary、Width 和 Height。
- `EnemySpawnRequest`、`GateSpawnRequest` 和 `PropSpawnRequest` 用 `float SpawnPosition` 替代 `int SpawnPoint`；其中 Gate/Prop 类型化请求由 ADR-038 取代原统一的 `ObstacleSpawnRequest`。
- `MonsterSpawned` 不再携带生成点编号，改为携带归一化出生位置和解析后的初始世界坐标。
- 关卡配置、模块说明和测试必须覆盖 `0`、`1`、中间值及非法范围，并验证计算不考虑对象尺寸。
