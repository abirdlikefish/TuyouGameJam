# ADR-052：原点居中道路与可配置 Army 出生坐标

## 状态

Accepted

## 日期

2026-09-21

## 背景

ADR-034 使用世界坐标 `Rect roadBounds` 描述道路，并固定 ArmyRoot 从世界原点开始。当前关卡道路实际都以世界原点为中心，继续序列化左下角会暴露不需要的自由度；同时 Army 初始坐标需要由关卡独立配置，不能继续把世界原点同时作为道路中心和 Army 出生点。

## 决策

- `LevelConfig` 只序列化有限且大于零的 `roadWidth`、`roadHeight`，道路中心永久固定为世界原点，不配置道路中心或左下角。
- 道路四边唯一派生为 `±roadWidth / 2` 与 `±roadHeight / 2`；`RoadLayoutSnapshot` 在构造时完成派生，并继续向现有消费者公开宽高和四边。
- `LevelConfig` 新增世界坐标 `armySpawnPosition: Vector2`。ArmyRoot 每局从 `(x, y, 0)` 开始，只沿世界 X 轴移动，运行期间世界 Y 固定为配置的 `y`。
- `GameplaySceneEntry` 只负责实例化 Army Prefab；ArmyController 是出生坐标的唯一应用者。
- 初始 Army 激活槽位的合并 AABB 必须能在配置坐标下落入道路左右边界；不满足时 Preparing 失败，不静默修正出生坐标。横移及后续阵型扩张仍使用既有道路限位。
- 敌人、Gate 和 Prop 的 `spawnPosition`、`spawnY` 及对象中心坐标规则不变；道路继续不设置玩法 Collider。

## 配置与验证

```text
LevelConfig
├── roadWidth: float
├── roadHeight: float
├── armySpawnPosition: Vector2
├── spawnY: float
├── enemyApproachY: float
└── despawnY: float
```

进入 Gameplay 前必须验证：

```text
所有标量和坐标分量均为有限值
roadWidth > 0
roadHeight > 0
LeftBoundary <= armySpawnPosition.x <= RightBoundary
BottomBoundary <= armySpawnPosition.y <= TopBoundary
BottomBoundary <= despawnY < armySpawnPosition.y
armySpawnPosition.y < enemyApproachY < spawnY <= TopBoundary
```

Army 根坐标只校验点位置；依赖 Prefab Collider 的初始阵型 AABB 校验由 ArmyController 在 Preparing 中完成。

## 迁移

第一关原 `roadBounds = (-2.5, -7.5, 5, 15)` 迁移为 `roadWidth = 5`、`roadHeight = 15`，并使用 `armySpawnPosition = (0, 0)` 保持当前表现。不保留旧 Rect 的运行时兼容或回退。

## 取代关系

本决策取代 ADR-034 中 `roadBounds: Rect`、道路只需包含原点以及 ArmyRoot 固定世界原点的规则。ADR-034 的无道路 Collider、数值边界、对象中心和归一化生成位置规则继续有效。

## 验收标准

- 任意合法宽高都产生以世界原点为中心且相互一致的宽高和四边。
- Army 每局精确从配置坐标开始，横移期间保持配置 Y，重新进入关卡时恢复配置坐标。
- 非法尺寸、Army 坐标、纵向线顺序或初始阵型越界会阻止 Gameplay Ready。
- 归一化生成、道路视觉、接近、离场和子弹上边界回收继续使用派生道路边界。

## 关联文档

- `../01_Architecture/ConfigurationSystem.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Army/README.md`
- `../02_Modules/Spawn/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-034-NumericRoadBoundsAndArmyOrigin.md`
