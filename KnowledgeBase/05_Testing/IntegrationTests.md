# 集成测试清单

## 应用流程

- [ ] 初始化成功后进入 MainMenu；初始化失败时不自动跳过。
- [ ] 只有 GlobalBootstrap 确认 ConfigService 为 Ready 后，才能通知 GameStateService 离开 Initializing；重复通知不重复进入 MainMenu。
- [ ] MainMenu 使用 RealTime 等待 1 秒后只转换一次到 LevelSelect。
- [ ] LevelSelect 只能选择目录中有效且当前可选的 `LevelId`；自动选择唯一关卡与手动选择都只产生一个选中结果。
- [ ] LevelSelect 使用 RealTime 等待 1 秒后只调用一次 `TryStartSelectedGameplay`；重复回调不会创建第二个会话。
- [ ] 开始 Gameplay 时先进入内部 `GameplayLoading`，只有场景加载、LevelConfig 注入和 LevelManager `Preparing` 完成后才进入 Gameplay/Playing。
- [ ] 场景加载失败时发布 `GameplaySceneLoadFailed`，不发布 `LevelRunStarted`，并回到 LevelSelect。
- [ ] Victory 或 GameOver 后只结束当前 `LevelRunId`，清理游玩对象并回到 LevelSelect，不返回 MainMenu。
- [ ] Victory 或 GameOver 只发布一次；GameStateService 在收到匹配的 `GameplaySceneUnloaded` 后才清除当前会话并回到 LevelSelect。
- [ ] 回到 LevelSelect 后等待 RealTime 1 秒，使用新的 `LevelRunId` 再次开始同一关。
- [ ] 上一局的延迟事件、定时器和对象池实例不会影响新的 `LevelRunId`。
- [ ] 离开 MainMenu/LevelSelect、加载失败和终局时旧的 RealTime 定时器均已取消，不能启动旧关卡。

## 装配与服务访问

- [ ] 冷启动和 Gameplay 重开期间始终只有一个 `GlobalRoot` 和一套 MVP 全局服务实例。
- [ ] 服务唯一性由 GlobalBootstrap / Composition Root 持有，不要求各服务暴露静态 `Instance`；Gameplay 和 Presentation 代码不通过运行时 `Find` 或通用 Service Locator 取得必需服务。
- [ ] GameStateService 可以注入假的 Config、Scene、Time 和 EventBus 实现，独立验证状态转换、失败和幂等行为。
- [ ] Gameplay 场景装配入口只向 LevelManager、EnemyManager、ObstacleManager 等消费者注入其实际需要的最小接口。
- [ ] Bullet、Monster、Gate 和 Prop 等池对象不访问全局服务集合；对应 Manager 在复用时传入本局配置快照、`LevelRunId`、`RuntimeInstanceId` 和必要回调。
- [ ] Input Adapter 只存在于 Gameplay 场景，在 Playing 阶段启用，不创建跨场景 `InputService`，也不依赖 TimeService。
- [ ] 除 Bootstrap/Config 的过渡加载边界外，模块不直接访问 `LubanTables.Instance`，所有运行时配置查询均经 `IConfigService`。

## 输入

- [ ] 键盘、手柄和触屏都由 Gameplay Input Adapter 汇总，并通过 `IArmyController.SetHorizontalInput` 同步提交；项目 EventBus 不发布连续横向输入事件，Army 不反向查询 Input Adapter。
- [ ] `TouchDragArea` 的 RectTransform 定义 Pointer 开始范围和归一化宽度；对应 UI Prefab 的 `horizontalMultiplier` 可在 Inspector 配置，默认值为 `1` 且不小于 `0`。
- [ ] PointerDown 记录活动 `pointerId` 和局部位置但输出 `0`；Drag 使用当前位置与上一个有效采样位置的水平差，并能累积同一次输入更新前的多个 Pointer 采样。
- [ ] 触屏先计算 `(accumulatedDeltaX / touchAreaWidth) / unscaledDeltaTime`，将该原始滑动速度 Clamp 到 `[-1,1]`，再乘 `horizontalMultiplier`；乘系数后的结果不再次 Clamp。
- [ ] 当原始触屏输入为 `0.75`、`horizontalMultiplier = 2` 时，传给 Army 的输入为 `1.5`；Army 拒绝 NaN/无穷值，但不把该有限值截断为 `1`。
- [ ] 手指仍按住但没有新的 Drag 差值时，下一次输入更新为 `0`，不会沿最后方向继续移动。
- [ ] 相同物理滑动速度在不同稳定帧率、分辨率和 Canvas 缩放下产生近似一致的原始归一化值。
- [ ] 活动触摸期间不叠加或回退到键盘/手柄；没有 Drag 时为 `0`，PointerUp 后才恢复桌面输入。
- [ ] 一次只跟踪一个 Pointer；第二个 Pointer 不覆盖活动 Pointer。PointerUp、组件/Canvas 禁用、离开 Playing、场景卸载、应用失焦和 Pointer 失效都会清除累计值并向 Army 发送一次 `0`。
- [ ] Pointer 到达 TouchDragArea 边界后继续向外移动不再累加输入，返回区域内时产生正确的反向输入。

## 核心闭环

- [ ] Gameplay 会话先进入 Preparing，初始化完成后进入 Playing。
- [ ] 子弹可以命中怪物并造成伤害。
- [ ] 子弹可以命中加法门并按 `HitIncrement` 增加数字。
- [ ] 加法门接触 Army 时按加法规则更新总人数；负数门标记失败但仍应用人数变化。
- [ ] 元素门在接触前 HP 清空后接触 Army，只更新一次元素。
- [ ] 元素门未清空时，对每个接触槽位造成相同伤害并继续向下离场。
- [ ] 门只对 Army 进行一次接触判定；未接触门可以直接从道路下方离场。
- [ ] Gate/Prop 先通过 `IArmyController` 完成状态变更再发布事实事件；增删 UI 或 VFX 监听者不会改变结算结果。
- [ ] 道具在接触前击破后只触发一次配置的击破效果；当前 MVP 的三种武器箱按 `WeaponId` 更新武器并保留当前元素。
- [ ] 道具未击破接触时，对每个接触槽位造成相同伤害并继续向下离场。
- [ ] 怪物从固定出生横线上的配置位置向下移动，到达接近线后向最近的有效士兵槽位移动；初始横向位置不限制后续移动。
- [ ] 初始总人数为 1，并按配置创建对应的上场槽位。
- [ ] 总人数超过最大上场槽位后，槽位代表人数平均分配，余数按槽位顺序分配。
- [ ] 槽位受击只减少当前槽位人数，不主动重新平均其他槽位。
- [ ] 新增人数优先补充受击后人数较少或为空的槽位。
- [ ] 槽位聚合 HP 按 `HpPerSoldier` 计算，伤害按比例转换为槽位人数损失。
- [ ] 每个激活槽位从独立发射点按同一武器间隔发射一枚子弹；改变代表人数不改变单次发射数量、伤害或速度。
- [ ] Weapon 与 Element 组合后的子弹属性稳定且可复现。
- [ ] 子弹、敌人、Enemy AttackCollider、Army 槽位、Gate 和 Prop 的 Prefab 均配置职责明确的 Collider2D 和 Layer。
- [ ] 子弹沿上一位置到期望位置执行 Collider Cast，高速或掉帧时不穿透敌人、Gate 或 Prop，且一次命中只结算一次。
- [ ] 精英/Boss 的 AttackCollider 只在攻击判定帧执行一次显式重叠查询，每个 Army 槽位最多受击一次。
- [ ] Gate/Prop 与 Army 的接触通过显式查询完成，接触状态和运行时 ID 保证一次性结算。
- [ ] 存活敌人的 BodyCollider 不重叠；前方敌人较慢或静止时，后方敌人在安全间距等待且不会推动前方敌人。
- [ ] 敌人进入 Dead 后退出受击和阻挡查询；死亡动画不会阻塞后方敌人。
- [ ] 首版不会因敌人受阻而执行侧向绕行、通道预留或局部导航。
- [ ] ArmyRoot 移动受当前激活槽位 AABB 限制，阵型变化后边界更新。
- [ ] ArmyRoot 的可移动范围同时受固定道路左右边界限制，任何激活槽位都不能越过道路边界。
- [ ] 多个槽位同时接触同一门时只应用一次门接触结果。
- [ ] `ObstacleManager` 能登记、查询、注销和回收 Gate/Prop，且同一配置的多个实例拥有不同运行时 ID。
- [ ] 军队人数为 0 时进入 GameOver。
- [ ] 所有敌人生成项处理完且 `AliveEnemyCount == 0` 后进入 Victory。
- [ ] Victory 携带当前关卡 ID 和配置中的 `unlockedLevelIds`。
- [ ] Victory 的 `unlockedLevelIds` 只作为本局结果传递，不创建或修改本地存档。
- [ ] Army 归零后进入 GameOver。
- [ ] 同一帧最后一只敌人死亡且 Army 归零时进入 GameOver。
- [ ] Victory 和 GameOver 停止本局逻辑并清理当前会话；重新进入同一关前经过 LevelSelect 的 1 秒 RealTime 等待。

## 时间系统

- [ ] 所有 Gameplay 时间域返回倍率为 `1` 的正常未缩放步进。
- [ ] MainMenu 和 LevelSelect 的等待使用 `RealTime` 定时器，不依赖 Gameplay 推进。
- [ ] `TimerHandle.Cancel()` 幂等；状态离开、加载失败、终局或会话失效后，旧回调不会执行或推进流程。
- [ ] MVP 不暴露倍率修改、暂停令牌、减速、加速或局部时停接口。

## Deferred 能力

- 声音、AudioService、音量设置和 AudioClip 绑定不属于 MVP 验收。
- SaveService、DebugService、暂停、减速、加速和局部时停不属于 MVP 验收。

## 配置系统

- [ ] 初始化阶段可以加载 `LevelCatalog`、Luban `cfg.Tables` 和资源注册表，不依赖 Gameplay 场景。
- [ ] `LevelCatalog` 中每个引用的 `LevelConfig.levelId` 唯一，第一关默认解锁且存在有效引用。
- [ ] LevelSelect 选定 `LevelId` 后，ConfigService 返回已校验的 `LevelConfig`；SceneService 不重复查询配置，并将同一关卡 ID、配置引用和新的 `LevelRunId` 传入 Gameplay。
- [ ] 配置目录、关卡配置或共享表加载失败时发布错误并阻止进入 Gameplay。
- [ ] `LevelConfig` 引用的敌人、Gate 和 Prop 配置 ID 全部存在；当前关卡需要的 Unity Prefab、阵型槽位和发射点绑定完整。
- [ ] `enemySpawns` 为空时返回 `InvalidLevelConfig` 并阻止进入 Gameplay；`gateSpawns` 或 `propSpawns` 为空仍可正常加载。
- [ ] `TbGate` 的 Additive/Element 条件字段和 `TbProp` 的生命值、伤害及武器引用校验符合配置契约。
- [ ] `TbArmy` 引用的 `TbWeapon`、`TbElement` 以及 `TbWeapon` 引用的 `TbBullet` 均存在，跨表引用校验失败时启动失败。
- [ ] `TbArmy.ArmyCountLimit = 0` 时不限制人数，大于 0 时正确应用上限；初始人数始终为固定值 1。
- [ ] `TbArmy.MoveSpeed` 是 Army 横向基础速度；实际位移按 `horizontalInput × MoveSpeed × 有效玩法 delta` 计算，触屏系数通过输入倍率影响最终速度但不改写配置。
- [ ] MVP Luban 表不要求 `PrefabKey`、`FormationKey`、`SoldierPrefabKey`、`PropType`、`AttackType`、代表人数缩放字段或 `CollisionBehavior`。
- [ ] 缺少必需的 Unity Prefab、Collider2D、阵型槽位或发射点绑定时阻止进入 Gameplay，并报告稳定来源。
- [ ] 修改 Luban 数据并重新生成后，Unity 使用新数值且未编辑生成代码。
- [ ] 三类生成列表的时间、数量、配置 ID 和 `[0,1]` 横向出生位置与 `LevelConfig` 一致；`0`、`1` 和中间值正确映射到固定 `spawnY` 横线。
- [ ] 相同 `spawnPosition` 的不同尺寸敌人、Gate 和 Prop 使用相同中心点坐标，不按碰撞体或渲染尺寸内缩。
- [ ] 任一生成项的 `spawnPosition` 越界、为 NaN 或无穷值时返回 `InvalidLevelConfig` 并阻止进入 Gameplay。
- [ ] 配置源不被运行时人数、生命值、门数字、道具 HP、生成游标或关卡计时覆盖。

## Spawn 与公共契约

- [ ] 三个时间轴游标只存在于 SpawnManager，LevelManager 不直接访问生成列表。
- [ ] LevelManager 每帧传入当前 `LevelRunId` 和 `elapsedTime`，生成条目只消费一次；过期会话的 Tick 不会推进新会话。
- [ ] 终局后 SpawnManager 停止消费；新会话只调用一次 `StartRun(LevelConfig, LevelRunId)`，并重置全部游标。
- [ ] 过期 `LevelRunId` 的生成请求、管理器操作和结果事件不会影响新会话。
- [ ] Config、Scene、Spawn、Enemy、Obstacle、EventBus 和 Time 接口均有明确输入、输出和失败语义；Pool 的失败、重复归还和重置语义在 `PoolSystem.md` 定案后再提升为 ContractReady。

## 确定性与基础设施语义

- [ ] MVP 所有时间域和对象局部倍率均为 `1`，未启用运行时倍率调整。
- [ ] 所有包含 Army 身份的事件使用 `ArmyId = 1`。
- [ ] 同一帧按移动阻挡、子弹、Gate/Prop、敌人攻击、终局判断的顺序结算。
- [ ] Bullet Cast 同距离目标按 `Enemy > Gate > Prop > RuntimeInstanceId` 稳定选择。
- [ ] EventBus 按注册顺序同步调用；发布期间使用订阅快照，异常隔离，重复订阅独立 Token，取消幂等。
- [ ] Unity 资源注册表按 `类别/身份` 键解析，缺失或类型不匹配在进入 Gameplay 前失败。
