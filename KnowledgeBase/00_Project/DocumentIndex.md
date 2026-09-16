# 文档索引与新对话入口

## 当前阶段

- 阶段：文档优先 / 机制设计中
- 工程状态：本阶段不默认生成或修改 Unity 代码、场景和资源
- 目标：先确认核心玩法、模块边界、共享契约和可验证的 MVP，再进入工程实现

## 推荐阅读顺序

1. `ProjectOverview.md`：项目定位、当前玩法基线和非目标。
2. `DesignBacklog.md`：尚未定案的机制、需要比较的方案和决策优先级。
3. `../01_Architecture/SystemOverview.md`：分层和依赖原则。
4. `../01_Architecture/ConfigurationSystem.md`：LevelConfig 与 Luban 的边界和加载流程。
5. `../02_Modules/README.md`：模块总表、并行认领和模块入口。
6. 目标模块的 `README.md`：职责、输入、输出、测试标准和已知问题。
7. `../03_SharedContracts`：接口、事件、配置表、字段、命名规则和 `CollisionRules.md`。
8. `../06_Decisions`、`../07_Changes/ChangeLog.md`：已接受决策和近期变更。

## 按任务选择入口

| 任务 | 首要文档 | 允许的默认修改范围 |
|---|---|---|
| 设计核心玩法 | `DesignBacklog.md`、`ProjectOverview.md` | `00_Project`，必要时新增 ADR |
| 设计单个玩法模块 | `02_Modules/README.md`、目标模块 README | 目标模块目录，必要时更新契约 |
| 修改跨模块接口 | `03_SharedContracts`、相关 ADR | 共享契约、ADR、受影响模块 README |
| 设计架构或服务 | `01_Architecture` | 对应架构文档、ADR |
| 制定验收标准 | `05_Testing` | 测试清单和关联模块文档 |

## 文档状态说明

`Planned` 表示只有范围；`InDesign` 表示规则仍在讨论；`ContractReady` 表示其他模块可以依赖已记录的接口。后三者是设计状态，不代表 Unity 工程已经实现。
