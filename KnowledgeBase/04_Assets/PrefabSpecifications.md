# MVP 首轮 Prefab 与场景绑定规格

## 范围

本文只定义首轮工程切片为验证玩法闭环所需的最小 Unity 层级、组件和 Inspector 绑定。Sprite、颜色、字体、动画和最终 UI 布局均可在不改变根脚本、Collider 职责、序列化引用和玩法状态所有权的前提下后续替换。

所有必需引用在 Bootstrap 或 Gameplay Preparing 阶段集中校验。缺失或非法时直接 `Debug.LogError` 输出对象路径、字段和原因，并停止进入 Ready；不得运行时 `Find`、`GetComponent`、`AddComponent` 或使用默认资源补齐。

## Gameplay 场景最小结构

```text
GameplayRoot [GameplaySceneEntry]
├── MainCamera [Camera；Orthographic]
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

首轮不创建 HUD 节点、胜负面板或计时文本。MainCamera、CanvasScaler 的最终适配参数在工程创建时按目标竖屏分辨率配置，但不作为玩法数值来源。

## Army

```text
PF_Army_001 [ArmyController]
└── Slots
    ├── Slot_00 [ArmySlotView]
    │   ├── SoldierVisual [SpriteRenderer 或占位视觉]
    │   ├── SlotCollider [Collider2D；ArmySlot Layer]
    │   └── FirePoint [Transform]
    └── Slot_XX ...
```

- ArmyController 显式绑定有序 `ArmySlotView[] slots`。
- ArmySlotView 显式绑定 `soldierVisual`、`slotCollider`、`firePoint`。
- 数组顺序就是稳定 SlotIndex；不得运行时扫描或排序。

## Bullet

```text
PF_Bullet [Bullet]
├── Visual [SpriteRenderer 或占位视觉]
└── BodyCollider [Collider2D；Bullet Layer]
```

- Bullet 根组件显式绑定 `bodyCollider` 和 `visual`。
- 首轮所有 BulletId 共用该 Prefab；基础伤害和速度由配置快照注入。
- 不要求 Animator、Trail 或命中特效。表现差异可以延后；当前可通过日志和调试 Inspector 验证 BulletId。

## Monster

```text
PF_Monster_Normal [NormalMonster；Animator]
├── Visual [SpriteRenderer]
└── BodyCollider [Collider2D；EnemyBody Layer]

PF_Monster_Elite [EliteMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer]
└── AttackCollider [Collider2D；EnemyAttack Layer]

PF_Monster_Boss [BossMonster；Animator]
├── Visual [SpriteRenderer]
├── BodyCollider [Collider2D；EnemyBody Layer]
└── AttackCollider [Collider2D；EnemyAttack Layer]
```

- 三个根 GameObject 都同时挂载具体 Monster 根脚本和 Animator，并显式绑定 `bodyCollider`、视觉引用、Animator 和有限且非负的 `blockingGap`。
- Elite/Boss 另外绑定 `attackCollider`；Normal 不绑定 AttackCollider。
- 三种 Prefab 均不创建 TargetSensor。
- `blockingGap` 只来自当前规范 Prefab，不进入 Luban 或 LevelConfig。
- 三种非循环 Attack Clip 都必须包含恰好一个调用 `OnAttackFrame()` 的命中关键帧事件，以及末帧一个调用 `OnAttackAnimationFinished()` 的结束事件。AnimationEvent 只登记请求，实际伤害由 EnemyManager.ResolveAttacks 执行。
- 三种非循环 Death Clip 的末帧都必须包含一个调用 `OnDeathAnimationFinished()` 的事件。该事件只向 EnemyManager 登记延后回收，不负责减少存活数、发布 `MonsterKilled` 或直接操作对象池。
- Elite/Boss 的 AttackCollider 可以保持启用作为查询形状；它不参与自动碰撞，只在 ResolveAttacks 消费关键帧请求时执行显式查询。

## Gate

```text
PF_Gate_Additive [AdditiveGate]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer]
└── StateText [TMP_Text]

PF_Gate_Element [ElementGate]
├── Visual [SpriteRenderer 或占位底图]
├── BodyCollider [Collider2D；Gate Layer]
└── StateText [TMP_Text]
```

- AdditiveGate 显式绑定 `bodyCollider`、`stateText` 和非负有限 `moveSpeed`。
- ElementGate 显式绑定 `bodyCollider`、`stateText`、非负有限 `moveSpeed` 和正整数 `contactDamage`。
- Additive 的 StateText 显示当前 GateValue。
- Element 的 StateText 至少显示 ElementType、`CurrentHp/MaxHp` 和 PostDepletionDamage；排版与最终文案不属于玩法契约。
- TMP_Text 只显示根组件已经结算的状态，不持有或修改玩法数值。

## Prop

```text
PF_Prop_Weapon [WeaponProp]
├── Visual [SpriteRenderer 或占位视觉]
├── BodyCollider [Collider2D；Prop Layer]
└── DebugText [TMP_Text；可选占位表现]
```

- WeaponProp 显式绑定 `bodyCollider` 和视觉引用。
- 首轮可以使用 DebugText 显示 WeaponId，或使用 Inspector 绑定的简单占位 Sprite；二者都不参与效果选择。
- MaxHp、ContactDamage、MoveSpeed 和 WeaponId 从 Prop 配置快照注入。

## Road

```text
PF_Road_Default [RoadView]
└── Visual [SpriteRenderer 或占位纯色矩形]
```

- RoadView 显式绑定视觉节点，并根据 RoadLayoutSnapshot 调整显示范围。
- Road 不包含玩法 Collider；`roadBounds` 始终是唯一玩法边界。

## Input UI

```text
PF_UI_TouchDragArea [RectTransform；Image；TouchDragInput]
```

- RectTransform 全屏拉伸。
- Image alpha 为 0 且开启 Raycast Target。
- TouchDragInput 显式绑定根 RectTransform、根 Image，`horizontalMultiplier = 1`。
- Prefab 不包含 Canvas、EventSystem、InputAdapter、HUD 或 Army 引用。

## 首轮校验结果

- 任一必需根脚本、引用、Collider、Layer 或数值非法时直接输出错误并停止 Gameplay Ready。
- 不要求正式 Sprite、正式序列帧、VFX、HUD 或音频资源才能验证玩法；允许使用占位视觉和占位 Animator Controller，但三种 Monster 的 Animator、Controller、非循环 Attack/Death Clip 及其 `OnAttackFrame()` / `OnAttackAnimationFinished()` / `OnDeathAnimationFinished()` 事件必须完整绑定。
- 对象池 Prefab 必须保持一个具体根类型对应一个规范 Prefab。
- 首轮通过合理的速度、Collider 尺寸和关卡编排避免离散阶段模型中的高速穿透，不额外实现相对运动扫掠或子步进。
