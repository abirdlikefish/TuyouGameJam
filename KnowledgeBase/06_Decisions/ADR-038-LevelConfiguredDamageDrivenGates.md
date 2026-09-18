# ADR-038：关卡内联门配置与伤害驱动结算

## 状态

Accepted

## 日期

2026-09-19

## 背景

此前 Gate 通过 `LevelConfig.gateSpawns[].configId` 引用 Luban `TbGate`。加法门每次有效子弹命中只增加固定 `HitIncrement`；元素门在 HP 清空后发放配置的固定 `ElementDuration`。当前项目尚未建立业务 Luban 表，而门的类型、初始数字、元素类型和元素门 HP 实际都属于每一条关卡生成编排。继续使用 `TbGate` 会为只差初始数字或 HP 的门创建大量间接配置，并要求尚未定案的 Gate 配置 Provider。

项目同时确认子弹对加法门、元素门和敌人统一使用发射快照中的最终 `BulletDamageContext.Damage`。元素门 HP 清空后仍允许继续承受并消耗子弹；只有 HP 归零后的额外伤害用于换算元素持续时间。

## 决策

### 移除 TbGate

- 当前 MVP 不建立 `TbGate`，Gate 不使用 Luban 配置 ID，也不新增 `IGateConfigProvider`。
- `LevelConfig.gateSpawns` 的每条 `GateSpawnEntry` 直接保存 `spawnTime`、`spawnPosition`、`GateType`、加法门 `InitialValue`、元素门 `ElementType` 和元素门 `MaxHp`。
- `LevelConfig` 顶层保存本关统一的 `elementDurationSecondsPerDamage`。本关存在元素门时，该值必须有限且大于 `0`；不存在元素门时使用中性值 `0`。
- `AdditiveGate` 规范 Prefab 的根组件通过 Inspector 保存所有加法门共用的 `MoveSpeed`。
- `ElementGate` 规范 Prefab 的根组件通过 Inspector 保存所有元素门共用的 `MoveSpeed` 和 `ContactDamage`。
- 当前数字、当前 HP、HP 归零后的额外伤害、接触状态、位置和运行时实例 ID 都是运行时状态，不回写 `LevelConfig` 或 Prefab。

`GateSpawnEntry` 使用条件字段：

| `GateType` | 必须使用 | 必须为中性值 |
|---|---|---|
| `Additive` | `InitialValue` | `ElementType = None`、`MaxHp = 0` |
| `Element` | `ElementType = Fire/Ice/Lightning`、`MaxHp > 0` | `InitialValue = 0` |

### 加法门按伤害增加数字

- 项目仍然只有加法门，不恢复乘法门。
- 每次有效子弹命中只结算一次，并使用 `BulletDamageContext.Damage` 作为本次数字增量：

```text
GateValue += Damage
```

- `Damage` 必须为正数；同一 `BulletInstanceId` 不得重复增加数字。
- `HitIncrement` 从当前配置与规则中删除。`GateValueChanged` 的变化量等于本次有效子弹伤害。
- Army 接触规则保持不变：`GateValue >= 0` 调用 `AddArmy(GateValue)`，`GateValue < 0` 调用 `RemoveArmy(Abs(GateValue))`。

### 元素门 HP 与额外伤害

- 每个元素门的 `MaxHp` 来自对应 `GateSpawnEntry`；初始化时 `CurrentHp = MaxHp`、`PostDepletionDamage = 0`。
- Pending 元素门收到正数伤害时按以下顺序结算：

```text
hpDamage = Min(CurrentHp, Damage)
CurrentHp -= hpDamage
extraDamage = Damage - hpDamage
PostDepletionDamage += extraDamage
```

- 使 HP 恰好归零的伤害没有额外部分；同一次致命命中超出剩余 HP 的部分立即计入 `PostDepletionDamage`。
- `CurrentHp == 0` 的 Pending 元素门仍是合法子弹目标。后续命中完整计入 `PostDepletionDamage`，子弹照常消费，不穿透该门继续命中后方目标。
- `PostDepletionDamage` 使用 `long` 保存，避免多次伤害累计发生 `int` 溢出。

Army 接触且 `CurrentHp == 0` 时标记成功，并计算：

```text
calculatedDuration = PostDepletionDamage * elementDurationSecondsPerDamage
```

- 不设置玩法持续时间上限；只要求计算结果有限且不发生数值溢出。
- `PostDepletionDamage > 0` 时调用 `AddElementDuration(ElementType, calculatedDuration, RuntimeInstanceId)`。
- `PostDepletionDamage == 0` 时仍然是成功接触，但计算持续时间为 `0`，不调用只接受正持续时间的 `AddElementDuration`，也不发布 `ArmyElementDurationChanged`。
- 同类型元素继续与 Army 已有剩余时间累加，不覆盖另外两种元素。

### 接触失败后的锁定

- 元素门接触时 `CurrentHp > 0` 仍标记失败，对每个已去重接触槽位应用 Prefab 配置的相同 `ContactDamage`，随后继续向下移动。
- 一旦进入 `Failed`，元素奖励永久锁定。后续子弹可以按表现和生命周期规则继续命中并消耗，但不得增加可兑换的 `PostDepletionDamage`，即使随后把 HP 打空也不得调用 `AddElementDuration`。
- 未接触直接离场不发放元素奖励。

### 子弹目标语义

- “有生命值”与“可以接收子弹命中”不再是同一概念。元素门在 `CurrentHp == 0` 时仍可命中，加法门则根本没有 HP。
- 子弹统一通过 `IBulletHittable.CanReceiveBulletHit` 与 `ReceiveBulletHit(BulletDamageContext)` 判断和提交有效命中；`IDamageable.IsAlive` 只保留给真正以生命值表达存活的对象。
- Enemy、Gate 和 Prop 可以各自按状态决定 `CanReceiveBulletHit`，BulletManager 不通过目标 HP 推断有效性。

### 生成请求与运行时身份

- Gate 不再拥有配置 ID。SpawnManager 从已校验的 `GateSpawnEntry` 构造 `GateSpawnRequest`，其中携带门类型、条件字段、关卡持续时间系数、生成项索引和已解析世界坐标。
- Prop 继续使用 `TbProp`，通过独立的 `PropSpawnRequest` 携带 `ConfigId`。
- `IObstacleManager` 使用 Gate/Prop 两个类型化 `Spawn` 重载，不再使用要求两类对象共享 `ConfigId` 的 `ObstacleSpawnRequest`。
- `RuntimeInstanceId` 仍是运行时对象唯一身份；`SpawnEntryIndex` 只用于本局来源诊断，不作为跨资产稳定配置 ID。

### 事实事件

- `GateSpawned` 不再携带 Gate 配置表 ID，改为携带 `SpawnEntryIndex`、门类型、对应内联初始字段和位置。
- 加法门有效命中发布 `GateValueChanged`，载荷包含 `BulletDamageContext`、旧值、新值和等于实际伤害的变化量。
- 元素门有效命中发布 `ElementGateDamageChanged`，载荷包含旧/新 HP、本次 HP 伤害、本次额外伤害、累计 `PostDepletionDamage` 和奖励锁定状态。
- 元素门接触的 `GateContactResolved` 携带 `ElementType`、累计额外伤害、换算系数、计算持续时间和可选的 `ElementDurationChangeResult`。额外伤害为 `0` 时没有 Army 持续时间变更结果，但接触结果仍为成功。

## 覆盖的旧规则

- 覆盖 ADR-003 中 Gate 数值必须来自 Luban 的部分。
- 覆盖 ADR-006 中加法门按 `HitIncrement` 增长、元素门使用固定 `ElementDuration`、`TbGate` 保存门条件字段的部分；一次接触状态机、正负门命令、失败奖励锁定和命令先于事实事件继续有效。
- 覆盖 ADR-020 的 `TbGate` 字段基线。
- 覆盖 ADR-035 中元素门直接配置固定 `ElementDuration` 的部分；Army 三元素独立计时、同类型累加和无玩法上限继续有效。
- 补充 ADR-023：Gate 生成请求除归一化出生位置外，还携带 LevelConfig 内联门数据。

## 影响

- ConfigService 校验 GateSpawnEntry 和关卡持续时间系数，但不创建 Gate 配置快照或 Gate Provider。
- ObstacleManager 通过 Inspector 校验两个 Gate 规范 Prefab 的统一速度和元素门接触伤害，并把 LevelConfig 内联数据初始化到租出的实例。
- Gate、Bullet、Level、Spawn、Obstacle、事件载荷、数据字典、配置文档和测试需要同步更新。
- 未来若同一具体 Gate 类型需要多套速度或接触伤害，不得通过为同一根类型复制第二个规范 Prefab 解决；届时应新增类型级配置资产、表或新的具体玩法类型，并另行记录决策。

## 验收标准

- Gate 运行时和生成流程不读取 `TbGate`、Gate ConfigId 或 `IGateConfigProvider`。
- 加法门对伤害分别为 `2` 和 `5` 的两次有效命中累计增加 `7`，而不是增加固定命中次数。
- 元素门 `MaxHp = 10` 依次受到 `6`、`6`、`5` 点伤害后，`CurrentHp = 0`、`PostDepletionDamage = 7`。
- HP 为零的 Pending 元素门继续消费子弹并累计完整伤害；同一子弹实例不重复累计。
- 成功接触时使用 `PostDepletionDamage × elementDurationSecondsPerDamage`；额外伤害为零时成功但不调用 `AddElementDuration`。
- 接触失败后后续伤害不增加可兑换额外伤害，HP 清空也不发放元素奖励。
- 增删 UI/VFX 监听者不改变伤害累计、持续时间计算、Army 状态或回收结果。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../02_Modules/Gate/README.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Spawn/README.md`
- `../02_Modules/Obstacle/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/EventCatalog.md`
- `../05_Testing/ObstacleTests.md`
- `ADR-006-AdditiveGateAndContactResolution.md`
- `ADR-020-MinimalMvpConfigurationSurface.md`
- `ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`
