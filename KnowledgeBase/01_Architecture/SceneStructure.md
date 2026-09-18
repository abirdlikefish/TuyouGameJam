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
    ├── Road
    ├── ArmyRoot [ArmyController]
    ├── LevelSystems
    │   ├── LevelManager
    │   └── SpawnManager
    ├── MonsterRoot [EnemyManager]
    ├── ObstacleRoot [ObstacleManager]
    │   ├── GateRoot
    │   └── PropRoot
    ├── BulletRoot
    ├── InputAdapter [GameplayInputAdapter]
    ├── UI
    │   └── TouchDragArea [TouchDragInput；horizontalMultiplier = 1]
    └── VFXRoot
```

`GameplayRoot` 下的对象均属于当前 `LevelRunId`，在 Gameplay 场景卸载时清理。`ArmyRoot` 是军队整体移动和槽位表现的容器，业务状态由 `ArmyController` 负责。`MonsterRoot` 保存当前敌人实例，`EnemyManager` 负责生成登记、存活统计和回收；`ObstacleRoot` 下的 Gate/Prop 由 `ObstacleManager` 统一登记、查询和回收。具体移动、HP 与接触规则仍由对象自身负责。

`InputAdapter` 是 Gameplay 场景组件，只在 `LevelManager.Playing` 期间启用并向 `IArmyController` 发送横向输入倍率。它汇总键盘/手柄和触屏输入，不跨场景保留，不依赖 TimeService，也不直接修改 ArmyRoot Transform。

`TouchDragArea` 虽然位于 Gameplay Canvas/UI 层级中，但职责归属 Input 模块。它通过 UGUI Pointer 回调采集相邻位置的水平拖动差值，使用 RectTransform 宽度和未缩放帧时间归一化，并将结果交给 `InputAdapter`；跨模块传递仍使用同步命令，不发布项目事件。灵敏度系数 `horizontalMultiplier` 默认值为 `1`，在对应 UI Prefab 的 Inspector 中配置。详细规则见 [Input 模块](../02_Modules/Input/README.md) 和 [ADR-028](../06_Decisions/ADR-028-MvpRelativeDragInput.md)。

`GameplaySceneEntry` 通过 Inspector 持有本场景 Manager 和固定组件引用；SceneService 的 Unity 适配器向它注入 `LevelConfig`、`LevelId`、`LevelRunId` 和所需最小服务。Entry 完成订阅与 `LevelManager.Preparing` 后才允许发布 `AppSceneReady(Gameplay)`；Gameplay 对象随后等待 `LevelRunStarted` 才进入 `Playing`。

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
- [ADR-032：MVP 应用场景入口、切换协议与分阶段初始化](../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md)
