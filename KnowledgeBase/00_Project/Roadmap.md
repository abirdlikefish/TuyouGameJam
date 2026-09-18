# 开发路线图

> 本文复选框只表示工程实现和相应验证已经完成，不表示 ADR 是否已接受或设计是否已收敛。当前仍处于文档优先阶段，尚未获得工程实现授权，因此所有工程条目保持未勾选。设计成熟度以模块状态表和 `DesignBacklog.md` 为准，见 ADR-015。

> 当前 MVP 明确不考虑得分、声音、AudioService、SaveService、DebugService、本地进度存档、设置持久化、暂停、减速和局部时停；阶段 4 中相关条目仅表示未来扩展（Deferred），不属于当前验收范围。

## 阶段 1：核心闭环

- [ ] GlobalBootstrap / Composition Root 与唯一 GlobalRoot
- [ ] GameStateService、TimeService、EventBus、SceneService、ConfigService，以及按具体根组件类型持有的 PoolService
- [ ] 服务按 Create、Connect、Start 三阶段装配，全部连接和应用级订阅完成后才启动流程
- [ ] Bootstrap、MainMenu、LevelSelect、Gameplay 四个场景及固定根 SceneEntry；同步 Additive 加载、异步卸载和结构化日志切换验证
- [ ] 军队人数和自动射击
- [ ] 军队固定槽位、聚合生命值和横向移动
- [ ] Gameplay 场景键盘/手柄横向输入，以及 UI 区域内停手即停的触屏相对拖动输入
- [ ] 子弹命中怪物、加法门、元素门和道具
- [ ] 门/道具接触 Army 并执行一次性结算
- [ ] ObstacleManager 登记、查询和回收道路上的门与道具
- [ ] 怪物生成、接近军队、攻击、死亡与胜负判断
- [ ] 所有玩法碰撞对象配置 Collider2D、职责 Layer 和显式 Cast/Overlap 查询
- [ ] 存活敌人身体不重叠，后方敌人被较慢或静止的前方敌人阻挡并排队
- [ ] 固定道路宽高、出生横线、归一化横向出生位置、Army 左右边界和敌人接近线

## 阶段 2：关卡化

- [ ] 无波次的三类时间轴生成控制
- [ ] `LevelConfig` ScriptableObject 配置单关卡道路、生成编排和解锁 ID
- [ ] Luban 配置角色/军队、敌人、Gate、Prop 和子弹属性
- [ ] Luban 配置 Weapon、Element、阵型槽位和每名士兵生命值
- [ ] 当前临时流程：MainMenuScene 与 LevelSelectScene 各在 Entry Ready 后等待 1 秒；胜利/失败后清理 Gameplay 并切换回 LevelSelectScene，再开始当前关卡

## 阶段 3：表现和性能

- [ ] UI 完整反馈
- [ ] 粒子和命中特效
- [ ] 在目标设备实测子弹、怪物和特效类型池，并按数据决定是否增加预热、容量上限或溢出策略
- [ ] 多分辨率和移动设备验证

## 阶段 4：可选扩展

- [ ] `+N`、`-N`、`÷N` 门
- [ ] 多种怪物和子弹
- [ ] 武器替换之外的道具击破效果及组合规则
- Deferred：声音、AudioService、音量设置和音频资源
- Deferred：DebugService 和通用调试指令
- Deferred：暂停、减速、加速和局部时停
- Deferred：本地进度存档
- [ ] 升级和关卡解锁
- [ ] 后方敌人从侧面绕过较慢、静止或局部时停的前方敌人

## MVP 后工程化要求

- Deferred：在公共契约和脚本目录稳定后，按 ADR-026 将 Contracts、Foundation、Gameplay、Presentation 和 Composition 划分为粗粒度程序集，并验证单向引用、依赖倒置、Unity 序列化引用与完整游玩闭环。当前不创建 `.asmdef`。
