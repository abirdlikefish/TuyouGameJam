# ADR-020：MVP 最小配置面与 Unity 资源绑定

## 状态

Accepted

## 日期

2026-09-14

## 背景

当前 Luban 工程只有 `demo.item` 示例表，玩法业务表尚未建立。现有配置草案提前列入了 Prefab 键、阵型键、人数缩放、多弹道、元素状态和可配置碰撞行为等字段，但这些能力没有进入当前 MVP 的必要规则。此时建立字段会同时增加 Excel 维护、生成代码、加载校验和 Unity 资源注册工作，并使固定规则看起来可以由策划调整。

MVP 只需要完成启动、游玩、胜负和重新开始闭环，因此配置面只保留会被至少两个配置实例复用或需要调平的稳定 ID 与玩法数值。

## 决策

- Luban 只保存可复用玩法数值、规则分类和跨表稳定 ID；运行时状态以及 Unity 对象引用不进入 Luban。
- MVP 的 Luban 表不配置 `PrefabKey`、`SoldierPrefabKey` 或 `FormationKey`。Prefab、Sprite、Animator、AudioClip、阵型槽位和发射点由 Unity Inspector 或 Unity 侧资源绑定管理。
- Unity 侧资源绑定可以按 `EnemyType`、`GateType`、`WeaponId`、`BulletId` 等已有语义标识选择资源，但资源键属于 Unity 资源侧，不反向成为 Luban 字段。
- Army 初始人数固定为 `1`，不建立 `InitialCount` 字段。`ArmyCountLimit` 保留；`0` 表示不设上限，大于 `0` 时才作为上限。
- `AttackType` 由 `EnemyType` 派生：`Normal -> SingleTarget`，`Elite/Boss -> Area`，不在 `TbEnemy` 重复配置。
- 当前 MVP 的三种 Prop 都是武器箱，其装备效果、Prefab 和表现由 `WeaponId` 决定，因此不建立 `PropType`。该最小字段面不把 Prop 的长期职责限制为武器切换；其他击破效果进入范围前按 ADR-022 和 DES-031 另行定案。
- 子弹固定为命中首个有效目标后回收，MVP 不建立 `CollisionBehavior`。
- 代表人数不缩放射速、伤害或弹丸数量；每个激活槽位按同一武器配置独立发射一枚子弹。
- 元素在 MVP 中只提供稳定身份和类型，用于装备状态与表现；伤害倍率、状态类型和持续时间延后。
- 当前没有独立的全局可调参数，MVP 不建立 `TbGameSettings`。出现真实参数后再决定归入专表还是全局表。

MVP 初始字段如下：

| 表 | 字段 |
|---|---|
| `TbArmy` | `Id`、`ArmyCountLimit`、`MaxDeployedSoldiers`、`HpPerSoldier`、`MoveSpeed`、`WeaponId`、`ElementId` |
| `TbWeapon` | `Id`、`FireInterval`、`BulletId` |
| `TbElement` | `Id`、`ElementType` |
| `TbEnemy` | `Id`、`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange`、`AttackCooldown` |
| `TbGate` | `Id`、`GateType`、`InitialValue`、`HitIncrement`、`MaxHp`、`ElementId`、`ContactDamage`、`MoveSpeed` |
| `TbProp` | `Id`、`WeaponId`、`MaxHp`、`ContactDamage`、`MoveSpeed` |
| `TbBullet` | `Id`、`Damage`、`MoveSpeed` |

## 不采用

- 不为未来可能的动态资源选择预留空的 `PrefabKey` 或通用字符串字段。
- 不把当前固定为单一行为的规则包装成可配置枚举。
- 不同时保存可由另一字段无歧义派生的身份字段。
- 不在业务表建立前保留没有当前消费者的平衡参数。

## 影响

- 本 ADR 收窄 ADR-003 中“Luban 保存字符串资源键”的范围：MVP 不保存 Unity 资源键；资源绑定仍属于 Unity 侧。
- 本 ADR 收窄 ADR-005 中 `AttackType` 由表配置的决定：攻击类型仍是运行时概念，但由 `EnemyType` 固定派生。
- Luban 业务表创建时以本 ADR 的初始字段为基线；新增延期字段前必须先出现明确玩法需求和消费者。
- 配置校验负责跨表 ID、数值范围和条件字段；Unity 场景进入 Gameplay 前另行验证必需 Prefab、碰撞体、阵型槽位和发射点绑定。

## 延后项

- 动态资源换肤或同一玩法配置选择多个 Prefab。
- 多弹道、散射以及按代表人数缩放射速、伤害或弹丸数量。
- 元素伤害倍率、状态类型和持续时间。
- 可穿透、弹跳、范围爆炸等子弹碰撞行为。
- 全局平衡参数表。

这些能力进入范围时，应新增或更新 ADR，并只增加实现该能力所需的字段。

## 关联文档

- `ADR-003-ConfigurationSources.md`
- `ADR-005-MonsterCombatAndManager.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../04_Assets/ArtList.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
