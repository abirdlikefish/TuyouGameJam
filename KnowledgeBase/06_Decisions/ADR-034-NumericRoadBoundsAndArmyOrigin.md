# ADR-034：数值道路边界与 Army 世界原点

## 状态

Superseded by [ADR-052](ADR-052-CenteredRoadAndConfigurableArmySpawn.md)

## 日期

2026-09-19

## 背景

LevelConfig 先前同时描述道路宽度、高度、道路坐标和左右边界，没有规定哪组字段是权威来源，也无法在字段互相冲突时进行唯一校验。玩法只需要数值边界、生成线、敌人接近线和道路对象离场线，不需要道路物理碰撞体。

## 决策

- `LevelConfig` 使用一个世界坐标 `Rect roadBounds` 作为道路空间的唯一序列化边界，运行时 `z = 0`。
- `LeftBoundary`、`RightBoundary`、`BottomBoundary`、`TopBoundary`、`Width` 和 `Height` 全部从 `roadBounds` 派生，不再重复序列化宽高或四条边界。
- `ArmyRoot` 每次本局开始时重置到世界坐标 `(0, 0, 0)`，只沿世界 X 轴移动；其世界 Y 固定为 `0`。阵型槽位继续使用 ArmyRoot 下的局部坐标。
- `roadBounds` 必须包含世界原点。Army 横向移动以激活槽位合并后的世界 AABB 约束在左右边界内，而不是只限制 ArmyRoot 中心。
- 敌人、Gate 和 Prop 共用 `spawnY`；同时保留 `enemyApproachY` 和 `despawnY`。
- 道路不创建玩法 Collider。边界、接近和离场均通过数值比较实现；道路 Sprite 或 Prefab 只负责视觉表现。
- 生成坐标仍以对象 Transform 中心计算，`spawnPosition == 0/1` 时不按对象尺寸内缩。

## 配置与验证

```text
LevelConfig
├── roadBounds: Rect
├── spawnY: float
├── enemyApproachY: float
└── despawnY: float
```

进入 Gameplay 前必须验证：

```text
所有值均为有限值
roadBounds.width > 0
roadBounds.height > 0
roadBounds.xMin <= 0 <= roadBounds.xMax
roadBounds.yMin <= 0 <= roadBounds.yMax
roadBounds.yMin <= despawnY < 0
0 < enemyApproachY < spawnY <= roadBounds.yMax
```

MVP 采用 Army 位于原点、敌人从上方接近、未接触道路对象越过 Army 后离场的空间关系，因此上述 Y 顺序是配置契约，不由运行时代码静默修正。

## 运行时快照

`RoadLayoutSnapshot` 至少提供：

```text
Width
Height
LeftBoundary
RightBoundary
BottomBoundary
TopBoundary
SpawnY
EnemyApproachY
DespawnY
```

RoadView 可以根据快照调整 SpriteRenderer 或 Transform，但不添加 Collider2D，不成为玩法边界权威。

## 不采用

- 不同时序列化宽度、高度、中心点和四条边界。
- 不使用道路 Collider、碰撞回调或物理推挤限制 Army 和道路对象。
- 不从道路 Sprite、Renderer Bounds 或摄像机视口反推玩法边界。
- 不按敌人、Gate 或 Prop 的尺寸修正归一化出生位置。

## 验收标准

- 修改 `roadBounds` 后，派生宽高和四条边界始终一致。
- 无效 Rect、未包含原点或 Y 线顺序错误时以 `InvalidLevelConfig` 阻止进入 Gameplay。
- ArmyRoot 每局从 `(0,0,0)` 开始并保持 y=0，所有激活槽位的合并 AABB 不越过左右边界。
- 道路没有玩法 Collider 时，Army 限位、生成、接近和离场仍能完成。

## 关联文档

- `../01_Architecture/ConfigurationSystem.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Army/README.md`
- `../02_Modules/Spawn/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/CollisionRules.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `ADR-023-NormalizedSpawnPosition.md`
