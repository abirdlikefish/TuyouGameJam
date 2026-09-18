# 对象池系统

## 状态

ContractReady（设计状态，不代表已有 Unity 实现）

## 目标与类型身份

`PoolService` 是由 `GlobalBootstrap` 持有的应用级服务，负责具体 `MonoBehaviour` 类型池的注册、实例复用和跨 Gameplay 场景的空闲实例生命周期。它不使用字符串资源键选择 Prefab，也不拥有玩法活动集合。

对象池遵守以下唯一性规则：

```text
一个具体池化根组件类型 T
→ 一个规范 Prefab
→ 一个应用级 IComponentPool<T> 实例
```

池身份使用准确的具体类型，不使用共同基类、配置 ID 或 Unity 资源键。同一类型以同一 Prefab 重复请求时返回已有池；同一类型绑定不同 Prefab 时报告注册冲突。只有数值或表现资源不同而生命周期一致的对象继续使用同一类型和规范 Prefab，由 Manager 在初始化时传入配置快照。

普通敌人、精英敌人和 Boss 使用 `NormalMonster`、`EliteMonster`、`BossMonster` 三个具体根脚本及三个规范 Prefab；公共移动、生命、目标查询和表现适配能力继续复用，不复制相同实现。

## 所有权与 Root

```text
GlobalRoot
├── ServiceHost
│   └── PoolService
└── PersistentPoolRoot
    ├── NormalMonster [Inactive]
    ├── EliteMonster [Inactive]
    ├── BossMonster [Inactive]
    ├── AdditiveGate [Inactive]
    ├── ElementGate [Inactive]
    ├── WeaponProp [Inactive]
    └── Bullet [Inactive]
```

- `GlobalBootstrap` 创建一个 PoolService，并通过 Inspector 绑定唯一的 `PersistentPoolRoot`。
- PoolService 为每个具体类型池维护一个空闲子节点；空闲实例全部失活并跨 Gameplay 场景保留。
- Gameplay Manager 通过 Inspector 持有自己需要的规范 Prefab，在场景初始化时调用 `GetOrCreatePool<T>(prefab)`，保存返回的类型池引用。
- PoolService 持有类型池；场景 Manager 只持有引用，Manager 销毁不会遗留无所有者的池。
- 活动实例不留在空闲 Root。Manager 取得实例后把它放入当前场景的 `MonsterRoot`、`ObstacleRoot`、`BulletRoot` 或其他职责节点。

## 公共契约

完整定义见 `../03_SharedContracts/PublicInterfaces.md`：

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

`GetOrCreatePool` 只接受规范 Prefab 根节点上的具体池化组件。Prefab 为空时抛出 `ArgumentNullException`；传入场景实例或组件不在 Prefab 根节点时抛出 `ArgumentException`；同类型绑定不同 Prefab 时抛出 `InvalidOperationException`。这些失败不得回退到运行时搜索、字符串键或另一个 Prefab。

## 借出、初始化与激活

类型池必须返回未激活实例。首次实例化同样不得在 Manager 完成初始化前触发有效 `OnEnable`：实现应在非激活创建层级中实例化，并在移出前保证根对象的 `activeSelf == false`。

```text
Manager 根据配置选择具体类型池
→ pool.RentInactive()
→ Manager 设置活动父节点、世界位置和旋转
→ Manager 分配 RuntimeInstanceId
→ Manager 注入配置快照、LevelRunId 和必要回调
→ Manager 登记活动集合
→ Manager 最后 SetActive(true)
```

PoolService 和类型池不设置玩法父节点、位置、旋转、配置或运行时 ID。池对象的 `Awake` 只缓存稳定组件引用；本次租用上下文必须从显式初始化入口取得。

## 结束、清理与防御性失活

池对象不持有完整 PoolService 或类型池，也不在 `OnDisable`、`OnDestroy` 中归还自身。对象在死亡动画完成、离场或命中结束时，通过 Manager 注入的窄回调请求结束当前实例。

```text
对象请求结束当前实例
→ Manager 校验 LevelRunId 与 RuntimeInstanceId
→ Manager 从活动集合注销并完成计数/事实事件
→ Manager 调用对象 PrepareForPool()
→ Manager 主动 SetActive(false)
→ pool.Return(instance)
→ 类型池防御性 SetActive(false)
→ 类型池将实例移入本类型空闲子节点
```

`PrepareForPool()` 属于具体玩法对象，负责清除 HP、数字、接触状态、命中标记、计时器、订阅、回调、会话 ID 和运行时 ID。类型池只保证实例归属、借出状态、失活和空闲层级，不调用玩法重置方法。

防御模式不替代 Manager 的主动清理：`Return` 再次调用 `SetActive(false)` 只是防止激活对象进入空闲池，不能补偿遗漏的业务注销或状态重置。

## 失败、幂等与清理

- `RentInactive` 在规范 Prefab 实例化失败时抛出包含具体组件类型的 `InvalidOperationException`；资源和 Inspector 引用应在进入 Gameplay 前验证。
- `Return` 只接受本类型池当前借出的实例。第一次合法归还返回 `true`；`null`、未知实例、其他类型池实例和重复归还返回 `false`，且不改变池状态。
- PoolService 可以使用注入的诊断委托记录注册和归还错误，但不为此创建 DebugService，也不发布玩法事件。
- MVP 使用懒创建，不预热、不配置容量上限、不暴露公共统计接口。空闲实例保留到应用退出；需要预热或容量策略时依据性能数据新增决策。
- Gameplay 终局时，各 Manager 的 `StopRun` 归还全部活动实例。场景卸载后不得遗留借出实例，也不得让旧 `LevelRunId` 的回调影响新会话。
- 应用退出或入口清理时，PoolService 销毁全部已知实例和类型池，并清除类型到 Prefab 的注册关系；正常 Gameplay 卸载前仍应由 Manager 先归还活动实例。

## 非职责

- 不决定生成时间、配置 ID、世界坐标、敌人存活统计或 Gate/Prop 接触结果。
- 不替代 EnemyManager、ObstacleManager 或子弹生成所有者的活动实例所有权。
- 不通过对象是否 Active 推断胜利、失败、死亡或时间轴完成。
- 不在归还时发布玩法死亡、离场、回收或终局事件。
- 不依赖 ConfigService、资源注册表、SpawnManager、EventBus 或具体玩法 Manager。
- 不允许池对象自行取得同类实例或绕过 Manager 归还自身。

## 验收标准

- 同一具体类型和同一 Prefab 重复请求得到同一类型池；同类型不同 Prefab 被明确拒绝。
- `RentInactive` 对首次创建和复用实例都返回未激活对象，Manager 初始化完成前不会执行依赖租用上下文的 `OnEnable` 逻辑。
- Manager 设置活动父节点、位置和旋转，分配新的 `RuntimeInstanceId`，完成初始化与登记后才激活对象。
- 第一次合法归还成功；重复、未知、跨池和空实例归还失败且不破坏池状态。
- 归还前实例已从 Manager 活动集合注销，业务状态已清除；类型池防御性失活并移入正确空闲子节点。
- 复用实例不会携带上一局 HP、数字、接触状态、命中标记、计时器、订阅、回调、`LevelRunId` 或 `RuntimeInstanceId`。
- `OnDisable` 和 `OnDestroy` 不调用类型池归还，失活过程不会递归释放。
- Gameplay 重开不创建第二个 PoolService、PersistentPoolRoot 或同类型池；空闲实例可以跨局复用。
- Manager 在场景卸载前归还全部活动实例；过期会话对象不会登记进新会话。
- 对象池存在与否不改变玩法事实事件、计数和胜负结果。

## 关联决策

- `../06_Decisions/ADR-014-SharedRuntimeContractBaseline.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
- `../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`

## 关联文档

- `GlobalServices.md`
- `BootstrapAndComposition.md`
- `SceneStructure.md`
- `../02_Modules/Spawn/README.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Obstacle/README.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `../05_Testing/PerformanceTests.md`
