# Bullet 子弹模块

## 模块信息

- ID：`MOD-BULLET`
- 层级：Gameplay
- 状态：`InProgress`（批次 4 玩法脚本与批次 7.4 Animator/池复用适配代码已实现并通过编译；Prefab 字段、Layer 与命中流程手测待完成）
- 依赖：IBulletConfigProvider、PoolService、IBulletHittable、Level
- 决策：`../../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`、`../../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`、`../../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`、`../../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`、`../../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md`

## 职责

- `BulletManager` 创建、登记、逐帧驱动和回收子弹。
- 消费 Army 槽位提供的运行时发射快照，使用 Weapon 引用的基础子弹参数并携带发射瞬间的 ElementMask。
- 与怪物、Gate 和 Prop 碰撞并造成伤害或触发对应效果。
- 每个运行时子弹都绑定 `BodyCollider`；移动使用自定义时间，沿上一位置到期望位置执行 Collider Cast 或等价扫掠查询。
- 子弹通过目标的受击碰撞体命中；一次命中后立即标记并回收，避免多个子碰撞体重复结算。
- `BulletId` 表示配置，运行时去重使用唯一的 `BulletInstanceId`。

## 配置输入

- 通过 `IBulletConfigProvider.GetBulletConfig(BulletId)` 取得 ConfigService 已校验并复制的 `BulletConfigSnapshot`，包含基础伤害和速度；BulletManager 不持有 Luban 生成行。
- 从 `TbWeapon` 获取发射间隔和基础子弹引用。当前不建立 `TbElement`；火、冰、雷只通过 Army 提交的 ElementMask 记录，具体效果延后设计。
- MVP 子弹只有配置与表现资源差异，共用一个 `Bullet` 池化根脚本和一个规范 Prefab；BulletManager 通过 Inspector 绑定该 Prefab，并按 `BulletId` 注入数值与表现。命中首个有效目标后回收是固定规则，不配置资源键或碰撞行为。
- Army 每个激活槽位提供一个发射点和代表人数；Bullet 不读取 Army 内部状态，只消费生成时的不可变快照。
- 子弹的发射时机由 Army 运行时逻辑决定，关卡编排不保存子弹实例。

## 规则

- 子弹命中后立即回收。
- 子弹根 GameObject 中心的世界坐标满足 `position.y > RoadLayoutSnapshot.TopBoundary`（`roadBounds.yMax`）时回收；不使用 Renderer、Collider 或摄像机视口边缘。
- 命中 Collider 节点必须存在已在 Preparing 验证的同节点 `BulletHitProxy`；代理显式绑定实现 `IBulletHittable` 的 Enemy/Gate/Prop 根组件，并提供对象类别和 RuntimeInstanceId。候选还必须满足 `CanReceiveBulletHit = true`；BulletManager 不通过父级搜索或 HP 推断目标。
- HP 已归零但仍处于 `Pending` 的元素门保持 `CanReceiveBulletHit = true`，命中后子弹照常消费，伤害交由 Gate 累计为可兑换额外伤害。Failed 元素门和 Prop 仍保持可命中，子弹照常消费，但目标 HP 最低锁在 `1`，不再产生奖励、击破或伤害回收。
- 移动使用 LevelManager 在帧开始读取并传入的 Bullet 时间域 delta；Bullet 和 BulletManager 不自行再次读取 TimeService。
- 不依赖 `OnTriggerEnter2D` 或 `OnCollisionEnter2D` 作为命中唯一入口；查询使用 Bullet Layer 到 Enemy/Gate/Prop 受击 Layer 的明确过滤。
- 首轮只对 Bullet 自身从上一逻辑位置到期望位置执行扫掠，不计算与本帧同时移动目标的相对运动，也不做子步进。通过 MVP 配置的合理速度、Collider 尺寸和目标帧率避免穿透，不承诺任意高速或严重掉帧场景。

## 运行时快照

最少包含：`SourceArmyId`、`SourceSlotIndex`、`BulletInstanceId`、`BulletId`、`WeaponId`、`ActiveElements`、最终伤害、速度和方向。规范 Prefab 引用由 BulletManager 的 Inspector 提供，不作为玩法快照字段。快照生成后不随 Army 人数、武器或元素计时变化。命中目标时从快照生成 `BulletDamageContext`，不新增独立 `DamageType`。

## BulletManager 与数据来源

- BulletManager 通过 Inspector 序列化引用唯一的 `Bullet` 规范 Prefab，并以具体根类型向 PoolService 取得类型池；Luban 不保存 PrefabKey。
- ArmyController 在发射瞬间提交 `BulletSpawnRequest`，其中包含 LevelRunId、来源 Army/槽位、BulletId、WeaponId、ActiveElements、世界位置和方向。BulletManager 不复制或查询 Army 当前装备状态。
- BulletManager 通过注入的 `IBulletConfigProvider` 按 BulletId 必得 `BulletConfigSnapshot`，并与请求组合成本次不可变运行时快照。配置 ID 已在 ConfigService 启动时通过引用校验；BulletManager 不编写 `TryGet`、默认值或恢复分支，也不得直接访问静态 Luban Tables。
- MVP ElementMask 只保存 Fire、Ice、Lightning 在发射瞬间是否有效，不修改 TbBullet 基础伤害。未来增加元素效果时，应由独立纯计算规则基于该不可变掩码生成结果。
- BulletManager 实现 `StartRun`、`Spawn`、`TickMovementAndHits`、`FlushPendingRecycles` 和 `StopRun`；核心逻辑不使用独立 Update。

## Prefab 与场景装配

```text
BulletRoot [BulletManager；序列化唯一 Bullet Prefab]
└── PF_Bullet [Bullet；Animator；运行时池实例]
    ├── Visual [SpriteRenderer 或占位视觉]
    └── BodyCollider [Collider2D；Bullet Layer]
```

Bullet 根组件显式绑定 `bodyCollider`、视觉引用和 Animator。首轮所有 BulletId 共用该 Prefab，数值从 Bullet 配置快照注入；Controller 通过整数 `BulletId` 参数选择 0/1/2 对应循环 Clip。池对象借出时必须在激活前写入本次 ID 并从目标状态起播，归还时清除表现状态；不通过动画事件处理命中、伤害或回收。Trail 和 VFX 仍可延后。必需引用、Controller、参数或 Layer 非法时直接输出错误并阻止 Gameplay Ready。完整导入与绑定见 [AnimationPipeline](../../04_Assets/AnimationPipeline.md) 和 [PrefabSpecifications](../../04_Assets/PrefabSpecifications.md)。

## 测试标准

- 每个激活槽位都能从自己的发射点生成子弹，空槽位不会生成。
- Bullet Prefab、TbBullet 基础数值和 Army 发射快照的来源互不混淆；BulletManager 不持有第二份 Army 装备状态。
- WeaponId 和 ElementMask 的组合按固定顺序计算，最终快照可复现。
- ElementMask 能表达 None 和三元素的全部组合；Army 后续元素获得或过期不修改飞行中的子弹。
- 改变代表人数不会改变单次发射数量、伤害或速度；发射源数量只取决于激活槽位数。
- 子弹命中目标后只结算一次伤害并回收。
- 子弹中心等于 TopBoundary 时仍保留，严格大于 `roadBounds.yMax` 后回收；不同 Collider 或 Sprite 尺寸不改变阈值。
- 加法门没有 HP 仍可合法消费子弹并按实际 `BulletDamageContext.Damage` 增加门值；零 HP、待接触的元素门也仍可合法消费子弹并累计额外伤害。
- 子弹 Collider Cast 覆盖 Bullet 自身在一个逻辑帧内从上一位置到期望位置的位移；在 MVP 约定速度、Collider 尺寸和测试帧率范围内可以稳定命中目标。首轮不验收双方高速相对运动或严重掉帧下的绝对不穿透。
- 子弹与怪物、Gate 或 Prop 的受击碰撞体碰撞时只生成一次伤害上下文并回收；攻击碰撞体不作为子弹目标。
- 同一个 `BulletInstanceId` 不会因多个目标子碰撞体重复结算。
- 同一 Cast 结果先通过 BulletHitProxy 按 RuntimeInstanceId 去重，再按距离、Enemy > Gate > Prop、RuntimeInstanceId 的稳定优先级选择首个有效目标。
- `Bullet` 具体类型只绑定一个规范 Prefab 和类型池；类型池返回未激活实例，BulletManager 设置发射父节点、Transform 和快照并登记后才激活。
- BulletId 0/1/2 分别播放正确的循环 Clip；池化实例从一个 BulletId 回收后以另一个 BulletId 借出时，不残留旧参数、状态、帧或 Sprite。
- 子弹只请求生成所有者结束实例，不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还；命中或离屏后清理状态、主动失活，再由类型池防御性失活并回收。
- LevelManager 只在子弹阶段调用一次 `TickMovementAndHits`；Preparing、Completed 和过期 LevelRunId 不移动或命中。
