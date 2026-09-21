# MVP 首轮 Prefab 与场景绑定规格

## 范围

本文定义首轮工程切片为验证玩法闭环所需的最小 Unity 层级、组件和 Inspector 绑定。批次 7 起，Army、Monster、Bullet 和 Gate 的正式序列帧、Animator 与 ID 映射进入装配范围；三种元素组合已有最小 LineRenderer 显示，颜色、字体、最终 UI 布局和其他 VFX 仍可在不改变根脚本、Collider 职责、序列化引用和玩法状态所有权的前提下后续替换。动画目录、命名、导入设置和制作顺序见 [AnimationPipeline](AnimationPipeline.md)。

所有必需引用在 Bootstrap 或 Gameplay Preparing 阶段集中校验。缺失或非法时直接 `Debug.LogError` 输出对象路径、字段和原因，并停止进入 Ready；不得运行时 `Find`、`GetComponent`、`AddComponent` 或使用默认资源补齐。

## Bootstrap 常驻摄像机

```text
GlobalRoot
└── AppCamera [Camera；Orthographic；MainCamera Tag；AudioListener]
```

AppCamera 保留唯一启用的 Camera 和 AudioListener，由 GlobalBootstrap 显式绑定并随 GlobalRoot 常驻。Camera 使用 Solid Color 清屏并渲染当前 Additive 应用场景；MainMenu、LevelSelect 和 Gameplay Canvas 均保持 Screen Space - Overlay。应用场景不得再创建自己的 Camera 或 AudioListener。

## LevelSelect UI

```text
LevelSelectCanvas [LevelSelectView]
└── LevelNodeContainer [不挂 LayoutGroup]
    ├── LevelNode_0 [LevelSelectNodeView]
    └── LevelNode_XX ...
```

- `LevelSelectView` 在 `LevelNodeBinding[]` 中显式绑定场景节点和唯一 LevelId；目录中的每个 LevelId 必须恰好出现一次。
- 节点是预放对象，不在运行时生成或销毁；每个节点的 RectTransform 可以独立调整。
- `PF_UI_LevelSelectNode` 显式绑定关卡名、按钮、`UnlockedState` 和 `CompletedState`。两个状态根互斥；未解锁时都隐藏，按钮禁用。
- 节点及全部绑定对象必须属于当前 LevelSelectView 层级，不使用运行时查找或自动补组件。

## Gameplay 场景最小结构

```text
GameplayRoot [GameplaySceneEntry]
├── Road [RoadView；PF_Road_Default 实例]
├── ArmyContainer
├── LevelSystems
│   ├── LevelManager
│   └── SpawnManager
├── MonsterRoot [EnemyManager]
├── ObstacleRoot [ObstacleManager]
│   ├── GateRoot
│   └── PropRoot
├── BulletRoot [BulletManager]
├── ElementComboRoot [ElementComboManager]
├── VFXRoot [运行时元素组合效果实例]
├── InputAdapter [GameplayInputAdapter]
├── UI [Canvas；GraphicRaycaster]
│   ├── TouchDragArea [PF_UI_TouchDragArea 实例]
│   ├── BattleHud [BattleHudView]
│   ├── LevelIntroVideo [Image；VideoPlayer；LevelIntroVideoView]
│   │   └── VideoRawImage [RawImage；AspectRatioFitter]
│   └── BattleResult [BattleResultView]
│       ├── GameOverRoot
│       ├── VictoryWithNextRoot
│       └── VictoryWithoutNextRoot
└── EventSystem [EventSystem；StandaloneInputModule]
```

GameplayScene 的 Canvas 直属节点顺序保持 TouchDragArea、BattleHud、LevelIntroVideo、BattleResult；具体视觉样式和本轮新增绑定由用户在 Editor 中装配。BattleHud 必须绑定一个 `Image.Type.Filled` 的击杀进度 Image。BattleResult 下必须有失败、有下一关胜利、无下一关胜利三个互斥根节点：失败根绑定重试与返回按钮，有下一关胜利根绑定下一关与返回按钮，无下一关胜利根绑定返回按钮；共用总耗时和击杀文本仍属于 BattleResult 层级。LevelIntroVideo 根节点全屏拉伸，黑色 Image 开启 Raycast Target，同节点 VideoPlayer 禁止 Play On Awake/Loop、使用 API Only 与无音频输出；子 RawImage 全屏拉伸并由 AspectRatioFitter 采用 Envelope Parent。GameplaySceneEntry 显式绑定 LevelIntroVideoView，并通过 `LevelIntroVideoBinding[]` 按 LevelId 绑定导入的 VideoClip。数组可为空；数组中的负数/重复 LevelId 或空 VideoClip 会阻止 Ready。AppCamera、CanvasScaler 的最终适配参数按目标竖屏分辨率配置，但不作为玩法数值来源。

## Army

```text
PF_Army_000 [ArmyController]
└── Slots
    ├── Slot_00 [ArmySlotView]
    │   ├── SoldierVisual [SpriteRenderer；Animator；ArmySlotAnimationEventProxy]
    │   ├── SlotCollider [Collider2D；ArmySlot Layer；ArmySlotHitProxy]
    │   └── FirePoint [Transform]
    └── Slot_XX ...
```

- ArmyController 显式绑定有序 `ArmySlotView[] slots`。
- ArmyController 额外显式绑定唯一的 `WeaponId -> AnimatorOverrideController` 数组，固定覆盖 WeaponId 0～9；ID 不得重复，引用不得为空。
- ArmySlotView 显式绑定 `soldierVisual`、`soldierAnimator`、`animationEventProxy`、`slotCollider`、`slotHitProxy`、`firePoint`；代理与 Animator 必须位于 SoldierVisual，并显式回指当前槽位。
- `ArmySlotHitProxy` 与 SlotCollider 位于同一 GameObject，并显式绑定当前 ArmySlotView；Army 初始化时注入固定 ArmyId 与数组下标 SlotIndex。
- 数组顺序就是稳定 SlotIndex；不得运行时扫描或排序。
- `AC_Army_Base` 固定提供 Idle、MoveLeft、MoveRight、Attack、Victory、Death 六个无参数、无 Transition 的状态；每个 WeaponId 使用预创建的 OverrideController 覆盖六个 Clip。Death 非循环且结束时间恰好包含一个 `OnDeathAnimationFinished()`，只结束槽位死亡表现，不重复结算人数。ArmyController 通过状态哈希显式播放，换武器更新所有非 Dying 槽位并让 Dying 槽位缓存最新 Controller；不运行时创建 OverrideController 或按路径加载。
- Army 不通过 AnimationEvent 生成子弹。Idle、Attack、MoveLeft、MoveRight 循环；后三者都是持续攻击姿态并与 FireInterval 解耦，Victory 非循环。移动状态依据道路限位后的实际 X 位移选择。

## Bullet

```text
PF_Bullet [Bullet；Animator]
├── Visual [SpriteRenderer 或占位视觉]
└── BodyCollider [Collider2D；Bullet Layer]
```

- Bullet 根组件显式绑定 `bodyCollider`、`visual` 和 `animator`，Animator Controller 必须提供整数参数 `BulletId`。
- 首轮所有 BulletId 共用该 Prefab；基础伤害和速度由配置快照注入。
- `BulletId 0`～`9` 各绑定一个循环 Clip。池对象借出时在激活前写入本次 BulletId 并从对应状态起播；归还和复用不能保留上一实例的参数、状态或帧。
- Bullet 动画不包含玩法 AnimationEvent；Trail 和命中特效仍可延后。

## Element Combo Effects

```text
PF_Effect_FireLightningExplosion [FireLightningExplosionEffect]
├── OuterRing [LineRenderer]
└── InnerRing [LineRenderer]

PF_Effect_IceLightningChain [IceLightningChainEffect]
├── PrimaryBolt [LineRenderer]
└── SecondaryBolt [LineRenderer]

PF_Effect_FireIceSteam [FireIceSteamEffect]
└── SteamLine [LineRenderer]
```

- `ElementComboManager` 显式绑定上述三个不同根类型的规范 Prefab、`ElementComboRoot` 和 `VFXRoot`，并分别取得类型池；Luban 不保存 PrefabKey。
- 三个根脚本都验证正数显示时长，并在失活状态通过一次性 `ResolveOnce` 接收命中请求。激活后的 `OnEnable` 只开始显示，`Update` 只由 Manager 传入 VFX 域 delta，不再次结算玩法。
- 火雷根脚本序列化非负额外伤害与正数爆炸半径，两个 LineRenderer 形成双环；冰雷根脚本序列化非负额外伤害、正数选取半径和正整数目标总数，两个 LineRenderer 绘制同一目标链的双层折线；冰火根脚本序列化正数固定 `+Y` 距离，一个 LineRenderer 显示起点到新位置。
- LineRenderer 不带 Collider，不参与 Layer 查询。效果实例的显示父节点是 `VFXRoot`；玩法参数仍由根效果脚本持有，不由材质或子显示节点决定。
- 每个效果实例播放结束时先清除运行时上下文与 LineRenderer，再主动失活并归还对应具体类型池；不得在 `OnDisable` 或 `OnDestroy` 中自行归还。

## Monster

```text
PF_Monster_Chick [ChickMonster；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
├── FireEffectRoot [默认关闭；后续特效挂点]
├── IceEffectRoot [默认关闭；后续特效挂点]
└── LightningEffectRoot [默认关闭；后续特效挂点]

PF_Monster_Hen [HenMonster；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
├── AttackCollider [Collider2D；EnemyAttack Layer]
├── FireEffectRoot [默认关闭；后续特效挂点]
├── IceEffectRoot [默认关闭；后续特效挂点]
└── LightningEffectRoot [默认关闭；后续特效挂点]

PF_Monster_Rooster [RoosterMonster；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
├── AttackCollider [Collider2D；EnemyAttack Layer]
├── FireEffectRoot [默认关闭；后续特效挂点]
├── IceEffectRoot [默认关闭；后续特效挂点]
└── LightningEffectRoot [默认关闭；后续特效挂点]

PF_Monster_Ikun [IkunMonster；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
├── AttackCollider [Collider2D；EnemyAttack Layer]
├── BasketballSpawnPoint [直属生成点]
├── FireEffectRoot [默认关闭；后续特效挂点]
├── IceEffectRoot [默认关闭；后续特效挂点]
└── LightningEffectRoot [默认关闭；后续特效挂点]
```

- 四个根 GameObject 都同时挂载具体 Monster 根脚本、Animator 和 ADR-055 的 Kinematic Rigidbody2D 查询适配，并显式绑定 `bodyCollider`、视觉引用、Animator 和有限且非负的 `blockingGap`；每个 BodyCollider 节点绑定同节点 `BulletHitProxy` 并显式引用根 Monster。
- Hen/Rooster/Ikun 另外绑定 `attackCollider`；Chick 不绑定 AttackCollider。Ikun 还必须绑定直属 `basketballSpawnPoint` 与正数生成间隔。
- 四种 Prefab 均不创建 TargetSensor。
- 四种 Prefab 根脚本都显式绑定三个互不重复的直属元素效果子节点。节点自身默认关闭且不带玩法组件；后续具体表现只能挂在对应节点内部，不替换根脚本引用。
- `blockingGap` 只来自当前规范 Prefab，不进入 Luban 或 LevelConfig。
- 四种 Prefab 的 Animator 使用相同的默认 Move、Attack Trigger、Death Trigger 和整数 `DeathVariant` 语义；Move Clip 循环，Attack 与普通/火/冰/雷 Death Clip 非循环。每种 Prefab 绑定基于公共状态机的本类型 OverrideController。
- 四种非循环近战 Attack Clip 都必须包含恰好一个调用 `OnAttackFrame()` 的命中关键帧事件，以及末帧一个调用 `OnAttackAnimationFinished()` 的结束事件。Ikun 的独立非循环 RangedAttack Clip 必须在篮球离手帧调用一次 `OnBasketballReleaseFrame()`，并在末帧调用一次 `OnRangedAttackAnimationFinished()`。AnimationEvent 只登记请求，实际伤害或篮球生成命令由 EnemyManager.ResolveAttacks 执行。
- 每类敌人的四种非循环 Death Clip 都必须在结束时间包含一个调用 `OnDeathAnimationFinished()` 的事件。该事件只向 EnemyManager 登记延后回收，不负责减少存活数、发布 `MonsterKilled` 或直接操作对象池。
- Hen/Rooster/Ikun 的 AttackCollider 可以保持启用作为查询形状；它不参与自动碰撞，只在 ResolveAttacks 消费关键帧请求时执行显式查询。

## Gate

```text
PF_Gate_Additive [AdditiveGate；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer；BulletHitProxy]
└── StateText [TMP_Text]

PF_Gate_Element [ElementGate；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer；BulletHitProxy]
└── StateText [TMP_Text]
```

- AdditiveGate 根节点提供 ADR-055 的 Kinematic Rigidbody2D 查询适配，并显式绑定 `bodyCollider`、同节点 `BulletHitProxy`、`stateText`、`visual`、`animator` 和非负有限 `moveSpeed`；Animator 绑定唯一循环 Clip。
- ElementGate 根节点提供 ADR-055 的 Kinematic Rigidbody2D 查询适配，并显式绑定 `bodyCollider`、同节点 `BulletHitProxy`、`stateText`、`visual`、`animator`、非负有限 `moveSpeed` 和正整数 `contactDamage`；Controller 必须提供整数参数 `ElementType`。
- ElementGate 仍只有一个规范 Prefab；`Fire=1`、`Ice=2`、`Lightning=3` 在每次借出激活前选择对应循环状态，归还和复用不得残留上一元素动画。
- Additive 的 StateText 显示当前 GateValue。
- Element 的 StateText 至少显示 ElementType、`CurrentHp/MaxHp` 和 PostDepletionDamage；排版与最终文案不属于玩法契约。
- TMP_Text 只显示根组件已经结算的状态，不持有或修改玩法数值。
- Gate 循环 Clip 不包含玩法 AnimationEvent，动画状态不决定 HP、数字、接触或奖励。

## Prop

```text
PF_Prop_Weapon [WeaponProp；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer 或占位视觉]
├── BodyCollider [Collider2D；Prop Layer；BulletHitProxy]
└── DebugText [TMP_Text；可选占位表现]

PF_Prop_Basketball [BasketballProp；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer 或占位视觉]
├── BodyCollider [Collider2D；Prop Layer；BulletHitProxy]
└── DebugText [TMP_Text；可选占位表现]

PF_Prop_GooseCage [GooseCageProp；Animator；Kinematic Rigidbody2D]
├── Visual [SpriteRenderer 或占位视觉]
├── BodyCollider [Collider2D；Prop Layer；BulletHitProxy]
└── DebugText [TMP_Text；可选占位表现]
```

- 三种 Prop 根节点都提供 ADR-055 的 Kinematic Rigidbody2D 查询适配，并显式绑定 `bodyCollider`、同节点 `BulletHitProxy`、视觉引用和 Animator。HP、接触伤害、移动速度及类型专用奖励均从 `PropConfigSnapshot` 注入。
- `PF_Prop_Basketball` 绑定只有 `Basketball_Loop` 的 `AC_Prop_Basketball`；`PF_Prop_Weapon` 绑定 `AC_Prop_Weapon`，该 Controller 以整数参数 `WeaponId` 选择 `Weapon_000_Loop`、`Weapon_001_Loop`、`Weapon_002_Loop`。两个 Controller 都由根 Animator 驱动 `Visual` 子节点的 SpriteRenderer。
- Prop 循环 Clip 不包含玩法 AnimationEvent。对象池借出时在激活前准备身份，激活当帧从第 0 帧播放；归还和跨局复用不得残留旧状态、参数或 Sprite。

上述六个子弹目标根节点的 Rigidbody2D 固定为 Kinematic、Simulated、关闭 Full Kinematic Contacts、Gravity Scale 0、Discrete、无插值并冻结旋转。BodyCollider 继续为 Trigger，自动碰撞矩阵保持关闭；适配刚体不驱动 Transform、推挤或玩法结算。Bullet Prefab 不挂 Rigidbody2D。
- 武器箱正式帧未到位时允许三个空 Clip；DebugText 继续用于诊断，但不参与效果选择。篮球正式帧接入后不再依赖 WeaponProp 占位 Sprite。
- `PF_Prop_GooseCage` 绑定仅含 `GooseCage_Loop` 的 `AC_Prop_GooseCage`；正式帧未到位时空 Clip 不写入 null Sprite 曲线，保留 Prefab 占位图。

## Road

```text
PF_Road_Default [RoadView]
└── Visual [SpriteRenderer 或占位纯色矩形]
```

- RoadView 显式绑定视觉节点，并根据 RoadLayoutSnapshot 调整显示范围。
- Road 不包含玩法 Collider；LevelConfig 的 `roadWidth`、`roadHeight` 及其派生四边始终是唯一玩法边界。

## Input UI

```text
PF_UI_TouchDragArea [RectTransform；Image；TouchDragInput]
```

- RectTransform 全屏拉伸。
- Image alpha 为 0 且开启 Raycast Target。
- TouchDragInput 显式绑定根 RectTransform、根 Image，`horizontalMultiplier = 1`。
- Prefab 不包含 Canvas、EventSystem、InputAdapter、HUD 或 Army 引用。

## 首轮校验结果

- 任一必需根脚本、引用、Collider、身份代理、Layer、Animator、Controller、ID 映射或数值非法时直接输出错误并停止 Gameplay Ready。`BulletHitProxy`/`ArmySlotHitProxy` 必须与对应 Collider 位于同一节点并显式绑定目标，不使用父级搜索补齐。
- 正式 Sprite 尚未到位时允许临时占位 Clip，但 Army 五状态与十套 WeaponId Override、十个 BulletId 循环状态、加法门和三种元素门循环状态、四种 Monster Move/Attack/四类 Death 以及全部必需 Controller/参数必须完整；不得用默认 Sprite 或错误 ID 回退继续 Ready。
- 四种 Monster 的非循环 Attack/Death Clip 及其 `OnAttackFrame()` / `OnAttackAnimationFinished()` / `OnDeathAnimationFinished()` 事件必须完整绑定。
- 对象池 Prefab 必须保持一个具体根类型对应一个规范 Prefab。
- 三种元素组合 Prefab 的根脚本、LineRenderer 数量、玩法参数和显示时长必须完整；三元素掩码不使用其中任一 Prefab。
- 首轮通过合理的速度、Collider 尺寸和关卡编排避免离散阶段模型中的高速穿透，不额外实现相对运动扫掠或子步进。
