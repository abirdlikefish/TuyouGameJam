# ADR-060：Army 攻击周期与战斗动画同步

> ADR-070 已将本文“每槽每逻辑帧最多发射一颗”修订为“每槽每逻辑帧最多触发一次齐射”：法杖家族一次齐射生成三颗，其他武器仍生成一颗；攻击周期、动画相位和大帧不追赶规则继续有效。

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-043、ADR-049、ADR-057

## 背景

Army 当前以每槽位 `FireInterval` 冷却调度子弹，但 Attack、MoveLeft、MoveRight 状态切换统一从第 0 帧重播，开局与实际换武器还会等待完整间隔后才发射。正式表现要求子弹在战斗动画第 1 帧发射，并在静止、左移、右移三种持续战斗姿态之间保持已经播放的攻击周期进度。

## 决策

1. `TbWeapon.FireInterval` 是 Army 攻击周期的唯一玩法来源；运行时代码不读取 AnimationClip 时长决定射击。正式 Attack、MoveLeft、MoveRight Clip 由资源侧调整为对应武器攻击周期的相同时长。
2. 本局初始活动槽位在首个 Playing Tick 从当前战斗状态第 0 帧开始播放并立即发射一颗子弹。运行中新激活或重新激活的槽位仍从第 0 帧开始，但保持既有规则，等待一个完整周期后首发。
3. 每槽位继续保存独立攻击周期。Attack、MoveLeft、MoveRight 之间切换时，以当前周期中已经经过的时间换算 normalized time，并从相同进度播放新状态；移动切换不刷新攻击、不额外发射。
4. 周期越界时每槽位每逻辑帧最多发射一颗。大帧不追赶补发，但必须保留越过周期边界的余量并取模推进相位，不得每次简单重置完整间隔造成长期漂移。
5. 任意原因导致实际 WeaponId 改变时，包括武器箱、元素激活和元素过期，全部活动槽位应用新 Controller、从当前战斗状态第 0 帧重播并立即使用新武器发射；随后进入新 `FireInterval` 周期。隐藏槽位只更新 Controller 和目标状态，不发射。重复相同 WeaponId 仍为 no-op。
6. Army Animator 使用与 Gameplay delta 相同的未缩放时间域。动画没有 AnimationEvent，仍不直接决定 BulletId、伤害或命中。
7. 武器箱可能在 BulletManager 遍历活动子弹时同步触发换武器。该次刷新生成的新子弹不参与当前遍历，从下一逻辑帧开始移动，避免同帧连锁命中。

## 后果

- 发射仍是 ArmyController 的权威玩法逻辑，Animator 只消费同一攻击周期的表现相位。
- 左右移动和道路限位引起的状态变化不再造成战斗动画回到第 1 帧。
- 实际换武器成为明确的攻击刷新边界；相同武器拾取不刷新。
- ADR-043 中“初始活动槽位和实际换武器后等待完整间隔”的部分由本决策修订；运行中新激活槽位等待完整间隔的规则保持不变。

## 验收标准

- 正式进入 Playing 时，活动槽位的第一颗子弹与战斗动画第 1 帧同一逻辑帧产生。
- Attack、MoveLeft、MoveRight 任意切换后保持切换前的周期相位，且不改变下次发射边界。
- 实际换武器时动画归零并立即生成新 WeaponId/BulletId 的子弹；重复相同 WeaponId 不重播也不发射。
- 大帧最多生成一颗周期弹，后续动画与发射相位不因丢弃余量持续漂移。
- 换武器命中回调中新生成的子弹不会在 BulletManager 当前 Tick 内移动或再次命中。

## 关联文档

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Loadout.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../04_Assets/AnimationPipeline.md`
- `../05_Testing/IntegrationTests.md`
