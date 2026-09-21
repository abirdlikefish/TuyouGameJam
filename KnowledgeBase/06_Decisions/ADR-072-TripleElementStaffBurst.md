# ADR-072：三元素法杖五秒动态散射

## 状态

Accepted

## 日期

2026-09-22

## 背景

ADR-057 将火、冰、雷三元素组合固定映射为 WeaponId 9，ADR-070 则让全部法杖共享固定三弹道。现在需要把三元素法杖改为一个限时特殊形态：持续五秒，三弹道夹角动态变化，每颗子弹分别从另外六种元素法杖子弹中随机选择，结束后清空全部元素并恢复普通法杖。

现有 `BulletSpawnRequest` 已能分别携带 `BulletId`、`WeaponId`、`ElementMask` 和方向；因此该玩法不需要扩展公共接口或增加配置字段。项目要求相同输入可复现，随机选择不能依赖 Unity 全局随机状态。

## 决策

1. 当法杖家族首次派生为 WeaponId 9 时启动独立的五秒三元素阶段。阶段使用 LevelManager 传入 Army 的 Gameplay delta，不读取其他时间源；同阶段内再次获得任意元素不重置或延长五秒。
2. 三元素阶段暂停火、冰、雷原有的独立自然过期推进，并向 HUD 报告统一的阶段剩余时间，保证 WeaponId 9 完整持续五秒。阶段结束时按火、冰、雷顺序把三种元素全部清零并各发布一次 `ArmyElementExpired`，随后以 `ElementExpired` 原因切换到无元素普通法杖 WeaponId 2。
3. ADR-060 的实际换武器规则继续有效：进入 WeaponId 9 时立即在阶段时间零点触发一次齐射；五秒结束切回 WeaponId 2 时立即触发一次普通法杖齐射。阶段结束所在 Tick 不再补发原三元素法杖的周期齐射。
4. 三元素法杖每次齐射仍生成中、左、右三颗独立子弹。中弹方向固定为 `(0,1)`；左右弹各自相对中弹偏转同一个角度，最大分别为 `30°`，因此最大左右总夹角为 `60°`。
5. 五秒角度曲线分为三个相等的线性阶段：`0s～5/3s` 从 `0°` 到 `30°`，`5/3s～10/3s` 从 `30°` 回到 `0°`，`10/3s～5s` 再从 `0°` 到 `30°`。每次齐射按当前阶段已用时间计算方向，不累计旋转误差。
6. 每颗子弹分别独立从 WeaponId 3～8 对应的六个子弹配置中选择，不包含普通法杖 BulletId 2 和三元素法杖 BulletId 9。选择按中、左、右的固定生成顺序推进确定性随机状态；状态由 `LevelRunId` 和本局三元素阶段序号派生，同一运行输入可复现。
7. 随机子弹使用被选中武器配置的 `BulletId`、伤害、速度与动画，同时携带该武器对应的单元素或双元素 `ElementMask`，因而双元素弹继续精确触发现有组合效果。子弹快照中的 `WeaponId` 保持为 9，表达实际发射者仍是三元素法杖。
8. WeaponId 2～8 的发射数量、固定方向、配置、元素语义和攻击周期均不改变。三元素阶段不新增 Luban 字段、Prefab、Animator 状态或公共契约。

## 覆盖关系

- 修订 ADR-057 中三元素按各自剩余时间自然降级的规则：WeaponId 9 改为独立持续五秒并在结束时一次性清空三元素。
- 修订 ADR-061 中“三元素不触发组合效果”的范围：三元素法杖随机生成的子弹按所选 WeaponId 3～8 的精确掩码结算，只有选中三个双元素子弹时触发对应组合。
- 修订 ADR-070 中 WeaponId 9 使用固定 `(-3,13).normalized`、`(3,13).normalized` 方向且同齐射三弹共享同一 BulletId/ElementMask 的规则；WeaponId 2～8 保持原规则。

## 验收标准

- 三元素法杖从进入 WeaponId 9 起完整保持五秒，不会因任一原元素剩余时间较短而提前降级；阶段中再次获得元素不延长时间。
- 起始、五秒三分之一、三分之二和结束边界的左右偏角依次为 `0°、30°、0°、30°`，左右方向镜像且单位化，中弹始终竖直。
- 每颗弹独立选择 WeaponId 3～8 对应 BulletId，长序列中六种结果均可出现；固定 LevelRunId 和相同操作顺序得到相同结果。
- 随机子弹的外观、基础伤害、速度和 ElementMask 与所选法杖一致；来源 WeaponId 保持 9。单元素弹不误触发组合，三个双元素弹分别只触发对应组合。
- 五秒结束时三元素 HUD 同帧归零，只发布三条元素过期事实和一条 `9 -> 2` 武器变化事实；不会经过中间元素法杖身份。
- WeaponId 0～1 仍为单发，WeaponId 2～8 仍为固定方向三发；所有既有冷却、首发、换武器立即发射和大帧不追赶规则保持不变。

## 关联

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Loadout.md`
- `../02_Modules/Bullet/README.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-057-ElementalStaffWeaponVariants.md`
- `ADR-060-ArmyAttackCycleAnimationSynchronization.md`
- `ADR-061-ElementComboImpactEffects.md`
- `ADR-070-StaffThreeProjectileSpread.md`
