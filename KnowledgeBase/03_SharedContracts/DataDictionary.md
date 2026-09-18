# 数据字典

| 字段 | 类型 | 含义 | 约束 |
|---|---|---|---|
| `ArmyCount` | `int` | 当前军队逻辑总人数 | `value >= 0`，如配置总人数上限则不得超过 `ArmyCountLimit` |
| `ArmyId` | `int` | Army 运行时身份 | MVP 固定为 `1`；多 Army 需另行定案 |
| `ArmyCountLimit` | `int` | 逻辑总人数上限 | `0` 表示不设上限；启用时大于 0 |
| `MaxDeployedSoldiers` | `int` | 最大上场槽位数量 | 大于 0 |
| `ActiveSlotCount` | `int` | 当前启用的上场槽位数量 | `0 <= value <= MaxDeployedSoldiers` |
| `SlotIndex` | `int` | 阵型中的稳定槽位索引 | `0 <= value < MaxDeployedSoldiers`；同一阵型配置中不重复 |
| `RepresentedCount` | `int` | 单个槽位当前代表的士兵数量 | 大于等于 0 |
| `HpPerSoldier` | `float` | 每名代表士兵提供的聚合生命值 | 大于 0 |
| `SlotCurrentHp` | `float` | 槽位当前聚合生命值 | `0 <= value <= SlotMaxHp` |
| `SlotMaxHp` | `float` | 槽位最大聚合生命值 | `RepresentedCount × HpPerSoldier` |
| `WeaponId` | `int` | 军队使用的武器配置 ID | 必须引用 `TbWeapon` |
| `ElementId` | `int` | 军队使用的元素配置 ID | 必须引用 `TbElement` |
| `ConfigId` | `int` | 道路对象或生成项引用的配置表 ID | 根据对象类别引用对应的 `TbGate`、`TbProp` 或 `TbEnemy` 主键 |
| `RuntimeInstanceId` | `int` | 道路对象或敌人本次生成的运行时实例 ID | 在对应 Manager 的活动实例中唯一；不能使用配置 ID 代替 |
| `WorldPosition` | `Vector2` | 运行时对象当前世界坐标 | 由对象运行时状态提供，不回写配置 |
| `IsOnRoad` | `bool` | 道路对象是否仍处于道路活动范围内 | 离场或回收后为 `false` |
| `LevelId` | `int` | 当前关卡的稳定 ID | 必须对应当前 `LevelConfig` |
| `LevelRunId` | `int` | 一次 Gameplay 会话的稳定运行 ID | 每次进入 Gameplay 递增；过期事件不得作用于新会话 |
| `ConfigLoadState` | `enum` | 配置服务加载状态 | `Uninitialized`、`Loading`、`Ready`、`Failed` |
| `ConfigErrorCode` | `enum` | 配置或资源加载失败原因 | 目录为空、ID 重复、关卡不存在、引用缺失、关卡配置无效、表加载失败、资源缺失 |
| `SceneLoadErrorCode` | `enum` | Gameplay 场景加载或入口绑定失败原因 | 请求无效、场景加载失败、Gameplay 入口缺失 |
| `LevelDescriptor` | `struct` | 选关使用的关卡目录只读描述 | `LevelId`、显示名、初始解锁标记 |
| `UnlockedLevelIds` | `List<int>` | 当前关卡通关后记录的解锁关卡 ID | 首版只记录，不执行下一关跳转 |
| `AppFlowState` | `enum` | 应用级流程状态；`GameplayLoading` 仅为内部过渡，不是用户可见页面 | `Initializing`、`MainMenu`、`LevelSelect`、`GameplayLoading`、`Gameplay` |
| `LevelRunState` | `enum` | 单局游玩状态 | `Preparing`、`Playing`、`Completed` |
| `LevelResult` | `enum` | 单局结束结果 | `Victory`、`GameOver` |
| `RoadWidth` | `float` | 固定道路宽度 | 大于 0 |
| `RoadHeight` | `float` | 固定道路高度 | 大于 0 |
| `SpawnY` | `float` | 敌人、Gate、Prop 共用的固定出生横线高度 | 位于固定道路空间内 |
| `LeftBoundary` | `float` | Army 可移动道路左边界 | 小于 `RightBoundary` |
| `RightBoundary` | `float` | Army 可移动道路右边界 | 大于 `LeftBoundary` |
| `EnemyApproachY` | `float` | 敌人结束垂直下移、开始接近 Army 的高度 | 位于固定道路空间内 |
| `DespawnY` | `float` | 道路对象离开道路并触发离场处理的高度 | 位于 `EnemyApproachY` 下方的道路空间内 |
| `ObstacleKind` | `enum` | 道路对象类别 | `Gate`、`Prop` |
| `ObstacleState` | `enum` | 道路对象当前生命周期/交互状态 | `MovingDown`、`ContactPending`、`ContactSucceeded`、`ContactFailed`、`ExitedUncontacted`、`Broken`、`Recycled` |
| `GateType` | `enum` | 门的规则类型 | `Additive`、`Element` |
| `GateContactState` | `enum` | 门与 Army 的接触结果状态 | `Pending`、`Succeeded`、`Failed`、`ExitedUncontacted` |
| `GateHp` | `int` | 元素门当前生命值 | `0 <= value <= GateMaxHp`；加法门不使用 |
| `GateMaxHp` | `int` | 元素门最大生命值 | 大于 0；加法门不使用 |
| `GateContactDamage` | `int` | 元素门失败接触时对每个槽位造成的伤害 | 大于 0；加法门不使用 |
| `PropContactState` | `enum` | 道具与 Army 的接触结果状态 | `Pending`、`Succeeded`、`Failed`、`ExitedUncontacted` |
| `PropHp` | `int` | 道具当前生命值 | `0 <= value <= PropMaxHp` |
| `PropMaxHp` | `int` | 道具最大生命值 | 大于 0 |
| `PropContactDamage` | `int` | 道具未击破接触时对每个槽位造成的伤害 | 大于 0 |
| `ElementType` | `enum` | 元素效果类型 | `Fire`、`Ice`、`Lightning` |
| `GateValue` | `int` | 加法门当前数字 | 可为任意整数；负数接触标记失败但仍应用加法效果 |
| `BulletDamage` | `int` | 子弹伤害 | 大于 0 |
| `MoveSpeed` | `float` | 物体移动速度 | 不小于 0 |
| `EnemyType` | `enum` | 敌人类型 | `Normal`、`Elite`、`Boss` |
| `AttackType` | `enum` | 敌人运行时攻击类型 | 由 `EnemyType` 派生：`Normal` 为 `SingleTarget`，`Elite`/`Boss` 为 `Area`；不单独配置 |
| `AttackPower` | `int` | 敌人每次攻击造成的槽位伤害 | 大于 0 |
| `AttackStartRange` | `float` | 敌人进入后停止移动并开始攻击的距离 | 大于等于 0 |
| `AttackCooldown` | `float` | 两次攻击开始之间的冷却时间 | 大于等于 0 |
| `BulletId` | `int` | 造成伤害的子弹配置 ID | 必须引用 `TbBullet` |
| `BulletInstanceId` | `int` | 本次生成的具体子弹实例 ID | 活动子弹中唯一；用于碰撞去重 |
| `LevelElapsedTime` | `float` | 本局开始后的关卡运行时间 | `value >= 0`；由 LevelManager 运行时维护 |
| `SpawnTime` | `float` | 生成项相对本局开始的触发时间 | `value >= 0`；按列表非递减排序 |
| `SpawnPosition` | `float` | 道路从左到右的归一化出生位置 | 闭区间 `[0,1]`；`0` 为左边界，`1` 为右边界；按对象中心点计算且只影响初始位置 |
| `SpawnKind` | `enum` | 生成请求类别 | `Enemy`、`Gate`、`Prop` |
| `TimeScale` | `float` | 时间倍率 | MVP 固定为 `1`；后续扩展范围另行定案 |

## 计算规则

MVP 初始人数固定为 `1`，不是配置字段。

加法门：先计算 `ArmyCount + GateValue`；`ArmyCountLimit > 0` 时截断到 `[0, ArmyCountLimit]`，`ArmyCountLimit = 0` 时只截断下限到 `0`。

加法门接触结果：`GateValue >= 0` 为成功，`GateValue < 0` 为失败；两者都应用相同的 `ArmyCount` 变化，负数门沿用普通加法门接触后流程回收且不重复判定。

元素门和道具接触失败时，对每一个接触到的 Army 槽位应用配置的相同伤害；成功效果只应用一次。道具失败后锁定所有击破效果，不只锁定当前 MVP 的武器替换。

槽位受击：

```text
SlotCurrentHp = Max(0, SlotCurrentHp - Damage)
RepresentedCount = SlotCurrentHp <= 0 ? 0 : Ceil(SlotCurrentHp / HpPerSoldier)
SlotMaxHp = RepresentedCount × HpPerSoldier
```

人数增加时不主动重新平均受击后的槽位；先补充代表人数较少的槽位。
