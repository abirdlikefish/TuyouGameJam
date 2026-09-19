# ADR-035：Army 配置、Prefab、运行时装备与负数门减员

## 状态

Accepted

> ADR-046 将所有表 ID 统一为从 `0` 开始，因而把本文的固定 `ArmyId` 与 `TbArmy.Id` 从 `1` 修订为 `0`；Prefab、槽位、装备、元素与减员规则不变。

> ADR-038 已覆盖本文中“元素门直接配置固定 `ElementDuration`”及 `TbGate` 元素字段的部分。Army 的三元素独立计时、`AddElementDuration` 正值输入、同类型累加和当前不设玩法上限仍然有效。

## 日期

2026-09-19

## 背景

现有契约把 `TbArmy` 同时用作军队数值、最大显示槽位、初始武器和单一元素来源，但 Army 的阵型实际由 Unity Prefab 决定，武器与元素又会在关卡内变化。单一 `ElementId` 也无法表达火、冰、雷三种元素同时生效。负数加法门此前直接计算新总人数，没有定义聚合槽位应如何承担减员。

## 决策

### Army 配置与身份

- MVP 固定 `ArmyId = 0`，同时表示读取首行 `TbArmy.Id = 0` 和选择序列化绑定中 `ArmyId = 0` 的 Army Prefab。
- `TbArmy` 只保存 `Id`、`ArmyCountLimit`、`HpPerSoldier`、`MoveSpeed`。
- `ArmyCountLimit` 是逻辑总人数上限，不是显示槽位上限；`0` 表示不设上限，大于 `0` 时必须至少为初始人数 `1`。
- `MaxDeployedSoldiers`、`WeaponId`、`ElementId` 从 `TbArmy` 删除。
- ConfigService 在初始化时把 Luban 行复制为不可变 `ArmyConfigSnapshot` 和 `WeaponConfigSnapshot`，并通过 `IArmyConfigProvider`、`IWeaponConfigProvider` 提供类型化只读查询。Gameplay 模块不得直接访问静态 Luban Tables 或保存生成表的可变行对象。
- 初始化时缺少 `TbArmy.Id = 0`、武器固定行或 `TbWeapon.BulletId` 引用时配置加载失败，不使用默认行兜底。

### Army Prefab 与槽位容量

- `GameplaySceneEntry` 使用可序列化的 `ArmyPrefabBinding` 列表维护 `ArmyId -> ArmyController Prefab`；Unity 默认不序列化普通 Dictionary，因此运行时只在验证后构建只读查找表。
- MVP 必须存在且只存在一个 `ArmyId = 0` 的有效绑定。Prefab 为空、ID 重复、根节点缺少 `ArmyController` 或槽位绑定无效时，Gameplay Preparing 失败并且不得发布 Ready。
- GameplayScene 中使用固定 `ArmyContainer`；Entry 在其下实例化选中的 Army Prefab。Army 不进入 PoolService，每局场景只创建一个实例并在清理时销毁。
- ArmyController 通过序列化 `ArmySlotView[] slots` 持有固定槽位引用，不使用运行时 `Find`、`GetComponentsInChildren` 或 `AddComponent` 作为缺失引用兜底。
- `SlotCapacity = slots.Length`，它是最大可见士兵数的唯一来源。`GetSlotCapacity()` 返回该派生值；逻辑 `ArmyCount` 可以大于槽位容量。
- 数组下标就是稳定 `SlotIndex`。每个槽位必须绑定士兵表现根、`SlotCollider` 和 `FirePoint`；数组为空、存在空项或重复引用时绑定无效。

### 武器运行时状态

- `WeaponId` 是本局运行时状态，不属于 `TbArmy`。固定 ID 为：`0 = Slingshot`、`1 = Bow`、`2 = Staff`。
- `TbWeapon` 必须准确提供上述三个 ID；`0` 是有效 ID，不能作为“无武器”哨兵，也不增加第二份 `WeaponType` 身份。
- 每次 `StartRun` 将当前武器重置为 `WeaponId = 0`。武器箱通过 `ApplyWeaponPickup` 修改当前武器；切换只影响之后生成的子弹。

### 三元素持续时间

- 当前删除 `TbElement`。`ElementType` 固定为 `None = 0`、`Fire = 1`、`Ice = 2`、`Lightning = 3`；`None` 只用于非元素门的未使用配置字段，不是可获得元素。
- ArmyController 分别保存三种元素的剩余持续时间；每次 `StartRun` 全部重置为 `0`。
- 元素门配置 `ElementType` 和 `ElementDuration`。成功接触时调用 `AddElementDuration`，把有限且大于 `0` 的持续时间加到对应元素；同类型重复获得时累加，当前不设上限。
- Army 在 `TickMovementAndFire` 中先使用传入的 Gameplay delta 将三个计时器截断扣减到不小于 `0`，再生成本帧子弹。因此 Gate 接触阶段新获得的元素从下一逻辑帧发射开始生效。
- 元素效果本身延后设计。当前只记录元素是否在发射瞬间有效，不修改子弹伤害、速度、数量或碰撞行为。
- 发射时从三个计时器派生不可变 `ElementMask`：`None = 0`、`Fire = 1`、`Ice = 2`、`Lightning = 4`。`BulletSpawnRequest`、子弹运行时快照和 `BulletDamageContext` 保存该掩码，不保存 Army 的可变计时器或单一 `ElementId`。
- 子弹生成后不随 Army 元素获得或过期而变化。

### 正负加法门与减员

- `GateValue >= 0` 时 Gate 调用 `AddArmy(GateValue)`；Army 应用 `ArmyCountLimit` 并返回包含请求增员、实际增员和剩余总人数的 `ArmyAdditionResult`。
- `GateValue < 0` 时 Gate 只调用 `RemoveArmy(Abs(GateValue))`，参数是正的请求减员人数。Gate 不选择槽位、不直接修改 `ArmyCount`，也不计算槽位 HP。
- Army 将请求减员换算为伤害预算：`RequestedDamage = RequestedRemoval * HpPerSoldier`。乘法与剩余伤害预算统一使用 `long`；单个槽位 HP 仍为 `int`。
- 仅 `RepresentedCount > 0 && CurrentHp > 0` 的槽位参与，按 `CurrentHp` 升序、再按 `SlotIndex` 升序确定承担顺序。
- 当前槽位最多承受其剩余 HP；伤害不足时扣减后停止，伤害足以清空时将该槽位置零并把剩余伤害传给下一个最低 HP 槽位，直到预算耗尽或 Army 全灭。
- 每次槽位受伤后使用整数向上取整重新计算 `RepresentedCount`；`ArmyCount` 始终等于所有槽位代表人数之和。
- 请求减员人数不保证等于实际人数损失。例如两个各剩 `5 HP` 的单兵槽位在 `HpPerSoldier = 10` 时承受一次 `RemoveArmy(1)` 会全部死亡。事件与返回结果必须区分请求减员、实际伤害和实际人数损失。
- `ArmyCountLimit` 只限制增员，不参与负数门、Monster 攻击或其他伤害结算。

### 事实事件

- 旧 `ArmyLoadoutChanged` 拆分为 `ArmyWeaponChanged`、`ArmyElementDurationChanged` 和 `ArmyElementExpired`。
- 元素剩余时间不通过 EventBus 每帧广播；增加持续时间和从有效变为过期时发布事实，UI 如需连续显示则读取只读 Army 元素快照。
- 负数门对每个实际受伤槽位发布对应受击事实，全部伤害结算完成后只发布一次汇总人数变化和一次阵型变化。事实中的人数变化使用实际损失值。

## 覆盖的旧规则

- 覆盖 ADR-004 中把 `MaxDeployedSoldiers` 作为独立配置，以及负数人数直接改写总人数但未定义槽位伤害顺序的部分。
- 覆盖 ADR-006 中元素门发放单一 `ElementId`，以及正负门统一使用 `ArmyCount + GateValue` 直接得到人数的部分。
- 补充 ADR-007：WeaponId 仍是唯一武器身份，但 MVP 固定为 `0/1/2`，当前武器从 `TbArmy` 移到本局运行时状态。
- 覆盖 ADR-020 中 `TbArmy.MaxDeployedSoldiers`、`TbArmy.WeaponId`、`TbArmy.ElementId`、`TbElement` 和 `TbGate.ElementId` 的字段基线。

## 验收标准

- `ArmyId = 0` 能同时取得 `TbArmy.Id = 0` 的不可变快照和唯一的序列化 Army Prefab；任一缺失都阻止 Gameplay Ready。
- 两个不同 Army Prefab 可以通过不同槽位数组长度得到不同 `SlotCapacity`，无需修改 Luban。
- 每局初始武器为弹弓，三种元素持续时间均为 `0`。
- 火、冰、雷可以任意组合同时有效；重复获得同元素累加持续时间。
- 子弹保留发射瞬间的 `ElementMask`，Army 后续元素变化不修改飞行中的子弹。
- 负数门按最低当前 HP、再按槽位索引稳定分配伤害；请求减员、实际伤害和实际人数损失可分别验证。

## 关联文档

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Formation.md`
- `../02_Modules/Army/Loadout.md`
- `../02_Modules/Gate/README.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/EventCatalog.md`
- `ADR-004-ArmyFormationAndDamage.md`
- `ADR-006-AdditiveGateAndContactResolution.md`
- `ADR-007-WeaponIdentity.md`
- `ADR-020-MinimalMvpConfigurationSurface.md`
