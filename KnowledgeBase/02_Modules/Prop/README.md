# Prop 道具模块

## 模块信息

- ID：`MOD-PROP`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：TimeService、EventBus、Army、Bullet、ObstacleManager、ConfigService

## 当前道具

- 弹弓箱：在接触前击破后将 Army 的 `WeaponId` 更新为弹弓配置。
- 弓箭箱：在接触前击破后将 Army 的 `WeaponId` 更新为弓箭配置。
- 法杖箱：在接触前击破后将 Army 的 `WeaponId` 更新为法杖配置。

每个道具配置直接引用一个 `WeaponId`。不同参数的武器必须使用不同的武器配置 ID。

## 职责

- 控制道具从道路上方生成并向下移动。
- 使用 Prefab 上的 `BodyCollider` 参与子弹命中和 Army 接触；移动后通过显式 Cast/Overlap 查询，不依赖自动碰撞回调。
- 保存运行时 HP、接触状态、运行时实例 ID 和配置 ID。
- 接收子弹伤害并在 HP 清空时触发击破效果。
- 与 Army 接触时只判定一次。
- 未击破接触时，对每个接触到的 Army 槽位造成相同伤害，然后继续向下移动离场。
- 未击破接触时通过 `IArmyController.ApplySlotDamage` 同步结算已去重槽位，随后发布 `PropContactDamage` 事实事件。
- 击破成功时通过 `IArmyController.ApplyWeaponPickup` 同步应用武器切换，随后发布 `PropBroken` 并向 `ObstacleManager` 报告回收。

## 接触规则

```text
Pending
  -> Succeeded：已击破，更新 WeaponId
  -> Failed：未击破，对每个接触槽造成一次相同伤害，继续下移
  -> ExitedUncontacted：未接触直接离场
```

同一道具与同一 Army 只进行一次接触判定，多个槽位分别结算接触伤害。失败后不再次进行成功判定。

`Failed` 状态下即使后续子弹将 HP 打空，也只能进入销毁或离场流程，不转换为 `Succeeded`。

接触失败后，后续子弹即使将道具 HP 打空，也不再发放武器奖励。失败状态锁定接触奖励；后续命中只用于受击表现和生命周期处理。

## 配置输入

- 从 Luban `TbProp` 读取 `WeaponId`、`MaxHp`、`ContactDamage` 和 `MoveSpeed`。
- 道具玩法身份由 `WeaponId` 决定，不重复配置 `PropType`；三种武器箱 Prefab 由 Unity 侧按 `WeaponId` 绑定。
- 关卡出现顺序和生成点由 `LevelConfig` 提供；当前 HP、接触状态和位置不回写 Luban。

## 非职责

- 不直接修改 Army 槽位内部状态。
- 不依赖 ArmyController 订阅 `PropBroken` 或 `PropContactDamage` 来执行效果；这些事件只用于事后观察。
- 不负责 Spawn 时序、活动列表和对象池管理。
- 不决定敌人生成完成和关卡胜负条件。

## 测试标准

- 三种道具分别引用正确的 `WeaponId`。
- 子弹命中只结算一次伤害，HP 清空只触发一次击破/销毁流程；只有接触前击破才更新武器。
- 未击破接触时每个接触槽位受到相同伤害。
- 同一道具不会因多帧碰撞重复伤害同一 Army。
- 未接触道具离场不触发武器切换或接触伤害。
- 道具接触失败后被后续子弹击破时不更新 `WeaponId`。
- Army 状态变更完成后才发布对应事实事件；增删其他事件监听者不会改变结算结果。
- Prop 的 BodyCollider 使用 Prop Layer；同一查询返回多个子 Collider 时按运行时实例 ID 去重。
