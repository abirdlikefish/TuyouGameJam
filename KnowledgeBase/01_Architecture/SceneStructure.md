# 场景结构

## 图例与命名边界

- `Root` 表示 GameObject 层级或运行时实例容器，本身不定义业务职责。
- 方括号中的名称表示挂载组件或由该对象持有、注册的运行时职责，不要求每个服务都单独占用一个 GameObject。
- `Controller` 控制一个明确的玩法聚合体；`Manager` 管理关卡内生命周期、时间轴或实例集合；`Service` 提供跨场景应用能力；`Bootstrap` 只负责初始化和注册。
- 不为了统一字面后缀而重命名现有类型，也不增加职责笼统的 `GameManager`。完整规则见 [ADR-025](../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md)。

## 常驻层

构建入口使用 `BootstrapScene`。它创建唯一的 `GlobalRoot`，由 `GlobalBootstrap` 调用 `DontDestroyOnLoad` 后跨场景保留：

```text
BootstrapScene
└── GlobalRoot [DontDestroyOnLoad]
    ├── GlobalBootstrap
    ├── ServiceHost
    │   ├── TimeService
    │   ├── GameStateService
    │   ├── SceneService
    │   ├── ConfigService
    │   ├── EventBus
    │   └── PoolService
    └── PersistentPoolRoot
```

`ServiceHost` 表示服务的所有权和注册位置，不要求图中的每个服务都实现为子 GameObject；适合使用纯 C# 的服务可以由 `GlobalBootstrap` 或统一宿主持有。服务由 Composition Root 创建并通过接口注入，不要求也不提供各自的静态 `Instance`。MVP 完全无声音，不创建 `AudioRoot` 或 `AudioService`；`SaveService` 和 `DebugService` 同样不初始化。

`PersistentPoolRoot` 由 PoolService 持有，并按具体池化根组件类型维护空闲子节点。空闲实例全部失活；租出后由对应 Manager 移入当前 Gameplay 场景的 `MonsterRoot`、`ObstacleRoot`、`BulletRoot` 或其他职责节点，场景卸载前归还。

场景重载时不得创建重复的 `GlobalRoot`。全局服务可以保存当前 `LevelRunId` 和公共契约数据，但不得长期持有已卸载 MainMenu、LevelSelect 或 Gameplay 场景对象的具体引用。

## 应用页面层

MainMenu、LevelSelect 和 Gameplay 都是由 SceneService 管理的 Additive 应用场景；`BootstrapScene` 始终保留。同一时刻至多有一个非 Bootstrap 应用场景处于 Ready：

```text
MainMenuScene
└── MainMenuRoot [MainMenuSceneEntry]

LevelSelectScene
└── LevelSelectRoot [LevelSelectSceneEntry]
```

MainMenuSceneEntry 和 LevelSelectSceneEntry 是场景装配入口，当前只完成固定引用校验、最小依赖注入、订阅生命周期和结构化日志验证。正式主界面与选关 UI 尚未实现；对应场景 Ready 后仍由 GameStateService 使用 RealTime 等待 1 秒自动推进。

三个应用场景都遵守固定根入口约定。Unity 场景适配器只在本次加载的 `Scene.GetRootGameObjects()` 中解析规范根，不使用跨场景 `GameObject.Find`。根缺失、重名、入口类型错误或初始化失败都必须报告加载失败，不得以运行时添加组件或静态入口注册兜底。

## Gameplay 单局层

```text
GameplayScene
└── GameplayRoot [GameplaySceneEntry]
    ├── MainCamera
    ├── Road [RoadView；无玩法 Collider]
    ├── ArmyContainer [固定场景容器]
    │   └── ArmyRoot [ArmyController；序列化绑定选择并实例化的 PF_Army_000；每局从世界原点开始]
    ├── LevelSystems
    │   ├── LevelManager
    │   └── SpawnManager
    ├── MonsterRoot [EnemyManager]
    ├── ObstacleRoot [ObstacleManager]
    │   ├── GateRoot
    │   └── PropRoot
    ├── BulletRoot [BulletManager]
    ├── InputAdapter [GameplayInputAdapter；实现 IGameplayInputController]
    ├── UI [Gameplay Canvas；GraphicRaycaster]
    │   └── TouchDragArea [PF_UI_TouchDragArea；TouchDragInput；horizontalMultiplier = 1]
    ├── EventSystem [EventSystem；StandaloneInputModule]
    └── VFXRoot
```

`GameplayRoot` 下的对象均属于当前 `LevelRunId`，在 Gameplay 场景卸载时清理。`ArmyContainer` 是固定场景容器；GameplaySceneEntry 使用序列化 `ArmyPrefabBinding[]` 按固定 `ArmyId = 0` 选择 Prefab，并在其下实例化唯一 `ArmyRoot [ArmyController]`。ArmyRoot 每局重置到世界坐标 `(0,0,0)`，业务状态与序列化槽位引用由 ArmyController 负责，Army 不进入 PoolService。`Road` 根据 LevelConfig 的唯一 `roadBounds` 提供视觉，不设置玩法 Collider。`MonsterRoot` 保存当前敌人实例，`EnemyManager` 负责生成登记、存活统计和回收；`ObstacleRoot` 下的 Gate/Prop 由 `ObstacleManager` 统一登记、查询和回收；`BulletRoot` 的 BulletManager 负责子弹类型池引用、活动集合和回收。

首轮玩法验证不创建 Gameplay HUD、胜负面板或计时文本。Canvas 只承载 Input 模块的 TouchDragArea；Gate 自身使用世界空间单个 TMP 调试文本显示当前状态。完整最小绑定见 [PrefabSpecifications](../04_Assets/PrefabSpecifications.md)。

LevelManager 是 Gameplay 逻辑帧阶段顺序的唯一协调者。Army、Enemy、Obstacle 和 Bullet Manager 仍拥有自己的规则和集合，但不通过独立 Update 推进核心移动、命中、接触或攻击；LevelManager 在 Playing 中按 ADR-033 使用同步阶段接口驱动它们。

`InputAdapter` 是 Gameplay 场景组件，只在 `LevelManager.Playing` 期间启用并通过 `IHorizontalInputReceiver` 向当前 Army 发送横向输入倍率。当前只消费相对拖拽，设备触屏与 Editor 左键共用 UGUI Pointer 路径；键盘/手柄延后。它不跨场景保留，不依赖 TimeService，也不直接修改 ArmyRoot Transform。

`TouchDragArea` 是 `Assets/Prefabs/UI/PF_UI_TouchDragArea.prefab` 的实例，虽然位于 Gameplay Canvas/UI 层级中，但职责归属 Input 模块。Prefab 根为全屏拉伸 RectTransform，带透明且开启 Raycast Target 的 Image 和 TouchDragInput；它不包含 Canvas、EventSystem、InputAdapter 或 Army 引用。TouchDragInput 通过 UGUI Pointer 回调采集相邻位置的水平拖动差值，使用 RectTransform 宽度和未缩放帧时间归一化，并将结果交给 `InputAdapter`；跨模块传递仍使用同步命令，不发布项目事件。灵敏度系数 `horizontalMultiplier` 默认值为 `1`。详细规则见 [Input 模块](../02_Modules/Input/README.md)、[ADR-028](../06_Decisions/ADR-028-MvpRelativeDragInput.md) 和 [ADR-036](../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md)。

Gameplay Canvas 必须持有 GraphicRaycaster；GameplayScene 中恰好一个 EventSystem 使用 StandaloneInputModule。GameplayInputAdapter 通过 Inspector 引用 TouchDragInput 实例，并在场景装配时通过 `Initialize(IHorizontalInputReceiver)` 绑定本局 Army。LevelManager 每个 Playing 帧在 Army 移动与发射前显式调用 `TickInput(unscaledDeltaTime)`，不依赖 MonoBehaviour 的隐式同类 `Update` 顺序。

`GameplaySceneEntry` 通过 Inspector 持有本场景 Manager、RoadView、Input Adapter、TouchDragInput、Gameplay Canvas、EventSystem、ArmyContainer、序列化 Army Prefab 绑定和固定 Root 引用；SceneService 的 Unity 适配器向它注入 `LevelConfigSnapshot`、`LevelId`、`LevelRunId` 和所需最小服务。Entry 必须先取得 `TbArmy.Id = 0` 快照、解析唯一 `ArmyId = 0` Prefab、实例化并校验槽位绑定，再向 Input 注入最小 `IHorizontalInputReceiver`、向其他模块注入所需的 IArmyController。所有配置、Prefab、Collider、Layer 和 Inspector 引用在调用 Manager `StartRun` 前集中校验；错误直接记录并停止 Ready。完成校验、Manager `StartRun`、输入初始化、订阅与 `LevelManager.Preparing` 后才允许发布 `AppSceneReady(Gameplay)`；Gameplay 对象随后等待 `LevelRunStarted` 才进入 `Playing`。

军队固定在屏幕下方；怪物、Gate 和 Prop 从道路上方生成并通过自身移动向下推进。玩法 Prefab 的 `Collider2D` 由 Inspector 绑定并按职责配置 Layer；对象移动和碰撞结算不依赖 Dynamic Rigidbody2D 的自动回调。

## 生命周期边界

```text
BootstrapScene 创建 GlobalRoot
→ GlobalBootstrap 按 Create、Connect、Start 初始化全局服务
→ SceneService 同步 Additive 加载 MainMenuScene，MainMenuSceneEntry Ready
→ 异步卸载 MainMenuScene，再同步加载 LevelSelectScene，LevelSelectSceneEntry Ready
→ 异步卸载 LevelSelectScene，再同步加载 GameplayScene
→ GameplaySceneEntry 完成 LevelManager.Preparing
→ AppSceneReady(Gameplay) 后 GameStateService 发布 LevelRunStarted 并进入单局
→ 单局完成并清理 GameplayRoot 下的运行时对象
→ SceneService 异步卸载 GameplayScene，再同步加载 LevelSelectScene
→ GlobalRoot 与全局服务继续保留
```

应用流程由 `GameStateService` 唯一推进，Gameplay 单局状态由 `LevelManager` 管理；`GlobalBootstrap`、`SceneService` 和 `LevelManager` 都不得替代 `GameStateService` 推进应用状态。

`GameStateService` 与 `SceneService` 的协作、失败和幂等规则见 [应用流程](ApplicationFlow.md)。二者可以由同一个入口装配，但不合并实现职责。

## 关联决策

- [ADR-010：统一运行时对象命名](../06_Decisions/ADR-010-CanonicalRuntimeNames.md)
- [ADR-011：应用流程与游玩会话生命周期](../06_Decisions/ADR-011-ApplicationFlowAndGameplaySession.md)
- [ADR-019：应用流程公共接口与场景握手](../06_Decisions/ADR-019-ApplicationFlowContract.md)
- [ADR-025：场景层级与运行时职责命名](../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md)
- [ADR-028：MVP 触屏相对拖动输入](../06_Decisions/ADR-028-MvpRelativeDragInput.md)
- [ADR-036：MVP 最小拖拽输入实现切片](../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md)
- [ADR-032：MVP 应用场景入口、切换协议与分阶段初始化](../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md)
