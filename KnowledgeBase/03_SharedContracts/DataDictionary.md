# 数据字典

| 字段 | 类型 | 含义 | 约束 |
|---|---|---|---|
| `ArmyCount` | `int` | 当前军队逻辑总人数 | `value >= 0`，如配置总人数上限则不得超过 `ArmyCountLimit` |
| `ArmyId` | `int` | Army 配置、Prefab 与运行时共同身份 | MVP 固定为 `0`，同时选择首行 `TbArmy.Id = 0` 和序列化 Prefab 绑定；多 Army 需另行定案 |
| `ArmyCountLimit` | `int` | 逻辑总人数上限 | `0` 表示不设上限；启用时大于 0 |
| `SlotCapacity` | `int` | Army Prefab 可见槽位容量 | 等于序列化 `ArmySlotView[]` 长度且大于 0，不属于 Luban 字段 |
| `ActiveSlotCount` | `int` | 当前启用的上场槽位数量 | `0 <= value <= SlotCapacity` |
| `SlotIndex` | `int` | 阵型中的稳定槽位索引 | 等于序列化槽位数组下标，`0 <= value < SlotCapacity` |
| `RepresentedCount` | `int` | 单个槽位当前代表的士兵数量 | 大于等于 0 |
| `HpPerSoldier` | `int` | 每名代表士兵提供的聚合生命值 | 大于 0 |
| `SlotCurrentHp` | `int` | 槽位当前聚合生命值 | `0 <= value <= SlotMaxHp` |
| `SlotMaxHp` | `int` | 槽位最大聚合生命值 | `RepresentedCount × HpPerSoldier` |
| `WeaponId` | `int` | Army 当前实际武器配置 ID | 本局运行时状态；0/1 为 Slingshot/Bow，2 为普通 Staff，3～9 为七种元素法杖，必须引用 `TbWeapon` |
| `ArmyWeaponChangeReason` | `enum` | 当前武器发生变化的原因 | `WeaponPickup`、`ElementActivated`、`ElementExpired` |
| `ConfigId` | `int` | 读取 Luban 的对象所引用的配置表 ID | 非负，`0` 合法；Enemy 引用 `TbEnemy`，Prop 引用 `TbProp`；Gate 不使用 ConfigId，也不读表 |
| `SpawnEntryIndex` | `int` | 生成项在所属 LevelConfig 时间轴列表中的稳定索引 | 大于等于 0；用于 Enemy/Gate/Prop 诊断和事件关联，不是配置表主键，也不能代替 RuntimeInstanceId |
| `RuntimeInstanceId` | `int` | 道路对象或敌人本次生成的运行时实例 ID | 非负；在对应 Manager 的活动实例中唯一，不能使用配置 ID 代替 |
| `WorldPosition` | `Vector2` | 运行时对象当前世界坐标 | 由对象运行时状态提供，不回写配置 |
| `IsOnRoad` | `bool` | 道路对象是否仍处于道路活动范围内 | 离场或回收后为 `false` |
| `LevelId` | `int` | 当前关卡的稳定 ID | 非负且 `0` 合法；运行时必须对应当前 `LevelConfigSnapshot` |
| `LevelRunId` | `int` | 一次 Gameplay 会话的稳定运行 ID | 每次进入 Gameplay 递增；过期事件不得作用于新会话 |
| `ConfigLoadState` | `enum` | 配置服务加载状态 | `Uninitialized`、`Loading`、`Ready`、`Failed` |
| `ConfigErrorCode` | `enum` | 配置或资源加载失败原因 | 目录为空、ID 重复、关卡不存在、引用缺失、关卡配置无效、表加载失败、资源缺失 |
| `ArmyConfigSnapshot` | `readonly struct` | `TbArmy` 的运行时只读副本 | `Id`、`ArmyCountLimit`、`HpPerSoldier`、`MoveSpeed`；只由 `IArmyConfigProvider` 返回 |
| `WeaponConfigSnapshot` | `readonly struct` | `TbWeapon` 的运行时只读副本 | `Id`、`FireInterval`、`BulletId`；只由 `IWeaponConfigProvider` 返回 |
| `BulletConfigSnapshot` | `readonly struct` | `TbBullet` 的运行时只读副本 | `Id`、`Damage`、`MoveSpeed`；只由 `IBulletConfigProvider` 返回 |
| `EnemyConfigSnapshot` | `readonly struct` | `TbEnemy` 的运行时只读副本 | `Id`、`EnemyType`、`MaxHp`、`AttackPower`、`MoveSpeed`、`AttackStartRange`、`AttackCooldown`；只由 `IEnemyConfigProvider` 返回 |
| `PropConfigSnapshot` | `readonly struct` | `TbProp` 的运行时只读副本 | `Id`、`WeaponId`、`MaxHp`、`ContactDamage`、`MoveSpeed`；只由 `IPropConfigProvider` 返回 |
| `AppSceneId` | `enum` | SceneService 管理的稳定应用场景身份 | `MainMenu`、`LevelSelect`、`Gameplay`；不包含 `Initializing`、`GameplayLoading` |
| `SceneLoadErrorCode` | `enum` | 应用场景加载、规范根解析或 SceneEntry 初始化失败原因 | 请求无效、场景未配置、加载失败、入口缺失/重名/类型错误、入口初始化失败 |
| `SceneUnloadErrorCode` | `enum` | 当前应用场景异步卸载失败原因 | 请求无效、场景卸载失败 |
| `LevelDescriptor` | `struct` | 选关使用的关卡目录只读描述 | `LevelId`、显示名、初始解锁标记 |
| `LevelConfigSnapshot` | `sealed class` | ConfigService 从 LevelConfig 资产复制的不可变关卡运行时快照 | 包含关卡元数据、道路数值、三个只读生成列表和元素换算系数；不暴露 ScriptableObject |
| `EnemySpawnEntrySnapshot` | `readonly struct` | 敌人生成项运行时副本 | `SpawnTime`、`SpawnPosition`、`ConfigId` |
| `GateSpawnEntrySnapshot` | `readonly struct` | Gate 生成项运行时副本 | `SpawnTime`、`SpawnPosition`、`GateType`、`InitialValue`、`ElementType`、`MaxHp` |
| `PropSpawnEntrySnapshot` | `readonly struct` | Prop 生成项运行时副本 | `SpawnTime`、`SpawnPosition`、`ConfigId` |
| `UnlockedLevelIds` | `List<int>` / 运行时 `IReadOnlyList<int>` | 当前关卡通关后记录的解锁关卡 ID | 空列表表示没有下一关；非空列表首项作为结算页下一关目标，其余项仍解锁；运行时快照只保留 LevelCatalog 中存在且非重复、非自引用的 ID，并保持原顺序 |
| `PlayerProgressSnapshot` | 不可变 `sealed class` | 本地玩家关卡进度快照 | `CompletedLevelIds`、`UnlockedLevelIds`；写盘前去重并按 LevelId 升序 |
| `PlayerProgressSchemaVersion` | `int` | 本地进度 JSON 格式版本 | 当前固定为 `1`；未知版本不读取 |
| `AppFlowState` | `enum` | 应用级流程状态；`GameplayLoading` 仅为内部过渡，不是用户可见页面 | `Initializing`、`MainMenu`、`LevelSelect`、`GameplayLoading`、`Gameplay` |
| `LevelRunState` | `enum` | 单局游玩状态 | `Preparing`、`Playing`、`Completed` |
| `LevelResult` | `enum` | 单局结束结果 | `Victory`、`GameOver` |
| `RoadWidth` | `float` | LevelConfig 序列化的固定道路宽度 | 有限且大于 0；左右边界为 `±value/2` |
| `RoadHeight` | `float` | LevelConfig 序列化的固定道路高度 | 有限且大于 0；上下边界为 `±value/2` |
| `LeftBoundary` | `float` | Army 可移动道路左边界 | 由 `-RoadWidth/2` 派生 |
| `RightBoundary` | `float` | Army 可移动道路右边界 | 由 `RoadWidth/2` 派生 |
| `BottomBoundary` | `float` | 道路下边界 | 由 `-RoadHeight/2` 派生 |
| `TopBoundary` | `float` | 道路上边界 | 由 `RoadHeight/2` 派生；也是 `BulletDespawnY` 的合法上限 |
| `ArmySpawnPosition` | `Vector2` | ArmyRoot 每局初始世界 XY 坐标 | 分量有限且根坐标位于道路内；运行中世界 Y 保持该配置值 |
| `SpawnY` | `float` | 敌人、Gate、Prop 根 GameObject 中心共用的固定出生横线高度 | `0 < EnemyApproachY < SpawnY <= TopBoundary` |
| `EnemyApproachY` | `float` | 敌人根 GameObject 中心结束垂直下移、开始接近 Army 的高度 | `0 < value < SpawnY` |
| `DespawnY` | `float` | Gate/Prop 根 GameObject 中心离开道路并触发离场处理的高度 | `BottomBoundary <= value < 0`；`position.y <= value` 时离场 |
| `BulletDespawnY` | `float` | 士兵子弹根 GameObject 中心的世界 Y 回收线 | LevelConfig 默认 `3`；`ArmySpawnPosition.y < value <= TopBoundary`；`position.y > value` 时当帧回池 |
| `ObstacleKind` | `enum` | 道路对象类别 | `Gate`、`Prop` |
| `ObstacleState` | `enum` | 道路对象当前生命周期/交互状态 | `MovingDown`、`ContactPending`、`ContactSucceeded`、`ContactFailed`、`ExitedUncontacted`、`Broken`、`Recycled` |
| `GateType` | `enum` | 门的规则类型 | `Additive`、`Element` |
| `GateContactState` | `enum` | 门与 Army 的接触结果状态 | `Pending`、`Succeeded`、`Failed`、`ExitedUncontacted` |
| `GateHp` | `int` | 元素门当前生命值 | Pending 时 `0 <= value <= GateMaxHp`；Failed 后锁定为 `1 <= value <= GateMaxHp`；加法门不使用 |
| `GateMaxHp` | `int` | 元素门最大生命值 | 大于 0；加法门不使用 |
| `GateContactDamage` | `int` | 元素门失败接触时对每个槽位造成的伤害 | 大于 0；加法门不使用 |
| `PostDepletionDamage` | `long` | 元素门 HP 归零后、成功接触前累计的可兑换额外伤害 | 初始为 0；只在 `Pending` 且奖励未锁定时增加，使用 `long` 防止多次命中累计溢出 |
| `ElementDurationSecondsPerDamage` | `float` | 本关所有元素门把可兑换额外伤害换算为持续时间的统一系数 | LevelConfig 资产字段并复制到快照；存在元素门时有限且大于 0，否则应为 0 |
| `CalculatedElementDuration` | `float` | 元素门成功接触时计算出的本次持续时间 | `PostDepletionDamage × ElementDurationSecondsPerDamage`；必须有限且大于等于 0，当前不设上限 |
| `PropContactState` | `enum` | 道具与 Army 的接触结果状态 | `Pending`、`Succeeded`、`Failed`、`ExitedUncontacted` |
| `PropHp` | `int` | 道具当前生命值 | Pending 时 `0 <= value <= PropMaxHp`；Failed 后锁定为 `1 <= value <= PropMaxHp` |
| `PropMaxHp` | `int` | 道具最大生命值 | 大于 0 |
| `PropContactDamage` | `int` | 道具未击破接触时对每个槽位造成的伤害 | 大于 0 |
| `ElementType` | `enum` | 元素效果类型 | `None = 0` 只用于未使用配置字段；可获得元素为 `Fire = 1`、`Ice = 2`、`Lightning = 3` |
| `ElementRemainingDuration` | `float` | Army 某一种元素的剩余有效时间 | 本局运行时状态，有限且大于等于 0；三种元素分别保存 |
| `ElementMask` | `flags enum` | 子弹发射瞬间有效的元素集合 | `None = 0`、`Fire = 1`、`Ice = 2`、`Lightning = 4`，允许组合 |
| `RequestedRemoval` | `int` | 负数门提交给 Army 的请求减员人数 | `Abs(GateValue)`，大于 0；不保证等于实际人数损失 |
| `RequestedRemovalDamage` | `long` | 请求减员换算的伤害预算 | `RequestedRemoval × HpPerSoldier`，以 `long` 计算和保存 |
| `ActualArmyCountLoss` | `int` | 一次减员伤害实际造成的人数损失 | 由各槽位受伤后代表人数差值求和，可能不同于 RequestedRemoval |
| `RequestedAddition` | `int` | 非负门提交给 Army 的请求增员人数 | `GateValue`，大于等于 0 |
| `ActualAddition` | `int` | 应用总人数上限后的实际增员人数 | `0 <= value <= RequestedAddition` |
| `GateValue` | `int` | 加法门当前数字 | 初始值不得为 `int.MinValue`；非负值增员，负数值转换为请求减员伤害；正向累加超过范围时饱和到 `int.MaxValue`，不得回绕 |
| `BulletDamage` | `int` | 子弹伤害 | 大于 0 |
| `MoveSpeed` | `float` | 物体移动速度 | 有限且不小于 0，单位为世界单位/秒 |
| `EnemyType` | `enum` | 敌人类型 | `Chick`、`Hen`、`Rooster`，固定值分别为 `0`、`1`、`2` |
| `AttackType` | `enum` | 敌人运行时攻击类型 | 由 `EnemyType` 派生：`Chick` 为 `SingleTarget`，`Hen`/`Rooster` 为 `Area`；不单独配置 |
| `AttackPower` | `int` | 敌人每次攻击造成的槽位伤害 | 大于 0 |
| `AttackStartRange` | `float` | 怪物与锁定槽位目标位置的 XY 欧氏距离小于等于该值时停止接近并开始攻击 | 有限且大于等于 0；距离为 0 的 Army/Enemy 重合状态同样允许攻击 |
| `AttackCooldown` | `float` | 两次攻击开始之间的冷却时间 | 有限且大于等于 0，单位为秒 |
| `BulletId` | `int` | 造成伤害的子弹配置 ID | 必须引用 `TbBullet`；固定 0～9 与十种武器各自的独立子弹对应 |
| `BulletInstanceId` | `int` | 本次生成的具体子弹实例 ID | 活动子弹中唯一；用于碰撞去重 |
| `LevelElapsedTime` | `float` | 本局开始后的关卡运行时间 | `value >= 0`；由 LevelManager 运行时维护 |
| `SpawnTime` | `float` | 生成项相对本局开始的触发时间 | `value >= 0`；按列表非递减排序 |
| `SpawnPosition` | `float` | 道路从左到右的归一化出生位置 | 闭区间 `[0,1]`；`0` 为左边界，`1` 为右边界；按对象中心点计算且只影响初始位置 |
| `SpawnKind` | `enum` | 生成请求类别 | `Enemy`、`Gate`、`Prop` |
| `TimeScale` | `float` | 时间倍率 | MVP 固定为 `1`；后续扩展范围另行定案 |

## 计算规则

MVP 固定 `ArmyId = 0`，初始人数固定为 `1`，初始武器固定为 `WeaponId = 0`，火、冰、雷三种剩余持续时间都为 `0`；这些都不是 `TbArmy` 字段。

非负加法门：Army 增加 `GateValue`；`ArmyCountLimit > 0` 时把结果截断到该逻辑人数上限，`ArmyCountLimit = 0` 时不设上限。

负数加法门：接触结果为失败，Gate 调用 `RemoveArmy(Abs(GateValue))`。Army 把请求减员乘以 `HpPerSoldier` 得到伤害预算，按 `SlotCurrentHp` 升序、再按 `SlotIndex` 升序让激活槽位承担，清空一个槽位后把剩余伤害传给下一个槽位。请求减员不保证等于实际人数损失。

加法门受击：每次有效命中执行 `GateValue += BulletDamageContext.Damage`，不使用固定命中增量，也不按命中次数计算。

元素门受击：

```text
HpDamage = Min(GateHp, BulletDamage)
GateHp = GateHp - HpDamage
ExtraDamage = BulletDamage - HpDamage
PostDepletionDamage = PostDepletionDamage + ExtraDamage
```

最后一发同时清空 HP 时，只有超过清空所需部分的 `ExtraDamage` 进入累计；HP 已为 0 且仍处于 `Pending` 时，本发全部伤害都属于 `ExtraDamage`，子弹照常消费。接触成功时计算 `CalculatedElementDuration = PostDepletionDamage × ElementDurationSecondsPerDamage`；结果为 0 时仍成功，但不调用 `AddElementDuration`。接触失败后奖励永久锁定，后续命中不得增加可兑换的 `PostDepletionDamage`。

元素门和道具接触失败时，对每一个接触到的 Army 槽位应用配置的相同伤害；成功效果只应用一次。道具失败后锁定所有击破效果，不只锁定当前 MVP 的武器替换。

元素门或 Prop 进入 `Failed` 后仍可接收有效子弹命中并播放受击表现，但 HP 使用 `Max(1, CurrentHp - Damage)` 锁在至少 `1`。Failed 元素门不再增加 `PostDepletionDamage`，Failed Prop 不发布 `PropBroken`；两者都不会因后续攻击回收，只继续移动到 `DespawnY` 或由 StopRun 清理。

槽位受击：

```text
SlotCurrentHp = Max(0, SlotCurrentHp - Damage)
RepresentedCount = SlotCurrentHp <= 0
    ? 0
    : (SlotCurrentHp + HpPerSoldier - 1) / HpPerSoldier
SlotMaxHp = RepresentedCount × HpPerSoldier
```

人数增加时不主动重新平均受击后的槽位；先补充代表人数较少的槽位。

元素计时在 Army 的 `TickMovementAndFire` 中先按 Gameplay delta 截断扣减到不小于 `0`，再从三个计时器派生本帧 `ElementMask`。元素门接触阶段新增的持续时间从下一逻辑帧发射开始生效；已经生成的子弹保留自己的掩码快照。
