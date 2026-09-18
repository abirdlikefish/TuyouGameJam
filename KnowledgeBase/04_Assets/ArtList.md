# 美术资源清单

| 资源 | 类型 | 用途 | 状态 |
|---|---|---|---|
| 道路背景 | Sprite | 竖直道路 | Planned |
| 军队 | Sprite / Animator | 屏幕底部单位 | Planned |
| 加法门 | Sprite / Prefab | 显示可为负数的门数字 | Planned |
| 元素门 | Sprite / Prefab | 显示元素、HP 和接触状态 | Planned |
| 弹弓箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 弓箭箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 法杖箱 | Sprite | 共用 `WeaponProp` 规范 Prefab 的武器箱表现 | Planned |
| 普通怪物 | Sprite / Animator / Prefab | `NormalMonster` 规范 Prefab；基础敌人 | Planned |
| 精英怪物 | Sprite / Animator / Prefab | `EliteMonster` 规范 Prefab；需要 `BodyCollider`、`AttackCollider`、`TargetSensor` | Planned |
| Boss 怪物 | Sprite / Animator / Prefab | `BossMonster` 规范 Prefab；需要 `BodyCollider`、`AttackCollider`、`TargetSensor` | Planned |
| 武器箱规范 Prefab | Prefab | 唯一 `WeaponProp` 根类型，按 `WeaponId` 绑定表现 | Planned |
| 子弹 | Sprite / Prefab | 唯一 `Bullet` 规范 Prefab；按 `BulletId` 绑定表现 | Planned |
| 命中特效 | Particle / Prefab | 子弹反馈 | Planned |
| 触屏拖动区域 | UI Prefab / Graphic | 定义 Gameplay 触控范围并承载 `TouchDragInput`；Graphic 可透明但需启用 Raycast Target | Planned |

## 绑定规则

- MVP 不在 Luban 表配置 `PrefabKey`。池化规范 Prefab 由对应 Manager 的 Inspector 引用绑定；Sprite、Animator、阵型槽位和发射点通过 Unity Inspector 或 Unity 侧资源注册表绑定。MVP 完全无声音，不要求 AudioClip。
- `NormalMonster`、`EliteMonster`、`BossMonster`、`AdditiveGate`、`ElementGate`、`WeaponProp` 和 `Bullet` 各自只对应一个规范 Prefab；同一具体根类型不能绑定第二个 Prefab。
- 敌人按 `EnemyType` 选择三个具体类型池；当前三种武器箱共用 `WeaponProp` 类型池并按 `WeaponId` 选择表现，MVP 子弹共用 `Bullet` 类型池并按 `BulletId` 选择数值与表现。
- 资源注册表键仍使用大小写敏感的 ASCII `类别/身份` 格式且不使用绝对路径或 Luban 资源键，但不作为对象池身份或 PoolService 的 Prefab 选择入口。
- 进入 Gameplay 前验证本关使用的资源绑定、Collider2D 和 Layer；缺失时报告配置或资源来源，不静默创建替代对象。
- 触屏拖动区域 Prefab 的 `RectTransform` 定义输入归一化宽度；`TouchDragInput.horizontalMultiplier` 通过 Inspector 配置且默认值为 `1`。该 Prefab 不直接保存对场景 Army 的资源引用，由场景装配或 Prefab 内 Input Adapter 完成运行时绑定。
