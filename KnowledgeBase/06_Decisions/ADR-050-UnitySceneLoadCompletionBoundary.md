# ADR-050：Unity 场景加载完成边界

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-032、ADR-039、ADR-047

## 背景

批次 7.5C 接入四个正式场景后，真实 Play Mode 证明 Unity 2022 的 `SceneManager.LoadScene` 即使使用非 `Async` API，也可能在返回时尚未把 Additive 目标场景标记为 `isLoaded`。原 `ISceneRuntime.Load` 同步返回结果并立即解析根节点，会把已经开始加载且随后成功出现的场景误判为 `SceneLoadFailed`。

首场景启动也存在同类边界：Bootstrap 对象在 `Awake` 时可以取得有效场景与正确路径，但 `Scene.isLoaded` 可能仍为 `false`。继续放宽场景校验会掩盖真实装配错误，因此需要把 Unity 完成通知变成明确边界。

## 决策

1. `ISceneRuntime.Load` 改为完成回调形式；`ISceneService` 的公开切换命令和应用事件不变。
2. `UnitySceneRuntime` 使用 `LoadSceneAsync(..., Additive)`。只在 `AsyncOperation.completed` 后取得 Scene、检查 `isLoaded`、解析固定根和 SceneEntry、执行 Entry 初始化并设置 Active Scene。
3. `SceneService` 在回调前保持 `Loading`；只有 Runtime 返回成功后才发布 `AppSceneReady`。失败且目标场景已加载时仍先清理和卸载，再发布 `AppSceneLoadFailed`。
4. 应用场景卸载继续使用异步完成回调。切换顺序仍是先清理并卸载旧场景，再加载新场景；同一时刻至多一个应用场景 Ready。
5. `GlobalBootstrap.Awake` 只完成序列化引用校验和 Create。其 `Start` 协程等待一帧，再执行 Connect、`DontDestroyOnLoad` 和 Start，确保首场景加载状态稳定且 SceneRuntime 在迁移前保存 BootstrapScene。

## 后果

- SceneService 的业务所有权、状态推进、Ready/Failed 事实和调用方接口不变。
- `Loading` 同时覆盖 Unity 加载、固定入口解析和 Entry 初始化；不再维护不能被外部观察的 `InitializingEntry` 子状态。
- 场景切换多一个自然帧边界，但不引入加载界面、并发加载、进度展示或取消机制。
- Runtime 必须拒绝重入 Load/Unload，回调只完成一次，失败清理继续沿用既有幂等规则。

## 验收

- 从 BootstrapScene 启动可依次得到 MainMenu、LevelSelect、Gameplay 的 EntryInitialized 和 Ready。
- 旧应用场景卸载完成后才加载下一场景。
- Gameplay 完成后返回 LevelSelect，再进入新一局时 `LevelRunId` 递增且无残留 SceneEntry。
- 场景根、Entry 或 Inspector 引用错误仍阻止 Ready，而不是因异步适配被忽略。

## 关联文档

- `../01_Architecture/ApplicationFlow.md`
- `../01_Architecture/BootstrapAndComposition.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
