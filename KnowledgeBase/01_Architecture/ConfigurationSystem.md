# 配置系统

## 目标

配置系统统一说明关卡资产、Luban 数据和运行时状态的边界。配置是只读输入，运行时对象可以复制配置值并产生自己的状态，但不得反向修改配置源。

## 配置来源

| 内容 | 权威来源 | 典型消费者 |
|---|---|---|
| 可用关卡目录和首次运行默认解锁状态 | `LevelCatalog` ScriptableObject | ConfigService、GameStateService、UI |
| 关卡元数据、固定道路、出生横线、归一化横向出生位置、生成时间轴和出现顺序 | `LevelConfig` ScriptableObject | Level、Spawn |
| 角色/军队基础属性 | Luban `TbArmy` | Army |
| 武器发射属性 | Luban `TbWeapon` | Army、Bullet |
| 元素身份和类型 | Luban `TbElement` | Army、Bullet |
| 敌人基础属性 | Luban `TbEnemy` | Monster、Spawn |
| Gate 属性、数字变化和接触参数 | Luban `TbGate` | Gate、Spawn、ObstacleManager |
| Prop 属性、生命值和武器奖励 | Luban `TbProp` | Prop、Spawn、ObstacleManager |
| 子弹属性 | Luban `TbBullet` | Bullet、Army |
| Prefab、Sprite、AudioClip | Unity 资源注册表/Inspector | 表现和生成系统 |
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

当前 Luban 数据只有 `demo.item` 示例表，MVP 玩法业务表尚未建立。建立业务表时只采用 `../03_SharedContracts/ConfigurationTables.md` 和 ADR-020 确认的初始字段；没有实际消费者的全局参数不提前建立 `TbGameSettings`。

## 运行时边界

`ConfigService` 在初始化阶段加载：

1. Inspector 绑定的 `LevelCatalog` ScriptableObject。
2. 一个 `cfg.Tables` 实例，内部由 Luban JSON loader 读取生成数据。
3. Inspector 提供的 Unity 资源绑定或资源注册表；绑定关系由 Unity 侧持有，不从 Luban `PrefabKey` 读取。

资源注册表键使用大小写敏感的 ASCII `类别/身份` 格式，例如 `Enemy/Normal`、`Gate/Additive`、`Prop/Weapon/{WeaponId}`、`Bullet/{BulletId}`。这些键属于 Unity 资源侧，不写入 Luban，也不使用绝对路径。

进入 LevelSelect 后，`ConfigService` 从 `LevelCatalog` 根据选定的 `LevelId` 提供对应的 `LevelConfig`。Gameplay 由 SceneService 接收并注入这份已校验的 `LevelConfig`；Level、Spawn 等模块消费注入的关卡配置，并通过 `ConfigService` 查询 Luban 可复用数值，不直接读取文件、访问 `StreamingAssets` 或创建新的 Tables。

`LevelCatalog` 只保存 `LevelConfig` 引用和 `initiallyUnlocked` 标记。`LevelConfig.levelId` 是唯一 ID，目录不重复保存 ID；当前目录只有第一关且该条目默认解锁。

配置初始化和选关使用以下边界：

```text
GlobalBootstrap → ConfigService.Initialize(LevelCatalog, Tables, ResourceRegistry)
LevelSelect → SelectLevel(levelId)
ConfigService → Validate and GetLevelConfig(levelId)
SceneService → LoadGameplay(levelId, levelConfig, levelRunId)
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
2. 打开对应 `LevelConfig` 资产，修改道路尺寸、`spawnY`、各生成项的 `spawnPosition`、生成时间轴或配置 ID。
3. 确认 `levelId` 唯一，引用的 `TbEnemy`、`TbGate`、`TbProp` 存在，且本关需要的 Unity 资源绑定完整。
4. 运行目录加载、选定关卡注入、时间轴顺序和终局重开测试。

## 关键约束

- Luban ID 是跨配置引用的稳定键，不能使用会随排序变化的行号。
- MVP Luban 表不保存 `PrefabKey`；Prefab、Sprite、Animator、AudioClip、阵型槽位和发射点由 Unity Inspector 或 Unity 资源注册表绑定。
- Unity 资源注册表如使用字符串键，键只属于 Unity 资源侧，不构成 Luban 表字段。
- LevelConfig 只描述本关卡如何编排，不复制敌人、军队和门的数值。
- Luban 表只描述可复用的数据，不承担场景对象的生命周期。
- 配置加载失败必须在初始化或选定关卡加载阶段报告，不允许静默使用缺省数值继续运行或自动进入 Gameplay。

## 平台说明

当前 JSON loader 适合 Editor 和 Windows 开发。Android、WebGL 等平台不能假设可以直接用 `File.ReadAllText` 读取 `StreamingAssets`；适配这些平台时，需要先以平台支持的方式读入内存，再交给 Tables loader。

## 关联文档

- `../06_Decisions/ADR-003-ConfigurationSources.md`
- `../06_Decisions/ADR-012-LevelCatalogConfigurationBootstrap.md`
- `../06_Decisions/ADR-014-SharedRuntimeContractBaseline.md`
- `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `GlobalServices.md`
