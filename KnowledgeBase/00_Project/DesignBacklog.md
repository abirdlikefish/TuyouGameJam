# 设计待决清单

本文件集中记录尚未定案、可能影响多个模块的游戏机制。条目在形成共识前只能作为讨论输入，不能直接当作实现规格。

## 使用规则

- 每个条目必须写明问题、当前假设、候选方案、影响范围和验收方式。
- 定案后在 `06_Decisions` 新增 ADR，回链本文件，并将条目标记为 `Accepted` 或移入历史。
- 只影响一个模块的细节留在该模块 README，不把所有细节堆到本文件。

## 待决问题

| ID | 主题 | 当前状态 | 影响模块 | 下一步 |
|---|---|---|---|---|
| DES-001 | 军队横向移动和动态阵型边界 | Accepted | Army、Input、Level | 见 ADR-004、ADR-009；道路边界由 LevelConfig 固定道路提供，Army 结合当前阵型 AABB 限制移动 |
| DES-002 | 道路/镜头是否持续向上推进，还是对象单向下落 | Accepted | Level、Spawn、Monster、Gate | 见 ADR-009；固定道路和镜头，生成对象先向下移动 |
| DES-003 | 加法门数字的伤害增量、正负接触结果和一次性结算 | Accepted | Bullet、Gate、Army、DataDictionary | 见 ADR-006、ADR-035、ADR-038；命中按实际子弹伤害增加门值，非负值增员，负数值提交请求减员并由 Army 按最低 HP 顺序分配等价伤害 |
| DES-004 | 怪物到达底部的扣人数方式：固定伤害、按怪物类型，还是直接结束 | Accepted | Monster、Army、Level | 首版移除到底部流程，见 ADR-005 |
| DES-011 | 道路接近线由关卡空间配置还是由 Army 位置动态计算 | Accepted | Level、Monster、Spawn | 见 ADR-009；由 LevelConfig 提供固定 `enemyApproachY` |
| DES-005 | 总人数上限、最大上场槽位和显示规则 | Accepted | Army、UI、Config | 见 ADR-004、ADR-020；`ArmyCountLimit = 0` 表示不设上限，最大上场槽位仍独立限制表现对象数 |
| DES-006 | MVP 的关卡数量、生成时间轴长度和胜利条件 | Accepted | Level、Spawn、UI | 见 ADR-009；单关卡、无波次，全部敌人生成且击杀后胜利 |
| DES-007 | 暂停、减速和局部时停是否属于首个可玩版本 | Accepted（MVP 延后） | Time、UI、所有玩法模块 | 见 ADR-001、ADR-021、ADR-027；MVP 所有时间倍率固定为 `1`，当前接口不暴露倍率修改或暂停能力 |
| DES-008 | 槽位聚合生命值和比例受击换算 | Accepted | Army、Monster、Bullet、UI | 见 ADR-004、ADR-035；使用整数 HpPerSoldier，负数门按最低当前 HP、再按槽位索引分配伤害 |
| DES-009 | Weapon/Element 组合及代表人数对发射参数的缩放 | Accepted | Army、Bullet、Config | 见 ADR-020、ADR-035；固定 WeaponId 与发射瞬间 ElementMask，不按代表人数缩放射速、伤害或数量 |
| DES-010 | 受击后槽位缺口的补充优先级 | Accepted | Army、Gate、UI | 见 ADR-004、ADR-046；增员逐人选择当前代表人数最少、再按 SlotIndex 最小的槽位，不保存额外缺口标志 |
| DES-012 | 门/道具接触失败后，后续子弹命中是否仍发放成功奖励 | Accepted | Gate、Prop、Bullet、Army | 见 ADR-006、ADR-046；失败状态锁定奖励并将 HP 最低锁在 1，后续仍受击和消费子弹，但不会击破或发放元素、武器效果 |
| DES-013 | 武器身份使用 `WeaponId`，不维护第二份 `WeaponType` 身份 | Accepted | Army、Prop、Bullet、Config | 见 ADR-007、ADR-035；固定 `0=Slingshot`、`1=Bow`、`2=Staff` |
| DES-014 | Gate/Prop 的道路实例由统一 ObstacleManager 管理 | Accepted | Obstacle、Gate、Prop、Spawn、Scene | 见 ADR-008；补充运行时实例查询和回收测试 |
| DES-015 | 初始化、主界面、选关与游玩会话的流程边界 | Accepted | GlobalServices、Level、Scene、Config、UI | 见 ADR-011、ADR-019、ADR-051、ADR-053；主界面等待开始，选关按目录显示节点并等待选择，终局回到选关 |
| DES-016 | LevelCatalog、LevelConfig 与配置初始化/注入边界 | Accepted | Config、GlobalServices、Scene、Level | 见 ADR-012；初始化加载目录，选关后按 LevelId 注入 Gameplay |
| DES-017 | 三类生成时间轴的游标所有权和调度接口 | Accepted | Level、Spawn、Monster、Obstacle | 见 ADR-013；游标只由 SpawnManager 持有，LevelManager 只传入时间并查询结果 |
| DES-018 | Config、Scene、Spawn、Manager、EventBus、Time 和 Pool 公共契约基线 | Accepted | 架构、全部 Gameplay 模块 | 见 ADR-014；Pool 的原始 `GameObject + string key` 契约已由 ADR-031 修订为具体组件类型池 |
| DES-019 | 路线图勾选与设计成熟度的状态边界 | Accepted | 项目管理、全部模块 | 见 ADR-015；路线图复选框只表示工程实现和验证完成 |
| DES-020 | 玩法碰撞形状、查询方式与敌人阻挡/绕行范围 | Accepted | Army、Bullet、Gate、Prop、Monster、Level | 见 ADR-016、ADR-037、ADR-046；所有玩法碰撞对象使用 Collider2D 与显式 Cast/Overlap，敌人阻挡只查询上一同步姿态并在 MVP 参数下尽量排队，不保证同帧绝对不重叠；Army/Enemy 允许重合，侧向绕行延后 |
| DES-021 | 得分、存档和设置持久化是否进入当前 MVP | Accepted（存档已启用） | 项目范围、事件、配置、全局服务、测试 | ADR-017 原先延后全部持久化；ADR-062 现启用移动端本地关卡进度，得分与设置持久化仍延后 |
| DES-022 | Luban MVP 初始字段和 Unity 资源绑定边界 | Accepted | Config、Army、Monster、Gate、Prop、Bullet、资源 | 见 ADR-020、ADR-035、ADR-038；Army Prefab 与槽位容量由 Unity 序列化绑定，当前不建立 TbElement 或 TbGate，Gate 的关卡内联字段与 Prefab 参数按 ADR-038 分工 |
| DES-023 | MVP Army 身份与时间倍率范围 | Accepted | Army、Time、事件、全部 Gameplay | 见 ADR-021、ADR-027、ADR-046；`ArmyId` 固定为首行 ID `0`，MVP 所有时间倍率固定为 `1`，倍率和暂停接口不进入当前契约 |
| DES-024 | 同帧碰撞阶段与同距离命中优先级 | Accepted | Bullet、Gate、Prop、Monster、Level、碰撞契约 | 见 ADR-021、ADR-061；移动阻挡→首次同步→子弹/组合结算→冰火位移→第二次同步→Gate/Prop→敌人攻击→终局，同距离按 Enemy > Gate > Prop > RuntimeInstanceId |
| DES-025 | 道路世界坐标系 | Accepted | Level、Army、Spawn、Monster、Gate、Prop | 见 ADR-021、ADR-023；世界 XY 平面、z=0、右为 +x、上为 +y，出生位置在固定 `SpawnY` 上按 `[0,1]` 横向映射 |
| DES-026 | Army 横向移动速度来源 | Accepted | Army、Input、Config | 见 ADR-021、ADR-028、ADR-036；`TbArmy.MoveSpeed` 提供基础速度，当前拖拽原始滑动速度限制到 `[-1,1]` 后再乘 Inspector 系数作为最终输入倍率；键盘/手柄延后 |
| DES-027 | Gate/Prop 统一道路状态快照映射 | Accepted | Gate、Prop、Obstacle、UI、调试 | 见 ADR-021；专用接触状态保留，快照使用统一 `ObstacleState` |
| DES-028 | EventBus 分发语义 | Accepted | EventBus、全部事件消费者、测试 | 见 ADR-021；同步、注册顺序、异常隔离、独立 Token、取消幂等 |
| DES-029 | Unity 资源注册表键命名 | Accepted | Config、资源、Spawn、Army、Monster、Gate、Prop、Bullet | 见 ADR-021、ADR-031；使用大小写敏感的 `类别/身份` 键维护非池身份资源，PoolService 不以资源键选择 Prefab |
| DES-030 | Layer Collision Matrix 最终关系 | Accepted | Bullet、Army、Gate、Prop、Monster、Project Settings | 见 ADR-037；固定六个 Gameplay Layer，自动物理矩阵默认全部关闭，只保留显式查询；EnemyBody 与 ArmySlot 不建立移动碰撞关系 |
| DES-031 | 道具击破效果目录、单个/组合方式、目标与叠加规则 | Partially Accepted | Prop、Army、Config、事件、UI、测试 | ADR-067 增加 `PropType`、配置化 BasketballProp 与 GooseCageProp 固定增员效果，并保持 WeaponProp 武器切换；通用多效果组合、叠加与任意目标结构仍延后，见 ADR-022、ADR-064、ADR-067 |
| DES-032 | 生成对象的横向出生位置表达 | Accepted | Level、Spawn、Monster、Gate、Prop、Config | 见 ADR-023；移除三路生成点 ID，所有生成项改用 `[0,1]` 的 `spawnPosition` |
| DES-033 | 程序集分层与跨层通信方式 | Accepted（实现延后） | 架构、全局服务、全部 Gameplay、UI、AudioVFX | 见 ADR-024、ADR-026；当前不创建 `.asmdef`，后续以粗粒度程序集强制单向依赖，同步接口用于必须执行的操作，事件只传递已发生的事实 |
| DES-034 | MVP 全局服务范围、访问方式与应用流程服务边界 | Accepted | 全局服务、Input、Time、AudioVFX、Save、测试 | 见 ADR-027、ADR-062；服务由 Composition 以应用级唯一实例持有并显式注入，不普遍使用静态单例；Save 现作为关卡进度旁路启用，Audio/Debug 仍延后 |
| DES-035 | MVP 触屏横向移动的采集、归一化和模块通信 | Accepted | Input、Army、UI、测试 | 见 ADR-028；在 Gameplay UI 区域内读取相邻采样点的水平差，按区域宽度和未缩放帧时间归一化，原始滑动速度先限制到 `[-1,1]` 再乘 Inspector 系数，通过同步接口传给 Army，停手即归零 |
| DES-036 | EventBus 实现、载荷类型和订阅生命周期 | Accepted | EventBus、Composition、全部事件发布者与监听者、测试 | 见 ADR-029；主线程同步精确类型分发、允许嵌套发布、Token 关联 Bus 身份、按领域组织事件，小型载荷用 `readonly struct`，大型快照用不可变 `sealed class` |
| DES-037 | 时间域的当前归属、父子包含与重叠规则 | Accepted（实现延后） | Time、全部 Gameplay、VFX、UI、测试 | 见 ADR-030；MVP 保持扁平枚举且每次操作只选一个最具体域，未来时间控制采用单父级层次，不允许任意重叠归属 |
| DES-038 | 类型对象池身份、激活顺序与归还语义 | Accepted | Pool、Composition、Monster、Obstacle、Bullet、VFX、测试 | 见 ADR-031；一个具体池化根类型对应一个规范 Prefab 和一个类型池，Manager 负责 Transform、初始化与主动激活/失活，类型池借出未激活实例并在归还时防御性失活 |
| DES-039 | MainMenu/LevelSelect 实际场景、固定 SceneEntry、场景切换与服务分阶段初始化 | Accepted | 应用流程、场景、Composition、全局服务、事件、测试 | 见 ADR-032；三个稳定页面使用实际 Additive 场景和固定根入口，GameState 只命令 SceneService，加载同步、卸载异步，服务按 Create/Connect/Start 装配 |
| DES-040 | Gameplay 同帧阶段顺序的执行所有者 | Accepted | Level、Army、Bullet、Monster、Obstacle、Spawn、Input、Time、测试 | 见 ADR-033；LevelRunStarted 只启动会话，LevelManager 通过同步阶段接口依次驱动生成、移动、命中、接触、攻击、回收和终局判断 |
| DES-041 | 道路边界权威字段与 Army 初始坐标 | Accepted | Level、Army、Spawn、Monster、Gate、Prop、Config、Scene | 见 ADR-052；LevelConfig 使用 `roadWidth`、`roadHeight` 派生原点居中的四边，ArmyRoot 每局从 `armySpawnPosition` 开始，道路不使用玩法 Collider |
| DES-042 | Gameplay 数值配置的类型化查询与只读快照 | Accepted | Config、Army、Bullet、Monster、Prop、Composition | 见 ADR-041；五类表分别通过最小类型化 Provider 返回不可变快照，ConfigService 启动时完整校验并复制，表错误单点报错后立即退出 |
| DES-043 | Animator 攻击判定与确定性敌人攻击阶段的衔接 | Accepted | Monster、Level、Animation、测试 | 见 ADR-040；非循环 Attack Clip 的命中关键帧调用 `OnAttackFrame()` 登记一次请求，末帧调用结束通知，实际伤害只在 `ResolveAttacks` 中校验并结算 |
| DES-044 | Gameplay Manager StartRun/StopRun 的失败语义 | Accepted | Level、Composition、Army、Bullet、Monster、Obstacle、Spawn、应用流程 | 见 ADR-039；配置与绑定在 StartRun 前集中校验，错误直接记录并阻止 Ready，接口保持 void，不建设通用 Result、降级或恢复状态机；意外异常只做必要清理并停止进入可玩状态 |
| DES-045 | unlockedLevelIds 的目录校验规则 | Accepted | Config、Level、GameState、LevelSelect | 见 ADR-044；重复 ID 和自引用按 InvalidLevelConfig 致命退出，目录缺失 ID 使用 Debug.LogWarning 后过滤，空列表合法，Victory 只携带过滤后的快照结果 |
| DES-046 | Army 配置、Prefab 容量、运行时装备、三元素计时与负数门减员 | Accepted | Army、Config、Composition、Gate、Bullet、事件、测试 | 见 ADR-035、ADR-046；ArmyId=0 同时选择 TbArmy 首行与序列化 Prefab，槽位数组决定容量，WeaponId 固定 0/1/2，当前删除 TbElement，元素按三计时器与 ElementMask 表达 |
| DES-047 | Gate 配置来源、伤害驱动数值与元素奖励结算 | Accepted | Level、Spawn、Obstacle、Gate、Bullet、Army、Config、事件、测试 | 见 ADR-038、ADR-046；仅保留加法门和元素门，Gate 不读表；逐门初始值、元素类型与 MaxHp 位于 LevelConfig，Prefab 提供同类共用速度/接触伤害；加法门按实际伤害累加，元素门只用 Pending 状态下 HP 归零后的额外伤害乘关卡系数兑换持续时间，Failed 后锁血 1 且永久锁定奖励 |
| DES-048 | LevelConfig 资产、运行时快照与程序集依赖闭合 | Accepted | Contracts、ConfigGenerated、Foundation、Composition、Scene、Level、Spawn | 见 ADR-042；LevelConfig 资产及转换归 Foundation，Luban 代码归生成程序集，Contracts 定义不可变 LevelConfigSnapshot，IConfigService 只保留查询，具体初始化由 Composition 调用 |
| DES-049 | 槽位射击、攻击冷却、死亡动画与道路接触/阈值语义 | Accepted | Army、Bullet、Monster、Gate、Prop、Obstacle、Level、Animation、测试 | 见 ADR-043、ADR-060；初始活动槽位和实际换武器立即首发，运行中新激活槽位等待完整间隔，每槽每帧最多一弹并保留周期余量；攻击冷却从起攻计算，Death 末帧登记回收；Gate/Prop 只做终点 Overlap，纵向阈值按根中心判定 |
| DES-050 | 正式序列帧的导入目录、Prefab 数量与 ID 到 Animator 的预绑定方式 | Accepted | Army、Bullet、Monster、Gate、资源、Prefab、测试 | 见 ADR-048、ADR-049；源包留在 Reference，运行时只导入透明 Sprite；保持单 Army、单 Bullet、单 ElementGate 与三类 Monster Prefab。Army 通过预绑定 AOC 与代码显式状态选择，Bullet/ElementGate 使用 Animator 参数，不运行时按路径加载 |
| DES-051 | 双元素子弹的派生伤害、目标选择、击退、死亡目标和表现生命周期 | Accepted | Bullet、Monster、ElementCombo、Level、VFX、Prefab、测试 | 见 ADR-061；三个二元素精确匹配并在命中阶段显式一次性结算，三元素延后；死亡目标只保留视觉覆盖，冰火位移后执行第二次物理同步 |

## 已接受决策

- `../06_Decisions/ADR-001-TimeSystem.md`：采用自定义时间域；MVP 所有时间倍率固定为 `1`，暂停、减速、加速和局部时停延后，当前接口范围见 ADR-027。
- `../06_Decisions/ADR-002-GateCalculation.md`：历史上的乘法门方案，已由 ADR-006 替代。
- `../06_Decisions/ADR-006-AdditiveGateAndContactResolution.md`：定案加法门、元素门、一次接触结算和未接触离场。
- `../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md`：修订 Gate 配置与伤害结算：Gate 不读表、只保留加法门和元素门、逐门参数进入 LevelConfig、同类共用参数进入 Prefab，并以实际伤害及 HP 归零后的额外伤害驱动数值与元素持续时间。
- `../06_Decisions/ADR-039-PrototypeValidationScope.md`：定案首轮工程切片的场景装配错误直接记录并停止 Ready、敌人 Prefab `blockingGap`、Gate 单文本占位表现、HUD 延后、调参规避高速穿透，以及 Victory 立即截断剩余 Gate/Prop；配置表致命失败语义由 ADR-041 后续修订。
- `../06_Decisions/ADR-040-AnimatorAttackFrameBridge.md`：定案 Animator 非循环 Attack Clip 的关键帧调用 Monster 本地方法登记攻击请求，实际伤害仍由 EnemyManager 在 ResolveAttacks 阶段执行，并以动画末帧通知结束本次攻击。
- `../06_Decisions/ADR-041-TypedConfigProvidersAndFatalValidation.md`：补齐 Army、Weapon、Bullet、Enemy、Prop 五类最小配置 Provider 与不可变快照，并定案配置表、目录和关卡数据错误由 ConfigService 单点报错后立即退出应用。
- `../06_Decisions/ADR-042-LevelConfigSnapshotAssemblyBoundary.md`：将 LevelConfig 编辑资产与运行时快照分离，关闭 Contracts/Foundation 对 Gameplay 具体类型的潜在反向依赖。
- `../06_Decisions/ADR-043-FireAttackDeathAndContactBoundaries.md`：定案槽位射击冷却、攻击冷却起点与重攻时机、死亡动画回收事件、Gate/Prop 终点 Overlap 及根中心阈值。
- `../06_Decisions/ADR-044-UnlockedLevelIdsValidation.md`：关闭 DES-045；`unlockedLevelIds` 的重复 ID 和自引用为致命配置错误，目录缺失 ID 只警告并从运行时快照中过滤，空列表合法。
- `../06_Decisions/ADR-045-DeferAutomatedTestsUntilAssemblyDefinitions.md`：当前不创建测试程序集或自动测试代码，首轮按测试清单执行编译、结构化日志、Inspector 与可复现手工验证，正式程序集阶段再迁移高价值自动化用例。
- `../06_Decisions/ADR-046-GameplayImplementationContractClosure.md`：收口首轮玩法实现契约，包括非负且允许 0 的配置 ID、void 槽位伤害命令、Collider 身份代理、Failed 对象锁血 1、敌人上一同步姿态近似阻挡以及生成诊断字段。
- `../06_Decisions/ADR-047-StagedCodeGenerationAndParallelOwnership.md`：定案分批代码生成、用户资源装配边界、唯一执行顺序、并行所有权和每批验证门。
- `../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md`：补齐批次 7 的正式序列帧目录、导入设置、动画资产矩阵、Prefab 预绑定、表现适配代码和验证边界。
- `../06_Decisions/ADR-049-ContinuousArmyCombatAnimationAndVictoryPresentation.md`：将 Army 改为代码显式选择的持续战斗动画，并把 Victory 表现与场景卸载时的最终清理分离。
- `../06_Decisions/ADR-007-WeaponIdentity.md`：定案使用 `WeaponId` 作为唯一武器身份。
- `../06_Decisions/ADR-035-ArmyConfigurationPrefabLoadoutAndRemoval.md`：定案 Army 配置快照与 Prefab 绑定、槽位容量、固定武器、三元素计时、子弹 ElementMask 和负数门最低 HP 减员。
- `../06_Decisions/ADR-008-ObstacleManager.md`：定案由 `ObstacleManager` 管理道路上的 Gate/Prop 实例。
- `../06_Decisions/ADR-005-MonsterCombatAndManager.md`：定案三类敌人的接近与攻击流程、碰撞判定和 EnemyManager 生命周期。
- `../06_Decisions/ADR-009-FixedRoadSingleLevelTimeline.md`：定案固定道路、无波次时间轴和 Gameplay 内终局判定；三路生成部分已由 ADR-023 替代，终局后的临时应用流程见 ADR-011。
- `../06_Decisions/ADR-011-ApplicationFlowAndGameplaySession.md`：定案应用级流程、单局状态、1 秒自动跳过和终局回到选关。
- `../06_Decisions/ADR-010-CanonicalRuntimeNames.md`：统一 ArmyController、LevelManager、EnemyManager 和 GameStateService 的运行时命名。
- `../06_Decisions/ADR-012-LevelCatalogConfigurationBootstrap.md`：定案 LevelCatalog、默认解锁和 LevelConfig 注入流程。
- `../06_Decisions/ADR-013-SpawnCursorOwnershipAndDispatch.md`：定案 SpawnManager 的时间轴游标所有权和调度边界。
- `../06_Decisions/ADR-014-SharedRuntimeContractBaseline.md`：定案跨模块服务、请求、事件和会话 ID 契约基线。
- `../06_Decisions/ADR-015-RoadmapStatusSemantics.md`：定案路线图复选框只表示工程交付，设计成熟度继续由模块状态和待决清单表达。
- `../06_Decisions/ADR-016-Collider2DCollisionQueries.md`：定案所有玩法碰撞对象使用 Collider2D 与显式查询；MVP 敌人只排队阻挡，侧向绕行延后。
- `../06_Decisions/ADR-017-DeferScoreAndSave.md`：原先延后得分、存档和设置持久化；关卡进度部分已由 ADR-062 取代，得分与设置仍延后。
- `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`：定案 Luban 的 MVP 最小字段、固定规则和 Unity 资源绑定边界。
- `../06_Decisions/ADR-019-ApplicationFlowContract.md`：定案应用流程公共命令、GameplayLoading 状态、场景就绪/卸载握手、会话 ID 所有权和流程定时器归属。
- `../06_Decisions/ADR-021-MvpRuntimeDeterminismAndBindings.md`：定案 ArmyId、MVP 时间倍率、同帧碰撞顺序、世界坐标、Army 移动速度、状态映射、EventBus 和资源键；其 Layer Collision Matrix 待决项已由 ADR-037 关闭。
- `../06_Decisions/ADR-022-PropBreakEffectBoundary.md`：定案 Prop 承载通用击破效果、当前 MVP 只实现武器替换，以及其他效果细节保持待决。
- `../06_Decisions/ADR-023-NormalizedSpawnPosition.md`：定案固定出生横线、所有生成项使用 `[0,1]` 归一化横向位置，以及出生坐标不考虑对象尺寸。
- `../06_Decisions/ADR-024-LayerDependencyDirection.md`：定案表现层、玩法层和全局基础层的依赖方向，并将存档和设置改为旁路扩展。
- `../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md`：定案常驻层与 Gameplay 单局层的场景层级，以及 Controller、Manager、Service、Root 和 Bootstrap 的职责命名。
- `../06_Decisions/ADR-026-AssemblyBoundariesAndCommunication.md`：定案后续粗粒度程序集目标、Composition 装配边界，以及同步接口、事实事件、依赖倒置和协调器的选择规则；工程实现延后。
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`：定案全局服务的应用级唯一实例与显式注入、Input 场景适配器边界、GameStateService 与 SceneService 分离及精简 TimeService；Save 延后部分已由 ADR-062 取代。
- `../06_Decisions/ADR-028-MvpRelativeDragInput.md`：定案相对拖动输入、UI 采集与同步命令边界、帧率/分辨率归一化、Inspector 灵敏度系数和清理规则；其中键盘/手柄汇总与回退已由 ADR-036 延后。
- `../06_Decisions/ADR-036-DragOnlyInputImplementationSlice.md`：将首个工程切片收窄为单一相对拖拽输入，补齐最小接收接口、代码结构、同步 Tick 顺序、Prefab 与场景绑定。
- `../06_Decisions/ADR-037-MonsterDistanceTargetingAndCollisionLayers.md`：移除 TargetSensor，以目标位置距离作为攻击起始权威；允许 Army 槽位与敌人身体重合，并定案六个 Gameplay Layer、显式查询关系和默认关闭的自动碰撞矩阵。
- `../06_Decisions/ADR-061-ElementComboImpactEffects.md`：定案三个精准二元素命中效果、显式同步结算、死亡目标过滤、冰火延后位移和最近元素记录。
- `../06_Decisions/ADR-029-EventBusImplementationAndPayloads.md`：定案 EventBus 实现、精确类型与嵌套分发、Token 身份、异常报告、事件载荷类型选择、订阅生命周期和 Monster 死亡事件边界。
- `../06_Decisions/ADR-030-TimeDomainStructure.md`：定案 MVP 时间域的唯一归属与消费者映射；当前不实现嵌套，未来时间控制采用单父级层次而非任意重叠。
- `../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`：定案具体类型到规范 Prefab 的唯一池身份、全局类型池所有权、Manager 初始化与归还顺序、未激活借出和防御性失活。
- `../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`：定案 MainMenu、LevelSelect、Gameplay 实际场景、固定根 SceneEntry、应用场景级握手、同步加载/异步卸载、服务三阶段初始化和首轮日志验收。
- `../06_Decisions/ADR-033-LevelManagerFramePipeline.md`：定案 LevelManager 持有 Gameplay 逻辑帧阶段顺序，启动事件不替代逐帧同步协调，并补齐 Manager 阶段与生命周期边界。
- `../06_Decisions/ADR-052-CenteredRoadAndConfigurableArmySpawn.md`：取代 ADR-034 的道路 Rect 与 Army 世界原点规则，定案原点居中道路尺寸、可配置 Army 出生坐标、三条 Y 线和无道路玩法 Collider 的空间契约。
