# 命名规则

- 脚本使用 PascalCase，例如 `ArmyController.cs`。
- 运行时对象命名固定为：`ArmyController`（军队控制器）、`LevelManager`（关卡生命周期）、`EnemyManager`（敌人实例管理）、`GameStateService`（全局状态服务）。`Army`、`Level`、`Monster` 等名称仅表示模块，不作为这些运行时类型的替代名称。
- 运行时职责后缀按语义使用：`Controller` 控制一个明确的玩法聚合体，`Manager` 管理关卡内生命周期、时间轴或实例集合，`Service` 提供跨场景应用能力，`Bootstrap` 负责创建和初始化，`Root` 只表示 GameObject 层级或实例容器。
- 不为了字面一致而把 `Controller`、`Manager` 和 `Service` 统一为同一后缀；不使用职责笼统的 `GameManager`。跨场景流程使用 `GameStateService` 和 `SceneService`，单局流程使用 `LevelManager`。
- 场景对象需要同时表达容器和行为时，使用“领域 `Root` + 职责组件”的形式，例如 `ArmyRoot [ArmyController]`、`MonsterRoot [EnemyManager]`、`ObstacleRoot [ObstacleManager]`。
- 私有字段使用 camelCase，序列化字段使用明确名称。
- 事件以 `On` 开头的 C# 事件或以过去式命名的事件消息保持一致。
- Prefab 使用功能前缀：`PF_Road_Default`、`PF_Army_000`、`PF_Gate_Additive`、`PF_Gate_Element`、`PF_Monster_Chick`、`PF_Monster_Hen`、`PF_Monster_Rooster`、`PF_Bullet`、`PF_UI_TouchDragArea`。Army Prefab 的三位数字对应 ArmyId；MVP 固定使用 `PF_Army_000`。Gameplay 拖拽区域固定使用 `PF_UI_TouchDragArea`。
- 配置使用 `CFG_` 前缀，材质使用 `MAT_`，特效使用 `VFX_`。
- 枚举值使用 PascalCase，例如 `Gameplay`、`GameOver`。
- 敌人 Prefab 的碰撞体使用稳定职责名称：`BodyCollider`、`AttackCollider`；MVP 不使用 `TargetSensor`。

场景层级和后缀职责的完整约束见 [ADR-025](../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md)。
