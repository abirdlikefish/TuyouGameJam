# Collider2D 碰撞契约

本文件是玩法碰撞形状、查询方向和结算所有权的共享契约。碰撞检测提供候选命中事实，最终是否造成伤害、接触效果或回收，仍由目标模块的状态机和运行时去重规则决定。

## 总体规则

- 所有参与玩法碰撞的对象都通过 Inspector 绑定 `Collider2D`，不在运行时使用 `AddComponent` 补齐缺失引用。
- 玩法移动使用 `TimeService` 的有效 delta；Collider2D 不负责通过力或自动碰撞响应移动对象。
- 核心结算由显式 Cast/Overlap 查询触发，不依赖自动碰撞回调的执行顺序。
- 查询统一使用无分配版本或复用结果缓存，并通过 Layer 和 `ContactFilter2D` 限定目标。
- 同一次命中或接触即使返回多个子 Collider，也只能结算一次。
- Level 不处于 `Playing` 时停止新的玩法查询与结算；重开前禁用或回收所有运行时碰撞对象。

## Collider 职责

| 所有者 | Collider | 用途 | 主要查询方式 | 目标 |
|---|---|---|---|---|
| Bullet | `BodyCollider` | 子弹飞行扫掠 | Collider Cast / CircleCast | Enemy、Gate、Prop 的受击 Collider |
| Monster | `BodyCollider` | 子弹受击、敌人间阻挡 | Bullet Cast、Monster Body Cast | Bullet、其他存活 Monster |
| Monster | `AttackCollider` | 精英/Boss 范围攻击 | 判定帧 `OverlapCollider` | Army `SlotCollider` |
| Monster | `TargetSensor` | 攻击起始范围辅助 | Overlap 或距离检查 | Army `SlotCollider` |
| Army Slot | `SlotCollider` | 接收攻击和道路对象接触 | Monster Attack、Gate/Prop Cast/Overlap | Monster、Gate、Prop |
| Gate | `BodyCollider` | 子弹命中和 Army 接触 | Bullet Cast、Gate Cast/Overlap | Bullet、Army Slot |
| Prop | `BodyCollider` | 子弹命中和 Army 接触 | Bullet Cast、Prop Cast/Overlap | Bullet、Army Slot |

道路左右边界使用 `RoadLayoutSnapshot.LeftBoundary/RightBoundary` 做数值限制，不要求额外道路 Collider。

## 查询与结算顺序

每个逻辑帧按以下阶段处理：

```text
读取各时间域和对象的有效 delta
→ 计算期望位移
→ 执行移动扫掠与敌人阻挡
→ 应用位置并在需要时同步 Physics2D Transform
→ 执行子弹命中查询并结算
→ 执行 Gate/Prop 接触查询并结算
→ 执行 Monster AttackCollider 攻击查询并结算
→ 按运行时 ID/攻击序号去重并发布事实事件、回收对象
→ LevelManager 执行终局判断
```

上述阶段是同帧玩法结算的权威顺序。MVP 所有时间倍率为 `1`；后续启用自定义时间域时仍保持阶段顺序不变。

同一次 Bullet Cast 存在多个有效目标时，按 Cast 距离优先；距离相同时按目标类别 `Enemy` > `Gate` > `Prop`；类别和距离仍相同时按 `RuntimeInstanceId` 升序。同一运行时实例的多个子 Collider 必须先去重。该优先级只处理完全相同距离的稳定决胜。

## 子弹规则

- 子弹保存上一次逻辑位置，并从该位置向期望位置扫掠自己的 Collider 形状。
- 命中首个仍可受击的 Enemy、Gate 或 Prop 后生成一次 `BulletDamageContext`，立即将 `BulletInstanceId` 标记为已消费并回收。
- `AttackCollider`、`TargetSensor`、其他 Bullet 和 Army `SlotCollider` 不属于首版子弹目标。
- 飞行中 Army 的武器、元素或人数变化不修改已经生成的子弹快照。

## 敌人阻挡

- 只有存活且已注册的 Monster `BodyCollider` 参与敌人间阻挡。
- 敌人主动移动前沿期望位移 Cast；检测到前方敌人时，将位移截断到配置或 Prefab 定义的安全间距。
- 前方敌人的速度较慢、为 0 或因局部时停停止时仍保持阻挡；后方敌人不得推动、穿透或与其重叠。
- 两侧绕行不属于 MVP。当前没有可直接前进的安全位移时，后方敌人保持等待；阻挡解除后继续原移动状态。
- 敌人进入 `Dead` 后立即禁用玩法 BodyCollider 或将其移出受击/阻挡查询 Layer。

## 范围攻击与道路对象接触

- `AttackCollider` 只在攻击判定时用于一次显式重叠查询。同一攻击以攻击序号和 `SlotIndex` 去重。
- Gate/Prop 只对当前查询命中的有效 Army 槽位结算。多个槽位接触同一对象时，整体成功效果只应用一次，逐槽伤害按槽位索引去重。
- Gate/Prop 接触失败后可以继续保留用于子弹受击表现的 BodyCollider，但接触状态机必须拒绝后续成功奖励。
- 局部时停对象的 Collider2D 默认仍可被其他活动对象查询；暂停本身不等同于无敌或无碰撞。

## Layer 约束

至少区分以下职责 Layer，最终名称在 Unity 工程建立前统一确认：

```text
Bullet
EnemyBody
EnemyAttack
ArmySlot
Gate
Prop
```

Layer 只负责过滤候选目标，不替代模块状态检查。具体 Layer Collision Matrix 在 Prefab 和 Project Settings 建立时，应与本文件的查询方向一致。

## Layer Collision Matrix 分析（DES-030，待评审）

当前核心规则不依赖 `OnTriggerEnter2D`、`OnCollisionEnter2D` 或 Rigidbody2D 自动响应，因此推荐将上述 Gameplay Layer 之间的物理碰撞矩阵默认全部关闭，避免隐式推挤、回调和重复结算。显式查询继续通过 `ContactFilter2D` 或 LayerMask 命中目标，不以矩阵开关代替查询过滤。

查询方向建议保持为：

| 查询者 | 允许查询目标 | 用途 |
|---|---|---|
| Bullet `BodyCollider` | `EnemyBody`、`Gate`、`Prop` | 子弹扫掠命中 |
| Monster `BodyCollider` | 其他存活 `EnemyBody` | 移动阻挡 |
| Monster `AttackCollider` | `ArmySlot` | 精英/Boss 攻击判定帧 |
| Monster `TargetSensor` | `ArmySlot` | 攻击起始范围辅助 |
| Gate `BodyCollider` | `ArmySlot` | Gate 接触 |
| Prop `BodyCollider` | `ArmySlot` | Prop 接触 |

不建议开放 `EnemyAttack` 与 `EnemyBody`、`TargetSensor` 与 `EnemyBody`、Gate 与 Prop、Enemy 与 Gate/Prop、ArmySlot 与 ArmySlot 等物理碰撞关系；这些对象之间没有 MVP 的自动物理结算需求。若后续启用 Kinematic Rigidbody2D 适配，必须保持上述查询方向和状态机仍为权威，并另行记录需要开启的最小矩阵对。

正式定案前应使用项目目标 Unity 版本验证：当 Layer Collision Matrix 关闭对应 Layer 对时，带显式 `ContactFilter2D`/LayerMask 的 Cast 与 Overlap 仍按上表返回目标；若具体 API 受矩阵影响，则只开启该查询所需的最小 Layer 对，不开放自动玩法回调。
