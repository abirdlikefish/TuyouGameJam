# Prop 道具模块

## 模块信息

- ID：`MOD-PROP`
- 层级：Gameplay
- 状态：`InProgress`（批次 4 脚本已实现并通过编译；Prefab/Layer 与击破流程手测待完成）
- 依赖：EventBus、Army、Bullet、ObstacleManager、IPropConfigProvider、Level
- 决策：`../../06_Decisions/ADR-022-PropBreakEffectBoundary.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`、`../../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`、`../../06_Decisions/ADR-055-KinematicBulletTargetAdapters.md`、`../../06_Decisions/ADR-067-ConfigurablePropTypesAndGooseCageReward.md`、`../../06_Decisions/ADR-073-WeaponPickupPriority.md`

## 玩法定位

Prop 是可被击破的道路对象，不等同于武器箱。WeaponProp 在 `Pending` 状态被击破时应用一次武器替换；BasketballProp 被击破时没有收益，只发布事实并回收；GooseCageProp 被击破时按配置同步增员。通用多效果组合规则仍处于设计待决状态。

## 当前 MVP 道具

- 弹弓箱：在接触前击破后请求 Army 使用 `WeaponId 0`，当前已是弓或法杖时不降级。
- 弓箭箱：在接触前击破后请求 Army 使用 `WeaponId 1`，当前已是法杖时不降级。
- 法杖箱：在接触前击破后请求 Army 使用 `WeaponId 2`，并按已有元素解析最终法杖。

这三种武器箱的配置直接引用一个固定 `WeaponId`：`0 = Slingshot`、`1 = Bow`、`2 = Staff`，拾取优先级为 `Staff > Bow > Slingshot`。低优先级请求不改变 Army，但武器箱仍完成成功击破、事实发布和回收。篮球与鹅笼通过 `PropType` 使用同一张 `TbProp`，但不把 `WeaponId` 作为自身身份；鹅笼另读取正数 `ArmyAddition`。

## 职责

- 控制道具从道路上方生成并向下移动；移动流程使用 LevelManager 传给 ObstacleManager 的 `Gate` 时间域 delta，一次更新不再读取或叠加 `Gameplay` delta。
- 使用 Prefab 上的 `BodyCollider` 参与子弹命中和 Army 接触；BodyCollider 节点必须绑定同节点 `BulletHitProxy` 并显式引用对应的 `WeaponProp` 或 `BasketballProp` 根组件。本帧移动与 Physics2D 同步完成后只对终点姿态执行一次 `OverlapCollider`，不做接触 Cast、扫掠或子步进，也不依赖自动碰撞回调。
- Prop 根节点提供 Kinematic Rigidbody2D 查询适配，使无刚体 Bullet 的 Collider Cast 能命中 Prop；该刚体不驱动移动、推挤或接触结算。
- 保存运行时 HP、接触状态、运行时实例 ID 和配置 ID。
- 使用根节点 Animator 驱动纯表现循环动画；`BasketballProp` 与 `GooseCageProp` 分别固定播放各自 Loop，`WeaponProp` 按 `WeaponId 0/1/2` 选择对应 Loop。动画不包含玩法 AnimationEvent，也不决定生命、接触、奖励或回收。
- 类型池每次借出时先在未激活状态准备动画身份，激活当帧从第 0 帧播放；归还时清除待播状态并重绑 Animator，复用对象不得残留上一实例的 WeaponId、状态或 Sprite。
- 接收子弹伤害，并在 `Pending` 状态下 HP 首次清空时触发一次配置的击破效果。
- 与 Army 接触时只判定一次。
- 未击破接触时，对每个接触到的 Army 槽位造成相同伤害，然后继续向下移动离场。
- 未击破接触时通过 `IArmyController.ApplySlotDamage` 同步结算已去重槽位，随后发布 `PropContactDamage` 事实事件。
- WeaponProp 击破成功时通过 `IArmyController.ApplyWeaponPickup` 同步应用武器切换；BasketballProp 不提交 Army 命令；GooseCageProp 通过 `IArmyController.AddArmy` 同步应用配置增员。三者随后发布各自事实事件并向 `ObstacleManager` 报告回收。
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
- 当前三种武器箱只有配置和表现资源差异，共用一个 `WeaponProp` 池化根脚本和一个规范 Prefab；装备效果及运行时表现绑定由 `WeaponId` 决定。篮球使用独立 `BasketballProp`、规范 Prefab 和类型池，生命、接触伤害和移动速度由唯一 Prefab 提供，不进入 `TbProp`。
- 篮球使用唯一循环 Clip；三种武器箱在唯一 `WeaponProp` Prefab 上通过整数 `WeaponId` 参数和显式状态哈希选择三个循环 Clip，不建立第二套运行时资源注册，也不按 WeaponId 拆分 Prefab。
- 当前动画只覆盖存活期间的循环显示。击破后仍沿用现有立即回收语义；如需非循环击破动画，必须另行设计 Breaking 状态与回收时机。
- ADR-064 只接受固定“无收益”篮球；其他击破效果进入范围前，仍须在 `DES-031` 确认效果类型、单个或组合规则、目标、叠加和配置结构。
- 关卡出现顺序和每条生成项的 `[0,1]` 横向出生位置由 `LevelConfig` 提供；SpawnManager 解析固定 `spawnY` 上的中心点世界坐标。当前 HP、接触状态和位置不回写 Luban。
- WeaponProp、BasketballProp 和 GooseCageProp 共用 `BreakablePropBase` 的 `ObstacleValueTextView`：只显示当前 HP 数字；HP 实际减少时执行一次短促相对缩放，动效期间再次减少只刷新数字、不重启动效。HP 到 `0` 后沿用击破立即回收，因此整个道具与文本一同消失。

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
- BasketballProp 每次借出都从篮球 Loop 第 0 帧播放；WeaponProp 的 `WeaponId 0/1/2` 分别从对应 Loop 第 0 帧播放。两类对象回池复借后不残留旧参数、状态、帧或 Sprite。
- Prop Animator、Controller 或 WeaponId 参数缺失、类型错误时阻止 Gameplay Ready；Prop 循环 Clip 不包含玩法 AnimationEvent。
- 三类 Prop 的 `ObstacleValueTextView` 与同节点 TMP_Text 都是必需绑定；初始化只显示当前 HP，不播放缩放。
- HP 实际减少时文本短暂放大并恢复；动效期间的后续伤害继续刷新数字但不重启计时，动效结束后的下一次伤害可以重新触发。
- 击破回池及跨类型复用后，文本缩放、可见性、内容和动效播放状态不得残留。
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
- BodyCollider 与 `BulletHitProxy` 必须位于同一节点并显式绑定对应的 WeaponProp/BasketballProp；缺失或绑定错误时阻止 Gameplay Ready。
- WeaponProp 与 BasketballProp 根 Kinematic Rigidbody2D 必须符合 ADR-055 且 BodyCollider 保持 Trigger；缺失或配置错误时阻止 Gameplay Ready。
