# 集成测试清单

## 应用流程

- [ ] BootstrapScene 常驻；MainMenuScene、LevelSelectScene、GameplayScene 都在 Build Settings 中，并按固定根名称各有且只有一个对应 SceneEntry。
- [ ] 服务完成 Create、Connect 且应用级事件订阅完成后才进入 Start；ConfigService Ready 前不得请求任何应用场景。
- [ ] 初始化成功后请求同步 Additive 加载 MainMenuScene；只有 MainMenuSceneEntry 初始化并发布 `AppSceneReady(MainMenu)` 后才进入 MainMenu。初始化失败时保持 `Initializing`。
- [ ] MainMenu 的 RealTime 1 秒计时从 Scene Ready 后开始；到期后只请求一次 LevelSelect 切换，旧场景异步卸载完成前不加载目标场景。
- [ ] LevelSelectSceneEntry Ready 后才进入 LevelSelect、选择目录中有效且可选的唯一 `LevelId`，并启动新的 RealTime 1 秒计时。
- [ ] LevelSelect 计时结束后只创建一个新 `LevelRunId` 并进入 `GameplayLoading`；GameplayScene 同步加载、LevelConfig 注入、入口订阅和 LevelManager `Preparing` 全部完成后才发布 `AppSceneReady(Gameplay)`。
- [ ] 只有匹配的 `AppSceneReady(Gameplay)` 才进入 Gameplay、发布一次 `LevelRunStarted` 并让 LevelManager 进入 Playing。
- [ ] MainMenu、LevelSelect 的场景事实使用 `LevelId = 0`、`LevelRunId = 0`；Gameplay 事实携带当前值，目标不匹配或过期事实不会推进状态。
- [ ] 根缺失、重名、入口类型错误或 Entry 初始化失败时，失败目标场景先完成清理，再发布 `AppSceneLoadFailed`；不得发布 Ready。
- [ ] Gameplay 加载失败时不发布 `LevelRunStarted`，清除待启动会话并请求恢复 LevelSelectScene；只有 LevelSelect Ready 后才进入 LevelSelect。
- [ ] 异步卸载失败发布 `AppSceneUnloadFailed`，停止本次切换且不加载目标场景。
- [ ] Victory 或 GameOver 只接受并发布一次；GameStateService 请求切换 LevelSelect，GameplaySceneEntry 清理并卸载，LevelSelectSceneEntry Ready 后才清除当前会话并进入 LevelSelect。
- [ ] 回到 LevelSelect 后等待 RealTime 1 秒，使用新的 `LevelRunId` 再次开始同一关；上一局的延迟事件、定时器和池实例不会影响新会话。
- [ ] 离开稳定状态、加载失败和终局时旧 RealTime 定时器均已取消，不能启动旧页面或旧关卡。
- [ ] 结构化日志中每次加载恰好出现一次切换请求、Entry 初始化和 Ready；每次卸载恰好出现一次 Entry 清理和 Unloaded，Gameplay 日志包含 `LevelId`、`LevelRunId`。日志不参与流程控制。

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

- [ ] `Gameplay`、`Bullet`、`Gate`、`Monster` 和 `VFX` 都返回倍率为 `1` 的正常未缩放步进；同一帧输入下与 `RealTime` 数值一致。
- [ ] Army 移动和 LevelManager 本局计时使用 `Gameplay`，SpawnManager 只消费由 LevelManager 累计的 `elapsedTime`；Bullet 使用 `Bullet`；Monster 使用 `Monster`；Gate/Prop 使用 `Gate`；Gameplay 世界特效使用 `VFX`。
- [ ] 单次移动、计时或调度只读取一个最具体的时间域，不把 `Gameplay` delta 与子系统域 delta 重复累计。
- [ ] MainMenu 和 LevelSelect 的等待使用 `RealTime` 定时器，不依赖 Gameplay 推进。
- [ ] `TimerHandle.Cancel()` 幂等；状态离开、加载失败、终局或会话失效后，旧回调不会执行或推进流程。
- [ ] MVP 不暴露父域图、重叠归属、倍率修改、暂停令牌、减速、加速或局部时停接口。

## Deferred 能力

- 声音、AudioService、音量设置和 AudioClip 绑定不属于 MVP 验收。
- SaveService、DebugService、暂停、减速、加速和局部时停不属于 MVP 验收。

## 配置系统

- [ ] 初始化阶段可以加载 `LevelCatalog`、Luban `cfg.Tables` 和资源注册表，不依赖 Gameplay 场景。
- [ ] `LevelCatalog` 中每个引用的 `LevelConfig.levelId` 唯一，第一关默认解锁且存在有效引用。
- [ ] LevelSelect 选定 `LevelId` 后，ConfigService 返回已校验的 `LevelConfig`；SceneService 不重复查询配置，并将同一关卡 ID、配置引用和新的 `LevelRunId` 传入 Gameplay。
- [ ] 配置目录、关卡配置或共享表加载失败时发布错误并阻止进入 Gameplay。
- [ ] `LevelConfig` 引用的敌人、Gate 和 Prop 配置 ID 全部存在；EnemyManager 的三个敌人规范 Prefab、ObstacleManager 的 Gate/Prop 规范 Prefab、子弹规范 Prefab、阵型槽位和发射点绑定完整。
- [ ] `enemySpawns` 为空时返回 `InvalidLevelConfig` 并阻止进入 Gameplay；`gateSpawns` 或 `propSpawns` 为空仍可正常加载。
- [ ] `TbGate` 的 Additive/Element 条件字段和 `TbProp` 的生命值、伤害及武器引用校验符合配置契约。
- [ ] `TbArmy` 引用的 `TbWeapon`、`TbElement` 以及 `TbWeapon` 引用的 `TbBullet` 均存在，跨表引用校验失败时启动失败。
- [ ] `TbArmy.ArmyCountLimit = 0` 时不限制人数，大于 0 时正确应用上限；初始人数始终为固定值 1。
- [ ] `TbArmy.MoveSpeed` 是 Army 横向基础速度；实际位移按 `horizontalInput × MoveSpeed × 有效玩法 delta` 计算，触屏系数通过输入倍率影响最终速度但不改写配置。
- [ ] MVP Luban 表不要求 `PrefabKey`、`FormationKey`、`SoldierPrefabKey`、`PropType`、`AttackType`、代表人数缩放字段或 `CollisionBehavior`。
- [ ] 缺少必需的 Unity Prefab、Collider2D、阵型槽位或发射点绑定时阻止进入 Gameplay，并报告稳定来源。
- [ ] `NormalMonster`、`EliteMonster`、`BossMonster`、`AdditiveGate`、`ElementGate`、`WeaponProp` 和 `Bullet` 的具体根类型与规范 Prefab 一一匹配；同类型不同 Prefab 注册被拒绝。
- [ ] 三种武器箱共用 `WeaponProp` 规范 Prefab 并按 `WeaponId` 绑定正确表现；MVP 子弹共用 `Bullet` 规范 Prefab 并按 `BulletId` 取得正确数值和表现。
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
- [ ] Config、Scene、Spawn、Enemy、Obstacle、EventBus、Time 和 Pool 接口均有明确输入、输出和失败语义；Pool 遵守 ADR-031 的类型身份、重复归还、业务重置和防御性失活契约。

## 类型对象池

- [ ] 同一具体类型以同一规范 Prefab 重复请求时返回同一类型池；同一类型绑定不同 Prefab 时明确失败，不创建第二个池。
- [ ] `RentInactive` 对首次创建和复用实例都返回未激活对象；首次实例化不会在 Manager 注入本次租用上下文前执行依赖上下文的 `OnEnable` 逻辑。
- [ ] Manager 在激活前依次设置活动父节点、位置、旋转、`RuntimeInstanceId`、配置、`LevelRunId` 和回调，并完成活动集合登记。
- [ ] Manager 归还前先注销活动实例、完成计数和事实事件、调用 `PrepareForPool()` 并主动失活；类型池随后防御性失活并移动到正确的空闲子节点。
- [ ] 第一次合法归还返回成功；`null`、未知实例、其他类型池实例和重复归还返回失败且不改变池状态。
- [ ] 池对象不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身；失活不会递归归还。
- [ ] `NormalMonster`、`EliteMonster`、`BossMonster` 使用三个不同具体类型池；共有移动、受伤和目标查询规则的复用不改变类型池身份。
- [ ] Gameplay 终局和场景卸载前各 Manager 归还全部活动实例；下一局复用已有 PoolService、PersistentPoolRoot 和类型池，不保留上一局借出状态。
- [ ] PoolService 懒创建实例，不要求预热、容量配置或公共统计；应用清理时销毁全部已知实例并清除类型注册。

## 确定性与基础设施语义

- [ ] MVP 所有时间域和对象局部倍率均为 `1`，未启用运行时倍率调整。
- [ ] 所有包含 Army 身份的事件使用 `ArmyId = 1`。
- [ ] 同一帧按移动阻挡、子弹、Gate/Prop、敌人攻击、终局判断的顺序结算。
- [ ] Bullet Cast 同距离目标按 `Enemy > Gate > Prop > RuntimeInstanceId` 稳定选择。
- [ ] EventBus 按注册顺序同步调用；发布期间使用订阅快照，异常隔离，重复订阅独立 Token，取消幂等。
- [ ] EventBus 只按准确消息类型分发；基类或接口订阅不会收到具体子类型消息。
- [ ] EventBus 支持同步嵌套发布，每层发布使用独立快照；`AppSceneReady(Gameplay)` 处理器发布的 `LevelRunStarted` 不会因重入丢失或重复。
- [ ] 发布期间新增或取消订阅只影响下一次发布；当前快照中的处理器仍按原注册顺序完成。
- [ ] 默认、未知、重复使用和其他 EventBus 实例的 `SubscriptionToken` 取消时无副作用。
- [ ] 单个处理器和异常报告委托抛出异常时，当前快照中的其他处理器仍继续执行。
- [ ] 零监听者发布安全；移除 UI/VFX 监听者不会改变玩法状态、敌人死亡计数或终局结果。
- [ ] 应用服务在配置初始化前完成订阅；各 SceneEntry 在对应 `AppSceneReady` 前完成场景订阅，并在场景卸载前取消；旧场景监听者不会接收新场景或新会话事件。
- [ ] 池对象不直接订阅或查找全局 EventBus；复用前注入本局回调，回收时清除，下一局不会重复发布。
- [ ] Monster 通过必执行回调报告死亡，EnemyManager 先完成死亡去重和 `AliveEnemyCount` 更新再发布一次 `MonsterKilled`；零监听者不影响计数和回收。
- [ ] Unity 资源注册表按 `类别/身份` 键解析，缺失或类型不匹配在进入 Gameplay 前失败。
