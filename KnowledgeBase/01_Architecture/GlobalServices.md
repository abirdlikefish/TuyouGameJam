# 全局服务

## 服务清单

| 服务 | 作用 | 优先级 |
|---|---|---|
| `GlobalBootstrap` | 初始化和销毁全局服务 | 必须 |
| `TimeService` | 时间域、暂停、时停、定时器 | 必须 |
| `GameStateService` | 应用流程状态、游玩会话进入/退出和结果分发 | 必须 |
| `EventBus` | 跨模块事件通信 | 必须 |
| `SceneService` | Gameplay 场景加载/卸载和流程交接 | 必须 |
| `ConfigService` | 读取 `LevelCatalog`、选定的 `LevelConfig`、唯一的 Luban `cfg.Tables` 实例和资源注册表 | 必须 |
| `InputService` | 统一桌面和触摸输入 | 建议 |
| `AudioService` | 音乐、音效、音量设置 | 建议 |
| `PoolService` | 高频对象复用 | 必须 |
| `SaveService` | 本地进度和设置 | 后续扩展（当前 MVP 不初始化） |
| `DebugService` | 调试数据和测试指令 | 建议 |

## 生命周期

```text
Bootstrap → 创建服务 → 注册事件 → 加载场景 → 场景注册玩法对象
→ 场景卸载 → 清理场景对象 → 保留全局服务
```

## 约束

- 只对基础设施使用全局访问点，不把每个玩法对象都做成单例。
- 服务初始化顺序必须固定：事件 → 时间 → 状态 → 共享配置（LevelCatalog、Luban Tables + 资源注册表）→ 音频/对象池 → 应用流程；具体 `LevelConfig` 在选关确定后交给 Gameplay。
- 场景重载时不得创建重复的 `GlobalRoot`。
- MainMenu 和 LevelSelect 当前可以由 `GameStateService` 表示，不要求创建实际场景；离开这两个状态的自动跳过计时使用 RealTime。
- `ConfigService` 初始化失败时保持应用在 `Initializing`，并发布带错误码的 `InitializationFailed`；未成功初始化不得进入 MainMenu。
- `GlobalBootstrap` 在确认 `ConfigService.GetConfigLoadState() == Ready` 后调用 `GameStateService.NotifyInitializationReady()`；`GameStateService` 负责后续 `MainMenu`、`LevelSelect` 和 Gameplay 流程推进。
- MainMenu/LevelSelect 的 RealTime 定时器由 `GameStateService` 持有并在离开状态、加载失败或终局时取消；UI 不直接创建流程定时器。
- `SceneService` 只执行 `LoadGameplay` / `UnloadGameplay`，通过 `GameplaySceneReady`、`GameplaySceneLoadFailed` 和 `GameplaySceneUnloaded` 与 `GameStateService` 握手，不直接修改应用状态。
