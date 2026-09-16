# ADR-006：加法门、元素门与一次接触结算

## 状态

Accepted

## 日期

2026-09-14

## 背景

现有 Gate 文档把门定义为乘法门，但当前玩法使用的是把门上数字加到军队总人数上的加法门；同时需要增加元素门。门和道具都从道路上方生成并向下移动，接触 Army 后只进行一次接触判定。未接触的对象允许从道路下方离场。

## 决策

### 加法门

- 门类型为 `Additive`，运行时数字为 `GateValue`。
- 子弹命中门后按配置的 `HitIncrement` 增加数字；每次物理命中只结算一次。
- Army 接触时应用：

```text
CandidateCount = Max(0, ArmyCount + GateValue)
NewArmyCount = ArmyCountLimit > 0
    ? Min(CandidateCount, ArmyCountLimit)
    : CandidateCount
```

- `GateValue >= 0` 时接触判定成功。
- `GateValue < 0` 时接触判定失败，但仍应用相同的加法效果；失败状态只表示接触结果，不表示忽略人数变化，也不进入元素门的接触伤害路径。
- 同一对象与同一 Army 的接触只判定一次；多个槽位同时接触时由 Army 按运行时门实例 ID 去重。
- 负数加法门沿用普通加法门的接触后流程；当前规则为接触效果完成后回收。

### 元素门

- 门类型为 `Element`，拥有独立的运行时 HP 和 `ElementId`。
- 接触时 `Hp <= 0` 为成功，Army 获得该门的 `ElementId`。
- 接触时 `Hp > 0` 为失败；对每一个接触到的 Army 槽位应用相同的接触伤害。
- 失败后继续向下移动并离场，不再次进行成功判定。
- 一旦元素门接触失败，后续子弹即使将其 HP 打空，也不再发放元素奖励；后续命中只允许产生受击或销毁表现。
- 未接触直接离场不算成功，也不算失败接触。

### 接触状态

门必须区分以下状态事实：

```text
Pending -> Succeeded
Pending -> Failed
Pending -> ExitedUncontacted
```

`Succeeded` 或 `Failed` 只描述接触判定结果；加法门接触后回收，元素门失败后继续向下移动，但任何失败对象都不得再次触发接触效果。

### 命令与事实事件顺序

- Gate 和 Prop 对 Army 产生的状态变更使用 `IArmyController` 的同步命令接口，不把事实事件当作修改 Army 的命令。
- 加法门和成功元素门分别调用 `ApplyGateEffect`；元素门失败时对已去重的接触槽位调用 `ApplySlotDamage`。
- 道具成功时调用 `ApplyWeaponPickup`；道具失败时对已去重的接触槽位调用 `ApplySlotDamage`。
- 对象完成本地状态转换和 Army 命令调用后，才发布 `GateContactResolved`、`PropBroken` 或 `PropContactDamage`。这些事件只供 UI、音频、VFX 和调试等观察者消费，`ArmyController` 不订阅它们重复执行效果。

### 范围

本 ADR 不决定敌人生成完成和关卡终局条件。Gate/Prop 是否在终局前离场不影响敌人胜利判定。

## 影响

- `TbGate` 必须区分门类型、数字、命中增量、元素、HP 和接触伤害。
- Gate 事件必须携带运行时实例 ID、接触成功标记和实际效果。
- Gate 模块不能再使用“倍增门”或乘法门作为唯一模型。
- `TbGate` 按 `GateType` 校验条件字段：`Additive` 只消费数字字段，`Element` 只消费 HP、元素和接触伤害字段；未使用字段必须保持中性值，不能参与运行时规则。

## 道具失败后的击破

- 道具接触失败后，后续子弹即使将其 HP 打空，也不再发放 `WeaponId` 奖励。
- 失败状态锁定接触奖励；对象仍可按生命周期规则受击、销毁或离场。
