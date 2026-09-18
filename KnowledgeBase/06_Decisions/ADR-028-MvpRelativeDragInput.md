# ADR-028：MVP 触屏相对拖动输入

## 状态

Accepted

## 日期

2026-09-18

> 后续变更：ADR-036 保留本文的相对拖拽公式、Pointer 生命周期、边界和清理规则，但将首个工程切片收窄为设备触屏与 Editor 左键共用的单一拖拽输入；键盘/手柄和输入源回退延后，并补齐代码、Prefab 与同步 Tick 契约。

## 背景

ADR-027 已确认 Input 是 Gameplay 场景适配器而不是全局服务，当时将触摸输入延后。当前 MVP 进一步确认需要在指定 UI 区域内支持触屏横向滑动。该操作不是虚拟摇杆：手指停在按下点右侧时不应持续移动，只有相邻采样位置发生水平变化时才产生输入。

直接把屏幕像素差交给 `IArmyController.SetHorizontalInput` 会同时受到屏幕分辨率、Canvas 缩放和帧率影响；如果 Army 再乘 `TbArmy.MoveSpeed` 与玩法 delta，结果会在不同设备上不一致。因此需要统一触控区域、归一化公式、数据传递和清理语义。

## 决策

### 模块边界与通信

- Input 继续作为 Gameplay 场景内的 Presentation / Gameplay Adapter，不创建跨场景 `InputService`。
- （已由 ADR-036 取代输入源范围与最小接收接口）本 ADR 当时定义键盘、手柄和触屏统一汇入 Input Adapter，由它通过同步命令 `IArmyController.SetHorizontalInput(float)` 传给当前 Army；横向输入不是事实事件，不通过项目 `EventBus` 发布。
- Army 不反向查询 Input Adapter。Input Adapter 不直接修改 `ArmyRoot` Transform，不计算道路边界，也不决定 `TbArmy.MoveSpeed`。
- 触屏控件虽然挂在 Gameplay Canvas 下，但职责归属 Input 模块。UGUI Pointer 回调只负责采集本地交互，不等同于跨模块 EventBus。

### 触控区域与 Prefab 参数

- Gameplay UI Prefab 提供一个带 Raycast Target 的 `TouchDragArea`，其 `RectTransform` 定义触控开始范围和横向归一化宽度。
- 触控组件提供序列化字段 `horizontalMultiplier`，默认值为 `1`，并在对应 UI Prefab 的 Inspector 中配置；该值必须大于等于 `0`。
- Pointer 必须从 `TouchDragArea` 内开始才会被接收。一次只跟踪一个活动 `pointerId`，其余 Pointer 忽略。
- 拖动期间将用于计算的局部位置限制在区域边界内；到达边界后继续向外移动不会继续累加输入，向区域内返回时恢复产生反向输入。

### 相对拖动计算

触控组件在 `PointerDown` 时记录当前局部位置并输出 `0`。每次 `Drag` 使用当前位置与上一个有效采样位置的差，而不是与最初按下位置的差：

```text
localDeltaX = currentLocalX - previousLocalX
accumulatedDeltaX += localDeltaX
previousLocalX = currentLocalX
```

每次输入更新消费自上次更新以来累计的水平差值，并立即将累计值清零。ADR-036 后由 `TouchDragInput.ConsumeHorizontalInput` 承担该计算，GameplayInputAdapter 负责调用与提交。限制顺序固定为先限制原始归一化滑动速度，再乘 Inspector 系数：

```text
normalizedVelocity = (accumulatedDeltaX / touchAreaWidth) / unscaledDeltaTime
baseTouchInput = Clamp(normalizedVelocity, -1, 1)
horizontalInput = baseTouchInput * horizontalMultiplier
```

- `touchAreaWidth` 使用 `TouchDragArea.RectTransform.rect.width` 的有效非零宽度。
- `unscaledDeltaTime` 只用于把每帧位移换算为与帧率无关的滑动速度，不表示 Input Adapter 依赖 `TimeService`。
- 当本次输入更新没有收到新 Drag 差值时，触屏输入必须为 `0`；不得保留上一帧值。
- 原始触屏滑动速度先限制到 `[-1,1]`；乘系数后的最终触屏输入范围为 `[-horizontalMultiplier, horizontalMultiplier]`，不得再次 Clamp 到 `[-1,1]`。
- `horizontalMultiplier` 同时调整触屏灵敏度和相对于 `TbArmy.MoveSpeed` 的最终移动倍率；默认 `1` 时不额外缩放，大于 `1` 时允许触屏移动速度超过基础 `TbArmy.MoveSpeed`。

### 输入源优先级与清理

- 存在活动触摸 Pointer 时由触屏取得控制权：本次更新有拖动则使用触屏结果，没有拖动则输入 `0`。
- （已由 ADR-036 取代）本 ADR 当时定义没有活动触摸 Pointer 时使用键盘/手柄横向输入；当前工程切片没有桌面轴回退。
- `PointerUp`、触控组件或 Canvas 禁用、Gameplay 离开 `Playing`、场景卸载、应用失去焦点或活动 Pointer 失效时，必须清除 Pointer、累计差值和上一位置，并向 Army 发送一次 `0`。
- Army 只在当前会话 `Playing` 状态消费移动意图，并使用 `horizontalInput × TbArmy.MoveSpeed × 有效玩法 delta`、道路边界和阵型 AABB 计算实际位移。Army 不得把乘系数后的有限输入再次限制到 `[-1,1]`。

### 非目标

- 当前不实现按下位置偏移形成持续方向的虚拟摇杆。
- 当前不保证手指屏幕位移与 Army 世界位移一一对应；如未来需要严格跟手，应新增独立位移命令和屏幕到道路世界坐标的换算契约，不能复用方向输入语义。
- 当前不实现多指合成、手势识别、按键重绑定或跨场景设备切换服务。

本 ADR 取代 ADR-021 中“Input 只传入 `[-1,1]` 归一化方向且不影响速度倍率”的部分；`TbArmy.MoveSpeed` 仍是 Army 的基础横向速度来源，但触屏 Inspector 系数可以对最终输入倍率进行缩放。

## 影响

- ADR-027 中“触摸输入延后”的决定被本 ADR 取代；Input 仍保持 Gameplay 场景适配器，不升级为全局服务。
- Input 模块进入 `ContractReady`，补充触控区域、公式、输入源优先级、清理规则和验收标准。
- `SceneStructure.md` 在 Gameplay UI 下记录 `TouchDragArea`，但其职责归属 Input 模块。
- `SetHorizontalInput(float)` 签名保持不变；ADR-036 后由 `IHorizontalInputReceiver` 拥有该最小命令，`IArmyController` 继承它。
- `ConfigurationTables.md` 明确 `TbArmy.MoveSpeed` 是基础速度，触屏 Inspector 系数通过输入倍率缩放最终位移，不形成第二份基础速度配置。
- 集成测试增加停手即停、跨分辨率/帧率归一化、Inspector 系数、Pointer 清理和禁止 EventBus 传递连续输入的检查。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/DesignBacklog.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/SceneStructure.md`
- `../01_Architecture/SystemOverview.md`
- `../02_Modules/Input/README.md`
- `../02_Modules/UI/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../03_SharedContracts/ConfigurationTables.md`
- `../04_Assets/ArtList.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-021-MvpRuntimeDeterminismAndBindings.md`
- `ADR-027-MvpGlobalServiceScope.md`
