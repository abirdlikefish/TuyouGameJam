# 事件系统

## 设计目的

通过事件让模块松耦合。发布者只发布事实，监听者自行决定是否更新显示、视觉反馈或其他非必需表现。MVP 完全无声音，不创建音频监听者。

## 事件命名

- 使用过去式或状态变化命名，例如 `ArmyCountChanged`、`MonsterKilled`。
- 事件参数使用明确的数据类型，不使用无意义字符串。
- 事件目录统一维护在 `../03_SharedContracts/EventCatalog.md`。

## 典型链路

```text
GlobalBootstrap → GameStateService.NotifyInitializationReady()
GameStateService → MainMenu / LevelSelect 定时器 → TrySelectLevel / TryStartSelectedGameplay
GameStateService → SceneService.LoadGameplay(levelId, levelConfig, levelRunId)
SceneService → GameplaySceneReady / GameplaySceneLoadFailed → GameStateService
GameStateService → LevelRunStarted → LevelManager、UI
Gate / Prop → 调用 IArmyController 类型化命令 → Army 状态变更
Gate / Prop → GateValueChanged、GateContactResolved、PropBroken、PropContactDamage → UI、VFX
ArmyController → ArmyCountChanged → UI、ArmyVisual
ArmyController → ArmyFormationChanged / SoldierHit → ArmyVisual、UI、VFX
EnemyManager / Monster → MonsterSpawned、MonsterDamaged、MonsterAttackLanded、MonsterKilled → LevelManager、VFX
LevelManager → calls GameStateService.CompleteGameplay(LevelCompletion)
GameStateService → SceneService.UnloadGameplay(levelRunId)
SceneService → GameplaySceneUnloaded → GameStateService → LevelSelect
```

`GameStateService` 负责应用流程和结果事件；`LevelManager` 负责当前 Gameplay 会话的玩法运行、终局判定和结果提交。`Victory`、`GameOver` 是结果事实，不是应用流程状态。

场景加载期间使用内部 `GameplayLoading` 状态。只有 `SceneService` 发布 `GameplaySceneReady` 且 `LevelManager` 完成 `Preparing` 后，`GameStateService` 才进入 `Gameplay` 并发布 `LevelRunStarted`。加载失败回到 `LevelSelect`，不得进入 `Playing`。

Gate/Prop 对 Army 的人数、槽位伤害、元素和武器变更使用同步接口；事实事件只在变更完成后发布。这样事件监听顺序不会改变玩法结果，也不会因 Army 同时直接调用和订阅事件而重复结算。

## 使用边界

- 需要立即执行、返回值、失败结果或确定顺序时，使用类型化同步接口，不发布请求型事件。
- 事件只描述已经发生的事实；发布者不知道、不等待也不依赖某个具体订阅者。
- 当订阅者数量为零时，发布者负责的核心结果仍必须正确；否则该交互必须改用同步接口、依赖倒置接口或职责明确的用例协调器。
- EventBus 可以让发布者不引用具体表现实现，但不得作为绕过程序集依赖规则的反向命令通道。

EventBus 的基础语义为同步发布、按注册顺序调用、发布开始时固定订阅快照、单个处理器异常隔离、重复订阅独立 Token、取消幂等；详见 ADR-021。

后续程序集边界、依赖倒置与 Composition 装配规则见 [程序集边界](AssemblyBoundaries.md)。
