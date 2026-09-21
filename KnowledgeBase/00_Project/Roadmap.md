# 开发路线图

> 本文复选框只表示工程实现和相应验证已经完成，不表示 ADR 是否已接受或设计是否已收敛。用户已授权 AI 按 `ImplementationPlan.md` 分批创建代码与目录；Scene、Prefab、表格、配置资产实例和 ProjectSettings 默认仍由用户手工完成。设计成熟度以模块状态表和 `DesignBacklog.md` 为准，见 ADR-015、ADR-047。

> 当前 MVP 明确不考虑得分、声音、AudioService、DebugService、云存档、多存档槽、设置持久化、暂停、减速和局部时停。ADR-062 已把最小本地关卡进度存档纳入当前范围。

## 实施顺序

工程按 `Contracts → EventBus/Time/Pool → Config → 可并行 Gameplay 模块 → Spawn/Level → Application/Composition → 用户资源装配 → 集成验收` 推进。每批先通过 Unity 编译和范围检查再进入下一批；详细目录、并行所有权、人工检查点和交付格式见 [MVP 代码生成实施计划](ImplementationPlan.md)。

下列“阶段”只按交付能力和验收范围分组，不表示代码生成的先后顺序。同一阶段可以由多个实施批次共同完成，同一实施批次也可以推进多个阶段的条目；发生顺序疑问时，以 `ImplementationPlan.md` 的“生成批次与依赖顺序”为准。复选框只在对应能力实际实现并完成要求的验证后勾选。

## 阶段 1：核心闭环

- [x] GlobalBootstrap / Composition Root 与唯一 GlobalRoot
- [x] GameStateService、TimeService、EventBus、SceneService、ConfigService，以及按具体根组件类型持有的 PoolService
- [x] 服务按 Create、Connect、Start 三阶段装配，全部连接和应用级订阅完成后才启动流程
- [x] Bootstrap、MainMenu、LevelSelect、Gameplay 四个场景及固定根 SceneEntry；异步 Additive 加载/卸载和结构化日志切换验证
- [ ] 军队人数和自动射击
- [ ] 军队固定槽位、聚合生命值和横向移动
- [ ] Gameplay 场景 UI 区域内停手即停的相对拖动输入；设备触屏与 Editor 左键共用 Pointer 路径，键盘/手柄延后
- [ ] 子弹命中怪物、加法门、元素门和道具
- [ ] 门/道具接触 Army 并执行一次性结算
- [ ] ObstacleManager 登记、查询和回收道路上的门与道具
- [ ] 怪物生成、接近军队、攻击、死亡与胜负判断
- [ ] 所有玩法碰撞对象配置 Collider2D 与职责 Layer；子弹/敌人阻挡使用 Cast，范围攻击及 Gate/Prop 终点接触使用 Overlap
- [ ] 敌人 BodyCollider 基于上一同步姿态 Cast；在 MVP 速度、尺寸和帧率下减少穿透并形成排队，不验收同帧绝对不重叠
- [ ] 原点居中的 `roadWidth`/`roadHeight`、无道路玩法 Collider、可配置 Army 出生坐标、出生/接近/离场线和归一化横向出生位置
- [ ] LevelManager 同步驱动生成、移动、子弹、道路接触、敌人攻击、回收和终局的固定帧阶段
- [ ] 按 ADR-048 导入 Army、Monster、Bullet 和 Gate 序列帧并完成 Animator/Prefab 预绑定；Gate 保留单个 TMP 调试文本；HUD/胜负面板功能后续已由 ADR-058 追加，VFX 仍延后

## 阶段 2：关卡化

- [ ] 无波次的三类时间轴生成控制
- [ ] `LevelConfig` ScriptableObject 配置单关卡道路、生成编排和解锁 ID，ConfigService 校验后生成供 Gameplay 使用的不可变 `LevelConfigSnapshot`
- [ ] Luban 配置角色/军队、敌人、Prop 和子弹属性；Gate 不读表，由 LevelConfig 生成项与对应 Prefab 提供配置
- [ ] Luban 配置 Army 基础数值、固定 Weapon 0/1/2 和每名士兵生命值；当前不建立 TbElement，阵型槽位由 Army Prefab 序列化绑定
- [ ] 当前应用流程：MainMenuScene 等待开始按钮；LevelSelectScene 初始化 Inspector 显式绑定的预放关卡节点并等待选择；胜利/失败后停留结算，支持返回选关、失败重试和有有效解锁目标时挑战下一关
- [ ] 移动端本地关卡进度：胜利保存完成/解锁集合，重启恢复，损坏文件回退；代码与 Editor 往返测试已通过，目标设备完整流程待验收

## 阶段 3：表现和性能

- [ ] 三套 WeaponId Army 动画、三类 Monster Move/Attack/Death、三个 BulletId 循环动画及四类 Gate 循环动画完成目标平台内存与多实例播放验证
- [ ] HUD、胜负面板最终样式与 UI 完整反馈（功能性 HUD、退出确认和结算流程已按 ADR-058 实现）
- [ ] 粒子和命中特效
- [ ] 在目标设备实测子弹、怪物和特效类型池，并按数据决定是否增加预热、容量上限或溢出策略
- [ ] 多分辨率和移动设备验证

## 阶段 4：可选扩展

- [ ] 扩展 `+N`、`-N` 加法门的关卡编排与表现；当前及可选扩展范围均不恢复乘法门
- [ ] 多种怪物和子弹
- [ ] 武器替换之外的道具击破效果及组合规则
- Deferred：声音、AudioService、音量设置和音频资源
- Deferred：DebugService 和通用调试指令
- Deferred：暂停、减速、加速和局部时停
- Deferred：云存档、多存档槽、跨卸载恢复和设置持久化
- [ ] 升级和关卡解锁
- [ ] 后方敌人从侧面绕过较慢、静止或局部时停的前方敌人

## MVP 后工程化要求

- Deferred：在公共契约和脚本目录稳定后，按 ADR-026 将 Contracts、Foundation、Gameplay、Presentation 和 Composition 划分为粗粒度程序集，并验证单向引用、依赖倒置、Unity 序列化引用与完整游玩闭环。当前不创建任何 `.asmdef`，包括测试程序集。
- Deferred：正式程序集落地时创建 EditMode 测试程序集，并按收益决定 PlayMode 测试程序集；优先自动化 Army、Gate/Prop、Spawn、终局优先级、EventBus、Pool 和 Config 的确定性规则。当前先执行测试清单、编译检查和完整手工游玩闭环，见 ADR-045。
