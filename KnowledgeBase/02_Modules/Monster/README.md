# Monster 怪物模块

## 模块信息

- ID：`MOD-MONSTER`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：Bullet、Army、Level、EventBus、ConfigService、PoolService
- 决策：`../../06_Decisions/ADR-005-MonsterCombatAndManager.md`、`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-037-MonsterDistanceTargetingAndCollisionLayers.md`

## 职责

- 由 `EnemyManager` 统一生成、注册、统计和回收敌人实例。
- 从道路上方生成，先向下移动至关卡接近线，再向最近的有士兵槽位移动。
- 只按怪物与锁定槽位目标位置的 XY 距离判断攻击起始范围；进入范围后停止移动，执行单体或范围普通攻击。
- 接收子弹碰撞伤害并保存命中子弹上下文；通过注入的表现事实回调报告受击，通过 EnemyManager 的必执行回调报告死亡。
- 死亡后停止移动、攻击和碰撞处理，播放死亡动画并在动画结束后回收。
- 使用 `BodyCollider` 参与子弹受击和存活敌人之间的阻挡；通过显式 Cast/Overlap 查询决定敌人间移动截断和范围攻击命中。

## 非职责

- 不直接修改 Army 总人数，只通过 ArmyController 的槽位伤害接口结算攻击。
- 不处理敌人与 Army 槽位之间的移动碰撞、推挤或重合分离；Army 移动造成的部分或完全重合属于允许状态。
- 不决定生成时间轴和关卡胜负条件。
- 不负责具体的 Sprite、粒子或 Animator Controller 资源选择；MVP 不使用 AudioClip。

## 敌人类型与配置

三种敌人使用三个具体池化根脚本和三个规范 Prefab，为后续不同逻辑保留边界；当前共有的接近、受击、死亡和目标查询规则继续提取复用，不复制三套相同实现：

| 类型 | 具体池化根脚本 | 规范 Prefab | 当前攻击类型 |
|---|---|---|---|
| `Normal` | `NormalMonster` | 普通敌人 Prefab | `SingleTarget` |
| `Elite` | `EliteMonster` | 精英敌人 Prefab | `Area` |
| `Boss` | `BossMonster` | Boss Prefab | `Area`；具体阶段和专属技能另行设计 |

敌人从 `TbEnemy` 读取：`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange` 和 `AttackCooldown`。`AttackType` 由 `EnemyType` 固定派生：普通敌人为单体攻击，精英和 Boss 为范围攻击。EnemyManager 通过 Inspector 分别绑定三个规范 Prefab，并按 `EnemyType` 选择对应的具体类型池；Prefab 或池类型不写入 Luban。道路接近线由 `LevelConfig` 的固定空间配置提供，不写入敌人运行时状态；攻击判定时刻由 Animator 判定帧提供。

## 行为流程

```text
MovingDown
  -> ApproachingTarget
  <-> Blocked（前方存活敌人较慢或静止）
  -> Attacking
  -> MovingDown / ApproachingTarget（攻击结束后重新判断）
  -> Dead
```

- `MovingDown`：向道路接近线移动。
- 归一化横向出生位置只决定初始中心点；进入 `ApproachingTarget` 后不保留出生位置约束。
- `ApproachingTarget`：从 ArmyController 获取最近的有效士兵槽位并向其当前位置移动；不使用 `TargetSensor`，当 XY 距离平方小于等于 `AttackStartRange²` 时进入攻击。
- `Blocked`：保持与前方敌人的安全间距并等待；首版不从侧面绕行。阻挡解除后恢复原移动状态。
- `Attacking`：进入攻击起始范围后停止移动。
- `Dead`：不可移动、不可攻击、不可再次受伤结算，等待死亡动画结束。

目标选择只接受 `RepresentedCount > 0` 的槽位；目标失效时，普通敌人取消当前攻击并重新选择目标、重新开始攻击动画。目标位置随 Army 移动更新；敌人与槽位部分或完全重合时距离仍满足攻击起始条件，不取消或阻塞攻击。

## 攻击规则

### 单体攻击

普通敌人锁定一个槽位，在 Animator 的攻击判定帧验证敌人和目标仍然有效，然后通过 ArmyController 的 `ApplySlotDamage(slotIndex, AttackPower)` 造成一次伤害。判定帧前任一方失效都会取消当前攻击。

### 范围攻击

精英和 Boss 的 Prefab 前方绑定 `AttackCollider`。碰撞体平时禁用，只在攻击判定帧执行一次显式重叠查询；对查询结果中的所有有效槽位各结算一次相同的 `AttackPower`。同一次攻击使用攻击序号和 `SlotIndex` 去重，随后结束本次查询。

### 攻击与动画

攻击状态由 Monster 控制，Animator 负责移动、攻击、受击和死亡序列帧。Animator 的攻击判定帧只登记本次攻击的待结算标记；EnemyManager 在 LevelManager 调用 `ResolveAttacks` 时验证会话、敌人、目标和攻击序号后执行伤害。敌人在正式攻击阶段前死亡时不得产生攻击伤害。

## 碰撞体

- `BodyCollider`：敌人身体与子弹命中目标；不同类型的尺寸在各自 Prefab 中配置。
- `AttackCollider`：仅用于精英和 Boss 的范围攻击，在判定帧执行一次显式重叠查询。
- 存活敌人的 BodyCollider 只参与子弹受击和敌人间阻挡，不查询或阻挡 Army `SlotCollider`。
- `AttackCollider` 不参与敌人身体阻挡或子弹命中；Army `SlotCollider` 与 Enemy `BodyCollider` 允许重合，重合本身不产生伤害、推挤或事件。
- 同一子弹命中多个子碰撞体时只结算一次。

## EnemyManager

`EnemyManager` 负责：

- 接收 Spawn 根据 `LevelConfig` 发出的生成请求。
- 通过 Inspector 绑定 `NormalMonster`、`EliteMonster`、`BossMonster` 三个规范 Prefab，并从 PoolService 分别取得三个具体类型池。
- 从对应类型池取得未激活敌人，设置 `MonsterRoot`、世界位置和旋转，分配运行时 ID、注入配置与回调、登记活动集合后再激活。
- 敌人请求结束时先完成死亡/离场去重、计数、事件和活动注销，再执行 `PrepareForPool`、主动失活并归还对应类型池；池的防御性失活不替代这些业务步骤。
- 维护活跃敌人列表以及 `ActiveEnemyCount`、`AliveEnemyCount`。
- 在敌人死亡时立即减少 `AliveEnemyCount`，在死亡动画完成后注销并回收。
- 对重复死亡、重复注册和重复回收进行幂等保护。
- 接受 Monster 的必执行死亡回调，在完成死亡去重并更新 `AliveEnemyCount` 后发布 `MonsterKilled`；事件监听者不参与必需的计数、注销或回收。
- 实现 `TickMovement`、`ResolveAttacks` 和 `FlushPendingRecycles`；只由 LevelManager 按 ADR-033 的阶段顺序调用，不使用独立 Update 推进核心玩法。

`AliveEnemyCount == 0` 只表示当前已生成敌人全部死亡；Level 还必须确认 `enemySpawns` 已全部处理，才允许判定关卡胜利。死亡动画尚未结束的敌人不再计入存活数量。

## 受击与反馈

子弹碰撞向敌人传入 `BulletDamageContext`，包含 `BulletId`、`WeaponId`、发射瞬间的 `ActiveElements`、最终伤害、命中位置和方向。Monster 通过 Manager 注入的发布回调报告 `MonsterDamaged`；生命值归零时先调用 EnemyManager 的必执行死亡回调，由 EnemyManager 完成死亡去重和 `AliveEnemyCount` 更新，再发布携带最后一击上下文的 `MonsterKilled`。MVP 的反馈表现由 Animator、VFX 或其他视觉适配器消费，不在 Monster 内写死具体资源；声音功能延后。

## 性能约束

- 每个具体敌人类型使用轻量状态更新，不为每个运行时实例创建状态对象；三种类型的公共规则通过组合、接口、基类或纯 C# 逻辑复用。
- 不每帧扫描全部 Army 槽位；只在进入攻击、攻击结束或目标失效时重新选择目标。
- 范围攻击只在判定帧执行一次显式碰撞查询。
- 攻击起始范围只使用目标位置距离，不维护 `TargetSensor` 或第二份碰撞范围；不执行 Army/Enemy 相对运动扫掠或重合分离。
- 敌人、子弹和死亡表现使用对象池，避免高频 Instantiate/Destroy。
- 碰撞查询使用明确的 Layer/ContactFilter2D；当前不以移动 Collider2D 的同步和 broadphase 成本作为首版阻塞项。

## 配置输入

- 从 Luban `TbEnemy` 读取敌人类型、生命值、攻击力、移动速度、攻击起始范围和攻击冷却；攻击类型由敌人类型派生，三个规范 Prefab 由 EnemyManager 的 Inspector 引用提供。
- 关卡何时生成哪一种敌人及其 `[0,1]` 横向出生位置由 `LevelConfig` 提供；敌人模块只消费 SpawnManager 已解析世界坐标的生成请求和配置。

## 测试标准

- `NormalMonster`、`EliteMonster`、`BossMonster` 各自只绑定一个规范 Prefab 和一个具体类型池；同一具体类型不能绑定第二个 Prefab。
- 三种敌人读取正确的类型和数值，并派生正确的攻击类型；EnemyManager 按 `EnemyType` 选择正确的具体类型池。
- 类型池返回未激活敌人；EnemyManager 设置父节点、位置、旋转、运行时 ID、配置和回调并完成登记后才激活。
- 敌人先向下移动至接近线，再向最近的有士兵槽位移动；只按当前目标位置的 XY 距离进入攻击起始范围并停止主动移动，距离为 0 的重合状态仍可正常攻击。
- 普通敌人单体攻击锁定目标；目标在判定帧前变为空时取消攻击并重新选目标。
- 精英和 Boss 的攻击碰撞体只在攻击判定帧执行一次显式重叠查询，所有命中槽位受到相同伤害且每槽位只结算一次。
- 所有敌人 Prefab 都提供职责明确的 `BodyCollider`；存活敌人不重叠，后方敌人遇到较慢或静止的前方敌人时保持安全距离等待。
- 三类敌人 Prefab 均不包含 `TargetSensor`；Normal 不要求攻击 Collider，Elite/Boss 的 `AttackCollider` 只查询 `ArmySlot`。
- Army 横向移动不查询或阻挡 `EnemyBody`，Monster 移动也不以 `ArmySlot` 截断；允许士兵与敌人重合，重合时单体锁定伤害和范围查询仍按原规则结算。
- MVP 不实现局部时停；敌人进入 `Dead` 后立即退出受击和阻挡查询。未来启用局部时停时另行确认其碰撞行为。
- 首版不实现后方敌人从侧面绕行、通道预留或局部导航。
- 子弹碰撞只对敌人造成一次伤害，并将 BulletId、WeaponId、ActiveElements 传递到受击/击杀事件。
- 生命值为 0 时只死亡一次；死亡后不再移动、攻击或接受新的伤害结算。
- `AliveEnemyCount` 的减少不依赖 `MonsterKilled` 监听者；移除所有事件监听者后，死亡去重、计数、注销和对象池回收仍保持正确。
- `MonsterKilled` 只在 EnemyManager 接受有效死亡报告并更新 `AliveEnemyCount` 后发布一次。
- 敌人不持有 PoolService 或类型池，也不在 `OnDisable`、`OnDestroy` 中归还；归还时已清除本局回调和运行时上下文，类型池再次防御性失活。
- 敌人在固定 `spawnY` 横线上按 `spawnPosition` 生成后先垂直下移，到达 `enemyApproachY` 后可以横向接近最近的有效士兵槽位。
- 敌人移动和攻击计时使用 LevelManager 传入的 Monster 时间域 delta，不直接访问 TimeService。
