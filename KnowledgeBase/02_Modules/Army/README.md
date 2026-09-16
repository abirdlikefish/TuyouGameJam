# Army 军队模块

## 模块信息

- ID：`MOD-ARMY`
- 层级：Gameplay
- 状态：`InDesign`
- 依赖：TimeService、EventBus、ConfigService、Level、Gate、Prop
- MVP `ArmyId` 固定为 `1`。

## 职责

- 保存军队逻辑总人数、总人数上限（如启用）和上场槽位状态。
- 根据固定阵型槽位生成/隐藏士兵表现，并维护每个槽位的代表人数、聚合生命值、碰撞体和子弹生成点。
- 向 Monster 提供有士兵槽位的只读位置和索引查询，不暴露槽位内部对象。
- 控制 ArmyRoot 的整体横向移动；移动范围受 LevelConfig 固定道路左右边界和当前激活槽位 AABB 共同限制。
- 按当前激活槽位自动发射子弹，将武器和元素组合成运行时子弹配置。
- 通过 `IArmyController` 的同步命令接口接收加法门、元素门和道具造成的人数、槽位伤害或装备变化，不订阅接触/击破事实事件重复结算。
- 通过 `WeaponId` 应用武器箱效果，通过 `ElementId` 应用元素门效果；两种切换互不覆盖另一维度。
- 发布 `ArmyCountChanged`、`ArmyFormationChanged`、`SoldierHit`、`ArmyReachedZero`、`ArmyLoadoutChanged`。

## 配置输入

- MVP 初始人数固定为 `1`；从 Luban `TbArmy` 读取总人数上限、最大上场槽位数、单兵生命值、横向移动速度、初始武器 ID 和初始元素 ID。
- 通过 `WeaponId` 和 `ElementId` 获取全军共享的武器与元素配置；武器引用基础子弹配置。
- `WeaponId` 是唯一的运行时武器身份；不维护第二份可变的 `WeaponType` 身份。
- 固定阵型槽位、士兵 Prefab 和每槽位发射点由 Unity Inspector 绑定，不通过 Luban 资源键查找。
- 当前人数和射击计时是运行时状态，不回写 Luban。

## 非职责

- 不决定加法门或元素门的计算规则。
- 不负责 UI 文本和具体士兵动画资源。
- 不把总人数创建成等量 GameObject；表现对象数量不超过最大上场槽位数。

## 接口

```csharp
int GetArmyCount();
int GetActiveSlotCount();
int GetMaxDeployedSoldiers();
void AddArmy(int amount);
void RemoveArmy(int amount);
void ApplySlotDamage(int slotIndex, int damage);
bool TryGetNearestActiveSlot(Vector2 origin, out ArmySlotTarget target);
bool TryGetSlotTarget(int slotIndex, out ArmySlotTarget target);
void ApplyGateEffect(GateEffect effect);
void ApplyWeaponPickup(int weaponId, int sourceRuntimeInstanceId);
void ApplyElement(int elementId, int sourceRuntimeInstanceId);
void SetHorizontalInput(float value);
```

## 测试标准

- 初始人数固定为 1，人数不能低于 0；`ArmyCountLimit = 0` 时不设上限，大于 0 时人数不能超过该上限。
- 初始分配和没有受击缺口时的新增分配采用整数平均、余数按槽位顺序分配。
- 槽位受击只减少当前槽位人数，不主动将其他槽位人数重新平均。
- 新增人数优先补充人数较少或受击后为空的槽位。
- 每个激活槽位有独立碰撞体和子弹生成点；空槽位禁用二者。
- 槽位碰撞体使用 `SlotCollider` 和 ArmySlot Layer；怪物范围攻击、Gate/Prop 接触通过显式查询命中槽位。
- 同一个门效果只应用一次，即使门同时接触多个槽位。
- `GateContactResolved`、`PropBroken` 和 `PropContactDamage` 的订阅顺序不会改变 Army 数值，且不会触发第二次效果。
- 元素门失败时，每个接触槽位受到相同伤害；成功时只切换一次 `ElementId`。
- 武器箱成功击破时只切换一次 `WeaponId`，并保留当前 `ElementId`。
- Army 在固定道路内移动时不得让当前激活槽位的合并 AABB 越过左右边界；阵型变化后重新计算可移动范围。
- 人数、槽位人数或槽位生命值变化时 UI 能通过事件同步。
- 横向移动速度只来自 `TbArmy.MoveSpeed`，Input 的输入值只表示方向和强度。

## 相关设计

- `Formation.md`：槽位分配、聚合生命值、受击和碰撞。
- `Loadout.md`：Weapon/Element 组合与人数对发射的影响。
