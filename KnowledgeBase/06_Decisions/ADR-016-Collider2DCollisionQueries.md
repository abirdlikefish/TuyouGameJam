# ADR-016：统一使用 Collider2D 与显式碰撞查询

## 状态

Accepted

> ADR-027 将暂停、减速和局部时停移出当前 MVP；本文涉及这些能力的碰撞规则只作为未来重新设计的输入，不属于当前验收。

> ADR-037 进一步移除 Monster `TargetSensor`，明确 Enemy `BodyCollider` 与 Army `SlotCollider` 不进行移动碰撞，并定案 Gameplay Layer 与自动碰撞矩阵；本文其余显式查询与敌人间阻挡规则继续有效。

> ADR-046 将敌人间阻挡收窄为对上一轮同步姿态的离散 Cast：目标 MVP 参数下尽量减少穿透和重叠，但不再保证同帧绝对不重叠，也不执行事后分离。

> ADR-055 将本 ADR 预留的 Kinematic 适配具体化为目标侧规则：Monster、Gate 和 Prop 根节点提供查询适配刚体，Bullet 保持无刚体；移动、阻挡和结算所有权不变。

## 日期

2026-09-14

## 背景

项目同时存在子弹命中敌人、Gate 和 Prop，敌人范围攻击 Army 槽位，Gate/Prop 接触 Army，以及敌人身体互相阻挡等碰撞关系。项目还需要支持全局和局部的暂停、减速、加速，不能让核心玩法结算依赖 Unity 全局物理时间。

完全自建几何碰撞会重复实现形状、Layer 过滤和空间查询；完全依赖 Dynamic Rigidbody2D 和自动碰撞回调则不利于局部时间控制和一次性结算。

## 决策

- 所有参与玩法命中、接触、受击或阻挡的运行时对象都使用 `Collider2D` 表达碰撞形状，包括子弹、敌人、Enemy `AttackCollider`、Army 槽位、Gate 和 Prop。
- 玩法移动继续由各模块使用 `TimeService` 提供的有效 delta 驱动，不使用 Dynamic Rigidbody2D 的力、速度或碰撞响应作为权威移动结果。
- 核心碰撞检测使用显式 `Collider2D.Cast`、`Physics2D.Raycast/CircleCast`、`OverlapCollider`、`OverlapBox` 或 `OverlapCircle` 查询；`OnTriggerEnter2D`、`OnCollisionEnter2D` 等自动回调不作为核心规则的唯一触发来源。
- 查询必须使用明确的 Layer/`ContactFilter2D`，只命中目标职责对应的 Collider。命中、接触、攻击和回收继续通过运行时实例 ID、攻击序号或槽位索引保证幂等。
- 子弹从上一次逻辑位置向期望位置执行扫掠查询，命中首个有效目标后只结算一次并回收，避免加速或掉帧导致穿透。
- 敌人身体使用 `BodyCollider` 参与子弹受击和敌人间阻挡。存活的较慢、静止或局部时停敌人仍是阻挡体；后方敌人将位移截断在安全间距并等待，不推动或穿过前方敌人。
- 敌人进入 `Dead` 后立即退出玩法阻挡和受击查询；死亡动画仅属于表现和延迟回收，不继续阻挡后方敌人。
- 敌人范围攻击在 Animator 判定帧使用 `AttackCollider` 执行一次显式重叠查询，对每个有效 Army 槽位最多结算一次，然后立即结束本次攻击查询。
- Gate/Prop 与 Army 的接触使用显式查询；查询方式已由 ADR-043 收窄为移动终点的一次 `OverlapCollider`，不再使用接触 Cast。对象自身的接触状态机仍是是否允许结算的权威来源。
- 道路左右边界继续使用 Level 提供的数值边界，不要求用 Collider2D 表达。
- `Rigidbody2D` 不进入模块间接口或玩法状态；ADR-055 要求 Enemy、Gate 和 Prop 规范 Prefab 根节点提供固定配置的 Kinematic Rigidbody2D 作为 Unity 2022.3 `Collider2D.Cast` 查询适配，但不得改变自定义时间、显式查询和玩法状态机的权威性。

## MVP 范围

- 首版使用上一同步姿态的 BodyCollider Cast 让后方敌人尽量排队；在约定速度、尺寸和帧率下减少明显穿透与重叠，但不把同帧绝对不重叠作为验收不变量。局部时停下的行为由未来决策确认。
- “后方敌人从侧面寻找路径并绕过较慢或静止敌人”是后续非必要需求，首版不实现横向绕行、通道预留、局部导航或人群避让。
- 当前不以移动 Collider2D 的 broadphase 或 Transform 同步成本作为设计阻塞，也不为此提前实现纯几何碰撞或自定义空间索引；进入性能阶段后依据目标设备实测结果再评估。

## 影响

- 各 Prefab 必须在 Inspector 中配置职责明确的 Collider2D 和 Layer，运行时不得通过 `AddComponent` 静默补齐。
- Bullet、Monster、Gate、Prop 和 Army 模块需要分别定义查询时机、目标 Layer、命中顺序和去重规则。
- 局部时间系数为 0 只停止对象主动移动、攻击计时和动画，不自动使其失去碰撞形状。是否可受击由对象玩法状态决定。
- 需要新增碰撞矩阵、扫掠命中、范围攻击、接触去重和敌人阻挡测试；局部时停下的碰撞测试延后。

## 关联文档

- `../01_Architecture/SystemOverview.md`
- `../01_Architecture/TimeSystem.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/CollisionRules.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-037-MonsterDistanceTargetingAndCollisionLayers.md`

