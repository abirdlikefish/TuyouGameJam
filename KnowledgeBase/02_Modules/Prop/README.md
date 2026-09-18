# Prop 道具模块

## 模块信息

- ID：`MOD-PROP`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：TimeService、EventBus、Army、Bullet、ObstacleManager、ConfigService
- 决策：`../../06_Decisions/ADR-022-PropBreakEffectBoundary.md`

## 玩法定位

Prop 是“可被击破并触发效果的道路对象”，不等同于武器箱。道具在 `Pending` 状态被击破时触发一次配置的击破效果；效果应用完成后才发布击破事实并回收。当前 MVP 只确认了替换武器效果，未来其他效果的目录和组合规则仍处于设计待决状态。

## 当前 MVP 道具

- 弹弓箱：在接触前击破后将 Army 的 `WeaponId` 更新为弹弓配置。
- 弓箭箱：在接触前击破后将 Army 的 `WeaponId` 更新为弓箭配置。
- 法杖箱：在接触前击破后将 Army 的 `WeaponId` 更新为法杖配置。

这三种道具的配置直接引用一个 `WeaponId`。不同参数的武器必须使用不同的武器配置 ID。该字段是当前 MVP 武器箱的具体配置，不代表未来所有 Prop 都必须以武器作为身份。

## 职责

- 控制道具从道路上方生成并向下移动。
- 使用 Prefab 上的 `BodyCollider` 参与子弹命中和 Army 接触；移动后通过显式 Cast/Overlap 查询，不依赖自动碰撞回调。
- 保存运行时 HP、接触状态、运行时实例 ID 和配置 ID。
- 接收子弹伤害，并在 `Pending` 状态下 HP 首次清空时触发一次配置的击破效果。
- 与 Army 接触时只判定一次。
- 未击破接触时，对每个接触到的 Army 槽位造成相同伤害，然后继续向下移动离场。
- 未击破接触时通过 `IArmyController.ApplySlotDamage` 同步结算已去重槽位，随后发布 `PropContactDamage` 事实事件。
- 当前 MVP 击破成功时通过 `IArmyController.ApplyWeaponPickup` 同步应用武器切换，随后发布 `PropBroken` 并向 `ObstacleManager` 报告回收。
- 后续新增的击破效果必须通过明确的同步命令应用，不能依赖 `PropBroken` 监听者修改玩法状态，也不能由 Prop 直接写入 Army 私有字段。

## 击破与接触规则

```text
Pending
  -> Succeeded：接触前已击破，触发配置的击破效果
  -> Failed：未击破，对每个接触槽造成一次相同伤害，继续下移
  -> ExitedUncontacted：未接触直接离场
```

同一道具与同一 Army 只进行一次接触判定，多个槽位分别结算接触伤害。失败后不再次进行成功判定。

`Failed` 状态下即使后续子弹将 HP 打空，也只能进入销毁或离场流程，不转换为 `Succeeded`。

接触失败后，后续子弹即使将道具 HP 打空，也不再发放任何击破效果。失败状态锁定当前武器奖励以及未来新增的其他效果；后续命中只用于受击表现和生命周期处理。

## 配置输入

- 当前 MVP 从 Luban `TbProp` 读取 `WeaponId`、`MaxHp`、`ContactDamage` 和 `MoveSpeed`。
- 当前三种武器箱的装备效果和资源绑定由 `WeaponId` 决定，不重复配置 `PropType`；这只是 MVP 配置面，不是 Prop 领域模型的长期上限。
- 其他击破效果进入范围前，必须先在 `DES-031` 确认效果类型、单个或组合规则、目标、叠加和配置结构；在此之前不预留通用参数字段。
- 关卡出现顺序和每条生成项的 `[0,1]` 横向出生位置由 `LevelConfig` 提供；SpawnManager 解析固定 `spawnY` 上的中心点世界坐标。当前 HP、接触状态和位置不回写 Luban。

## 非职责

- 不直接修改 Army 槽位内部状态。
- 不依赖 ArmyController 订阅 `PropBroken` 或 `PropContactDamage` 来执行效果；这些事件只用于事后观察。
- 不负责 Spawn 时序、活动列表和对象池管理。
- 不决定敌人生成完成和关卡胜负条件。

## 测试标准

- 当前三种武器箱分别引用正确的 `WeaponId`。
- 子弹命中只结算一次伤害，HP 清空只触发一次击破/销毁流程；只有接触前击破才触发配置的击破效果。
- 未击破接触时每个接触槽位受到相同伤害。
- 同一道具不会因多帧碰撞重复伤害同一 Army。
- 未击破且未接触的道具离场不触发击破效果或接触伤害。
- 道具接触失败后被后续子弹击破时不触发任何击破效果；当前 MVP 可通过 `WeaponId` 不变化验证。
- Army 状态变更完成后才发布对应事实事件；增删其他事件监听者不会改变结算结果。
- Prop 的 BodyCollider 使用 Prop Layer；同一查询返回多个子 Collider 时按运行时实例 ID 去重。
