# 公共接口

## 配置与应用流程接口

```csharp
public interface IConfigService
{
    void Initialize(
        LevelCatalog levelCatalog,
        cfg.Tables tables,
        IResourceRegistry resourceRegistry);
    ConfigLoadState GetConfigLoadState();
    IReadOnlyList<LevelDescriptor> GetLevelDescriptors();
    bool TryGetLevelConfig(int levelId, out LevelConfig levelConfig);
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
```

`GlobalBootstrap` 通过 Inspector 提供 `LevelCatalog` 和资源注册表，并创建唯一的 Luban `cfg.Tables` 实例，再调用 `IConfigService.Initialize`。`TryGetLevelConfig` 只返回已通过校验的关卡资产。初始化或选定关卡失败时发布 `InitializationFailed` 或 `LevelConfigLoadFailed`，不得返回缺省配置。

```csharp
public interface IGameStateService
{
    AppFlowState GetAppFlowState();
    int GetSelectedLevelId();
    int GetCurrentLevelRunId();
    void NotifyInitializationReady();
    bool TrySelectLevel(int levelId);
    bool TryStartSelectedGameplay();
    void CompleteGameplay(LevelCompletion completion);
}
```

`GameStateService` 是 `AppFlowState` 的唯一推进者。`NotifyInitializationReady` 只接受 `ConfigService` 已为 `Ready` 的情况；`TrySelectLevel` 只允许在 `LevelSelect` 选择当前可选关卡；`TryStartSelectedGameplay` 负责校验选定关卡、创建新的 `LevelRunId`、切换到内部过渡状态 `GameplayLoading` 并调用 `SceneService.LoadGameplay`。没有活动会话时 `GetCurrentLevelRunId` 返回 `0`。

`CompleteGameplay` 只接受当前 `LevelRunId` 的结果；重复或过期结果必须忽略。结果接受后由 `GameStateService` 调用 `SceneService.UnloadGameplay`，收到匹配的 `GameplaySceneUnloaded` 后才回到 `LevelSelect`。

```csharp
public interface ISceneService
{
    void LoadGameplay(
        int levelId,
        LevelConfig levelConfig,
        int levelRunId);
    void UnloadGameplay(int levelRunId);
}
```

`SceneService` 不再次查询或解析关卡配置。调用方必须先通过 `IConfigService.TryGetLevelConfig` 取得已校验的 `LevelConfig`，再把同一 `levelId`、配置引用和新的 `LevelRunId` 传入。`LoadGameplay` 是异步边界：场景加载完成、`LevelManager` 完成 `Preparing` 初始化后发布 `GameplaySceneReady`；失败时发布 `GameplaySceneLoadFailed`。`SceneService` 不推进 `AppFlowState`，也不得在未就绪时伪装成进入 `Playing`。卸载完成后发布 `GameplaySceneUnloaded`。

```csharp
public readonly struct GameplaySceneReady
{
    public int LevelId { get; }
    public int LevelRunId { get; }
}

public readonly struct GameplaySceneUnloaded
{
    public int LevelId { get; }
    public int LevelRunId { get; }
}
```

## 军队接口

```csharp
public interface IArmyController
{
    int GetArmyCount();
    int GetActiveSlotCount();
    int GetMaxDeployedSoldiers();
    void AddArmy(int amount);
    void RemoveArmy(int amount);
    void ApplySlotDamage(int slotIndex, int damage);
    bool TryGetNearestActiveSlot(Vector2 origin, out ArmySlotTarget target);
    bool TryGetSlotTarget(int slotIndex, out ArmySlotTarget target);
    void ApplyGateEffect(GateEffect effect);
    void ApplyWeaponPickup(int weaponId, int sourceRuntimeInstanceId);
    void ApplyElement(int elementId, int sourceRuntimeInstanceId);
    void SetHorizontalInput(float value);

    // 返回只读槽位快照，不暴露槽位内部对象或可变集合。
    ArmyFormationSnapshot GetFormationSnapshot();
}
```

MVP 只有一个 Army，所有需要 Army 身份的事件和去重上下文使用固定 `ArmyId = 1`；不增加运行时 Army ID 分配接口。

`GateEffect` 只描述已经通过接触判定的结果；负数加法门的 `ArmyCountDelta` 仍然可以为负数。

Gate 和 Prop 使用 `IArmyController` 的方法同步提交玩法状态变更：门调用 `ApplyGateEffect` 或 `ApplySlotDamage`，当前 MVP 的武器箱调用 `ApplyWeaponPickup`，道具接触失败调用 `ApplySlotDamage`。调用方完成本地去重和状态转换后执行命令，再发布对应事实事件；ArmyController 不通过订阅这些事件重复执行命令。`ApplyWeaponPickup` 是当前 MVP 的具体道具效果接口，不代表未来所有 Prop 都必须修改武器；扩展边界见 ADR-022。

```csharp
public readonly struct GateEffect
{
    public int RuntimeInstanceId { get; }
    public GateType GateType { get; }
    public int ArmyCountDelta { get; }
    public int ElementId { get; }
    public bool ContactSucceeded { get; }
}
```

## 可受伤接口

```csharp
public interface IDamageable
{
    void TakeDamage(int damage);
    bool IsAlive { get; }
}

// 需要根据子弹、武器和元素来源选择反馈的目标实现此扩展接口。
public interface IBulletDamageable : IDamageable
{
    void TakeDamage(BulletDamageContext damage);
}

public readonly struct BulletDamageContext
{
    public int BulletInstanceId { get; }
    public int BulletId { get; }
    public int WeaponId { get; }
    public int ElementId { get; }
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

每个道路对象必须拥有唯一的 `RuntimeInstanceId`。配置表中的 `GateId` 或 `PropId` 只表示配置，不用于区分同时存在的实例。

```csharp
public readonly struct RoadObjectSnapshot
{
    public int RuntimeInstanceId { get; }
    public int ConfigId { get; }
    public ObstacleKind Kind { get; }
    public Vector2 WorldPosition { get; }
    public bool IsOnRoad { get; }
    public ObstacleState State { get; }
}
```

`State` 只用于只读观察；具体的门/道具状态机仍由对象自身维护。

`RoadObjectSnapshot.State` 的统一映射见 ADR-021：正常移动为 `MovingDown`，检测到接触候选但尚未结算为 `ContactPending`，专用接触结果映射到 `ContactSucceeded`、`ContactFailed` 或 `ExitedUncontacted`；道具击破但尚未回收时为 `Broken`。`Recycled` 仅用于回收事实或注销前瞬时状态，已注销对象不得出现在活动查询结果中。

槽位碰撞适配需要额外携带 `ArmyId` 和 `SlotIndex`，由 Army 负责把伤害转换为槽位人数和聚合生命值变化。

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
    public float CurrentHp { get; }
    public float MaxHp { get; }
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
```

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
    Gameplay
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
    public float SpawnY { get; }
    public float EnemyApproachY { get; }
    public float DespawnY { get; }
}
```

道路快照和所有位置字段使用世界 XY 坐标，运行时 `z = 0`，右方为 `+x`、上方为 `+y`；世界原点由道路 Prefab/场景决定，不属于公共契约。生成项的 `SpawnPosition` 必须已校验为 `[0,1]`，SpawnManager 按 `Lerp(LeftBoundary, RightBoundary, SpawnPosition)` 计算中心点 `x`，并使用 `SpawnY` 作为 `y`。计算不考虑对象尺寸；敌人到达 `EnemyApproachY` 后由 Monster 选择最近的有效士兵槽位。

## 时间接口

```csharp
public interface ITimeService
{
    float GetDeltaTime(TimeDomain domain);
    float GetTimeScale(TimeDomain domain);
    void SetTimeScale(TimeDomain domain, float scale);
    PauseToken PushPause(TimeDomain domain, string reason);
    TimerHandle Schedule(float seconds, Action callback, TimeDomain domain);
}
```

`PauseToken` 必须支持释放；`TimerHandle` 必须支持取消。所有 RealTime 自动跳过计时都使用 `RealTime` 时间域。

MVP 中所有时间域倍率和对象局部倍率固定为 `1`，不启用运行时倍率调整；暂停、减速、加速和局部时停规则延后。

```csharp
public readonly struct PauseToken
{
    public void Dispose();
}

public readonly struct TimerHandle
{
    public bool IsValid { get; }
    public void Cancel();
}

public readonly struct SubscriptionToken
{
    public bool IsValid { get; }
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
    void StartRun(LevelConfig levelConfig, int levelRunId);
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
    public int ConfigId { get; }
    public float SpawnPosition { get; }
    public Vector2 WorldPosition { get; }
}

public readonly struct ObstacleSpawnRequest
{
    public int LevelRunId { get; }
    public int ConfigId { get; }
    public ObstacleKind Kind { get; }
    public float SpawnPosition { get; }
    public Vector2 WorldPosition { get; }
}
```

## EnemyManager 与 ObstacleManager 接口

```csharp
public interface IEnemyManager
{
    void StartRun(int levelRunId);
    void Spawn(EnemySpawnRequest request);
    int GetAliveEnemyCount();
    int GetActiveEnemyCount();
    void StopRun(int levelRunId);
}
```

```csharp
public interface IObstacleManager : IObstacleRegistry
{
    void StartRun(int levelRunId);
    void Spawn(ObstacleSpawnRequest request);
    void StopRun(int levelRunId);
}
```

Manager 在 `StartRun` 时切换当前会话并清空上局状态，只接受当前 `LevelRunId` 的请求，并负责活动实例、回收和会话清理；SpawnManager 不复制这些集合。

## EventBus 与 PoolService 接口

```csharp
public interface IEventBus
{
    SubscriptionToken Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
    void Unsubscribe(SubscriptionToken token);
}
```

`Publish` 同步执行并按注册顺序调用；发布开始时固定订阅快照，发布期间的订阅变更只影响下一次发布。重复订阅各自拥有独立 Token，单个处理器异常隔离后继续调用其他处理器，`Unsubscribe` 幂等。完整语义见 ADR-021。

```csharp
public interface IPoolService
{
    GameObject Get(string key, Vector3 position, Quaternion rotation);
    void Release(GameObject instance);
}
```

对象获取和归还属于 PoolService，不属于 SpawnManager。

资源注册表键使用 Unity 侧大小写敏感的 ASCII `类别/身份` 格式，例如 `Enemy/Normal`、`Gate/Additive`、`Prop/Weapon/{WeaponId}`、`Bullet/{BulletId}`；不使用绝对路径或 Luban 资源键。

## 失败事件数据

```csharp
public readonly struct InitializationFailed
{
    public ConfigErrorCode ErrorCode { get; }
    public string Source { get; }
}

public readonly struct LevelConfigLoadFailed
{
    public int LevelId { get; }
    public ConfigErrorCode ErrorCode { get; }
    public string Source { get; }
}

public enum SceneLoadErrorCode
{
    InvalidRequest,
    SceneLoadFailed,
    GameplayBootstrapMissing
}

public readonly struct GameplaySceneLoadFailed
{
    public int LevelId { get; }
    public int LevelRunId { get; }
    public SceneLoadErrorCode ErrorCode { get; }
}
```

`Source` 使用稳定的配置项、表名或资源键，供日志和 UI 定位；不得放入异常堆栈或本地绝对路径。失败事件只报告事实，不负责重试或推进应用状态。

## 碰撞契约

所有参与玩法命中、接触、受击或阻挡的运行时对象都必须通过 Inspector 绑定 `Collider2D`。碰撞形状、Layer、查询方向和结算所有权见 `CollisionRules.md`。

核心玩法使用显式 Cast/Overlap 查询，不以 `OnTriggerEnter2D`、`OnCollisionEnter2D` 或 Dynamic Rigidbody2D 的自动移动、推挤作为权威规则。`Rigidbody2D` 仅可作为后续特定对象的 Kinematic 适配，不属于模块间必需接口。

## 约束

公共接口只暴露模块真正需要的能力，不暴露完整控制器或可变内部数据。
