# 配置系统

## 目标

配置系统统一说明关卡资产、Luban 数据和运行时状态的边界。配置是只读输入，运行时对象可以复制配置值并产生自己的状态，但不得反向修改配置源。

## 配置来源

| 内容 | 权威来源 | 典型消费者 |
|---|---|---|
| 可用关卡目录和首次运行默认解锁状态 | `LevelCatalog` ScriptableObject | ConfigService、GameStateService、UI |
| 关卡元数据、唯一 `roadBounds`、出生/接近/离场横线、归一化横向出生位置、生成时间轴和出现顺序 | `LevelConfig` ScriptableObject | Level、Spawn |
| 角色/军队基础属性 | Luban `TbArmy` | Army |
| 武器发射属性 | Luban `TbWeapon` | Army、Bullet |
| 元素类型、元素门 HP 与持续时间换算 | `LevelConfig.gateSpawns`、`elementDurationSecondsPerDamage` | Gate、Army、Bullet；当前不建立 `TbElement` 或 `TbGate` |
| 敌人基础属性 | Luban `TbEnemy` | Monster、Spawn |
| Gate 类型、初始数字、元素类型、每门 MaxHp | `LevelConfig.gateSpawns` | Gate、Spawn、ObstacleManager |
| Gate 类型级统一速度与元素门接触伤害 | AdditiveGate / ElementGate 规范 Prefab Inspector | Gate、ObstacleManager |
| Prop 属性、生命值和武器奖励 | Luban `TbProp` | Prop、Spawn、ObstacleManager |
| 子弹属性 | Luban `TbBullet` | Bullet、Army |
| 池化规范 Prefab | 对应 Manager 的 Inspector 引用 | EnemyManager、ObstacleManager、BulletManager、PoolService 类型池 |
| `ArmyId -> Army Prefab` | GameplaySceneEntry 的序列化 `ArmyPrefabBinding[]` | Composition、Army；Army 不进入对象池 |
| Sprite、Animator 等表现资源 | Unity 资源注册表/Inspector | 表现和生成系统 |
| 当前人数、生命值、生成游标、关卡计时和胜负状态 | 运行时对象或服务 | Gameplay、UI |

## Luban 工程

```text
LubanData/
├── luban.conf
├── Data/
│   ├── __tables__.xlsx
│   ├── __beans__.xlsx
│   ├── __enums__.xlsx
│   └── 业务数据表.xlsx
└── Defines/
```

生成入口是 `LubanData/gen_client_json.bat`，使用根目录的 `Luban/Luban.dll`。当前开发目标为：

```text
-t client
-c cs-simple-json
-d json
```

输出目录为：

```text
Assets/Generated/Luban
Assets/StreamingAssets/Luban
```

`Assets/Generated/Luban` 中的 `.cs` 和 `Assets/StreamingAssets/Luban` 中的数据文件都是生成物，不直接编辑。

当前 Luban 数据只有 `demo.item` 示例表，MVP 玩法业务表尚未建立。建立业务表时只采用 `../03_SharedContracts/ConfigurationTables.md`、ADR-020、ADR-035 和 ADR-038 确认的初始字段；当前不建立 `TbElement` 或 `TbGate`，没有实际消费者的全局参数不提前建立 `TbGameSettings`。

## 运行时边界

`ConfigService` 在初始化阶段加载：

1. Inspector 绑定的 `LevelCatalog` ScriptableObject。
2. 一个 `cfg.Tables` 实例，内部由 Luban JSON loader 读取生成数据。
3. Inspector 提供的 Unity 资源绑定或资源注册表；绑定关系由 Unity 侧持有，不从 Luban `PrefabKey` 读取。

资源注册表键使用大小写敏感的 ASCII `类别/身份` 格式，只用于需要运行时选择的非池身份资源。这些键属于 Unity 资源侧，不写入 Luban，也不使用绝对路径。池化规范 Prefab 不通过资源键交给 PoolService：对应 Manager 通过 Inspector 持有具体根组件 Prefab，并以具体类型取得类型池。

启动阶段由 Composition 直接调用具体 `ConfigService.Initialize(LevelCatalog, Tables, ResourceRegistry)`；该初始化入口不放入公共 `IConfigService`。ConfigService 完整校验后，把每个 `LevelConfig` 资产防御性复制为不可变 `LevelConfigSnapshot`。进入 LevelSelect 后，应用层按 `LevelId` 查询对应快照；SceneService、GameplaySceneEntry、Level 和 Spawn 只传递或消费快照，不持有 `ScriptableObject` 资产。

ConfigService 在启动初始化中完成唯一一次配置转换：校验目录内全部 LevelConfig、`TbArmy`、`TbWeapon`、`TbBullet`、`TbEnemy`、`TbProp` 的主键、枚举、数值和当前跨表引用，然后分别复制为 `LevelConfigSnapshot`、`ArmyConfigSnapshot`、`WeaponConfigSnapshot`、`BulletConfigSnapshot`、`EnemyConfigSnapshot`、`PropConfigSnapshot` 的只读字典。Composition 只向消费者注入最小查询面：ArmyController 取得 Army/Weapon Provider，BulletManager 取得 Bullet Provider，EnemyManager 取得 Enemy Provider，ObstacleManager 取得 Prop Provider。场景装配验证 `ArmyId = 0` 的序列化 Prefab 绑定和槽位数组，各 Manager 验证规范 Prefab 后再向全局 PoolService 请求类型池。任何 Gameplay 模块都不得直接读取文件、访问 `StreamingAssets`、创建新的 Tables、保存 Luban 生成行或访问静态 `LubanTables.Instance`。

`LevelConfig.unlockedLevelIds` 在复制快照前执行专用校验。空列表合法；ConfigService 先在原始列表上检查重复 ID 和当前 `levelId` 自引用，任一命中都以 `ConfigErrorCode.InvalidLevelConfig` 致命退出。之后按原始顺序检查目录存在性：当前 LevelCatalog 中不存在的未来关卡 ID 使用 `Debug.LogWarning` 输出稳定 Source、条目索引和缺失 ID，并从 `LevelConfigSnapshot.UnlockedLevelIds` 中过滤；该警告不改变 Ready 状态。GameStateService 和其他运行时消费者只读取过滤后的不可变快照。

类型化 Provider 使用必得查询而不是 `TryGet + 默认值`。初始化成功已经保证所有关卡和跨表引用可解析；非 Ready 查询或未知 ID 属于程序不变量被破坏，直接抛出异常，不由 Gameplay Manager 捕获并恢复。

`LevelCatalog` 只保存 `LevelConfig` 引用和 `initiallyUnlocked` 标记。`LevelConfig.levelId` 是唯一 ID，目录不重复保存 ID；当前目录只有第一关且该条目默认解锁。

配置初始化和选关使用以下边界：

```text
GlobalBootstrap → ConfigService.Initialize(LevelCatalog, Tables, ResourceRegistry)
LevelSelect → SelectLevel(levelId)
ConfigService → Validate assets/tables and build snapshots
IConfigService → TryGetLevelConfig(levelId, out levelConfigSnapshot)
SceneService → SwitchToGameplay(levelId, levelConfigSnapshot, levelRunId)
LevelManager → Initialize(levelConfigSnapshot, levelRunId)
```

当前工程的 `LubanTables` 是 Luban loader 适配器；它不应成为各模块的第二套配置服务。后续实现 `ConfigService` 时，应复用同一个 Tables 实例。

创建 asmdef 时，`Assets/Generated/Luban` 下的 `cfg.Tables` 与生成行统一编入只读支撑程序集 `Game.ConfigGenerated`，由 Foundation 的具体 ConfigService 和 Composition 启动装配引用。生成类型不得进入 `IConfigService`、快照、Scene 或 Gameplay 接口。若生成命令会清空程序集定义所在目录，应修改生成源/生成脚本保留或重建该 asmdef，而不是直接编辑生成 `.cs`。

## 新增或修改配置的流程

### 修改 Luban 数值

1. 在对应数据表修改数据。
2. 检查主键、类型、范围和引用。
3. 运行 `LubanData/gen_client_json.bat`。
4. 确认生成代码和 JSON 没有校验错误。
5. 运行 Unity 编译和配置加载测试。

### 修改关卡编排

1. 打开 `LevelCatalog` 确认关卡条目和默认解锁标记。
2. 打开对应 `LevelConfig` 资产，修改唯一 `roadBounds`、三条 Y 线、各生成项的 `spawnPosition`、生成时间轴、Enemy/Prop 配置 ID、Gate 内联字段或元素持续时间换算系数。
3. 确认 `levelId` 唯一，引用的 `TbEnemy`、`TbProp` 存在，Gate 条件字段通过校验，且本关需要的 Unity 资源绑定完整。
4. 运行目录加载、选定关卡注入、时间轴顺序和终局重开测试。

## 关键约束

- Luban ID 是跨配置引用的稳定键，不能使用会随排序变化的行号。
- MVP Luban 表不保存 `PrefabKey`；池化规范 Prefab 由对应 Manager 的 Inspector 引用绑定，Sprite、Animator、阵型槽位和发射点由 Unity Inspector 或 Unity 资源注册表绑定。MVP 完全无声音，不要求 AudioClip 绑定。
- MVP 固定 ArmyId=0；ConfigService 必须提供首行 `TbArmy.Id=0` 的只读快照，GameplaySceneEntry 必须提供同 ID 的唯一 Army Prefab。槽位容量由 Prefab 的序列化槽位数组长度派生。
- 当前武器、火/冰/雷剩余时间属于 Army 本局状态；`TbArmy` 不保存 WeaponId 或元素，当前也不建立 `TbElement`。
- Unity 资源注册表如使用字符串键，键只属于 Unity 资源侧，不构成 Luban 表字段。
- LevelConfig 只描述本关卡如何编排，不复制敌人、军队和门的数值。
- LevelConfig、LevelCatalog 和生成条目资产结构属于 Foundation 配置实现；Contracts 与 Gameplay 只接收不可变 LevelConfigSnapshot，不暴露 ScriptableObject 或 Luban 生成类型。
- LevelConfig 不重复保存道路宽高和四边；这些值由唯一 `roadBounds` 派生。道路不通过 Collider 提供玩法边界。
- Luban 表只描述可复用的数据，不承担场景对象的生命周期。
- 配置错误采用 ADR-041 的单点 Fail-Fast：ConfigService 在启动初始化中遇到第一个 Luban 表、LevelCatalog、LevelConfig 或必需引用错误时，通过 `Debug.LogError` 报告 `ConfigErrorCode`、稳定来源、字段或条目索引和原因，将状态置为 `Failed`，随后在 Player 退出应用、在 Editor 停止 Play Mode。唯一例外是 ADR-044 的 `unlockedLevelIds` 目录缺失 ID，它使用 `Debug.LogWarning` 并过滤，不视为配置失败。
- 配置失败不发布项目事件、不聚合第二套错误结果、不重试；GlobalBootstrap 不调用 `NotifyInitializationReady`，Gameplay Manager 不重复记录或恢复同一错误。
- Prefab、Collider、Layer 和 Inspector 绑定继续由场景装配校验并阻止 Ready，不与配置表致命校验混为一套恢复框架。

## 平台说明

当前 JSON loader 适合 Editor 和 Windows 开发。Android、WebGL 等平台不能假设可以直接用 `File.ReadAllText` 读取 `StreamingAssets`；适配这些平台时，需要先以平台支持的方式读入内存，再交给 Tables loader。

## 关联文档

- `../06_Decisions/ADR-003-ConfigurationSources.md`
- `../06_Decisions/ADR-012-LevelCatalogConfigurationBootstrap.md`
- `../06_Decisions/ADR-014-SharedRuntimeContractBaseline.md`
- `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`
- `../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`
- `../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`
- `../06_Decisions/ADR-042-LevelConfigSnapshotAssemblyBoundary.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `GlobalServices.md`
