# ADR-066：Army 槽位死亡动画与延迟隐藏

## 状态

- 状态：Accepted / InProgress
- 日期：2026-09-21
- 影响范围：Army、Level、Animation、Prefab、测试

## 背景

Army 槽位当前以 `RepresentedCount > 0` 同时决定逻辑存活、受击、发射和 SoldierVisual 显隐。槽位最后一名士兵死亡时会立即隐藏，无法播放不同 WeaponId 对应的死亡动画；若只延迟隐藏而不增加状态，又会让增员按最少人数规则立即复活该槽位，或让全军归零后的 `StopRun` 当帧清除最终死亡表现。

## 决策

1. 槽位内部采用 `Empty`、`Alive`、`Dying` 三态。致死伤害同步把代表人数、HP、最大 HP 和射击冷却归零，立即更新 Army 总人数、事件和终局判定；死亡动画只延迟 SoldierVisual 隐藏，不延迟权威死亡。
2. `Dying` 保留 SoldierVisual，关闭 SlotCollider，不再受击、发射、成为敌人目标或参与 `RemoveArmy`；增员选择最少人数时跳过 `Dying`。全部槽位都处于 `Dying` 时本次 `AddArmy` 的实际增加量为 0，不缓存奖励。
3. 每个 WeaponId 增加一个非循环 Death Clip。槽位死亡时锁定死亡瞬间的 AnimatorOverrideController；期间发生武器或移动状态变化只记录待恢复状态，不打断 Death。动画结束后进入 `Empty`、隐藏表现并应用最新 Controller，之后才能由增员重新激活。
4. SoldierVisual 上的 `ArmySlotAnimationEventProxy` 显式引用所属 ArmySlotView。每个正式 Death Clip 在结束时间恰好调用一次 `OnDeathAnimationFinished()`；重复、过期或 StopRun 后的回调幂等忽略，不再次修改人数或发布事件。
5. GameOver 仍在帧末按 ArmyCount 立即提交并停止其他玩法模块；LevelManager 调用 `EnterDefeatPresentation` 保留 Army 实例，使最后一批 Death 播放完成。场景卸载、重试、返回或显式停止时仍由 `StopRun` 最终清理。Victory 沿用既有保留表现流程。
6. Sequence Animation Builder 登记十个 Army Death 目录和 Clip，补齐 Army 基础 Death 状态、十个 AOC 覆盖、末帧事件和 Prefab 代理绑定。本次只创建目录与空 Clip，不生成或推断正式动画帧。

## 结果

- ArmyCount 始终等于所有槽位 `RepresentedCount` 之和；Dying 槽位的代表人数固定为 0。
- 终局优先级、伤害事件和 GameOver 提交时机保持不变，仅延长败北结果页背景中的死亡表现。
- Army AOC 从五个覆盖项扩展到六个；缺失 Death Clip、事件或显式代理绑定会阻止 Gameplay Ready。

## 验收

- 非致死伤害不进入 Dying；致死伤害只触发一次 Death，且人数立即归零。
- Dying 期间不受击、不发射、不被选中、不参与增减员，普通战斗状态和换武器不打断死亡动画。
- Death 完成后槽位隐藏并重新具备增员资格；复活后使用最新 WeaponId 的战斗状态。
- 最后一名士兵死亡时 GameOver 只提交一次，死亡动画仍能播放至末帧；离场或重试可提前 StopRun 并安全清理。
- 十种 WeaponId 的 Death 目录、非循环 Clip、AOC 覆盖和唯一结束事件完整。
