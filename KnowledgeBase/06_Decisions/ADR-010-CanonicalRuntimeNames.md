# ADR-010：统一运行时对象命名

## 状态

Accepted

## 日期

2026-09-14

## 背景

知识库同时使用了 `Army`、`ArmyController`、`ArmyManager`，以及 `Level`、`LevelController`、`LevelManager` 表示运行时对象；事件目录还使用 `GameState` 作为全局状态服务名称。这会导致模块职责、场景对象和公共事件发布者无法一一对应。

## 决策

- `ArmyController` 是军队运行时控制器，负责军队人数、槽位、移动、射击和装备状态。
- `LevelManager` 是关卡运行时管理器，负责本局时间轴、终局判断、停止本局逻辑和结果提交；应用级重开与场景切换由 `GameStateService`、`SceneService` 按 ADR-011 负责。
- `EnemyManager` 是敌人实例管理器，负责敌人的生成登记、存活统计和回收。
- `GameStateService` 是全局状态服务，负责应用流程状态；其与 `LevelManager` 的状态边界由 ADR-011 定义，`Victory` 和 `GameOver` 作为结果事实而不是应用页面状态。
- `Army`、`Level`、`Monster` 等名称仅表示模块或设计领域概念，不作为上述运行时类型的替代名称。
- 场景层级、事件目录、ADR 和模块间运行时依赖均使用上述唯一名称。

## 影响

- 事件发布者和监听者可以映射到唯一运行时对象或服务。
- `LevelManager` 与 `GameStateService` 的职责按 ADR-011 的应用流程和单局会话契约协作，但不再使用 `LevelController` 或 `GameState` 作为别名。
- 既有模块标题和配置表消费者保留领域模块名，不要求将 `Army`、`Level` 等目录名改为运行时类型名。

## 关联文档

- `../03_SharedContracts/NamingRules.md`
- `../03_SharedContracts/EventCatalog.md`
- `../01_Architecture/EventSystem.md`
- `../01_Architecture/SceneStructure.md`
