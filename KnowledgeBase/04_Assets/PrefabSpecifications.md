# MVP 首轮 Prefab 与场景绑定规格

## 范围

本文定义首轮工程切片为验证玩法闭环所需的最小 Unity 层级、组件和 Inspector 绑定。批次 7 起，Army、Monster、Bullet 和 Gate 的正式序列帧、Animator 与 ID 映射进入装配范围；颜色、字体、最终 UI 布局和 VFX 仍可在不改变根脚本、Collider 职责、序列化引用和玩法状态所有权的前提下后续替换。动画目录、命名、导入设置和制作顺序见 [AnimationPipeline](AnimationPipeline.md)。

所有必需引用在 Bootstrap 或 Gameplay Preparing 阶段集中校验。缺失或非法时直接 `Debug.LogError` 输出对象路径、字段和原因，并停止进入 Ready；不得运行时 `Find`、`GetComponent`、`AddComponent` 或使用默认资源补齐。

## Bootstrap 常驻摄像机

```text
GlobalRoot
└── AppCamera [Camera；Orthographic；MainCamera Tag；AudioListener]
```

AppCamera 保留唯一启用的 Camera 和 AudioListener，由 GlobalBootstrap 显式绑定并随 GlobalRoot 常驻。Camera 使用 Solid Color 清屏并渲染当前 Additive 应用场景；MainMenu、LevelSelect 和 Gameplay Canvas 均保持 Screen Space - Overlay。应用场景不得再创建自己的 Camera 或 AudioListener。

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
├── InputAdapter [GameplayInputAdapter]
├── UI [Canvas；GraphicRaycaster]
│   └── TouchDragArea [PF_UI_TouchDragArea 实例]
└── EventSystem [EventSystem；StandaloneInputModule]
```

首轮不创建 HUD 节点、胜负面板或计时文本。AppCamera、CanvasScaler 的最终适配参数在工程创建时按目标竖屏分辨率配置，但不作为玩法数值来源。

## Army

```text
PF_Army_000 [ArmyController]
└── Slots
    ├── Slot_00 [ArmySlotView]
    │   ├── SoldierVisual [SpriteRenderer；Animator]
    │   ├── SlotCollider [Collider2D；ArmySlot Layer；ArmySlotHitProxy]
    │   └── FirePoint [Transform]
    └── Slot_XX ...
```

- ArmyController 显式绑定有序 `ArmySlotView[] slots`。
- ArmyController 额外显式绑定唯一的 `WeaponId -> AnimatorOverrideController` 数组，固定覆盖 `0 = Slingshot`、`1 = Bow`、`2 = Staff`；ID 不得重复，引用不得为空。
- ArmySlotView 显式绑定 `soldierVisual`、`soldierAnimator`、`slotCollider`、`slotHitProxy`、`firePoint`；Animator 必须作用于当前 SoldierVisual 的 SpriteRenderer。
- `ArmySlotHitProxy` 与 SlotCollider 位于同一 GameObject，并显式绑定当前 ArmySlotView；Army 初始化时注入固定 ArmyId 与数组下标 SlotIndex。
- 数组顺序就是稳定 SlotIndex；不得运行时扫描或排序。
- `AC_Army_Base` 固定提供 Idle、MoveLeft、MoveRight、Attack、Victory 五个无参数、无 Transition 的状态；每个 WeaponId 使用预创建的 OverrideController 覆盖五个 Clip。ArmyController 通过状态哈希显式播放，换武器更新所有槽位并保持当前战斗状态，包括当前隐藏槽位；不运行时创建 OverrideController 或按路径加载。
- Army 不通过 AnimationEvent 生成子弹。Idle、Attack、MoveLeft、MoveRight 循环；后三者都是持续攻击姿态并与 FireInterval 解耦，Victory 非循环。移动状态依据道路限位后的实际 X 位移选择。

## Bullet

```text
PF_Bullet [Bullet；Animator]
├── Visual [SpriteRenderer 或占位视觉]
└── BodyCollider [Collider2D；Bullet Layer]
```

- Bullet 根组件显式绑定 `bodyCollider`、`visual` 和 `animator`，Animator Controller 必须提供整数参数 `BulletId`。
- 首轮所有 BulletId 共用该 Prefab；基础伤害和速度由配置快照注入。
- `BulletId 0/1/2` 各绑定一个循环 Clip。池对象借出时在激活前写入本次 BulletId 并从对应状态起播；归还和复用不能保留上一实例的参数、状态或帧。
- Bullet 动画不包含玩法 AnimationEvent；Trail 和命中特效仍可延后。

## Monster

```text
PF_Monster_Normal [NormalMonster；Animator]
├── Visual [SpriteRenderer]
└── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]

PF_Monster_Elite [EliteMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
└── AttackCollider [Collider2D；EnemyAttack Layer]

PF_Monster_Boss [BossMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer；BulletHitProxy]
└── AttackCollider [Collider2D；EnemyAttack Layer]
```

- 三个根 GameObject 都同时挂载具体 Monster 根脚本和 Animator，并显式绑定 `bodyCollider`、视觉引用、Animator 和有限且非负的 `blockingGap`；每个 BodyCollider 节点绑定同节点 `BulletHitProxy` 并显式引用根 Monster。
- Elite/Boss 另外绑定 `attackCollider`；Normal 不绑定 AttackCollider。
- 三种 Prefab 均不创建 TargetSensor。
- `blockingGap` 只来自当前规范 Prefab，不进入 Luban 或 LevelConfig。
- 三种 Prefab 的 Animator 使用相同的默认 Move、Attack Trigger 和 Death Trigger 语义；Move Clip 循环，Attack/Death Clip 非循环。每种 Prefab 直接绑定本类型 Controller 或基于公共状态机的预创建 OverrideController。
- 三种非循环 Attack Clip 都必须包含恰好一个调用 `OnAttackFrame()` 的命中关键帧事件，以及末帧一个调用 `OnAttackAnimationFinished()` 的结束事件。AnimationEvent 只登记请求，实际伤害由 EnemyManager.ResolveAttacks 执行。
- 三种非循环 Death Clip 的末帧都必须包含一个调用 `OnDeathAnimationFinished()` 的事件。该事件只向 EnemyManager 登记延后回收，不负责减少存活数、发布 `MonsterKilled` 或直接操作对象池。
- Elite/Boss 的 AttackCollider 可以保持启用作为查询形状；它不参与自动碰撞，只在 ResolveAttacks 消费关键帧请求时执行显式查询。

## Gate

```text
PF_Gate_Additive [AdditiveGate；Animator]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer；BulletHitProxy]
└── StateText [TMP_Text]

PF_Gate_Element [ElementGate；Animator]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer；BulletHitProxy]
└── StateText [TMP_Text]
```

- AdditiveGate 显式绑定 `bodyCollider`、同节点 `BulletHitProxy`、`stateText`、`visual`、`animator` 和非负有限 `moveSpeed`；Animator 绑定唯一循环 Clip。
- ElementGate 显式绑定 `bodyCollider`、同节点 `BulletHitProxy`、`stateText`、`visual`、`animator`、非负有限 `moveSpeed` 和正整数 `contactDamage`；Controller 必须提供整数参数 `ElementType`。
- ElementGate 仍只有一个规范 Prefab；`Fire=1`、`Ice=2`、`Lightning=3` 在每次借出激活前选择对应循环状态，归还和复用不得残留上一元素动画。
- Additive 的 StateText 显示当前 GateValue。
- Element 的 StateText 至少显示 ElementType、`CurrentHp/MaxHp` 和 PostDepletionDamage；排版与最终文案不属于玩法契约。
- TMP_Text 只显示根组件已经结算的状态，不持有或修改玩法数值。
- Gate 循环 Clip 不包含玩法 AnimationEvent，动画状态不决定 HP、数字、接触或奖励。

## Prop

```text
PF_Prop_Weapon [WeaponProp]
├── Visual [SpriteRenderer 或占位视觉]
├── BodyCollider [Collider2D；Prop Layer；BulletHitProxy]
└── DebugText [TMP_Text；可选占位表现]
```

- WeaponProp 显式绑定 `bodyCollider`、同节点 `BulletHitProxy` 和视觉引用。
- 首轮可以使用 DebugText 显示 WeaponId，或使用 Inspector 绑定的简单占位 Sprite；二者都不参与效果选择。
- MaxHp、ContactDamage、MoveSpeed 和 WeaponId 从 Prop 配置快照注入。

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
- 正式 Sprite 尚未到位时允许临时占位 Clip，但 Army 五状态与三套 WeaponId Override、三个 BulletId 循环状态、加法门和三种元素门循环状态、三种 Monster Move/Attack/Death 以及全部必需 Controller/参数必须完整；不得用默认 Sprite 或错误 ID 回退继续 Ready。
- 三种 Monster 的非循环 Attack/Death Clip 及其 `OnAttackFrame()` / `OnAttackAnimationFinished()` / `OnDeathAnimationFinished()` 事件必须完整绑定。
- 对象池 Prefab 必须保持一个具体根类型对应一个规范 Prefab。
- 首轮通过合理的速度、Collider 尺寸和关卡编排避免离散阶段模型中的高速穿透，不额外实现相对运动扫掠或子步进。
