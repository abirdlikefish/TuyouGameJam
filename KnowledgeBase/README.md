# 倍增门 Unity Game Jam 知识库

这是《倍增门》2D 竖屏单机小游戏的设计、接口、数据和测试知识库。

当前阶段以“文档先行”为准：机制和模块契约仍可调整，知识库内容是设计依据，不代表已经存在对应的 Unity 实现。

## 使用方式

1. 新对话窗口先阅读 `00_Project/DocumentIndex.md`，确认当前设计阶段、模块状态和并行编辑边界。
2. 再阅读 `00_Project/ProjectOverview.md`、`00_Project/DesignBacklog.md` 和 `01_Architecture/SystemOverview.md`。
3. 处理单模块时，只在对应的 `02_Modules/<Module>` 目录维护模块文档；模块总表和认领规则见 `02_Modules/README.md`。
4. 跨模块接口、事件、字段和命名统一维护在 `03_SharedContracts`，不得在模块文档内复制出第二份“权威定义”。
5. 方案取舍写入 `06_Decisions`，变更摘要写入 `07_Changes/ChangeLog.md`，路线状态同步到 `00_Project/Roadmap.md`。

## 权威性与冲突处理

- 已接受的 ADR 和共享契约是跨模块协作的权威来源。
- 项目概览描述目标和当前基线；如果与 ADR 冲突，以较新的已接受 ADR 为准，并补充变更记录。
- 模块 README 只描述本模块如何使用契约，不重新定义契约含义。
- “待定”“草案”“假设”内容不能作为实现前提；先更新设计状态，再允许其他模块依赖。

## 模块状态

| 模块 | 状态 | 入口文档 |
|---|---|---|
| 全局服务 | ContractReady | `01_Architecture/GlobalServices.md` |
| 时间系统 | ContractReady | `01_Architecture/TimeSystem.md` |
| 军队 | InDesign | `02_Modules/Army/README.md` |
| Gate 门 | InDesign | `02_Modules/Gate/README.md` |
| Prop 道具 | InDesign | `02_Modules/Prop/README.md` |
| Obstacle 道路对象管理 | InDesign | `02_Modules/Obstacle/README.md` |
| 怪物 | InDesign | `02_Modules/Monster/README.md` |
| 子弹 | Planned | `02_Modules/Bullet/README.md` |
| 关卡 | Planned | `02_Modules/Level/README.md` |
| 生成与对象池 | Planned | `02_Modules/Spawn/README.md` |
| UI | Planned | `02_Modules/UI/README.md` |
| 音效与特效 | Planned | `02_Modules/AudioVFX/README.md` |
| 输入 | InDesign | `02_Modules/Input/README.md` |

## 文档约定

- 状态使用：`Planned`、`InDesign`、`ContractReady`、`InProgress`、`InTest`、`Integration`、`Done`、`Blocked`。
- `Planned`、`InDesign`、`ContractReady` 表示设计阶段；后五个状态表示得到明确授权后的工程阶段。
- 公共接口先改文档，再改代码。
- 每个模块必须记录职责边界、依赖、输入、输出和测试标准。
- 本知识库只记录项目知识，不提交 `Library`、`Temp`、`Logs` 等 Unity 生成目录。
