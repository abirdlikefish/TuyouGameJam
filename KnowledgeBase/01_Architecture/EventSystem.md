# 事件系统

## 状态

ContractReady（设计状态，不代表已有 Unity 实现）

当前工程尚未创建 `EventBus`、事件载荷或相关测试代码。本文件定义实现边界、文件布局、订阅生命周期和新增事件流程；具体事件清单与载荷语义以 `../03_SharedContracts/EventCatalog.md` 为准。

## 设计目的

`EventBus` 用于传播已经发生、且允许零个监听者的跨模块事实。发布者先完成自己负责的状态变化，再发布事实；监听者只能更新自己的状态、表现或后续协调结果，不能成为发布者完成当前命令的隐式前提。MVP 完全无声音，不创建音频监听者。

需要立即执行、返回值、失败结果或确定顺序的操作使用类型化同步接口或必执行回调，不通过事件请求另一个模块执行私有逻辑。

## MVP 文件布局

当前不创建 `.asmdef`。目录只表达职责，后续程序集工程化可以在不搬移脚本的前提下进行。

```text
Assets/Scripts/Game/
├── Contracts/
│   └── Events/
│       ├── IEventBus.cs
│       ├── SubscriptionToken.cs
│       ├── ApplicationEvents.cs
│       ├── ArmyEvents.cs
│       ├── ObstacleEvents.cs
│       └── MonsterEvents.cs
└── Services/
    └── Events/
        └── EventBus.cs
```

- `IEventBus.cs`：保存唯一的跨模块订阅、发布和取消订阅接口。
- `SubscriptionToken.cs`：保存不透明订阅凭证；默认值无效，不暴露可由业务代码构造的订阅 ID。
- `EventBus.cs`：纯 C# 实现，不继承 `MonoBehaviour`，不对应独立 GameObject，由 `GlobalBootstrap` 创建和持有。
- `ApplicationEvents.cs`：应用流程、配置失败、场景握手和终局事件。
- `ArmyEvents.cs`：Army 人数、阵型、受击和装备事实。
- `ObstacleEvents.cs`：Gate、Prop 和道路对象回收事实。
- `MonsterEvents.cs`：敌人生成、受击、攻击命中和击杀事实。

一个事件必须有独立的消息类型，但不要求每个事件占用一个 `.cs` 文件。事件先按领域集中；只有文件过大、载荷复杂或需要独立维护时才拆分单事件脚本。事件消息是普通数据类型，不挂载到 GameObject。

## 公共接口

权威接口见 `../03_SharedContracts/PublicInterfaces.md`：

```csharp
public interface IEventBus
{
    SubscriptionToken Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
    void Unsubscribe(SubscriptionToken token);
}
```

`IEventBus` 不限制 `T` 必须是值类型。小型事件可以使用 `readonly struct`，大型不可变快照可以使用 `sealed class`。

## EventBus 分发语义

- EventBus 只在 Unity 主线程使用；MVP 不引入锁、后台线程分发或异步队列。
- `Publish<T>` 只匹配准确的 `typeof(T)`，不向基类或接口订阅者做多态广播。
- `Publish<T>` 同步执行；方法返回时，本次发布的所有处理器都已完成或已被异常隔离。
- 发布开始时复制当前事件类型的处理器快照，并按注册顺序调用。
- 发布期间新增或取消订阅只影响下一次发布；不得通过活动标记跳过已进入当前快照的处理器。
- 允许同步嵌套发布。每层 `Publish` 使用自己的订阅快照，例如 `AppSceneReady(Gameplay)` 处理器可以立即发布 `LevelRunStarted`。
- 同一处理器重复订阅视为多个独立订阅，每次返回独立 `SubscriptionToken`。
- 单个处理器抛出异常时报告错误并继续调用当前快照中的后续处理器。
- 无监听者的发布正常返回，不产生错误。
- `Unsubscribe` 幂等；默认 Token、未知 Token、已取消 Token 或属于其他 EventBus 实例的 Token 均不产生副作用。

推荐实现使用按消息类型维护的有序订阅列表，并为每个订阅分配单调递增 ID。`SubscriptionToken` 内部同时关联 EventBus 身份和订阅 ID，防止不同 EventBus 实例之间交叉取消。发布时复制强类型 `Action<T>` 列表，不使用 `Delegate.DynamicInvoke`。

## 异常报告

MVP 不创建 `DebugService`。`EventBus` 构造时接收 `Action<Exception>` 异常报告委托，`GlobalBootstrap` 可以传入 `Debug.LogException`，测试则传入可收集异常的替代委托。

异常报告委托自身失败也不能阻断剩余事件处理器。EventBus 不把处理器异常重新抛给发布者，也不使用事件再次报告事件处理异常，避免递归故障。

## 事件载荷规则

- 小型、字段固定且频繁发布的事实默认使用 `readonly struct`，例如 `LevelRunStarted`、`SoldierHit`。
- 包含大型槽位快照、多个集合或复制成本明显的事实使用不可变 `sealed class`。
- 无论使用值类型还是引用类型，发布后的字段语义都必须不可变。
- `readonly struct` 中的数组、列表仍是可变引用；发布前必须生成只读快照或防御性副本。
- `sealed class` 构造时必须复制外部传入的可变集合，不能直接保存发布者持有的 `List<T>`。
- EventBus 不接收字符串事件名、`object Data` 或通用字典载荷；事件由消息类型区分。
- Gameplay、跨场景或可能延迟处理的事件携带 `LevelRunId`；涉及具体运行时对象时还携带稳定的 `RuntimeInstanceId`。
- 事件只携带跨模块消费者需要的快照，不携带发布者组件、可变控制器或场景 GameObject 引用。

## 订阅所有权与生命周期

### 应用级服务

`GameStateService` 等应用级服务保存自己取得的 Token，并在自身清理阶段逐一取消。应用流程订阅必须在 `ConfigService.Initialize(...)` 之前完成，保证初始化失败和场景握手事实不会丢失。

服务可以在具体实现上提供 `StartListening()` / `Dispose()`，但不把这两个生命周期方法加入业务消费者使用的公共服务接口。重复启动必须幂等，避免注册两套处理器。

### Gameplay 场景组件

每个 SceneEntry 先接收本场景所需的最小服务并建立场景订阅，再报告入口准备完成。GameplaySceneEntry 还必须注入 `LevelConfig`、`LevelId`、`LevelRunId` 并完成 `LevelManager.Preparing`，SceneService 才能发布 `AppSceneReady(Gameplay)`：

```text
加载 GameplayScene
→ 场景入口注入依赖与 LevelRunId
→ 场景监听者完成订阅
→ LevelManager 完成 Preparing
→ SceneService 发布 AppSceneReady(Gameplay)
→ GameStateService 同步发布 LevelRunStarted
```

场景组件在 `OnDestroy` 或明确的会话清理入口取消订阅。即使已正确取消，Gameplay 处理器仍必须校验消息的 `LevelRunId`，拒绝过期会话。

### 池对象

Bullet、Monster、Gate、Prop 等池对象不自行查找或长期持有全局 EventBus，也不在每次启用时直接订阅全局事件。对应 Manager 在复用时传入当前会话 ID、运行时实例 ID 和必要的类型化回调；归还对象池前清空回调、计时器和会话状态。

池对象可以通过 Manager 注入的发布委托报告表现事实，但必须执行的登记、注销、存活统计和回收流程使用 Manager 回调或类型化接口完成，不能依赖允许零监听者的 EventBus。

## 典型链路

```text
GlobalBootstrap → GameStateService.NotifyInitializationReady()
GameStateService → SceneService.SwitchToMainMenu() → AppSceneReady(MainMenu)
GameStateService → MainMenu 定时器 → SceneService.SwitchToLevelSelect() → AppSceneReady(LevelSelect)
GameStateService → LevelSelect 定时器 → TrySelectLevel / TryStartSelectedGameplay
GameStateService → SceneService.SwitchToGameplay(levelId, levelConfig, levelRunId)
SceneService → AppSceneReady / AppSceneLoadFailed / AppSceneUnloadFailed → GameStateService
GameStateService → LevelRunStarted → LevelManager、UI
Gate / Prop → 调用 IArmyController 类型化命令 → Army 状态变更
Gate / Prop → GateValueChanged、ElementGateDamageChanged、GateContactResolved、PropBroken、PropContactDamage → UI、VFX
ArmyController → ArmyCountChanged、ArmyFormationChanged、SoldierHit、ArmyWeaponChanged、ArmyElementDurationChanged、ArmyElementExpired → UI、ArmyVisual、VFX
Monster → 必执行回调通知 EnemyManager 死亡
EnemyManager → 更新 AliveEnemyCount → 发布 MonsterKilled → VFX
LevelManager → GameStateService.CompleteGameplay(LevelCompletion)
GameStateService → SceneService.SwitchToLevelSelect()
SceneService → AppSceneUnloaded(Gameplay) → 加载并初始化 LevelSelectScene
SceneService → AppSceneReady(LevelSelect) → GameStateService → LevelSelect
```

`GameStateService` 负责应用流程和结果事件；`LevelManager` 负责当前 Gameplay 会话的玩法运行、终局判定和结果提交。`Victory`、`GameOver` 是结果事实，不是应用流程状态。

Gate/Prop 必须先通过 `IArmyController` 完成玩法状态变更，再发布接触、击破或伤害事实。Monster 死亡时必须先通过 EnemyManager 的必执行回调完成死亡去重和 `AliveEnemyCount` 更新，再由 EnemyManager 发布 `MonsterKilled`；事件监听者不承担敌人注销、计数或回收的必需步骤。

元素门 HP 清空后的伤害累计属于 Gate 自身同步规则，不由事件监听者完成。`ElementGateDamageChanged` 只报告 HP 伤害、额外伤害、累计可兑换伤害和奖励锁定状态；接触成功后若计算持续时间为 `0`，只发布成功的 `GateContactResolved`，不发布并不存在的 `ArmyElementDurationChanged`。

## 新增或修改事件的流程

1. 判断交互是否是已经发生的事实；命令、查询、必须执行的回调不进入 EventBus。
2. 明确唯一事实所有者、发布者、主要监听者和零监听者时的正确行为。
3. 定义不可变载荷；Gameplay 事件加入 `LevelRunId`，实例事件加入运行时实例 ID。
4. 修改跨模块事件、载荷或发布所有权前，先在 `../06_Decisions` 新增或更新 ADR。
5. 在 `../03_SharedContracts/EventCatalog.md` 登记事件名、发布者、监听者和参数。
6. 新增公共类型、错误码或接口时同步 `../03_SharedContracts/PublicInterfaces.md`；新增共享字段语义时同步 `DataDictionary.md`。
7. 在发布模块和监听模块 README 中记录使用方式，不复制第二份权威载荷定义。
8. 在 `../05_Testing/IntegrationTests.md` 增加正常、重复、异常、过期会话和零监听者验证。
9. 在 `../07_Changes/ChangeLog.md` 登记影响范围，再创建或修改对应 C# 消息类型。

`EventSystem.md` 只维护事件机制和通用实现规则；具体事件清单始终以 `EventCatalog.md` 为准。

## 实现顺序

1. 实现并独立验证 `SubscriptionToken`、`IEventBus` 和 `EventBus`。
2. 实现应用流程事件，接通 Config、Scene、GameState 的失败与场景握手链路。
3. 在 Army、Obstacle、Monster 模块实现时分别补齐领域事件载荷，不提前复制尚未实现的领域契约。
4. 接入 Gameplay 场景订阅、会话过滤和清理。
5. 最后接入 UI、ArmyVisual 和 VFX 等允许缺席的表现监听者。

## 验收标准

- 同步调用、注册顺序、订阅快照、重复订阅、异常隔离和幂等取消符合 ADR-021。
- 精确类型匹配、嵌套发布、零监听者、默认/跨 Bus Token 均有测试。
- `AppSceneReady(Gameplay) → LevelRunStarted` 同步嵌套链路不会因订阅时机漏失。
- 场景加载失败不发布 `LevelRunStarted`，过期 `LevelRunId` 不影响新会话。
- Gameplay 场景卸载后不再调用旧场景监听者。
- 池对象复用不会携带旧回调、订阅、会话 ID 或运行时实例 ID。
- 增删 UI/VFX 监听者不改变 Army、Gate、Prop、Monster 或终局结果。

## 关联文档

- `GlobalServices.md`
- `BootstrapAndComposition.md`
- `ApplicationFlow.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/EventCatalog.md`
- `../05_Testing/IntegrationTests.md`
- `../06_Decisions/ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `../06_Decisions/ADR-029-EventBusImplementationAndPayloads.md`
- `../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`
