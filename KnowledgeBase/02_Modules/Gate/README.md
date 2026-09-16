# Gate 门模块

## 模块信息

- ID：`MOD-GATE`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：TimeService、EventBus、Army、Bullet、ObstacleManager、ConfigService
- 决策：`../../06_Decisions/ADR-006-AdditiveGateAndContactResolution.md`

## 职责

- 控制加法门和元素门向下移动。
- 使用 Prefab 上的 `BodyCollider` 参与子弹命中和 Army 接触；移动后通过显式 Cast/Overlap 查询，不依赖自动碰撞回调。
- 保存门的运行时数字、HP、接触状态和运行时实例 ID。
- 接收子弹命中并更新门数字或 HP。
- 在 Army 首次接触时执行一次判定。
- 通过 `IArmyController` 同步应用门效果或槽位伤害，完成状态变更后再发布接触结果事实事件。
- 成功、失败和下方离场后向 `ObstacleManager` 报告生命周期变化。

## 门类型

### 加法门

- `GateValue` 是可为负数的当前数字。
- 子弹命中后增加配置的 `HitIncrement`。
- Army 接触时应用：

```text
CandidateCount = Max(0, ArmyCount + GateValue)
NewArmyCount = ArmyCountLimit > 0
    ? Min(CandidateCount, ArmyCountLimit)
    : CandidateCount
```

- `GateValue >= 0` 标记为成功。
- `GateValue < 0` 标记为失败，但仍执行同样的加法效果；不进入元素门的接触伤害路径，并沿用普通加法门的接触后流程回收。

### 元素门

- 拥有独立的运行时 HP 和 `ElementId`。
- HP 清空后接触 Army，成功应用元素。
- HP 未清空时接触失败，对每一个接触到的 Army 槽位造成相同的接触伤害。
- 失败后继续向下移动并离场，不再次进行成功判定。
- 接触失败后，后续子弹即使将 HP 打空，也不再发放元素奖励；后续命中只用于受击或销毁表现。

## 接触状态

```text
Pending
  -> Succeeded
  -> Failed
  -> ExitedUncontacted
```

实际运行时只有一条状态转换路径。`Succeeded` 和 `Failed` 都表示接触判定已经消费；同一门不能因为多个槽位或多帧碰撞重复结算。未接触直接离场使用 `ExitedUncontacted`，不产生成功或失败接触事件。

门的接触去重键使用 `ArmyId + RuntimeInstanceId`，不使用配置表中的 `GateId`。

## 配置输入

- 从 Luban `TbGate` 读取 `GateType`、`InitialValue`、`HitIncrement`、`MaxHp`、`ElementId`、`ContactDamage` 和 `MoveSpeed`。
- 加法门与元素门 Prefab 由 Unity 侧按 `GateType` 绑定，不读取 Luban 资源键。
- 关卡出现顺序和生成点由 `LevelConfig` 提供。
- 当前数字、HP、接触状态和位置属于运行时状态，不回写 Luban。

## 非职责

- 不直接修改 Army 槽位内部人数或 HP。
- 不依赖 ArmyController 订阅 `GateContactResolved` 来执行门效果；该事件只用于事后观察。
- 不负责 Spawn 时序和活动对象列表。
- 不决定敌人生成完成和关卡胜负条件。

## 测试标准

- 子弹命中一次只更新一次数字或 HP。
- 加法门的正数、零和负数都按配置规则计算；负数接触标记失败但仍应用人数变化并回收。
- 同一门对同一 Army 只进行一次接触判定。
- Army 状态变更完成后才发布一次 `GateContactResolved`；增删其他事件监听者不会改变结算结果。
- 元素门在接触前 HP 清空并接触 Army 时只成功应用一次元素。
- 元素门接触失败后 HP 被打空时不更新 Army 的 `ElementId`。
- 元素门未清空时，对每个接触槽位造成相同伤害，之后继续移动离场。
- 元素门接触失败后被后续子弹击破时不更新 Army 的 `ElementId`。
- 未接触的门离场时不产生接触成功或失败事件。
- Gate 的 BodyCollider 使用 Gate Layer；同一查询返回多个子 Collider 时按运行时实例 ID 去重。
