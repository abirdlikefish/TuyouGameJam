# ADR-033：LevelManager 持有 Gameplay 帧阶段顺序

## 状态

Accepted；ADR-061 在 Bullet 阶段后增加组合位移与第二次物理同步

## 日期

2026-09-19

## 背景

ADR-021 已确定每个逻辑帧必须按“移动与阻挡、子弹命中、Gate/Prop 接触、敌人攻击、终局判断”的顺序结算，但此前没有定义谁负责实际调用这些阶段。仅通过 `LevelRunStarted` 同时启用多个 Manager，再让它们各自在 Unity `Update` 中执行，不能保证不同脚本的先后顺序，也不能让 EnemyManager 在同一帧分别参与较早的移动阶段和较晚的攻击阶段。

## 决策

- `LevelRunStarted` 只表示匹配的 Gameplay 会话可以从 `Preparing` 进入 `Playing`；它不承担逐帧调度。
- Gameplay Manager 和池对象在 `Preparing` 完成依赖注入、类型池取得、活动集合初始化和 `StartRun`，但在 LevelManager 进入 `Playing` 前不执行玩法阶段。
- `LevelManager` 是 MVP Gameplay 逻辑帧阶段顺序的唯一协调者。它在自身 `Update` 中读取本帧需要的时间域 delta，并通过类型化同步接口依次调用各状态所有者。
- Manager 继续拥有自己的活动集合、规则和回收；LevelManager 不遍历或修改敌人、道路对象和子弹的内部集合。
- 必须执行的阶段调用不使用 EventBus。事件仍只传播已经发生且允许零监听者的事实。
- 池对象不得使用独立 `Update` 推进核心移动、命中、接触或攻击结算；它们由对应 Manager 的阶段方法驱动。Animator 判定帧只能登记待处理攻击，实际伤害在敌人攻击阶段结算。

## 每帧顺序

```text
LevelManager 确认状态为 Playing
→ 读取 RealTime、Gameplay、Bullet、Gate、Monster delta
→ GameplayInputAdapter.TickInput
→ elapsedTime += Gameplay delta
→ SpawnManager.Tick(LevelRunId, elapsedTime)
→ ArmyController.TickMovementAndFire
→ EnemyManager.TickMovement
→ ObstacleManager.TickMovement
→ Physics2D.SyncTransforms（当前项目 Auto Sync Transforms 关闭）
→ BulletManager.TickMovementAndHits
→ EnemyManager.ApplyPendingDisplacements
→ Physics2D.SyncTransforms（ADR-061 冰火组合位移后）
→ ObstacleManager.ResolveContacts
→ EnemyManager.ResolveAttacks
→ 各 Manager FlushPendingRecycles
→ LevelManager.EvaluateCompletion
```

Army 发射产生的新子弹在本帧子弹阶段参与移动和命中。SpawnManager 在移动阶段之前派发到时对象，新生成对象参与本帧后续阶段。三类生成列表之间仍不定义可供玩法依赖的跨类型顺序。

当前 `ProjectSettings/Physics2DSettings.asset` 关闭 Auto Sync Transforms。原始管线在 Army、Enemy、Gate/Prop 应用常规位置后调用一次 `Physics2D.SyncTransforms()`；ADR-061 进一步要求在 Bullet 阶段登记并应用冰火位移后调用第二次，再进入接触与攻击查询。两次同步都只由 LevelManager 调用，各 Manager 和池对象不得重复调用。

进入 `Playing` 时，LevelManager 先调用一次 `SpawnManager.Tick(LevelRunId, 0)`，确保 `spawnTime == 0` 的条目在首个逻辑帧移动前出现。

## 生命周期与终局

- `GameplaySceneEntry` 在报告 Ready 前完成固定引用校验、依赖注入、Manager `StartRun` 和 LevelManager 的 `Preparing`。
- 只有 LevelManager 订阅 `LevelRunStarted` 以改变单局状态；其他 Manager 已准备完成并等待同步阶段调用。
- 终局判断只读取 `IArmyController.GetArmyCount()`、`ISpawnManager.AreAllEnemySpawnsDispatched()` 和 `IEnemyManager.GetAliveEnemyCount()`。`ArmyReachedZero`、`MonsterKilled` 不触发即时终局。
- 判定终局时先把 LevelManager 状态改为 `Completed`，再禁用输入、停止未来生成和玩法阶段，最后提交一次 `LevelCompletion`。
- `StopRun` 和 SceneEntry 清理必须幂等。LevelManager 负责立即停止当前会话；SceneEntry 卸载清理负责取消订阅和执行兜底清理，不重复产生结果事实。

## 接口影响

- Army、Bullet、Enemy、Obstacle 需要暴露职责明确的本局生命周期和阶段方法，不引入笼统的通用 `ITickable`。
- 新增 `IBulletManager`，由它持有 Bullet 规范 Prefab、类型池、活动集合和回收流程。
- Input 通过依赖倒置的 `IGameplayInputController` 接受启停与逐帧输入命令；每帧输入 Tick 在 Army 移动与发射前执行，禁用时必须向 Army 提交一次零输入。详细实现切片由 ADR-036 定义。
- TimeService 仍是各时间域 delta 的权威来源，但由 LevelManager 在逻辑帧开始集中读取并传给各阶段，具体池对象不直接访问 TimeService。

## 不采用

- 不让多个 Manager 只靠各自 `Update` 和启动事件自行推进。
- 不把 Unity Script Execution Order 作为玩法确定性的唯一保障。
- 不使用逐帧 EventBus 事件驱动必须执行的阶段。
- 不让 LevelManager 接管各 Manager 的活动集合或模块内部规则。

## 验收标准

- 同一输入和配置下，阶段调用顺序固定且可由 EditMode 假实现记录验证。
- 每个 Playing 帧输入 Tick 先于 Army 移动与发射；不得依赖 Unity 同类脚本的隐式 `Update` 顺序。
- Auto Sync Transforms 关闭时，每个 Playing 逻辑帧在全部移动后、首次显式查询前准确同步一次 Transform。
- `spawnTime == 0` 的条目在首帧移动前只生成一次。
- 同帧最后一个敌人死亡且 Army 归零时仍判定 `GameOver`。
- 增删 EventBus 监听者不会改变帧阶段、伤害、计数和终局结果。
- Preparing、Completed 和过期 `LevelRunId` 不执行任何新玩法阶段。

## 关联文档

- `../01_Architecture/TimeSystem.md`
- `../02_Modules/Level/README.md`
- `../02_Modules/Spawn/README.md`
- `../02_Modules/Army/README.md`
- `../02_Modules/Bullet/README.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Obstacle/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/CollisionRules.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `ADR-036-DragOnlyInputImplementationSlice.md`
