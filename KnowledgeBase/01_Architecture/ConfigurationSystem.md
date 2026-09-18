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

进入 LevelSelect 后，`ConfigService` 从 `LevelCatalog` 根据选定的 `LevelId` 提供对应的 `LevelConfig`。Gameplay 由 SceneService 接收并注入这份已校验的 `LevelConfig`；Level 和 Spawn 只消费注入的关卡配置与道路快照。ConfigService 在初始化时把 `TbArmy`、`TbWeapon` 等生成行验证并复制为不可变快照；ArmyController 只取得 `IArmyConfigProvider` 和 `IWeaponConfigProvider`。场景装配验证 `ArmyId = 1` 的序列化 Prefab 绑定和槽位数组，其他 Manager 验证各自规范 Prefab 后再向全局 PoolService 请求类型池。任何 Gameplay 模块都不得直接读取文件、访问 `StreamingAssets`、创建新的 Tables 或访问静态 `LubanTables.Instance`。

`LevelCatalog` 只保存 `LevelConfig` 引用和 `initiallyUnlocked` 标记。`LevelConfig.levelId` 是唯一 ID，目录不重复保存 ID；当前目录只有第一关且该条目默认解锁。

配置初始化和选关使用以下边界：

```text
GlobalBootstrap → ConfigService.Initialize(LevelCatalog, Tables, ResourceRegistry)
LevelSelect → SelectLevel(levelId)
ConfigService → Validate and GetLevelConfig(levelId)
SceneService → SwitchToGameplay(levelId, levelConfig, levelRunId)
LevelManager → Initialize(levelConfig, levelRunId)
```

当前工程的 `LubanTables` 是 Luban loader 适配器；它不应成为各模块的第二套配置服务。后续实现 `ConfigService` 时，应复用同一个 Tables 实例。

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
- MVP 固定 ArmyId=1；ConfigService 必须提供 `TbArmy.Id=1` 的只读快照，GameplaySceneEntry 必须提供同 ID 的唯一 Army Prefab。槽位容量由 Prefab 的序列化槽位数组长度派生。
- 当前武器、火/冰/雷剩余时间属于 Army 本局状态；`TbArmy` 不保存 WeaponId 或元素，当前也不建立 `TbElement`。
- Unity 资源注册表如使用字符串键，键只属于 Unity 资源侧，不构成 Luban 表字段。
- LevelConfig 只描述本关卡如何编排，不复制敌人、军队和门的数值。
- LevelConfig 不重复保存道路宽高和四边；这些值由唯一 `roadBounds` 派生。道路不通过 Collider 提供玩法边界。
- Luban 表只描述可复用的数据，不承担场景对象的生命周期。
- 配置加载失败必须在初始化或选定关卡加载阶段报告，不允许静默使用缺省数值继续运行或自动进入 Gameplay。

## 平台说明

当前 JSON loader 适合 Editor 和 Windows 开发。Android、WebGL 等平台不能假设可以直接用 `File.ReadAllText` 读取 `StreamingAssets`；适配这些平台时，需要先以平台支持的方式读入内存，再交给 Tables loader。

## 关联文档

- `../06_Decisions/ADR-003-ConfigurationSources.md`
- `../06_Decisions/ADR-012-LevelCatalogConfigurationBootstrap.md`
- `../06_Decisions/ADR-014-SharedRuntimeContractBaseline.md`
- `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`
- `../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `GlobalServices.md`
