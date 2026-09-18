# Bullet 子弹模块

## 模块信息

- ID：`MOD-BULLET`
- 层级：Gameplay
- 状态：`Planned`
- 依赖：TimeService、PoolService、IDamageable、IBulletDamageable
- 决策：`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`

## 职责

- 创建、移动和回收子弹。
- 消费 Army 槽位提供的运行时发射快照，使用 Weapon 引用的基础子弹参数并携带当前 Element 身份。
- 与怪物、Gate 和 Prop 碰撞并造成伤害或触发对应效果。
- 每个运行时子弹都绑定 `BodyCollider`；移动使用自定义时间，沿上一位置到期望位置执行 Collider Cast 或等价扫掠查询。
- 子弹通过目标的受击碰撞体命中；一次命中后立即标记并回收，避免多个子碰撞体重复结算。
- `BulletId` 表示配置，运行时去重使用唯一的 `BulletInstanceId`。

## 配置输入

- 从 Luban `TbBullet` 读取基础伤害和速度。
- 从 `TbWeapon` 获取发射间隔和基础子弹引用，从 `TbElement` 获取元素身份与类型。
- MVP 子弹只有配置与表现资源差异，共用一个 `Bullet` 池化根脚本和一个规范 Prefab；子弹生成所有者通过 Inspector 绑定该 Prefab，并按 `BulletId` 注入数值与表现。命中首个有效目标后回收是固定规则，不配置资源键或碰撞行为。
- Army 每个激活槽位提供一个发射点和代表人数；Bullet 不读取 Army 内部状态，只消费生成时的不可变快照。
- 子弹的发射时机由 Army 运行时逻辑决定，关卡编排不保存子弹实例。

## 规则

- 子弹命中后立即回收。
- 子弹离开屏幕时回收。
- 移动使用 Bullet 时间域。
- 不依赖 `OnTriggerEnter2D` 或 `OnCollisionEnter2D` 作为命中唯一入口；查询使用 Bullet Layer 到 Enemy/Gate/Prop 受击 Layer 的明确过滤。

## 运行时快照

最少包含：`SourceArmyId`、`SourceSlotIndex`、`BulletInstanceId`、`BulletId`、`WeaponId`、`ElementId`、最终伤害、速度和方向。规范 Prefab 引用由子弹生成所有者的 Inspector 提供，不作为玩法快照字段。快照生成后不随 Army 人数变化，避免飞行中的子弹属性突然改变。命中目标时从快照生成 `BulletDamageContext`，不新增独立 `DamageType`。

## 测试标准

- 每个激活槽位都能从自己的发射点生成子弹，空槽位不会生成。
- Weapon 和 Element 的组合按固定顺序计算，最终属性可复现。
- 改变代表人数不会改变单次发射数量、伤害或速度；发射源数量只取决于激活槽位数。
- 子弹命中目标后只结算一次伤害并回收。
- 子弹 Collider Cast 能覆盖整段移动位移，高速或掉帧时不穿透敌人、Gate 或 Prop。
- 子弹与怪物、Gate 或 Prop 的受击碰撞体碰撞时只生成一次伤害上下文并回收；攻击碰撞体不作为子弹目标。
- 同一个 `BulletInstanceId` 不会因多个目标子碰撞体重复结算。
- `Bullet` 具体类型只绑定一个规范 Prefab 和类型池；类型池返回未激活实例，生成所有者设置发射父节点、Transform 和快照并登记后才激活。
- 子弹只请求生成所有者结束实例，不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还；命中或离屏后清理状态、主动失活，再由类型池防御性失活并回收。
