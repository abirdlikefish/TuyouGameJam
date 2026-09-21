# ADR-049：Army 持续战斗动画与胜利表现阶段

> ADR-060 补充并修订射击同步：`FireInterval` 成为三种战斗动画的权威周期，状态切换继承周期相位，实际换武器从第 0 帧立即发射。

- 状态：Accepted
- 日期：2026-09-20
- 关联：ADR-009、ADR-033、ADR-043、ADR-048

## 背景

ADR-048 最初把 Army Attack 定义为每次实际发射后触发的非循环表现，并依赖 Animator 参数和 Transition 返回 Idle 或移动状态。当前正式素材与玩法目标进一步明确：士兵在 Gameplay Playing 期间始终自动攻击，静止、左移和右移只是三种持续战斗姿态；子弹发射仍由逻辑冷却独立调度。原状态机因此会制造不必要的 Trigger、返回转换和动画重播边界。

同时，现有 LevelManager 在 Victory 时先执行 Army `StopRun`，会立即隐藏士兵，再请求切换 LevelSelect。后续结算 UI 需要保留胜利画面，因此需要把“停止玩法推进”和“最终隐藏清理”分成两个明确阶段。

## 决策

### Army 动画状态

1. Army 保留 `Idle`、`Attack`、`MoveLeft`、`MoveRight`、`Victory` 五个稳定状态和五个 OverrideController 替换键。
2. Preparing 使用循环 Idle。进入 Playing 后，原地使用循环 Attack，实际向左或向右位移时分别使用循环 MoveLeft、MoveRight；三个 Playing 状态都表示持续自动攻击。
3. MoveLeft/MoveRight 根据道路限位后的实际 X 位移选择，不直接根据输入意图选择。顶在道路边缘且实际位移为零时播放 Attack。
4. Victory 非循环并停留末帧。士兵全部死亡时沿用槽位失活逻辑，不新增 Army Death Clip。
5. ArmyController 保存当前表现状态并通过状态哈希显式调用 `Animator.Play`；只在状态变化、换武器或槽位重新激活时播放，不在每帧无条件重启。
6. `AC_Army_Base` 不再包含 `MoveDirection`、`Attack`、`Victory` 参数，也不包含状态间 Transition。代码是唯一切换来源。

### 射击与换武器

1. 子弹发射继续由 ArmyController 的每槽位 FireInterval 冷却决定。Army 动画没有发射 AnimationEvent，也不决定伤害、BulletId 或射击时刻。
2. 换武器时把预创建的目标 AnimatorOverrideController 应用到全部槽位，包括隐藏槽位，并从当前表现状态第 0 帧开始播放；战斗中不先回 Idle。
3. 新增或重新激活的槽位继承当前 WeaponId 和当前表现状态。
4. 缺少正式图片时允许继续使用已经绑定的空正式 Clip，但不得出现 Missing Motion、空 Override 映射或运行时资源回退。

### Victory 表现与清理

1. `IArmyRunController` 增加 `EnterVictoryPresentation(levelRunId)`。它停止接受移动和玩法命令、保留活动 SoldierVisual，并切换到 Victory。
2. LevelManager 确认 Victory 后停止输入、Spawn、Obstacle、Enemy 和 Bullet，Army 暂不执行 `StopRun`；LevelManager 已处于 Completed，因此不会继续驱动 Army Tick。
3. 当前版本仍立即请求 LevelSelect。Victory 动画可能只能短暂显示，但士兵不会在请求场景切换前被主动隐藏。
4. Gameplay 场景卸载、显式 LevelManager `StopRun` 或销毁时仍必须执行 Army `StopRun`，完成身份、数值、Collider 和视觉清理。未来结算 UI 只延长 Victory 表现阶段，不改变最终清理入口。
5. GameOver 继续立即停止并隐藏 Army，不进入 Victory 表现。

## 后果

- Army 不通过 AnimationEvent 驱动发射，玩法逻辑保持权威；ADR-060 进一步要求三种战斗 Clip 时长与 `FireInterval` 一致并共享攻击周期相位。
- Army Animator Controller 更简单，三套武器仍共用同一状态结构和预创建 AOC。
- 胜利表现拥有可延长的生命周期边界，但当前应用流程仍直接返回 LevelSelect。
- Attack 改为循环后需要检查素材首尾接缝；这属于视觉验收，不改变射击结果。

## 验收标准

- Preparing 中 Army 为 Idle；第一个 Playing Tick 后根据实际位移进入 Attack、MoveLeft 或 MoveRight。
- 未发生状态变化时动画不会每帧回到第 0 帧。
- 道路边缘无实际位移时为 Attack，而不是移动状态。
- W000/W001/W002 换装后全部活动和隐藏槽位使用新 AOC，并保持当前战斗状态。
- Army 的四个非 Victory Clip 循环，Victory 非循环；Controller 无参数、无自动 Transition。
- Victory 请求切换场景前士兵保持显示，场景卸载时仍完成最终 StopRun 清理。

## 关联文档

- `../02_Modules/Army/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../04_Assets/AnimationPipeline.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
