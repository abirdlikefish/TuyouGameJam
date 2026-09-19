# ADR-004：军队总人数、上场槽位与比例受击

## 状态

Accepted（槽位容量来源与负数门减员顺序由 ADR-035 修订）

> ADR-035 已删除 `TbArmy.MaxDeployedSoldiers`，槽位容量改由 Army Prefab 的序列化槽位数组长度派生；负数门不再直接设置总人数，而是由 Army 按最低当前 HP 顺序分配等价伤害。ADR-046 进一步统一运行时增员为每次选择 `RepresentedCount` 最少、再按 `SlotIndex` 最小的槽位，不保存 `NeedsRefill`。本 ADR 的聚合生命值、槽位受击不主动迁移和人数守恒结论继续有效。

## 日期

2026-09-13

## 背景

军队的逻辑总人数可能大于画面可显示的士兵数量。每个上场槽位需要独立碰撞体、血量和子弹生成点；槽位受击后不能把其他槽位的人数主动挪过来，但获得新人数时需要优先补充人数较少的槽位。

## 决策

- `ArmyCount` 表示军队逻辑总人数，初始值为 1。
- `ArmyCountLimit`（如果关卡需要总人数上限）与 Prefab 派生的 `SlotCapacity` 分离；前者限制逻辑人数，后者限制可见槽位数量。
- 初始创建或没有受击缺口时，`ActiveSlotCount = Min(ArmyCount, SlotCapacity)`；受击后启用槽位数量以 `RepresentedCount > 0` 为准，空槽位仍保留。
- 初始分配采用整数平均分配，余数按槽位索引从小到大依次加 1。
- 每个槽位保存自己的 `RepresentedCount`、`CurrentHp`、`MaxHp`、碰撞体和子弹生成点。槽位人数不因其他槽位受击而重新平均。
- 槽位最大生命值按 `MaxHp = RepresentedCount × HpPerSoldier` 计算。新增人数带来等量健康生命值。
- 槽位受击先扣除聚合生命值；受击后的代表人数为 `Ceil(CurrentHp / HpPerSoldier)`，生命值为 0 时槽位人数为 0。槽位人数因受击减少时，不向其他槽位转移人数。
- 槽位受击导致的代表人数减少量同步从 `ArmyCount` 扣除；始终保持 `ArmyCount == Sum(Slot.RepresentedCount)`。
- 人数增加时逐人处理；每次从全部槽位选择 `RepresentedCount` 最少者，相同时选择 `SlotIndex` 最小者。空槽位因此自然优先重新启用，超过槽位数量后继续保持人数最少优先；不保存或推断额外的受击缺口状态。
- 槽位为 0 人时保留其槽位索引和局部位置，禁用表现、碰撞体和发射；后续获得人数时优先重新启用该槽位。
- ArmyRoot 负责整体移动；槽位碰撞体是唯一的士兵受击目标。ArmyRoot 不作为可受伤目标。
- Gate 与多个槽位重叠时，Army 以 `ArmyId + RuntimeInstanceId` 去重，保证一个门效果只应用一次。

## 影响

- Army 需要维护总人数与槽位快照，而不是只维护一个人数整数。
- Bullet 发射数量受激活槽位数量限制；每个激活槽位拥有一个独立发射点。
- Gate、Monster 和 UI 必须区分 Army 总人数、槽位人数和槽位生命值。
- 需要新增槽位分配、比例受击、空槽位补充、碰撞去重和动态边界测试。

## 未覆盖

- `HpPerSoldier`、基础射速和人数对单发伤害/射速的具体数值由配置表和平衡测试确定。
- 武器和元素的特殊组合若出现独有规则，再通过组合覆盖配置扩展。

## 关联文档

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Formation.md`
- `../03_SharedContracts/DataDictionary.md`
- `../03_SharedContracts/ConfigurationTables.md`
