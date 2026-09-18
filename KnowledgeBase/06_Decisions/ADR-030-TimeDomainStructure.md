# ADR-030：时间域结构、归属与未来组合方向

## 状态

Accepted

> ADR-033 已将 Gameplay 核心阶段的 delta 读取集中到 LevelManager；时间域归属不变，但具体 Manager 和池对象消费由 LevelManager 传入的对应域 delta。

## 日期

2026-09-18

## 背景

当前 `TimeDomain` 同时包含 `RealTime`、总括性的 `Gameplay`，以及 `Bullet`、`Gate`、`Monster`、`VFX` 等子系统名称。MVP 中所有域都直接返回 `unscaledDeltaTime`，因此这些枚举值目前只表达调用意图；但如果把它们直接理解为互相独立的平级时钟，未来实现全局 Gameplay 暂停、单类对象减速或局部时停时，会出现以下歧义：

- `Gameplay` 与 Bullet、Monster 等玩法子系统是否存在包含关系。
- Gate 和 Prop 是否属于同一时间策略，`Gate` 名称是否足以代表全部道路对象。
- Gameplay 世界特效和 UI 特效是否应使用同一个 `VFX` 时间域。
- 一个对象能否同时加入多个时间域，以及多个倍率应当相乘、覆盖还是按优先级处理。

MVP 尚不实现暂停、倍率修改和局部时停，因此需要先明确当前归属规则和未来演进方向，同时避免提前实现没有当前行为的层级系统。

## 决策

### MVP 保持扁平公共契约

- 当前保留 `RealTime`、`Gameplay`、`Bullet`、`Gate`、`Monster`、`VFX` 枚举，不增加父域 API、运行时域图、倍率修改接口或暂停接口。
- 所有域当前都返回正常的 `unscaledDeltaTime`；枚举值只表达消费者希望遵循的时间策略。
- 每次移动、计时或调度只选择一个最具体的时间域，不同时累计多个平级域的 delta，也不把时间域当作 GameObject 分类、程序集归属或事件标签。
- `Schedule` 当前只要求支持 MainMenu 和 LevelSelect 使用的 `RealTime` 流程定时器；其他域的定时器需求在出现实际消费者后再补充验收。

当前归属如下：

| 消费者或逻辑 | MVP 时间域 | 说明 |
|---|---|---|
| MainMenu、LevelSelect 和不依赖 Gameplay 推进的应用流程等待 | `RealTime` | 使用未缩放时间，独立于 Gameplay 语义 |
| Gameplay 相对拖拽归一化 | `RealTime` | LevelManager 读取后传给 Input；Input 不注入 TimeService，也不直接读取 Unity `Time` |
| Army 移动、LevelManager 本局计时和未单独分类的玩法逻辑 | `Gameplay` | LevelManager 读取并传给 Army；SpawnManager 只消费累计的 `elapsedTime` |
| 子弹移动、寿命和命中查询 | `Bullet` | LevelManager 读取并传给 BulletManager |
| 怪物移动、攻击和计时 | `Monster` | LevelManager 读取并传给 EnemyManager |
| Gate、Prop 和其他道路对象的移动与接触流程 | `Gate` | LevelManager 读取并传给 ObstacleManager；未来启用独立倍率时评估改名为 `RoadObject` |
| 跟随 Gameplay 世界推进的视觉特效 | `VFX` | 不包含 UI 动画和应用流程表现；后者使用 `RealTime` 或未来单独定案 |

### 未来只采用单父级层次，不采用任意重叠

暂停、减速、加速或局部时停进入实际范围时，必须新增或更新 ADR，并优先按以下概念结构演进：

```text
未缩放时间源
├── RealTime
└── Gameplay
    ├── Army
    ├── Bullet
    ├── Monster
    ├── RoadObject
    └── GameplayVFX
```

- 每个消费者仍只选择一个最具体的域；子域自动继承唯一父域的影响。
- 全局 Gameplay 暂停或减速作用于 `Gameplay` 父域，并自然影响全部玩法子域；单类效果只作用于对应子域。
- 结构倍率采用从时间源到目标域逐级相乘的方向，例如 `unscaledDeltaTime × GameplayScale × BulletScale`。具体修改 API、令牌、并发修饰器和恢复顺序仍需在启用该能力时定案。
- 单个对象的冰冻、命中特写或临时加速属于对象局部修饰，不通过同时加入多个时间域表达；局部修饰的叠加规则同样延后定案。
- Gameplay 世界 VFX 可以作为 `Gameplay` 子域；UI 动画或应用流程表现不得因为同为“特效”而隐式继承 Gameplay 暂停。

## 不采用

- **当前直接实现嵌套时间域**：MVP 没有任何非 `1` 倍率或暂停行为，提前实现只增加不可验证的复杂度。
- **一个消费者同时属于多个平级时间域**：会使倍率组合、暂停恢复和定时器累计规则产生歧义。
- **继续把全部域视为永久平级时钟**：无法自然表达“暂停全部 Gameplay，但 RealTime 继续”的全局规则。
- **立即将 `Gate` 重命名为 `RoadObject`**：当前公共接口和模块文档已使用 `Gate`，且 MVP 没有行为差异；在真正启用独立倍率时再通过迁移决策统一命名。

## 影响

- `TimeSystem.md` 和 `PublicInterfaces.md` 记录唯一归属与当前消费者映射。
- MVP 实现仍可使用简单扁平枚举和固定 `unscaledDeltaTime`，不需要树结构或倍率存储。
- 测试应验证消费者选择约定域、所有域返回相同正常步进，以及不存在多个域 delta 的重复累计。
- 未来时间控制设计拥有明确的单父级演进方向，但暂停、碰撞、Animator、粒子、物理和对象局部修饰仍不属于当前契约。

## 关联文档

- `../01_Architecture/TimeSystem.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-001-TimeSystem.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `ADR-036-DragOnlyInputImplementationSlice.md`
- `ADR-027-MvpGlobalServiceScope.md`
