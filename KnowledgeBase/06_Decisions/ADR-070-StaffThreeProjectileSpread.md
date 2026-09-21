# ADR-070：法杖三弹道散射

> ADR-072 修订 WeaponId 9：三元素法杖仍保持三发，但改用五秒动态偏角和逐弹随机的 WeaponId 3～8 子弹语义；本文固定方向、同 BulletId 和同 ElementMask 规则仅继续适用于 WeaponId 2～8。

## 状态

Accepted

## 日期

2026-09-22

## 背景

Army 当前每个活动槽位在一次攻击周期到期时只生成一颗竖直向上的子弹。玩法现在要求普通法杖与全部元素法杖共享三弹道散射：同一发射点同时产生竖直、上偏左、上偏右三颗子弹，同时保留现有武器射击间隔和子弹速度配置。

`BulletSpawnRequest` 已携带方向，Bullet 也会在初始化时归一化方向并使用 `TbBullet.MoveSpeed` 计算位移，因此该需求不需要增加 Luban 字段或修改公共接口。

## 决策

1. 法杖家族继续固定为 `WeaponId 2..9`。每次法杖攻击周期触发一次齐射，按中、左、右顺序从同一个槽位 `FirePoint` 生成三颗子弹；弹弓与弓 `WeaponId 0..1` 仍保持单发。
2. 三条世界 XY 方向固定为：中间 `(0,1)`、左侧 `(-3,13).normalized`、右侧 `(3,13).normalized`。方向只表达轨迹，不缩放速度。
3. 同一齐射中的三颗子弹使用相同的 BulletId、WeaponId、ElementMask、伤害配置和来源槽位，但各自取得独立 BulletInstanceId，并独立移动、命中、触发元素组合效果和回收。
4. Bullet 根节点的本地默认朝向为向上。初始化时将根节点的世界 `up` 对齐归一化飞行方向，使 Visual 与 BodyCollider 同步旋转；中间弹保持单位旋转，两侧弹约偏转正负 `12.995°`。
5. `FireInterval`、每槽独立冷却、首帧立即发射、实际换武器立即发射以及大帧不追赶补发的规则保持不变。“每槽每逻辑帧最多一颗”修订为“每槽每逻辑帧最多触发一次齐射”；法杖一次齐射固定生成三颗。
6. 散射数量与方向是当前固定玩法规则，不新增 TbWeapon/TbBullet 配置字段，不复制 Bullet Prefab，也不改变 `IBulletManager` 或 `BulletSpawnRequest`。

## 覆盖关系

- 覆盖 ADR-020 中“每个活动槽位独立发射一枚子弹”和“多弹道不属于 MVP”的范围限制；代表人数仍不缩放射速、伤害或弹丸数量。
- 覆盖 ADR-043、ADR-060 中“每槽每逻辑帧最多生成一颗子弹”的数量描述；其攻击周期、冷却余量与动画同步规则继续有效。

## 验收

- WeaponId 0、1 每次射击仍只生成一颗竖直向上的子弹。
- WeaponId 2～9 每次射击从同一 FirePoint 在同一逻辑帧生成三颗子弹，方向分别为 `(0,1)`、`(-3,13).normalized`、`(3,13).normalized`。
- 三颗子弹的速度大小都等于对应 `TbBullet.MoveSpeed`，不会乘以方向向量原始长度。
- 中间弹保持默认向上；左右子弹视觉和碰撞体朝向各自飞行方向，池复用不会残留上一颗子弹的旋转。
- 初始首发、周期发射和实际换武器立即发射均遵循相同单发/三发规则；大帧每槽最多触发一次齐射且保留周期余量。
- 三颗法杖子弹可分别命中和结算；同一颗子弹仍只结算首个有效目标一次。

## 关联

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Loadout.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `ADR-020-MinimalMvpConfigurationSurface.md`
- `ADR-043-FireAttackDeathAndContactBoundaries.md`
- `ADR-057-ElementalStaffWeaponVariants.md`
- `ADR-060-ArmyAttackCycleAnimationSynchronization.md`
