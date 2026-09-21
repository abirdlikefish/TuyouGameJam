# 配置表契约

本文件登记 Luban 表、`LevelCatalog` 和 `LevelConfig` 之间的跨模块契约。字段的详细类型、范围和引用关系在表结构稳定后补充；模块文档只声明消费关系，不复制这里的定义。

## 配置目录

| 配置 | 形式 | 主责模块 | 当前状态 |
|---|---|---|---|
| `LevelCatalog` | Unity ScriptableObject | Config、LevelSelect | ContractReady |
| `LevelConfig` | Unity ScriptableObject | Config/Foundation（资产结构）；Level（内容） | ContractReady |
| `TbArmy` | Luban 表 | Army | ContractReady |
| `TbWeapon` | Luban 表 | Army、Bullet | ContractReady |
| `TbEnemy` | Luban 表 | Monster | ContractReady |
| `TbProp` | Luban 表 | Prop | ContractReady |
| `TbBullet` | Luban 表 | Bullet | ContractReady |

`TbGameSettings` 不属于当前 MVP；在出现真实的全局可调参数前不建立该表。

所有 LevelId、Luban 表主键和外键都必须为非负整数，`0` 是合法 ID；`TbArmy`、`TbWeapon`、`TbEnemy`、`TbProp`、`TbBullet` 每张表的首行都使用 `Id = 0`，除已明确固定的 ID 外不要求后续编号连续。不得把 `0` 当作缺失值；无引用只能通过可空字段或已定义的条件字段表达。所有浮点配置必须为有限值。`MoveSpeed` 的单位统一为世界单位/秒，`FireInterval`、`AttackCooldown` 和元素持续时间的单位统一为秒。

## Luban 表初始字段

### `TbArmy`

初始字段：`Id`、`ArmyCountLimit`、`HpPerSoldier`、`MoveSpeed`。

MVP 固定读取首行 `TbArmy.Id = 0`，初始人数固定为 `1`，不配置 `InitialCount`。`ArmyCountLimit = 0` 表示不设逻辑总人数上限；大于 `0` 时必须至少为 `1`。`HpPerSoldier` 为大于 `0` 的整数，`MoveSpeed` 必须有限且大于等于 `0`，单位为世界单位/秒。实际位移使用 `horizontalInput × MoveSpeed × 有效玩法 delta`，其中触屏输入可按 [ADR-028](../06_Decisions/ADR-028-MvpRelativeDragInput.md) 的 Inspector 系数缩放。

最大可见士兵数不属于 Luban。GameplaySceneEntry 按 `ArmyId = 0` 选择序列化 Army Prefab，ArmyController 的 `SlotCapacity = slots.Length` 是显示容量的唯一来源。当前武器和三种元素剩余时间都是本局运行时状态，不写入 `TbArmy`。

### `TbWeapon`

初始字段：`Id`、`FireInterval`、`BulletId`。

`Id` 是唯一的武器身份，固定 `0 = Slingshot`、`1 = Bow`、`2 = Staff`、`3 = FireStaff`、`4 = IceStaff`、`5 = LightningStaff`、`6 = FireIceStaff`、`7 = FireLightningStaff`、`8 = IceLightningStaff`、`9 = FireIceLightningStaff`，十行都必须存在且不得把 `0` 当作缺失值或无武器哨兵。每个武器引用独立 BulletId。每个活动槽位按对应行的 `FireInterval` 独立触发一次射击；WeaponId 0～1 生成一枚子弹，WeaponId 2～8 按 `(0,1)`、`(-3,13).normalized`、`(3,13).normalized` 固定生成三枚等速子弹。WeaponId 9 仍以自身 `FireInterval` 触发三发齐射，但每颗分别从 WeaponId 3～8 选择 BulletId 和元素掩码，中弹竖直，左右弹各自偏角在五秒内按 `0°→30°→0°→30°` 变化。`FireInterval` 必须有限且大于 `0`，单位为秒，也是 Attack、MoveLeft、MoveRight 的权威攻击周期。本局初始活动槽位和实际换到不同武器的活动槽位在周期第 0 帧立即发射；运行中新激活或重新激活的槽位等待完整间隔。单槽每逻辑帧最多触发一次射击且不追赶补发，但保留周期余量；重复当前 WeaponId 不重置。代表人数不缩放射速、伤害或弹丸数量。

当前不建立 `TbElement`。火、冰、雷使用固定 `ElementType`，元素门按 LevelConfig 中的关卡系数和 HP 归零后的额外伤害计算持续时间，Army 保存本局剩余时间；具体元素效果进入范围后再决定是否新增元素配置表。

### `TbEnemy`

初始字段：`Id`、`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange`、`AttackCooldown`。

首版 `EnemyType` 为 `Chick`、`Hen`、`Rooster`、`Ikun`，固定值分别为 `0`、`1`、`2`、`3`。`MaxHp`、`AttackPower` 必须大于 `0`；`MoveSpeed` 必须有限且大于等于 `0`，单位为世界单位/秒；`AttackStartRange`、`AttackCooldown` 必须有限且大于等于 `0`，冷却单位为秒。攻击类型由敌人类型固定派生：小鸡敌人为 `SingleTarget`，母鸡、公鸡和 ikun 为 `Area`，不重复配置 `AttackType`。`AttackStartRange` 使用怪物与锁定槽位目标位置的 XY 欧氏距离，距离为 `0` 的重合状态同样允许开始攻击，不使用 `TargetSensor`。`AttackCooldown` 表示两次攻击开始之间的最短时间，从每次攻击开始时递减；动画结束时若已到期，可在下一次 EnemyManager Tick 重新验证后起攻。道路接近线由 `LevelConfig` 的关卡空间配置提供。首版不配置 `ContactDamage`，敌人到达道路偏下接近线后向军队接近，不继续向底部移动。EnemyManager 通过 Inspector 分别绑定四个具体根类型的规范 Prefab，按 `EnemyType` 选择类型池；Prefab 与池身份不写入 Luban。敌人间阻挡安全间距由各规范 Prefab 根脚本的 `blockingGap` 提供。ikun 的唯一 Prefab 另外提供篮球生成间隔与生成点；这些类型专属参数不加入 `TbEnemy` 或 LevelConfig，见 ADR-064。

### `TbProp`

字段：`Id`、`WeaponId`、`MaxHp`、`ContactDamage`、`MoveSpeed`、`PropType`、`ArmyAddition`。

`PropType` 固定为 `WeaponBox=0`、`Basketball=1`、`GooseCage=2`。配置 ID `0~2` 保持三种武器箱，ID `3` 为篮球，ID `4` 为鹅笼。ObstacleManager 按类型选择 `WeaponProp`、`BasketballProp` 或 `GooseCageProp` 的规范 Prefab与类型池；关卡 `propSpawns` 对三类道具统一引用 `TbProp.Id`。

每条 `TbProp` 必须满足 `MaxHp > 0`、`ContactDamage > 0`，以及有限非负的 `MoveSpeed`。武器箱要求有效 `WeaponId` 且 `ArmyAddition=0`；篮球要求中性 `WeaponId=0`、`ArmyAddition=0`；鹅笼要求中性 `WeaponId=0` 且 `ArmyAddition>0`。

### `TbBullet`

初始字段：`Id`、`Damage`、`MoveSpeed`。

每条 `TbBullet` 必须满足 `Damage > 0`，`MoveSpeed` 有限且大于等于 `0`，速度单位为世界单位/秒。固定 BulletId 0～9 分别对应十个固定 WeaponId 的独立子弹配置和 Loop 动画。MVP 子弹固定为命中首个有效目标后回收，不配置 `CollisionBehavior`。全部子弹共用一个 `Bullet` 根类型、规范 Prefab 和类型池；数值与表现按 `BulletId` 初始化，Prefab 与池身份不写入 Luban。

实际表不提供绕过上述契约的隐式默认值；非法主键、引用、枚举、整数或浮点值由 ConfigService 在启动时拒绝。

## 类型化运行时快照

ConfigService 是 Luban 生成行到运行时配置的唯一转换边界。初始化成功后只通过以下最小 Provider 返回不可变快照：

Luban 生成的 `cfg.Tables` 与表行编入 `Game.ConfigGenerated`，只供 Foundation 的具体 ConfigService 和 Composition 启动装配引用；以下 Provider 与快照均定义在 Contracts，不暴露生成类型。

| Luban 表 | Provider | 快照 |
|---|---|---|
| `TbArmy` | `IArmyConfigProvider` | `ArmyConfigSnapshot(Id, ArmyCountLimit, HpPerSoldier, MoveSpeed)` |
| `TbWeapon` | `IWeaponConfigProvider` | `WeaponConfigSnapshot(Id, FireInterval, BulletId)` |
| `TbBullet` | `IBulletConfigProvider` | `BulletConfigSnapshot(Id, Damage, MoveSpeed)` |
| `TbEnemy` | `IEnemyConfigProvider` | `EnemyConfigSnapshot(Id, EnemyType, MaxHp, AttackPower, MoveSpeed, AttackStartRange, AttackCooldown)` |
| `TbProp` | `IPropConfigProvider` | `PropConfigSnapshot(Id, PropType, WeaponId, ArmyAddition, MaxHp, ContactDamage, MoveSpeed)` |

Provider 使用必得 `GetXxxConfig(id)`；它只接受初始化引用链中已校验的 ID，不返回默认行或可变 Luban 对象。`TryGetLevelConfig` 只用于应用层验证关卡选择，不作为 Gameplay 数值表查询模式。

`LevelCatalog`、`LevelConfig` 和三类可序列化生成条目属于 Foundation 配置实现。ConfigService 在启动校验后把每个关卡资产复制为 Contracts 中的不可变 `LevelConfigSnapshot` 与三类生成条目快照；SceneService、GameplaySceneEntry、Level 和 Spawn 都不得持有 ScriptableObject 资产。具体 `ConfigService.Initialize(LevelCatalog, cfg.Tables, IResourceRegistry)` 只由 Composition 调用，不进入 `IConfigService` 查询契约。

## Unity 关卡资产

### `LevelCatalog`

`LevelCatalog` 是初始化阶段读取的关卡目录，至少包含：

```text
entries[].levelConfig       LevelConfig 资产引用
entries[].initiallyUnlocked bool
```

目录不重复保存 `levelId`；每个条目的 ID 由其 `LevelConfig.levelId` 提供。当前目录只包含第一关，且第一关默认解锁。目录资产和关卡资产均为只读配置。ADR-062 规定启动时把 `initiallyUnlocked`、有效存档解锁和有效已完成集合取并集；任何存档状态都不得回写这些资产。

### `LevelConfig`

`LevelConfig` 是首版唯一实际关卡配置资产，负责：

- 关卡 ID 和展示信息。
- 通关后应解锁的关卡 ID 列表 `unlockedLevelIds`；它不是当前玩家已解锁状态。空列表表示结算时没有下一关；非空列表的首个有效 ID 是“挑战下一关”的目标，其余有效 ID 仍加入运行期解锁集合。重复 ID 或当前 `levelId` 自引用按 `InvalidLevelConfig` 致命失败；当前 LevelCatalog 中不存在的未来关卡 ID 使用 `Debug.LogWarning` 后忽略。ConfigService 保持其余有效 ID 的原始顺序并只把过滤后的防御性副本写入 `LevelConfigSnapshot.UnlockedLevelIds`。
- 固定道路只配置 `roadWidth`、`roadHeight`，中心永久为世界原点，四边由半宽和半高派生；ArmyRoot 初始世界坐标使用 `armySpawnPosition: Vector2`。
- `spawnY`、`enemyApproachY`、`despawnY`、`bulletDespawnY` 等关卡空间参数；`bulletDespawnY` 默认值为 `3`，控制士兵子弹根中心严格越过该世界 Y 后回池。
- 本关元素门统一使用的 `elementDurationSecondsPerDamage`。
- `ikunBasketballConfigId`：包含 ikun 时必须引用 `PropType.Basketball`；无 ikun 时使用中性值 `0`。
- `enemySpawns`、`gateSpawns`、`propSpawns` 三个按本局开始时间编排的列表。

`levelId` 必须为非负整数且在目录中唯一。`roadWidth`、`roadHeight` 必须有限且大于 0，`armySpawnPosition` 分量必须有限且位于派生道路边界内。ArmyRoot 每局从该坐标开始且只沿世界 X 轴移动。MVP 的对象生成与离场顺序固定为 `BottomBoundary <= despawnY < armySpawnPosition.y < enemyApproachY < spawnY <= TopBoundary`；子弹回收线单独满足 `armySpawnPosition.y < bulletDespawnY <= TopBoundary`，不要求高于 `enemyApproachY` 或 `spawnY`。道路只提供数值边界与视觉，不设置玩法 Collider。

所有生成项包含：

```text
spawnTime  相对本局开始的秒数，非负
spawnPosition 道路从左到右的归一化出生位置，范围为 [0,1]
```

`enemySpawns` 和 `propSpawns` 另外保存分别引用 `TbEnemy`、`TbProp` 的稳定 `configId`。`gateSpawns` 不保存配置 ID，而是直接保存：

```text
gateType     Additive / Element
initialValue 仅 Additive 使用
elementType  仅 Element 使用，必须为 Fire / Ice / Lightning
maxHp        仅 Element 使用且大于 0
```

Additive 条目的 `elementType = None`、`maxHp = 0`；Element 条目的 `initialValue = 0`。未使用字段不得被运行时读取。本关存在元素门时，`elementDurationSecondsPerDamage` 必须有限且大于 `0`；不存在元素门时使用中性值 `0`。条件字段不符合规则时以 `InvalidLevelConfig` 报告 LevelConfig 资产和生成项索引，并按统一配置致命语义退出应用。

Additive 条目的 `initialValue` 不得为 `int.MinValue`，避免负数门请求减员取绝对值时越界。运行时门值正向累加及 Army 人数、槽位聚合 HP、元素持续时间的计算必须使用足够宽的中间类型，并在超过公开存储类型时饱和到最大有限值，不能发生整数回绕、NaN 或无穷值。

生成列表按 `spawnTime` 非递减排序；同一时间按列表顺序处理。每条敌人、Gate、Prop 生成项都必须提供有限且位于 `[0,1]` 的 `spawnPosition`，越界、NaN 或无穷值以 `InvalidLevelConfig` 报错并退出应用。SpawnManager 使用 LevelManager 从道路宽高构建的 RoadLayoutSnapshot，按左右边界线性计算 `x`，使用固定 `spawnY` 作为 `y`；坐标以对象中心点为准，不考虑 Collider、Renderer 或 Prefab 尺寸。该值不约束对象生成后的移动路径。MVP 的 `enemySpawns` 至少包含一个条目；`gateSpawns` 和 `propSpawns` 可以为空。

LevelConfig 不保存敌人 HP、子弹伤害等表驱动的复用数值。Gate 是例外：每条门生成项直接保存初始数字、元素类型和元素门 MaxHp；加法门统一速度来自 AdditiveGate Prefab，元素门统一速度和接触伤害来自 ElementGate Prefab。当前门数字、当前 HP 和 HP 归零后的额外伤害仍属于运行时状态。

## 引用关系

```text
LevelCatalog.entries[].levelConfig.levelId -> LevelId
LevelConfig.enemySpawns[].configId -> TbEnemy.Id
LevelConfig.propSpawns[].configId  -> TbProp.Id
LevelConfig.ikunBasketballConfigId -> TbProp.Id（仅 ikun 关卡，且必须是 Basketball）
TbWeapon.bulletId          -> TbBullet.Id
TbProp.weaponId            -> TbWeapon.Id（仅 WeaponBox）
```

参与玩法碰撞的 Prefab 必须按 `CollisionRules.md` 配置对应的 `Collider2D`、职责 Layer 和稳定职责名称。池化规范 Prefab 由对应 Manager 的 Inspector 引用提供，一个具体池化根类型只绑定一个 Prefab；Prefab、碰撞形状、阵型槽位、发射点与对象生命周期属于 Unity 资源或运行时状态，不写入 Luban 数值表，MVP 不通过 Luban `PrefabKey` 间接引用这些对象。

所有数值、枚举、主键、跨表引用和目录内 LevelConfig 条件字段必须在 ConfigService 启动初始化时完成验证。任一 Luban 表缺少首行 `Id = 0`、找不到目录条目、重复 `levelId`、缺少任一固定 `TbWeapon` 行、空 `enemySpawns`、无效目标 ID 或无效条件字段时，ConfigService 只报告首个包含稳定来源的错误，将状态置为 `Failed` 并立即退出应用；不得进入关卡选择后才由 Manager 补做同一配置检查。必需的 Unity Prefab、Collider、Layer 和 Inspector 绑定在进入 Gameplay 前单独验证；序列化 Army Prefab 绑定必须包含唯一的 `ArmyId = 0`，其槽位数组必须至少包含一个有效槽位。

## 运行时状态边界

以下字段不得写入配置表作为运行时回写值：当前人数、当前槽位人数、当前槽位 HP、槽位补充标记、当前 WeaponId、火/冰/雷剩余持续时间、当前门数字、当前门/道具 HP、元素门 `PostDepletionDamage`、奖励锁定状态、接触判定状态、运行时实例 ID、生成游标、关卡运行时间和胜负状态。它们属于 ArmyController、Monster、Gate、Prop、ObstacleManager、LevelManager 和 GameStateService 的运行时状态；已完成/已解锁集合只写入玩家进度文件，不回写 LevelCatalog、LevelConfig 或 Luban 表。分数仍不属于 MVP 状态模型。

## 变更规则

- 新增跨模块字段先更新本文件，再更新对应模块文档和表结构。
- 修改主键或引用语义必须新增 ADR 或更新现有 ADR。
- 生成代码只读；代码问题回溯到表、`luban.conf` 或模板。
- 当前 MVP 字段边界见 `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`；延期能力进入范围前不得预留空字段。
- Army、Prefab、武器和三元素字段对 ADR-020 的修订见 `../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`。
- Gate 内联配置、伤害驱动数字和元素门额外伤害换算见 `../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`。
- 固定出生横线与 `spawnPosition` 的坐标解析和校验见 `../06_Decisions/ADR-023-NormalizedSpawnPosition.md`。
- 类型化 Provider、不可变快照与配置错误单点退出见 `../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`。
- LevelConfig 资产、运行时快照与程序集依赖边界见 `../06_Decisions/ADR-042-LevelConfigSnapshotAssemblyBoundary.md`。
- 士兵子弹的关卡回收线与跨线命中边界见 `../06_Decisions/ADR-071-LevelConfiguredBulletDespawnY.md`。
