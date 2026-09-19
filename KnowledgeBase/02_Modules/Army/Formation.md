# Army Formation 阵型与槽位

## 目标

用固定数量的表现槽位承载可能很大的逻辑人数。槽位是 Army 聚合体的一部分，不是独立的 Gameplay 模块。

## 槽位状态

ArmyController 通过 Inspector 序列化 `ArmySlotView[] slots`。数组下标就是稳定 `SlotIndex`，`SlotCapacity = slots.Length`，不从 `TbArmy` 读取第二份最大槽位数。每个槽位具有固定局部位置，并绑定：

- `RepresentedCount`：当前槽位代表的士兵数量。
- `CurrentHp`、`MaxHp`：整数槽位聚合生命值。
- 独立 `SlotCollider`（Collider2D）受击碰撞体。
- 与 SlotCollider 同节点、显式绑定当前 ArmySlotView 的 `ArmySlotHitProxy`。
- 一个子弹生成点。

`RepresentedCount == 0` 时保留槽位位置和索引，但禁用士兵表现、碰撞体和发射点，等待后续新增人数补充。

## 人数分配

初始创建时使用整数平均分配：

```text
active = Min(ArmyCount, SlotCapacity)
if active == 0: 所有槽位保持 0 人并结束分配。
base = ArmyCount / active
remainder = ArmyCount % active
Slot[i].RepresentedCount = base + (i < remainder ? 1 : 0)
```

运行时人数增加时逐人分配。每次都从全部槽位选择 `RepresentedCount` 最少者，相同时选择 `SlotIndex` 较小者；空槽位人数为 `0`，因此会自然优先重新启用。Army 不保存 `NeedsRefill`，也不需要推断受击前的目标人数。

受击不会触发其他槽位向少人数槽位的转移。任何分配后都必须满足：

```text
Sum(Slot[i].RepresentedCount) == ArmyCount
```

## 聚合生命值与受击

`TbArmy.Id = 0` 的不可变配置快照提供整数 `HpPerSoldier`。槽位最大生命值为：

```text
MaxHp = RepresentedCount × HpPerSoldier
```

新增人数为槽位增加等量健康生命值。槽位受击时先减少 `CurrentHp`，然后按聚合生命值换算人数：

```text
RepresentedCount = CurrentHp <= 0 ? 0 : Ceil(CurrentHp / HpPerSoldier)
```

人数减少后重新计算 `MaxHp`，但不把其他槽位的人数迁移过来。槽位人数变为 0 时禁用表现、Collider 和发射资格，后续增员按最少人数规则自然重新启用。受击导致的代表人数减少量同步从 `ArmyCount` 扣除，并发布人数和阵型变化事件。

## 负数门请求减员

负数加法门不直接指定目标槽位，也不直接设置 ArmyCount。Gate 只调用：

```text
RemoveArmy(Abs(GateValue))
```

Army 内部执行：

```text
damageRemaining = requestedRemoval × HpPerSoldier
candidates = 所有 RepresentedCount > 0 且 CurrentHp > 0 的槽位
candidates 按 CurrentHp 升序，再按 SlotIndex 升序排序

for slot in candidates:
    if damageRemaining <= 0:
        break

    oldCount = slot.RepresentedCount
    applied = Min(damageRemaining, slot.CurrentHp)
    slot.CurrentHp -= applied
    damageRemaining -= applied

    slot.RepresentedCount = slot.CurrentHp <= 0
        ? 0
        : (slot.CurrentHp + HpPerSoldier - 1) / HpPerSoldier

    if slot.CurrentHp == 0:
        清空并禁用该槽位的表现、碰撞体和发射

    actualArmyCountLoss += oldCount - slot.RepresentedCount
```

伤害预算和剩余伤害统一使用 `long`；单个槽位 HP 与单次槽位扣减仍使用 `int`。ArmyCount 在结算后等于所有槽位代表人数之和。如果 Army 已全部死亡，未使用的伤害直接丢弃。

请求减员人数与实际损失人数必须分开。例如 `HpPerSoldier = 10`，两个单兵槽位都只剩 `5 HP` 时，`RemoveArmy(1)` 产生 `10` 点伤害并会依次清空两个槽位，实际损失为 `2`。`ArmyRemovalResult` 分别报告请求减员、请求/实际伤害、实际人数损失和剩余总人数。

## 碰撞与发射

- 每个激活槽位的碰撞体独立实现 `IDamageable` 适配，并把 `SlotIndex` 回传 Army。
- ArmyRoot 不作为受伤目标，避免根碰撞体和子碰撞体重复扣人数。
- 每个激活槽位从自己的发射点生成子弹。
- Gate 与多个槽位重叠时由 Army 按 `ArmyId + RuntimeInstanceId` 去重。

## 动态边界

ArmyRoot 作为唯一移动对象。移动边界使用当前激活槽位碰撞体的合并 AABB 计算；槽位启用、禁用或 Prefab 阵型变化后重新计算边界。

## Prefab 绑定校验

- `slots.Length > 0`，不存在空项或重复引用。
- 每个 ArmySlotView 必须绑定 SoldierVisual、SlotCollider 和 FirePoint。
- 每个 SlotCollider 节点必须绑定 `ArmySlotHitProxy`，代理显式引用对应 ArmySlotView；初始化时注入 ArmyId 和数组下标 SlotIndex。
- SlotCollider 使用 ArmySlot Layer；空槽位只禁用表现、Collider 和发射资格，SlotIndex 与局部位置保持不变。
- Prefab 在 ArmyRoot 位于世界原点时的最大激活槽位合并宽度必须能放入道路宽度，否则 Gameplay Preparing 失败。
