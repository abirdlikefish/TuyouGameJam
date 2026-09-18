# ADR-003：区分关卡资产配置与 Luban 数值配置

## 状态

Accepted（资源键范围由 ADR-020 收窄；生成位置由 ADR-023 修订）

## 日期

2026-09-13

## 背景

项目需要配置关卡编排、角色/军队属性、敌人属性、Gate、Prop 和子弹参数。关卡编排与 Unity 场景、生成点和 Prefab 引用关系紧密，而单位数值需要频繁调整、校验和复用。全部使用一种配置形式会导致资源引用不便或数值来源分散。

## 决策

- `LevelConfig` 使用 Unity `ScriptableObject`，包含关卡元数据、固定道路布局、固定出生横线、按时间轴的敌人/Gate/Prop 生成项、归一化横向出生位置和配置 ID。
- 角色/军队、敌人、Gate、Prop、子弹及全局平衡参数使用 Luban 表。
- Weapon 和 Element 作为军队/子弹共享的 Luban 表，MVP 分别保存基础发射规则和元素身份。
- Luban 表保存稳定玩法数值和跨表 ID，不直接保存 `UnityEngine.Object` 引用；MVP 也不保存 `PrefabKey` 等 Unity 资源键。
- Prefab、Sprite、Animator、AudioClip 等 Unity 对象由 Unity Inspector 或 Unity 资源注册表绑定。
- 当前 MVP 将可控角色的基础属性归入 `TbArmy`；运行中的人数、当前生命值、生成游标、关卡计时和胜负状态不属于配置表或 `LevelConfig`。
- `ConfigService` 负责提供 `LevelCatalog`、选定的 `LevelConfig` 和唯一的 Luban `cfg.Tables` 实例；业务模块不自行加载文件或创建第二个 Tables 实例。

## 不采用

- 不在 Luban 和 ScriptableObject 中重复保存同一个数值。
- 不把当前运行状态写回配置资产或生成的 JSON。
- 不把关卡生成编排拆成 Luban 表；道路和三类生成时间轴由 `LevelConfig` 统一维护。

## 影响

- 项目同时存在 LevelCatalog/LevelConfig Unity 资产加载和 Luban 数据加载两条配置路径，必须在初始化阶段统一完成共享配置加载，并在选关后解析具体 LevelConfig。
- 修改 Luban 数据后需要重新运行生成脚本并通过校验；修改关卡编排则需要修改对应 `LevelConfig` 资产。
- 需要对 Luban ID 引用、LevelConfig 中的配置 ID 和 Unity 资源绑定增加集成测试。
- 当前 `LubanTables` 可作为 Luban 加载适配器，后续由 `ConfigService` 统一封装。

## 关联文档

- `../01_Architecture/ConfigurationSystem.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../00_Project/DesignBacklog.md`
- `ADR-012-LevelCatalogConfigurationBootstrap.md`
- `ADR-020-MinimalMvpConfigurationSurface.md`
- `ADR-023-NormalizedSpawnPosition.md`
