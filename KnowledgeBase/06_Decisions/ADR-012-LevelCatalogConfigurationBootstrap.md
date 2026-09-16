# ADR-012：LevelCatalog 配置目录与关卡配置注入

## 状态

Accepted

## 日期

2026-09-14

## 背景

应用流程在初始化阶段不应依赖 Gameplay 场景，但选关阶段需要知道可选关卡并取得对应的 `LevelConfig`。如果 `ConfigService` 直接从当前场景读取 `LevelConfig`，就会与 `GlobalBootstrap`、`SceneService` 和选关流程形成循环依赖。

## 决策

- 新增一个由 Inspector 序列化引用的 `LevelCatalog` ScriptableObject，作为关卡目录的唯一来源。
- `LevelCatalog` 保存关卡条目列表；每个条目只保存一个 `LevelConfig` 引用和 `initiallyUnlocked` 标记。
- `LevelConfig.levelId` 是关卡唯一 ID，`LevelCatalog` 不重复保存 `levelId`。
- 当前目录只包含第一关，且第一关 `initiallyUnlocked = true`。
- `LevelCatalog` 只描述项目可用关卡和首次运行时的默认解锁状态，不保存运行中的玩家解锁状态。
- `LevelConfig.unlockedLevelIds` 表示通关当前关卡后应解锁的关卡 ID，不能替代 `LevelCatalog` 的初始解锁标记。
- `ConfigService` 初始化时加载 `LevelCatalog`、唯一的 Luban `cfg.Tables` 和 Unity 资源注册表；初始化不读取 Gameplay 场景中的 `LevelConfig`。
- 当前版本的 LevelSelect 只根据目录中的 `initiallyUnlocked` 得到可选 `LevelId`，不读取存档。未来加入存档后再单独定案存档状态与默认解锁的合并规则，见 ADR-017。
- 选定 `LevelId` 后，`ConfigService` 校验并返回对应 `LevelConfig`；调用方将关卡 ID、配置引用和新的 `LevelRunId` 一并交给 `SceneService`。`SceneService` 不再次查询配置，只负责加载 Gameplay 并将三者交给场景中的 `LevelManager`。
- 找不到关卡、重复 ID、空引用或配置校验失败时，流程不得自动进入 Gameplay，必须报告具体错误。

## 数据结构

```text
LevelCatalog
└── entries: List<LevelEntry>

LevelEntry
├── levelConfig: LevelConfig
└── initiallyUnlocked: bool
```

## 初始化与进入 Gameplay

```text
GlobalBootstrap
→ ConfigService.Initialize(LevelCatalog, LubanTables, ResourceRegistry)
→ GameStateService.MainMenu / LevelSelect
→ SelectLevel(levelId)
→ ConfigService.TryGetLevelConfig(levelId)
→ SceneService.LoadGameplay(levelId, levelConfig, levelRunId)
→ LevelManager.Initialize(levelConfig, levelRunId)
```

## 不采用

- 不从 Gameplay 场景反向搜索或创建关卡目录。
- 不使用 `Find`、隐式资源路径或运行时扫描替代 Inspector 绑定的 `LevelCatalog` 引用。
- 不在运行时修改 `LevelCatalog` 或 `LevelConfig` 的解锁字段。
- 不在 `ConfigService` 中创建第二个 Luban Tables 实例。

## 影响

- `ConfigService` 需要提供关卡目录查询、按 ID 获取配置和分层校验能力。
- `SceneService` 需要支持将已校验的 `LevelConfig` 和 `LevelRunId` 传入 Gameplay。
- `SaveService` 属于后续扩展；未来实现时不修改目录资产，存档状态与 `initiallyUnlocked` 的合并规则需另行定案。
- 配置测试需要覆盖目录唯一性、默认解锁、选定关卡注入和失败阻断。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../01_Architecture/GlobalServices.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-011-ApplicationFlowAndGameplaySession.md`
