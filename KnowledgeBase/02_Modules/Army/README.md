# Army 军队模块

## 模块信息

- ID：`MOD-ARMY`
- 层级：Gameplay
- 状态：`ContractReady`
- 依赖：EventBus、IArmyConfigProvider、IWeaponConfigProvider、Level、IBulletManager
- 决策：`../../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`、`../../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`
- MVP `ArmyId` 固定为 `0`，同时读取首行 `TbArmy.Id = 0` 并选择序列化 `ArmyPrefabBinding.ArmyId = 0` 的 Prefab。

## 职责

- 保存军队逻辑总人数、总人数上限（如启用）和上场槽位状态。
- 根据所选 Army Prefab 的序列化固定槽位生成/隐藏士兵表现，并维护每个槽位的代表人数、整数聚合生命值、碰撞体和子弹生成点；`SlotCapacity = slots.Length` 是最大可见士兵数的唯一来源。
- 向 Monster 提供有士兵槽位的只读位置和索引查询，不暴露槽位内部对象。
- 控制 ArmyRoot 的整体横向移动；每局开始时重置到世界原点 `(0,0,0)` 并保持世界 y=0，移动范围受 RoadLayoutSnapshot 左右边界和当前激活槽位 AABB 共同限制，位移使用 LevelManager 传入的 `Gameplay` delta。
- 按当前激活槽位自动发射子弹；当前武器为本局运行时 `WeaponId`，火、冰、雷分别保存剩余持续时间，发射瞬间派生不可变 `ElementMask`。
- 通过 `IArmyController` 的同步命令接口接收增员、请求减员、指定槽位伤害、元素持续时间和武器变化，不订阅接触/击破事实事件重复结算。
- 负数加法门只提交正的请求减员人数；Army 将其换算为伤害并按当前 HP 最少、槽位索引最小的顺序承担。未来道具效果的 Army 命令边界需在 DES-031 定案，不能由 Prop 直接写入 Army 私有状态。
- 发布 `ArmyCountChanged`、`ArmyFormationChanged`、`SoldierHit`、`ArmyReachedZero`、`ArmyWeaponChanged`、`ArmyElementDurationChanged`、`ArmyElementExpired`。

## 配置输入

- MVP 初始人数固定为 `1`；通过 `IArmyConfigProvider.GetArmyConfig(0)` 必得 ConfigService 已校验并复制的总人数上限、单兵生命值和横向移动速度不可变快照。
- `TbArmy` 不保存槽位容量、武器或元素。`WeaponId` 是本局运行时唯一武器身份，固定 `0 = Slingshot`、`1 = Bow`、`2 = Staff`；每局从 `0` 开始，并通过 `IWeaponConfigProvider.GetWeaponConfig(WeaponId)` 必得发射间隔和基础子弹 ID。ArmyController 不持有 Luban 生成行，也不处理配置缺失恢复。
- 当前不建立 `TbElement`。火、冰、雷三种剩余持续时间每局从 `0` 开始，由元素门按类型增加。
- GameplaySceneEntry 通过序列化 `ArmyPrefabBinding` 选择 Army Prefab；ArmyController 通过序列化 `ArmySlotView[] slots` 持有槽位，不通过 Luban 资源键或运行时搜索取得。
- 当前人数、槽位状态、WeaponId、三元素剩余时间和射击计时都是本局运行时状态，不回写 Luban。

## 非职责

- 不决定加法门或元素门的计算规则。
- 不负责 UI 文本和具体士兵动画资源。
- 不把总人数创建成等量 GameObject；表现对象数量不超过最大上场槽位数。
- 不实现火、冰、雷的具体子弹效果；当前只保存持续时间并把发射瞬间的元素集合交给 Bullet。
- 不处理 Army 槽位与敌人身体的移动碰撞、推挤或重合分离；横向移动不查询 `EnemyBody`，允许士兵与敌人重合。

## 接口

```csharp
// IArmyController : IHorizontalInputReceiver
int GetArmyCount();
int GetActiveSlotCount();
int GetSlotCapacity();
int GetCurrentWeaponId();
ArmyAdditionResult AddArmy(int amount);
ArmyRemovalResult RemoveArmy(int amount);
void ApplySlotDamage(int slotIndex, int damage);
bool TryGetNearestActiveSlot(Vector2 origin, out ArmySlotTarget target);
bool TryGetSlotTarget(int slotIndex, out ArmySlotTarget target);
void ApplyWeaponPickup(int weaponId, int sourceRuntimeInstanceId);
ElementDurationChangeResult AddElementDuration(ElementType elementType, float duration, int sourceRuntimeInstanceId);
ArmyFormationSnapshot GetFormationSnapshot();
ArmyElementStateSnapshot GetElementStateSnapshot();
```

`SetHorizontalInput(float)` 由继承的 `IHorizontalInputReceiver` 提供。Input Adapter 只取得该最小接收接口，不取得上述其他 Army 命令与查询能力。

`ApplySlotDamage` 保持 `void`。Monster、Gate 和 Prop 在调用前必须通过 `TryGetSlotTarget` 重新确认槽位仍有效；无效或空槽位不提交伤害。方法同步完成权威 HP、人数和事件更新，实际扣除与人数损失只由 Army 的 `SoldierHit`、人数及阵型事件表达。攻击者不读取伤害返回值推进状态。

LevelManager 通过 `IArmyRunController.StartRun`、`TickMovementAndFire`、`StopRun` 驱动本局生命周期。ArmyController 不使用独立 Update 推进核心移动、元素计时或射击；Tick 中先扣减元素计时与每槽位射击冷却，再把当前 WeaponId、发射瞬间的 ElementMask、来源槽位、位置和方向组成 `BulletSpawnRequest` 交给 BulletManager。槽位首次激活等待完整 `FireInterval`，实际换到不同武器时全部激活槽位按新间隔重置，每槽位每逻辑帧最多发射一颗且不追赶补发。

## Prefab 与场景装配

```text
GameplayRoot [GameplaySceneEntry；序列化 ArmyPrefabBinding[]]
└── ArmyContainer
    └── ArmyRoot [ArmyController；运行时实例化 PF_Army_000]
        └── Slots
            ├── Slot_00 [ArmySlotView]
            │   ├── SoldierVisual
            │   ├── SlotCollider [Collider2D；ArmySlot Layer；ArmySlotHitProxy]
            │   └── FirePoint
            └── ...
```

Gameplay Preparing 先取得 Army 与 Weapon 配置快照，再查找唯一 `ArmyId = 0` Prefab，实例化到 ArmyContainer，校验 `slots` 非空、无空项、无重复引用且每项绑定完整，然后才向 Level、Input、Enemy 和 Obstacle 注入 IArmyController。Prefab 不进入对象池；清理时销毁本局实例。

### 序列化字段契约

以下字段名和类型作为后续脚本生成基线；全部引用由 Inspector 显式绑定：

```csharp
[Serializable]
public sealed class ArmyPrefabBinding
{
    [SerializeField] private int armyId;
    [SerializeField] private ArmyController prefab;

    public int ArmyId => armyId;
    public ArmyController Prefab => prefab;
}

// GameplaySceneEntry
[SerializeField] private Transform armyContainer;
[SerializeField] private ArmyPrefabBinding[] armyPrefabs;

// ArmyController（位于 Army Prefab 根节点）
[SerializeField] private ArmySlotView[] slots;

// ArmySlotView（每个 Slot_xx 节点一个）
[SerializeField] private GameObject soldierVisual;
[SerializeField] private Collider2D slotCollider;
[SerializeField] private ArmySlotHitProxy slotHitProxy;
[SerializeField] private Transform firePoint;
```

`ArmyPrefabBinding.armyId` 必须大于等于 `0` 且在数组中唯一，`prefab` 必须非空并以 `ArmyController` 为根组件；MVP 必须存在唯一 `ArmyId = 0` 的绑定。`armyContainer` 必须是 GameplaySceneEntry 场景层级中固定的 `ArmyContainer`。`slots` 的数组顺序定义稳定 `SlotIndex`，不可在运行时重新扫描或排序。`ArmySlotHitProxy` 必须与 SlotCollider 位于同一 GameObject 并显式绑定当前 ArmySlotView，Army 初始化时向代理写入固定 ArmyId 和数组下标。所有字段缺失均视为 Preparing 失败，不通过 `Find`、`GetComponentInParent`、`GetComponentsInChildren` 或 `AddComponent` 静默补齐。

## 测试标准

- 初始人数固定为 1，初始 WeaponId 为 0，三元素剩余时间均为 0；人数不能低于 0，`ArmyCountLimit = 0` 时不设上限，大于 0 时人数不能超过该上限。
- 非负门取得 `ArmyAdditionResult`，其中请求增员、受上限约束后的实际增员和结算后总人数可分别验证。
- `TbArmy.Id = 0`、三个固定武器配置或 ArmyId=0 Prefab 任一缺失时不得进入 Gameplay Ready。
- 槽位容量准确等于序列化槽位数组长度，不读取或维护第二份 `MaxDeployedSoldiers`。
- 初始分配采用整数平均、余数按槽位顺序分配；运行时每增加一人都选择 `RepresentedCount` 最少、再按 SlotIndex 最小的槽位，空槽位自然优先重新启用，不保存 `NeedsRefill`。
- 槽位受击只减少当前槽位人数，不主动将其他槽位人数重新平均。
- 新增人数优先补充人数较少或受击后为空的槽位。
- 每个激活槽位有独立碰撞体和子弹生成点；空槽位禁用二者。
- 槽位碰撞体使用 `SlotCollider` 和 ArmySlot Layer；怪物范围攻击、Gate/Prop 接触通过显式查询命中槽位。
- Army 横向移动不对 Enemy `BodyCollider` 执行 Cast，也不因敌人位置截断 ArmyRoot 位移；与敌人重合不产生 Army 侧接触结算。
- 同一个门效果只应用一次，即使门同时接触多个槽位。
- `GateContactResolved`、`PropBroken` 和 `PropContactDamage` 的订阅顺序不会改变 Army 数值，且不会触发第二次效果。
- 负数门按 `Abs(GateValue) × HpPerSoldier` 产生伤害，优先由当前 HP 最少、同 HP 时 SlotIndex 最小的槽位承担；请求减员和实际损失人数分别记录。
- 元素门失败时，每个接触槽位受到相同伤害；成功时只给对应 ElementType 增加一次持续时间，同类型累加且不覆盖其他元素。
- 当前 MVP 的武器箱成功击破时只切换一次 WeaponId；元素持续时间保持不变，道具接触失败后不得触发任何击破效果。
- `AddArmy(0)` 不改变 Army 状态或发布人数/阵型变化事件；重复获得当前 WeaponId 不发布 `ArmyWeaponChanged` 且不重置射击冷却，但 Prop 仍可完成自身的成功击破事实。
- 本局初始激活槽位和运行中新激活槽位均等待一个完整 FireInterval；实际换武器后全部激活槽位重置为新间隔，重复当前 WeaponId 不重置。
- 单个逻辑帧内每个激活槽位最多生成一颗子弹；大帧间隔不补发历史跨过的射击次数。
- Army 先扣减本帧元素计时再发射；元素门在接触阶段新增的元素从下一逻辑帧子弹开始生效，飞行中的子弹元素掩码保持不变。
- Army 在固定道路内移动时不得让当前激活槽位的合并 AABB 越过左右边界；阵型变化后重新计算可移动范围。
- 人数、槽位人数或槽位生命值变化时 UI 能通过事件同步。
- `TbArmy.MoveSpeed` 是横向基础速度；实际位移使用 `horizontalInput × MoveSpeed × gameplayDeltaTime`，该 delta 由 LevelManager 在帧开始读取并传入，同一次更新不得再读取或叠加其他时间域。当前拖拽先把原始归一化滑动速度限制到 `[-1,1]` 再乘 Inspector 系数，因此最终有限输入允许超过该范围；Army 不得再次 Clamp 到 `[-1,1]`。键盘/手柄输入延后。
- ArmyRoot 每局准确重置到世界原点，移动期间 y 保持为 0；道路没有 Collider 时仍能用激活槽位合并 AABB 完成左右限位。

- 人数、槽位聚合 HP 和元素持续时间使用宽中间类型计算；超过公开存储类型时饱和到最大有限值，不得整数回绕、变负、NaN 或无穷。

## 相关设计

- `Formation.md`：槽位分配、聚合生命值、受击和碰撞。
- `Loadout.md`：Weapon/Element 组合与人数对发射的影响。
