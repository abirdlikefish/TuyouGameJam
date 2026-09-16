# 命名规则

- 脚本使用 PascalCase，例如 `ArmyController.cs`。
- 运行时对象命名固定为：`ArmyController`（军队控制器）、`LevelManager`（关卡生命周期）、`EnemyManager`（敌人实例管理）、`GameStateService`（全局状态服务）。`Army`、`Level`、`Monster` 等名称仅表示模块，不作为这些运行时类型的替代名称。
- 私有字段使用 camelCase，序列化字段使用明确名称。
- 事件以 `On` 开头的 C# 事件或以过去式命名的事件消息保持一致。
- Prefab 使用功能前缀：`PF_Army`、`PF_Gate_Additive`、`PF_Gate_Element`、`PF_Monster_Normal`。
- 配置使用 `CFG_` 前缀，材质使用 `MAT_`，特效使用 `VFX_`。
- 枚举值使用 PascalCase，例如 `Gameplay`、`GameOver`。
- 敌人 Prefab 的碰撞体使用稳定职责名称：`BodyCollider`、`AttackCollider`、`TargetSensor`。
