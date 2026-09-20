# 美术资源清单

| 资源 | 类型 | 用途 | 状态 |
|---|---|---|---|
| 道路背景 | Sprite / `PF_Road_Default` Prefab | RoadView 根据 `roadBounds` 调整视觉；不配置玩法 Collider | Planned |
| 军队 | Sprite / Animator | 三个 WeaponId 各一套 Idle、Victory、Attack、MoveLeft、MoveRight；由唯一 `PF_Army_000` 预绑定选择 | Attack Imported / Other Actions Pending |
| Army 规范 Prefab | Prefab | `PF_Army_000` 根挂 ArmyController，序列化槽位数组决定 SlotCapacity；GameplaySceneEntry 以 ArmyId=0 绑定 | Planned |
| 加法门 | Sprite / Animator / `PF_Gate_Additive` Prefab | 单循环动画；TMP_Text 显示 GateValue，Inspector 配置统一移动速度 | DirectoryReady / AssetsPending |
| 元素门 | Sprite / Animator / `PF_Gate_Element` Prefab | Fire/Ice/Lightning 三个循环动画按 ElementType 选择；TMP_Text 显示元素、HP 和额外伤害 | DirectoryReady / AssetsPending |
| 弹弓箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 弓箭箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 法杖箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 普通怪物 | Sprite / Animator / Prefab | Move、Attack、Death；Attack 含攻击帧与结束事件，Death 末帧含回收事件；需要 `BodyCollider` 与非负 `blockingGap` | Move/Attack Imported / Death & Events Pending |
| 精英怪物 | Sprite / Animator / Prefab | Move、Attack、Death；需要 `BodyCollider`、`AttackCollider` 与非负 `blockingGap` | Move/Attack Imported / Death & Events Pending |
| Boss 怪物 | Sprite / Animator / Prefab | Move、Attack、Death；需要 `BodyCollider`、`AttackCollider` 与非负 `blockingGap` | Move/Attack Imported / Death & Events Pending |
| 武器箱规范 Prefab | Prefab | 唯一 `WeaponProp` 根类型，按 `WeaponId` 绑定表现 | Planned |
| 子弹 | Sprite / Animator / Prefab | 唯一 `Bullet` 规范 Prefab；BulletId 0/1/2 各一个循环 Clip | Three Loop Clips Imported |
| 命中特效 | Particle / Prefab | 子弹反馈 | Planned |
| `PF_UI_TouchDragArea` | UI Prefab / Image | 全屏拉伸 RectTransform 定义 Gameplay 拖拽范围并承载 `TouchDragInput`；Image alpha 为 0 且启用 Raycast Target | Planned |

## 绑定规则

- MVP 不在 Luban 表配置 `PrefabKey`。池化规范 Prefab 由对应 Manager 的 Inspector 引用绑定；Army 不入池，由 GameplaySceneEntry 的序列化 ArmyPrefabBinding 按 ArmyId 选择。Sprite、Animator、阵型槽位和发射点通过 Unity Inspector 绑定。MVP 完全无声音，不要求 AudioClip。
- 正式动画按 [AnimationPipeline](AnimationPipeline.md) 导入：原始导出包保留在 `Reference/AnimationSource`，Unity 只导入 `Assets/Art/Sprites` 中的最终透明帧；Clip/Controller 放入 `Assets/Animations`，不使用 Resources、StreamingAssets 或运行时路径加载。
- 重复导入通过 `Tools/Game Jam/Sequence Animation Builder` 先扫描再应用；当前已同步 12 个正式 Clip、554 帧，剩余动作仍使用空轨道。
- `NormalMonster`、`EliteMonster`、`BossMonster`、`AdditiveGate`、`ElementGate`、`WeaponProp` 和 `Bullet` 各自只对应一个规范 Prefab；同一具体根类型不能绑定第二个 Prefab。
- Army 保持唯一 `PF_Army_000`，通过序列化的 WeaponId→AnimatorOverrideController 映射选择三套动作并同步到全部槽位。Bullet 保持唯一 `PF_Bullet` 并按 BulletId 选择循环状态；ElementGate 保持唯一 `PF_Gate_Element` 并按 ElementType 选择循环状态。
- Gate Prefab 不保存逐门初始数字、元素类型或 MaxHp，也不引用 Gate 配置表。`AdditiveGate` 序列化统一移动速度；`ElementGate` 序列化统一移动速度和接触伤害，逐门参数由 `GateSpawnRequest` 注入。
- 敌人按 `EnemyType` 选择三个具体类型池；当前三种武器箱共用 `WeaponProp` 类型池并按 `WeaponId` 选择表现，MVP 子弹共用 `Bullet` 类型池并按 `BulletId` 选择数值与表现。
- 资源注册表键仍使用大小写敏感的 ASCII `类别/身份` 格式且不使用绝对路径或 Luban 资源键，但不作为对象池身份或 PoolService 的 Prefab 选择入口。
- 进入 Gameplay 前验证本关使用的资源绑定、Collider2D 和 Layer；缺失时报告配置或资源来源，不静默创建替代对象。
- `PF_Road_Default` 只提供 SpriteRenderer/Transform 和 RoadView，不参与 PoolService，也不设置玩法 Collider；道路四边以 LevelConfig 的数值 `roadBounds` 为权威。
- `Assets/Prefabs/UI/PF_UI_TouchDragArea.prefab` 根对象名为 `TouchDragArea`，默认相对 Gameplay Canvas 全屏拉伸；根同时持有透明 Image 和 TouchDragInput。`TouchDragInput.touchArea` 显式绑定根 RectTransform，`raycastGraphic` 显式绑定根 Image，`horizontalMultiplier` 默认值为 `1`。Prefab 不包含 Canvas、GraphicRaycaster、EventSystem、StandaloneInputModule、Input Adapter 或 Army 引用；GameplaySceneEntry 校验场景组件并由独立 GameplayInputAdapter 绑定 `IHorizontalInputReceiver`。
- 正式动画未全部到位前可以使用占位 Sprite/Clip，但 Controller、参数、ID 映射和 Monster AnimationEvent 必须完整；HUD、VFX、音频和最终 UI 仍不属于本轮。最小层级与字段见 [PrefabSpecifications](PrefabSpecifications.md)。

## 动画资产矩阵

| 身份 | 必需动作 | 循环 |
|---|---|---|
| WeaponId 0 Slingshot | Idle、Victory、Attack、MoveLeft、MoveRight | Idle、MoveLeft、MoveRight |
| WeaponId 1 Bow | Idle、Victory、Attack、MoveLeft、MoveRight | Idle、MoveLeft、MoveRight |
| WeaponId 2 Staff | Idle、Victory、Attack、MoveLeft、MoveRight | Idle、MoveLeft、MoveRight |
| EnemyType Normal | Move、Attack、Death | Move |
| EnemyType Elite | Move、Attack、Death | Move |
| EnemyType Boss | Move、Attack、Death | Move |
| BulletId 0/1/2 | 各一个 Loop | 是 |
| AdditiveGate | Loop | 是 |
| ElementType Fire/Ice/Lightning | 各一个 Loop | 是 |
