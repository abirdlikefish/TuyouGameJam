# ADR-071：关卡配置士兵子弹回收线

## 状态

Accepted

## 日期

2026-09-22

## 背景

士兵子弹原先以道路派生的 `TopBoundary` 作为统一回收线。该边界同时承担道路视觉范围和子弹生命周期，无法按关卡在道路内部提前结束子弹，也会让子弹生命周期随道路视觉尺寸变化。

## 决策

- `LevelConfig` 新增世界坐标字段 `bulletDespawnY: float`，默认值为 `3`；现有全部关卡资产显式保存该值。
- ConfigService 要求 `bulletDespawnY` 为有限值，并将其复制到 `LevelConfigSnapshot`。
- LevelManager 将该值写入 `RoadLayoutSnapshot.BulletDespawnY`；Gameplay Preparing 必须验证 `ArmySpawnPosition.y < BulletDespawnY <= TopBoundary`。
- `BulletDespawnY` 不要求高于 `EnemyApproachY` 或 `SpawnY`。关卡可以让子弹只覆盖道路下半段；目标移动进入该范围后仍可被命中。
- BulletManager 不再使用 `TopBoundary` 作为子弹生命周期阈值。子弹根 GameObject 中心严格大于 `BulletDespawnY` 时，在当前逻辑帧的既有回收阶段失活并归还对象池；等于该值时仍有效。
- 当单帧位移跨过 `BulletDespawnY` 时，子弹扫掠查询只覆盖当前位置到回收线的路径。回收线以上的目标不能在同一帧被命中，线以内的有效命中仍正常结算。
- 该字段只作用于由 Army 发射并由 BulletManager 管理的子弹，不作用于 ikun 篮球等 Prop。

## 取代关系

本决策取代 ADR-043 和 ADR-052 中“子弹以 `RoadLayoutSnapshot.TopBoundary` 回收”的规则。道路 `TopBoundary` 仍是道路几何上边界和 `BulletDespawnY` 的合法上限。

## 后果

- 子弹生命周期可按关卡独立调节，不再与道路视觉上边界强绑定。
- `LevelConfig`、不可变配置快照和道路运行时快照各增加一个字段。
- 新字段缺失或超出场景道路范围时按既有 Fail-Fast 规则阻止 Gameplay Ready，不提供回退到 `TopBoundary` 的兼容分支。
- 子弹仍由 BulletManager 和类型池统一回收，不调用 `Destroy`，也不由 Bullet 自己管理生命周期。

## 验收标准

- 当前 20 个关卡的 `bulletDespawnY` 均为 `3`。
- 子弹根中心等于 `3` 时保持有效，严格大于 `3` 后在当前逻辑帧回池。
- 单帧跨线时可以命中回收线以内的首个合法目标，但不能命中回收线以上的目标。
- 竖直和斜向子弹使用同一世界 Y 回收线；Sprite、Collider 和旋转不改变阈值。
- 非有限值、`BulletDespawnY <= ArmySpawnPosition.y` 或 `BulletDespawnY > TopBoundary` 会阻止配置或 Gameplay Ready。

## 关联文档

- `../02_Modules/Bullet/README.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/CollisionRules.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-043-FireAttackDeathAndContactBoundaries.md`
- `ADR-052-CenteredRoadAndConfigurableArmySpawn.md`
