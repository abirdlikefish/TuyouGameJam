# ADR-007：WeaponId 作为唯一武器身份

## 状态

Accepted（固定 WeaponId 与初始状态由 ADR-035 补充）

## 日期

2026-09-14

## 决策

- 运行时、Army、武器箱和事件只使用 `WeaponId` 表示具体武器。
- 不把 `WeaponType` 作为第二份可变身份状态。
- MVP 固定 `0 = Slingshot`、`1 = Bow`、`2 = Staff`，三者使用不同的 `WeaponId` 和配置行；`0` 是合法身份而不是空值。
- 如果表现层需要分类，使用配置数据派生分类，不参与装备身份判断。
- 武器箱通过 `WeaponId` 更新 Army，并保留 Army 当前火、冰、雷三种元素的剩余持续时间。

## 影响

- `TbWeapon.Id` 是武器稳定身份和跨表引用键。
- `ArmyWeaponChanged` 传递旧、新 `WeaponId`，不再依赖 `WeaponType` 判断装备变化。
- `../02_Modules/Army/Loadout.md` 不再把 `Slingshot`、`Bow`、`Staff` 作为唯一武器枚举。
