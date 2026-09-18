# ADR-026：程序集边界与跨层通信

## 状态

Accepted（工程实现 Deferred）

## 日期

2026-09-18

## 背景

ADR-024 已确认“表现层 → 玩法层 → 全局基础层”的允许依赖方向。后续需要用 Unity Assembly Definition 在编译期落实该边界，同时保留依赖倒置、表现反馈和跨模块协作能力。

当前仍处于文档优先阶段，尚未获得创建或调整 Unity 脚本、目录和 `.asmdef` 的工程实现授权。过早划分会在类型和目录尚未落地时制造无法验证的绑定和序列化风险。

## 决策

- 将程序集划分确认为 MVP 后需实施的工程化要求，当前只记录边界与验收标准，不创建 `.asmdef`。
- 目标采用 `Game.Contracts`、`Game.Foundation`、`Game.Gameplay`、`Game.Presentation` 和 `Game.Composition` 五个粗粒度逻辑程序集。不默认按 Army、Gate、Prop、Monster 等单模块继续拆分。
- `Game.Foundation` 不引用玩法和表现程序集；`Game.Gameplay` 不引用表现程序集；`Game.Presentation` 可以引用玩法公开契约。
- `Game.Composition` 作为最外层装配边界可以同时引用各层具体实现，但只负责创建、注入和生命周期连接，不承担玩法规则。该边界不是新的通用 `GameManager`，不扩大 `GlobalBootstrap` 已确认的职责。
- 需要返回值、失败结果或确定顺序的命令与查询使用类型化同步接口。
- 事件只表示已经发生的事实。事件发布者的核心结果在零订阅者时仍必须正确；不使用“请求某模块修改状态”的事件伪装必须执行的反向调用。
- 下层策略必须使用外层能力时，由需要该能力的一侧或公共契约定义最小接口，由外层实现，再由 Composition 注入。
- 需要协调多个状态所有者的用例使用职责明确的协调器；不把业务流程塞入 EventBus 处理器、Composition 或通用管理器。

## 不采用

- 不在当前文档阶段提前创建空 `.asmdef` 或移动尚未实现的脚本。
- 不为了形式上解除程序集引用，把同步命令、查询或必须成功的操作改成事件。
- 不为每个玩法模块默认创建独立程序集。
- 不让 `Game.Contracts` 收容无明确所有模块或不跨程序集的便利类型。

## 影响

- 当前 Unity 工程结构不变，设计状态不代表程序集已实现。
- 后续工程实现需同时验证编译、EditMode 测试、启动场景、Inspector 引用、Prefab/场景脚本引用和完整游玩闭环。
- ADR-014 中 Gate/Prop 同步调用 `IArmyController` 并在状态变更后发布事实事件的方式作为标准示例。

## 关联文档

- `../00_Project/Roadmap.md`
- `../01_Architecture/SystemOverview.md`
- `../01_Architecture/AssemblyBoundaries.md`
- `../01_Architecture/EventSystem.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `ADR-014-SharedRuntimeContractBaseline.md`
- `ADR-024-LayerDependencyDirection.md`
- `ADR-025-SceneHierarchyAndRuntimeRoleNaming.md`
