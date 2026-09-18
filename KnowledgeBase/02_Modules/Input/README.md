# Input 输入模块

## 模块信息

- ID：`MOD-INPUT`
- 层级：Presentation / Gameplay Adapter
- 状态：`ContractReady`
- 生命周期：Gameplay 场景内；不跨场景保留
- 依赖：Army、UGUI EventSystem；由 Gameplay 场景装配控制启停
- 决策：`../../06_Decisions/ADR-027-MvpGlobalServiceScope.md`、`../../06_Decisions/ADR-028-MvpRelativeDragInput.md`

## 职责

- 读取桌面端键盘/手柄横向值，以及 Gameplay UI 指定区域内的触屏相对拖动。
- 将键盘/手柄解析为 `[-1,1]` 的横向方向与强度；触屏先把原始滑动速度限制到 `[-1,1]`，再乘 Inspector 灵敏度系数。
- 将输入作为 Army 的移动命令，不直接修改 ArmyRoot 的 Transform。
- 只传递横向输入倍率；Army 使用 `TbArmy.MoveSpeed` 作为基础速度。键盘/手柄倍率位于 `[-1,1]`，触屏倍率按本模块公式允许被 Inspector 系数放大。
- 只在 `LevelManager.Playing` 阶段启用；Preparing、Completed、失去焦点和场景卸载期间清空输入并停止移动命令。

Input Adapter 由 Gameplay 场景装配入口取得 `IArmyController` 并显式初始化，不通过 `InputService.Instance`、运行时 `Find` 或全局 Service Locator 获取 Army 或服务。当前没有跨场景输入消费者，因此不创建全局 `InputService`。

横向输入是必须由当前 Army 接收的连续命令，不是已经发生的事实。Input Adapter 直接调用 `IArmyController.SetHorizontalInput(float)`，不通过项目 `EventBus` 广播输入，也不让 Army 反向查询 Input Adapter。

## 桌面输入

- 使用当前 Unity Input Manager 的 `Horizontal` 轴读取键盘和手柄。
- 输入限制到 `[-1,1]` 后交给统一的 Input Adapter。
- 没有活动触摸 Pointer 时使用桌面输入；触摸结束后恢复桌面输入。

## 触屏相对拖动

### UI 绑定

Gameplay UI Prefab 提供 `TouchDragArea`：

- `RectTransform` 定义允许开始触摸的范围，并提供横向归一化宽度。
- 可透明的 UI Graphic 必须开启 Raycast Target，使 UGUI EventSystem 可以分发 Pointer 回调。
- `TouchDragInput` 组件持有序列化字段 `horizontalMultiplier`，默认值为 `1`，通过对应 UI Prefab 的 Inspector 配置；值不得小于 `0`。
- `TouchDragInput` 在 UI 层级中，但职责属于 Input 模块，不直接引用 Army，而是把采集值交给同一 Prefab 或场景装配绑定的 Input Adapter。

### Pointer 生命周期

- `PointerDown`：只接受从 `TouchDragArea` 内开始的触摸；记录活动 `pointerId` 和当前局部位置，清空累计差值并输出 `0`。
- `Drag`：只处理活动 `pointerId`；将屏幕坐标转换为 `TouchDragArea` 的局部坐标，限制到区域边界，计算当前位置与上一个有效采样位置的水平差并累加。
- `PointerUp`：清除活动 Pointer、上一位置和累计差值，并发送一次 `0`。
- 同一时间只跟踪一个活动 Pointer；其他 Pointer 不参与合成。
- 组件/Canvas 禁用、Gameplay 离开 `Playing`、场景卸载、应用失去焦点或 Pointer 失效时执行与 PointerUp 相同的清理。

手指是否仍按住不代表存在移动输入。没有新的水平 Drag 差值时，本次输入更新必须输出 `0`；不得保留上一帧方向。

### 归一化公式

每次 Drag 使用相邻有效采样点，而不是最初按下位置：

```text
localDeltaX = currentLocalX - previousLocalX
accumulatedDeltaX += localDeltaX
previousLocalX = currentLocalX
```

Input Adapter 每次更新消费并清零累计值。限制顺序固定为先限制原始滑动速度，再乘系数：

```text
normalizedVelocity = (accumulatedDeltaX / touchAreaWidth) / unscaledDeltaTime
baseTouchInput = Clamp(normalizedVelocity, -1, 1)
horizontalInput = baseTouchInput * horizontalMultiplier
```

- `touchAreaWidth` 取 `TouchDragArea.RectTransform.rect.width` 的有效非零宽度。
- 除以 `unscaledDeltaTime` 用于消除帧率对 Pointer 位移采样的影响，不代表依赖 `TimeService`。
- 按区域宽度归一化用于降低分辨率和 Canvas 缩放差异；必须使用 RectTransform 局部坐标，不能直接把屏幕像素差作为玩法输入。
- `baseTouchInput` 必须先限制到 `[-1,1]`；乘系数后的 `horizontalInput` 不再 Clamp，范围为 `[-horizontalMultiplier, horizontalMultiplier]`。
- `horizontalMultiplier` 默认值为 `1`。大于 `1` 时允许触屏输入按比例超过键盘/手柄的最大轴值，小于 `1` 时降低触屏灵敏度和最终移动倍率。

### 输入源优先级

```text
存在活动触摸 Pointer
├── 本次更新有拖动差值 → 使用触屏计算结果
└── 本次更新没有差值   → 输入 0

不存在活动触摸 Pointer
└── 使用键盘/手柄 Horizontal
```

不把触屏与键盘/手柄数值相加。活动触摸直到 PointerUp 或强制清理前都拥有控制权。

## 非职责

- 不决定 Army 的基础移动速度、阵型宽度或左右边界；基础速度仍来自 `TbArmy.MoveSpeed`，触屏系数只通过输入倍率缩放最终速度。
- 不读取或修改槽位人数、生命值和武器元素配置。
- 不依赖 TimeService；时间步进和实际位移由 Army 负责。
- 不保证手指屏幕位移与 Army 世界位移一一对应；当前输入表达滑动速度方向与强度，不是世界位移命令。
- 不实现按下位置偏移形成持续方向的虚拟摇杆、多指合成、手势识别或按键重绑定。

## 测试标准

- 键盘、手柄和触屏最终都只通过 `IArmyController.SetHorizontalInput` 传递，不发布横向输入事件。
- 左右输入只改变 ArmyRoot 的横向位置；键盘/手柄值限制在 `[-1,1]`，触屏原始归一化滑动速度限制在 `[-1,1]` 后再乘系数。
- PointerDown 本身输出 `0`；第一次 Drag 使用 PointerDown 记录的位置作为上一采样点。
- 连续 Drag 使用相邻局部位置之差，手指停止移动的下一次输入更新输出 `0`，即使 Pointer 仍按住也不会持续移动。
- 同一物理滑动速度在不同稳定帧率、分辨率和 Canvas 缩放下产生近似一致的归一化输入。
- `horizontalMultiplier` 默认值为 `1`；系数为 `2` 时，原始触屏输入 `0.75` 产生最终输入 `1.5`，不得在乘系数后再次 Clamp 到 `1`。
- 活动触摸期间不叠加键盘/手柄输入；PointerUp 后恢复桌面输入。
- 第二个 Pointer 不会覆盖活动 Pointer；PointerUp、禁用、失焦、离开 Playing 和卸载都会清零，不产生粘滞移动。
- Pointer 到达触控区域边界后继续向外移动不再累加，返回区域内时能够产生正确的反向输入。
- ArmyRoot 的边界限制由 Army 根据 LevelConfig 固定道路边界和当前激活槽位 AABB 执行；Input 不直接读取或修改道路坐标。
- 离开 Playing 后，即使仍收到设备输入也不会继续移动 Army；重新进入新会话时由场景装配重新绑定当前 Army。
