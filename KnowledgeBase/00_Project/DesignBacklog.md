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
| DES-003 | 加法门数字的命中增量、正负接触结果和一次性结算 | Accepted | Bullet、Gate、Army、DataDictionary | 见 ADR-006；当前数字使用 `ArmyCount + GateValue` |
| DES-004 | 怪物到达底部的扣人数方式：固定伤害、按怪物类型，还是直接结束 | Accepted | Monster、Army、Level | 首版移除到底部流程，见 ADR-005 |
| DES-011 | 道路接近线由关卡空间配置还是由 Army 位置动态计算 | Accepted | Level、Monster、Spawn | 见 ADR-009；由 LevelConfig 提供固定 `enemyApproachY` |
| DES-005 | 总人数上限、最大上场槽位和显示规则 | Accepted | Army、UI、Config | 见 ADR-004、ADR-020；`ArmyCountLimit = 0` 表示不设上限，最大上场槽位仍独立限制表现对象数 |
| DES-006 | MVP 的关卡数量、生成时间轴长度和胜利条件 | Accepted | Level、Spawn、UI | 见 ADR-009；单关卡、无波次，全部敌人生成且击杀后胜利 |
| DES-007 | 暂停、减速和局部时停是否属于首个可玩版本 | Accepted（MVP 延后） | Time、UI、所有玩法模块 | 见 ADR-001、ADR-021、ADR-027；MVP 所有时间倍率固定为 `1`，当前接口不暴露倍率修改或暂停能力 |
| DES-008 | 槽位聚合生命值和比例受击换算 | Accepted | Army、Monster、Bullet、UI | 见 ADR-004；确定 `HpPerSoldier` 数值 |
| DES-009 | Weapon/Element 组合及代表人数对发射参数的缩放 | Accepted | Army、Bullet、Config | 见 ADR-020；MVP 不按代表人数缩放射速、伤害或数量，每个激活槽位独立发射一枚子弹 |
| DES-010 | 受击后槽位缺口的补充优先级 | Accepted | Army、Gate、UI | 见 ADR-004；增加分配和回归测试 |
| DES-012 | 门/道具接触失败后，后续子弹击破是否仍发放成功奖励 | Accepted | Gate、Prop、Bullet、Army | 见 ADR-006；失败状态锁定奖励，后续击破不发放元素或武器效果 |
| DES-013 | 武器身份使用 `WeaponId`，不维护第二份 `WeaponType` 身份 | Accepted | Army、Prop、Bullet、Config | 见 ADR-007；不同参数武器使用不同 ID |
| DES-014 | Gate/Prop 的道路实例由统一 ObstacleManager 管理 | Accepted | Obstacle、Gate、Prop、Spawn、Scene | 见 ADR-008；补充运行时实例查询和回收测试 |
| DES-015 | 初始化、主界面、选关与游玩会话的流程边界 | Accepted | GlobalServices、Level、Scene、Config、UI | 见 ADR-011、ADR-019；主界面和选关暂时各等待 1 秒，终局回到选关并重新开始同一关 |
| DES-016 | LevelCatalog、LevelConfig 与配置初始化/注入边界 | Accepted | Config、GlobalServices、Scene、Level | 见 ADR-012；初始化加载目录，选关后按 LevelId 注入 Gameplay |
| DES-017 | 三类生成时间轴的游标所有权和调度接口 | Accepted | Level、Spawn、Monster、Obstacle | 见 ADR-013；游标只由 SpawnManager 持有，LevelManager 只传入时间并查询结果 |
| DES-018 | Config、Scene、Spawn、Manager、EventBus、Time 和 Pool 公共契约基线 | Accepted | 架构、全部 Gameplay 模块 | 见 ADR-014；Pool 的原始 `GameObject + string key` 契约已由 ADR-031 修订为具体组件类型池 |
| DES-019 | 路线图勾选与设计成熟度的状态边界 | Accepted | 项目管理、全部模块 | 见 ADR-015；路线图复选框只表示工程实现和验证完成 |
| DES-020 | 玩法碰撞形状、查询方式与敌人阻挡/绕行范围 | Accepted | Army、Bullet、Gate、Prop、Monster、Level | 见 ADR-016；所有玩法碰撞对象使用 Collider2D 与显式 Cast/Overlap，MVP 只实现敌人不重叠和排队阻挡，侧向绕行延后 |
| DES-021 | 得分、存档和设置持久化是否进入当前 MVP | Accepted | 项目范围、事件、配置、全局服务、测试 | 见 ADR-017；当前不实现、不进入事件和验收，作为后续扩展 |
| DES-022 | Luban MVP 初始字段和 Unity 资源绑定边界 | Accepted | Config、Army、Monster、Gate、Prop、Bullet、资源 | 见 ADR-020；只保留当前玩法数值和稳定 ID，Prefab 等资源由 Unity 侧绑定 |
| DES-023 | MVP Army 身份与时间倍率范围 | Accepted | Army、Time、事件、全部 Gameplay | 见 ADR-021、ADR-027；`ArmyId` 固定为 `1`，MVP 所有时间倍率固定为 `1`，倍率和暂停接口不进入当前契约 |
| DES-024 | 同帧碰撞阶段与同距离命中优先级 | Accepted | Bullet、Gate、Prop、Monster、Level、碰撞契约 | 见 ADR-021；移动阻挡→子弹→Gate/Prop→敌人攻击→终局，同距离按 Enemy > Gate > Prop > RuntimeInstanceId |
| DES-025 | 道路世界坐标系 | Accepted | Level、Army、Spawn、Monster、Gate、Prop | 见 ADR-021、ADR-023；世界 XY 平面、z=0、右为 +x、上为 +y，出生位置在固定 `SpawnY` 上按 `[0,1]` 横向映射 |
| DES-026 | Army 横向移动速度来源 | Accepted | Army、Input、Config | 见 ADR-021、ADR-028；`TbArmy.MoveSpeed` 提供基础速度，键盘/手柄输入限制到 `[-1,1]`，触屏原始滑动速度限制到 `[-1,1]` 后再乘 Inspector 系数作为最终输入倍率 |
| DES-027 | Gate/Prop 统一道路状态快照映射 | Accepted | Gate、Prop、Obstacle、UI、调试 | 见 ADR-021；专用接触状态保留，快照使用统一 `ObstacleState` |
| DES-028 | EventBus 分发语义 | Accepted | EventBus、全部事件消费者、测试 | 见 ADR-021；同步、注册顺序、异常隔离、独立 Token、取消幂等 |
| DES-029 | Unity 资源注册表键命名 | Accepted | Config、资源、Spawn、Army、Monster、Gate、Prop、Bullet | 见 ADR-021、ADR-031；使用大小写敏感的 `类别/身份` 键维护非池身份资源，PoolService 不以资源键选择 Prefab |
| DES-030 | Layer Collision Matrix 最终关系 | InDesign | Bullet、Army、Gate、Prop、Monster、Project Settings | 先按 `CollisionRules.md` 评审显式查询目标和是否启用物理接触，再单独定案允许/禁止矩阵 |
| DES-031 | 道具击破效果目录、单个/组合方式、目标与叠加规则 | InDesign | Prop、Army、Config、事件、UI、测试 | 当前 MVP 保留三种武器箱；实现其他效果前确认效果模型、配置结构、同步命令和事实事件载荷，见 ADR-022 |
| DES-032 | 生成对象的横向出生位置表达 | Accepted | Level、Spawn、Monster、Gate、Prop、Config | 见 ADR-023；移除三路生成点 ID，所有生成项改用 `[0,1]` 的 `spawnPosition` |
| DES-033 | 程序集分层与跨层通信方式 | Accepted（实现延后） | 架构、全局服务、全部 Gameplay、UI、AudioVFX | 见 ADR-024、ADR-026；当前不创建 `.asmdef`，后续以粗粒度程序集强制单向依赖，同步接口用于必须执行的操作，事件只传递已发生的事实 |
| DES-034 | MVP 全局服务范围、访问方式与应用流程服务边界 | Accepted | 全局服务、Input、Time、AudioVFX、测试 | 见 ADR-027；服务由 Composition 以应用级唯一实例持有并显式注入，不普遍使用静态单例；Input 为场景适配器；Audio/Save/Debug 延后，GameState 与 Scene 保持分离，TimeService 使用精简接口 |
| DES-035 | MVP 触屏横向移动的采集、归一化和模块通信 | Accepted | Input、Army、UI、测试 | 见 ADR-028；在 Gameplay UI 区域内读取相邻采样点的水平差，按区域宽度和未缩放帧时间归一化，原始滑动速度先限制到 `[-1,1]` 再乘 Inspector 系数，通过同步接口传给 Army，停手即归零 |
| DES-036 | EventBus 实现、载荷类型和订阅生命周期 | Accepted | EventBus、Composition、全部事件发布者与监听者、测试 | 见 ADR-029；主线程同步精确类型分发、允许嵌套发布、Token 关联 Bus 身份、按领域组织事件，小型载荷用 `readonly struct`，大型快照用不可变 `sealed class` |
| DES-037 | 时间域的当前归属、父子包含与重叠规则 | Accepted（实现延后） | Time、全部 Gameplay、VFX、UI、测试 | 见 ADR-030；MVP 保持扁平枚举且每次操作只选一个最具体域，未来时间控制采用单父级层次，不允许任意重叠归属 |
| DES-038 | 类型对象池身份、激活顺序与归还语义 | Accepted | Pool、Composition、Monster、Obstacle、Bullet、VFX、测试 | 见 ADR-031；一个具体池化根类型对应一个规范 Prefab 和一个类型池，Manager 负责 Transform、初始化与主动激活/失活，类型池借出未激活实例并在归还时防御性失活 |
| DES-039 | MainMenu/LevelSelect 实际场景、固定 SceneEntry、场景切换与服务分阶段初始化 | Accepted | 应用流程、场景、Composition、全局服务、事件、测试 | 见 ADR-032；三个稳定页面使用实际 Additive 场景和固定根入口，GameState 只命令 SceneService，加载同步、卸载异步，服务按 Create/Connect/Start 装配 |

## 已接受决策

- `../06_Decisions/ADR-001-TimeSystem.md`：采用自定义时间域；MVP 所有时间倍率固定为 `1`，暂停、减速、加速和局部时停延后，当前接口范围见 ADR-027。
- `../06_Decisions/ADR-002-GateCalculation.md`：历史上的乘法门方案，已由 ADR-006 替代。
- `../06_Decisions/ADR-006-AdditiveGateAndContactResolution.md`：定案加法门、元素门、一次接触结算和未接触离场。
- `../06_Decisions/ADR-007-WeaponIdentity.md`：定案使用 `WeaponId` 作为唯一武器身份。
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
- `../06_Decisions/ADR-017-DeferScoreAndSave.md`：定案当前 MVP 不实现得分、存档和设置持久化，相关条目延后。
- `../06_Decisions/ADR-020-MinimalMvpConfigurationSurface.md`：定案 Luban 的 MVP 最小字段、固定规则和 Unity 资源绑定边界。
- `../06_Decisions/ADR-019-ApplicationFlowContract.md`：定案应用流程公共命令、GameplayLoading 状态、场景就绪/卸载握手、会话 ID 所有权和流程定时器归属。
- `../06_Decisions/ADR-021-MvpRuntimeDeterminismAndBindings.md`：定案 ArmyId、MVP 时间倍率、同帧碰撞顺序、世界坐标、Army 移动速度、状态映射、EventBus 和资源键；Layer Collision Matrix 保留待评审。
- `../06_Decisions/ADR-022-PropBreakEffectBoundary.md`：定案 Prop 承载通用击破效果、当前 MVP 只实现武器替换，以及其他效果细节保持待决。
- `../06_Decisions/ADR-023-NormalizedSpawnPosition.md`：定案固定出生横线、所有生成项使用 `[0,1]` 归一化横向位置，以及出生坐标不考虑对象尺寸。
- `../06_Decisions/ADR-024-LayerDependencyDirection.md`：定案表现层、玩法层和全局基础层的依赖方向，并将存档和设置改为旁路扩展。
- `../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md`：定案常驻层与 Gameplay 单局层的场景层级，以及 Controller、Manager、Service、Root 和 Bootstrap 的职责命名。
- `../06_Decisions/ADR-026-AssemblyBoundariesAndCommunication.md`：定案后续粗粒度程序集目标、Composition 装配边界，以及同步接口、事实事件、依赖倒置和协调器的选择规则；工程实现延后。
- `../06_Decisions/ADR-027-MvpGlobalServiceScope.md`：定案全局服务的应用级唯一实例与显式注入、Input 场景适配器边界、MVP 完全无声音、Audio/Save/Debug 服务延后、GameStateService 与 SceneService 分离，以及 TimeService 使用精简公共接口。
- `../06_Decisions/ADR-028-MvpRelativeDragInput.md`：定案触屏相对拖动输入、UI 采集与同步命令边界、帧率/分辨率归一化、Inspector 灵敏度系数、输入源优先级和清理规则。
- `../06_Decisions/ADR-029-EventBusImplementationAndPayloads.md`：定案 EventBus 实现、精确类型与嵌套分发、Token 身份、异常报告、事件载荷类型选择、订阅生命周期和 Monster 死亡事件边界。
- `../06_Decisions/ADR-030-TimeDomainStructure.md`：定案 MVP 时间域的唯一归属与消费者映射；当前不实现嵌套，未来时间控制采用单父级层次而非任意重叠。
- `../06_Decisions/ADR-031-TypedComponentPoolsAndDefensiveDeactivation.md`：定案具体类型到规范 Prefab 的唯一池身份、全局类型池所有权、Manager 初始化与归还顺序、未激活借出和防御性失活。
- `../06_Decisions/ADR-032-AppScenesEntriesAndStagedInitialization.md`：定案 MainMenu、LevelSelect、Gameplay 实际场景、固定根 SceneEntry、应用场景级握手、同步加载/异步卸载、服务三阶段初始化和首轮日志验收。
