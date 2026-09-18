# 时间系统

## 目标

MVP 为玩法模块提供统一的正常时间步进，并为 MainMenu、LevelSelect 等应用流程提供可取消的 RealTime 定时任务。暂停、减速、加速、局部时停和对象局部倍率属于后续扩展。

## 时间域

```text
RealTime   应用流程自动等待、本地系统任务
Gameplay   关卡和普通游戏逻辑
Bullet     子弹
Gate       Gate、Prop 与其他道路对象
Monster    怪物
VFX        视觉特效
```

时间域是“本次移动、计时或调度应遵循哪套时间策略”的调用标签，不是 GameObject 分类、程序集归属、独立 Update 循环或事件标签。每个消费者在一次操作中只选择一个最具体的域，不同时累计多个域的 delta。

当前消费者映射如下：

| 消费者或逻辑 | MVP 时间域 | 边界 |
|---|---|---|
| MainMenu、LevelSelect 和不依赖 Gameplay 推进的流程等待 | `RealTime` | UI 或应用流程计时不归入 Gameplay |
| Army 移动、LevelManager 本局计时及未细分的玩法逻辑 | `Gameplay` | Gameplay 默认域；SpawnManager 只消费 LevelManager 基于该域累计的 `elapsedTime` |
| 子弹移动、寿命和命中查询 | `Bullet` | 不再额外叠加 `Gameplay` delta |
| 怪物移动、攻击和计时 | `Monster` | 不再额外叠加 `Gameplay` delta |
| Gate、Prop 和其他道路对象的移动与接触流程 | `Gate` | MVP 沿用现有名称；独立倍率启用前再评估改名为 `RoadObject` |
| 跟随 Gameplay 世界推进的视觉特效 | `VFX` | 不包含 UI 动画和应用流程表现 |

当前公共契约是扁平枚举，不实现父子关系或重叠归属。`Gameplay` 表示默认玩法域，同时也是未来可能的概念父域；这一未来关系目前不产生额外运行时行为。

## 计算公式

```text
MVP 最终 deltaTime = unscaledDeltaTime
```

MVP 保留时间域参数作为调用意图，但所有域倍率固定为 `1`。玩法对象通过 `TimeService.GetDeltaTime(domain)` 获取时间，不直接依赖 `Time.deltaTime`，以便未来扩展时不改动各模块的时间来源。

## 公共接口

完整的跨模块接口定义见 `../03_SharedContracts/PublicInterfaces.md` 的 `ITimeService`。本文件只说明 MVP 时间来源、固定倍率和定时器规则。

```csharp
float GetDeltaTime(TimeDomain domain);
TimerHandle Schedule(float seconds, Action callback, TimeDomain domain);
```

## MVP 范围

MVP 中所有时间域倍率固定为 `1`，不提供运行时倍率查询或修改接口，也不提供暂停令牌。`RealTime` 用于 MainMenu、LevelSelect 等流程定时器，Gameplay 使用正常未缩放步进。`TimerHandle.Cancel()` 必须幂等，已取消或已完成的任务不得再次调用回调。

## 后续扩展

暂停、减速、加速、局部时停、对象局部倍率及其完整组合规则均未进入当前公共契约。未来启用前必须新增或更新 ADR，并同步接口、事件、碰撞语义和测试；不得依据历史草案直接补出 `PushPause` 或 `SetTimeScale`。

未来优先采用单父级层次，而不是让对象同时属于多个平级域：

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

子域自动继承唯一父域的影响，结构倍率按父级到子级逐级相乘；例如未来的子弹有效时间可以表示为 `unscaledDeltaTime × GameplayScale × BulletScale`。单个对象的冰冻或临时加速属于对象局部修饰，不通过多重时间域归属表达。具体枚举迁移、修改接口、令牌和局部修饰叠加规则仍需在能力启用时定案。

## 注意事项

- `WaitForSeconds` 会受 Unity 全局时间缩放影响；当前应用流程统一使用 `RealTime` 定时器。
- Collider2D 作为碰撞形状和查询依据，不改变时间权威；子弹、敌人、Gate、Prop 和 Army 的移动与碰撞结算使用各自有效 delta。
- 当前不实现暂停或局部时停下的 Collider2D 行为；未来启用时另行确认。

## 关联决策

- `../06_Decisions/ADR-001-TimeSystem.md`
- `../06_Decisions/ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
- `../06_Decisions/ADR-030-TimeDomainStructure.md`
