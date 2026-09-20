# ADR-054：应用使用单一常驻摄像机

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-025、ADR-032、ADR-050、ADR-051、ADR-053

## 背景

GameplayScene 当前持有唯一 MainCamera。切换到 MainMenuScene 或 LevelSelectScene 时，SceneService 会先卸载 GameplayScene，再加载新的应用场景；Gameplay Camera 随场景正确销毁，但两个 UI 场景只有 Screen Space - Overlay Canvas，没有 Camera 继续清理颜色缓冲。透明 UI 区域因此可能继续显示 Gameplay 的最后一帧，看起来像旧摄像机没有消失。

当前 Gameplay 代码不查询 `Camera.main`，GameplaySceneEntry 也不持有 Camera 引用。三个应用场景同一时刻至多只有一个处于 Ready，因此可以把渲染摄像机提升为应用生命周期资源，而不改变场景切换协议或玩法规则。

## 决策

1. 将现有 Gameplay MainCamera 移到 BootstrapScene 的 GlobalRoot 下并命名为 `AppCamera`，由 GlobalRoot 的 `DontDestroyOnLoad` 生命周期统一持有。
2. GameplayScene 不再拥有 Camera 或 AudioListener。所有应用场景共享唯一启用的 Camera、`MainCamera` Tag 和 AudioListener。
3. AppCamera 保留现有正交投影、Transform、Clear Flags、背景色、裁剪面、Culling Mask 和其他渲染参数；本决策只迁移所有权，不调整镜头构图。
4. MainMenu、LevelSelect 和 Gameplay 的 Canvas 继续使用 Screen Space - Overlay，`worldCamera` 保持为空。Camera 负责每帧清屏和世界对象渲染，Overlay Canvas 继续独立渲染与接收事件。
5. GlobalBootstrap 使用序列化引用绑定 AppCamera 与同对象 AudioListener，并在创建服务前 Fail-Fast 校验：二者必须存在、启用、位于 GlobalRoot 子层级、属于同一个 GameObject，且 Camera 必须使用 `MainCamera` Tag。
6. 不引入 CameraService、运行时搜索、动态补组件或场景切换时的摄像机启停逻辑。未来需要镜头移动、缩放或震动时，再以明确的场景适配接口控制这台 AppCamera。

## 后果

- GameplayScene 卸载后仍有 Camera 每帧执行 Solid Color 清屏，不再暴露上一帧玩法画面。
- 加载 MainMenu、LevelSelect 或 Gameplay 不会创建第二台 Camera 或 AudioListener。
- 世界对象即使来自 Additive 加载的应用场景，仍会按 AppCamera 的 Culling Mask 正常渲染。
- Camera 成为 Bootstrap 装配的一部分；其引用或层级错误会阻止应用启动。
- 当前 MVP 无音频服务，但保留唯一 AudioListener，避免未来接入音频时改变监听者所有权。

## 验收

- 冷启动到 MainMenu、进入 LevelSelect、进入 Gameplay、终局返回 LevelSelect 的全过程始终只有一台启用 Camera 和一个启用 AudioListener。
- Camera 实例在应用场景切换前后保持不变，GameplayScene 中不存在 Camera 或 AudioListener。
- 切换离开 Gameplay 后不显示 Gameplay 最后一帧；MainMenu 与 LevelSelect 的 Overlay UI 正常显示和点击。
- Gameplay 世界构图、正交尺寸和可见内容与迁移前一致。
- 必需引用缺失、跨层级、不同 GameObject、禁用或 Tag 错误时，GlobalBootstrap 在加载应用场景前失败。

## 关联文档

- `../01_Architecture/BootstrapAndComposition.md`
- `../01_Architecture/SceneStructure.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
- `../07_Changes/ChangeLog.md`
