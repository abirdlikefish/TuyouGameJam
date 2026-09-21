# ADR-065：敌人元素死亡动画选择

## 状态

- 状态：Accepted / InProgress
- 日期：2026-09-21
- 影响范围：Monster、Animation、Prefab、测试

## 背景

敌人已经保存火、冰、雷三种“最近受到元素伤害”的剩余时间，但死亡时只触发单一 `Death` 动画。现在需要为 Chick、Hen、Rooster、Ikun 各准备普通、火、冰、雷四种死亡表现，并保证致命元素伤害与历史元素记录之间的选择确定且可复现。

## 决策

1. `MonsterBase` 在 HP 归零的同一次伤害结算中确定死亡类型，并在触发 `Death` 前写入 Animator 整数参数 `DeathVariant`：`0=Normal`、`1=Fire`、`2=Ice`、`3=Lightning`。
2. 若致命伤害携带至少一种火、冰、雷元素，只在这次致命伤害携带的元素中选择；多元素同时命中时固定按火、冰、雷优先。该规则保证致命元素伤害永远是最新记录，不会被旧计时覆盖。
3. 若致命伤害不带元素，则比较死亡前火、冰、雷三个正数剩余时间，选择数值最大的元素；完全相同时按火、冰、雷优先。三个计时均已归零时选择普通死亡。
4. 死亡类型在对象池初始化、借出播放 Move 和回池时重置为普通，避免复用上一个实例的 Animator 参数。
5. `AC_Monster_Base` 使用原 `Death` Trigger 配合 `DeathVariant` 从 Any State 分流到四个无出口死亡状态。四种敌人分别使用自己的 OverrideController，并完整覆盖 Move、Attack 与四个死亡基础 Clip。
6. 普通死亡继续使用既有 `Death` 目录和 Clip，保持资源路径与 GUID；元素死亡帧放在 `Death/Fire`、`Death/Ice`、`Death/Lightning`。四种正式死亡 Clip 均为非循环，并恰好包含一个位于结束时间的 `OnDeathAnimationFinished()`。
7. 本次只创建可导入的目录、空 Clip、Controller/AOC/Prefab 绑定，不生成或假定正式动画帧。

## 结果

- 元素死亡选择只影响表现与延迟回收，不改变死亡事实发布、存活数、胜利判定或元素组合伤害。
- `Sequence Animation Builder` 登记四类敌人的四种死亡序列；重建死亡 Clip 时自动把唯一回收事件对齐到 Clip 结束时间。
- Ikun 不再复用 Boss AOC，改用自己的 AOC，后续可以独立导入 Move、Attack 与四种死亡素材。

## 验收

- 无元素历史时播放普通死亡；仅有一个未过期元素时播放对应死亡。
- 非元素致命伤害在多个历史元素间选择最大剩余时间；相同时按火、冰、雷。
- 元素致命伤害忽略更早的历史元素；同一次致命伤害携带多元素时按火、冰、雷。
- 四类敌人 AOC 的四种死亡替换均非空，所有正式死亡 Clip 非循环且只有一个末尾回收事件。
- 对象池复借后 `DeathVariant` 为普通，不能继承上一实例的死亡类型。
