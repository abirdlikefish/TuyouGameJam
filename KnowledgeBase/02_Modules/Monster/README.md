# Monster 怪物模块

## 模块信息

- ID：`MOD-MONSTER`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：TimeService、Bullet、Army、Level、EventBus、ConfigService、PoolService

## 职责

- 由 `EnemyManager` 统一生成、注册、统计和回收敌人实例。
- 从道路上方生成，先向下移动至关卡接近线，再向最近的有士兵槽位移动。
- 进入攻击起始范围后停止移动，执行单体或范围普通攻击。
- 接收子弹碰撞伤害，保存命中子弹上下文，并发布受击/击杀事件。
- 死亡后停止移动、攻击和碰撞处理，播放死亡动画并在动画结束后回收。
- 使用 `BodyCollider` 参与子弹受击和存活敌人之间的阻挡；通过显式 Cast/Overlap 查询决定移动截断和攻击目标。

## 非职责

- 不直接修改 Army 总人数，只通过 ArmyController 的槽位伤害接口结算攻击。
- 不决定生成时间轴和关卡胜负条件。
- 不负责具体的 Sprite、粒子或 Animator Controller 资源选择；MVP 不使用 AudioClip。

## 敌人类型与配置

首版三种敌人共享同一套行为流程：

| 类型 | 攻击类型 | 首版差异来源 |
|---|---|---|
| `Normal` | `SingleTarget` | 数值和普通敌人 Prefab |
| `Elite` | `Area` | 数值和精英敌人 Prefab |
| `Boss` | `Area` | 更高或不同数值、Boss Prefab；首版无额外阶段 |

敌人从 `TbEnemy` 读取：`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange` 和 `AttackCooldown`。`AttackType` 由 `EnemyType` 固定派生：普通敌人为单体攻击，精英和 Boss 为范围攻击。Prefab 由 Unity 侧按 `EnemyType` 绑定。道路接近线由 `LevelConfig` 的固定空间配置提供，不写入敌人运行时状态；攻击判定时刻由 Animator 判定帧提供。

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
- `ApproachingTarget`：从 ArmyController 获取最近的有效士兵槽位并向其当前位置移动。
- `Blocked`：保持与前方敌人的安全间距并等待；首版不从侧面绕行。阻挡解除后恢复原移动状态。
- `Attacking`：进入攻击起始范围后停止移动。
- `Dead`：不可移动、不可攻击、不可再次受伤结算，等待死亡动画结束。

目标选择只接受 `RepresentedCount > 0` 的槽位；目标失效时，普通敌人取消当前攻击并重新选择目标、重新开始攻击动画。

## 攻击规则

### 单体攻击

普通敌人锁定一个槽位，在 Animator 的攻击判定帧验证敌人和目标仍然有效，然后通过 ArmyController 的 `ApplySlotDamage(slotIndex, AttackPower)` 造成一次伤害。判定帧前任一方失效都会取消当前攻击。

### 范围攻击

精英和 Boss 的 Prefab 前方绑定 `AttackCollider`。碰撞体平时禁用，只在攻击判定帧执行一次显式重叠查询；对查询结果中的所有有效槽位各结算一次相同的 `AttackPower`。同一次攻击使用攻击序号和 `SlotIndex` 去重，随后结束本次查询。

### 攻击与动画

攻击状态由 Monster 控制，Animator 负责移动、攻击、受击和死亡序列帧。Animator 的攻击判定帧只通知 Monster 执行结算；敌人在判定帧前死亡时不得产生攻击伤害。

## 碰撞体

- `BodyCollider`：敌人身体与子弹命中目标；不同类型的尺寸在各自 Prefab 中配置。
- `AttackCollider`：仅用于精英和 Boss 的范围攻击，在判定帧执行一次显式重叠查询。
- `TargetSensor`：使用 Collider2D 辅助确认攻击起始范围，不参与实体阻挡。
- 存活敌人的 BodyCollider 参与敌人间阻挡；AttackCollider、TargetSensor 不参与敌人身体阻挡或子弹命中。
- 同一子弹命中多个子碰撞体时只结算一次。

## EnemyManager

`EnemyManager` 负责：

- 接收 Spawn 根据 `LevelConfig` 发出的生成请求。
- 从 PoolService 获取、初始化和回收敌人。
- 维护活跃敌人列表以及 `ActiveEnemyCount`、`AliveEnemyCount`。
- 在敌人死亡时立即减少 `AliveEnemyCount`，在死亡动画完成后注销并回收。
- 对重复死亡、重复注册和重复回收进行幂等保护。

`AliveEnemyCount == 0` 只表示当前已生成敌人全部死亡；Level 还必须确认 `enemySpawns` 已全部处理，才允许判定关卡胜利。死亡动画尚未结束的敌人不再计入存活数量。

## 受击与反馈

子弹碰撞向敌人传入 `BulletDamageContext`，包含 `BulletId`、`WeaponId`、`ElementId`、最终伤害、命中位置和方向。Monster 发布 `MonsterDamaged`；造成生命值归零的最后一次上下文随 `MonsterKilled` 发布。MVP 的反馈表现由 Animator、VFX 或其他视觉适配器消费，不在 Monster 内写死具体资源；声音功能延后。

## 性能约束

- 使用 `enum + switch` 的轻量状态更新，不创建每个敌人的状态对象。
- 不每帧扫描全部 Army 槽位；只在进入攻击、攻击结束或目标失效时重新选择目标。
- 范围攻击只在判定帧执行一次显式碰撞查询。
- 敌人、子弹和死亡表现使用对象池，避免高频 Instantiate/Destroy。
- 碰撞查询使用明确的 Layer/ContactFilter2D；当前不以移动 Collider2D 的同步和 broadphase 成本作为首版阻塞项。

## 配置输入

- 从 Luban `TbEnemy` 读取敌人类型、生命值、攻击力、移动速度、攻击起始范围和攻击冷却；攻击类型由敌人类型派生，Prefab 从 Unity 资源绑定取得。
- 关卡何时生成哪一种敌人及其 `[0,1]` 横向出生位置由 `LevelConfig` 提供；敌人模块只消费 SpawnManager 已解析世界坐标的生成请求和配置。

## 测试标准

- 三种敌人读取正确的类型和数值，并派生正确的攻击类型、取得对应的 Unity Prefab 绑定。
- 敌人先向下移动至接近线，再向最近的有士兵槽位移动；进入攻击起始范围后停止移动。
- 普通敌人单体攻击锁定目标；目标在判定帧前变为空时取消攻击并重新选目标。
- 精英和 Boss 的攻击碰撞体只在攻击判定帧执行一次显式重叠查询，所有命中槽位受到相同伤害且每槽位只结算一次。
- 所有敌人 Prefab 都提供职责明确的 `BodyCollider`；存活敌人不重叠，后方敌人遇到较慢或静止的前方敌人时保持安全距离等待。
- MVP 不实现局部时停；敌人进入 `Dead` 后立即退出受击和阻挡查询。未来启用局部时停时另行确认其碰撞行为。
- 首版不实现后方敌人从侧面绕行、通道预留或局部导航。
- 子弹碰撞只对敌人造成一次伤害，并将 BulletId、WeaponId、ElementId 传递到受击/击杀事件。
- 生命值为 0 时只死亡一次；死亡后不再移动、攻击或接受新的伤害结算。
- `AliveEnemyCount` 与死亡事件、对象池回收保持一致。
- 敌人在固定 `spawnY` 横线上按 `spawnPosition` 生成后先垂直下移，到达 `enemyApproachY` 后可以横向接近最近的有效士兵槽位。
- 敌人移动和攻击计时使用 Monster 时间域。
