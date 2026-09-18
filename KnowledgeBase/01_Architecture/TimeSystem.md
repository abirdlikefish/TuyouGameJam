# 时间系统

## 目标

MVP 为玩法模块提供统一的正常时间步进，并为 MainMenu、LevelSelect 等应用流程提供可取消的 RealTime 定时任务。暂停、减速、加速、局部时停和对象局部倍率属于后续扩展。

## 时间域

```text
RealTime   应用流程自动等待、本地系统任务
Gameplay   关卡和普通游戏逻辑
Bullet     子弹
Gate       Gate 与道路门对象
Monster    怪物
VFX        视觉特效
```

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

暂停、减速、加速、局部时停、对象局部倍率及其组合规则均未进入当前公共契约。未来启用前必须新增或更新 ADR，并同步接口、事件、碰撞语义和测试；不得依据历史草案直接补出 `PushPause` 或 `SetTimeScale`。

## 注意事项

- `WaitForSeconds` 会受 Unity 全局时间缩放影响；当前应用流程统一使用 `RealTime` 定时器。
- Collider2D 作为碰撞形状和查询依据，不改变时间权威；子弹、敌人、Gate、Prop 和 Army 的移动与碰撞结算使用各自有效 delta。
- 当前不实现暂停或局部时停下的 Collider2D 行为；未来启用时另行确认。

## 关联决策

- `../06_Decisions/ADR-001-TimeSystem.md`
- `../06_Decisions/ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`
