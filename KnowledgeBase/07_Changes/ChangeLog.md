# 变更记录

| 日期 | 变更 | 影响模块 | 记录人 |
|---|---|---|---|
| 2026-09-13 | 创建知识库目录、公共契约和模块初稿 | 全部 | Codex |
| 2026-09-13 | 增加文档优先流程、并行模块协作规则、文档索引和设计待决清单 | 项目规范、全部文档 | Codex |
| 2026-09-13 | 定案关卡使用 LevelConfig ScriptableObject，角色/军队、敌人、倍增门、子弹和全局数值使用 Luban；新增配置系统与表契约文档 | 架构、Army、Monster、Gate、Bullet、Level、Spawn、测试 | Codex |
| 2026-09-13 | 定案军队总人数与上场槽位分离；新增槽位聚合 HP、比例受击、受击缺口优先补充、ArmyRoot 移动边界及 Weapon/Element 组合契约 | Army、Input、Bullet、Gate、Monster、UI、配置、测试 | Codex |
| 2026-09-14 | 定案三类敌人的接近与攻击流程、单体/范围攻击碰撞判定、EnemyManager 生成与存活统计、子弹伤害上下文及受击/击杀事件；移除首版到底部流程 | Monster、Army、Bullet、Spawn、Level、AudioVFX、共享契约、路线图 | Codex |
| 2026-09-14 | 定案加法门与元素门的一次接触结算、WeaponId 唯一武器身份，以及由 ObstacleManager 统一管理 Gate/Prop 的登记、查询和回收；新增 Prop 与 Obstacle 模块文档和规则测试清单 | Gate、Prop、Obstacle、Army、Bullet、Spawn、共享契约、架构、测试、资源 | Codex |
| 2026-09-14 | 定案门或道具接触失败后，后续子弹击破不再发放元素或武器奖励；失败状态锁定接触奖励并补充规则测试 | Gate、Prop、Bullet、Army、测试、设计待决 | Codex |
| 2026-09-14 | 定案固定道路宽高、左中右三路仅影响初始生成、无波次时间轴、敌人接近线、同帧 Army 归零失败优先，以及终局固定 3 秒重开；新增 ADR-009 并同步 Level/Spawn/Monster/Army/Input/UI 与共享契约 | 项目目标、Level、Spawn、Monster、Army、Input、UI、配置、事件、测试 | Codex |
| 2026-09-14 | 新增 ADR-010，统一 ArmyController、LevelManager、EnemyManager 和 GameStateService 的运行时命名，并清理事件、场景和怪物文档中的旧别名 | 架构、场景、Monster、Level、共享契约、命名规则 | Codex |
| 2026-09-14 | 新增 ADR-011，定案初始化→主界面→选关→游玩→回选关的应用流程；主界面和选关各等待 1 秒，终局不再在游玩场景内 3 秒直接重开；同步配置选择、会话清理、事件、接口和集成测试 | 项目流程、架构、配置、Level、场景、共享契约、测试 | Codex |
| 2026-09-14 | 新增 ADR-012/013/014，定案 LevelCatalog 配置目录与关卡注入、SpawnManager 时间轴游标所有权，以及 Config/Scene/Spawn/Manager/EventBus/Time/Pool 公共契约基线；明确由 ConfigService 唯一解析配置、SceneService 只加载并传递，统一 `StartRun` 会话入口，补齐接口归属、失败 payload 和错误码；同步配置、模块、共享契约和集成测试 | 配置、Level、Spawn、架构、共享契约、测试 | Codex |
| 2026-09-14 | 修复明确的中优先级一致性问题：Gate/Prop 使用同步接口修改 Army 后再发布事实事件；空敌人时间轴在加载期判错；补充 Gate/Prop 条件字段校验、加法/元素门命名、精英/Boss 资源、正确文档路径和路线图状态语义；新增 ADR-015 | Gate、Prop、Army、Level、配置、事件、测试、资源、路线图 | Codex |
| 2026-09-14 | 新增 ADR-017，明确当前 MVP 不实现得分、SaveService、本地进度存档和设置持久化；移除相关事件消费者与得分字段，保留为 Deferred 后续扩展 | 项目范围、全局服务、事件、配置、路线图、测试 | Codex |
| 2026-09-14 | 新增 ADR-016，定案所有玩法碰撞对象（包括子弹）使用 Collider2D 与显式 Cast/Overlap 查询；MVP 敌人身体不重叠并排队阻挡，侧向绕行列为后续非必要需求；当前暂不以移动 Collider2D 的性能成本阻塞设计 | Army、Bullet、Gate、Prop、Monster、Level、架构、共享契约、测试、路线图 | Codex |
| 2026-09-14 | 同步 DES-007 与 ADR-001 的 MVP 范围，明确支持暂停、减速、加速和局部时停；修正配置系统关联文档的相对路径 | Time、配置、项目索引 | Codex |
| 2026-09-14 | 新增 ADR-020，收敛 Luban MVP 初始字段：移除 Prefab/阵型资源键、重复身份、人数缩放、多弹道和可配置碰撞行为；固定初始人数与首版派生规则，明确 Unity 资源由 Inspector/资源注册表绑定，并同步模块、事件、测试和资源文档 | 配置、Army、Monster、Gate、Prop、Bullet、共享契约、测试、资源、设计待决 | Codex |
| 2026-09-14 | 新增 ADR-019，补齐应用流程公共命令、GameplayLoading 状态、场景就绪/卸载握手、会话 ID 所有权和流程定时器归属；同步公共接口、事件、架构、Level 和集成测试 | 应用流程、场景、配置、Level、共享契约、测试 | Codex |
| 2026-09-14 | 按 ADR-014 补齐 Gameplay 事件的 `LevelRunId` 和运行时实例 ID，统一 ObstacleManager 的离场/回收事实发布者；同步时间系统表述、模块依赖表和 ADR 关联文档路径 | 事件、Obstacle、Time、模块索引、ADR | Codex |
| 2026-09-14 | 修正 ADR-014 的事件实例 ID 表述，补齐 `RoadLayoutSnapshot.DespawnY` 及数据字典/Level 验收项，清理 Deferred 路线图复选框和 Bug 占位行 | 共享契约、Level、路线图、测试、变更记录 | Codex |
| 2026-09-14 | 同步明确的职责与字段文档：将 PoolService 标为当前必需服务，修正 SceneService/LevelManager 的旧重开职责表述，补齐道路快照的 SpawnY、LaneSpawnPoints、ConfigId、WorldPosition、IsOnRoad 与槽位 SlotIndex 字段，统一事件中的 LevelId/LevelRunId 字段名，收窄全局配置来源表述，并更新性能验收场景 | 全局服务、运行时命名、架构、共享契约、测试 | Codex |
| 2026-09-14 | 新增 ADR-021，定案 MVP ArmyId=1、时间倍率固定为 1、同帧碰撞与同距离命中顺序、世界坐标约束、Army MoveSpeed 配置、Gate/Prop 状态映射、EventBus 语义和资源键命名；Layer Collision Matrix 保留为单独待评审项 | Army、Time、Level、碰撞、EventBus、Config、资源、测试 | Codex |
