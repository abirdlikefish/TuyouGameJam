# ADR-025：场景层级与运行时职责命名

## 状态

Accepted

## 日期

2026-09-18

## 背景

现有场景结构图把 `ArmyController`、`LevelManager` 等运行时组件名称与 `GateRoot`、`MonsterRoot` 等 GameObject 容器名称并列展示，但没有说明二者的类型差异。图中也只用一句话提到跨场景的 `GlobalRoot`，且缺少已由 ADR-005 和 ADR-010 确认的 `EnemyManager`，容易产生以下误解：

- `Controller` 与 `Manager` 是同义后缀，需要机械统一。
- `Root`、`Controller`、`Manager` 都表示相同类型的场景节点。
- 需要再增加一个包揽全局流程和单局玩法的 `GameManager`。
- Gameplay 场景卸载时可以同时销毁应用级服务。

## 决策

### 运行时职责后缀

- `Controller` 控制一个明确的玩法聚合体。当前 `ArmyController` 负责军队人数、槽位、移动、射击和装备状态。
- `Manager` 管理关卡内的一段生命周期、时间轴或同类运行时实例集合。`LevelManager`、`SpawnManager`、`EnemyManager` 和 `ObstacleManager` 保留各自既有名称与职责。
- `Service` 提供跨场景保留的应用级能力，例如 `GameStateService`、`SceneService`、`TimeService` 和 `ConfigService`。
- `Root` 只表示 GameObject 层级或运行时实例容器，不单独拥有业务规则。业务行为由挂载组件或被注册的纯 C# 对象承担。
- `Bootstrap` 只负责创建、注册和初始化全局服务，并在初始化成功后把应用流程交给 `GameStateService`。
- 不为了字面一致而把 `Controller`、`Manager` 和 `Service` 统一为同一后缀。

### 跨场景层与单局层

- 构建入口使用 `BootstrapScene`。该场景提供唯一的 `GlobalRoot`，并由 `GlobalBootstrap` 调用 `DontDestroyOnLoad` 使其跨场景保留。
- `GlobalRoot` 持有或注册全局服务；纯 C# 服务不要求各自占用一个 GameObject。
- 场景重载或回到 Gameplay 时不得创建第二个 `GlobalRoot`。直接从 Gameplay 场景进入 Play Mode 的编辑器辅助流程如需自举，必须复用同一初始化入口并执行重复实例保护。
- Gameplay 场景中的 `GameplayRoot`、玩法控制器、管理器、运行时对象容器、UI 和 VFX 均属于当前 `LevelRunId`，随 Gameplay 场景卸载；全局服务不得长期持有已卸载场景对象的具体引用。
- `EnemyManager` 必须在 Gameplay 场景结构中明确出现，并与 `MonsterRoot` 的实例容器职责区分。

### 不设置通用 GameManager

不新增笼统的 `GameManager`。其可能承担的职责已经分别归属：

- `GlobalBootstrap`：全局服务初始化。
- `GameStateService`：应用流程推进。
- `SceneService`：Gameplay 场景加载与卸载。
- `LevelManager`：当前单局生命周期与终局提交。

如后续出现新职责，应先归入明确的现有模块，或在有独立生命周期和公共契约时新增专用服务，不使用 `GameManager` 作为兜底容器。

## 影响

- `SceneStructure.md` 分别展示常驻层和 Gameplay 单局层，并标注 GameObject 容器与运行时组件。
- `NamingRules.md` 增加后缀语义和禁止通用 `GameManager` 的约束。
- 现有 `ArmyController`、`LevelManager`、`SpawnManager`、`EnemyManager`、`ObstacleManager`、`GameStateService` 等公共名称不变，不产生接口重命名。
- 本决策只确定文档结构和实现边界，不表示 Unity 场景、Prefab 或脚本已经创建。

## 关联文档

- `../01_Architecture/SceneStructure.md`
- `../01_Architecture/SystemOverview.md`
- `../01_Architecture/GlobalServices.md`
- `../03_SharedContracts/NamingRules.md`
- `ADR-005-MonsterCombatAndManager.md`
- `ADR-010-CanonicalRuntimeNames.md`
- `ADR-011-ApplicationFlowAndGameplaySession.md`
- `ADR-019-ApplicationFlowContract.md`
