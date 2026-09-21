# ADR-017：得分与存档延后

## 状态

Accepted

## 日期

2026-09-14

> 后续决策：ADR-062 已启用移动端本地关卡完成/解锁进度，并取代本文关于关卡进度存档继续延后的部分。得分和设置持久化仍按本文延后。

## 背景

当前 MVP 目标是完成一次可运行的道路防守游玩闭环。得分统计、本地进度存档和设置持久化不会影响当前的生成、战斗、胜负和重开流程；提前把它们放入事件和全局服务契约，会增加尚未使用的依赖和状态边界。

## 决策

- 当前 MVP 不实现 Score 模块，不在 `MonsterKilled` 或其他核心事件中携带得分字段。
- 当前 MVP 不初始化、注入或订阅 `SaveService`；`Victory`、`GameOver` 不以存档为监听者。
- `unlockedLevelIds` 仍作为当前关卡结果数据传递，但不写入本地存档，也不代表玩家当前已解锁状态。
- 本地进度存档、得分、设置持久化和解锁状态合并规则保留为后续扩展；实现前需要新增或更新 ADR，不能依赖本 ADR 推断数据格式。

## 不采用

- 不为未来得分或存档预留当前模块的空字段、空事件或隐式单例。
- 不因为存在 `unlockedLevelIds` 就创建玩家进度文件。

## 影响

- 当前事件目录不包含 Score 消费者或 SaveService 消费者。
- 当前配置和运行时状态边界不包含分数或存档状态。
- 路线图阶段 4 的本地存档条目标记为 Deferred，不属于当前 MVP 验收范围。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/Roadmap.md`
- `../01_Architecture/SystemOverview.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/EventSystem.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-012-LevelCatalogConfigurationBootstrap.md`
