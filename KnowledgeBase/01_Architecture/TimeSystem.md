# 时间系统

## 目标

支持全局暂停、局部时停、减速、加速、不同物体不同时间倍率，以及基于时间域的定时任务。

## 时间域

```text
RealTime   UI、暂停菜单、本地系统任务
Gameplay   关卡和普通游戏逻辑
Bullet     子弹
Gate       Gate 与道路门对象
Monster    怪物
VFX        视觉特效
```

## 计算公式

```text
最终 deltaTime = unscaledDeltaTime × 时间域倍率 × 对象局部倍率
```

对象需要通过 `TimeService.GetDeltaTime(domain)` 获取时间，不直接依赖 `Time.deltaTime`。

## 公共接口

完整的跨模块接口定义见 `../03_SharedContracts/PublicInterfaces.md` 的 `ITimeService`。本文件只说明时间域、计算和暂停规则。

```csharp
float GetDeltaTime(TimeDomain domain);
float GetTimeScale(TimeDomain domain);
void SetTimeScale(TimeDomain domain, float scale);
PauseToken PushPause(TimeDomain domain, string reason);
TimerHandle Schedule(float seconds, Action callback, TimeDomain domain);
```

## MVP 范围

MVP 中所有时间域倍率和对象局部倍率固定为 `1`，不调用运行时倍率调整；暂停、减速、加速和局部时停属于后续扩展。`RealTime` 仍用于 MainMenu、LevelSelect 等流程定时器，Gameplay 使用正常未缩放步进。

## 暂停规则

使用令牌而不是简单布尔值。多个系统同时申请暂停时，只有全部令牌释放后才恢复时间。

```text
暂停菜单申请 → Gameplay 暂停
时停技能申请 → Monster、Gate、Bullet 暂停
UI 保持 RealTime → 暂停按钮仍可操作
```

## 注意事项

- `WaitForSeconds` 会受 Unity 全局时间缩放影响。
- UI 任务使用 `WaitForSecondsRealtime` 或 `RealTime` 定时器。
- 选择性时停优先使用自定义移动和计时；不要对大量 Rigidbody2D 做隐式时间修正。
- Collider2D 作为碰撞形状和查询依据，不改变时间权威；子弹、敌人、Gate、Prop 和 Army 的移动与碰撞结算使用各自有效 delta。
- 局部时间系数为 0 时，对象停止移动、攻击计时和动画，但 Collider2D 默认仍可被其他活动对象查询；暂停不等同于无敌或无碰撞。
