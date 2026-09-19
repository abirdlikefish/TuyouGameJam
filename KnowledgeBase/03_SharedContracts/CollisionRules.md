# Collider2D 碰撞契约

本文件是玩法碰撞形状、查询方向和结算所有权的共享契约。碰撞检测提供候选命中事实，最终是否造成伤害、接触效果或回收，仍由目标模块的状态机和运行时去重规则决定。

## 总体规则

- 所有参与玩法碰撞的对象都通过 Inspector 绑定 `Collider2D`，不在运行时使用 `AddComponent` 补齐缺失引用。
- LevelManager 在逻辑帧开始从 `TimeService` 读取各域有效 delta，并传给对应 Manager；Collider2D 不负责通过力或自动碰撞响应移动对象。
- 核心结算由显式 Cast/Overlap 查询触发，不依赖自动碰撞回调的执行顺序。
- 查询统一使用无分配版本或复用结果缓存，并通过 Layer 和 `ContactFilter2D` 限定目标。
- 同一次命中或接触即使返回多个子 Collider，也只能结算一次。
- 受击 Collider 节点通过同 GameObject 上已验证的 `BulletHitProxy` 映射到 Enemy/Gate/Prop 根玩法对象；Army SlotCollider 节点通过 `ArmySlotHitProxy` 映射到 `ArmyId + SlotIndex`。运行时不向父级搜索缺失引用。
- Level 不处于 `Playing` 时停止新的玩法查询与结算；重开前禁用或回收所有运行时碰撞对象。

## Collider 职责

| 所有者 | Collider | 用途 | 主要查询方式 | 目标 |
|---|---|---|---|---|
| Bullet | `BodyCollider` | 子弹飞行扫掠 | Collider Cast / CircleCast | Enemy、Gate、Prop 的受击 Collider |
| Monster | `BodyCollider` | 子弹受击、敌人间阻挡 | Bullet Cast、Monster Body Cast | Bullet、其他存活 Monster |
| Monster | `AttackCollider` | 精英/Boss 范围攻击 | 判定帧 `OverlapCollider` | Army `SlotCollider` |
| Army Slot | `SlotCollider` | 接收攻击和道路对象接触 | Monster Attack、Gate/Prop 终点 Overlap | Monster `AttackCollider`、Gate、Prop |
| Gate | `BodyCollider` | 子弹命中和 Army 接触 | Bullet Cast、Gate `OverlapCollider` | Bullet、Army Slot |
| Prop | `BodyCollider` | 子弹命中和 Army 接触 | Bullet Cast、Prop `OverlapCollider` | Bullet、Army Slot |

道路四边、生成线、接近线和离场线全部来自 `RoadLayoutSnapshot` 的数值；道路不设置玩法 Collider。ArmyRoot 从世界原点开始，其激活槽位合并 AABB 使用 LeftBoundary/RightBoundary 做横向限制。

## 查询与结算顺序

LevelManager 是以下阶段的唯一调用顺序所有者。各 Manager 管理自己的对象和规则，但池对象不得通过独立 Update 绕过该顺序：

```text
读取 Gameplay、Bullet、Gate、Monster delta
→ SpawnManager 派发到时对象
→ Army、Enemy、Gate/Prop 计算期望位移并执行敌人阻挡
→ 应用位置并由 LevelManager 调用一次 Physics2D.SyncTransforms
→ BulletManager 执行移动扫掠、命中查询与结算
→ ObstacleManager 执行 Gate/Prop 接触查询与结算
→ EnemyManager 执行 Monster AttackCollider 攻击查询与结算
→ 按运行时 ID/攻击序号去重、发布事实并刷新待回收对象
→ LevelManager 执行终局判断
```

上述阶段是同帧玩法结算的权威顺序，使用类型化同步接口而不是逐帧 EventBus 事件。MVP 所有时间倍率为 `1`；后续启用自定义时间域时仍保持阶段顺序不变。`LevelRunStarted` 只切换到 Playing，不替代该逐帧协调。

当前项目关闭 Physics2D Auto Sync Transforms。LevelManager 在全部玩法移动应用完成后、首次 Bullet/接触/攻击查询前准确调用一次 `Physics2D.SyncTransforms()`；各 Manager 和池对象不得自行重复同步。

同一次 Bullet Cast 存在多个有效目标时，按 Cast 距离优先；距离相同时按目标类别 `Enemy` > `Gate` > `Prop`；类别和距离仍相同时按 `RuntimeInstanceId` 升序。同一运行时实例的多个子 Collider 必须先去重。该优先级只处理完全相同距离的稳定决胜。

## 子弹规则

- 子弹保存上一次逻辑位置，并从该位置向期望位置扫掠自己的 Collider 形状。
- 命中首个实现 `IBulletHittable` 且 `CanReceiveBulletHit = true` 的 Enemy、Gate 或 Prop 后生成一次 `BulletDamageContext`，立即将 `BulletInstanceId` 标记为已消费并回收。合法子弹目标不要求一定实现带 HP 语义的 `IDamageable`。
- 加法门没有 HP，但在 `Pending` 状态下是合法子弹目标；命中按实际伤害增加门值并消费子弹。
- 元素门 HP 归零后，只要仍处于 `Pending`，就继续作为合法子弹目标并消费子弹；该次全部伤害计入 `PostDepletionDamage`。接触失败后奖励永久锁定，后续命中不得增加可兑换的 `PostDepletionDamage`。
- `AttackCollider`、其他 Bullet 和 Army `SlotCollider` 不属于首版子弹目标。
- 飞行中 Army 的武器、元素或人数变化不修改已经生成的子弹快照。
- 首轮只扫掠子弹自身从上一逻辑位置到期望位置的位移，不计算子弹与本帧同时移动目标的相对扫掠，也不做子步进。MVP 通过合理的速度、Collider 尺寸、编排和目标帧率避免穿透；测试不承诺任意高速或严重掉帧下绝不穿透。

## 敌人阻挡

- 只有存活且已注册的 Monster `BodyCollider` 参与敌人间阻挡。
- 敌人主动移动前沿期望位移 Cast；查询只看到上一轮 `Physics2D.SyncTransforms` 后的物理姿态。检测到前方敌人时，将位移按 Cast 距离和当前敌人规范 Prefab 根脚本序列化的 `blockingGap` 截断。该值必须有限且非负，不进入 Luban 或 LevelConfig。
- 前方敌人的速度较慢或为 0 时仍可形成阻挡；该离散规则只在 MVP 约定速度、Collider 尺寸和帧率下减少穿透与重叠，不保证多个敌人同帧移动后的绝对不重叠，也不执行事后分离。
- 两侧绕行不属于 MVP。当前没有可直接前进的安全位移时，后方敌人保持等待；阻挡解除后继续原移动状态。
- 敌人进入 `Dead` 后立即禁用玩法 BodyCollider 或将其移出受击/阻挡查询 Layer。

## 敌人与 Army 的空间关系

- MVP 不处理 Enemy `BodyCollider` 与 Army `SlotCollider` 之间的移动碰撞。Monster 移动不查询 `ArmySlot`，Army 横向移动也不查询 `EnemyBody`。
- Army 槽位与敌人身体允许部分或完全重合；重合不产生接触伤害、推挤、位移修正或额外事件。
- 怪物是否开始攻击只比较自身世界位置与已锁定 `ArmySlotTarget.WorldPosition` 的 XY 距离；`distanceSquared <= AttackStartRange²` 时进入攻击，距离为 `0` 时同样成立。
- 普通敌人通过锁定的 `SlotIndex` 结算单体伤害；精英和 Boss 通过 `AttackCollider` 查询 `ArmySlot`。允许重合不能阻止上述两类攻击。
- `AttackStartRange` 和范围攻击形状应通过关卡与 Prefab 调参尽量减少不自然重合，但“不重合”不是 MVP 玩法不变量。不处理 Army 与 Enemy 同帧相向快速移动或相对运动扫掠。

## 范围攻击与道路对象接触

- Animator Attack Clip 的命中关键帧只登记当前 AttackSequenceId 的攻击请求；`AttackCollider` 只在 EnemyManager.ResolveAttacks 消费该请求时用于一次显式重叠查询。同一攻击以攻击序号和 `SlotIndex` 去重。Collider 可以保持启用作为查询形状，但自动碰撞矩阵关闭，不通过启停 Collider 决定攻击窗口。
- Gate/Prop 在本帧位移已经应用且 `Physics2D.SyncTransforms()` 完成后，只以终点姿态执行一次 `BodyCollider.OverlapCollider`。不对移动路径执行 Cast、扫掠或子步进；合理速度、Collider 尺寸与目标帧率是 MVP 的防穿透约束。
- Gate/Prop 只对当前查询命中的有效 Army 槽位结算。多个槽位接触同一对象时，整体成功效果只应用一次，逐槽伤害按槽位索引去重。
- Gate/Prop 接触失败后继续保留用于子弹受击表现的 BodyCollider，但接触状态机必须拒绝后续成功奖励。后续命中正常消费子弹，HP 按 `Max(1, CurrentHp - Damage)` 锁在至少 `1`：元素门不再累计可兑换伤害，Prop 不发布 `PropBroken`；两者只继续移动至离场或由 StopRun 清理。
- MVP 不实现暂停或局部时停。未来启用后需要重新确认停止对象的 Collider2D 是否仍可被其他活动对象查询，以及暂停与受击判定的关系。

## Layer 约束

固定区分以下职责 Layer：

```text
Bullet
EnemyBody
EnemyAttack
ArmySlot
Gate
Prop
```

Layer 只负责过滤候选目标，不替代模块状态检查。道路左右边界、生成线、接近线和离场线均使用数值，不建立 Collider 或 Layer。

所有纵向数值线以池化实例根 GameObject 的 `transform.position` 为准：出生使用中心落在 `SpawnY`；怪物中心到达 `EnemyApproachY` 后切换接近 Army；Gate/Prop 中心满足 `y <= DespawnY` 时离场；子弹中心满足 `y > TopBoundary`（即 `roadBounds.yMax`）时回收。不得改用 Collider 或 Renderer 边缘。Army 横向边界使用激活槽位合并 AABB，是唯一明确例外。

## Layer Collision Matrix（DES-030）

当前核心规则不依赖 `OnTriggerEnter2D`、`OnCollisionEnter2D` 或 Rigidbody2D 自动响应，因此上述 Gameplay Layer 之间的自动物理碰撞矩阵默认全部关闭，避免隐式推挤、回调和重复结算。显式查询继续通过 `ContactFilter2D` 或 LayerMask 命中目标，不以矩阵开关代替查询过滤。

显式查询方向固定为：

| 查询者 | 允许查询目标 | 用途 |
|---|---|---|
| Bullet `BodyCollider` | `EnemyBody`、`Gate`、`Prop` | 子弹扫掠命中 |
| Monster `BodyCollider` | 其他存活 `EnemyBody` | 移动阻挡 |
| Monster `AttackCollider` | `ArmySlot` | 精英/Boss 攻击判定帧 |
| Gate `BodyCollider` | `ArmySlot` | Gate 接触 |
| Prop `BodyCollider` | `ArmySlot` | Prop 接触 |

`EnemyBody` 与 `ArmySlot` 不建立移动查询或自动物理关系。`EnemyAttack` 与 `EnemyBody`、Gate 与 Prop、Enemy 与 Gate/Prop、ArmySlot 与 ArmySlot 等关系同样保持关闭。若后续启用 Kinematic Rigidbody2D 适配，必须保持上述查询方向和状态机仍为权威，并另行记录需要开启的最小矩阵对。

`BulletHitProxy` 和 `ArmySlotHitProxy` 都是查询身份适配，不新增物理关系。Gameplay Preparing 必须验证代理位于对应 Collider 的同一节点、目标引用非空且职责 Layer 正确；查询命中后只读取该节点代理，并按 RuntimeInstanceId 或 SlotIndex 去重。

工程实现时应使用项目目标 Unity 版本验证：当 Layer Collision Matrix 关闭对应 Layer 对时，带显式 `ContactFilter2D`/LayerMask 的 Cast 与 Overlap 仍按上表返回目标；若具体 API 受矩阵影响，则只开启该查询所需的最小 Layer 对，不开放自动玩法回调。设计定案见 ADR-037。
