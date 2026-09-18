# 配置表契约

本文件登记 Luban 表、`LevelCatalog` 和 `LevelConfig` 之间的跨模块契约。字段的详细类型、范围和引用关系在表结构稳定后补充；模块文档只声明消费关系，不复制这里的定义。

## 配置目录

| 配置 | 形式 | 主责模块 | 当前状态 |
|---|---|---|---|
| `LevelCatalog` | Unity ScriptableObject | Config、LevelSelect | Planned |
| `LevelConfig` | Unity ScriptableObject | Level | Planned |
| `TbArmy` | Luban 表 | Army | Planned |
| `TbWeapon` | Luban 表 | Army、Bullet | Planned |
| `TbEnemy` | Luban 表 | Monster | Planned |
| `TbProp` | Luban 表 | Prop | Planned |
| `TbBullet` | Luban 表 | Bullet | Planned |

`TbGameSettings` 不属于当前 MVP；在出现真实的全局可调参数前不建立该表。

## Luban 表初始字段

### `TbArmy`

初始字段：`Id`、`ArmyCountLimit`、`HpPerSoldier`、`MoveSpeed`。

MVP 固定读取 `TbArmy.Id = 1`，初始人数固定为 `1`，不配置 `InitialCount`。`ArmyCountLimit = 0` 表示不设逻辑总人数上限；大于 `0` 时必须至少为 `1`。`HpPerSoldier` 为大于 `0` 的整数，`MoveSpeed` 必须有限且大于等于 `0`。实际位移使用 `horizontalInput × MoveSpeed × 有效玩法 delta`，其中触屏输入可按 [ADR-028](../06_Decisions/ADR-028-MvpRelativeDragInput.md) 的 Inspector 系数缩放。

最大可见士兵数不属于 Luban。GameplaySceneEntry 按 `ArmyId = 1` 选择序列化 Army Prefab，ArmyController 的 `SlotCapacity = slots.Length` 是显示容量的唯一来源。当前武器和三种元素剩余时间都是本局运行时状态，不写入 `TbArmy`。

### `TbWeapon`

初始字段：`Id`、`FireInterval`、`BulletId`。

`Id` 是唯一的武器身份，MVP 固定 `0 = Slingshot`、`1 = Bow`、`2 = Staff`，三行都必须存在且不得把 `0` 当作缺失值或无武器哨兵。每个激活槽位按对应行的 `FireInterval` 独立发射一枚 `BulletId` 对应的子弹；`FireInterval` 必须有限且大于 `0`。多弹道、散射和代表人数缩放不属于 MVP。

当前不建立 `TbElement`。火、冰、雷使用固定 `ElementType`，元素门按 LevelConfig 中的关卡系数和 HP 归零后的额外伤害计算持续时间，Army 保存本局剩余时间；具体元素效果进入范围后再决定是否新增元素配置表。

### `TbEnemy`

初始字段：`Id`、`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange`、`AttackCooldown`。

首版 `EnemyType` 为 `Normal`、`Elite`、`Boss`。攻击类型由敌人类型固定派生：普通敌人为 `SingleTarget`，精英和 Boss 为 `Area`，不重复配置 `AttackType`。`AttackStartRange` 使用怪物与锁定槽位目标位置的 XY 欧氏距离，距离为 `0` 的重合状态同样允许开始攻击，不使用 `TargetSensor`。道路接近线由 `LevelConfig` 的关卡空间配置提供。首版不配置 `ContactDamage`，敌人到达道路偏下接近线后向军队接近，不继续向底部移动。EnemyManager 通过 Inspector 分别绑定 `NormalMonster`、`EliteMonster`、`BossMonster` 三个具体根类型的规范 Prefab，按 `EnemyType` 选择类型池；Prefab 与池身份不写入 Luban。

### `TbProp`

初始字段：`Id`、`WeaponId`、`MaxHp`、`ContactDamage`、`MoveSpeed`。

当前 MVP 的道具均为武器箱，其装备效果和表现由 `WeaponId` 确定，不重复配置 `PropType`。弹弓箱、弓箭箱和法杖箱共用一个 `WeaponProp` 根类型、规范 Prefab 和类型池，ObstacleManager 通过 Inspector 绑定 Prefab，初始化时按 `WeaponId` 绑定表现；不同武器必须使用不同的 `WeaponId`。该字段集只描述当前 MVP；Prop 的领域职责允许未来配置其他击破效果，但在 DES-031 定案前不预留通用效果字段，见 ADR-022。

每条 `TbProp` 必须具有有效 `WeaponId`、`MaxHp > 0`、`ContactDamage > 0` 和 `MoveSpeed >= 0`。

### `TbBullet`

初始字段：`Id`、`Damage`、`MoveSpeed`。

MVP 子弹固定为命中首个有效目标后回收，不配置 `CollisionBehavior`。全部 MVP 子弹共用一个 `Bullet` 根类型、规范 Prefab 和类型池；数值与表现按 `BulletId` 初始化，Prefab 与池身份不写入 Luban。

数值单位、上下限和默认值必须在实际表建立时补齐，不在模块代码中另写一份。

## Unity 关卡资产

### `LevelCatalog`

`LevelCatalog` 是初始化阶段读取的关卡目录，至少包含：

```text
entries[].levelConfig       LevelConfig 资产引用
entries[].initiallyUnlocked bool
```

目录不重复保存 `levelId`；每个条目的 ID 由其 `LevelConfig.levelId` 提供。当前目录只包含第一关，且第一关默认解锁。目录资产和关卡资产均为只读配置。当前版本只使用 `initiallyUnlocked`；存档属于后续扩展，未来引入 `SaveService` 时另行定案存档状态与默认解锁的合并规则，且不得回写这些资产。

### `LevelConfig`

`LevelConfig` 是首版唯一实际关卡配置资产，负责：

- 关卡 ID 和展示信息。
- 通关后应解锁的关卡 ID 列表 `unlockedLevelIds`；首版不执行下一关跳转。它不是当前玩家已解锁状态。
- 固定道路唯一的世界坐标 `roadBounds: Rect`；宽度、高度和上下左右边界均从该 Rect 派生，不重复序列化。
- `spawnY`、`enemyApproachY`、`despawnY` 等关卡空间参数。
- 本关元素门统一使用的 `elementDurationSecondsPerDamage`。
- `enemySpawns`、`gateSpawns`、`propSpawns` 三个按本局开始时间编排的列表。

`roadBounds` 必须是有限且宽高大于 0 的 Rect，并包含世界原点。ArmyRoot 每局从 `(0,0,0)` 开始且只沿世界 X 轴移动。MVP 的 Y 顺序固定为 `roadBounds.yMin <= despawnY < 0 < enemyApproachY < spawnY <= roadBounds.yMax`。道路只提供数值边界与视觉，不设置玩法 Collider。

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

Additive 条目的 `elementType = None`、`maxHp = 0`；Element 条目的 `initialValue = 0`。未使用字段不得被运行时读取。本关存在元素门时，`elementDurationSecondsPerDamage` 必须有限且大于 `0`；不存在元素门时使用中性值 `0`。条件字段不符合规则时以 `InvalidLevelConfig` 阻止该关卡进入 Gameplay，并报告 LevelConfig 资产和生成项索引。

生成列表按 `spawnTime` 非递减排序；同一时间按列表顺序处理。每条敌人、Gate、Prop 生成项都必须提供有限且位于 `[0,1]` 的 `spawnPosition`，越界、NaN 或无穷值以 `InvalidLevelConfig` 拒绝。SpawnManager 使用 LevelManager 从 `roadBounds` 构建的 RoadLayoutSnapshot，按左右边界线性计算 `x`，使用固定 `spawnY` 作为 `y`；坐标以对象中心点为准，不考虑 Collider、Renderer 或 Prefab 尺寸。该值不约束对象生成后的移动路径。MVP 的 `enemySpawns` 至少包含一个条目；`gateSpawns` 和 `propSpawns` 可以为空。

LevelConfig 不保存敌人 HP、子弹伤害等表驱动的复用数值。Gate 是例外：每条门生成项直接保存初始数字、元素类型和元素门 MaxHp；加法门统一速度来自 AdditiveGate Prefab，元素门统一速度和接触伤害来自 ElementGate Prefab。当前门数字、当前 HP 和 HP 归零后的额外伤害仍属于运行时状态。

## 引用关系

```text
LevelCatalog.entries[].levelConfig.levelId -> LevelId
LevelConfig.enemySpawns[].configId -> TbEnemy.Id
LevelConfig.propSpawns[].configId  -> TbProp.Id
TbWeapon.bulletId          -> TbBullet.Id
TbProp.weaponId            -> TbWeapon.Id
```

参与玩法碰撞的 Prefab 必须按 `CollisionRules.md` 配置对应的 `Collider2D`、职责 Layer 和稳定职责名称。池化规范 Prefab 由对应 Manager 的 Inspector 引用提供，一个具体池化根类型只绑定一个 Prefab；Prefab、碰撞形状、阵型槽位、发射点与对象生命周期属于 Unity 资源或运行时状态，不写入 Luban 数值表，MVP 不通过 Luban `PrefabKey` 间接引用这些对象。

所有跨表引用和条件字段必须在初始化或选定关卡加载期验证。找不到目录条目、重复 `levelId`、缺少 `TbArmy.Id = 1`、缺少任一固定 `TbWeapon` 行、空 `enemySpawns`、无效目标 ID 或无效条件字段时，配置加载失败并报告具体来源。必需的 Unity 资源绑定在进入 Gameplay 前单独验证；序列化 Army Prefab 绑定必须包含唯一的 `ArmyId = 1`，其槽位数组必须至少包含一个有效槽位。

## 运行时状态边界

以下字段不得写入配置表作为运行时回写值：当前人数、当前槽位人数、当前槽位 HP、槽位补充标记、当前 WeaponId、火/冰/雷剩余持续时间、当前门数字、当前门/道具 HP、元素门 `PostDepletionDamage`、奖励锁定状态、接触判定状态、运行时实例 ID、生成游标、关卡运行时间和胜负状态。它们属于 ArmyController、Monster、Gate、Prop、ObstacleManager、LevelManager 和 GameStateService 的运行时状态；分数和存档状态当前不属于 MVP 状态模型。

## 变更规则

- 新增跨模块字段先更新本文件，再更新对应模块文档和表结构。
- 修改主键或引用语义必须新增 ADR 或更新现有 ADR。
- 生成代码只读；代码问题回溯到表、`luban.conf` 或模板。
- 当前 MVP 字段边界见 `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`；延期能力进入范围前不得预留空字段。
- Army、Prefab、武器和三元素字段对 ADR-020 的修订见 `../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`。
- Gate 内联配置、伤害驱动数字和元素门额外伤害换算见 `../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`。
- 固定出生横线与 `spawnPosition` 的坐标解析和校验见 `../06_Decisions/ADR-023-NormalizedSpawnPosition.md`。
