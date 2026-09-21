# ADR-064：ikun 敌人与篮球道具

## 状态

Accepted

## 日期

2026-09-21

## 背景

现有敌人只有 Chick、Hen、Rooster，且只有关卡时间轴可以生成道路对象；现有 Prop 实现也只覆盖击破后切换武器的 WeaponProp。新需求增加范围攻击 Boss 敌人 ikun，以及由 ikun 在到达 `EnemyApproachY` 前周期生成、击破后无收益的篮球道具。

## 决策

1. 新增 `EnemyType.Ikun = 3`、`IkunMonster`、规范 Prefab 和独立具体类型池。ikun 的普通攻击与 Rooster 相同，使用前方 `AttackCollider` 执行 `Area` 攻击。
2. ikun 只在本局仍存活且运行状态为 `MovingDown` 时推进篮球计时。第一颗篮球必须等待一个完整间隔；到达 `EnemyApproachY` 后永久停止生成，即使随后被效果位移到接近线上方也不恢复。
3. `PF_Monster_Ikun` 显式绑定 `basketballSpawnPoint` 和正数 `basketballSpawnInterval`。生成点决定“面前”的世界坐标，不在代码中硬编码偏移。本轮不把专属技能参数加入 `TbEnemy`。
4. 新增 `BasketballProp`、`PF_Prop_Basketball` 和独立具体类型池。篮球的正数 `maxHp`、正数 `contactDamage` 与非负有限 `moveSpeed` 由唯一规范 Prefab 提供，不加入只描述武器箱的 `TbProp`。
5. WeaponProp 与 BasketballProp 复用可击破道路道具的生命、子弹去重、接触、离场和回池规则。篮球在 Pending 状态被击破时不执行任何奖励或 Army 命令，只发布篮球击破事实并回收。
6. 篮球沿用武器箱失败语义：第一次与 Army 重叠时对当次去重后的每个有效槽位造成一次相同伤害并进入 `Failed`；之后继续移动，子弹仍被消费但 HP 最低锁为 `1`，不得再次击破或产生收益。
7. 敌人请求生成篮球属于必须执行的同步命令，由 `IBasketballSpawner` 实现；EventBus 只发布已经生成或击破的事实。运行时请求携带 `LevelRunId`、来源敌人运行时 ID 和世界坐标，不伪造 LevelConfig 的 `SpawnEntryIndex` 或 `TbProp.ConfigId`。
8. ikun 死亡只停止后续生成，已经生成的篮球继续独立移动、受击、接触和离场。篮球与其他 Gate/Prop 一样不进入胜利条件；胜利仍只要求敌人时间轴派发完成且存活敌人数为零，结算 StopRun 统一清理残留篮球。
9. ikun 的新序列帧、专属 Controller 和正式视觉延期。本轮使用可编译、可校验的占位表现边界，不修改现有 Rooster 的 `Boss` 动画技术身份。

## 不采用

- 不给篮球伪造 WeaponId；`0` 是有效弹弓身份，负数也不作为配置缺失哨兵。
- 不让 EnemyManager 直接操作 ObstacleManager 私有集合，也不通过事实事件监听器执行生成命令。
- 不让篮球阻止胜利，不因 ikun 死亡级联销毁已生成篮球。
- 不为唯一篮球新增 Luban 表或扩张 `TbProp` 条件字段；出现多种篮球或按敌人配置调参需求时再评估迁移。

## 验收

- ikun 使用独立 EnemyType、Prefab 和池，普通攻击与 Rooster 一样按 AttackCollider 对范围内槽位各结算一次。
- ikun 出生后经过完整间隔才生成第一颗篮球；仅在 MovingDown 阶段按间隔生成，到达接近线、死亡、StopRun 或回池后不再生成。
- 篮球从绑定的 SpawnPoint 出生，使用 Prop Layer、Kinematic Rigidbody2D、BodyCollider 和 BulletHitProxy，参与既有子弹与接触阶段。
- 篮球接触前可被击破且不改变武器、人数或元素；接触后只伤害当次有效槽位并锁定失败，之后不能击破。
- ikun 死亡后已有篮球继续存在；敌人清空时仍可立即胜利，残留篮球由结算清理。
- 对象池复用不会残留篮球 HP、子弹去重、接触状态或 ikun 技能计时。

## 关联文档

- `ADR-022-PropBreakEffectBoundary.md`
- `ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`
- `ADR-043-FireAttackDeathAndContactBoundaries.md`
- `ADR-055-KinematicBulletTargetAdapters.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Prop/README.md`
- `../03_SharedContracts/CollisionRules.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
