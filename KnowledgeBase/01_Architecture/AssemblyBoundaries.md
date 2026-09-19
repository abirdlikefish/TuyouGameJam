# 程序集边界

## 当前状态

- 设计状态：`Accepted`
- 工程状态：`Deferred`
- 当前不创建 `.asmdef`，不移动脚本，不改变场景、Prefab 或序列化引用。
- 待进入 Unity 工程实现且公共契约稳定后，再按本文创建程序集并进行编译验证。

## 目标

用 Unity Assembly Definition 在编译期约束分层依赖，防止全局基础层引用具体玩法或表现实现，以及玩法层引用 UI、音效或特效实现。程序集边界用于落实现有分层，不改变模块的状态所有权和运行时流程。

## 目标程序集

| 逻辑程序集 | 职责 | 允许的引用 |
|---|---|---|
| `Game.Contracts` | 跨程序集公共接口、事件载荷、不可变运行时快照、DTO 和稳定 ID；不放置具体运行时实现或配置资产 | 尽量无 Unity 依赖；不引用其他项目程序集 |
| `Game.ConfigGenerated` | Luban 自动生成的 `cfg.Tables`、表行和容器类型；只作为配置实现细节 | 不引用 Gameplay、Presentation 或 Composition |
| `Game.Foundation` | 时间、事件、配置资产与转换、应用状态、场景和对象池等全局基础能力 | `Game.Contracts`、`Game.ConfigGenerated` |
| `Game.Gameplay` | Army、Gate、Prop、Monster、Bullet、Level、Spawn 和 Obstacle 等核心玩法 | `Game.Contracts`、`Game.Foundation` |
| `Game.Presentation` | UI、摄像机、音效和特效 | `Game.Contracts`、`Game.Gameplay`；不得让玩法层反向引用本程序集 |
| `Game.Composition` | 在最外层创建实现、完成注入与场景绑定 | 可引用上述所有程序集；不承担玩法规则 |

```text
Game.Composition
        │ 装配
        ├──→ Game.Presentation ──→ Game.Gameplay ──→ Game.Foundation
        ├──→ Game.Gameplay
        └──→ Game.Foundation
                                  └──→ Game.ConfigGenerated

Game.Contracts 可被各层引用，但不引用各层具体实现。
```

`Game.Composition` 是编译期和运行时的最外层装配边界，不等于新增一个通用 `GameManager`，也不改变 `GlobalBootstrap` “只初始化和注册全局服务”的职责。实现时可由现有入口和职责明确的场景装配器承担绑定，不因程序集划分增加笼统管理器。

全局服务由 Composition 创建为应用级唯一实例，再以最小接口注入消费者。程序集边界不通过静态 `XxxService.Instance`、运行时 `Find` 或通用 Service Locator 连接；否则依赖会绕过编译期引用方向和测试替代边界。场景装配入口可以集中取得服务，但只能向下分发消费者实际需要的接口。

`Game.Contracts` 只收录真正跨程序集的稳定契约，并按所有模块维护命名空间和文档归属，不作为无归属类型的“杂项”程序集。

## LevelConfig 资产与快照边界

`LevelConfig` 是 Unity `ScriptableObject` 编辑资产，不是公共运行时契约。它与 `LevelCatalog`、三类可序列化生成条目结构、具体 `ConfigService` 一并放入 `Game.Foundation`。`Game.Contracts` 只定义不可变的 `LevelConfigSnapshot` 和三类生成条目快照；`Game.Gameplay`、`ISceneService` 与场景入口只传递快照。

`IConfigService` 是查询接口，不包含 `LevelCatalog`、`cfg.Tables`、`IResourceRegistry` 等具体初始化参数。最外层 `Game.Composition` 可以引用 Foundation 实现和 `Game.ConfigGenerated`，并在启动时直接调用具体 `ConfigService.Initialize(...)`，随后只向消费者分发 `IConfigService` 或更小的类型化 Provider。由此避免 Contracts/Foundation 为取得 `LevelConfig` 而反向引用 Gameplay，也避免 Luban 生成类型泄漏到 Contracts 或 Gameplay。

建议文件归属：

```text
Assets/Scripts/Game/Contracts/Configuration/LevelConfigSnapshot.cs
Assets/Generated/Game.ConfigGenerated.asmdef
Assets/Scripts/Game/Foundation/Configuration/LevelCatalog.cs
Assets/Scripts/Game/Foundation/Configuration/LevelConfig.cs
Assets/Scripts/Game/Foundation/Configuration/LevelSpawnEntries.cs
Assets/Scripts/Game/Foundation/Configuration/ConfigService.cs
Assets/Scripts/Game/Gameplay/Level/LevelManager.cs
```

`Assets/Generated/Game.ConfigGenerated.asmdef` 是生成输出目录的稳定程序集边界，不修改其中自动生成的 `.cs`。若 Luban 生成流程会清空整个目录，则应把 asmdef 纳入生成源或生成脚本复制步骤，不能在每次生成后手工修补。

## 通信方式

| 需求 | 方式 | 约束 |
|---|---|---|
| 需要立即执行、返回值、失败结果或确定顺序 | 类型化同步接口 | 调用方只依赖状态所有者暴露的最小契约 |
| 通知已经发生的事实，监听者可为零个或多个 | 过去式事实事件 | 发布者的核心结果不得依赖监听者存在或执行顺序 |
| 下层策略必须使用外部能力 | 依赖倒置接口 | 抽象由需要该能力的一侧或公共契约定义，外层实现，由 Composition 注入 |
| 一个用例需要按顺序协调多个状态所有者 | 职责明确的用例协调器 | 协调器依赖接口；Composition 只装配，不代替协调器承担业务流程 |

事件系统不是绕过程序集依赖规则的“反向调用通道”。如果发布者要求必须有某个监听者执行、必须取得返回值，或必须按特定顺序完成才能继续，就不得将该交互建模为事件。

事件适用性的最小检查是：**当订阅者数量为零时，发布者负责的核心结果仍然必须正确。**

## 实现时机

满足以下条件后再实施程序集划分：

1. 用户明确授权 Unity 工程实现。
2. 核心脚本目录和公共契约已稳定，可以一次性确定粗粒度程序集边界。
3. 可以同时验证编译、EditMode 测试、启动场景、Inspector 引用和 Prefab/场景脚本引用。

不按 Army、Gate、Prop、Monster 等单个玩法模块拆分独立程序集，除非后续出现可独立复用、独立发布或需要长期并行编译的真实需求。

## 实现验收标准

- 不存在循环程序集引用。
- `Game.Foundation` 不引用 `Game.Gameplay`、`Game.Presentation` 或其具体类。
- `Game.Contracts` 和公共接口不引用 `LevelConfig`、Luban 生成类型或其他 Foundation/Gameplay 具体类型。
- `Game.ConfigGenerated` 不引用任何 Gameplay/Presentation/Composition 类型；Luban 生成类型只允许被 Foundation 配置实现与 Composition 启动装配使用。
- Gameplay 关卡启动链只消费 `LevelConfigSnapshot`；Composition 仅在装配阶段调用具体 ConfigService 初始化入口。
- `Game.Gameplay` 不引用 `Game.Presentation` 或其具体类。
- 必须的反向能力通过依赖倒置接口注入，不使用请求型事件伪装同步调用。
- 玩法在无 UI、音效和特效订阅者时仍能完成正确结算。
- `Game.Composition` 只负责装配和生命周期连接，不包含玩法计算。
- Gameplay、Presentation 和池对象代码中不存在对全局服务静态 `Instance` 或通用 Service Locator 的依赖。
- Unity 编译、相关 EditMode 测试、启动场景和完整游玩闭环验证通过，且现有 Inspector、Prefab 和场景脚本引用没有丢失。

## 关联决策

- [ADR-014：跨模块运行时契约基线](../06_Decisions/ADR-014-SharedRuntimeContractBaseline.md)
- [ADR-024：分层依赖方向与旁路扩展](../06_Decisions/ADR-024-LayerDependencyDirection.md)
- [ADR-025：场景层级与运行时职责命名](../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md)
- [ADR-026：程序集边界与跨层通信](../06_Decisions/ADR-026-AssemblyBoundariesAndCommunication.md)
- [ADR-042：以 LevelConfigSnapshot 闭合程序集依赖](../06_Decisions/ADR-042-LevelConfigSnapshotAssemblyBoundary.md)
