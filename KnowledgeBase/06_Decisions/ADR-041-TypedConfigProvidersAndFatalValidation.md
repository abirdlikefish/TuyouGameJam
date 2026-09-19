# ADR-041：Gameplay 类型化配置查询与致命校验

## 状态

Accepted

## 日期

2026-09-20

## 背景

Army 与 Weapon 已有类型化 Provider 和不可变快照，但 Bullet、Enemy、Prop 仍被描述为通过完整 `IConfigService` 查询，公共接口没有相应方法和快照。若直接实现，各 Manager 只能访问静态 Luban Tables、自行转换生成行，或重复编写缺失配置的错误分支。

当前 MVP 不需要配置恢复、默认值、降级或重试。配置表、目录和关卡资产是应用启动前必须正确的只读输入；发现错误后继续运行只会把根因扩散到 Gameplay 初始化和运行时。

## 决策

### 五类最小查询接口

ConfigService 实现并向消费者分别注入：

```text
IArmyConfigProvider   -> ArmyConfigSnapshot
IWeaponConfigProvider -> WeaponConfigSnapshot
IBulletConfigProvider -> BulletConfigSnapshot
IEnemyConfigProvider  -> EnemyConfigSnapshot
IPropConfigProvider   -> PropConfigSnapshot
```

每个 Provider 只暴露 `GetXxxConfig(id)`。配置初始化成功后，调用者传入的 ID 必须已经通过目录、关卡和跨表引用校验；查询不返回默认值，也不要求 Gameplay Manager 编写 `TryGet` 失败恢复分支。

`IConfigService.TryGetLevelConfig` 保留：它用于应用层验证外部关卡选择请求，不等同于 Gameplay 对已校验表行的必得查询。

### 不可变快照

- `ArmyConfigSnapshot`：`Id`、`ArmyCountLimit`、`HpPerSoldier`、`MoveSpeed`。
- `WeaponConfigSnapshot`：`Id`、`FireInterval`、`BulletId`。
- `BulletConfigSnapshot`：`Id`、`Damage`、`MoveSpeed`。
- `EnemyConfigSnapshot`：`Id`、`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange`、`AttackCooldown`。
- `PropConfigSnapshot`：`Id`、`WeaponId`、`MaxHp`、`ContactDamage`、`MoveSpeed`。

ConfigService 在一次初始化中读取 Luban 生成行，完成数值、枚举、主键和跨表引用校验，并复制为自己的只读快照字典。Gameplay 不持有 Luban 生成行，不读取文件，不访问 `LubanTables.Instance`，也不修改快照。

### 单点 Fail-Fast

ConfigService 初始化时验证完整 `LevelCatalog`、目录内全部 `LevelConfig`、五类 Luban 表及当前已确认的跨表引用。遇到第一个错误时：

1. 使用 `Debug.LogError` 输出一个包含 `ConfigErrorCode`、稳定 Source、字段或条目索引和原因的错误。
2. 将自身状态设为 `Failed`。
3. 立即终止应用；Player 调用 `Application.Quit()`，Editor 停止 Play Mode。
4. 不调用 `NotifyInitializationReady`，不加载 MainMenu，不发布配置恢复事件，不收集第二套错误结果，不重试。

配置查询只允许在 `Ready` 状态执行。若代码在非 Ready 状态查询，或查询一个没有通过初始化引用链登记的 ID，属于程序不变量被破坏，直接抛出异常；调用方不捕获并降级为默认配置。

场景 Prefab、Collider、Layer 和 Inspector 引用不是 Luban/目录配置表错误，仍由 Bootstrap 或 GameplaySceneEntry 在装配阶段报告并阻止对应 Ready；本 ADR 不把所有装配错误都升级为应用退出。

## 不采用

- 不让 BulletManager、EnemyManager、ObstacleManager 直接访问 `cfg.Tables` 或静态 `LubanTables.Instance`。
- 不为每个 Manager 重复 `TryGet + LogError + StopRun`。
- 不返回零值快照、默认行或临时 ScriptableObject 继续运行。
- 不增加配置失败事件、错误 UI、重试状态机或错误聚合框架。

## 影响

- 关闭 `DES-042`。
- BulletManager 只依赖 `IBulletConfigProvider`；EnemyManager 只依赖 `IEnemyConfigProvider`；ObstacleManager 查询 Prop 时只依赖 `IPropConfigProvider`。
- ArmyController 继续只依赖 `IArmyConfigProvider` 和 `IWeaponConfigProvider`。
- Gate 继续不读取 Luban 表。
- ConfigService 是 Luban 行到运行时不可变快照的唯一转换边界。
- 本 ADR 将 ADR-039 中“配置错误只阻止 Ready”的范围收窄为场景/Prefab/Inspector 装配错误；Luban、LevelCatalog 和 LevelConfig 数据错误按本 ADR 直接终止应用。

## 验收标准

- 五类 Provider 和五类快照字段与 `ConfigurationTables.md` 一致。
- ConfigService Ready 后，相同 ID 重复查询返回相同值语义的不可变快照。
- 任一非法数值、重复 ID、缺失固定行或跨表/关卡引用缺失都会只记录首个可定位错误并终止应用。
- 配置错误不会进入 MainMenu 或 Gameplay，也不会创建默认配置、发布恢复事件或由 Manager 再记录同一错误。
- 移除所有静态 Tables 访问后，Army、Bullet、Monster、Prop 仍可只通过注入的最小 Provider 初始化运行时对象。

## 关联文档

- `../00_Project/DesignBacklog.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`
- `ADR-039-PrototypeValidationScope.md`
