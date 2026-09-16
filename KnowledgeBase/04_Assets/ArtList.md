# 美术资源清单

| 资源 | 类型 | 用途 | 状态 |
|---|---|---|---|
| 道路背景 | Sprite | 竖直道路 | Planned |
| 军队 | Sprite / Animator | 屏幕底部单位 | Planned |
| 加法门 | Sprite / Prefab | 显示可为负数的门数字 | Planned |
| 元素门 | Sprite / Prefab | 显示元素、HP 和接触状态 | Planned |
| 弹弓箱 | Sprite / Prefab | 武器箱道具 | Planned |
| 弓箭箱 | Sprite / Prefab | 武器箱道具 | Planned |
| 法杖箱 | Sprite / Prefab | 武器箱道具 | Planned |
| 普通怪物 | Sprite / Animator | 基础敌人 | Planned |
| 精英怪物 | Sprite / Animator | 范围攻击敌人；Prefab 需要 `BodyCollider`、`AttackCollider`、`TargetSensor` | Planned |
| Boss 怪物 | Sprite / Animator | 首版 Boss；Prefab 需要 `BodyCollider`、`AttackCollider`、`TargetSensor` | Planned |
| 子弹 | Sprite | 自动射击 | Planned |
| 命中特效 | Particle / Prefab | 子弹反馈 | Planned |

## 绑定规则

- MVP 不在 Luban 表配置 `PrefabKey`。Prefab、Sprite、Animator、AudioClip、阵型槽位和发射点通过 Unity Inspector 或 Unity 侧资源注册表绑定。
- 敌人资源按 `EnemyType`，Gate 资源按 `GateType`，武器箱资源按 `WeaponId`，子弹资源按 `BulletId` 选择；这些映射属于 Unity 资源侧。
- 资源注册表键统一使用大小写敏感的 ASCII `类别/身份` 格式，例如 `Enemy/Normal`、`Gate/Additive`、`Prop/Weapon/{WeaponId}`、`Bullet/{BulletId}`；不使用绝对路径或 Luban 资源键。
- 进入 Gameplay 前验证本关使用的资源绑定、Collider2D 和 Layer；缺失时报告配置或资源来源，不静默创建替代对象。
