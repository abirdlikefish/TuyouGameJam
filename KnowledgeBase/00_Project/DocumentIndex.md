# 文档索引与新对话入口

## 当前阶段

- 阶段：工程实现 / 批次 7 资源、动画与场景装配
- 工程状态：批次 7.5A～7.5C 已完成；批次 7.6 已完成现有素材基线的结构与运行态专项验证。Controller/AOC、Weapon/Enemy/Bullet/Gate 身份映射、既有 Monster AnimationEvent、显式 Army 状态切换及池对象复用均通过；当前工作树另有正在导入的帧素材，以最新 Scan Report 为准。ADR-061 双元素命中效果、ADR-062 移动端本地关卡进度、ADR-064 ikun/篮球首轮功能、ADR-065 四类元素死亡动画选择、ADR-066 Army 槽位死亡表现、ADR-067 可配置道具类型/鹅笼增员与 ADR-068 ikun 篮球远程攻击动画桥接均已进入工程实现；十种 Army Death 与四类敌人的普通/火/冰/雷死亡资源骨架已建立，Ikun RangedAttack 正式帧、Clip、Animator 状态和 AOC 覆盖已完成，远程 AnimationEvent 与鹅笼美术待后续补齐
- 目标：完成批次 7.6 剩余的竖屏目标分辨率实机验证与 19 个动作素材补齐；之后进入批次 8 完整命中、接触、胜负和重开闭环验收

## 推荐阅读顺序

1. `ProjectOverview.md`：项目定位、当前玩法基线和非目标。
2. `ImplementationPlan.md`：代码目录、分批顺序、AI/用户边界、并行所有权和每批验收门。
3. `DesignBacklog.md`：尚未定案的非 MVP 机制和后续设计入口。
4. `../01_Architecture/SystemOverview.md`：分层和依赖原则。
5. `../01_Architecture/AssemblyBoundaries.md`：后续程序集划分、依赖倒置和跨层通信边界。
6. `../01_Architecture/BootstrapAndComposition.md`：GlobalBootstrap 的装配、初始化、失败和清理边界。
7. `../01_Architecture/ApplicationFlow.md`：GameStateService、SceneService、三个 SceneEntry、异步加载/卸载完成边界和应用流程。
8. `../01_Architecture/ConfigurationSystem.md`：LevelConfig 资产、LevelConfigSnapshot、Luban 与程序集之间的边界和加载流程。
9. `../01_Architecture/PoolSystem.md`：具体组件类型池、未激活借出、防御性失活和跨场景生命周期契约。
10. `../02_Modules/README.md`：模块总表、并行认领和模块入口。
11. 目标模块的 `README.md`：职责、输入、输出、测试标准和已知问题。
12. `../03_SharedContracts`：接口、事件、配置表、字段、命名规则和 `CollisionRules.md`。
13. `../04_Assets/AnimationPipeline.md`：批次 7 序列帧的目录、导入、Clip/Controller、Prefab 预绑定与验证流程。
14. `../04_Assets/PrefabSpecifications.md`：首轮工程切片的最小场景层级、Prefab 结构、序列化引用和校验边界。
15. `../05_Testing/TestingStrategy.md`：当前无 `.asmdef` 阶段的验证方式与未来自动化优先级。
16. `../06_Decisions`、`../07_Changes/ChangeLog.md`：已接受决策和近期变更。

## 按任务选择入口

| 任务 | 首要文档 | 允许的默认修改范围 |
|---|---|---|
| 设计核心玩法 | `DesignBacklog.md`、`ProjectOverview.md` | `00_Project`，必要时新增 ADR |
| 设计单个玩法模块 | `02_Modules/README.md`、目标模块 README | 目标模块目录，必要时更新契约 |
| 修改跨模块接口 | `03_SharedContracts`、相关 ADR | 共享契约、ADR、受影响模块 README |
| 设计架构或服务 | `01_Architecture` | 对应架构文档、ADR |
| 修改本地关卡进度 | `../02_Modules/Save/README.md`、`../06_Decisions/ADR-062-MobileLocalPlayerProgress.md` | Save、Application、Bootstrap、相关共享契约与测试记录 |
| 制定验收标准 | `05_Testing` | 测试清单和关联模块文档 |
| 导入动画或绑定 Animator | `../04_Assets/AnimationPipeline.md`、`../04_Assets/PrefabSpecifications.md` | `Assets/Art`、`Assets/Animations`、目标 Prefab 与直接相关表现适配记录 |
| 生成代码或组织并行窗口 | `ImplementationPlan.md`、目标模块 README | 已认领代码目录、直接相关实现记录 |

## 文档状态说明

`Planned` 表示只有范围；`InDesign` 表示规则仍在讨论；`ContractReady` 表示其他模块可以依赖已记录的接口。后三者是设计状态，不代表 Unity 工程已经实现。
