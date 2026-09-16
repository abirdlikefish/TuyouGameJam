# ADR-007：WeaponId 作为唯一武器身份

## 状态

Accepted

## 日期

2026-09-14

## 决策

- 运行时、Army、武器箱和事件只使用 `WeaponId` 表示具体武器。
- 不把 `WeaponType` 作为第二份可变身份状态。
- 不同参数的弓、法杖或弹弓均视为不同武器，使用不同的 `WeaponId` 和配置行。
- 如果表现层需要分类，使用配置数据派生分类，不参与装备身份判断。
- 武器箱通过 `WeaponId` 更新 Army，并保留 Army 当前的 `ElementId`。

## 影响

- `TbWeapon.Id` 是武器稳定身份和跨表引用键。
- `ArmyLoadoutChanged` 传递旧、新 `WeaponId`，不再依赖 `WeaponType` 判断装备变化。
- `../02_Modules/Army/Loadout.md` 不再把 `Slingshot`、`Bow`、`Staff` 作为唯一武器枚举。
