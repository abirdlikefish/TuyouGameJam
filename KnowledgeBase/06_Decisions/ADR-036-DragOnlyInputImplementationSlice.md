# ADR-036：MVP 最小拖拽输入实现切片

## 状态

Accepted

## 日期

2026-09-19

## 背景

ADR-028 已确认相对拖拽的计算、Pointer 生命周期和清理语义，但同时把键盘、手柄和触屏汇总写入当前 MVP。首个工程切片只需要一种输入：玩家在 Gameplay UI 指定区域内横向拖拽，系统产生一个横向输入倍率。若直接按原文生成代码，仍无法唯一确定采集组件与 Adapter 的接口、逐帧调用顺序、Prefab 结构以及对尚未实现的完整 `IArmyController` 的依赖。

当前 Unity 工程使用 UGUI 和旧 Input Manager。为便于 Editor 验证，左键拖拽与设备触屏共用同一套 UGUI Pointer 路径；这不是第二种玩法输入源，也不读取 Input Manager 的 `Horizontal` 轴。

## 决策

### 当前范围

- 当前 MVP 工程切片只实现 Gameplay 相对拖拽输入。键盘、手柄、按键重绑定和多个输入源优先级全部延后。
- 触屏与 Editor 左键拖拽共用 `IPointerDownHandler`、`IDragHandler`、`IPointerUpHandler`、`IEndDragHandler` 和 `ICancelHandler`；非左键鼠标 Pointer 不开始拖拽。
- 拖拽只产生一个有限的有符号横向倍率。它不是世界位移、虚拟摇杆位置或已发生的事实事件。

### 脚本职责与最小接口

目标脚本结构固定为：

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

- `RelativeDragTracker` 是纯 C# 状态对象，保存活动 `pointerId`、上一个受边界限制的局部 x 和累计水平差；负责开始、追加采样、结束、清理与消费累计差值，不引用 Army、EventBus 或 Unity 场景对象。
- `TouchDragInput` 是 UGUI MonoBehaviour，负责把屏幕坐标使用 `PointerEventData.pressEventCamera` 转换到 `TouchDragArea` 局部坐标、限制到 `RectTransform.rect.xMin/xMax`、转交 Pointer 状态，并按区域宽度、未缩放帧时间和 Inspector 系数返回最终横向倍率。
- `GameplayInputAdapter` 是 Gameplay 场景 MonoBehaviour，持有序列化 `TouchDragInput`，由场景装配显式注入 `IHorizontalInputReceiver`，负责 Playing 启停、每帧消费和提交零值；它不直接引用 ArmyController 具体类型。

公共契约固定为：

```csharp
public interface IHorizontalInputReceiver
{
    void SetHorizontalInput(float value);
}

public interface IGameplayInputGate
{
    void SetGameplayEnabled(bool enabled);
}

public interface IGameplayInputController : IGameplayInputGate
{
    void TickInput(float unscaledDeltaTime);
}
```

`IArmyController` 继承 `IHorizontalInputReceiver`。`GameplayInputAdapter.Initialize(IHorizontalInputReceiver receiver)` 完成运行时接收者绑定；`TouchDragInput` 由 Adapter 的序列化字段绑定，不使用 `Find` 或运行时 `AddComponent`。

具体 API 签名固定为：

```csharp
public sealed class RelativeDragTracker
{
    public bool HasActivePointer { get; }
    public bool TryBegin(int pointerId, float localX);
    public bool TryAddSample(int pointerId, float localX);
    public bool TryEnd(int pointerId);
    public float ConsumeAccumulatedDeltaX();
    public void Reset();
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

public sealed class GameplayInputAdapter : MonoBehaviour,
    IGameplayInputController
{
    [SerializeField] private TouchDragInput touchDragInput;

    public void Initialize(IHorizontalInputReceiver receiver);
    public bool TryValidate(out string error);
    public void SetGameplayEnabled(bool enabled);
    public void TickInput(float unscaledDeltaTime);
}
```

`TryBegin` 只在没有活动 Pointer 时成功；`TryAddSample` 和 `TryEnd` 只接受活动 `pointerId`。`ConsumeHorizontalInput` 消费后立即清零累计差值。无活动 Pointer、没有新水平差值、时间无效或区域宽度无效时返回 `0`。`ResetInput` 清除 Pointer、上一位置和累计差值。以上名称、参数、返回值和可见性作为首版代码生成基线。

`GameplayInputAdapter.Initialize` 只接受非空接收者，必须在 LevelManager `Preparing` 完成前调用；空值抛出 `ArgumentNullException`，以同一接收者重复调用幂等，尝试替换为不同接收者抛出 `InvalidOperationException` 并视为装配失败。初始化前调用启用或 Tick 抛出 `InvalidOperationException`；禁用保持安全幂等，以便未完成初始化的场景仍可清理。启用状态下每次 Tick 都向接收者提交一个值，包括 `0`；第一次有效 Tick 前接收者保持初始化时提交的 `0`。重复禁用不重复产生业务效果。

`TouchDragInput.OnDisable` 和 `OnApplicationFocus(false)` 调用 `ResetInput`；`GameplayInputAdapter.OnDisable` 调用 `SetGameplayEnabled(false)`。同一帧先收到 Drag 后又收到 PointerUp/EndDrag/Cancel 时，结束操作优先并丢弃尚未消费的差值，避免松手后再移动一帧。

屏幕到局部坐标转换失败或局部 x 非有限时，PointerDown 不开始、Drag 不更新上一采样点。`RelativeDragTracker.TryBegin` 和 `TryAddSample` 拒绝非有限 x，避免无效值污染累计状态。

### 逐帧顺序与清理

- LevelManager 持有 `IGameplayInputController`，而不是依赖 Presentation 具体类。
- 每个 Playing 逻辑帧在 Army 移动与发射之前调用一次 `TickInput(unscaledDeltaTime)`；随后 Army 在同一逻辑帧消费最新倍率，不依赖 Unity 同类脚本的隐式 `Update` 顺序。
- `unscaledDeltaTime` 由 LevelManager 在帧开始通过 `ITimeService.GetDeltaTime(TimeDomain.RealTime)` 取得一次并传入。Input 不注入或查询 TimeService，也不直接读取 Unity `Time`。
- `SetGameplayEnabled(false)` 幂等，并立即重置拖拽状态及向当前接收者提交一次 `0`。禁用状态下 `TickInput` 不采集新输入，接收者保持零值。
- PointerUp、EndDrag、Cancel、组件或 Canvas 禁用、场景清理、应用失焦都执行重置。若这些回调发生在本帧输入阶段之前，本帧提交零值；否则下一逻辑帧首先提交零值。

### Prefab 与场景绑定

触控 Prefab 固定为：

```text
Assets/Prefabs/UI/PF_UI_TouchDragArea.prefab
└── TouchDragArea
    ├── RectTransform
    ├── Image
    └── TouchDragInput
```

- `RectTransform` 默认相对 Gameplay Canvas 全屏拉伸：anchorMin `(0,0)`、anchorMax `(1,1)`、offsetMin/offsetMax `(0,0)`、pivot `(0.5,0.5)`。后续若增加会遮挡的交互 UI，再由 Prefab 变体或场景实例收窄区域。
- `Image` 颜色 alpha 为 `0`，`Raycast Target` 开启；不使用运行时添加 Graphic 的兜底。
- `TouchDragInput.touchArea` 显式绑定根 RectTransform，`raycastGraphic` 显式绑定根 Image；`horizontalMultiplier` 默认 `1` 且必须大于等于 `0`。
- Prefab 不包含 Canvas、GraphicRaycaster、EventSystem、StandaloneInputModule、GameplayInputAdapter 或 Army 引用。
- GameplayScene 的 Canvas 持有 GraphicRaycaster；场景中恰好一个 EventSystem 使用 StandaloneInputModule。`GameplayRoot/InputAdapter` 单独挂载 GameplayInputAdapter，并序列化引用 Canvas 下的 TouchDragInput 实例。

### 校验与失败

- Gameplay Preparing 先完成 Input Adapter 初始化，再调用 TouchDragInput 和 GameplayInputAdapter 的 `TryValidate(out string error)`，并校验 Gameplay Canvas、GraphicRaycaster、EventSystem 和 StandaloneInputModule 场景引用。
- `horizontalMultiplier` 非有限或小于 `0`、RectTransform 宽度非有限或小于等于 `0`、缺少必需引用时阻止 Gameplay Ready，并报告稳定字段或对象路径；不得静默创建组件。
- 单帧 `unscaledDeltaTime` 非有限或小于等于 `0` 时只为该帧输出 `0`，不破坏 Pointer 状态；下一有效帧可以继续采样。

## 影响

- 本 ADR 取代 ADR-028 中“当前 MVP 汇总键盘、手柄与触屏”和“PointerUp 后恢复桌面输入”的部分；ADR-028 的相对拖拽公式、边界限制、灵敏度和清理规则继续有效。
- 本 ADR 补充 ADR-033：输入 Tick 成为 Army 移动前的明确同步阶段。
- `PublicInterfaces.md` 新增 `IHorizontalInputReceiver` 和 `IGameplayInputController`；Input 不再依赖完整 Army 命令面。
- 键盘/手柄支持保持 Deferred，未来启用时需要新决策补充输入源优先级和同一接口下的组合规则。
- 当前只更新设计与工程规格，不代表脚本、Prefab、GameplayScene 或测试已经生成。

## 验收标准

- 同一相对拖拽在稳定的不同帧率、分辨率和 Canvas 缩放下产生近似一致的横向倍率。
- PointerDown 输出零；相邻 Drag 产生输入；没有新差值的下一次 Tick 输出零。
- 触屏和 Editor 左键走同一 Pointer 状态机；右键或中键不开始拖拽。
- 每个 Playing 帧输入 Tick 严格发生在 Army 移动与发射之前。
- 禁用、失焦、取消、组件禁用和卸载不会留下粘滞输入。
- 没有键盘、手柄或 `Horizontal` 轴读取代码，零 EventBus 监听者不影响输入提交。
- Prefab 与场景必需引用缺失时 Gameplay 不进入 Ready。

## 测试文件规划

```text
Assets/Tests/EditMode/Input/
├── RelativeDragTrackerTests.cs
└── GameplayInputAdapterTests.cs

Assets/Tests/PlayMode/Input/
└── TouchDragInputTests.cs
```

EditMode 覆盖 Pointer 身份、相邻差值、消费清零、启停幂等、无效 delta 和倍率顺序；PlayMode 覆盖 UGUI 坐标转换、Raycast、左键/触屏事件、边界返回、禁用与失焦清理。当前只记录文件与职责，不创建测试程序集或测试代码。

## 关联文档

- `../00_Project/ProjectOverview.md`
- `../00_Project/Roadmap.md`
- `../01_Architecture/SceneStructure.md`
- `../01_Architecture/TimeSystem.md`
- `../02_Modules/Input/README.md`
- `../02_Modules/Level/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../04_Assets/ArtList.md`
- `../05_Testing/IntegrationTests.md`
- `ADR-028-MvpRelativeDragInput.md`
- `ADR-033-LevelManagerFramePipeline.md`
