# 美术资源清单

| 资源 | 类型 | 用途 | 状态 |
|---|---|---|---|
| 道路背景 | Sprite / `PF_Road_Default` Prefab | RoadView 根据 `roadBounds` 调整视觉；不配置玩法 Collider | Planned |
| 军队 | Sprite / Animator | 屏幕底部单位 | Planned |
| Army 规范 Prefab | Prefab | `PF_Army_001` 根挂 ArmyController，序列化槽位数组决定 SlotCapacity；GameplaySceneEntry 以 ArmyId=1 绑定 | Planned |
| 加法门 | Sprite / `PF_Gate_Additive` Prefab | 根组件 `AdditiveGate`；显示可为负数的门数字，Inspector 配置所有加法门共用的移动速度 | Planned |
| 元素门 | Sprite / `PF_Gate_Element` Prefab | 根组件 `ElementGate`；显示元素、HP、HP 清空后的额外伤害/可兑换持续时间及接触状态，Inspector 配置所有元素门共用的移动速度和接触伤害 | Planned |
| 弹弓箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 弓箭箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 法杖箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 普通怪物 | Sprite / Animator / Prefab | `NormalMonster` 规范 Prefab；需要 `BodyCollider`，不使用攻击 Collider 或 `TargetSensor` | Planned |
| 精英怪物 | Sprite / Animator / Prefab | `EliteMonster` 规范 Prefab；需要 `BodyCollider`、`AttackCollider`，不使用 `TargetSensor` | Planned |
| Boss 怪物 | Sprite / Animator / Prefab | `BossMonster` 规范 Prefab；需要 `BodyCollider`、`AttackCollider`，不使用 `TargetSensor` | Planned |
| 武器箱规范 Prefab | Prefab | 唯一 `WeaponProp` 根类型，按 `WeaponId` 绑定表现 | Planned |
| 子弹 | Sprite / Prefab | 唯一 `Bullet` 规范 Prefab；按 `BulletId` 绑定表现 | Planned |
| 命中特效 | Particle / Prefab | 子弹反馈 | Planned |
| `PF_UI_TouchDragArea` | UI Prefab / Image | 全屏拉伸 RectTransform 定义 Gameplay 拖拽范围并承载 `TouchDragInput`；Image alpha 为 0 且启用 Raycast Target | Planned |

## 绑定规则

- MVP 不在 Luban 表配置 `PrefabKey`。池化规范 Prefab 由对应 Manager 的 Inspector 引用绑定；Army 不入池，由 GameplaySceneEntry 的序列化 ArmyPrefabBinding 按 ArmyId 选择。Sprite、Animator、阵型槽位和发射点通过 Unity Inspector 绑定。MVP 完全无声音，不要求 AudioClip。
- `NormalMonster`、`EliteMonster`、`BossMonster`、`AdditiveGate`、`ElementGate`、`WeaponProp` 和 `Bullet` 各自只对应一个规范 Prefab；同一具体根类型不能绑定第二个 Prefab。
- Gate Prefab 不保存逐门初始数字、元素类型或 MaxHp，也不引用 Gate 配置表。`AdditiveGate` 序列化统一移动速度；`ElementGate` 序列化统一移动速度和接触伤害，逐门参数由 `GateSpawnRequest` 注入。
- 敌人按 `EnemyType` 选择三个具体类型池；当前三种武器箱共用 `WeaponProp` 类型池并按 `WeaponId` 选择表现，MVP 子弹共用 `Bullet` 类型池并按 `BulletId` 选择数值与表现。
- 资源注册表键仍使用大小写敏感的 ASCII `类别/身份` 格式且不使用绝对路径或 Luban 资源键，但不作为对象池身份或 PoolService 的 Prefab 选择入口。
- 进入 Gameplay 前验证本关使用的资源绑定、Collider2D 和 Layer；缺失时报告配置或资源来源，不静默创建替代对象。
- `PF_Road_Default` 只提供 SpriteRenderer/Transform 和 RoadView，不参与 PoolService，也不设置玩法 Collider；道路四边以 LevelConfig 的数值 `roadBounds` 为权威。
- `Assets/Prefabs/UI/PF_UI_TouchDragArea.prefab` 根对象名为 `TouchDragArea`，默认相对 Gameplay Canvas 全屏拉伸；根同时持有透明 Image 和 TouchDragInput。`TouchDragInput.touchArea` 显式绑定根 RectTransform，`raycastGraphic` 显式绑定根 Image，`horizontalMultiplier` 默认值为 `1`。Prefab 不包含 Canvas、GraphicRaycaster、EventSystem、StandaloneInputModule、Input Adapter 或 Army 引用；GameplaySceneEntry 校验场景组件并由独立 GameplayInputAdapter 绑定 `IHorizontalInputReceiver`。
