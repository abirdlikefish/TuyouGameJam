# Prop 道具模块

## 模块信息

- ID：`MOD-PROP`
- 层级：Gameplay
- 状态：`InProgress`（批次 4 脚本已实现并通过编译；Prefab/Layer 与击破流程手测待完成）
- 依赖：EventBus、Army、Bullet、ObstacleManager、IPropConfigProvider、Level
- 决策：`../../06_Decisions/ADR-022-PropBreakEffectBoundary.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`、`../../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`

## 玩法定位

Prop 是“可被击破并触发效果的道路对象”，不等同于武器箱。道具在 `Pending` 状态被击破时触发一次配置的击破效果；效果应用完成后才发布击破事实并回收。当前 MVP 只确认了替换武器效果，未来其他效果的目录和组合规则仍处于设计待决状态。

## 当前 MVP 道具

- 弹弓箱：在接触前击破后将 Army 的 `WeaponId` 更新为 `0`。
- 弓箭箱：在接触前击破后将 Army 的 `WeaponId` 更新为 `1`。
- 法杖箱：在接触前击破后将 Army 的 `WeaponId` 更新为 `2`。

这三种道具的配置直接引用一个固定 `WeaponId`：`0 = Slingshot`、`1 = Bow`、`2 = Staff`。该字段是当前 MVP 武器箱的具体配置，不代表未来所有 Prop 都必须以武器作为身份。

## 职责

- 控制道具从道路上方生成并向下移动；移动流程使用 LevelManager 传给 ObstacleManager 的 `Gate` 时间域 delta，一次更新不再读取或叠加 `Gameplay` delta。
- 使用 Prefab 上的 `BodyCollider` 参与子弹命中和 Army 接触；BodyCollider 节点必须绑定同节点 `BulletHitProxy` 并显式引用 WeaponProp 根组件。本帧移动与 Physics2D 同步完成后只对终点姿态执行一次 `OverlapCollider`，不做接触 Cast、扫掠或子步进，也不依赖自动碰撞回调。
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

`Failed` 状态锁定当前武器奖励以及未来新增的所有击破效果。后续子弹仍正常命中和消费并产生受击表现，但 HP 按 `Max(1, CurrentHp - Damage)` 锁在至少 `1`，永远不会归零，因此不转换为 `Succeeded`、不发布 `PropBroken`、不因伤害请求回收。对象只继续向下移动至离场，或在 StopRun 中清理。

## 配置输入

- ObstacleManager 通过 `IPropConfigProvider.GetPropConfig(ConfigId)` 必得 ConfigService 已校验并复制的 `PropConfigSnapshot`，再向 WeaponProp 注入 `WeaponId`、`MaxHp`、`ContactDamage` 和 `MoveSpeed`；Prop 不持有 Luban 生成行，也不处理缺失配置恢复。
- 当前三种武器箱只有配置和表现资源差异，共用一个 `WeaponProp` 池化根脚本和一个规范 Prefab；装备效果及运行时表现绑定由 `WeaponId` 决定，不重复配置 `PropType`。未来只有出现不同生命周期或玩法逻辑时才新增具体根类型和 Prefab。
- 首轮表现允许统一占位 Sprite 或 DebugText 显示 WeaponId，不要求建立第二套运行时资源注册；正式武器箱外观绑定进入表现迭代后再补充，不影响玩法契约。
- 其他击破效果进入范围前，必须先在 `DES-031` 确认效果类型、单个或组合规则、目标、叠加和配置结构；在此之前不预留通用参数字段。
- 关卡出现顺序和每条生成项的 `[0,1]` 横向出生位置由 `LevelConfig` 提供；SpawnManager 解析固定 `spawnY` 上的中心点世界坐标。当前 HP、接触状态和位置不回写 Luban。

## 非职责

- 不直接修改 Army 槽位内部状态。
- 不依赖 ArmyController 订阅 `PropBroken` 或 `PropContactDamage` 来执行效果；这些事件只用于事后观察。
- 不负责 Spawn 时序、活动列表和对象池管理。
- 不决定敌人生成完成和关卡胜负条件。
- 不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身；只通过 ObstacleManager 注入的窄回调请求结束当前实例。
- 不使用独立 Update 推进移动或接触；由 ObstacleManager 在 LevelManager 指定阶段驱动。

## 测试标准

- 当前三种武器箱分别引用正确的 `WeaponId`。
- 三种武器箱共用一个 `WeaponProp` 规范 Prefab 和类型池，初始化后按 `WeaponId` 呈现正确资源；同一具体类型不能绑定第二个 Prefab。
- 类型池返回未激活实例；ObstacleManager 完成 Transform、配置、ID、回调和活动登记后才激活，归还前完成业务清理和主动失活。
- 子弹命中只结算一次伤害，HP 清空只触发一次击破/销毁流程；只有接触前击破才触发配置的击破效果。
- 未击破接触时每个接触槽位受到相同伤害。
- 同一道具不会因多帧碰撞重复伤害同一 Army。
- 未击破且未接触的道具离场不触发击破效果或接触伤害。
- Prop 根 GameObject 中心满足 `position.y <= RoadLayoutSnapshot.DespawnY` 时离场；不使用 Collider 或 Renderer 下边缘。
- 接触只使用移动终点的 `OverlapCollider` 结果；MVP 不验收路径中穿过但终点未重叠的接触。
- 道具接触失败后继续受到任意正伤害时 HP 最低锁在 `1`，不发布 `PropBroken`、不触发任何击破效果或伤害回收；当前 MVP 可通过 `WeaponId` 不变化并最终只从 DespawnY 离场验证。
- Army 状态变更完成后才发布对应事实事件；增删其他事件监听者不会改变结算结果。
- Prop 的 BodyCollider 使用 Prop Layer；同一查询返回多个子 Collider 时按运行时实例 ID 去重。
- BodyCollider 与 `BulletHitProxy` 必须位于同一节点并显式绑定 WeaponProp；缺失或绑定错误时阻止 Gameplay Ready。
