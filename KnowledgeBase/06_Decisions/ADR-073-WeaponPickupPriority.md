# ADR-073：武器箱拾取优先级

## 状态

Accepted

## 日期

2026-09-22

## 背景

当前武器箱在击破后直接把配置的 `WeaponId` 提交给 Army，因此弹弓箱、弓箭箱和法杖箱都可以无条件替换当前武器。玩法现在要求武器只允许保持或向更高家族升级，避免玩家获得法杖或弓后被后续低级武器箱降级。

WeaponId 2～9 包含普通法杖和全部元素法杖。它们是同一个法杖家族，不能按 WeaponId 数值大小推导拾取优先级。

## 决策

1. 武器箱拾取优先级固定为 `Staff > Bow > Slingshot`：弹弓 WeaponId 0 为最低级，弓 WeaponId 1 为中级，法杖家族 WeaponId 2～9 为最高级。
2. `ArmyController.ApplyWeaponPickup` 在确认拾取 WeaponId 存在配置后比较拾取武器与当前武器的家族优先级。拾取优先级较低时忽略换武器命令；相同或更高时继续既有切换流程。
3. 法杖箱仍提交 WeaponId 2，并在通过优先级判断后按当前有效元素解析为 WeaponId 2～9。同级法杖拾取不引入元素法杖之间的独立优先级。
4. 低优先级拾取不改变当前 WeaponId、元素计时、三元素阶段、Animator、攻击冷却或发射状态，不发布 `ArmyWeaponChanged`，也不生成换武器立即齐射。
5. WeaponProp 仍完成成功击破、发布 `PropBroken` 并回收。奖励粒子继续由成功击破事实驱动；本决策不把低优先级箱子改成击破失败。
6. 元素获得、元素过期和三元素阶段结束继续通过既有法杖派生逻辑切换，不受武器箱优先级约束。公共接口、Luban 配置、Prefab 和场景装配均不改变。

## 验收标准

- 当前为弹弓时，弹弓箱保持不变，弓箱升级为弓，法杖箱升级为当前元素组合对应法杖。
- 当前为弓时，弹弓箱不降级，弓箱保持不变，法杖箱升级为当前元素组合对应法杖。
- 当前为任意 WeaponId 2～9 时，弹弓箱和弓箱均不降级；法杖箱继续按现有元素派生规则处理。
- 被忽略的低优先级拾取不发布 `ArmyWeaponChanged`，不重播武器动画、不重置冷却、不立即发射，也不中断 WeaponId 9 的五秒阶段。
- 低优先级武器箱仍只发布一次成功 `PropBroken`，并保持既有回收和奖励表现。

## 关联

- `../02_Modules/Army/README.md`
- `../02_Modules/Army/Loadout.md`
- `../02_Modules/Prop/README.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-057-ElementalStaffWeaponVariants.md`
- `ADR-060-ArmyAttackCycleAnimationSynchronization.md`
- `ADR-072-TripleElementStaffBurst.md`
