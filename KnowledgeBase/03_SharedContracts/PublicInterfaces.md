# 公共接口

## 获取与注入约定

- 本文件中的服务接口由 `GlobalBootstrap` / Composition Root 创建实现并注入消费者，不为每个服务定义静态 `Instance`。
- 纯 C# 状态所有者优先构造注入；Unity 场景组件使用明确的初始化入口或 Inspector 引用。不得通过运行时 `Find` 或通用 Service Locator 隐式取得必需依赖。
- Gameplay 场景装配入口只向每个 Manager 提供它实际需要的最小接口；Bullet、Monster、Gate、Prop 等池对象接收配置快照、会话 ID、实例 ID 和必要回调，不接收完整全局服务集合。
- 类型化接口承载必须执行、需要返回值或有顺序要求的操作；`IEventBus` 只传播已经发生且允许零监听者的事实。

## 配置与应用流程接口

```csharp
public interface IArmyConfigProvider
{
    ArmyConfigSnapshot GetArmyConfig(int armyId);
}

public interface IWeaponConfigProvider
{
    WeaponConfigSnapshot GetWeaponConfig(int weaponId);
}

public interface IBulletConfigProvider
{
    BulletConfigSnapshot GetBulletConfig(int bulletId);
}

public interface IEnemyConfigProvider
{
    EnemyConfigSnapshot GetEnemyConfig(int enemyId);
}

public interface IPropConfigProvider
{
    PropConfigSnapshot GetPropConfig(int propId);
}

public interface IConfigService :
    IArmyConfigProvider,
    IWeaponConfigProvider,
    IBulletConfigProvider,
    IEnemyConfigProvider,
    IPropConfigProvider
{
    ConfigLoadState GetConfigLoadState();
    IReadOnlyList<LevelDescriptor> GetLevelDescriptors();
    bool TryGetLevelConfig(int levelId, out LevelConfigSnapshot levelConfig);
}

public readonly struct ArmyConfigSnapshot
{
    public int Id { get; }
    public int ArmyCountLimit { get; }
    public int HpPerSoldier { get; }
    public float MoveSpeed { get; }
}

public readonly struct WeaponConfigSnapshot
{
    public int Id { get; }
    public float FireInterval { get; }
    public int BulletId { get; }
}

public readonly struct BulletConfigSnapshot
{
    public int Id { get; }
    public int Damage { get; }
    public float MoveSpeed { get; }
}

public readonly struct EnemyConfigSnapshot
{
    public int Id { get; }
    public EnemyType EnemyType { get; }
    public int MaxHp { get; }
    public int AttackPower { get; }
    public float MoveSpeed { get; }
    public float AttackStartRange { get; }
    public float AttackCooldown { get; }
}

public readonly struct PropConfigSnapshot
{
    public int Id { get; }
    public PropType PropType { get; }
    public int WeaponId { get; }
    public int ArmyAddition { get; }
    public int MaxHp { get; }
    public int ContactDamage { get; }
    public float MoveSpeed { get; }
}

public enum EnemyType
{
    Chick = 0,
    Hen = 1,
    Rooster = 2,
    Ikun = 3
}

public enum AttackType
{
    SingleTarget = 0,
    Area = 1
}

public enum GateType
{
    Additive = 0,
    Element = 1
}

public enum ObstacleKind
{
    Gate = 0,
    Prop = 1
}

public enum ObstacleState
{
    MovingDown = 0,
    ContactPending = 1,
    ContactSucceeded = 2,
    ContactFailed = 3,
    ExitedUncontacted = 4,
    Broken = 5,
    Recycled = 6
}

public enum GateContactState
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    ExitedUncontacted = 3
}

public enum PropContactState
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    ExitedUncontacted = 3
}

public enum ArmyCountChangeReason
{
    Addition = 0,
    Damage = 1,
    RemovalRequest = 2
}

public enum ArmyReachedZeroReason
{
    Damage = 0,
    RemovalRequest = 1
}

public enum GameOverReason
{
    ArmyReachedZero = 0
}

public enum ObstacleRecycleReason
{
    ContactResolved = 0,
    Broken = 1,
    ExitedRoad = 2,
    StopRun = 3
}

public interface IResourceRegistry
{
    bool TryGet<T>(string key, out T asset) where T : UnityEngine.Object;
}
```

```csharp
public enum ConfigLoadState
{
    Uninitialized,
    Loading,
    Ready,
    Failed
}

public enum ConfigErrorCode
{
    EmptyCatalog,
    DuplicateLevelId,
    LevelNotFound,
    MissingReference,
    InvalidLevelConfig,
    TableLoadFailed,
    ResourceMissing
}

public readonly struct LevelDescriptor
{
    public int LevelId { get; }
    public string DisplayName { get; }
    public bool InitiallyUnlocked { get; }
}

public sealed class LevelConfigSnapshot
{
    public int LevelId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<int> UnlockedLevelIds { get; }
    public float RoadWidth { get; }
    public float RoadHeight { get; }
    public Vector2 ArmySpawnPosition { get; }
    public float SpawnY { get; }
    public float EnemyApproachY { get; }
    public float DespawnY { get; }
    public IReadOnlyList<EnemySpawnEntrySnapshot> EnemySpawns { get; }
    public IReadOnlyList<GateSpawnEntrySnapshot> GateSpawns { get; }
    public IReadOnlyList<PropSpawnEntrySnapshot> PropSpawns { get; }
    public int IkunBasketballConfigId { get; }
    public float ElementDurationSecondsPerDamage { get; }
}

public readonly struct EnemySpawnEntrySnapshot
{
    public float SpawnTime { get; }
    public float SpawnPosition { get; }
    public int ConfigId { get; }
}

public readonly struct GateSpawnEntrySnapshot
{
    public float SpawnTime { get; }
    public float SpawnPosition { get; }
    public GateType GateType { get; }
    public int InitialValue { get; }
    public ElementType ElementType { get; }
    public int MaxHp { get; }
}

public readonly struct PropSpawnEntrySnapshot
{
    public float SpawnTime { get; }
    public float SpawnPosition { get; }
    public int ConfigId { get; }
}
```

`GlobalBootstrap` 通过 Inspector 提供 `LevelCatalog` 和资源注册表，并创建唯一的 Luban `cfg.Tables` 实例，再直接调用 Foundation 中具体 `ConfigService.Initialize(...)`；初始化方法不属于 `IConfigService` 查询契约。ConfigService 在初始化时一次性验证完整目录、目录内全部 LevelConfig、五类表的数值/主键/枚举及当前跨表引用，然后把资产和表数据复制为不可变快照字典。表或目录错误按 ADR-041 只记录首个明确错误并立即终止应用，不进入 MainMenu，不提供默认配置、恢复事件或重试；ADR-044 允许的 `unlockedLevelIds` 目录缺失 ID 只记录 `Debug.LogWarning` 并从快照中过滤。

`LevelId`、五类 Luban 表主键和外键均为非负整数，`0` 是合法 ID；每张 Luban 表的首行 `Id = 0`，除明确固定的 ID 外不要求后续编号连续。不得使用 `0` 表示缺失配置；不存在的可选引用必须使用可空值或明确的条件字段表达。活动 Gameplay 的 `LevelRunId` 仍必须大于 `0`，`0` 只表示没有活动会话；MVP 唯一 Army 使用 `ArmyId = TbArmy.Id = 0`。

五类 `GetXxxConfig` 只允许在 `Ready` 后对已通过引用校验的 ID 调用；非 Ready 查询或未知 ID 表示程序不变量被破坏，直接抛出异常，Gameplay Manager 不重复捕获并降级。`TryGetLevelConfig` 只保留给应用层验证外部关卡选择请求。Composition 分别注入最小 Provider：ArmyController 取得 Army/Weapon，BulletManager 取得 Bullet，EnemyManager 取得 Enemy，ObstacleManager 取得 Prop；它们都不需要完整 `IConfigService`。

```csharp
public interface IGameStateService
{
    AppFlowState GetAppFlowState();
    int GetSelectedLevelId();
    int GetCurrentLevelRunId();
    void NotifyInitializationReady();
    bool TryEnterLevelSelect();
    bool IsLevelUnlocked(int levelId);
    bool IsLevelCompleted(int levelId);
    bool TrySelectLevel(int levelId);
    bool TryStartSelectedGameplay();
    bool TryReturnToLevelSelect();
    bool TryRetryCurrentGameplay();
    bool TryStartNextGameplay();
    void CompleteGameplay(LevelCompletion completion);
}
```

```csharp
public interface IPlayerProgressStore
{
    bool TryLoad(out PlayerProgressSnapshot progress, out string diagnostic);
    bool TrySave(PlayerProgressSnapshot progress, out string diagnostic);
}
```

`GameStateService` 是 `AppFlowState`、运行期完成集合和解锁集合的唯一所有者。`NotifyInitializationReady` 只接受 `ConfigService` 已为 `Ready` 的情况，先通过 `IPlayerProgressStore` 加载并与目录默认状态合并，再请求 `SceneService.SwitchToMainMenu()`；只有 `AppSceneReady(MainMenu)` 后才进入 MainMenu。`IsLevelUnlocked` 与 `IsLevelCompleted` 只查询规范化后的运行期集合。存储实现只负责版本化 JSON 文件，不判断解锁规则，也不订阅 Victory/GameOver。

`TryEnterLevelSelect` 只允许在 MainMenuScene Ready 且没有待处理切换时请求 LevelSelect；`TrySelectLevel` 只允许选择当前已解锁且仍能取得配置的关卡；`TryStartSelectedGameplay` 创建新的 `LevelRunId`、进入 `GameplayLoading` 并调用 SceneService。Gameplay Ready 后仍等待匹配的 `LevelIntroFinished`，随后才进入 Gameplay 并发布 `LevelRunStarted`。没有活动会话时 `GetCurrentLevelRunId` 返回 `0`。

`CompleteGameplay` 只接受当前 `LevelRunId` 的结果；重复或过期结果必须忽略。接受 Victory 后把当前 LevelId 加入完成与解锁集合，再合并快照中已按 ADR-044 过滤的 `UnlockedLevelIds`；集合有变化时在发布 Victory 前同步保存。存储失败只记录错误，不撤销运行期状态或阻断 `GameplayResult`。GameOver 和主动退出不写进度。

`TryReturnToLevelSelect` 在 `Gameplay` 中表示主动放弃且不产生结果，在 `GameplayResult` 中表示结束结果展示。`TryRetryCurrentGameplay` 只接受当前结果为 GameOver 的 `GameplayResult`，使用当前关卡配置和新的 `LevelRunId` 重新进入 `GameplayLoading`。`TryStartNextGameplay` 只接受当前结果为 Victory 且过滤后的 `UnlockedLevelIds` 非空的 `GameplayResult`，使用列表首项作为下一关；目标必须已进入运行期解锁集合且仍能取得配置。重试和下一关都通过 SceneService 重新加载 GameplayScene，任何重复点击、过期状态或已有 pending scene 时返回 `false`。

```csharp
public interface ISceneService
{
    void SwitchToMainMenu();
    void SwitchToLevelSelect();
    void SwitchToGameplay(
        int levelId,
        LevelConfigSnapshot levelConfig,
        int levelRunId);
}
```

`SceneService` 不再次查询或解析关卡配置。调用方必须先通过 `IConfigService.TryGetLevelConfig` 取得已校验的 `LevelConfigSnapshot`，再把同一 `levelId`、快照和新的 `LevelRunId` 传入。三个命令都表示切换目标：如果已有应用场景，SceneService 先清理并异步卸载旧场景，再异步 Additive 加载目标场景；Unity 完成回调后才解析固定根 SceneEntry 并显式初始化。SceneService 不推进 `AppFlowState`，也不得在 Entry 未就绪时伪装 Ready。

```csharp
public readonly struct AppSceneReady
{
    public AppSceneId SceneId { get; }
    public int LevelId { get; }
    public int LevelRunId { get; }
}

public readonly struct AppSceneUnloaded
{
    public AppSceneId SceneId { get; }
    public int LevelId { get; }
    public int LevelRunId { get; }
}

public readonly struct LevelIntroFinished
{
    public int LevelId { get; }
    public int LevelRunId { get; }
    public LevelIntroEndReason Reason { get; }
}
```

MainMenu 和 LevelSelect 的 `LevelId`、`LevelRunId` 固定为 `0`。Gameplay 事实必须携带当前值。`AppSceneReady` 只能在目标场景加载、规范 Entry 初始化、场景监听者订阅完成，且 Gameplay 的 `LevelManager.Preparing` 完成后发布；`AppSceneUnloaded` 只描述旧场景异步卸载完成。

`LevelIntroEndReason` 包含 `Completed`、`NoVideoConfigured`、`PlaybackFailed`、`PreparationTimedOut`。四种原因都会释放开场门禁；该枚举用于诊断结束路径，不改变玩法结果。

## 军队接口

```csharp
public enum ElementType
{
    None = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3
}

[Flags]
public enum ElementMask : byte
{
    None = 0,
    Fire = 1 << 0,
    Ice = 1 << 1,
    Lightning = 1 << 2
}

public enum ArmyWeaponChangeReason
{
    WeaponPickup = 0,
    ElementActivated = 1,
    ElementExpired = 2
}

public interface IHorizontalInputReceiver
{
    void SetHorizontalInput(float value);
}

public interface IArmyController : IHorizontalInputReceiver
{
    int GetArmyCount();
    int GetActiveSlotCount();
    int GetSlotCapacity();
    int GetCurrentWeaponId();
    ArmyAdditionResult AddArmy(int amount);
    ArmyRemovalResult RemoveArmy(int amount);
    void ApplySlotDamage(int slotIndex, int damage);
    bool TryGetNearestActiveSlot(Vector2 origin, out ArmySlotTarget target);
    bool TryGetSlotTarget(int slotIndex, out ArmySlotTarget target);
    void ApplyWeaponPickup(int weaponId, int sourceRuntimeInstanceId);
    ElementDurationChangeResult AddElementDuration(
        ElementType elementType,
        float duration,
        int sourceRuntimeInstanceId);
    // 返回只读槽位快照，不暴露槽位内部对象或可变集合。
    ArmyFormationSnapshot GetFormationSnapshot();
    ArmyElementStateSnapshot GetElementStateSnapshot();
}
```

`IArmyController` 保存 Army 的玩法命令与查询；LevelManager 通过单独的会话接口驱动生命周期和每帧阶段，避免让 Gate、Prop、Monster 或 Input 取得不需要的启动能力：

```csharp
public interface IArmyRunController : IArmyController
{
    void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
    void TickMovementAndFire(int levelRunId, float gameplayDeltaTime);
    void EnterVictoryPresentation(int levelRunId);
    void EnterDefeatPresentation(int levelRunId);
    void StopRun(int levelRunId);
}

public interface IGameplayInputGate
{
    void SetGameplayEnabled(bool enabled);
}

public interface IGameplayInputController : IGameplayInputGate
{
    void TickInput(float unscaledDeltaTime);
}
```

`IHorizontalInputReceiver` 是 Input 所需的最小玩法命令面，ArmyController 通过 `IArmyController` 继承并实现它。GameplayInputAdapter 只接收该最小接口，不取得 Army 的人数、伤害、装备或本局生命周期能力。

`ArmyWeaponChanged` 携带 `ArmyWeaponChangeReason`；`WeaponPickup` 与 `ElementActivated` 的 `SourceRuntimeInstanceId` 有值，`ElementExpired` 为 `null`。元素获得或过期只有在实际 WeaponId 改变时才发布该事件。

`EnterVictoryPresentation(levelRunId)` 只接受当前会话，停止 Army 后续移动、射击和玩法命令，保留活动 SoldierVisual 并播放 Victory。`EnterDefeatPresentation(levelRunId)` 同样停止玩法命令，但不覆盖正在播放的槽位 Death，使 GameOver 结果页仍可显示最终死亡动画。两个入口都不发布结果事实、不决定终局，也不替代 `StopRun`；Gameplay 场景卸载或显式清理仍调用 `StopRun` 复位身份、Collider、数值和视觉。

`IGameplayInputController` 由 GameplayInputAdapter 实现并通过 Composition 注入 LevelManager。LevelManager 在每个 Playing 帧、Army 移动与发射之前调用一次 `TickInput(unscaledDeltaTime)`。禁用必须幂等，并立即重置 Pointer 状态、调用当前接收者的 `SetHorizontalInput(0)`；这些接口只控制当前场景输入适配器，不创建全局 InputService。

`SetHorizontalInput` 接受有限的有符号横向输入倍率，并覆盖 Army 保存的当前横向移动意图。当前只由相对拖拽调用，最终值允许位于 `[-horizontalMultiplier, horizontalMultiplier]`。Army 必须拒绝 NaN 或无穷值，但不得把已经乘拖拽系数的有限值再次 Clamp 到 `[-1,1]`。该命令要求当前 Army 直接接收，不通过 `EventBus` 广播，Army 也不反向读取 Input Adapter。

相对拖拽使用 ADR-028 与 ADR-036 的语义：`TouchDragInput` 累计相邻 Pointer 采样点在 `TouchDragArea` 局部空间中的水平差，按区域宽度和传入的 `unscaledDeltaTime` 换算为归一化滑动速度，先 Clamp 到 `[-1,1]`，再乘 UI Prefab Inspector 中默认值为 `1` 的 `horizontalMultiplier`。没有新 Drag 差值、Pointer 结束、输入被禁用或 Gameplay 离开 `Playing` 时必须提交 `0`，不得保留上一帧值。Army 的实际横向位移使用 `horizontalInput × TbArmy.MoveSpeed × 有效玩法 delta`。当前不读取键盘、手柄或旧 Input Manager 的 `Horizontal` 轴。

MVP 只有一个 Army，固定 `ArmyId = 0` 同时选择 `TbArmy.Id = 0` 和序列化 `ArmyPrefabBinding.ArmyId = 0`；所有需要 Army 身份的事件和去重上下文使用该值，不增加运行时 Army ID 分配接口。最大可见士兵数由所选 Prefab 的序列化 `ArmySlotView[]` 长度派生，不能从 Luban 读取第二份容量。

Gate 和 Prop 使用 `IArmyController` 的方法同步提交玩法状态变更：非负加法门调用 `AddArmy`，负数加法门把绝对值作为正的请求减员人数调用 `RemoveArmy`；成功元素门仅在计算持续时间大于 `0` 时调用 `AddElementDuration`，计算结果为 `0` 时仍成功但不提交空命令；失败元素门和道具调用 `ApplySlotDamage`，当前武器箱调用 `ApplyWeaponPickup`。调用方完成本地去重和状态转换后执行命令，再发布对应事实事件；ArmyController 不通过订阅这些事件重复执行命令。

```csharp
public readonly struct ArmyAdditionResult
{
    public int RequestedAddition { get; }
    public int ActualAddition { get; }
    public int RemainingArmyCount { get; }
}

public readonly struct ArmyRemovalResult
{
    public int RequestedRemoval { get; }
    public long RequestedDamage { get; }
    public long AppliedDamage { get; }
    public int ActualArmyCountLoss { get; }
    public int RemainingArmyCount { get; }
}

public readonly struct ElementDurationChangeResult
{
    public ElementType ElementType { get; }
    public float PreviousDuration { get; }
    public float AddedDuration { get; }
    public float CurrentDuration { get; }
}

public readonly struct ArmyElementStateSnapshot
{
    public float FireRemainingDuration { get; }
    public float IceRemainingDuration { get; }
    public float LightningRemainingDuration { get; }
    public ElementMask ActiveElements { get; }
}
```

`AddArmy(amount)` 接受大于等于 `0` 的请求增员人数，按 `ArmyCountLimit` 截断后返回请求值、实际增加量和剩余总人数；`ArmyCountLimit = 0` 表示没有配置上限。`RemoveArmy(amount)` 只接受正的请求减员人数。Army 使用 `long` 计算 `amount × HpPerSoldier` 伤害预算，按 `CurrentHp` 升序、再按 `SlotIndex` 升序让激活槽位承担，并把未耗尽伤害继续传给下一个槽位。请求减员和实际人数损失可能不同，返回结果必须报告两者。`ElementType.None` 只作为不使用元素字段时的配置空值；`AddElementDuration` 必须拒绝 `None`，并只接受有限且大于 `0` 的持续时间，同类型重复获得时累加。

`ApplySlotDamage` 保持 `void`，不返回 `SlotDamageResult`。调用方必须先用 `TryGetSlotTarget` 确认槽位仍有效，再同步提交正伤害；无效或空槽位不提交。Army 在方法返回前完成权威 HP、人数和事实事件更新。攻击者不读取实际损失推进自己的状态，Monster 在攻击动画结束后的下一次 `TickMovement` 重新验证或选择目标。

## 子弹命中与可受伤接口

```csharp
public interface IDamageable
{
    void TakeDamage(int damage);
    bool IsAlive { get; }
}

// 子弹统一查询此接口；可命中状态不从 HP 或 IsAlive 推断。
public interface IBulletHittable
{
    bool CanReceiveBulletHit { get; }
    void ReceiveBulletHit(BulletDamageContext damage);
}

public readonly struct BulletDamageContext
{
    public int BulletInstanceId { get; }
    public int BulletId { get; }
    public int WeaponId { get; }
    public ElementMask ActiveElements { get; }
    public int Damage { get; }
    public Vector2 HitPosition { get; }
    public Vector2 HitDirection { get; }
}
```

## 道路对象管理接口

Gate 和 Prop 都是由 `ObstacleManager` 登记的道路对象。管理器只暴露只读快照，不暴露内部对象或可变集合：

```csharp
public interface IObstacleRegistry
{
    IReadOnlyList<RoadObjectSnapshot> GetActiveObjects();
    IReadOnlyList<RoadObjectSnapshot> GetActiveObjects(ObstacleKind kind);
    bool TryGetObject(int runtimeInstanceId, out RoadObjectSnapshot snapshot);
}
```

每个道路对象必须拥有唯一的 `RuntimeInstanceId`。Gate 不再拥有配置 ID；Prop 的 `TbProp.Id` 只表示配置，不用于区分同时存在的实例。两类对象都保存对应生成列表中的 `SpawnEntryIndex` 作为本局来源诊断。

```csharp
public readonly struct RoadObjectSnapshot
{
    public int RuntimeInstanceId { get; }
    public int SpawnEntryIndex { get; }
    public int? ConfigId { get; }
    public ObstacleKind Kind { get; }
    public Vector2 WorldPosition { get; }
    public bool IsOnRoad { get; }
    public ObstacleState State { get; }
}
```

`State` 只用于只读观察；具体的门/道具状态机仍由对象自身维护。

`RoadObjectSnapshot.State` 的统一映射见 ADR-021：正常移动为 `MovingDown`，检测到接触候选但尚未结算为 `ContactPending`，专用接触结果映射到 `ContactSucceeded`、`ContactFailed` 或 `ExitedUncontacted`；道具击破但尚未回收时为 `Broken`。`Recycled` 仅用于回收事实或注销前瞬时状态，已注销对象不得出现在活动查询结果中。

槽位碰撞适配需要额外携带 `ArmyId` 和 `SlotIndex`，由 Army 负责把伤害转换为槽位人数和聚合生命值变化。

Physics2D 查询使用显式身份代理解析 Collider：Enemy、Gate、Prop 的受击 Collider 节点必须绑定 `BulletHitProxy`，Army 的 SlotCollider 节点必须绑定 `ArmySlotHitProxy`。代理与 Collider 位于同一 GameObject，并通过 Inspector 显式绑定根玩法对象或 `ArmySlotView`；Gameplay Preparing 在运行前验证绑定。查询只读取命中节点上的代理，不使用 `GetComponentInParent`、`Find` 或缺失引用兜底。同一查询按代理提供的 RuntimeInstanceId 或 SlotIndex 去重。

```csharp
public readonly struct ArmyFormationSnapshot
{
    public int ArmyCount { get; }
    public int ActiveSlotCount { get; }
    public IReadOnlyList<ArmySlotSnapshot> Slots { get; }
}

public readonly struct ArmySlotSnapshot
{
    public int SlotIndex { get; }
    public int RepresentedCount { get; }
    public int CurrentHp { get; }
    public int MaxHp { get; }
    public bool IsActive { get; }
}

public readonly struct ArmySlotTarget
{
    public int SlotIndex { get; }
    public Vector2 WorldPosition { get; }
    public int RepresentedCount { get; }
    public bool IsActive { get; }
}
```

## 关卡与道路接口

`LevelManager` 对需要查询当前游玩会话的模块提供只读信息，不暴露生成列表或可变运行时集合：

```csharp
public interface ILevelRuntime
{
    LevelRunState GetRunState();
    int GetLevelRunId();
    float GetElapsedTime();
    RoadLayoutSnapshot GetRoadLayout();
}

public interface IGameplayHudSource
{
    GameplayHudSnapshot GetHudSnapshot();
}
```

`GameplayHudSnapshot` 包含当前 `LevelId`、`LevelRunId`、关卡显示名、玩法耗时、火/冰/雷剩余时间、击杀数、敌人总数和终局标记。Playing 时由 LevelManager 组合权威状态；Completed 后返回清理前冻结的最终快照。BattleHud 用 `KilledEnemyCount / TotalEnemyCount` 驱动 `Image.Type.Filled` 进度条，不直接显示击杀文本；UI 不自行累计击杀或按 Unity 对象数量推断统计。

```csharp
public enum LevelRunState
{
    Preparing,
    Playing,
    Completed
}

public enum AppFlowState
{
    Initializing,
    MainMenu,
    LevelSelect,
    GameplayLoading,
    Gameplay,
    GameplayResult
}

public enum AppSceneId
{
    MainMenu,
    LevelSelect,
    Gameplay
}

public enum LevelIntroEndReason
{
    Completed,
    NoVideoConfigured,
    PlaybackFailed,
    PreparationTimedOut
}

public enum LevelResult
{
    Victory,
    GameOver
}

public readonly struct LevelCompletion
{
    public int LevelId { get; }
    public int LevelRunId { get; }
    public LevelResult Result { get; }
}

public readonly struct RoadLayoutSnapshot
{
    public float Width { get; }
    public float Height { get; }
    public float LeftBoundary { get; }
    public float RightBoundary { get; }
    public float BottomBoundary { get; }
    public float TopBoundary { get; }
    public Vector2 ArmySpawnPosition { get; }
    public float SpawnY { get; }
    public float EnemyApproachY { get; }
    public float DespawnY { get; }
    public float BulletDespawnY { get; }
}

```

道路快照和所有位置字段使用世界 XY 坐标，运行时 `z = 0`，右方为 `+x`、上方为 `+y`。`LevelConfig.roadWidth`、`roadHeight` 是唯一序列化道路尺寸，中心固定为世界原点，四边由半宽和半高派生；ArmyRoot 每局从 `ArmySpawnPosition` 开始且世界 Y 固定为该配置值。道路不使用玩法 Collider。生成项的 `SpawnPosition` 必须已校验为 `[0,1]`，SpawnManager 按 `Lerp(LeftBoundary, RightBoundary, SpawnPosition)` 计算中心点 `x`，并使用 `SpawnY` 作为 `y`。SpawnY、EnemyApproachY、DespawnY 和 BulletDespawnY 都比较根 GameObject 中心，不考虑 Collider、Renderer 或 Prefab 尺寸；Army 横向合并 AABB 是明确例外。

配置校验必须满足 Army 根坐标位于道路内、`BottomBoundary <= DespawnY < ArmySpawnPosition.y < EnemyApproachY < SpawnY <= TopBoundary`，并单独满足 `ArmySpawnPosition.y < BulletDespawnY <= TopBoundary`；BulletDespawnY 可以低于 EnemyApproachY 或 SpawnY。无效值不得在运行时 Clamp 或回退到 TopBoundary、场景 Renderer Bounds。依赖 Prefab Collider 的初始阵型 AABB 若越过左右边界，则在 Preparing 失败。

## 时间接口

```csharp
public interface ITimeService
{
    float GetDeltaTime(TimeDomain domain);
    TimerHandle Schedule(float seconds, Action callback, TimeDomain domain);
}
```

`ConfigId` 对 Gate 为 `null`，对 Prop 为 `TbProp.Id`；不得使用 `0` 伪装 Gate 配置 ID。`SpawnEntryIndex` 只在当前 `LevelConfigSnapshot` 的对应生成列表内有意义，不是跨资产稳定 ID。

`IDamageable` 只表达真正以生命值决定存活的对象。Enemy、Prop 等对象可以同时实现 `IDamageable` 和 `IBulletHittable`；加法门没有 HP，只实现 `IBulletHittable`。Pending 元素门即使 `CurrentHp == 0`，在接触结算前仍保持 `CanReceiveBulletHit == true`，后续子弹继续消费并累计 HP 归零后的额外伤害。元素门进入 `Failed` 后奖励永久锁定；若生命周期仍允许 `CanReceiveBulletHit == true`，命中只能用于表现或其他已明确的生命周期处理，不得增加可兑换额外伤害。BulletManager 不能用 `IsAlive` 或目标 HP 替代 `CanReceiveBulletHit`。

`TimerHandle` 必须支持幂等取消。MainMenu 和 LevelSelect 已改为显式 UI 命令，不再创建应用页面自动跳过计时器。

MVP 中所有时间域倍率固定为 `1`。当前公共契约不包含倍率查询/修改、暂停令牌、减速、加速或局部时停；未来启用前另行定案。

`TimeDomain` 是一次移动、计时或调度所选择的时间策略，不是对象可以同时加入的标签集合。每次操作只选择一个最具体的域，不把 `Gameplay` delta 与 `Bullet`、`Gate`、`Monster` 或 `VFX` delta 重复累计。MVP 归属固定为：

- MainMenu 和 LevelSelect 都由 UI 命令推进，不使用时间域自动跳过。
- LevelManager 在逻辑帧开始集中读取 `Gameplay`、`Bullet`、`Gate` 和 `Monster` delta。Army 与未细分玩法消费传入的 `Gameplay` delta；SpawnManager 只消费 LevelManager 累计的 `elapsedTime`。
- BulletManager 消费传入的 `Bullet` delta；EnemyManager 消费传入的 `Monster` delta；ObstacleManager 及 Gate/Prop 消费传入的 `Gate` delta。具体池对象不直接访问 `ITimeService`。
- `VFX` 只表示跟随 Gameplay 世界推进的视觉特效；UI 动画和应用流程表现不使用该域。

当前枚举不提供运行时父子关系或重叠归属。未来启用时间控制时，按 ADR-030 的单父级方向重新确认接口；不得由消费者自行组合多个域。

```csharp
public readonly struct TimerHandle
{
    public bool IsValid { get; }
    public void Cancel();
}
```

```csharp
public enum TimeDomain
{
    RealTime,
    Gameplay,
    Bullet,
    Gate,
    Monster,
    VFX
}
```

## SpawnManager 接口

```csharp
public interface ISpawnManager
{
    // 绑定本关配置、切换当前会话并将三个游标归零。
    void StartRun(
        LevelConfigSnapshot levelConfig,
        RoadLayoutSnapshot roadLayout,
        int levelRunId);
    void Tick(int levelRunId, float elapsedTime);
    bool AreAllEnemySpawnsDispatched(int levelRunId);
    void StopRun(int levelRunId);
}
```

SpawnManager 是三个时间轴游标的唯一所有者；LevelManager 只能调用这些接口，不能直接访问生成列表或游标。每次进入 Gameplay 只调用一次 `StartRun`；它同时完成配置绑定、会话切换和游标重置，不再暴露语义重复的 `ResetRun`。

```csharp
public readonly struct EnemySpawnRequest
{
    public int LevelRunId { get; }
    public int SpawnEntryIndex { get; }
    public int ConfigId { get; }
    public float SpawnPosition { get; }
    public Vector2 WorldPosition { get; }
}

public readonly struct GateSpawnRequest
{
    public int LevelRunId { get; }
    public int SpawnEntryIndex { get; }
    public GateType GateType { get; }
    public int InitialValue { get; }
    public ElementType ElementType { get; }
    public int MaxHp { get; }
    public float ElementDurationSecondsPerDamage { get; }
    public float SpawnPosition { get; }
    public Vector2 WorldPosition { get; }
}

public readonly struct PropSpawnRequest
{
    public int LevelRunId { get; }
    public int SpawnEntryIndex { get; }
    public int ConfigId { get; }
    public float SpawnPosition { get; }
    public Vector2 WorldPosition { get; }
}

public readonly struct BasketballSpawnRequest
{
    public int LevelRunId { get; }
    public int SourceEnemyRuntimeInstanceId { get; }
    public int ConfigId { get; }
    public Vector2 WorldPosition { get; }
}
```

`GateSpawnRequest` 来自已校验的 `LevelConfigSnapshot.GateSpawns`，不携带 Gate ConfigId。`EnemySpawnRequest.ConfigId` 与 `PropSpawnRequest.ConfigId` 分别引用 `TbEnemy.Id`、`TbProp.Id`。时间轴请求的 `SpawnEntryIndex` 只用于本局来源诊断。`BasketballSpawnRequest` 由存活且仍处于接近线前的 Ikun 同步提交，携带 `LevelConfigSnapshot.IkunBasketballConfigId` 与来源敌人运行时 ID，不伪造时间轴索引。

## BulletManager 接口

```csharp
public readonly struct BulletSpawnRequest
{
    public int LevelRunId { get; }
    public int SourceArmyId { get; }
    public int SourceSlotIndex { get; }
    public int BulletId { get; }
    public int WeaponId { get; }
    public ElementMask ActiveElements { get; }
    public Vector2 WorldPosition { get; }
    public Vector2 Direction { get; }
}

public interface IBulletManager
{
    void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
    void Spawn(BulletSpawnRequest request);
    void TickMovementAndHits(int levelRunId, float bulletDeltaTime);
    void FlushPendingRecycles(int levelRunId);
    void StopRun(int levelRunId);
}
```

BulletManager 通过 Inspector 持有唯一 Bullet 规范 Prefab，并以具体 `Bullet` 根类型取得类型池。它根据 `BulletSpawnRequest.BulletId` 通过注入的 `IBulletConfigProvider` 取得已校验的 `BulletConfigSnapshot`；WeaponId、ActiveElements、来源槽位、位置和方向以发射瞬间快照为准。完成父节点、Transform、配置、LevelRunId、BulletInstanceId、回调和活动登记后才激活实例。飞行中的子弹不读取 Army 当前元素计时器，Army 后续获得或失去元素不会修改既有快照。

## EnemyManager 与 ObstacleManager 接口

```csharp
public interface IEnemyManager
{
    void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout, int ikunBasketballConfigId);
    void Spawn(EnemySpawnRequest request);
    void TickMovement(int levelRunId, float monsterDeltaTime);
    void ResolveAttacks(int levelRunId, float monsterDeltaTime);
    void FlushPendingRecycles(int levelRunId);
    int GetAliveEnemyCount();
    int GetActiveEnemyCount();
    void StopRun(int levelRunId);
}
```

```csharp
public interface IBasketballSpawner
{
    void SpawnBasketball(BasketballSpawnRequest request);
}

public interface IObstacleManager : IObstacleRegistry, IBasketballSpawner
{
    void StartRun(int levelRunId, RoadLayoutSnapshot roadLayout);
    void Spawn(GateSpawnRequest request);
    void Spawn(PropSpawnRequest request);
    void TickMovement(int levelRunId, float gateDeltaTime);
    void ResolveContacts(int levelRunId);
    void FlushPendingRecycles(int levelRunId);
    void StopRun(int levelRunId);
}
```

Manager 在 `StartRun` 时切换当前会话并清空上局状态，只接受当前 `LevelRunId` 的请求，并负责活动实例、阶段规则、延后回收和会话清理；SpawnManager 不复制这些集合。阶段方法只由 LevelManager 在 `Playing` 中调用，池对象不得通过独立 Update 绕过该顺序。伤害或死亡事实在对应阶段立即完成状态与计数，`FlushPendingRecycles` 只处理已登记的结束请求和对象池归还，不延迟权威数值结果。

首轮切片不为 `StartRun/StopRun` 增加通用 Result 或错误恢复状态机。配置数据已由 ConfigService 在加载场景前完成致命校验；GameplaySceneEntry 在调用任何 StartRun 前只集中校验本次会话参数、Prefab、Collider、Layer 和 Inspector 引用，发现错误时记录稳定来源并停止 Ready。StartRun 中发生的意外异常只触发已启动模块的必要 StopRun 清理，不允许使用缺省值或降级继续游玩。

## EventBus 与 PoolService 接口

```csharp
public readonly struct SubscriptionToken
{
    public bool IsValid { get; }
}

public interface IEventBus
{
    SubscriptionToken Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
    void Unsubscribe(SubscriptionToken token);
}
```

`SubscriptionToken` 是不透明值，内部关联 EventBus 身份和单次订阅 ID；业务代码只检查 `IsValid` 并交回原 EventBus 取消，不读取或构造内部 ID。默认 Token、未知 Token、已取消 Token 和其他 EventBus 的 Token 取消时均无副作用。

`Publish` 只在 Unity 主线程同步执行，只匹配准确的消息类型，并按注册顺序调用；发布开始时固定订阅快照，发布期间的订阅变更只影响下一次发布。允许同步嵌套发布，每层发布拥有独立快照。重复订阅各自拥有独立 Token，单个处理器异常经注入的异常报告委托记录后继续调用其他处理器，`Unsubscribe` 幂等。

`IEventBus` 不对 `T` 增加 `struct` 或 `class` 约束。小型、字段固定或高频事件默认使用 `readonly struct`；包含大型快照、多个集合或明显复制成本的事件使用不可变 `sealed class`。两者都不得在发布后被修改，可变集合必须在构造时复制为快照。完整实现、生命周期和文件布局见 `../01_Architecture/EventSystem.md` 与 ADR-029。

```csharp
public interface IPoolService
{
    IComponentPool<T> GetOrCreatePool<T>(T prefab)
        where T : MonoBehaviour;
}

public interface IComponentPool<T> where T : MonoBehaviour
{
    T RentInactive();
    bool Return(T instance);
}
```

对象获取和归还属于对应 Manager 与类型池的协作，不属于 SpawnManager。全局 PoolService 持有全部类型池；Manager 通过 Inspector 持有规范 Prefab，并以准确的具体根组件类型取得或创建类型池。一个具体类型只允许绑定一个规范 Prefab；同类型同 Prefab 返回已有池。Prefab 为空时抛出 `ArgumentNullException`，传入场景实例或组件不在 Prefab 根节点时抛出 `ArgumentException`，同类型绑定不同 Prefab 时抛出 `InvalidOperationException`。

`RentInactive` 对首次创建和复用实例都返回未激活对象。Manager 负责活动父节点、位置、旋转、配置、`LevelRunId`、`RuntimeInstanceId`、回调、活动登记和最终激活。归还前 Manager 注销并清理业务状态、调用对象的 `PrepareForPool()` 并主动失活；`Return` 再次防御性失活并移入类型空闲 Root。第一次合法归还返回 `true`；空、未知、跨池或重复归还返回 `false` 且不改变池状态。

池化对象不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身；它只通过 Manager 注入的窄回调请求结束当前实例。完整生命周期见 `../01_Architecture/PoolSystem.md` 与 ADR-031。

Unity 资源注册表键仍可用于非池身份的配置与表现资源绑定，但不用于 PoolService 选择 Prefab。池化规范 Prefab 由对应 Manager 的 Inspector 引用提供。

## 场景失败事件数据

配置与必需资源校验失败不定义项目事件载荷。Luban 表、LevelCatalog 或 LevelConfig 数据错误由 ConfigService 使用 `Debug.LogError` 输出一次 `ConfigErrorCode`、稳定来源和具体原因，将状态置为 `Failed` 并立即终止应用。ADR-044 允许的 `unlockedLevelIds` 目录缺失 ID 不属于失败：ConfigService 对每个缺失条目调用一次 `Debug.LogWarning`，从快照中过滤后继续初始化。Prefab、Collider、Layer 或 Inspector 装配错误由对应入口记录并阻止 GameplaySceneEntry Ready。任何路径都不得使用缺省值伪造缺失配置。

```csharp
public enum SceneLoadErrorCode
{
    InvalidRequest,
    SceneNotConfigured,
    SceneLoadFailed,
    SceneEntryMissing,
    SceneEntryAmbiguous,
    SceneEntryTypeMismatch,
    SceneEntryInitializationFailed
}

public enum SceneUnloadErrorCode
{
    InvalidRequest,
    SceneUnloadFailed
}

public readonly struct AppSceneLoadFailed
{
    public AppSceneId SceneId { get; }
    public int LevelId { get; }
    public int LevelRunId { get; }
    public SceneLoadErrorCode ErrorCode { get; }
}

public readonly struct AppSceneUnloadFailed
{
    public AppSceneId SceneId { get; }
    public int LevelId { get; }
    public int LevelRunId { get; }
    public SceneUnloadErrorCode ErrorCode { get; }
}
```

配置错误日志中的 Source 使用稳定的配置项、表名、资源键、字段或条目索引，不得依赖本地绝对路径。ConfigService 只报告首个配置错误并退出，不发布配置失败或恢复事件。场景失败事件只报告事实，不负责推进应用状态。目标加载或 Entry 初始化失败时，SceneService 必须先异步清理失败场景再发布 `AppSceneLoadFailed`；旧场景卸载失败时发布 `AppSceneUnloadFailed`，不得继续加载目标场景。

## 碰撞契约

所有参与玩法命中、接触、受击或阻挡的运行时对象都必须通过 Inspector 绑定 `Collider2D`。碰撞形状、Layer、查询方向和结算所有权见 `CollisionRules.md`。

核心玩法使用显式 Cast/Overlap 查询，不以 `OnTriggerEnter2D`、`OnCollisionEnter2D` 或 Dynamic Rigidbody2D 的自动移动、推挤作为权威规则。按 ADR-055，Enemy、Gate 和 Prop 规范 Prefab 根节点必须提供固定配置的 Kinematic Rigidbody2D 查询适配，Bullet 保持无刚体；该组件不进入模块间接口，也不取得移动、阻挡或结算所有权。

## 约束

公共接口只暴露模块真正需要的能力，不暴露完整控制器或可变内部数据。
