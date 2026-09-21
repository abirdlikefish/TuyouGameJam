# ADR-027：MVP 全局服务范围、访问方式与应用流程边界

## 状态

Accepted

## 日期

2026-09-18

> 后续决策：ADR-051 与 ADR-053 分别为 MainMenu、LevelSelect 增加场景内 View 和按钮命令，但不增加跨场景输入服务；两者均不再使用临时自动推进。

> 后续决策：ADR-062 已启用由 Composition 注入的 `JsonPlayerProgressStore`，取代本文关于 SaveService 和本地关卡进度继续延后的部分；Audio、Debug 和设置持久化仍延后。

## 背景

现有全局服务清单同时包含 MVP 必需能力、建议能力和后续扩展，容易让实现阶段误以为每个服务都需要在首个可玩版本中创建。时间系统的公共接口也保留了暂停、倍率和局部时停能力，但这些能力并不属于当前 MVP；测试清单却仍包含相应验收项。另一方面，`GameStateService` 与 `SceneService` 共同完成应用流程，但二者的状态所有权和 Unity 场景操作职责不能因此混为一体。

全局服务还缺少实例所有权和访问规则。“全局”可能被误解为每个服务都要实现静态 `Instance`，使模块隐藏依赖、绕过 Composition、难以替换测试实现，并在场景重载或关闭 Domain Reload 时遗留静态状态。需要区分“应用生命周期内唯一实例”和“静态单例访问模式”。

## 决策

### MVP 当时不实现音频、存档和调试服务

- MVP 完全无声音，不创建或初始化 `AudioService`、`AudioRoot`、`AudioMixer`、音频事件监听器、音量设置或 AudioClip 绑定。
- `SaveService` 继续按 ADR-017 延后；MVP 不创建存档文件，也不持久化关卡进度或设置。
- `DebugService` 延后；在出现明确的调试数据、指令和构建范围前，不创建通用调试单例或公共接口。
- 现有玩法事实事件不因缺少音频和调试监听者而改变。零表现监听者时，核心玩法结果必须保持正确。

### GameStateService 与 SceneService 保持分离

- `GameStateService` 继续作为 `AppFlowState`、选中关卡、`LevelRunId`、流程定时器和终局推进的唯一所有者。
- `SceneService` 保持为薄场景协调器，只负责 MainMenu、LevelSelect、Gameplay 场景切换、入口绑定、卸载及成功/失败事实发布；应用场景范围由 ADR-032 扩展。
- 二者可以由同一个 `GlobalBootstrap` 创建并在同一份应用流程架构文档中描述，但不合并为一个承担状态机和 Unity 场景操作的服务。
- `GameStateService` 通过 `ISceneService` 调用场景能力；测试可以使用替代实现验证状态转换，不依赖真实 Unity 场景加载。

### 应用级唯一实例与显式注入

- MVP 必需服务在应用生命周期内各有一个活动实例，由唯一的 `GlobalBootstrap` / Composition Root 创建、持有和清理。
- 应用级唯一实例不要求静态 `Instance`、独立 GameObject 或 MonoBehaviour。适合的服务使用纯 C# 实现；Unity 适配器仅在确实需要场景 API 或生命周期时挂载到 `ServiceHost`。
- 业务代码不得通过 `XxxService.Instance`、运行时 `Find` 或通用 Service Locator 获取服务。纯 C# 对象优先构造注入，场景 MonoBehaviour 使用明确的 `Initialize(...)` 或 Inspector 引用。
- Composition Root 只完成创建、注入和生命周期连接，不承担应用状态机或玩法规则。Gameplay 场景装配入口只取得一次全局服务接口，再向 LevelManager、各 Manager 和表现适配器分发最小依赖。
- 池对象不注入完整服务集合，也不自行查找全局服务；对应 Manager 在获取实例后传入配置快照、`LevelRunId`、`RuntimeInstanceId` 和必要回调。
- 现有 `LubanTables.Instance` 只作为创建唯一 `cfg.Tables` 的过渡加载入口；除 Bootstrap/Config 适配边界外，其他模块统一依赖 `IConfigService`。

### Input 保持为场景适配器

- 当前 MainMenu 和 LevelSelect 自动跳过，MVP 没有跨场景输入消费需求，因此不创建全局 `InputService`。
- （已由 ADR-036 取代输入源与最小接收接口）Input Adapter 属于 Gameplay 场景，只在 `LevelManager` 的 `Playing` 阶段启用；本 ADR 当时定义为读取键盘/手柄横向值并调用 `IArmyController.SetHorizontalInput`。
- Army 使用 `TimeService` 和 `TbArmy.MoveSpeed` 计算移动；Input Adapter 不依赖 `TimeService`，不决定速度，也不自行修改 Transform。
- 触摸输入和多设备切换延后；启用前再决定是否需要跨场景输入服务。

> 后续变更：ADR-028 已将触屏相对拖动纳入 MVP，并取代上一条“触摸输入延后”的决定；Input 仍保持 Gameplay 场景适配器，不创建全局服务。

> 后续变更：ADR-036 将首个工程切片收窄为单一相对拖拽输入，键盘/手柄延后，并以 `IHorizontalInputReceiver` 和 `IGameplayInputController` 补齐最小工程契约；Input 的场景级生命周期不变。

### MVP 使用精简 TimeService

- 所有时间域倍率固定为 `1`；当前不实现暂停、减速、加速、局部时停或对象局部倍率。
- MVP 的 `ITimeService` 只暴露 `GetDeltaTime(TimeDomain)` 和 `Schedule(seconds, callback, domain)`；`TimerHandle` 必须支持取消。
- `GetTimeScale`、`SetTimeScale`、`PushPause`、`PauseToken`、`GamePaused` 和 `GameResumed` 不属于当前公共契约。未来启用相关玩法前，必须重新设计组合规则、接口、事件和测试。
- `RealTime` 只用于 MainMenu、LevelSelect 等不依赖 Gameplay 推进的流程定时器；Gameplay 及各玩法域当前返回倍率为 `1` 的正常步进。

本 ADR 取代 ADR-001、ADR-014 和 ADR-021 中“在当前 `ITimeService` 保留暂停与倍率扩展接口”的部分；这些 ADR 对时间来源、事实事件和其他运行时确定性规则仍然有效。

## 影响

- `GlobalServices.md` 的当前服务清单只列实际由 `GlobalRoot` 持有的 MVP 全局服务；Bootstrap、场景模块和 Deferred 候选分别由装配文档、模块文档和决策记录维护。
- `GlobalServices.md` 和启动装配文档明确应用级唯一实例、接口注入以及禁止普遍静态单例/Service Locator 的规则。
- `SceneStructure.md` 不再把 InputService 放在常驻 ServiceHost；Gameplay 场景持有 Input Adapter。
- 新增应用流程活文档，集中说明 `GameStateService` 与 `SceneService` 的协作，同时保持两个实现边界。
- `PublicInterfaces.md` 移除当前未实现的暂停与倍率方法及 `PauseToken`。
- `EventCatalog.md` 移除当前未实现的暂停事件；音频不作为 MVP 监听者。
- 时间系统和集成测试只验收固定倍率 delta、RealTime 定时和取消语义。
- AudioVFX 模块保留未来入口，但明确 Audio 为 Deferred；音频资源不属于 MVP 资源清单。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/ApplicationFlow.md`
- `../01_Architecture/TimeSystem.md`
- `../01_Architecture/SceneStructure.md`
- `../01_Architecture/BootstrapAndComposition.md`
- `../01_Architecture/AssemblyBoundaries.md`
- `../02_Modules/Input/README.md`
- `../02_Modules/AudioVFX/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/EventCatalog.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-017-DeferScoreAndSave.md`
- `ADR-019-ApplicationFlowContract.md`
- `ADR-028-MvpRelativeDragInput.md`
