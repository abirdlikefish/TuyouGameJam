# Monster 怪物模块

## 模块信息

- ID：`MOD-MONSTER`
- 层级：Gameplay
- 状态：`InProgress`（批次 4 玩法脚本与批次 7.4 Animator 池复用重置已实现并通过编译；剩余正式动画、Prefab 字段、Layer 与战斗手测待完成）
- 依赖：Bullet、Army、Level、EventBus、IEnemyConfigProvider、PoolService
- 决策：`../../06_Decisions/ADR-005-MonsterCombatAndManager.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-037-MonsterDistanceTargetingAndCollisionLayers.md`、`../../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`、`../../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`、`../../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md`、`../../06_Decisions/ADR-055-KinematicBulletTargetAdapters.md`、`../../06_Decisions/ADR-056-ChickenEnemyIdentityNaming.md`、`../../06_Decisions/ADR-061-ElementComboImpactEffects.md`、`../../06_Decisions/ADR-065-ElementalMonsterDeathAnimations.md`

## 职责

- 由 `EnemyManager` 统一生成、注册、统计和回收敌人实例。
- 从道路上方生成，先向下移动至关卡接近线，再向最近的有士兵槽位移动。
- 只按怪物与锁定槽位目标位置的 XY 距离判断攻击起始范围；进入范围后停止移动，执行单体或范围普通攻击。
- 接收子弹碰撞伤害并保存命中子弹上下文；通过注入的表现事实回调报告受击，通过 EnemyManager 的必执行回调报告死亡。
- 死亡后停止移动、攻击和碰撞处理，清除待结算攻击请求，播放死亡动画并在动画结束后回收。
- 使用 `BodyCollider` 参与子弹受击和存活敌人之间的阻挡；通过显式 Cast/Overlap 查询决定敌人间移动截断和范围攻击命中。
- 三类规范 Prefab 根节点都提供固定配置的 Kinematic Rigidbody2D 查询适配，使 Bullet 命中和 Monster BodyCollider Cast 可用；刚体不驱动移动或自动推挤。

## 非职责

- 不直接修改 Army 总人数，只通过 ArmyController 的槽位伤害接口结算攻击。
- 不处理敌人与 Army 槽位之间的移动碰撞、推挤或重合分离；Army 移动造成的部分或完全重合属于允许状态。
- 不决定生成时间轴和关卡胜负条件。
- 不负责具体的 Sprite、粒子或 Animator Controller 资源选择；MVP 不使用 AudioClip。

## 敌人类型与配置

四种敌人使用四个具体池化根脚本和四个规范 Prefab，为不同逻辑保留边界；当前共有的接近、受击、死亡和目标查询规则继续提取复用，不复制相同实现：

| 类型 | 具体池化根脚本 | 规范 Prefab | 当前攻击类型 |
|---|---|---|---|
| `Chick` | `ChickMonster` | 小鸡敌人 Prefab | `SingleTarget` |
| `Hen` | `HenMonster` | 母鸡敌人 Prefab | `Area` |
| `Rooster` | `RoosterMonster` | 公鸡敌人 Prefab | `Area`；具体阶段和专属技能另行设计 |
| `Ikun` | `IkunMonster` | ikun 敌人 Prefab | `Area`；到达接近线前周期生成篮球 |

敌人从 `TbEnemy` 读取：`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange` 和 `AttackCooldown`。`AttackType` 由 `EnemyType` 固定派生：小鸡敌人为单体攻击，母鸡、公鸡和 ikun 为范围攻击。EnemyManager 通过 Inspector 分别绑定四个规范 Prefab，并按 `EnemyType` 选择对应的具体类型池；Prefab 或池类型不写入 Luban。每个规范 Prefab 的具体根脚本另外序列化有限且非负的 `blockingGap`；ikun Prefab 另外序列化正数 `basketballSpawnInterval` 和直属 `basketballSpawnPoint`。这些类型专属字段不进入 `TbEnemy` 或 `LevelConfig`。道路接近线由 `LevelConfig` 的固定空间配置提供，不写入敌人运行时状态；攻击判定时刻由 Animator 判定帧提供。

## 行为流程

```text
MovingDown
  -> ApproachingTarget
  <-> Blocked（前方存活敌人较慢或静止）
  -> Attacking
  -> MovingDown / ApproachingTarget（攻击结束后重新判断）
  -> Dead
```

- `MovingDown`：向道路接近线移动；以怪物根 GameObject 中心 `position.y` 与 `EnemyApproachY` 比较，不使用 BodyCollider 或 Renderer 边缘。本帧若会越过接近线，则截断到该线，下一次 TickMovement 才开始向 Army 槽位接近，不消费剩余位移。
- ikun 只在 `MovingDown` 状态推进篮球计时，出生后等待一个完整间隔再生成第一颗；到达接近线、死亡、StopRun 或回池后停止生成。已经生成的篮球拥有独立生命周期，不因 ikun 死亡回收。
- 归一化横向出生位置只决定初始中心点；进入 `ApproachingTarget` 后不保留出生位置约束。
- `ApproachingTarget`：从 ArmyController 获取最近的有效士兵槽位并向其当前位置移动；不使用 `TargetSensor`，当 XY 距离平方小于等于 `AttackStartRange²` 时进入攻击。
- `Blocked`：保持当前敌人规范 Prefab 的 `blockingGap` 并等待；首版不从侧面绕行。阻挡解除后恢复原移动状态。
- `Attacking`：进入攻击起始范围后停止移动。
- `Dead`：不可移动、不可攻击、不可再次受伤结算，等待死亡动画结束。

目标选择只接受 `RepresentedCount > 0` 的槽位；目标在命中关键帧前失效时，本次攻击不得造成伤害，Monster 在攻击动画结束后重新选择目标。目标位置随 Army 移动更新；敌人与槽位部分或完全重合时距离仍满足攻击起始条件，不取消或阻塞攻击。

## 攻击规则

### 单体攻击

小鸡敌人锁定一个槽位。非循环 Attack Clip 的命中关键帧调用 `OnAttackFrame()` 登记当前攻击序号；EnemyManager 在 `ResolveAttacks` 重新验证会话、敌人、攻击序号和目标槽位仍有效后，通过 ArmyController 的 `ApplySlotDamage(slotIndex, AttackPower)` 提交一次伤害。攻击开始后不因目标随后移出 `AttackStartRange` 取消本次单体命中；关键帧前任一方失效才取消。本次动画结束后的下一次 TickMovement 再重新验证或选择目标。

### 范围攻击

母鸡和公鸡的 Prefab 前方绑定 `AttackCollider`。该 Collider 可以保持启用作为显式查询形状，但自动 Layer Collision Matrix 关闭，平时不会触发玩法回调。非循环 Attack Clip 的命中关键帧调用 `OnAttackFrame()` 登记当前攻击序号；EnemyManager 只在 `ResolveAttacks` 消费有效请求时对 AttackCollider 执行一次显式重叠查询，通过 SlotCollider 同节点的 `ArmySlotHitProxy` 解析并按 SlotIndex 去重，对每个仍有效槽位分别提交一次相同的 `AttackPower`，并各发布一条 `MonsterAttackLanded`。

### 攻击与动画

攻击状态由 Monster 控制，Animator 负责移动、攻击、受击和死亡序列帧。开始攻击时 Monster 递增 `AttackSequenceId`、锁定本次目标、把 `AttackCooldown` 计时起点设为当前攻击开始时刻并播放非循环 Attack Clip；攻击动画期间冷却继续按 Monster delta 递减。Clip 的命中关键帧调用根脚本 `OnAttackFrame()`，只登记本次攻击的待结算标记；末帧调用 `OnAttackAnimationFinished()`，只结束本次 Attacking，不在 AnimationEvent 中直接开始下一次攻击。若动画结束时冷却已归零，下一次 `EnemyManager.TickMovement` 重新验证存活、目标和距离后即可立即开始新攻击，不增加额外等待，也不在同一动画回调中递归起攻；若冷却未到且目标仍在范围内则保持当前位置等待，目标失效或离开范围才重新索敌或继续接近。EnemyManager 在 LevelManager 调用 `ResolveAttacks` 时验证会话、敌人、状态、目标和攻击序号后执行伤害。若关键帧发生在当帧 ResolveAttacks 之后，则请求顺延到下一次 ResolveAttacks；动画回调不得直接伤害 Army。敌人在请求消费前死亡时不得产生攻击伤害。

### 死亡动画

HP 归零后立即进入 `Dead`、退出受击与阻挡查询，由 EnemyManager 完成一次死亡去重、`AliveEnemyCount` 递减和 `MonsterKilled` 发布。Monster 在触发 `Death` 前设置 `DeathVariant`：致命伤害带元素时只从本次元素中按火、冰、雷优先选择；致命伤害不带元素时选择正数剩余时间最大的最近元素，同值仍按火、冰、雷；全部过期时选择普通死亡。四种非循环 Death Clip 的末尾均调用一次根脚本 `OnDeathAnimationFinished()`；该本地 AnimationEvent 只向 EnemyManager 登记回收请求，真正注销和归还对象池在 `FlushPendingRecycles` 阶段完成。重复事件、过期会话事件以及 StopRun/回池后的事件均忽略；StopRun 不等待死亡动画。

## 碰撞体

- `BodyCollider`：敌人身体与子弹命中目标；不同类型的尺寸在各自 Prefab 中配置。
- `AttackCollider`：仅用于母鸡和公鸡的范围攻击；可以保持启用作为查询形状，但只在消费关键帧请求时执行一次显式重叠查询，不通过 Collider 启停决定攻击窗口。
- 存活敌人的 BodyCollider 只参与子弹受击和敌人间阻挡，不查询或阻挡 Army `SlotCollider`。
- `AttackCollider` 不参与敌人身体阻挡或子弹命中；Army `SlotCollider` 与 Enemy `BodyCollider` 允许重合，重合本身不产生伤害、推挤或事件。
- 同一子弹命中多个子碰撞体时只结算一次。

## Prefab 与序列化字段

```text
PF_Monster_Chick [ChickMonster；Animator]
├── Visual [SpriteRenderer]
└── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]

PF_Monster_Hen [HenMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
└── AttackCollider [Collider2D；EnemyAttack Layer]

PF_Monster_Rooster [RoosterMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
└── AttackCollider [Collider2D；EnemyAttack Layer]

PF_Monster_Ikun [IkunMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
├── AttackCollider [Collider2D；EnemyAttack Layer]
└── BasketballSpawnPoint [Transform]
```

四个 Prefab 根 GameObject 都同时挂载具体根脚本和 Animator，并显式序列化 `bodyCollider`、视觉引用、Animator 和 `blockingGap`；BodyCollider 节点绑定同节点 `BulletHitProxy` 并显式引用根 Monster，Hen/Rooster/Ikun 另外序列化 `attackCollider`。`blockingGap` 必须有限且大于等于 `0`。每种怪物提供循环 Move、非循环 Attack，以及普通/火/冰/雷四种非循环 Death；Prefab 使用相同 Trigger/参数语义并绑定本类型 OverrideController。池对象每次借出都清除 Attack/Death Trigger、把 `DeathVariant` 重置为普通并从 Move 第 0 帧起播，回池时重绑 Animator，不能继承上一实例的 Death 状态、参数、帧或 Sprite。Ikun 使用独立 AOC；正式帧动画后续导入。完整导入和绑定见 [AnimationPipeline](../../04_Assets/AnimationPipeline.md)、[PrefabSpecifications](../../04_Assets/PrefabSpecifications.md)、ADR-040、ADR-043、ADR-046、ADR-048、ADR-056、ADR-064 和 ADR-065。

## EnemyManager

`EnemyManager` 负责：

- 接收 Spawn 根据 `LevelConfig` 发出的生成请求。
- 通过 Inspector 绑定 `ChickMonster`、`HenMonster`、`RoosterMonster`、`IkunMonster` 四个规范 Prefab，并从 PoolService 分别取得四个具体类型池。
- 从对应类型池取得未激活敌人，设置 `MonsterRoot`、世界位置和旋转，分配运行时 ID、注入配置与回调、登记活动集合后再激活。
- 敌人请求结束时先完成死亡/离场去重、计数、事件和活动注销，再执行 `PrepareForPool`、主动失活并归还对应类型池；池的防御性失活不替代这些业务步骤。
- 维护活跃敌人列表以及 `ActiveEnemyCount`、`AliveEnemyCount`。
- 在敌人死亡时立即减少 `AliveEnemyCount`，在死亡动画完成后注销并回收。
- 接收 `OnDeathAnimationFinished()` 登记的本地回收请求，并仅在 `FlushPendingRecycles` 阶段注销、失活和归还对象池；StopRun 可直接清理而不等待动画。
- 对重复死亡、重复注册和重复回收进行幂等保护。
- 接受 Monster 的必执行死亡回调，在完成死亡去重并更新 `AliveEnemyCount` 后发布 `MonsterKilled`；事件监听者不参与必需的计数、注销或回收。
- 在 ResolveAttacks 实际向有效槽位提交伤害后作为 `MonsterAttackLanded` 的唯一发布者；范围攻击每个有效槽位各发布一条，事件中的伤害是配置 `AttackPower`，实际 HP 与人数损失由 Army 事实事件表达。
- 实现 `TickMovement`、`ResolveAttacks` 和 `FlushPendingRecycles`；只由 LevelManager 按 ADR-033 的阶段顺序调用，不使用独立 Update 推进核心玩法。

`AliveEnemyCount == 0` 只表示当前已生成敌人全部死亡；Level 还必须确认 `enemySpawns` 已全部处理，才允许判定关卡胜利。死亡动画尚未结束的敌人不再计入存活数量。

## 受击与反馈

Monster 的统一受击入口接收 `EnemyDamageContext`。它保留来源 `BulletDamageContext`，并额外区分组合种类、直接伤害、效果伤害、命中位置和方向；`Damage` 是两部分的饱和求和。Monster 通过 Manager 注入的发布回调报告 `MonsterDamaged`；生命值归零时先调用 EnemyManager 的必执行死亡回调，由 EnemyManager 完成死亡去重和 `AliveEnemyCount` 更新，再发布携带最后一击上下文的 `MonsterKilled`。

每个存活敌人分别记录 Fire、Ice、Lightning 最近一次有效受击后的剩余时间，窗口固定为 1 秒。直接伤害和范围/链式组合伤害都会刷新本次上下文携带的元素；无效、重复、已死亡目标不刷新。该记录只描述历史状态，本轮不参与组合触发。

三个规范 Monster Prefab 的根节点分别显式绑定默认关闭的 `FireEffectRoot`、`IceEffectRoot`、`LightningEffectRoot`。Monster 只依据上述三个最近元素计时器控制节点显隐：剩余时间大于 0 且存活时开启，归零、死亡、初始化或回池时关闭；多个节点可以同时开启。节点内部的具体特效资源由后续美术装配，不参与玩法判断。

冰火效果只登记固定世界 `+Y` 位移。EnemyManager 在子弹阶段结束后统一应用，允许越过道路、其他敌人和 Army，不修改动作状态或动画；致命命中也先在新位置播放死亡动画。若范围攻击已经起手，其后续判定使用位移后的 Collider 姿态。死亡中的敌人仍可被组合特效视觉覆盖，但不再进入伤害候选或元素记录更新。反馈表现由 Animator、VFX 或其他视觉适配器消费，不在 Monster 内写死具体资源；声音功能延后。

## 性能约束

- 每个具体敌人类型使用轻量状态更新，不为每个运行时实例创建状态对象；四种类型的公共规则通过组合、接口、基类或纯 C# 逻辑复用。
- 不每帧扫描全部 Army 槽位；只在进入攻击、攻击结束或目标失效时重新选择目标。
- 范围攻击只在判定帧执行一次显式碰撞查询。
- 攻击起始范围只使用目标位置距离，不维护 `TargetSensor` 或第二份碰撞范围；不执行 Army/Enemy 相对运动扫掠或重合分离。
- AnimationEvent 只登记待结算攻击；同一 AttackSequenceId 最多登记和消费一次，死亡、StopRun 或回池会清除未消费请求。
- 敌人、子弹和死亡表现使用对象池，避免高频 Instantiate/Destroy。
- 碰撞查询使用明确的 Layer/ContactFilter2D；当前不以移动 Collider2D 的同步和 broadphase 成本作为首版阻塞项。

## 配置输入

- EnemyManager 通过 `IEnemyConfigProvider.GetEnemyConfig(ConfigId)` 必得已校验的 `EnemyConfigSnapshot`，读取敌人类型、生命值、攻击力、移动速度、攻击起始范围和攻击冷却；它不持有 Luban 生成行、不重复处理缺失 ID。攻击类型由敌人类型派生，四个规范 Prefab 由 EnemyManager 的 Inspector 引用提供。
- 敌人间阻挡安全间距只读取当前具体 Prefab 的 `blockingGap`；不同敌人类型可以使用不同值，不从 Luban 或 LevelConfig 读取第二份间距。
- 关卡何时生成哪一种敌人及其 `[0,1]` 横向出生位置由 `LevelConfig` 提供；敌人模块只消费 SpawnManager 已解析世界坐标的生成请求和配置。

## 测试标准

- `ChickMonster`、`HenMonster`、`RoosterMonster`、`IkunMonster` 各自只绑定一个规范 Prefab 和一个具体类型池；同一具体类型不能绑定第二个 Prefab。
- 四种敌人读取正确的类型和数值，并派生正确的攻击类型；EnemyManager 按 `EnemyType` 选择正确的具体类型池。
- 类型池返回未激活敌人；EnemyManager 设置父节点、位置、旋转、运行时 ID、配置和回调并完成登记后才激活。
- 敌人先向下移动至接近线，再向最近的有士兵槽位移动；只按当前目标位置的 XY 距离进入攻击起始范围并停止主动移动，距离为 0 的重合状态仍可正常攻击。
- 小鸡敌人单体攻击锁定目标；目标在判定帧前变为空时取消攻击并重新选目标。
- 母鸡和公鸡的攻击碰撞体只在攻击判定帧执行一次显式重叠查询，所有命中槽位受到相同伤害且每槽位只结算一次。
- 四种敌人的非循环 Attack Clip 都由命中关键帧调用一次 `OnAttackFrame()`，末帧调用一次 `OnAttackAnimationFinished()`；重复攻击帧回调不会造成第二次伤害。Ikun 在正式素材补入前由 Rooster 占位事件满足该契约。
- 四种敌人的 Move Clip 循环，默认进入 Move；Attack 与四种 Death 非循环，死亡分流由 `Death` Trigger 和 `DeathVariant` 共同决定，已绑定的 Controller/OverrideController 不出现 Missing Motion。
- AttackCooldown 从攻击开始时刻计算并在动画期间继续递减；若动画结束时已到期，下一次 TickMovement 满足存活、目标有效且在范围内即可开始下一次攻击。
- 四类敌人的普通/火/冰/雷 Death Clip 末尾各调用一次 `OnDeathAnimationFinished()`；它只登记回收，重复/过期事件不重复注销、计数或发布 MonsterKilled，StopRun 不等待该事件。
- 致命元素命中选择本次元素；非元素致命命中选择剩余时间最大的最近元素；任意并列按火、冰、雷。复借对象必须恢复普通死亡类型。
- AnimationEvent 发生后但 ResolveAttacks 前死亡、StopRun 或回池时，请求作废；事件在当帧 ResolveAttacks 之后发生时允许顺延到下一逻辑帧结算。
- 所有敌人 Prefab 都提供职责明确的 `BodyCollider`、同节点 `BulletHitProxy` 和有限非负的 `blockingGap`。BodyCollider Cast 只查询上一轮 Physics2D 同步姿态；后方敌人在命中前方敌人时截断本帧位移，但 MVP 不保证多个敌人同帧移动后的绝对不重叠，也不执行事后分离。
- 四类敌人根节点的 Rigidbody2D 必须满足 ADR-055 的 Kinematic 查询适配配置；BodyCollider 继续为 Trigger，自动碰撞矩阵关闭，阻挡结果仍只由 `ApplyBlockedMovement` 决定。
- 四类敌人 Prefab 均不包含 `TargetSensor`；Chick 不要求攻击 Collider，Hen/Rooster/Ikun 的 `AttackCollider` 只查询 `ArmySlot`。
- Army 横向移动不查询或阻挡 `EnemyBody`，Monster 移动也不以 `ArmySlot` 截断；允许士兵与敌人重合，重合时单体锁定伤害和范围查询仍按原规则结算。
- MVP 不实现局部时停；敌人进入 `Dead` 后立即退出受击和阻挡查询。未来启用局部时停时另行确认其碰撞行为。
- 首版不实现后方敌人从侧面绕行、通道预留或局部导航。
- 子弹碰撞只对敌人造成一次伤害，并将 BulletId、WeaponId、ActiveElements 传递到受击/击杀事件。
- 直接命中与组合效果统一生成 `EnemyDamageContext`；火雷范围和冰雷链只选择当时仍存活的敌人，同一目标可因同一帧多个独立效果分别受伤。
- 最近元素记录按元素分别计时 1 秒；组合范围伤害会刷新记录，死亡中或已死亡目标不会刷新。
- 三个元素效果根节点默认关闭，严格跟随对应最近元素记录；复用同一池实例不会残留上一轮节点激活状态，死亡后立即全部关闭。
- 冰火位移固定沿世界 `+Y`，不打断动作、不做碰撞/道路修正；已起手范围攻击在统一位移和第二次物理同步后按新位置结算。
- 生命值为 0 时只死亡一次；死亡后不再移动、攻击或接受新的伤害结算。
- `AliveEnemyCount` 的减少不依赖 `MonsterKilled` 监听者；移除所有事件监听者后，死亡去重、计数、注销和对象池回收仍保持正确。
- `MonsterKilled` 只在 EnemyManager 接受有效死亡报告并更新 `AliveEnemyCount` 后发布一次。
- 敌人不持有 PoolService 或类型池，也不在 `OnDisable`、`OnDestroy` 中归还；归还时已清除本局回调和运行时上下文，类型池再次防御性失活。
- 敌人在固定 `spawnY` 横线上按 `spawnPosition` 生成后先垂直下移，到达 `enemyApproachY` 后可以横向接近最近的有效士兵槽位。
- 敌人移动和攻击计时使用 LevelManager 传入的 Monster 时间域 delta，不直接访问 TimeService。
