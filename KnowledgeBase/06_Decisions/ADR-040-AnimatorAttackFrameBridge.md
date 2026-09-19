# ADR-040：Animator 攻击关键帧与确定性结算桥接

## 状态

Accepted

> ADR-043 补充本决策：AttackCooldown 从攻击开始计算并在动画期间递减；动画结束且冷却已到时，只能在下一次 `EnemyManager.TickMovement` 重新验证后起攻。死亡动画末帧回收事件也由 ADR-043 定义。

> ADR-046 补充命中帧语义：普通敌人起攻后，只要锁定槽位仍有效，就不因其移出 `AttackStartRange` 取消本次命中；`ApplySlotDamage` 保持 `void`。`MonsterAttackLanded` 由 EnemyManager 在实际提交伤害后发布，范围攻击每个有效槽位一条。

## 日期

2026-09-19

## 背景

怪物攻击需要由序列帧动画中的具体关键帧决定命中时机，同时项目已经确定由 LevelManager 和 EnemyManager 持有同帧阶段顺序。若 AnimationEvent 直接修改 Army，会绕过 `ResolveAttacks`、会话校验、死亡优先级和同一次攻击去重；若完全使用纯逻辑计时，又无法让实际攻击时机与动画帧对齐。

## 决策

### Animator 是攻击时机来源

- Monster 进入 `Attacking` 时递增本地 `AttackSequenceId`、锁定本次目标上下文并播放非循环 Attack 动画。
- Attack 动画在实际命中帧配置一个 Unity AnimationEvent，调用同一根 GameObject 上 Monster 具体根脚本的无参公开方法 `OnAttackFrame()`。
- 关键帧的位置是“本次攻击请求何时产生”的唯一来源；不再使用独立的纯逻辑前摇计时器模拟命中帧。
- Attack 动画末帧配置另一个 Unity AnimationEvent，调用无参公开方法 `OnAttackAnimationFinished()`，通知 Monster 结束本次 Attacking。该回调不直接起攻；Monster 在下一次 `TickMovement` 重新验证目标与距离，再决定移动、重新索敌或开始下一次攻击。

### AnimationEvent 只登记请求

- `OnAttackFrame()` 不直接调用 `ApplySlotDamage`，也不执行范围 Overlap；它只为当前 `AttackSequenceId` 登记一个待结算攻击请求。
- 同一 `AttackSequenceId` 最多登记一次。重复 AnimationEvent、循环片段误配置或同帧重复回调不得产生第二次伤害。
- Unity AnimationEvent 是 Animator 到 Monster 的本地回调，不是项目 `IEventBus` 事实事件。
- EnemyManager 在 LevelManager 调用 `ResolveAttacks` 时消费请求，重新校验 `LevelRunId`、Monster 存活状态、Attacking 状态、攻击序号和目标有效性，然后执行单体伤害或 AttackCollider 范围查询。
- 如果 AnimationEvent 发生在当帧 `ResolveAttacks` 之后，请求在下一次 `ResolveAttacks` 消费；不得为了强制同 Unity 帧结算而在动画回调中直接伤害 Army。

### 冷却与取消

- `AttackCooldown` 仍表示两次攻击开始之间的最短时间，从播放本次 Attack 动画时开始计时。
- 下一次攻击必须同时满足：当前攻击动画已结束、冷却已到期、Monster 存活、目标有效且仍满足攻击起始范围。
- 若动画结束时冷却已经到期，下一次 `EnemyManager.TickMovement` 满足上述条件即可开始攻击，不增加另一个完整冷却，也不在 AnimationEvent 中递归起攻。
- 目标在命中关键帧前失效时，本次请求不登记或在 `ResolveAttacks` 中作废；Monster 在动画结束后重新索敌。
- Monster 在请求消费前死亡、离开当前会话、停止运行或回池时，待结算请求作废并清除。
- 命中关键帧已经登记但尚未进入 `ResolveAttacks` 时，不发布 `MonsterAttackLanded`；只有实际完成至少一次有效伤害结算后才发布对应事实。

### Prefab 与动画绑定

- `NormalMonster`、`EliteMonster`、`BossMonster` 根 GameObject 同时挂载具体 Monster 根脚本和 Animator，使 AnimationEvent 可以直接解析 `OnAttackFrame()` 与 `OnAttackAnimationFinished()`。
- 三种敌人的 Attack Clip 都必须为非循环，并各自包含恰好一个攻击帧事件和一个结束事件。
- Animator、RuntimeAnimatorController 或 Attack Clip 缺失时属于 Gameplay Preparing 错误：直接记录 Prefab 路径与缺失项并阻止 Ready，不使用纯逻辑计时兜底。
- Normal 的攻击帧请求锁定槽位伤害；Elite/Boss 的攻击帧请求在 `ResolveAttacks` 使用各自 Prefab 的 AttackCollider 查询 ArmySlot。
- Elite/Boss 的 AttackCollider 可以保持启用作为显式查询形状；自动 Layer Collision Matrix 仍关闭，只有消费有效攻击请求时才执行 Overlap 查询，不通过启停 Collider 表达攻击窗口。

## 不采用

- 不在 AnimationEvent 回调中直接修改 Army。
- 不用 EventBus 广播“请求攻击”。
- 不用与动画片段独立的纯逻辑前摇时间替代关键帧。
- 不在缺失 Animator 或 Attack Clip 时切换为另一套攻击实现。

## 影响

- DES-043 关闭。
- 三种 Monster Prefab 的 Animator、非循环 Attack Clip、攻击帧事件和结束事件成为首轮必需绑定。
- EnemyManager 的 `ResolveAttacks` 保持实际伤害的唯一阶段入口。
- 测试需要覆盖事件前死亡、事件后但结算前死亡、重复关键帧、目标失效、范围去重和回池清理。

## 关联文档

- `../02_Modules/Monster/README.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/CollisionRules.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-043-FireAttackDeathAndContactBoundaries.md`
