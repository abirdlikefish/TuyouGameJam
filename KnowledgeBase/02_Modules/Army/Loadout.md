# Army Loadout 武器与元素组合

## 组成

Army 同时持有一个 `WeaponId` 和一个 `ElementId`。二者是独立配置维度，并作用于全军所有激活槽位：

- `WeaponId` 唯一确定一条 `TbWeapon` 配置，决定发射间隔和基础子弹配置。
- `ElementId` 决定当前元素身份和表现分类；MVP 不配置元素伤害倍率或状态效果。
- Bullet 在生成时组合两者，形成不可变的运行时子弹快照。

不同参数的弓、法杖或弹弓都视为不同武器，必须使用不同的 `WeaponId` 和配置行。运行时不维护第二份 `WeaponType` 武器身份；表现层如需分类，应由 `WeaponId` 查询配置派生。

## 运行时切换

- 武器箱通过 `WeaponId` 更新 Army 的当前武器。
- 更新武器时保留当前 `ElementId`。
- Army 发布 `ArmyLoadoutChanged`，事件携带旧、新 `WeaponId` 和当前 `ElementId`。
- 元素门通过 `ElementId` 更新 Army 的当前元素，不能修改当前 `WeaponId`。

## MVP 发射规则

每个激活槽位按相同的 `TbWeapon.FireInterval` 独立发射一枚基础子弹。`RepresentedCount` 不缩放射速、伤害或弹丸数量，也不将逻辑人数直接转换为等量子弹。相关缩放与多弹道在出现明确玩法需求后再扩展。

推荐的统一计算顺序：

```text
WeaponId 对应的基础参数
→ BulletId 对应的基础伤害和速度
→ 携带当前 ElementId
→ 生成 Bullet 快照
```

## 测试标准

- 武器箱只能通过有效的 `WeaponId` 更新 Army。
- 换武器不会意外清除当前元素。
- 相同武器 ID 在不同槽位生成的子弹基础参数一致。
- 不同武器 ID 即使外观相似，也按独立配置验证和表现。
- 改变槽位的 `RepresentedCount` 不改变单次发射数量、子弹伤害或速度。
