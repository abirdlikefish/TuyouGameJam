# MVP 代码生成实施计划

## 当前阶段与授权范围

- 阶段：首轮 MVP 工程实现准备完成，即将按批次进入代码生成。
- 设计基线：核心架构、Gameplay 模块、共享契约和测试清单均已达到 `ContractReady`；实现以已接受 ADR 和 `03_SharedContracts` 为准。
- AI 默认负责：创建约定内的代码目录和 `.cs` 文件，维护与本批实现直接相关的状态、变更记录和手工装配清单。
- 用户默认负责：Luban 表格及生成、Scene、Prefab、ScriptableObject 实例、Animator/AnimationClip、Sprite、材质、Layer、Physics2D Matrix、Build Settings 和 Inspector 引用。
- AI 不通过 Editor 工具或临时代码绕过上述手工边界；确需改变范围时由用户单独授权。

Unity 自动导入代码和目录后产生的 `.meta` 必须与对应脚本或目录一同保留。AI 不手写或复制现有 GUID；每批在 Unity 刷新后检查新增脚本的 `.meta` 是否齐全。

## 权威来源与冲突顺序

实现前按以下优先级处理冲突：

1. 较新的 `Accepted` ADR。
2. `03_SharedContracts` 中的公共接口、事件、数据和碰撞契约。
3. `01_Architecture` 中的分层、生命周期和装配规则。
4. 目标模块 README 中的模块内规则。
5. 本实施计划与路线图。

若实现发现契约缺口，不允许在代码中自行创造跨模块规则。先停止受影响部分，记录问题并通过 ADR/共享契约收口；不受影响的独立工作可以继续。

## 代码目录与依赖方向

首轮不创建任何 `.asmdef`，所有代码仍编入 Unity 默认程序集，但目录和命名空间按未来程序集边界组织：

```text
Assets/Scripts/Game/
├── Contracts/
│   ├── Application/
│   ├── Configuration/
│   ├── Events/
│   ├── Gameplay/
│   └── Pooling/
├── Foundation/
│   ├── Application/
│   ├── Configuration/
│   ├── Events/
│   ├── Pooling/
│   ├── Scenes/
│   └── Time/
├── Gameplay/
│   ├── Army/
│   ├── Bullet/
│   ├── Gate/
│   ├── Input/
│   ├── Level/
│   ├── Monster/
│   ├── Obstacle/
│   ├── Prop/
│   └── Spawn/
├── Presentation/
│   └── UI/
└── Composition/
    ├── Bootstrap/
    └── Scenes/
```

允许的逻辑依赖方向为：

```text
Composition → Presentation → Gameplay → Foundation
      ├──────────────→ Gameplay
      └──────────────→ Foundation

Contracts 可被各层引用，但不引用任何具体实现。
Foundation 可使用 Luban 生成类型；Contracts、Gameplay 和 Presentation 不出现 cfg.*。
```

`Assets/Scripts/Luban` 和 `Assets/Generated/Luban` 保持现状；AI 不修改 Luban 自动生成的 `.cs`。配置表尚未由用户生成到稳定 API 前，具体 `ConfigService` 映射不得先猜测字段名。

## 通用编码规则

- 只实现当前 MVP 契约，不顺带加入 HUD、音频、存档、暂停、调速、对象池预热、通用调试服务或复杂框架。
- MonoBehaviour 只承担 Unity 生命周期、序列化绑定、Transform/Physics2D/Animator 适配和阶段入口；可计算规则与状态转换优先放入纯 C# 类型。
- 跨模块必须执行的命令和查询使用最小同步接口；EventBus 只发布已经发生的事实。
- 依赖由 Composition、SceneEntry 或职责明确的场景装配器显式注入；不使用静态 `Instance`、通用 Service Locator、`Find` 或运行时父级搜索。
- 必需组件和资源使用 `[SerializeField]` 显式绑定并在 Bootstrap/Preparing 阶段集中校验；不以 `GetComponent`、`AddComponent` 或默认值静默补齐。
- Gameplay 核心逻辑不由各对象独立 `Update` 推进。`LevelManager` 集中读取时间并按契约顺序同步驱动 Manager。
- 配置、运行时状态和事件载荷使用已定义的不可变快照、ID 与枚举；不在模块内创建第二份等价身份或字段。
- 池化对象不自行取得或归还对象池，不在 `OnDisable`/`OnDestroy` 中递归归还；Manager 拥有登记、清理和归还流程。
- 数值、碰撞、会话和错误处理不得添加文档外的降级路径。契约规定 Fail-Fast 时必须停止 Ready 或应用流程。
- 当前不创建自动测试脚本；保持规则可测试结构，并按 `05_Testing/TestingStrategy.md` 执行编译、日志、Inspector 和手工验证。

## 生成批次与依赖顺序

### 批次 1：目录骨架与 Contracts

AI 生成逻辑目录、命名空间、稳定枚举、ID/DTO、不可变配置与运行时快照、公共接口和事件载荷。

完成条件：

- 类型与 `PublicInterfaces.md`、`EventCatalog.md`、`DataDictionary.md` 一致。
- Contracts 不引用 Foundation、Gameplay、Presentation、Composition、`LevelConfig` 或 `cfg.*`。
- Unity 刷新后无编译错误，新增脚本 `.meta` 齐全。

### 批次 2：Foundation 基础设施

按 `EventBus → TimeService/Timer → PoolService` 顺序实现。每个服务独立完成后先编译，再开始下一个服务。

完成条件：

- EventBus 满足同步顺序、订阅快照、异常隔离、嵌套发布和 Token 幂等语义。
- TimeService 只实现 MVP 固定倍率域和可取消 RealTime Timer。
- PoolService 满足具体根组件类型身份、未激活借出、防御性失活和跨池/重复归还规则。

### 批次 3：配置资产类型与配置实现

AI 先生成 `LevelCatalog`、`LevelConfig`、三类生成条目等代码类型，以及 Foundation 内的快照转换和 Provider 边界。随后暂停，由用户创建五类 Luban 表、填充最小数据并重新生成代码和 JSON。生成 API 稳定后，AI 再实现具体 `ConfigService`、表映射、校验与快照复制。

完成条件：

- 用户生成的表包含 `TbArmy`、`TbWeapon`、`TbEnemy`、`TbProp`、`TbBullet`，字段与配置契约一致。
- AI 未修改任何生成 `.cs` 或表格。
- ConfigService 对合法最小数据进入 Ready；非法配置按首错规则失败，`unlockedLevelIds` 按 ADR-044 处理。

### 批次 4：可并行 Gameplay 模块

Contracts 和 Foundation 编译稳定后，可以并行生成以下互不重叠的目录组：

- Army + Input：人数、槽位、HP、武器/元素、射击调度、横向移动和相对拖拽适配。
- Bullet + Monster：子弹 Cast/命中/回收，敌人移动、近似阻挡、攻击动画桥接、死亡与 EnemyManager。
- Gate + Prop + Obstacle：道路对象状态、受击、一次性接触、奖励锁定、离场、登记和回收。

模块只能依赖已冻结的 Contracts/Foundation。发现公共契约缺口时交给集成窗口处理，不得由模块窗口直接修改共享类型。

完成条件：

- 每组只修改认领目录及直接相关实现记录。
- 无 Prefab 和场景实例时脚本仍可完成编译。
- 交付中明确列出所有 `[SerializeField]`、组件、Collider、Layer、Animator Event 和用户绑定要求。

### 批次 5：Spawn 与 Level 单局协调

由集成窗口实现 SpawnManager、三类时间轴游标、LevelManager 状态和固定帧阶段。该批不得与玩法模块窗口并行修改 Contracts 或 Manager 公共入口。

完成条件：

- 零时刻生成、过期 `LevelRunId` 隔离和终局停止派发符合契约。
- LevelManager 是核心同帧顺序的唯一协调者，并准确执行一次 `Physics2D.SyncTransforms`。
- 同帧 Victory/GameOver 冲突时失败优先，StopRun 能清理全部模块。

### 批次 6：应用流程与 Composition

实现 GameStateService、SceneService、GlobalBootstrap、三个 SceneEntry、Gameplay 场景装配器及结构化日志。Composition 只负责创建、连接、启动和注入，不承担玩法计算。

完成条件：

- 服务按 Create、Connect、Start 三阶段装配。
- 场景 Ready、卸载、失败和过期回调语义与应用流程契约一致。
- 在用户创建场景前代码可编译，交付清单列出全部场景根对象和 Inspector 引用。

### 批次 7：用户资源装配

用户根据 `04_Assets/PrefabSpecifications.md` 和各批交付清单手动完成：

- 六个 Gameplay Layer 与 Physics2D Matrix。
- Army、Bullet、三类 Monster、两类 Gate、WeaponProp、Road 和 TouchDragArea Prefab。
- Collider 身份代理、TMP 调试文本、Animator Controller、Attack/Death Clip 及 AnimationEvent。
- LevelCatalog、LevelConfig、Prefab 引用和最小关卡数据。
- Bootstrap、MainMenu、LevelSelect、Gameplay 场景、Build Settings 与 Inspector 绑定。

AI 在本批只根据编译错误、日志和用户提供的绑定结果修正代码，不自动创建或改写上述资源。

### 批次 8：集成、验收与修复

按“初始化 → MainMenu → LevelSelect → Gameplay → Victory/GameOver → 清理 → 返回选关 → 重开”验证完整闭环，并执行 `05_Testing` 中与当前切片相关的代表性边界用例。

完成条件：

- Unity 无编译错误，启动场景和完整游玩闭环实际通过。
- 配置失败、引用失败和场景失败均产生可定位日志并阻止错误推进。
- 重开后无重复 GlobalRoot、旧订阅、旧实例或上一局回调。
- 未验证项和剩余风险明确记录；只有实际通过的路线图与测试项才可勾选。

## 多对话窗口并行规则

### 可并行条件

- 批次 1～3 已完成，Contracts、Foundation 和配置 Provider 已编译稳定。
- 每个窗口在开始前声明唯一认领目录、依赖文档、预计文件和不会修改的范围。
- 目录骨架由单一窗口预先创建，避免多个窗口同时创建同一目录或 `.meta`。

### 所有权

| 所有者 | 独占范围 |
|---|---|
| 集成窗口 | `Contracts`、`Foundation`、`Gameplay/Spawn`、`Gameplay/Level`、`Composition`、共享项目状态 |
| Army/Input 窗口 | `Gameplay/Army`、`Gameplay/Input` |
| Bullet/Monster 窗口 | `Gameplay/Bullet`、`Gameplay/Monster` |
| Obstacle 窗口 | `Gameplay/Gate`、`Gameplay/Prop`、`Gameplay/Obstacle` |

- 任何时刻同一文件只能有一个窗口写入。
- 模块窗口不得修改 Contracts、Foundation、生成代码、Scene、Prefab、ProjectSettings 或其他模块目录。
- 共享契约变更只能由集成窗口在 ADR 与共享文档同步完成后实施。
- `LevelManager`、Composition 和场景装配属于集成点，在模块实现稳定前不并行抢写。
- 推荐使用独立 Git branch/worktree；若共用工作区，必须严格保持目录不重叠，并由唯一 Unity Editor/集成窗口负责刷新、编译和 `.meta` 检查。
- 任一并行批次结束后先停写，由集成窗口统一检查 diff 和编译，再开始下一批。

## 每批交付格式

每个代码批次必须报告：

1. 认领范围与实际新增/修改文件。
2. 实现了哪些契约，以及明确未实现的内容。
3. 新增的序列化字段和用户手工装配步骤。
4. Unity 编译结果、已执行的手工验证和未验证项。
5. `git diff --check`、目标文件 diff 和生成目录未被修改的检查结果。
6. 剩余风险、下一批的进入条件和是否允许并行。

状态按实际进度更新：开始编码为 `InProgress`，完成编译和模块级手工验证为 `InTest`，进入场景联调为 `Integration`，只有完整验收通过后才为 `Done`。

## 关联文档

- [开发路线图](Roadmap.md)
- [文档索引](DocumentIndex.md)
- [程序集边界](../01_Architecture/AssemblyBoundaries.md)
- [启动与装配](../01_Architecture/BootstrapAndComposition.md)
- [应用流程](../01_Architecture/ApplicationFlow.md)
- [模块总表与并行认领](../02_Modules/README.md)
- [公共接口](../03_SharedContracts/PublicInterfaces.md)
- [Prefab 规格](../04_Assets/PrefabSpecifications.md)
- [测试与验证策略](../05_Testing/TestingStrategy.md)
- [ADR-047：分批代码生成与并行所有权](../06_Decisions/ADR-047-StagedCodeGenerationAndParallelOwnership.md)
