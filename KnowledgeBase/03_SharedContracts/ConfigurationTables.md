# 配置表契约

本文件登记 Luban 表、`LevelCatalog` 和 `LevelConfig` 之间的跨模块契约。字段的详细类型、范围和引用关系在表结构稳定后补充；模块文档只声明消费关系，不复制这里的定义。

## 配置目录

| 配置 | 形式 | 主责模块 | 当前状态 |
|---|---|---|---|
| `LevelCatalog` | Unity ScriptableObject | Config、LevelSelect | Planned |
| `LevelConfig` | Unity ScriptableObject | Level | Planned |
| `TbArmy` | Luban 表 | Army | Planned |
| `TbWeapon` | Luban 表 | Army、Bullet | Planned |
| `TbElement` | Luban 表 | Army、Bullet | Planned |
| `TbEnemy` | Luban 表 | Monster | Planned |
| `TbGate` | Luban 表 | Gate | Planned |
| `TbProp` | Luban 表 | Prop | Planned |
| `TbBullet` | Luban 表 | Bullet | Planned |

`TbGameSettings` 不属于当前 MVP；在出现真实的全局可调参数前不建立该表。

## Luban 表初始字段

### `TbArmy`

初始字段：`Id`、`ArmyCountLimit`、`MaxDeployedSoldiers`、`HpPerSoldier`、`MoveSpeed`、`WeaponId`、`ElementId`。

MVP 初始人数固定为 `1`，不配置 `InitialCount`。`ArmyCountLimit = 0` 表示不设总人数上限；大于 `0` 时才限制逻辑总人数。`TbArmy.MoveSpeed` 是 Army 横向移动速度，Input 不提供速度。阵型槽位、士兵 Prefab 和槽位发射点由 Unity Inspector 绑定，不配置 `FormationKey` 或 `SoldierPrefabKey`。

### `TbWeapon`

初始字段：`Id`、`FireInterval`、`BulletId`。

`Id` 是唯一的武器身份。不同参数的武器必须使用不同的 `Id` 和配置行；不在运行时维护第二份武器类型身份。每个激活槽位按 `FireInterval` 独立发射一枚 `BulletId` 对应的子弹。多弹道、散射和代表人数缩放不属于 MVP。

### `TbElement`

初始字段：`Id`、`ElementType`。

初始 `ElementType` 为 `Fire`、`Ice`、`Lightning`，用于装备状态和表现分类。伤害倍率、状态类型和持续时间不属于 MVP。

### `TbEnemy`

初始字段：`Id`、`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange`、`AttackCooldown`。

首版 `EnemyType` 为 `Normal`、`Elite`、`Boss`。攻击类型由敌人类型固定派生：普通敌人为 `SingleTarget`，精英和 Boss 为 `Area`，不重复配置 `AttackType`。道路接近线由 `LevelConfig` 的关卡空间配置提供。首版不配置 `ContactDamage`，敌人到达道路偏下接近线后向军队接近，不继续向底部移动。敌人 Prefab 由 Unity 侧按 `EnemyType` 绑定。

### `TbGate`

初始字段：`Id`、`GateType`、`InitialValue`、`HitIncrement`、`MaxHp`、`ElementId`、`ContactDamage`、`MoveSpeed`。

`GateType` 为 `Additive` 或 `Element`。加法门使用 `InitialValue` 和 `HitIncrement`；元素门使用 `MaxHp`、`ElementId` 和 `ContactDamage`。当前数字和 HP 是运行时状态。

条件字段按以下规则校验：

| `GateType` | 必须使用 | 必须为中性值 `0` |
|---|---|---|
| `Additive` | `InitialValue`、`HitIncrement > 0`、`MoveSpeed >= 0` | `MaxHp`、`ElementId`、`ContactDamage` |
| `Element` | `MaxHp > 0`、有效 `ElementId`、`ContactDamage > 0`、`MoveSpeed >= 0` | `InitialValue`、`HitIncrement` |

未使用字段不得被运行时读取。条件字段不符合规则时以 `InvalidLevelConfig` 阻止该关卡进入 Gameplay，并报告具体表和配置 ID。

### `TbProp`

初始字段：`Id`、`WeaponId`、`MaxHp`、`ContactDamage`、`MoveSpeed`。

当前 MVP 的道具均为武器箱，其装备效果、Prefab 与表现由 `WeaponId` 确定，不重复配置 `PropType`。弹弓箱、弓箭箱和法杖箱由 Unity 侧按 `WeaponId` 绑定，不同武器必须使用不同的 `WeaponId`。该字段集只描述当前 MVP；Prop 的领域职责允许未来配置其他击破效果，但在 DES-031 定案前不预留通用效果字段，见 ADR-022。

每条 `TbProp` 必须具有有效 `WeaponId`、`MaxHp > 0`、`ContactDamage > 0` 和 `MoveSpeed >= 0`。

### `TbBullet`

初始字段：`Id`、`Damage`、`MoveSpeed`。

MVP 子弹固定为命中首个有效目标后回收，不配置 `CollisionBehavior`。子弹 Prefab 由 Unity Inspector 或 Unity 侧按 `BulletId` 绑定。

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
- 固定道路的宽度、高度、坐标、左右边界和出生横线 `spawnY`，所有位置使用世界 XY 坐标且运行时 `z = 0`。
- `spawnY`、`enemyApproachY`、`despawnY` 等关卡空间参数。
- `enemySpawns`、`gateSpawns`、`propSpawns` 三个按本局开始时间编排的列表。

每条生成项至少包含：

```text
spawnTime  相对本局开始的秒数，非负
configId   对应 TbEnemy / TbGate / TbProp 的稳定 ID
spawnPosition 道路从左到右的归一化出生位置，范围为 [0,1]
```

生成列表按 `spawnTime` 非递减排序；同一时间按列表顺序处理。每条敌人、Gate、Prop 生成项都必须提供有限且位于 `[0,1]` 的 `spawnPosition`，越界、NaN 或无穷值以 `InvalidLevelConfig` 拒绝。SpawnManager 按道路左右边界线性计算 `x`，使用固定 `spawnY` 作为 `y`；坐标以对象中心点为准，不考虑 Collider、Renderer 或 Prefab 尺寸。该值不约束对象生成后的移动路径。MVP 的 `enemySpawns` 至少包含一个条目；`gateSpawns` 和 `propSpawns` 可以为空。

它不负责保存敌人 HP、门数字、子弹伤害等可复用数值。

## 引用关系

```text
LevelCatalog.entries[].levelConfig.levelId -> LevelId
LevelConfig.enemySpawns[].configId -> TbEnemy.Id
LevelConfig.gateSpawns[].configId  -> TbGate.Id
LevelConfig.propSpawns[].configId  -> TbProp.Id
TbArmy.weaponId            -> TbWeapon.Id
TbArmy.elementId           -> TbElement.Id
TbWeapon.bulletId          -> TbBullet.Id
TbProp.weaponId            -> TbWeapon.Id
```

参与玩法碰撞的 Prefab 必须按 `CollisionRules.md` 配置对应的 `Collider2D`、职责 Layer 和稳定职责名称。Prefab、碰撞形状、阵型槽位、发射点与对象生命周期属于 Unity 资源或运行时状态，不写入 Luban 数值表；MVP 不通过 Luban `PrefabKey` 间接引用这些对象。

所有跨表引用和条件字段必须在初始化或选定关卡加载期验证。找不到目录条目、重复 `levelId`、空 `enemySpawns`、无效目标 ID 或无效条件字段时，配置加载失败并报告具体来源。必需的 Unity 资源绑定在进入 Gameplay 前单独验证；`TbArmy.MaxDeployedSoldiers` 不得超过 Inspector 阵型提供的槽位数。

## 运行时状态边界

以下字段不得写入配置表作为运行时回写值：当前人数、当前槽位人数、当前槽位 HP、槽位补充标记、当前门数字、当前门/道具 HP、接触判定状态、运行时实例 ID、生成游标、关卡运行时间和胜负状态。它们属于 ArmyController、Monster、Gate、Prop、ObstacleManager、LevelManager 和 GameStateService 的运行时状态；分数和存档状态当前不属于 MVP 状态模型。

## 变更规则

- 新增跨模块字段先更新本文件，再更新对应模块文档和表结构。
- 修改主键或引用语义必须新增 ADR 或更新现有 ADR。
- 生成代码只读；代码问题回溯到表、`luban.conf` 或模板。
- 当前 MVP 字段边界见 `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`；延期能力进入范围前不得预留空字段。
- 固定出生横线与 `spawnPosition` 的坐标解析和校验见 `../06_Decisions/ADR-023-NormalizedSpawnPosition.md`。
