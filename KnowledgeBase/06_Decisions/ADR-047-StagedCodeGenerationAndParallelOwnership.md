# ADR-047：分批代码生成、手工资源边界与并行所有权

- 状态：Accepted
- 日期：2026-09-20
- 关联：ADR-015、ADR-024、ADR-026、ADR-039、ADR-042、ADR-045、ADR-046

## 背景

核心玩法契约已经收口，用户授权项目进入逐步代码生成阶段。工程当前只有 Luban 示例生成代码和示例场景；正式 Gameplay、Foundation、Composition、Prefab、关卡资产和场景装配尚未建立。

如果一次性生成全部模块，配置生成 API、序列化字段和场景绑定问题会在最后集中暴露；如果多个对话窗口同时修改共享契约、目录骨架或集成入口，又会产生文件覆盖、接口分叉和 Unity `.meta` 冲突。因此需要在进入首批代码前固定生成顺序、AI/用户职责边界和并行所有权。

## 决策

### AI 与用户边界

1. AI 默认只创建和修改约定内的代码目录、`.cs` 文件，以及与本批实现直接相关的知识库状态和变更记录。
2. 用户默认手工负责 Luban 表格及生成、Scene、Prefab、ScriptableObject 实例、Animator/AnimationClip、Sprite、材质、Layer、Physics2D Matrix、Build Settings 和 Inspector 引用。
3. AI 不通过 Unity Editor 自动化、Editor 生成器或临时代码绕过用户的资源制作边界；范围变化必须获得单独授权。
4. Unity 导入产生的脚本和目录 `.meta` 必须保留并随批次检查；不手写、复用或猜测 GUID。
5. AI 不修改 Luban 自动生成 `.cs`。配置表尚未生成稳定 API 时，不根据计划字段预写无法编译的具体映射。

### 小步生成与验证门

1. 实现顺序固定为：Contracts → EventBus/Time/Pool → 配置资产类型与 ConfigService → 可并行 Gameplay 模块 → Spawn/Level → 应用流程/Composition → 用户资源装配 → 完整集成。
2. 每个基础服务和每组模块完成后先刷新 Unity 并消除编译错误，再生成依赖它的下一批代码。
3. 当前不创建 `.asmdef` 或 Unity 自动测试代码；纯规则保持可测试结构，验证按 ADR-045 和 `TestingStrategy.md` 执行。
4. 没有 Scene/Prefab 实例时，脚本仍必须能够编译；运行时资源验收延后到用户完成手工装配之后。
5. 每批必须提交序列化绑定清单、已验证项、未验证项和下一批进入条件。未实际验证的路线图或测试项不得勾选。

### 并行窗口

1. Contracts、Foundation 和配置 Provider 编译稳定前不并行生成 Gameplay 实现。
2. 并行阶段按目录组认领：Army/Input、Bullet/Monster、Gate/Prop/Obstacle。每个窗口只写自身目录。
3. 集成窗口独占 Contracts、Foundation、Spawn、Level、Composition、共享项目状态和跨模块契约修改。
4. 模块窗口发现契约缺口时报告给集成窗口，不直接修改公共接口或用本地重复类型绕过。
5. 同一文件同一时刻只能有一个写入者。目录骨架和 `.meta` 导入由单一集成窗口协调。
6. 推荐不同 Git branch/worktree；共用工作区时必须保持目录完全不重叠，并只由一个 Unity Editor/集成窗口执行刷新、编译和最终 diff 检查。

### 工程状态

1. 项目从“文档优先 / 等待实现授权”进入“工程实现 / 分批代码生成”。
2. `ContractReady` 只表示设计可被实现；具体模块开始写代码时才改为 `InProgress`。
3. 编译成功不等于模块完成。模块级手工验证后为 `InTest`，跨模块场景联调时为 `Integration`，完整验收通过后才为 `Done`。
4. 正式程序集拆分继续 Deferred，目录和命名空间先遵守未来 Contracts/Foundation/Gameplay/Presentation/Composition 边界。

## 后果

- 首轮实现会产生多个较小批次和用户装配检查点，推进速度低于一次性生成，但错误来源和回退范围更清晰。
- Gameplay 模块可在公共契约冻结后并行开发，同时避免共享文件竞争。
- ConfigService 的具体映射必须等待用户完成 Luban 表和生成，配置批次存在明确的人工握手点。
- Scene、Prefab 和 ProjectSettings 不由 AI 自动创建，完整运行验证必须等待用户完成手工绑定。
- Unity 自动化测试继续延后；当前依赖小步编译、结构化日志、Inspector 和可复现手工测试控制风险。

## 验收标准

- `ImplementationPlan.md` 明确目录、依赖、批次、并行所有权、人工交接和每批交付格式。
- 文档索引和路线图显示项目已经获得分步代码实现授权。
- 第一批代码只创建目录与 Contracts，不同时生成 Gameplay、场景或资源。
- Gameplay 并行窗口不会同时修改 Contracts、Foundation、Level、Composition 或同一文件。
- 任何批次都不修改 Luban 生成代码，不自动创建 Scene、Prefab、表格或 ProjectSettings。
- 每批在继续前报告 Unity 编译结果、`.meta`、diff、手工装配需求和未验证项。

## 关联文档

- `../00_Project/ImplementationPlan.md`
- `../00_Project/DocumentIndex.md`
- `../00_Project/Roadmap.md`
- `../01_Architecture/AssemblyBoundaries.md`
- `../02_Modules/README.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/TestingStrategy.md`
- `../07_Changes/ChangeLog.md`
