# ADR-042：以 LevelConfigSnapshot 闭合程序集依赖

- 状态：Accepted
- 日期：2026-09-20
- 关联：DES-016、DES-033、DES-042、DES-048、ADR-012、ADR-026、ADR-041

## 背景

既有文档同时把 `LevelConfig` 描述为 Unity `ScriptableObject` 资产、`IConfigService` 的公共返回类型和 Gameplay 的运行时输入，并计划把该类型放在 Gameplay 目录。这样会导致 `Game.Contracts` 或 `Game.Foundation` 反向依赖 `Game.Gameplay`，与 ADR-026 的单向程序集边界冲突。

## 决策

1. `LevelConfig`、`LevelCatalog` 和三类生成条目资产结构属于 `Game.Foundation` 的配置实现层；它们可以依赖 Unity 序列化，但不得进入公共运行时接口。
2. `Game.Contracts` 定义不可变的 `LevelConfigSnapshot`、`EnemySpawnEntrySnapshot`、`GateSpawnEntrySnapshot` 与 `PropSpawnEntrySnapshot`。快照只包含运行时所需的值，不暴露 `ScriptableObject`、Luban 生成类型或可变集合。
3. `ConfigService` 在启动阶段完整校验 `LevelCatalog`、所有 `LevelConfig` 和 Luban 表后，复制并缓存快照。集合必须防御性复制；运行阶段不得回读或修改资产。
4. `IConfigService` 只保留查询契约：配置状态、关卡描述和按 `LevelId` 获取 `LevelConfigSnapshot`。具体初始化方法不进入接口。
5. `Game.Composition` 持有 `LevelCatalog`、Luban `cfg.Tables` 与 `IResourceRegistry`，并在装配阶段直接调用 `Game.Foundation` 中具体 `ConfigService.Initialize(...)`；初始化完成后，其他模块只接收 `IConfigService`。
6. `ISceneService`、Gameplay 场景入口、`LevelManager` 与 `SpawnManager` 只传递或消费 `LevelConfigSnapshot`。
7. Luban 自动生成的 `cfg.Tables` 与表行编入独立的 `Game.ConfigGenerated` 支撑程序集。它不引用 Gameplay、Presentation 或 Composition；只有 Foundation 的配置实现与 Composition 启动装配可以引用它，生成类型不得进入 Contracts 或 Gameplay 接口。
8. 程序集依赖仍遵守：`Contracts` 无项目引用；`ConfigGenerated` 无领域层引用；`Foundation -> Contracts/ConfigGenerated`；`Gameplay -> Contracts/Foundation`；`Presentation -> Contracts/Gameplay`；`Composition ->` 其余程序集。任何公共契约都不得引用只存在于更高层的具体类型。

## 快照最小字段

`LevelConfigSnapshot` 至少包含：

- `LevelId`、`DisplayName`、`UnlockedLevelIds`；
- `RoadBounds`、`SpawnY`、`EnemyApproachY`、`DespawnY`；
- `EnemySpawns`、`GateSpawns`、`PropSpawns`；
- `ElementDurationSecondsPerDamage`。

三类生成条目快照保留各自的 `SpawnTime`、`SpawnPosition` 与运行时必需参数；Gate 条目额外包含 `GateType`、`InitialValue`、`ElementType`、`MaxHp`，不恢复已移除的 Gate `ConfigId`。

## 后果

- Unity 资产仍可在 Inspector 中编辑，但运行时公共层不再携带资产引用。
- `ConfigService` 是资产、Luban 数据与运行时快照之间唯一的转换和校验边界。
- 场景切换与 Gameplay 测试可直接构造快照，不需要创建 `ScriptableObject`。
- 实现阶段需要在创建 `.asmdef` 前按本 ADR 放置类型，否则编译依赖仍可能倒置。

## 验收标准

- `IConfigService` 不含 `Initialize`，也不暴露 `LevelConfig`、`cfg.Tables` 或 Unity 资产类型。
- `Game.Contracts` 与 `Game.Foundation` 均不引用 `Game.Gameplay`。
- Luban 生成代码有明确程序集归属，且其类型不出现在 Contracts、Gameplay 或 Presentation 的公共表面。
- Gameplay 的关卡启动链全程只传递 `LevelConfigSnapshot`。
- 修改已加载的 `LevelConfig` 资产不会改变当前运行快照。
- 配置错误继续按 ADR-041 在启动期直接报错并退出，不增加运行时兜底。
