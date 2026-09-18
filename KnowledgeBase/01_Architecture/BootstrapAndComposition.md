# 启动与装配

## 状态

ContractReady（设计状态，不代表已有 Unity 实现）

## 职责

`GlobalBootstrap` 是应用入口和全局服务的 Composition Root，只负责：

- 在 `BootstrapScene` 中确认唯一 `GlobalRoot`，并使其跨场景保留。
- 创建服务实现，按稳定顺序注入依赖并完成初始化。
- 绑定 Inspector 提供的 `LevelCatalog`、资源注册表和唯一的 Luban `cfg.Tables` 实例。
- 在 ConfigService 为 `Ready` 后，调用一次 `GameStateService.NotifyInitializationReady()`。
- 在应用退出或入口销毁时取消全局订阅和定时任务，并按初始化逆序释放已创建服务。

`GlobalBootstrap` 不选择关卡、不推进 MainMenu/LevelSelect/Gameplay 状态、不加载 Gameplay 场景，也不承担玩法规则。

## MVP 装配范围

```text
GlobalRoot
├── GlobalBootstrap [Composition Root]
└── ServiceHost
    ├── EventBus
    ├── TimeService
    ├── GameStateService
    ├── SceneService
    ├── ConfigService
    └── PoolService
```

Gameplay 场景模块不进入 `ServiceHost`。MVP 也不创建 `AudioService`、`SaveService` 或 `DebugService`。

服务可以是纯 C# 对象，不要求每个服务对应一个 GameObject。`ServiceHost` 表示所有权和注册位置，不是新的通用 `GameManager`。

## 实例与注入规则

- Composition Root 为每种 MVP 服务创建一个应用级实例，并持有其完整生命周期；服务唯一性不通过各类型的静态 `Instance` 实现。
- 纯 C# 服务优先在构造时取得最小接口。需要 Unity 场景 API 的组件由 `GlobalBootstrap` 或场景装配入口通过明确的 `Initialize(...)` 连接，不使用运行时 `Find`。
- 不提供可被任意业务对象访问的通用 Service Locator。Gameplay 场景装配入口可以读取一次全局服务接口并向场景 Manager 分发，但不能把完整服务集合继续传给 Bullet、Monster、Gate 或 Prop。
- 场景内固定组件与资源使用 Inspector 引用；应用级服务使用接口注入；单次会话数据使用参数或初始化快照。这三种依赖不得混成静态全局状态。
- 当前 `LubanTables.Instance` 是已有加载适配器，不是其他服务采用静态单例的先例。Bootstrap 只读取一次并把同一 `cfg.Tables` 交给 ConfigService；其他模块不直接访问该静态入口。

## 初始化顺序

```text
确认唯一 GlobalRoot
→ 创建 EventBus
→ 创建 TimeService
→ 创建 ConfigService
→ 创建薄 SceneService
→ 创建 GameStateService，并注入 IConfigService、ISceneService、ITimeService 与 IEventBus
→ 创建 PoolService
→ 注册应用流程事件
→ ConfigService.Initialize(LevelCatalog, Tables, ResourceRegistry)
→ ConfigService Ready
→ GameStateService.NotifyInitializationReady()
```

具体 `LevelConfig` 不在全局初始化时绑定；它在 LevelSelect 确定关卡后由 ConfigService 返回，再经 SceneService 注入 Gameplay。

## 失败与幂等

- 如果发现已有有效 `GlobalRoot`，新入口不得再创建第二套服务；重复入口应停止自身初始化。
- 任一必需 Inspector 引用、配置表或资源注册项无效时，ConfigService 发布稳定失败事实，应用保持 `Initializing`。
- 初始化失败后不得调用 `NotifyInitializationReady`，不得加载 Gameplay，也不得使用缺省配置继续运行。
- 重复成功通知由 GameStateService 幂等拒绝，不能重复创建 MainMenu 定时器。
- 已完成初始化的服务在清理时按逆序释放；只清理本入口实际创建的对象和订阅。
- 全局服务不得长期持有已卸载 Gameplay 场景对象的具体引用。

## 非职责

- 不使用运行时搜索作为缺失 Inspector 引用的静默兜底。
- 不在 Bootstrap 中实现应用状态机、关卡终局、生成、对象重置或表现逻辑。
- 不为了统一访问而把 Gameplay Controller/Manager 注册为跨场景单例。
- 不为减少参数传递而暴露 `XxxService.Instance` 或全局可变服务集合。
- 不初始化 Deferred 服务或创建空占位实现。

## 验收标准

- 冷启动只存在一个 `GlobalRoot` 和一套全局服务。
- ConfigService 未 Ready 时应用始终处于 `Initializing`。
- 初始化成功只通知 GameStateService 一次。
- Gameplay 重开不会创建重复服务，也不会遗留对已卸载场景对象的引用。
- MVP 启动层级中不存在 AudioRoot、SaveService 或 DebugService。
- 服务初始化与清理顺序可通过 EditMode 测试或测试替代实现复现。
- GameStateService 可以注入假的 Config、Scene、Time 和 EventBus 实现进行纯流程测试，不依赖静态全局状态。

## 关联文档

- `GlobalServices.md`
- `ApplicationFlow.md`
- `ConfigurationSystem.md`
- `SceneStructure.md`
- `AssemblyBoundaries.md`
- `../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
