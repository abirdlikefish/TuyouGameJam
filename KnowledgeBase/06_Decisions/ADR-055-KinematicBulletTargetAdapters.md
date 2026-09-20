# ADR-055：子弹目标侧 Kinematic Rigidbody2D 查询适配

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-016、ADR-037、ADR-043、DES-030

## 背景

玩法移动和结算已经定案为自定义时间域驱动的 Transform 位移，以及由 LevelManager 排序的显式 Cast/Overlap 查询。当前 Bullet 和 Monster 阻挡直接调用 `Collider2D.Cast`，但规范 Prefab 都没有 `Rigidbody2D`；在目标 Unity 2022.3 中，该 API 需要查询双方至少存在可参与查询的 Rigidbody2D 适配，否则无刚体 Bullet 无法命中同样无刚体的 Enemy、Gate 或 Prop，Monster Body Cast 也无法形成可靠阻挡。

子弹实例数量可能显著高于 Enemy、Gate 和 Prop 的总量。为每颗子弹增加物理 Body 会扩大活动物理对象数量；改写为逐形状 `Physics2D.CircleCast/BoxCast` 又会扩大当前修复范围，并要求重新维护 Collider 世界形状换算。当前 MVP 优先采用目标侧适配并保留既有查询、排序和去重逻辑。

## 决策

- 所有实现运行时子弹目标契约的规范 Prefab 根节点必须挂载一个 `Rigidbody2D`：三类 Monster、两类 Gate 和 WeaponProp。
- 适配刚体固定为 `Kinematic`、`Simulated = true`、`Use Full Kinematic Contacts = false`、`Gravity Scale = 0`、`Collision Detection = Discrete`、`Interpolate = None`，并冻结 Z 轴旋转。
- Bullet Prefab 不添加 Rigidbody2D。Bullet 继续使用自身 `BodyCollider.Cast`，依靠目标侧 Kinematic Rigidbody2D 返回 Enemy、Gate 和 Prop 候选。
- Monster 根节点的同一 Kinematic Rigidbody2D 同时为自身 BodyCollider Cast 和其他 Monster 的阻挡候选提供查询适配；`ApplyBlockedMovement` 仍是允许位移的唯一权威。
- Kinematic Rigidbody2D 不负责移动、推挤、寻路或伤害结算。对象继续由模块按自定义时间域修改 Transform，LevelManager 在全部位移后只调用一次 `Physics2D.SyncTransforms()`。
- Gameplay Collider 继续保持 Trigger，自动 Layer Collision Matrix 继续关闭；不得新增 `OnTriggerEnter2D`、`OnCollisionEnter2D` 或自动接触结算。
- Monster、Gate 和 Prop 的 Preparing 校验必须确认 BodyCollider 所属 Rigidbody2D 位于玩法根节点，且为已启用模拟的 Kinematic Body。配置不合法时阻止 Gameplay Ready。

## 不采用

- 不只给 Monster 添加刚体，因为这会留下 Bullet 对 Gate/Prop 的同类失效。
- 不给每个 Bullet 添加刚体，避免在高弹量场景中扩大活动物理 Body 数量。
- 不改用 Dynamic Rigidbody2D、速度、力或物理求解器承担移动与阻挡。
- 本轮不把两处 `Collider2D.Cast` 重写为形状特定的静态 Physics2D Cast；若后续性能实测要求移除适配刚体，再单独决策并验证世界形状换算。

## 后果

- 现有 Bullet 命中排序、代理解析、RuntimeInstanceId 去重和 Monster 阻挡截断逻辑保持不变。
- 所有子弹合法目标都增加一个轻量 Kinematic Body；需要在目标设备通过 Profiler 观察 Physics2D Transform 同步和查询成本。
- Prefab 复制或新增子弹目标类型时，缺失或错误配置的 Rigidbody2D 会在 Preparing 阶段直接报错，而不是静默漏判。

## 验收标准

- BulletId 0/1/2 均能命中三类 Monster、两类 Gate 和 WeaponProp，首个合法目标只结算一次并回收子弹。
- 存活 Monster 的 BodyCollider Cast 能检测前方 Monster 并按 `blockingGap` 截断位移。
- Gameplay Layer 自动碰撞矩阵保持关闭；运行中不存在自动推挤、Trigger/Collision 回调伤害或 Rigidbody2D 驱动位移。
- 六个目标 Prefab 根节点的 Rigidbody2D 配置和运行前校验一致，代码编译且 Gameplay Preparing 通过。

