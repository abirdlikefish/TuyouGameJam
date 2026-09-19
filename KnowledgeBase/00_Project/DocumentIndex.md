# 文档索引与新对话入口

## 当前阶段

- 阶段：工程实现 / 分批代码生成准备完成
- 工程状态：用户已授权 AI 按 `ImplementationPlan.md` 逐批创建代码与目录；Scene、Prefab、表格、配置资产实例和 ProjectSettings 默认由用户手工完成
- 目标：保持公共契约稳定，以小批次编译和人工装配检查点逐步完成最小游戏闭环

## 推荐阅读顺序

1. `ProjectOverview.md`：项目定位、当前玩法基线和非目标。
2. `ImplementationPlan.md`：代码目录、分批顺序、AI/用户边界、并行所有权和每批验收门。
3. `DesignBacklog.md`：尚未定案的非 MVP 机制和后续设计入口。
4. `../01_Architecture/SystemOverview.md`：分层和依赖原则。
5. `../01_Architecture/AssemblyBoundaries.md`：后续程序集划分、依赖倒置和跨层通信边界。
6. `../01_Architecture/BootstrapAndComposition.md`：GlobalBootstrap 的装配、初始化、失败和清理边界。
7. `../01_Architecture/ApplicationFlow.md`：GameStateService、SceneService、三个 SceneEntry、同步加载/异步卸载和应用流程。
8. `../01_Architecture/ConfigurationSystem.md`：LevelConfig 资产、LevelConfigSnapshot、Luban 与程序集之间的边界和加载流程。
9. `../01_Architecture/PoolSystem.md`：具体组件类型池、未激活借出、防御性失活和跨场景生命周期契约。
10. `../02_Modules/README.md`：模块总表、并行认领和模块入口。
11. 目标模块的 `README.md`：职责、输入、输出、测试标准和已知问题。
12. `../03_SharedContracts`：接口、事件、配置表、字段、命名规则和 `CollisionRules.md`。
13. `../04_Assets/PrefabSpecifications.md`：首轮工程切片的最小场景层级、Prefab 结构、序列化引用和校验边界。
14. `../05_Testing/TestingStrategy.md`：当前无 `.asmdef` 阶段的验证方式与未来自动化优先级。
15. `../06_Decisions`、`../07_Changes/ChangeLog.md`：已接受决策和近期变更。

## 按任务选择入口

| 任务 | 首要文档 | 允许的默认修改范围 |
|---|---|---|
| 设计核心玩法 | `DesignBacklog.md`、`ProjectOverview.md` | `00_Project`，必要时新增 ADR |
| 设计单个玩法模块 | `02_Modules/README.md`、目标模块 README | 目标模块目录，必要时更新契约 |
| 修改跨模块接口 | `03_SharedContracts`、相关 ADR | 共享契约、ADR、受影响模块 README |
| 设计架构或服务 | `01_Architecture` | 对应架构文档、ADR |
| 制定验收标准 | `05_Testing` | 测试清单和关联模块文档 |
| 生成代码或组织并行窗口 | `ImplementationPlan.md`、目标模块 README | 已认领代码目录、直接相关实现记录 |

## 文档状态说明

`Planned` 表示只有范围；`InDesign` 表示规则仍在讨论；`ContractReady` 表示其他模块可以依赖已记录的接口。后三者是设计状态，不代表 Unity 工程已经实现。
