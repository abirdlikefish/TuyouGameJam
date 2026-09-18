# ADR-031：按具体类型持有的组件对象池与防御性失活

## 状态

Accepted

## 日期

2026-09-18

## 背景

现有 `IPoolService` 以字符串资源键取得 `GameObject`，但尚未定义返回类型、激活状态、首次实例化时的 `OnEnable` 顺序、同类型多 Prefab 冲突和重复归还语义。玩法 Manager 已经拥有活动集合、`LevelRunId`、`RuntimeInstanceId`、生成坐标和业务清理顺序；继续让 PoolService 同时选择资源、设置玩法 Transform 或驱动对象状态会扩大基础设施职责。

项目决定以具体的池化根 `MonoBehaviour` 类型作为池身份。普通敌人、精英敌人和 Boss 将使用不同的根脚本与规范 Prefab，为后续不同玩法逻辑保留明确边界；共同的移动、生命、目标查询和表现适配能力仍应提取复用，不复制三套相同实现。

## 决策

### 类型、Prefab 与池身份

- 一个具体的池化根组件类型只允许绑定一个规范 Prefab，并对应一个应用级 `IComponentPool<T>` 实例；池身份使用准确的具体类型 `T`，不使用共同基类、字符串资源键或配置 ID。
- 同一具体类型再次以同一 Prefab 请求池时返回已有池。Prefab 为空时抛出 `ArgumentNullException`，传入场景实例或组件不在 Prefab 根节点时抛出 `ArgumentException`，同一类型尝试绑定另一个 Prefab 时抛出 `InvalidOperationException`；不得静默创建第二个池。
- `NormalMonster`、`EliteMonster`、`BossMonster` 分别作为三个具体池化根类型，对应三个独立 Prefab。该决定取代 ADR-005 中“三种敌人共享一个轻量敌人行为控制器”和“不为三种敌人创建独立控制器”的部分；ADR-005 的接近、攻击、碰撞、计数和回收规则继续有效。
- 只有配置或表现资源不同而玩法生命周期相同的对象继续使用同一具体根类型和同一规范 Prefab，由 Manager 在初始化时传入配置快照并绑定表现；不得仅为区分对象池而创建空子类或重复 Prefab。

### 所有权与绑定

- 全局 `PoolService` 由 `GlobalBootstrap` 创建并持有，负责全部类型池及其应用级生命周期；Gameplay Manager 只保存注入后取得的 `IComponentPool<T>` 引用。
- Manager 通过 Inspector 持有自己需要的具体 Prefab，在场景初始化时调用 `IPoolService.GetOrCreatePool<T>(prefab)`；PoolService 不依赖 Unity 资源注册表，也不按资源键选择 Prefab。
- `PoolService` 持有唯一的 `PersistentPoolRoot`，并为每个具体类型池维护独立的空闲子节点。空闲实例跨 Gameplay 场景保留；活动实例由 Manager 放入当前场景的 `MonsterRoot`、`ObstacleRoot`、`BulletRoot` 或其他职责节点。
- 池化根组件必须位于规范 Prefab 根节点。辅助 MonoBehaviour 可以被多个 Prefab 复用，不参与池身份。

### 借出、初始化与激活

- `IComponentPool<T>.RentInactive()` 必须返回未激活的具体组件实例。首次实例化也不得在 Manager 完成初始化前触发有效 `OnEnable`；实现应在非激活创建层级中实例化，并在移出前保证根对象的 `activeSelf == false`。
- PoolService 不设置活动父节点、世界位置或旋转。Manager 在取得未激活实例后依次设置活动父节点与 Transform、分配 `RuntimeInstanceId`、注入配置、`LevelRunId` 和回调、登记活动集合，最后调用 `SetActive(true)`。
- 池对象的 `Awake` 只缓存自身稳定组件，不读取本次租用上下文；依赖配置、会话或 Manager 的逻辑从显式初始化入口开始。

### 归还与防御性失活

- 玩法对象只通过注入的窄回调请求结束当前实例，不持有完整 `IPoolService` 或 `IComponentPool<T>`，也不在 `OnDisable`、`OnDestroy` 中自行归还。
- Manager 校验当前 `LevelRunId` 和 `RuntimeInstanceId`，先从活动集合注销并完成计数、事实事件和业务清理，再调用对象的 `PrepareForPool()`，主动失活对象，最后归还对应类型池。
- 类型池采用防御模式：即使 Manager 已主动失活，`Return` 仍幂等调用 `SetActive(false)`，再把实例移动到本类型的空闲子节点。PoolService 不调用玩法重置方法，也不根据 Active 状态推断玩法结果。
- `Return` 对 `null`、未知实例、其他类型池实例和重复归还返回 `false`，不改变池状态；第一次合法归还返回 `true`。实现可通过注入的诊断委托记录错误，但不为此引入 DebugService。

### 容量与清理

- MVP 使用懒创建，不预热、不配置每类容量上限，也不暴露公共统计接口。空闲实例保留到应用退出，后续只在目标设备实测表明需要时新增预热或容量决策。
- Gameplay 结束时，各 Manager 的 `StopRun` 必须归还自己仍持有的全部活动实例；场景卸载后不得遗留借出实例。
- 应用退出或 `GlobalBootstrap` 清理时，PoolService 销毁全部已知实例和类型池，并清理类型到 Prefab 的注册关系；正常 Gameplay 卸载前仍由 Manager 先归还活动实例。

## 不采用

- 不按字符串资源键取得和返回无类型的 `GameObject`。
- 不用共同基类类型作为多个具体 Prefab 的池身份。
- 不让场景 Manager 长期拥有跨场景池或空闲 Root，否则场景卸载后会遗留无所有者的持久实例。
- 不由 PoolService 设置玩法位置、旋转、运行时 ID、配置或活动集合。
- 不在 `OnDisable` 中归还对象，也不允许对象绕过 Manager 直接归还自身。
- 不因为三种敌人存在公共逻辑而强制它们共享同一个池化根类型；公共能力通过组合、接口、基类或纯 C# 规则复用。

## 影响

- `PoolSystem.md` 可以在同步类型池、失败、归还、重置和清理语义后提升为 `ContractReady`。
- `IPoolService` 改为取得或创建 `IComponentPool<T>`；类型池返回具体 `MonoBehaviour`，不再返回 `GameObject`。
- PoolService 不再依赖资源注册表。资源注册表仍可用于配置和表现资源绑定，但不作为对象池身份或池化 Prefab 选择入口。
- EnemyManager 通过 Inspector 分别绑定 `NormalMonster`、`EliteMonster` 和 `BossMonster` Prefab，并取得三个不同的类型池。Gate、Prop、Bullet 和 VFX 遵守相同的一类型一规范 Prefab 约束。
- Manager 和具体对象的测试必须覆盖未激活借出、初始化后激活、业务清理、主动失活、防御性失活、重复归还以及跨场景复用。

## 关联文档

- `../01_Architecture/PoolSystem.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/BootstrapAndComposition.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Obstacle/README.md`
- `../02_Modules/Bullet/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-005-MonsterCombatAndManager.md`
- `ADR-014-SharedRuntimeContractBaseline.md`
- `ADR-027-MvpGlobalServiceScope.md`
