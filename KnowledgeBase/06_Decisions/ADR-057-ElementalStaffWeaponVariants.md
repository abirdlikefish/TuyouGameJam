# ADR-057：元素法杖作为独立武器身份派生

> ADR-060 修订实际换武器后的发射起点：元素派生造成 WeaponId 改变时，活动槽位从新周期第 0 帧立即发射，不再等待完整间隔。

## 状态

Accepted

## 日期

2026-09-21

## 背景

Army 已经同时保存唯一的当前 `WeaponId` 与火、冰、雷三个独立元素计时器，但元素目前只进入子弹快照，不会改变武器。玩法现在要求：持有法杖时，当前有效元素组合把普通法杖替换为对应的元素法杖；元素耗尽时按剩余组合逐级退回，全部耗尽后恢复普通法杖。七种元素法杖和对应子弹都需要独立配置与序列帧动画。

## 决策

### 固定身份

`WeaponId` 继续是当前实际装备的唯一身份，不新增 `WeaponType` 或第二份可变法杖状态：

| WeaponId | 武器 | BulletId |
|---:|---|---:|
| 0 | Slingshot | 0 |
| 1 | Bow | 1 |
| 2 | Staff | 2 |
| 3 | FireStaff | 3 |
| 4 | IceStaff | 4 |
| 5 | LightningStaff | 5 |
| 6 | FireIceStaff | 6 |
| 7 | FireLightningStaff | 7 |
| 8 | IceLightningStaff | 8 |
| 9 | FireIceLightningStaff | 9 |

每个 ID 都必须存在独立 `TbWeapon` 与 `TbBullet` 行。首轮元素法杖数值复制普通法杖作为独立可调占位；差异只来自各自配置和表现，不增加额外命中机制。

### 派生与切换

- 法杖家族是 `WeaponId 2..9`。当前武器不属于该家族时，元素获得或耗尽不改变武器。
- 法杖家族的目标 WeaponId 只从三个计时器派生的 `ElementMask` 决定：None→2、Fire→3、Ice→4、Lightning→5、Fire|Ice→6、Fire|Lightning→7、Ice|Lightning→8、三元素→9。
- 武器箱继续通过 `ApplyWeaponPickup` 提交配置 WeaponId。法杖箱仍提交 `2`；Army 在应用时根据已有元素解析最终法杖。Prop、TbProp 和关卡生成链路不增加元素法杖箱。
- 同类型元素只增加时间且有效元素集合未变化时，不重复换武器、不重置冷却。
- 每帧先扣减三个元素计时并发布各自过期事实，再只计算一次最终法杖。多个元素同帧耗尽时不经过中间 WeaponId。
- 元素法杖之间的变化属于实际换武器：全部活动槽位切换 AOC，从新攻击周期第 0 帧立即发射。本帧若发生过期降级，刷新发射及后续发射阶段均使用降级后的配置。
- 已生成子弹保留发射瞬间的 WeaponId、BulletId 和 ElementMask，不随 Army 后续变化。

### 事件

`ArmyWeaponChanged` 增加 `ArmyWeaponChangeReason`，并把来源运行时实例 ID 改为可空：

- `WeaponPickup`：来源是 WeaponProp 实例。
- `ElementActivated`：来源是成功元素门实例。
- `ElementExpired`：自然计时结束，无来源实例，值为 `null`。

元素增加先发布 `ArmyElementDurationChanged`，再发布可能发生的 `ArmyWeaponChanged`；元素过期先发布全部 `ArmyElementExpired`，再发布至多一次 `ArmyWeaponChanged`。

### 动画资产

- 每个 WeaponId 3..9 创建独立五动作 Clip 与 AOC，不复用普通法杖 AOC。
- 每个 BulletId 3..9 创建独立 Loop Clip，并在唯一 `AC_Bullet` 中建立独立状态。
- Army 仍使用唯一 `PF_Army_000`，Bullet 仍使用唯一 `PF_Bullet`；不得因动画身份复制玩法 Prefab。
- Sequence Animation Builder 登记全部新目录。空目录保持 Empty；用户放入连续 PNG 后通过既有 Scan/Apply 流程生成曲线。

## 影响

- 覆盖 ADR-007、ADR-035 中“固定只有 WeaponId 0/1/2”的范围描述；WeaponId 唯一身份原则继续有效。
- ADR-060 修订 ADR-043：元素派生导致的实际换武器同样从新周期第 0 帧立即发射。
- 扩展 ADR-048：Army/Bullet 表现从 3 个身份扩展到 10 个身份，Prefab 数量保持不变。
- 不建立 `TbElement`，不修改 `WeaponConfigSnapshot`、`BulletConfigSnapshot` 或 Bullet 命中契约。

## 验收标准

- 七种非空 ElementMask 均得到固定元素法杖，None 恢复普通法杖。
- 先获得元素再拾取法杖时直接装备正确组合；非法杖持有元素时不自动变杖。
- 多元素同帧过期只发生一次最终 WeaponId 切换。
- 10 个 WeaponId 和 10 个 BulletId 均有独立配置、Clip 身份和可追踪表现绑定。
- 飞行中的子弹不受后续元素获得、过期或法杖降级影响。
