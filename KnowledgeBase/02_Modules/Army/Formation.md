# Army Formation 阵型与槽位

## 目标

用固定数量的表现槽位承载可能很大的逻辑人数。槽位是 Army 聚合体的一部分，不是独立的 Gameplay 模块。

## 槽位状态

每个槽位具有稳定的 `SlotIndex` 和局部位置，并绑定：

- `RepresentedCount`：当前槽位代表的士兵数量。
- `CurrentHp`、`MaxHp`：槽位聚合生命值。
- 独立 `SlotCollider`（Collider2D）受击碰撞体。
- 一个子弹生成点。
- `NeedsRefill`：是否存在受击后未补齐的人数缺口。

`RepresentedCount == 0` 时保留槽位位置和索引，但禁用士兵表现、碰撞体和发射点，等待后续新增人数补充。

## 人数分配

初始创建时，或没有受击缺口时需要创建新的上场槽位，使用整数平均分配：

```text
active = Min(ArmyCount, MaxDeployedSoldiers)
if active == 0: 所有槽位保持 0 人并结束分配。
base = ArmyCount / active
remainder = ArmyCount % active
Slot[i].RepresentedCount = base + (i < remainder ? 1 : 0)
```

人数增加时按以下顺序逐人分配：

1. 先选择 `RepresentedCount` 最少的受击缺口/空槽位。
2. 人数相同时选择 `SlotIndex` 较小的槽位。
3. 没有受击缺口且总人数小于最大上场人数时，按槽位顺序启用未使用槽位。
4. 所有槽位都已启用后，继续补充当前人数最少的槽位。

受击不会触发其他槽位向少人数槽位的转移。任何分配后都必须满足：

```text
Sum(Slot[i].RepresentedCount) == ArmyCount
```

## 聚合生命值与受击

配置提供 `HpPerSoldier`。槽位最大生命值为：

```text
MaxHp = RepresentedCount × HpPerSoldier
```

新增人数为槽位增加等量健康生命值。槽位受击时先减少 `CurrentHp`，然后按聚合生命值换算人数：

```text
RepresentedCount = CurrentHp <= 0 ? 0 : Ceil(CurrentHp / HpPerSoldier)
```

人数减少后重新计算 `MaxHp`，但不把其他槽位的人数迁移过来。槽位人数变为 0 时标记 `NeedsRefill`。受击导致的代表人数减少量同步从 `ArmyCount` 扣除，并发布人数和阵型变化事件。

## 碰撞与发射

- 每个激活槽位的碰撞体独立实现 `IDamageable` 适配，并把 `SlotIndex` 回传 Army。
- ArmyRoot 不作为受伤目标，避免根碰撞体和子碰撞体重复扣人数。
- 每个激活槽位从自己的发射点生成子弹。
- Gate 与多个槽位重叠时由 Army 按 `ArmyId + RuntimeInstanceId` 去重。

## 动态边界

ArmyRoot 作为唯一移动对象。移动边界使用当前激活槽位碰撞体的合并 AABB 计算；槽位启用、禁用或阵型配置变化后重新计算边界。
