# Bullet 子弹模块

## 模块信息

- ID：`MOD-BULLET`
- 层级：Gameplay
- 状态：`Planned`
- 依赖：ConfigService、PoolService、IBulletHittable、Level
- 决策：`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`

## 职责

- `BulletManager` 创建、登记、逐帧驱动和回收子弹。
- 消费 Army 槽位提供的运行时发射快照，使用 Weapon 引用的基础子弹参数并携带发射瞬间的 ElementMask。
- 与怪物、Gate 和 Prop 碰撞并造成伤害或触发对应效果。
- 每个运行时子弹都绑定 `BodyCollider`；移动使用自定义时间，沿上一位置到期望位置执行 Collider Cast 或等价扫掠查询。
- 子弹通过目标的受击碰撞体命中；一次命中后立即标记并回收，避免多个子碰撞体重复结算。
- `BulletId` 表示配置，运行时去重使用唯一的 `BulletInstanceId`。

## 配置输入

- 从 Luban `TbBullet` 读取基础伤害和速度。
- 从 `TbWeapon` 获取发射间隔和基础子弹引用。当前不建立 `TbElement`；火、冰、雷只通过 Army 提交的 ElementMask 记录，具体效果延后设计。
- MVP 子弹只有配置与表现资源差异，共用一个 `Bullet` 池化根脚本和一个规范 Prefab；BulletManager 通过 Inspector 绑定该 Prefab，并按 `BulletId` 注入数值与表现。命中首个有效目标后回收是固定规则，不配置资源键或碰撞行为。
- Army 每个激活槽位提供一个发射点和代表人数；Bullet 不读取 Army 内部状态，只消费生成时的不可变快照。
- 子弹的发射时机由 Army 运行时逻辑决定，关卡编排不保存子弹实例。

## 规则

- 子弹命中后立即回收。
- 子弹离开屏幕时回收。
- 命中候选必须实现 `IBulletHittable` 且 `CanReceiveBulletHit = true`。Enemy/Prop 可以同时实现 `IDamageable`，加法门只实现子弹命中契约；BulletManager 不要求所有合法目标都有 HP。
- HP 已归零但仍处于 `Pending` 的元素门保持 `CanReceiveBulletHit = true`，命中后子弹照常消费，伤害交由 Gate 累计为可兑换额外伤害。元素门接触失败后是否继续保留命中表现由 Gate 生命周期决定，但命中不得再累计可兑换伤害。
- 移动使用 LevelManager 在帧开始读取并传入的 Bullet 时间域 delta；Bullet 和 BulletManager 不自行再次读取 TimeService。
- 不依赖 `OnTriggerEnter2D` 或 `OnCollisionEnter2D` 作为命中唯一入口；查询使用 Bullet Layer 到 Enemy/Gate/Prop 受击 Layer 的明确过滤。

## 运行时快照

最少包含：`SourceArmyId`、`SourceSlotIndex`、`BulletInstanceId`、`BulletId`、`WeaponId`、`ActiveElements`、最终伤害、速度和方向。规范 Prefab 引用由 BulletManager 的 Inspector 提供，不作为玩法快照字段。快照生成后不随 Army 人数、武器或元素计时变化。命中目标时从快照生成 `BulletDamageContext`，不新增独立 `DamageType`。

## BulletManager 与数据来源

- BulletManager 通过 Inspector 序列化引用唯一的 `Bullet` 规范 Prefab，并以具体根类型向 PoolService 取得类型池；Luban 不保存 PrefabKey。
- ArmyController 在发射瞬间提交 `BulletSpawnRequest`，其中包含 LevelRunId、来源 Army/槽位、BulletId、WeaponId、ActiveElements、世界位置和方向。BulletManager 不复制或查询 Army 当前装备状态。
- BulletManager 通过注入的 IConfigService 按 BulletId 取得已校验的 `TbBullet` 基础伤害和速度，并与请求组合成本次不可变运行时快照。不得直接访问静态 Luban Tables。
- MVP ElementMask 只保存 Fire、Ice、Lightning 在发射瞬间是否有效，不修改 TbBullet 基础伤害。未来增加元素效果时，应由独立纯计算规则基于该不可变掩码生成结果。
- BulletManager 实现 `StartRun`、`Spawn`、`TickMovementAndHits`、`FlushPendingRecycles` 和 `StopRun`；核心逻辑不使用独立 Update。

## 测试标准

- 每个激活槽位都能从自己的发射点生成子弹，空槽位不会生成。
- Bullet Prefab、TbBullet 基础数值和 Army 发射快照的来源互不混淆；BulletManager 不持有第二份 Army 装备状态。
- WeaponId 和 ElementMask 的组合按固定顺序计算，最终快照可复现。
- ElementMask 能表达 None 和三元素的全部组合；Army 后续元素获得或过期不修改飞行中的子弹。
- 改变代表人数不会改变单次发射数量、伤害或速度；发射源数量只取决于激活槽位数。
- 子弹命中目标后只结算一次伤害并回收。
- 加法门没有 HP 仍可合法消费子弹并按实际 `BulletDamageContext.Damage` 增加门值；零 HP、待接触的元素门也仍可合法消费子弹并累计额外伤害。
- 子弹 Collider Cast 能覆盖整段移动位移，高速或掉帧时不穿透敌人、Gate 或 Prop。
- 子弹与怪物、Gate 或 Prop 的受击碰撞体碰撞时只生成一次伤害上下文并回收；攻击碰撞体不作为子弹目标。
- 同一个 `BulletInstanceId` 不会因多个目标子碰撞体重复结算。
- `Bullet` 具体类型只绑定一个规范 Prefab 和类型池；类型池返回未激活实例，BulletManager 设置发射父节点、Transform 和快照并登记后才激活。
- 子弹只请求生成所有者结束实例，不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还；命中或离屏后清理状态、主动失活，再由类型池防御性失活并回收。
- LevelManager 只在子弹阶段调用一次 `TickMovementAndHits`；Preparing、Completed 和过期 LevelRunId 不移动或命中。
