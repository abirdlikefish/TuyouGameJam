# ADR-056：鸡类敌人身份命名

## 状态

Accepted

## 日期

2026-09-21

## 背景

现有三种敌人使用 `Normal`、`Elite`、`Boss` 作为领域身份，并由 `NormalMonster`、`EliteMonster`、`BossMonster` 三个具体类型和规范 Prefab 承载。当前美术设定已经明确为小鸡、母鸡和公鸡，原命名不再准确表达角色身份。

本次只统一身份命名。既有配置数值、攻击类型、移动、受击、死亡、对象池、碰撞体、Animator 状态和 AnimationEvent 规则均保持不变。

## 决定

- `EnemyType` 使用 `Chick = 0`、`Hen = 1`、`Rooster = 2`，分别表示小鸡敌人、母鸡敌人和公鸡敌人。整数值及敌人配置 ID 不变。
- 三个具体池化根类型改为 `ChickMonster`、`HenMonster`、`RoosterMonster`；规范 Prefab 改为 `PF_Monster_Chick`、`PF_Monster_Hen`、`PF_Monster_Rooster`。
- 小鸡继续派生 `SingleTarget`，母鸡和公鸡继续派生 `Area`。母鸡和公鸡继续使用既有 `AttackCollider`。
- 脚本和 Prefab 重命名必须保留原 `.meta` GUID。三个新 Monster 类型使用 `MovedFrom` 迁移旧类名，EnemyManager 的三个序列化 Prefab 字段使用 `FormerlySerializedAs` 兼容现有场景数据。
- Luban 枚举源和敌人表同步使用 `Chick`、`Hen`、`Rooster`，生成文件只由 Luban 重新生成。
- 现有动画和原画技术身份 `Normal`、`Elite`、`Boss` 保持不变：`Normal` 服务于 Chick，`Elite` 服务于 Hen，`Boss` 服务于 Rooster。Clip、Controller、OverrideController、Sprite 目录及 Sequence Animation Builder 不重命名。
- 历史 ADR 和既有变更记录保留当时使用的旧名称；当前契约、模块说明和验收文档使用新领域名称。

## 影响

- 源码层面的枚举成员、具体类型和 Prefab 名称发生破坏性重命名，但整数配置协议和 Unity 资源 GUID 保持兼容。
- 当前场景无需手工改写序列化 YAML；Unity 可通过字段迁移标记继续解析原字段名。
- 动画资产仍显示旧技术名称，相关文档必须明确映射，避免把保留名称误判为遗漏。
