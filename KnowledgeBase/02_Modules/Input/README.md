# Input 输入模块

## 模块信息

- ID：`MOD-INPUT`
- 层级：Presentation / Gameplay Adapter
- 状态：`ContractReady`
- 生命周期：Gameplay 场景内；不跨场景保留
- 依赖：`IHorizontalInputReceiver`、UGUI EventSystem、Level；由 Gameplay 场景装配控制启停
- 决策：`../../06_Decisions/ADR-027-MvpGlobalServiceScope.md`、`../../06_Decisions/ADR-028-MvpRelativeDragInput.md`、`../../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md`

## 职责

- 读取 Gameplay UI 指定区域内的相对拖动；设备触屏与 Editor 左键共用同一 UGUI Pointer 路径。
- 把原始归一化滑动速度限制到 `[-1,1]`，再乘 Inspector 灵敏度系数，产生唯一的横向输入倍率。
- 将输入作为 Army 的移动命令，不直接修改 ArmyRoot 的 Transform。
- 只传递横向输入倍率；Army 使用 `TbArmy.MoveSpeed` 作为基础速度。最终倍率按本模块公式允许被 Inspector 系数放大。
- 只在 `LevelManager.Playing` 阶段启用；Preparing、Completed、失去焦点和场景卸载期间清空输入并停止移动命令。

Input Adapter 由 Gameplay 场景装配入口取得最小 `IHorizontalInputReceiver` 并通过 `Initialize(...)` 显式初始化，不通过 `InputService.Instance`、运行时 `Find` 或全局 Service Locator 获取 Army 或服务。ArmyController 实现该最小接口；当前没有跨场景输入消费者，因此不创建全局 `InputService`。

GameplayInputAdapter 实现 `IGameplayInputController`。LevelManager 在匹配的 LevelRunStarted 后调用 `SetGameplayEnabled(true)`，在每个 Playing 帧的 Army 移动前调用 `TickInput(unscaledDeltaTime)`，终局或清理时调用 `SetGameplayEnabled(false)`；禁用必须立即向当前接收者提交一次 `SetHorizontalInput(0)`。这些同步接口用于必须执行的采集与启停，不通过 EventBus 请求 Input 改变状态。

横向输入是必须由当前接收者执行的连续命令，不是已经发生的事实。Input Adapter 直接调用 `IHorizontalInputReceiver.SetHorizontalInput(float)`，不通过项目 `EventBus` 广播输入，也不让 Army 反向查询 Input Adapter。

## 当前实现范围

- 当前只实现一种玩法输入：在 `TouchDragArea` 内进行相对横向拖动。
- 设备触屏和 Editor 左键用于触发同一套 Pointer 状态机；右键和中键不开始拖拽。
- 不读取旧 Input Manager 的 `Horizontal` 轴，不实现键盘、手柄、输入源切换或 Pointer 结束后的桌面输入回退。
- 键盘与手柄为 Deferred；启用前需新增输入源优先级与组合规则决策。

## 相对拖动

### UI 绑定

Gameplay UI 使用 `Assets/Prefabs/UI/PF_UI_TouchDragArea.prefab` 提供 `TouchDragArea`：

- 根 `RectTransform` 默认相对 Gameplay Canvas 全屏拉伸，anchorMin 为 `(0,0)`、anchorMax 为 `(1,1)`、两侧 offset 为 `(0,0)`、pivot 为 `(0.5,0.5)`；它定义允许开始拖拽的范围和横向归一化宽度。
- 根对象使用 alpha 为 `0` 的 `Image`，必须开启 Raycast Target，使 UGUI EventSystem 可以分发 Pointer 回调。
- `TouchDragInput.touchArea` 显式绑定根 RectTransform，`raycastGraphic` 显式绑定根 Image；`horizontalMultiplier` 默认值为 `1`，值不得小于 `0`。
- Prefab 不包含 Canvas、GraphicRaycaster、EventSystem、StandaloneInputModule、GameplayInputAdapter 或 Army 引用。
- Gameplay Canvas 持有 GraphicRaycaster，场景中恰好一个 EventSystem 使用 StandaloneInputModule。`GameplayRoot/InputAdapter` 单独挂载 GameplayInputAdapter，并序列化引用 Canvas 下的 TouchDragInput 实例。

### Pointer 生命周期

- `PointerDown`：只接受从 `TouchDragArea` 内开始的设备触屏或左键 Pointer；记录活动 `pointerId` 和当前局部位置，清空累计差值并输出 `0`。
- `Drag`：只处理活动 `pointerId`；将屏幕坐标转换为 `TouchDragArea` 的局部坐标，限制到区域边界，计算当前位置与上一个有效采样位置的水平差并累加。
- `PointerUp`、`EndDrag` 或 `Cancel`：清除活动 Pointer、上一位置和累计差值；本次或下一次输入 Tick 发送 `0`。
- 同一时间只跟踪一个活动 Pointer；其他 Pointer 不参与合成。
- 组件/Canvas 禁用、Gameplay 离开 `Playing`、场景卸载、应用失去焦点，或 EventSystem 发出 EndDrag/Cancel 时执行与 PointerUp 相同的清理；当前不额外轮询 `Input.touchCount` 判断 Pointer 存活。

手指是否仍按住不代表存在移动输入。没有新的水平 Drag 差值时，本次输入更新必须输出 `0`；不得保留上一帧方向。

### 归一化公式

每次 Drag 使用相邻有效采样点，而不是最初按下位置：

```text
localDeltaX = currentLocalX - previousLocalX
accumulatedDeltaX += localDeltaX
previousLocalX = currentLocalX
```

`TouchDragInput.ConsumeHorizontalInput(unscaledDeltaTime)` 每次调用消费并清零累计值。限制顺序固定为先限制原始滑动速度，再乘系数：

```text
normalizedVelocity = (accumulatedDeltaX / touchAreaWidth) / unscaledDeltaTime
baseTouchInput = Clamp(normalizedVelocity, -1, 1)
horizontalInput = baseTouchInput * horizontalMultiplier
```

- `touchAreaWidth` 取 `TouchDragArea.RectTransform.rect.width` 的有效非零宽度。
- 除以 `unscaledDeltaTime` 用于消除帧率对 Pointer 位移采样的影响；该参数由 LevelManager 读取 `TimeDomain.RealTime` 后传入，不代表 Input 注入或查询 `TimeService`。
- 按区域宽度归一化用于降低分辨率和 Canvas 缩放差异；必须使用 RectTransform 局部坐标，不能直接把屏幕像素差作为玩法输入。
- `baseTouchInput` 必须先限制到 `[-1,1]`；乘系数后的 `horizontalInput` 不再 Clamp，范围为 `[-horizontalMultiplier, horizontalMultiplier]`。
- `horizontalMultiplier` 默认值为 `1`。大于 `1` 时允许拖拽输入按比例超过基础移动倍率，小于 `1` 时降低拖拽灵敏度和最终移动倍率。

### 代码结构与接口

```text
Assets/Scripts/Game/Contracts/Input/
├── IHorizontalInputReceiver.cs
├── IGameplayInputGate.cs
└── IGameplayInputController.cs

Assets/Scripts/Input/
├── RelativeDragTracker.cs
├── TouchDragInput.cs
└── GameplayInputAdapter.cs
```

- `RelativeDragTracker`：纯 C# Pointer 状态与累计差值；不引用场景、Army 或 EventBus。
- `TouchDragInput`：实现 UGUI Pointer 回调，使用 `PointerEventData.pressEventCamera` 转换局部坐标并限制到 RectTransform 边界。
- `GameplayInputAdapter`：持有序列化 TouchDragInput，以最小接口接收 Army，按 LevelManager 的同步阶段提交输入。

```csharp
public interface IHorizontalInputReceiver
{
    void SetHorizontalInput(float value);
}

public interface IGameplayInputController : IGameplayInputGate
{
    void TickInput(float unscaledDeltaTime);
}

public sealed class RelativeDragTracker
{
    public bool HasActivePointer { get; }
    public bool TryBegin(int pointerId, float localX);
    public bool TryAddSample(int pointerId, float localX);
    public bool TryEnd(int pointerId);
    public float ConsumeAccumulatedDeltaX();
    public void Reset();
}

public sealed class GameplayInputAdapter : MonoBehaviour,
    IGameplayInputController
{
    [SerializeField] private TouchDragInput touchDragInput;

    public void Initialize(IHorizontalInputReceiver receiver);
    public bool TryValidate(out string error);
    public void SetGameplayEnabled(bool enabled);
    public void TickInput(float unscaledDeltaTime);
}

public sealed class TouchDragInput : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    IEndDragHandler,
    ICancelHandler
{
    [SerializeField] private RectTransform touchArea;
    [SerializeField] private Graphic raycastGraphic;
    [SerializeField, Min(0f)] private float horizontalMultiplier = 1f;

    public bool HasActivePointer { get; }
    public bool TryValidate(out string error);
    public float ConsumeHorizontalInput(float unscaledDeltaTime);
    public void ResetInput();
    public void OnPointerDown(PointerEventData eventData);
    public void OnDrag(PointerEventData eventData);
    public void OnPointerUp(PointerEventData eventData);
    public void OnEndDrag(PointerEventData eventData);
    public void OnCancel(BaseEventData eventData);
}
```

每个 Playing 逻辑帧固定先调用 `GameplayInputAdapter.TickInput`，再调用 Army 的移动与发射阶段。不得依赖两个 MonoBehaviour 的隐式 `Update` 顺序。单帧 `unscaledDeltaTime` 无效时本帧输出 `0`，但不破坏仍有效的 Pointer 状态。

`GameplayInputAdapter.Initialize` 只接受非空接收者，必须在 LevelManager `Preparing` 完成前调用；空值抛出 `ArgumentNullException`，以同一接收者重复调用幂等，尝试替换为不同接收者抛出 `InvalidOperationException`。初始化前调用启用或 Tick 抛出 `InvalidOperationException`；禁用保持安全幂等，以便未完成初始化的场景仍可清理。初始化成功时先提交一次 `0`；启用后每次 Tick 都提交一个值，包括没有新 Drag 时的 `0`。重复禁用不重复产生业务效果。

`TouchDragInput.OnDisable` 和 `OnApplicationFocus(false)` 调用 `ResetInput`；`GameplayInputAdapter.OnDisable` 调用 `SetGameplayEnabled(false)`。同一帧先收到 Drag 后又收到 PointerUp、EndDrag 或 Cancel 时，结束操作优先并丢弃尚未消费的差值，防止松手后再移动一帧。

屏幕到局部坐标转换失败或局部 x 非有限时，PointerDown 不开始、Drag 不更新上一采样点。`RelativeDragTracker` 的开始与追加方法拒绝非有限 x，避免无效值污染累计状态。

### 准备期校验

Gameplay Preparing 先完成 Input Adapter 初始化，再调用 TouchDragInput 和 GameplayInputAdapter 的 `TryValidate(out string error)`，并校验 Gameplay Canvas、GraphicRaycaster、EventSystem 和 StandaloneInputModule 场景引用。`horizontalMultiplier` 非有限或小于 `0`、区域宽度非有限或小于等于 `0`、透明 Image/Raycast Target 或其他引用无效时阻止 Gameplay Ready，并报告具体字段或对象路径；不得静默添加组件或使用运行时搜索兜底。

### 测试文件规划

```text
Assets/Tests/EditMode/Input/
├── RelativeDragTrackerTests.cs
└── GameplayInputAdapterTests.cs

Assets/Tests/PlayMode/Input/
└── TouchDragInputTests.cs
```

EditMode 覆盖纯状态、数值和 Adapter 生命周期；PlayMode 覆盖 UGUI 坐标转换、Raycast、Pointer 回调、边界、禁用和失焦。当前文档阶段不创建测试程序集或测试代码。

## 非职责

- 不决定 Army 的基础移动速度、阵型宽度或左右边界；基础速度仍来自 `TbArmy.MoveSpeed`，拖拽系数只通过输入倍率缩放最终速度。
- 不读取或修改槽位人数、生命值和武器元素配置。
- 不依赖 TimeService；时间步进和实际位移由 Army 负责。
- 不保证手指屏幕位移与 Army 世界位移一一对应；当前输入表达滑动速度方向与强度，不是世界位移命令。
- 不实现按下位置偏移形成持续方向的虚拟摇杆、多指合成、手势识别或按键重绑定。
- 当前不实现键盘、手柄、旧 Input Manager `Horizontal` 轴或多个输入源优先级。

## 测试标准

- 设备触屏和 Editor 左键最终都只通过 `IHorizontalInputReceiver.SetHorizontalInput` 传递，不发布横向输入事件，也不读取 `Horizontal` 轴。
- 左右输入只改变 ArmyRoot 的横向位置；拖拽原始归一化滑动速度限制在 `[-1,1]` 后再乘系数。
- PointerDown 本身输出 `0`；第一次 Drag 使用 PointerDown 记录的位置作为上一采样点。
- 连续 Drag 使用相邻局部位置之差，手指停止移动的下一次输入更新输出 `0`，即使 Pointer 仍按住也不会持续移动。
- 同一物理滑动速度在不同稳定帧率、分辨率和 Canvas 缩放下产生近似一致的归一化输入。
- `horizontalMultiplier` 默认值为 `1`；系数为 `2` 时，原始拖拽输入 `0.75` 产生最终输入 `1.5`，不得在乘系数后再次 Clamp 到 `1`。
- 右键和中键不开始拖拽；不存在键盘或手柄回退路径。
- 第二个 Pointer 不会覆盖活动 Pointer；PointerUp、禁用、失焦、离开 Playing 和卸载都会清零，不产生粘滞移动。
- Pointer 到达触控区域边界后继续向外移动不再累加，返回区域内时能够产生正确的反向输入。
- ArmyRoot 的边界限制由 Army 根据 LevelConfig 固定道路边界和当前激活槽位 AABB 执行；Input 不直接读取或修改道路坐标。
- 离开 Playing 后，即使仍收到设备输入也不会继续移动 Army；重新进入新会话时由场景装配重新绑定当前 Army。
- `IGameplayInputController` 的重复启用/禁用幂等；禁用后即使设备仍有输入也只保持零输入。
- `Initialize(null)`、初始化前启用/Tick 或使用不同接收者重复初始化都会被拒绝；同一接收者重复初始化幂等。
- 每个 Playing 逻辑帧的输入 Tick 在 Army 移动与发射之前执行，停手后的下一次 Tick 输出 `0`。
- Prefab 或场景的必需引用、Raycast Target、区域宽度或系数无效时 Gameplay 不进入 Ready。
