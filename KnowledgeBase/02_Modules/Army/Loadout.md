# Army Loadout 武器与元素组合

## 组成

Army 的本局装备状态由一个当前武器和三个并行元素计时器组成：

```text
CurrentWeaponId
FireRemainingDuration
IceRemainingDuration
LightningRemainingDuration
```

- WeaponId 唯一确定一条 `TbWeapon` 配置：`0 = Slingshot`、`1 = Bow`、`2 = Staff`，`3..9` 为火、冰、雷的七种非空组合元素法杖；运行时不维护第二份 WeaponType。
- 当前不建立 `TbElement`。`ElementType.None` 只作为未使用配置字段的空值，Army 只接受火、冰、雷三种可获得元素；具体效果延后设计。
- 三个元素可以任意组合同时有效；ActiveElements 只从剩余时间是否大于 `0` 派生，不保存第四份可变状态。
- Bullet 在生成时保存当前 WeaponId 和发射瞬间的 ElementMask，形成不可变运行时快照。

## 运行时切换

- 每次 StartRun 把当前武器重置为 `0`，三元素剩余时间全部重置为 `0`。
- 武器箱通过 WeaponId 更新当前武器，不改变任何元素剩余时间。法杖箱仍提交 `2`；Army 按已有元素解析最终法杖。
- 成功元素门通过 `AddElementDuration(ElementType, duration, sourceRuntimeInstanceId)` 增加对应计时；传入 `None` 必须拒绝。当前武器属于法杖家族时，Army 按新的有效元素组合切换到对应 WeaponId。
- 同类型持续时间直接累加，当前不设上限；增加时发布 `ArmyElementDurationChanged`。
- 某元素从大于 `0` 首次变为 `0` 时发布一次 `ArmyElementExpired`；三个计时器均扣减后只派生一次最终法杖，全部元素耗尽时恢复 WeaponId 2。
- 实际法杖变体切换发布 `ArmyWeaponChanged`，活动槽位从新攻击周期第 0 帧重播并立即发射；重复增加仍有效的元素不切换武器。

## 元素计时与掩码

Army 在 `TickMovementAndFire` 中先扣减计时，再生成本帧发射快照：

```text
FireRemainingDuration = Max(0, FireRemainingDuration - gameplayDeltaTime)
IceRemainingDuration = Max(0, IceRemainingDuration - gameplayDeltaTime)
LightningRemainingDuration = Max(0, LightningRemainingDuration - gameplayDeltaTime)

ActiveElements = None
if FireRemainingDuration > 0: ActiveElements |= Fire
if IceRemainingDuration > 0: ActiveElements |= Ice
if LightningRemainingDuration > 0: ActiveElements |= Lightning
```

ElementMask 固定为 `None = 0`、`Fire = 1`、`Ice = 2`、`Lightning = 4`。Gate 接触晚于 Army 发射阶段，因此本帧新获得的元素从下一逻辑帧发射开始生效。子弹生成后不随 Army 的计时器变化。

法杖映射固定为：None→2、Fire→3、Ice→4、Lightning→5、Fire|Ice→6、Fire|Lightning→7、Ice|Lightning→8、Fire|Ice|Lightning→9。

## MVP 发射规则

每个激活槽位按相同的 `TbWeapon.FireInterval` 独立发射一枚基础子弹。`RepresentedCount` 不缩放射速、伤害或弹丸数量，也不将逻辑人数直接转换为等量子弹。相关缩放与多弹道在出现明确玩法需求后再扩展。

每个槽位独立保存 `fireCooldownRemaining`。本局开始时已经激活的槽位在首个 Playing Tick 以到期状态进入当前武器周期，因此从战斗动画第 1 帧立即首发；运行中刚从未激活变为激活的槽位仍把计时器设为当前武器完整的 `FireInterval`。实际切换到不同 `WeaponId` 时，所有当前活动槽位从新武器周期第 0 帧重播并立即发射；重复获得当前武器不重置计时。

`TickMovementAndFire` 每帧推进攻击周期。到期槽位本帧最多生成一颗子弹；即使单帧 delta 跨过多个间隔也不追赶补发，但通过取模保留越过边界的余量，使后续发射与动画相位保持一致。失活槽位不发射，再次激活时重新等待完整间隔。

推荐的统一计算顺序：

```text
WeaponId 对应的基础参数
→ BulletId 对应的基础伤害和速度
→ 携带发射瞬间的 ActiveElements
→ 生成 Bullet 快照
```

## 测试标准

- `TbWeapon` 必须准确包含 0～9 十个固定 ID，0 不得被当作未配置值。
- 每局以弹弓和三个 0 秒元素计时开始。
- 武器箱只能通过有效的 WeaponId 更新 Army，换武器不会改变任何元素计时。
- 本局初始活动槽位在首个 Playing Tick 立即首发；运行中新激活槽位和失活后重新激活的槽位等待一个完整 FireInterval。
- 实际切换到不同武器时，全部活动槽位从新周期第 0 帧立即发射；重复获得当前武器不重置。
- 每个槽位每逻辑帧最多发射一颗，超长帧不补发跨过的历史射击次数，但保留周期余量。
- 火、冰、雷可以同时生效；重复获得同元素累加持续时间，不覆盖另外两种。
- 七种非空元素组合映射到七种独立元素法杖；元素部分或全部过期时按剩余组合降级。
- 多个元素同帧过期时只切换一次最终法杖；先获得元素再拾取法杖时直接得到正确组合。
- 计时只在 Playing 使用传入的 Gameplay delta 扣减且不会小于 0；每次有效期只发布一次过期事实。
- 相同武器 ID 在不同槽位生成的子弹基础参数一致。
- 不同武器 ID 即使外观相似，也按独立配置验证和表现。
- 改变槽位的 `RepresentedCount` 不改变单次发射数量、子弹伤害或速度。
- 子弹保存发射瞬间的 ElementMask；Army 后续获得或失去元素不修改飞行中的子弹。
