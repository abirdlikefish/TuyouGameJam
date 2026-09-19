# ADR-044：unlockedLevelIds 校验、警告与运行时过滤

## 状态

Accepted

## 日期

2026-09-20

## 背景

`LevelConfig.unlockedLevelIds` 表示通关当前关卡后应作为本局结果发布的解锁关卡 ID。当前 MVP 不执行下一关跳转，也不保存玩家进度，但 `GameStateService` 仍会在 Victory 中发布该列表。

既有配置规则要求 ConfigService 对 LevelCatalog、LevelConfig 和已确认引用执行启动期校验，并在配置错误时 Fail-Fast。这里需要区分两类问题：重复 ID 或自引用表示当前关卡资产自身存在明确错误；目录中暂不存在的 ID 则可能是策划提前填写的未来关卡。如果把后一类也视为致命错误，会阻止尚未完整录入未来关卡时验证当前 MVP。

## 决策

### 允许的输入

- `unlockedLevelIds` 允许为空，表示通关后不产生其他关卡的解锁结果。
- 有效 ID 必须在当前 `LevelCatalog` 中存在，且不能等于当前 `LevelConfig.levelId`。
- 有效 ID 在运行时快照中保持资产列表的原始顺序，不自动排序。

### 致命错误

ConfigService 必须先对原始 `unlockedLevelIds` 执行重复和自引用校验，再检查目录存在性：

1. 任意 ID 在同一列表中重复出现时，使用 `ConfigErrorCode.InvalidLevelConfig` 和 `Debug.LogError` 报告首个重复项，将状态置为 `Failed`，Player 退出、Editor 停止 Play Mode。
2. 任意 ID 等于当前 `LevelConfig.levelId` 时，以相同方式报告自引用并终止应用。
3. 即使重复的是目录中暂不存在的未来关卡 ID，也先按重复 ID 的致命错误处理，不允许通过后续过滤掩盖资产错误。

错误 Source 必须稳定定位到当前 LevelConfig、`unlockedLevelIds` 字段和条目索引；不使用自动去重或删除自引用的方式继续运行。

### 非致命目录缺失

通过重复和自引用校验后，如果某个 ID 在当前 `LevelCatalog` 中不存在：

1. 使用 `Debug.LogWarning` 输出当前 `LevelId`、稳定字段 Source、条目索引和缺失 ID。
2. 忽略该 ID，不把它复制进 `LevelConfigSnapshot.UnlockedLevelIds`。
3. 不把 ConfigService 置为 `Failed`，不退出应用，不阻止初始化进入 `Ready`，也不发布配置失败或恢复事件。

由于重复 ID 已在前一步被拒绝，每个缺失 ID 在当前列表中最多出现一次，因此每个缺失条目只记录一次警告。未来把该 ID 对应的 LevelConfig 加入 LevelCatalog 后，下次初始化会自然把它保留为有效结果，无需修改代码或当前关卡资产。

### 运行时结果

ConfigService 按原始顺序把目录中实际存在的有效 ID 防御性复制进 `LevelConfigSnapshot.UnlockedLevelIds`。GameStateService 在 Victory 时只从该已过滤快照复制结果；LevelManager、LevelSelect、UI 和未来存档消费者都不再处理原始资产列表或缺失 ID。

## 对 ADR-041 的修订

ADR-041 的单点 Fail-Fast 继续适用于 Luban 表、LevelCatalog、LevelConfig 数值、重复 ID、自引用和其他已确认的必需引用错误。`unlockedLevelIds` 指向当前 LevelCatalog 中尚不存在的未来关卡，是唯一明确的非致命目录引用例外：它只警告并从运行时快照中过滤。

## 不采用

- 不把目录缺失 ID 作为 `InvalidLevelConfig` 终止应用。
- 不静默忽略缺失 ID；必须留下可定位警告。
- 不原样把缺失 ID 传播到 Victory、LevelSelect 或未来存档系统。
- 不自动去重，不把自引用解释为“保持当前关卡解锁”。
- 不因为允许未来关卡引用而跳过对当前目录、当前关卡或其他配置引用的严格校验。

## 验收标准

- 空 `unlockedLevelIds` 初始化成功，快照和 Victory 结果均为空。
- `[2, 2]` 无论关卡 2 是否已在目录中，都报告首个重复项并终止应用。
- 当前 `levelId = 1` 时，列表包含 `1` 会报告自引用并终止应用。
- 目录为 `[1, 2, 3]` 且关卡 1 配置 `[2, 99, 3]` 时，只对 `99` 记录一次 `Debug.LogWarning`，初始化成功，快照和 Victory 结果为 `[2, 3]`。
- 缺失 ID 加入 LevelCatalog 后，下次初始化不再警告，并按原位置进入过滤后的快照。
- 运行时消费者只读取防御性复制后的 `LevelConfigSnapshot.UnlockedLevelIds`，不回读或修改 LevelConfig 资产列表。

## 关联文档

- `../00_Project/DesignBacklog.md`
- `../00_Project/ProjectOverview.md`
- `../01_Architecture/ConfigurationSystem.md`
- `../01_Architecture/ApplicationFlow.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-012-LevelCatalogConfigurationBootstrap.md`
- `ADR-041-TypedConfigProvidersAndFatalValidation.md`
- `ADR-042-LevelConfigSnapshotAssemblyBoundary.md`
