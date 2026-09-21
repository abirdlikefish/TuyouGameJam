# 项目概述

> 当前已进入按 `ImplementationPlan.md` 分批生成代码的工程实现阶段。以下 MVP 规则已按 ADR-009、ADR-023、ADR-046 等决策收敛为实现基线；路线图未勾选的条目仍不代表已经存在或验证了对应 Unity 实现。

## 项目定位

《倍增门》是一款 2D 竖屏、单机、无付费系统的道路防守小游戏。当前已确认的数字门只有加法门，不恢复乘法门。玩家控制道路下方的军队横向移动并自动向上射击，在持续下落的怪物、门和道具之间选择目标与接触路线：加法门直接改变军队规模，元素门要求先削减 HP 才能安全取得元素，未及时击破的元素门或道具会造成槽位伤害。道具被击破后触发自身配置的击破效果；替换武器只是当前 MVP 已确认的一种效果，不是道具系统的永久边界。

## 核心交互对象

### 加法门

- 门上显示可正、可零、可负的整数 `GateValue`。每次有效子弹命中后，数字增加本次子弹的实际伤害值；不存在按命中次数固定递增的 `HitIncrement`。
- Army 接触时只结算一次：数字大于等于 `0` 时请求增加对应人数并由 ArmyCountLimit 限制；数字小于 `0` 时把绝对值作为请求减员人数交给 Army。
- 负数门接触记为失败。Army 将请求减员乘以 HpPerSoldier 得到伤害预算，优先由当前 HP 最少的激活槽位承担；请求减员和实际人数损失可能不同。加法门没有 HP，也不走元素门的逐接触槽位伤害规则。
- 正数、零和负数门都在接触结算后回收；未接触的门可以直接从道路下方离场。

### 元素门

- 元素门拥有运行时 HP；每个生成项在 `LevelConfig` 中分别配置 `ElementType` 与 `MaxHp`。子弹先扣除剩余 HP，只有 HP 归零后的额外伤害记入 `PostDepletionDamage`。
- HP 归零但尚未接触的元素门仍是合法子弹目标；命中它的子弹正常消耗，全部伤害继续累计到 `PostDepletionDamage`。
- Army 接触 HP 已清空的元素门时结算成功：增加的持续时间为 `PostDepletionDamage × LevelConfig.elementDurationSecondsPerDamage`。当前不设置持续时间上限；同类型持续时间累加，另外两种元素和当前武器保持不变。若额外伤害为 `0`，接触仍成功，但不调用持续时间增加命令，也不发布持续时间变化事件。
- Army 接触时仍有 HP，则接触失败：对本次接触到的每个有效槽位造成相同的配置伤害，然后继续向下移动。
- 失败状态会永久锁定元素奖励。此后仍可被子弹命中并消费子弹，但 HP 最低锁在 `1`，不能归零，也不能再累计可兑换的 `PostDepletionDamage` 或取得元素；对象继续移动至离场。

### 道具

- 道具拥有运行时 HP。处于待接触状态时被子弹击破，会立即触发一次配置的击破效果并进入回收流程。
- 当前 MVP 的具体道具是弹弓箱、弓箭箱和法杖箱；它们的击破效果是把 Army 的当前 WeaponId 替换为 `0 = Slingshot`、`1 = Bow`、`2 = Staff` 中的对应配置，同时保留三种元素剩余时间。
- Prop 的概念不限定为武器箱。后续可以增加其他击破效果，但效果目录、单个道具是否允许组合效果、目标与叠加规则仍需单独定案，不属于当前 MVP 的已确认契约。
- 道具未被击破就接触 Army 时，对每个接触槽位造成相同伤害并进入失败状态。失败后仍可被子弹命中，但 HP 最低锁在 `1`，不会被击破、发放效果或因伤害回收，只继续移动至离场。

两类门和道具都只消费一次接触结果；多个槽位或多帧碰撞不能重复结算。详细状态机见 [Gate 模块](../02_Modules/Gate/README.md)、[Prop 模块](../02_Modules/Prop/README.md)、[ADR-006](../06_Decisions/ADR-006-AdditiveGateAndContactResolution.md)、[ADR-022](../06_Decisions/ADR-022-PropBreakEffectBoundary.md) 和 [ADR-038](../06_Decisions/ADR-038-LevelConfiguredDamageDrivenGates.md)。

## 核心玩法循环

```text
开始关卡 → 敌人、门和道具按时间轴在固定出生横线上出现
→ 玩家横向移动 Army 并持续自动射击
→ 子弹伤害敌人、按伤害增加加法门数字，或削减元素门/道具 HP；元素门 HP 归零后继续累计可兑换的额外伤害
→ 玩家选择避让或接触道路对象 → Army 的人数、元素、武器或其他玩法状态改变
→ 怪物接近并攻击 Army → 继续移动、射击和选择目标
→ 全部敌人生成并被消灭时胜利 / Army 归零时失败
```

上述循环描述一局内反复发生的核心玩法，不包含页面跳转和临时自动等待。

## 当前临时应用流程

```text
初始化 → 加载 MainMenuScene 并等待玩家点击开始 → 切换 LevelSelectScene 并等待玩家选择关卡
→ 切换 GameplayScene → 胜利或失败 → 停留并显示对应结算 UI
→ 返回选关，或以新的 LevelRunId 重试当前关/挑战下一关
```

MainMenu、LevelSelect 和 Gameplay 当前都使用实际 Additive 场景和固定根 SceneEntry。MainMenu 已接入开始与退出按钮；LevelSelect 初始化 Inspector 中与 LevelId 一一绑定的预放节点，按运行期完成/解锁状态显示并等待玩家选择，节点位置可由 RectTransform 自由编排。页面交互不是游戏的核心循环。

## MVP 基线

- 首版当前资源只包含一个关卡和一个 `LevelConfig` ScriptableObject；`unlockedLevelIds` 记录通关后应解锁的关卡 ID。空列表表示成功后没有下一关；非空列表首项作为结算页“挑战下一关”的目标，其余项仍正常解锁。重复 ID 或自引用是致命配置错误，当前目录中尚不存在的未来关卡 ID 只记录 `Debug.LogWarning` 并从运行时快照中过滤。
- Gameplay 当前只实现指定 UI 区域内的相对横向拖动；设备触屏与 Editor 左键共用 UGUI Pointer 路径，键盘/手柄延后。拖拽只消费相邻采样点的水平差，手指或鼠标停止移动时输入立即归零；原始归一化滑动速度先限制到 `[-1,1]`，再乘 `PF_UI_TouchDragArea` Inspector 中默认值为 `1` 的灵敏度系数。
- 道路固定以世界原点为中心，LevelConfig 只配置 `roadWidth` 与 `roadHeight`，启动时复制到不可变 LevelConfigSnapshot，四边由半宽和半高派生；道路不设置玩法 Collider。ArmyRoot 每局从 `armySpawnPosition` 开始，共用出生横线 `spawnY` 由关卡道路配置提供。
- 每条敌人、Gate、Prop 生成项都配置 `[0, 1]` 范围内的 `spawnPosition`：`0` 对应道路最左边，`1` 对应道路最右边，中间值线性映射为世界坐标 `x`。
- 出生坐标以对象中心点计算，不考虑敌人、门或道具的尺寸；`spawnPosition` 只决定初始位置。敌人随后先垂直向下移动，到达关卡接近线后再向最近的有效士兵槽位移动。
- 敌人、Gate、Prop 分别使用按本局开始时间计时的生成列表，不使用波次概念。
- 当所有敌人生成项都已处理且 `AliveEnemyCount == 0` 时胜利；敌人死亡动画尚未回收不影响该条件，符合 EnemyManager 的存活统计规则。
- Victory 会立即结束当前会话，不等待 Gate/Prop 时间轴派发完成，也不等待仍在道路上的 Gate/Prop 接触或离场；未来条目停止生成，活动道路对象在 StopRun 中清理。这是当前预期规则，不属于内容丢失。
- 当 Army 总人数小于等于 0 时失败。同一帧同时满足“最后一个敌人死亡”和“Army 归零”时，失败优先。
- 初始化成功后异步加载 MainMenuScene，固定入口 Ready 后进入主界面并等待玩家点击开始；随后异步卸载旧场景、异步加载 LevelSelectScene，入口 Ready 后生成关卡节点并等待选择。
- 胜利或失败后停止本局逻辑并冻结 HUD，留在 GameplayScene 显示对应结算根节点。玩家可以返回选关；失败时还可重试当前关，胜利且存在有效解锁目标时还可挑战 `unlockedLevelIds` 首项。重试和下一关都创建新的 `LevelRunId` 并重新加载 GameplayScene。
- 军队逻辑上使用整数总人数，画面使用 Army Prefab 序列化槽位数组决定的固定数量上场槽位；总人数超过槽位数时由槽位代表多人。
- 每个上场槽位拥有独立聚合生命值、碰撞体和子弹生成点，军队整体通过 ArmyRoot 横向移动。
- 军队每局以 `WeaponId = 0` 的弹弓开始；火、冰、雷分别保存剩余持续时间且初始为 `0`。初始活动槽位和实际换武器后的活动槽位在攻击周期第 0 帧立即发射，运行中新激活槽位等待完整 `FireInterval`；每槽每逻辑帧最多发射一颗且不追赶补发，但保留周期余量。子弹保存发射瞬间的 WeaponId 和 ElementMask，飞行中不随 Army 状态变化。
- SpawnY、EnemyApproachY、DespawnY 与子弹 `TopBoundary` 离场阈值都按实例根 GameObject 中心判断；Army 横向边界仍使用激活槽位合并 AABB。
- 加法门数字可以为负数；每次有效子弹命中按本次实际伤害累加，不按命中次数使用固定增量。
- 非负加法门增加人数并受 ArmyCountLimit 限制；负数门请求 Army 按等价单兵 HP 伤害执行减员，标记失败但仍只结算一次。
- 元素门的 `ElementType` 与 `MaxHp` 逐生成项配置在 `LevelConfig`；HP 清空后的额外伤害乘关卡级系数得到元素持续时间，当前不设置上限。HP 为零且仍处于待接触状态时继续作为合法子弹目标并消耗子弹；未清空时接触会伤害每个接触槽位，永久锁定奖励并继续离场。
- 道具未击破接触时对每个接触槽位造成相同伤害；接触前击破则触发一次配置的击破效果。当前 MVP 的三种武器箱按 `WeaponId` 更新武器，其他效果及组合规则仍在设计待决中。
- 门和道具在移动并同步后只对终点姿态执行一次 Overlap 接触查询，不做接触 Cast；未接触时允许直接从道路下方离场。
- 怪物不通过到达道路底部扣除军队人数；首版按 ADR-005 在接近线后向 Army 接近并攻击。
- 所有参与命中、接触、受击或阻挡的玩法对象（包括子弹）使用 Inspector 绑定的 `Collider2D`；LevelManager 集中读取各时间域 delta，并按移动、子弹、道路接触、敌人攻击、回收和终局的顺序同步驱动对应 Manager。子弹与敌人阻挡使用 Cast，范围攻击及 Gate/Prop 终点接触使用 Overlap。
- 玩家关卡进度保存于移动端 `Application.persistentDataPath`：记录已完成与已解锁 LevelId；启动时与 `initiallyUnlocked` 合并，胜利后同步保存，失败和主动退出不写进度。卸载或清除应用数据后不保证保留。
- 存活敌人的身体 Collider 使用上一同步姿态执行 Cast 阻挡。前方敌人较慢或静止时，后方敌人尽量按安全间距排队；该离散规则在 MVP 参数下减少穿透和重叠，但不保证同帧移动后的绝对不重叠，首版不实现事后分离或侧向绕行。
- Army 在 Preparing 播放 Idle；进入 Playing 后持续自动攻击，原地、实际左移、实际右移分别由循环 Attack、MoveLeft、MoveRight 表达。子弹仍由 FireInterval 逻辑生成，Animator 不决定射击。Victory 停止玩法推进并保留士兵表现，Gameplay HUD 与结算数据冻结；玩家选择返回、重试或下一关后卸载场景并执行最终 Army StopRun。
- 敌人阻挡安全间距由各敌人规范 Prefab 的 `blockingGap` 序列化字段提供，不进入 Luban 或 LevelConfig。
- 怪物进入攻击状态后由 Animator 播放非循环 Attack 序列帧；AttackCooldown 从起攻时计算。Clip 命中关键帧调用 `OnAttackFrame()` 登记攻击请求，实际伤害统一在 EnemyManager 的 `ResolveAttacks` 阶段校验并结算，末帧调用 `OnAttackAnimationFinished()` 结束本次攻击；非循环 Death Clip 末帧用 `OnDeathAnimationFinished()` 登记回收。
- 首轮工程切片通过合理的移动速度、Collider 尺寸和关卡编排控制离散碰撞风险；不实现相对运动扫掠、子步进或任意高速/严重掉帧下的绝对不穿透保证。
- 批次 7 已导入 Army、Monster、Bullet 和 Gate 的正式序列帧并完成 Animator/Prefab 预绑定；Gate 仍保留单个调试文本。Gameplay Canvas 已按 ADR-058 增加功能性 HUD、退出确认和结算控制器；击杀进度条、三种结算根节点及其按钮的具体视觉和 Inspector 绑定由后续场景装配完成。
- Luban 表、LevelCatalog 或 LevelConfig 数据非法时由 ConfigService 输出首个明确错误并立即退出应用；ADR-044 明确允许的 `unlockedLevelIds` 目录缺失 ID 是唯一例外，只警告并过滤。Prefab、Collider、Layer 或 Inspector 引用非法时输出错误并阻止对应 Ready。两类错误都不使用默认值、自动补组件、降级或重试继续运行。

## 非目标

本阶段不包含联网、账号、支付、广告、在线排行榜、得分系统、云存档、多存档槽、设置持久化、声音、暂停、减速、局部时停、通用调试服务和复杂养成系统。本地存档仅保存关卡完成与解锁状态。

HUD 与胜负面板的最终美术、Gate 最终美术、VFX 和音频仍不属于当前功能切片；现有基础 UI、调试文本、结构化日志和测试负责提供验证反馈。
