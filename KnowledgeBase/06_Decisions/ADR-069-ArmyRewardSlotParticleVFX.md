# ADR-069：Army 奖励槽位共享粒子光效

## 状态

Accepted

## 日期

2026-09-22

## 背景

Army 在加法门或鹅笼实际增员、武器箱成功击破以及元素门成功增加持续时间后，需要在结算完成时的每个存活士兵槽位上播放一次短促光效。Army Prefab 当前拥有 51 个固定槽位，但任意时刻只有 `Alive` 槽位参与受击、发射和目标查询；`Dying` 槽位虽然暂时保留 SoldierVisual，却已经不是活动槽位。

直接监听所有 Army 事实会产生两个边界问题：`StartRun` 会把初始 1 人发布为 `ArmyCountChanged(Addition)`；元素增加又可能紧接着派生一次 `ArmyWeaponChanged(ElementActivated)`。如果表现层同时监听人数、元素和武器变化，开局会误播且元素法杖变化会重复播放。

## 决策

1. 新增场景级 `ArmyRewardVfxPresenter`，由 `GameplaySceneEntry` 显式注入当前 `LevelRunId`、`IArmyController`、`IEventBus`、`ITimeService` 和 ArmyRoot Transform。该组件属于 Presentation，不修改 Army、Gate 或 Prop 的玩法状态。
2. 表现器只监听已经完成奖励结算的来源事实：
   - `GateContactResolved`：加法门成功且 `AdditionResult.ActualAddition > 0`；元素门成功且存在正数 `ElementDurationChangeResult`。
   - `GooseCageBroken`：`AdditionResult.ActualAddition > 0`。
   - `PropBroken`：成功且 `EffectApplied = true`。重复拾取当前武器仍播放取得反馈。
3. 每次奖励事实到达后遍历 `IArmyController.GetSlotCapacity()`，仅对 `TryGetSlotTarget` 成功的槽位发射。该查询以 Army 的 `Alive` 状态为权威，因此包含本次增员刚激活的槽位，并排除 Empty 与 Dying。
4. `VFXRoot` 下只保留一组共享 Glow、Ring、Spark ParticleSystem；通过手动 Emit 在所有活动槽位位置生成粒子，不为 51 个槽位各挂一套 ParticleSystem，也不在触发时实例化临时 GameObject。
5. 三套粒子使用 ArmyRoot 作为 Custom Simulation Space，使短生命周期光效继续跟随军队整体横移。粒子由表现器使用 `TimeDomain.VFX` 的 delta 手动推进；系统关闭自动发射、循环和 Play On Awake。
6. 增员使用青绿色、武器使用金白色，元素按 Fire/Ice/Lightning 使用橙红、青蓝、紫黄配色。粒子仅表达奖励类别，不改变数值、武器、元素持续时间或终局状态。
7. Gameplay 场景清理时先取消事件订阅并清空全部粒子，再销毁 Army 实例；过期 `LevelRunId` 事件必须忽略。

## 不采用

- 不监听 `ArmyCountChanged` 作为增员触发，避免开局初始人数误播。
- 不同时监听 `ArmyElementDurationChanged` 与 `ArmyWeaponChanged(ElementActivated)`，避免一次元素奖励播放两次。
- 不在每个 ArmySlotView 下复制粒子组件，避免 51 份序列化配置和空闲组件。
- 不引入 URP、VFX Graph 或第三方粒子框架；项目当前使用 Built-in Render Pipeline。

## 验收

- 加法门、鹅笼实际增员大于 0 时各播放一次；达到上限或实际增员为 0 时不播放。
- 武器箱成功击破播放一次，包括拾取当前武器；Failed 道具不播放。
- 元素门成功增加持续时间时播放一次；由此派生法杖变化不产生第二次播放。
- 每次只覆盖奖励结算后的 Alive 槽位，新激活槽位包含在内，Empty 与 Dying 排除。
- 1、多个和 51 个活动槽位均能显示，快速连续奖励受 `maxParticles` 限制且不创建临时 GameObject。
- 军队横移时光效跟随 ArmyRoot；重试、返回和场景卸载后无旧粒子与旧会话订阅。

## 关联

- `../02_Modules/Army/README.md`
- `../02_Modules/AudioVFX/README.md`
- `../03_SharedContracts/EventCatalog.md`
- `ADR-057-ElementalStaffWeaponVariants.md`
- `ADR-066-ArmySlotDeathPresentation.md`
- `ADR-067-ConfigurablePropTypesAndGooseCageReward.md`
