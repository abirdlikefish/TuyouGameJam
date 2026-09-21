# ADR-059：关卡开场视频门禁与 Gameplay UI 叠层

- 状态：Accepted
- 日期：2026-09-21
- 关联：ADR-019、ADR-032、ADR-050、ADR-058

## 背景

GameplayScene 已在同一个 Overlay Canvas 中提供 TouchDragArea、BattleHud 与 BattleResult。当前 GameStateService 在收到匹配的 `AppSceneReady(Gameplay)` 后立即进入 `Gameplay` 并发布 `LevelRunStarted`，LevelManager 随即从 `Preparing` 进入 `Playing`。如果只在 UI 上覆盖视频，关卡计时、生成、射击和输入仍会在视频背后开始。

关卡视频素材尚未加入仓库，因此实现还需要允许未配置视频的关卡保持原有可游玩流程，同时对已配置但非法的 Inspector 绑定继续执行严格校验。

## 决策

1. Gameplay Canvas 的直接子节点顺序固定为 `TouchDragArea`、`BattleHud`、`LevelIntroVideo`、`BattleResult`。LevelIntroVideo 播放时覆盖 HUD 和触摸区域；BattleResult 保持最高层级并在开场阶段隐藏。
2. `LevelIntroVideoView` 属于 Presentation，只负责 VideoPlayer、RawImage、黑色射线遮挡、宽高比、准备超时和一次性完成回调。它不修改 AppFlowState、不启动 LevelManager、不查询关卡配置。
3. GameplaySceneEntry 使用 Inspector 中的 `LevelIntroVideoBinding[]` 按 LevelId 解析 VideoClip。数组允许为空，缺少当前 LevelId 表示该关卡没有开场视频并在场景 Ready 后异步跳过；数组内空条目、负数或重复 LevelId、空 VideoClip 属于装配错误并阻止 Ready。
4. GameplaySceneEntry 在初始化时完成视频 View、HUD、Result、LevelManager 和事件订阅；只在收到匹配的 `AppSceneReady(Gameplay)` 后开始视频准备与播放。
5. GameStateService 收到 `AppSceneReady(Gameplay)` 后记录当前场景，但继续保持 `GameplayLoading`，不得立即发布 `LevelRunStarted`。收到匹配当前 LevelId、LevelRunId 和场景的 `LevelIntroFinished` 后才进入 `Gameplay` 并发布一次 `LevelRunStarted`。
6. `LevelIntroFinished` 是已发生事实，携带 `LevelId`、`LevelRunId` 和 `LevelIntroEndReason`。当前结束原因包括正常完成、未配置视频、播放失败和准备超时；四种终止原因都允许进入玩法，避免运行时解码问题造成永久黑屏。
7. 视频使用 `VideoClip`、`VideoPlayer` API Only 输出和 `RawImage`；使用 `UnscaledGameTime`，不输出音频。播放完成前全屏黑色 Graphic 必须开启 Raycast Target；结束前显式重置 TouchDragInput，防止残留 Pointer 差值进入首个 Playing 帧。
8. 场景清理时先停止并清理视频、取消场景 Ready 订阅，再清理 Result、HUD 和 LevelManager。所有延迟完成都必须匹配当前 LevelRunId 且幂等。

## 后果

- `GameplayLoading` 从“等待 GameplayScene Ready”扩展为“等待 GameplayScene Ready 与本局开场表现完成”，不新增额外 AppFlowState。
- `LevelRunStarted` 继续精确表示 LevelManager 可以进入 Playing 的边界，现有 LevelManager 与 GameplayInputAdapter 不需要修改。
- 未提供视频素材时当前关卡只记录 `NoVideoConfigured` 并安全进入玩法；导入 MP4 并绑定后即可启用播放，无需修改代码。
- 本轮保持无声音 MVP；如未来需要视频音轨，需单独决定 AudioSource、音量与生命周期边界。

## 验收

- GameplayScene Ready 后，LevelManager 在视频终止前保持 Preparing，ElapsedTime 为 0，输入、出生、移动、射击和碰撞阶段不推进。
- 视频层完整覆盖 HUD 与 TouchDragArea；结束后隐藏，BattleHud 正常显示，BattleResult 仍为最高层且只在终局显示。
- 正常结束、无配置、播放失败和准备超时都只发布一次匹配的 LevelIntroFinished，并只产生一次 LevelRunStarted。
- 过期 LevelRunId、重复回调或场景卸载后的回调不能启动当前或下一局。
- 连续进入两局时不存在旧 VideoPlayer 回调、旧纹理或旧输入差值。

## 关联文档

- `../01_Architecture/ApplicationFlow.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/UI/README.md`
- `../03_SharedContracts/EventCatalog.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
