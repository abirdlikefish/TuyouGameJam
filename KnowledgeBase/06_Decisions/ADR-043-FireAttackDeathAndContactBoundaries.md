# ADR-043：射击、攻击、死亡动画与接触边界

> ADR-071 已将子弹回收线从道路派生的 `TopBoundary` 改为关卡配置的 `BulletDespawnY`；根节点中心、严格大于和当帧回池语义保持不变。

> ADR-070 已将本文“每槽每逻辑帧最多生成一颗子弹”修订为“每槽每逻辑帧最多触发一次齐射”：法杖家族一次齐射生成三颗，其他武器仍生成一颗；冷却、大帧不追赶和结算边界继续有效。

> ADR-060 修订 Army 发射起点：本局初始活动槽位和实际换武器后的活动槽位在攻击周期第 0 帧立即发射；运行中新激活槽位仍等待完整间隔。

- 状态：Accepted
- 日期：2026-09-20
- 关联：DES-020、DES-024、DES-040、DES-043、DES-049、ADR-033、ADR-037、ADR-040

## 背景

首轮工程实现前仍缺少若干可直接编码的时间与空间语义：新槽位何时首发、换武器如何处理射击冷却、怪物攻击冷却从何时计算、死亡动画如何触发回收、Gate/Prop 是否需要扫掠检测，以及离场阈值按对象何处判定。

## 决策

### Army 槽位射击

1. 每个槽位独立持有 `fireCooldownRemaining`。
2. 槽位在本局开始时已激活，或运行中从未激活变为激活时，计时器初始化为当前武器完整的 `FireInterval`；必须等待该时长后才可首发。
3. 只有实际切换到不同 `WeaponId` 时，所有当前激活槽位的计时器才重置为新武器完整的 `FireInterval`。重复获得当前武器不重置。
4. 每次 `TickMovementAndFire` 先用 Gameplay delta 将计时器扣至不小于零；到期且槽位仍激活时最多生成一颗子弹，随后把计时器重置为当前完整 `FireInterval`。
5. 不补发历史欠下的子弹，不在单个逻辑帧循环生成多颗。当前武器射速与目标帧率保证该简化满足 MVP。

### 怪物攻击与死亡动画

1. `AttackCooldown` 从一次攻击开始的逻辑时刻计算，而不是从命中帧或动画结束开始。
2. 攻击动画播放期间冷却继续使用 `Monster` 时间域 delta 递减。
3. `OnAttackAnimationFinished()` 只结束当前攻击动画状态，不直接开始下一次攻击。
4. 若动画结束时冷却已到，在下一次 `EnemyManager.TickMovement` 中重新验证怪物存活、目标有效且仍在攻击范围内；条件成立则立即开始下一次攻击，不附加额外等待。
5. 若动画结束时冷却未到且目标仍在范围内，怪物保持当前位置等待计时；目标失效或离开范围时则重新索敌或继续接近。
6. Monster 的非循环 Death Clip 在末帧调用本地 `AnimationEvent` 方法 `OnDeathAnimationFinished()`。该方法只向 `EnemyManager` 登记回收请求；真正归还对象池仍由 Manager 在既定回收阶段完成。
7. HP 归零时立即完成死亡计数并发布一次 `MonsterKilled`；死亡动画完成不是新的 EventBus 事实事件。重复、过期或 `StopRun` 后到达的动画事件必须被忽略。
8. `StopRun` 不等待死亡动画，直接清理本局实例。

### Gate/Prop 接触与空间阈值

1. Gate/Prop 在本帧完成位移并同步 Physics2D Transform 后，只对终点姿态执行一次 `OverlapCollider` 接触查询；不做 `Cast`、扫掠检测或路径补样。
2. 通过合理移动速度、Collider 尺寸和目标帧率避免穿透；该风险进入手动与集成验证，不增加补偿框架。
3. `SpawnY`、`EnemyApproachY`、`DespawnY` 等纵向阈值均以池化实例根 GameObject 的 `transform.position`（对象中心）判定，不使用 Collider 边缘或 Renderer Bounds。
4. 子弹根 GameObject 中心满足 `position.y > RoadLayoutSnapshot.TopBoundary` 时回收。
5. Army 横向移动边界仍以全部激活槽位 Collider 的合并 AABB 计算，这是明确例外。

## 后果

- 射击节奏和武器切换可用单计时器规则实现，无需追赶循环。
- 攻击动画事件只登记边界事实，攻击开始与伤害结算仍由逻辑帧阶段决定。
- 死亡表现可以自然结束后再回收，同时不改变胜利计数时机。
- Gate/Prop 接触为离散终点采样；Prefab 尺寸和速度需要在工程验证中调参。
- 所有离场线可直接与根 Transform 比较，避免不同 Collider 尺寸造成语义漂移。

## 验收标准

- 新激活槽位和实际换武器后，在一个完整 `FireInterval` 内不发射。
- 超长单帧最多使每个激活槽位发射一颗，不补发多颗。
- 从攻击开始到动画结束期间冷却可归零；动画结束后的下一逻辑帧满足条件即可再次攻击。
- Death Clip 末帧只登记一次回收；死亡计数和 `MonsterKilled` 不因动画事件重复。
- Gate/Prop 仅用终点 `OverlapCollider`，文档与测试不再要求移动 Cast。
- Gate/Prop 的 `DespawnY` 和子弹的 `TopBoundary` 均按根 GameObject 中心验证。
