# ADR-039：首轮工程切片的失败、占位表现与终局范围

## 状态

Accepted

> 后续修订：ADR-041 将 Luban 表、LevelCatalog 和 LevelConfig 数据错误收敛为 ConfigService 单点报错后立即退出应用。本 ADR 的“记录错误并阻止 Ready”继续适用于 Prefab、Collider、Layer、Inspector 引用及其他场景装配错误。ADR-046 明确敌人阻挡只查询上一同步姿态并接受少量同帧重叠风险。

## 日期

2026-09-19

## 背景

项目即将从文档阶段进入首轮 Unity 工程实现。当前目标是先验证应用流程、玩法阶段顺序、生成、移动、射击、命中、接触、攻击和胜负闭环，而不是在首轮切片中建设复杂容错、完整 HUD、正式 Gate 美术或连续碰撞求解。

现有文档仍保留 Manager 初始化失败结果类型、逆序恢复、敌人阻挡安全间距来源、高速相对运动、Gate 最终表现和胜利后剩余 Gate/Prop 等待规则的待决空间。若不收敛，实现者仍需要自行选择。

## 决策

### 配置与装配错误直接失败

- ConfigService 在应用启动时校验全部目录与配置数据；GlobalBootstrap 和 GameplaySceneEntry 在各自初始化或 Preparing 阶段校验必需 Prefab、Collider、Layer 和 Inspector 引用。
- 发现错误时使用 `Debug.LogError` 输出稳定来源、字段或对象路径和原因，将对应初始化视为失败，并停止继续进入 MainMenu 或 Gameplay Ready。
- 不使用缺省配置、运行时补组件、静默跳过、自动重试或降级流程让错误配置继续运行。
- 当前 Manager 的 `StartRun/StopRun` 保持 `void`，不为错误增加通用 Result、错误码层级或恢复状态机。Luban、目录和关卡数据由 ConfigService 在加载场景前完成致命校验；场景绑定在调用 `StartRun` 前完成校验。
- 若启动过程中仍发生意外异常，场景入口记录异常并停止发布 Ready；已经启动的场景模块只执行必要的幂等 `StopRun` 以释放本局实例，不尝试恢复为可游玩状态。

### 敌人阻挡间距来自 Prefab

- 每个敌人规范 Prefab 的具体根脚本序列化 `blockingGap: float`，表示该类型敌人与前方敌人 BodyCollider 之间希望保留的非负世界单位间距。
- `blockingGap` 必须有限且大于等于 `0`，由 EnemyManager 在 Gameplay Preparing 时随 Prefab 其他绑定一起校验。
- `blockingGap` 不进入 `TbEnemy` 或 `LevelConfig`；不同敌人类型可以通过各自 Prefab 使用不同值。

### Gate 使用单一调试文本

- 首轮切片的 `PF_Gate_Additive` 和 `PF_Gate_Element` 各自只要求一个显式绑定的 `TMP_Text stateText` 作为世界空间调试表现。
- 加法门文本显示当前 `GateValue`；元素门文本显示 `ElementType`、当前 HP 和 `PostDepletionDamage`。具体排版、颜色、动画和最终文案不是当前契约。
- 文本只读取 Gate 已结算的状态并刷新，不参与数值计算或接触判定。正式美术表现后续迭代时可以替换该 View，但不得改变玩法状态所有权。

### HUD 延后

- 当前 Gameplay Canvas 只保留 Input 模块所需的 `PF_UI_TouchDragArea`、GraphicRaycaster 和 EventSystem。
- Army、关卡计时、生成进度、胜负面板等 HUD 不属于首轮工程切片；逻辑正确性先通过调试文本、结构化日志和自动化测试验证。
- HUD 后续接入时只读取只读快照或订阅事实事件，不参与核心结算。

### 不增加高速相对运动求解

- Bullet 仍沿自身上一逻辑位置到期望位置执行 Collider Cast；Gate/Prop 仍按当前阶段契约执行显式接触查询。
- 首轮切片不实现双方相对运动扫掠、子步进、连续碰撞求解或额外的 `Physics2D.SyncTransforms`。
- 关卡数据、Prefab 移动速度、Collider 尺寸和目标帧率必须调到当前离散阶段模型可稳定工作的范围。测试只验证 MVP 配置范围内的命中和接触，不承诺任意高速或严重掉帧下绝不穿透。

### Victory 立即截断剩余道路内容

- 当全部敌人生成项已派发且 `AliveEnemyCount == 0` 时立即判定 Victory；Army 同帧归零仍优先判定 GameOver。
- 胜利不等待 Gate/Prop 时间轴派发完成，也不等待仍在道路上的 Gate/Prop 接触或离场。
- 进入 Completed 后 SpawnManager 停止派发所有未来条目；ObstacleManager、BulletManager 和 EnemyManager 在 `StopRun` 中清理并归还当前活动实例。未生成或未结算的 Gate/Prop 被有意截断，不产生补偿效果。

## 不采用

- 不为错误配置建立可继续游玩的回退值、占位组件或恢复状态机。
- 不在首轮切片建设正式 Gate 美术或完整 HUD。
- 不为超出 MVP 调参范围的高速相对运动增加复杂物理解法。
- 不把 Gate/Prop 时间轴完成作为 Victory 的额外条件。

## 影响

- 本 ADR 覆盖 ADR-014 中“配置失败必须发布 InitializationFailed/LevelConfigLoadFailed 事实”的部分；后续 ADR-041 又把 Luban、目录和关卡数据错误收敛为直接记录首个错误并退出应用。场景加载/卸载失败事件不受影响。
- DES-044 关闭：Manager 生命周期继续使用 `void`；配置数据在应用启动时集中致命校验，场景绑定在 StartRun 前集中校验并在失败时阻止 Ready。
- Monster Prefab 增加并校验 `blockingGap`。
- Gate Prefab 的首轮表现统一为单个 `TMP_Text stateText`。
- UI 状态调整为首轮 HUD Deferred，Input UI 不受影响。
- Bullet、碰撞和测试文档移除任意高速不穿透承诺。
- Level、Spawn 和测试文档明确 Victory 会截断未来及活动 Gate/Prop。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Bullet/README.md`
- `../02_Modules/Gate/README.md`
- `../02_Modules/UI/README.md`
- `../03_SharedContracts/CollisionRules.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
