# ADR-068：ikun 篮球远程攻击动画桥接

## 状态

Accepted

## 日期

2026-09-22

## 背景

ADR-064 首轮实现让 ikun 在 `MovingDown` 阶段按固定间隔直接生成篮球。该实现满足了玩法闭环，但生成时机没有对应的起手、释放和收招表现，怪物在生成篮球时也不会停止移动。现在需要把篮球生成改为独立远程攻击动画驱动，同时保留接近 Army 后使用 `AttackCollider` 的范围近战。

## 决策

1. ikun 增加 `RangedAttacking` 运行状态。篮球间隔只在 `MovingDown` 推进；到期时丢弃掉帧造成的周期超额，不追赶补发，重置为完整间隔并进入一次远程攻击。
2. `RangedAttacking` 期间停止主动移动，篮球间隔冻结。远程攻击动画结束后返回 `MovingDown`，从下一次 Monster Tick 开始继续移动和推进完整间隔。
3. 远程攻击使用独立 `RangedAttack` Animator Trigger 和非循环 Clip。篮球离手帧调用 `OnBasketballReleaseFrame()`，末帧调用 `OnRangedAttackAnimationFinished()`。
4. `OnBasketballReleaseFrame()` 只按本次远程攻击序号登记一次待生成请求并冻结 SpawnPoint 世界坐标，不直接调用 `IBasketballSpawner`。`EnemyManager.ResolveAttacks()` 统一消费有效请求，再执行现有篮球同步生成命令。
5. 请求消费前若 ikun 死亡、StopRun、离开当前会话或回池，请求作废。成功生成的篮球继续保持 ADR-064 的独立生命周期，不因 ikun 后续死亡回收。
6. 到达 `EnemyApproachY` 后仍永久停止远程攻击；随后只按既有 `Attacking`、`OnAttackFrame()` 和 `AttackCollider` 执行范围近战。远程攻击事件不登记近战伤害，近战事件也不生成篮球。
7. 对象池复用必须清除远程攻击序号、待生成请求、释放帧去重标记、倒计时和 `RangedAttack` Trigger。
8. 远程动画素材目录预建为 `Assets/Art/Sprites/Monsters/Ikun/RangedAttack`。正式 PNG、Clip 和 Animator 状态由后续资源装配完成；缺少 `RangedAttack` Trigger 时 ikun Prefab 校验失败并阻止 Gameplay Ready，不使用直接生成篮球或纯计时动画作为兜底。

## 实施状态

2026-09-22 已完成 Attack 6 帧与 RangedAttack 12 帧导入、正式 Clip、公共 `RangedAttack` Trigger/状态/Transition、Ikun AOC 覆盖和实例化 Prefab 校验。按资源制作分工，`OnBasketballReleaseFrame()` 与 `OnRangedAttackAnimationFinished()` 的关键帧位置仍由用户在 Animation 窗口手工设置；在这两个事件完成前，不把远程攻击运行时闭环标记为已验收。

## 不采用

- 不在 AnimationEvent 中直接生成篮球，避免绕过会话、死亡和同帧阶段校验。
- 不让远程攻击复用近战 `Attack` Trigger、Clip 或 `OnAttackFrame()`，避免范围伤害与篮球生成耦合。
- 不在严重掉帧后连续补放多次远程攻击。
- 不改变篮球自身 HP、移动、击破、接触或胜利条件。

## 验收

- 第一轮与后续每轮均先等待完整间隔；到期当帧进入远程攻击且不再移动。
- 释放事件前没有篮球；每次远程攻击最多生成一颗，重复事件不能重复生成。
- 释放前死亡不生成篮球；成功生成后 ikun 死亡不销毁篮球。
- 远程动画结束后的下一次 Tick 恢复移动；动画期间不推进下一轮间隔。
- 到达接近线后不再远程攻击，只使用原有范围近战。
- 回池复借不残留远程状态、请求、倒计时或 Animator Trigger。

## 关联文档

- `ADR-040-AnimatorAttackFrameBridge.md`
- `ADR-064-IkunBasketballEnemyAbility.md`
- `../02_Modules/Monster/README.md`
- `../04_Assets/AnimationPipeline.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
