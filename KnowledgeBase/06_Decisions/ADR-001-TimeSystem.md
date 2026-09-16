# ADR-001：采用自定义时间域

## 状态

Accepted

## 背景

游戏需要全局暂停、局部时停和不同物体的时间倍率。仅使用 `Time.timeScale` 无法让 UI 和部分对象在暂停期间继续运行。

## 决策

采用 `TimeService`，以 `unscaledDeltaTime` 为基础，按时间域和对象局部倍率计算最终 deltaTime。暂停使用令牌管理。

MVP 范围收窄为：所有时间域倍率和对象局部倍率固定为 `1`，不启用运行时倍率调整；暂停、减速、加速和局部时停保留为后续扩展能力。流程自动跳过仍使用 `RealTime`。

## 影响

- 游戏对象不能直接依赖 `Time.deltaTime` 处理可暂停逻辑。
- 需要为时间域和定时器编写基础测试。
- 选择性时停优先用于自定义移动和计时逻辑。

MVP 的时间范围和后续扩展边界见 `ADR-021-MvpRuntimeDeterminismAndBindings.md`。
