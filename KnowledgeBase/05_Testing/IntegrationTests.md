# 集成测试清单

> 当前不创建 `.asmdef` 或 EditMode/PlayMode 自动测试代码。本清单是验收规格，当前通过 Unity 编译、结构化日志、Inspector 观察和可复现手工步骤执行；只有实际验证通过的条目才能勾选。正式程序集阶段再把高价值规则和场景流程迁移为自动化测试，详见 [测试与验证策略](TestingStrategy.md) 和 [ADR-045](../06_Decisions/ADR-045-DeferAutomatedTestsUntilAssemblyDefinitions.md)。

## 应用流程

- [ ] BootstrapScene 常驻；MainMenuScene、LevelSelectScene、GameplayScene 都在 Build Settings 中，并按固定根名称各有且只有一个对应 SceneEntry。
- [ ] 服务完成 Create、Connect 且应用级事件订阅完成后才进入 Start；ConfigService Ready 前不得请求任何应用场景。
- [ ] 配置初始化成功后请求异步 Additive 加载 MainMenuScene；只有加载完成、MainMenuSceneEntry 初始化并发布 `AppSceneReady(MainMenu)` 后才进入 MainMenu。配置数据失败时直接退出应用；MainMenu 场景加载或 Entry 装配失败时不得进入 MainMenu。
- [ ] 配置数据非法时日志包含稳定来源、字段或条目索引并立即退出应用；Prefab、Collider、Layer 或必需 Inspector 引用非法时日志包含对象路径并阻止对应 Ready。两者都不使用默认值、自动补组件、降级或重试继续运行。
- [ ] MainMenu Scene Ready 后持续停留且不创建自动计时；开始按钮只请求一次 LevelSelect 切换，快速重复点击无重复请求，旧场景异步卸载完成前不加载目标场景。
- [ ] MainMenu 退出按钮在 Editor 停止 Play Mode，在 Player 退出应用；开始或退出请求接受后两个按钮立即不可交互。
- [ ] LevelSelectSceneEntry 使用目录、运行期完成集合和解锁集合初始化 Inspector 显式绑定的预放节点；当前关卡 `0` 恰好绑定一次，运行时不生成额外节点，停留超过 1 秒不自动开始关卡。
- [ ] 每个选关节点保持自身 RectTransform 自由位置；已通关时只显示 CompletedState，仅解锁时只显示 UnlockedState，未解锁时两个状态根都隐藏且按钮不可交互。
- [ ] LevelSelect 的空节点引用、重复节点、重复 LevelId、未知 LevelId 或目录漏配会阻止场景 Ready，并记录对应绑定索引或 LevelId。
- [ ] 点击已解锁节点后只创建一个新 `LevelRunId` 并进入 `GameplayLoading`；GameplayScene 异步加载、LevelConfig 注入、入口订阅和 LevelManager `Preparing` 全部完成后才发布 `AppSceneReady(Gameplay)`。
- [ ] 只有匹配的 `AppSceneReady(Gameplay)` 才启动当前 LevelId 的开场视频；视频终止前保持 `GameplayLoading`、LevelManager `Preparing`、耗时为 0，输入、生成、移动和射击都不推进。
- [ ] 视频正常结束后只发布一次匹配的 `LevelIntroFinished(Completed)`；GameStateService 随后进入 Gameplay、发布一次 `LevelRunStarted` 并让 LevelManager 进入 Playing。
- [ ] 当前关卡未绑定视频时下一帧发布 `NoVideoConfigured` 并安全进入玩法；播放错误或准备超过 10 秒分别记录 `PlaybackFailed`/`PreparationTimedOut` 后安全进入玩法，不永久黑屏。
- [ ] LevelIntroVideo 黑色遮挡完整覆盖 TouchDragArea 与 BattleHud，RawImage 保持原视频宽高比并覆盖屏幕；BattleResult 保持最高层级，视频结束前拖拽状态归零。
- [ ] 重复、过期、场景不匹配或卸载后的 `LevelIntroFinished` 不推进状态；连续两局没有旧 VideoPlayer 回调、纹理或输入差值残留。
- [ ] MainMenu、LevelSelect 的场景事实使用 `LevelId = 0`、`LevelRunId = 0`；Gameplay 事实携带当前值，目标不匹配或过期事实不会推进状态。
- [ ] 根缺失、重名、入口类型错误或 Entry 初始化失败时，失败目标场景先完成清理，再发布 `AppSceneLoadFailed`；不得发布 Ready。
- [ ] Gameplay 加载失败时不发布 `LevelRunStarted`，清除待启动会话并请求恢复 LevelSelectScene；只有 LevelSelect Ready 后才进入 LevelSelect。
- [ ] 异步卸载失败发布 `AppSceneUnloadFailed`，停止本次切换且不加载目标场景。
- [ ] Victory 或 GameOver 只接受并发布一次；GameStateService 进入 `GameplayResult` 后停留在 GameplayScene，等待返回选关、失败重试或胜利下一关命令。
- [ ] `BattleHud` 在 Playing 与 GameplayResult 都保持显示；终局前显示实时耗时、三元素剩余时间和 Filled 击杀进度条，进度按 `KilledEnemyCount / TotalEnemyCount` 递增且 Victory 时为 `1`，不显示击杀文本；终局后全部数值冻结。
- [ ] GameOver 只显示失败根节点，提供再次挑战当前关和返回选关；重试使用相同 LevelId、全新 LevelRunId，并经过 GameplayLoading 和开场门禁进入新一局。
- [ ] Victory 的过滤后 `UnlockedLevelIds` 非空时只显示有下一关的成功根节点，挑战按钮以列表首项为目标并创建新 LevelRunId；列表为空时只显示无下一关的成功根节点且不存在下一关按钮。两种成功根节点都能返回选关。
- [ ] 战斗中退出先显示二次确认；确认层显示期间玩法与计时继续但 Pointer 拖拽被遮挡，取消后恢复操作，确认后不发布 Victory/GameOver、不解锁并返回 LevelSelect。
- [ ] 退出确认期间发生自然终局时确认层关闭且只显示唯一匹配的 BattleResult 根节点；任一结算按钮快速重复点击只接受一次场景请求。
- [ ] 回到 LevelSelect 后只重新初始化预放节点并等待再次选择；上一局的延迟事件、节点监听和池实例不会影响新会话。
- [ ] 快速重复点击只接受一次选择和开始命令，不能创建第二个会话或重复场景请求。
- [ ] 结构化日志中每次加载恰好出现一次切换请求、Entry 初始化和 Ready；每次卸载恰好出现一次 Entry 清理和 Unloaded，Gameplay 日志包含 `LevelId`、`LevelRunId`。日志不参与流程控制。

## 装配与服务访问

- [ ] 冷启动和 Gameplay 重开期间始终只有一个 `GlobalRoot` 和一套 MVP 全局服务实例。
- [x] Bootstrap、MainMenu、LevelSelect、Gameplay 和终局返回选关的全过程始终只有一个启用的 AppCamera、`MainCamera` Tag 和 AudioListener；Camera 实例跨应用场景切换保持不变。
- [ ] GameplayScene 不包含 Camera 或 AudioListener；离开 Gameplay 后 AppCamera 继续以 Solid Color 清屏，不残留上一帧玩法画面，三个 Overlay Canvas 不绑定 World Camera。
- [ ] 服务唯一性由 GlobalBootstrap / Composition Root 持有，不要求各服务暴露静态 `Instance`；Gameplay 和 Presentation 代码不通过运行时 `Find` 或通用 Service Locator 取得必需服务。
- [ ] GameStateService 可以注入假的 Config、Scene 和 EventBus 实现，独立验证状态转换、失败和幂等行为。
- [ ] Gameplay 场景装配入口只向 LevelManager、ArmyController、BulletManager、EnemyManager、ObstacleManager 等消费者注入其实际需要的最小接口。
- [ ] Bullet、Monster、Gate 和 Prop 等池对象不访问全局服务集合；对应 Manager 在复用时传入本局配置快照、`LevelRunId`、`RuntimeInstanceId` 和必要回调。
- [ ] Input Adapter 只存在于 Gameplay 场景，在 Playing 阶段启用，不创建跨场景 `InputService`，也不注入或查询 TimeService；LevelManager 读取本帧 `RealTime` delta 并作为 `unscaledDeltaTime` 传给输入 Tick，Input 不直接读取 Unity `Time`。
- [ ] 除 Bootstrap/Config 的过渡加载边界外，模块不直接访问 `LubanTables.Instance`；Army 只经 Army/Weapon Provider、BulletManager 只经 Bullet Provider、EnemyManager 只经 Enemy Provider、ObstacleManager 只经 Prop Provider 取得不可变快照，任何 Gameplay 模块都不接收完整 Tables。

## 输入

- [ ] 当前只实现相对拖拽；设备触屏和 Editor 左键由 Gameplay Input Adapter 汇总，并通过 `IHorizontalInputReceiver.SetHorizontalInput` 同步提交。项目 EventBus 不发布连续横向输入事件，Army 不反向查询 Input Adapter，工程中不存在键盘、手柄或 `Horizontal` 轴读取路径。
- [ ] `Assets/Prefabs/UI/PF_UI_TouchDragArea.prefab` 根为全屏拉伸 `TouchDragArea`，包含显式绑定根 RectTransform 的 TouchDragInput，以及 alpha 为 `0`、Raycast Target 开启的 Image；Prefab 不包含 Canvas、EventSystem、Input Adapter 或 Army 引用。
- [ ] Gameplay Canvas 持有 GraphicRaycaster；GameplayScene 中恰好一个 EventSystem 使用 StandaloneInputModule；GameplayInputAdapter 在独立 `GameplayRoot/InputAdapter` 上并序列化引用 TouchDragInput 实例。
- [ ] `TouchDragArea` 的 RectTransform 定义 Pointer 开始范围和归一化宽度；`horizontalMultiplier` 可在 Inspector 配置，默认值为 `1` 且不小于 `0`。
- [ ] PointerDown 记录活动 `pointerId` 和局部位置但输出 `0`；Drag 使用当前位置与上一个有效采样位置的水平差，并能累积同一次输入更新前的多个 Pointer 采样。
- [ ] 触屏先计算 `(accumulatedDeltaX / touchAreaWidth) / unscaledDeltaTime`，将该原始滑动速度 Clamp 到 `[-1,1]`，再乘 `horizontalMultiplier`；乘系数后的结果不再次 Clamp。
- [ ] 当原始触屏输入为 `0.75`、`horizontalMultiplier = 2` 时，传给 Army 的输入为 `1.5`；Army 拒绝 NaN/无穷值，但不把该有限值截断为 `1`。
- [ ] 手指仍按住但没有新的 Drag 差值时，下一次输入更新为 `0`，不会沿最后方向继续移动。
- [ ] 相同物理滑动速度在不同稳定帧率、分辨率和 Canvas 缩放下产生近似一致的原始归一化值。
- [ ] 没有 Drag 时输入为 `0`；PointerUp、EndDrag 或 Cancel 后保持 `0`，不存在键盘/手柄回退路径；右键和中键不开始拖拽。
- [ ] 一次只跟踪一个 Pointer；第二个 Pointer 不覆盖活动 Pointer。PointerUp、EndDrag、Cancel、组件/Canvas 禁用、离开 Playing、场景卸载和应用失焦都会清除累计值并向当前接收者发送一次 `0`；实现不额外轮询 `Input.touchCount`。
- [ ] Pointer 到达 TouchDragArea 边界后继续向外移动不再累加输入，返回区域内时产生正确的反向输入。
- [ ] 每个 Playing 逻辑帧 `IGameplayInputController.TickInput` 在 `ArmyController.TickMovementAndFire` 前执行；同帧 Army 消费刚提交的倍率，不依赖 MonoBehaviour 的隐式同类 `Update` 顺序。
- [ ] Input Adapter 初始化时先提交一次 `0`；同一接收者重复初始化幂等，空接收者、初始化前启用/Tick 或尝试替换为不同接收者均被拒绝并阻止错误装配进入 Ready。
- [ ] `horizontalMultiplier` 非有限或小于 `0`、区域宽度无效、Raycast Target 关闭或必需场景引用缺失时 Gameplay 不进入 Ready；单帧 `unscaledDeltaTime` 无效时只输出 `0` 且不破坏 Pointer 状态；坐标转换失败或局部 x 非有限时不污染累计差值。

## 动画资源与表现绑定

- [ ] 原始导出包只保存在 `Reference/AnimationSource`；`Assets/Art/Sprites` 中没有原始绿底帧、预览 GIF、导出 JSON 或供应方 4K 图集。
- [ ] 所有运行时动画帧使用连续四位编号；同一对象家族的 Texture Type、Alpha、Clamp、Bilinear、MipMap、Read/Write、Max Size、PPU 和 Pivot 设置一致，透明边缘在深浅背景上无明显绿边。
- [ ] 十个 WeaponId 各有 Idle、Victory、Attack、MoveLeft、MoveRight；Idle/Attack/左右移动循环，Victory 非循环；`AC_Army_Base` 无参数、无自动 Transition，所有 OverrideController 完整覆盖且无 Missing Motion。
- [ ] WeaponId 在 0～9 间实际切换时，当前活动和隐藏槽位都更新到对应 Controller；之后增加人数或重新激活槽位不会回到旧武器动画。
- [ ] 持有法杖时，火/冰/雷的 7 种非空组合分别映射 WeaponId 3～9；元素结束后直接降级到剩余组合，全部结束后回到 WeaponId 2。同一帧多个元素结束只发生一次最终切换。
- [ ] Preparing 播放 Idle；Playing 原地持续 Attack，实际左/右位移持续 MoveLeft/MoveRight；道路边缘无实际位移时为 Attack，状态不变时不会每帧重启动画。
- [ ] Army 持续战斗动画与实际 FireInterval 解耦，AnimationEvent 不生成子弹、不修改 FireInterval、伤害或 WeaponId；Victory 不参与终局判定，请求切换 LevelSelect 前保留士兵，场景卸载时仍完成 StopRun。
- [ ] Normal、Elite、Boss 各有循环 Move、非循环 Attack 和非循环 Death；Controller 使用既有 Attack/Death Trigger，所有 Motion 引用完整。
- [ ] 三种 Monster Attack Clip 恰好各有一个 `OnAttackFrame()` 和末帧一个 `OnAttackAnimationFinished()`；Death Clip 末帧恰好一个 `OnDeathAnimationFinished()`。
- [ ] BulletId 0～9 在唯一 `PF_Bullet` 上分别播放正确循环动画；同一池实例以不同 BulletId 复用时不残留旧参数、状态、帧或 Sprite。
- [ ] `PF_Gate_Additive` 播放唯一循环动画；唯一 `PF_Gate_Element` 按 Fire/Ice/Lightning 播放对应循环动画，以不同 ElementType 复用时不残留旧表现。
- [ ] Army、Bullet、Gate 动画不包含玩法结算事件；所有 Controller、OverrideController 和 ID 映射都由 Inspector 显式绑定，不存在 Resources、StreamingAssets、AssetDatabase 或字符串路径运行时加载。
- [ ] 当前动画纹理在目标平台记录实际导入尺寸和内存；关闭 Mipmap/ReadWrite，发现超预算时优先降低 Max Size、公共裁切或重新打包，不直接采用供应方稀疏 4K 图集。

## 核心闭环

### 2026-09-21 批次 7.6 动画与输入专项记录

- 通过：现有 12 个已填帧 Clip 与 Importer 一致，0 Pending、0 Invalid；39 Clip、5 Controller、6 AOC、10 个规范 Prefab 均无 Missing Script，运行态 Gameplay 成功进入 Ready。
- 通过：WeaponId 0/1/2 在活动与隐藏槽位间完整切换；新增槽位继承当前武器；运行态原地 Attack、左右移动 Attack 和 Victory 显式状态正确。Idle 入口已完成代码与状态名检查，Preparing 短窗口尚未截图留证。
- 通过：Normal/Elite/Boss 的 Attack 命中事件分别位于第 10/17/4 帧，结束事件位于第 22/61/30 帧；三个 Death 回收事件均位于第 3 帧。运行态完成 Move → Attack → Move、Death → 回收，并验证复借后 Trigger 清空。
- 通过：BulletId 0/1/2 与 Additive/Fire/Ice/Lightning Gate 的 Animator 状态和参数匹配；两轮借还复用了相同 GameObject 实例集合，无旧身份残留。
- 通过：场景实例 TouchDragInput 必需引用和区域宽度有效；拖动产生有限输入，PointerUp 后读取归零。
- 待完成：19 个正式 Clip 仍为空占位，不能勾选完整动作视觉验收；当前仅在 `844×529` Game View 验证输入，PlayerSettings 仍为横屏 `1920×1080`/AutoRotation，跨 9:16 分辨率与目标设备验证未完成。

- [ ] Gameplay 会话先进入 Preparing，初始化完成后进入 Playing。
- [ ] 子弹可以命中怪物并造成伤害。
- [ ] 子弹可以命中没有 HP 的加法门并按实际伤害增加数字；命中只结算一次且子弹正常消费。
- [ ] 非负加法门按增加规则更新总人数；负数门标记失败并把绝对值作为请求减员人数交给 Army，Gate 不选择槽位或直接设置 ArmyCount。
- [ ] 负数门伤害按当前 HP、SlotIndex 稳定分配；请求减员、实际伤害和实际人数损失可分别验证。
- [ ] 元素门最后一发清空 HP 时只把超过剩余 HP 的伤害计为额外伤害；HP 为 0 且仍待接触时继续接受并消费子弹，后续全部伤害累计为可兑换额外伤害。
- [ ] 元素门成功接触时只给对应 ElementType 增加一次 `PostDepletionDamage × elementDurationSecondsPerDamage` 的持续时间，同类型累加且不覆盖其他元素；额外伤害为 0 时成功但不调用 Army 持续时间命令。
- [ ] 元素门未清空时，对每个接触槽位造成相同伤害并继续向下离场。
- [ ] 元素门接触失败后永久锁定奖励；继续受击可以消费子弹和播放表现，但 HP 最低锁在 `1`，不再累计可兑换伤害、不能归零或获得元素。
- [ ] 门只对 Army 进行一次接触判定；未接触门可以直接从道路下方离场。
- [ ] Gate/Prop 先通过 `IArmyController` 完成状态变更再发布事实事件；增删 UI 或 VFX 监听者不会改变结算结果。
- [ ] 道具在接触前击破后只触发一次配置的击破效果；当前 MVP 的三种武器箱按固定 WeaponId `0/1/2` 更新武器并保留三元素剩余时间。配置保证获得法杖后不再生成其他武器箱，元素法杖 3～9 不作为武器箱直接掉落。
- [ ] 道具未击破接触时，对每个接触槽位造成相同伤害并继续向下离场。
- [ ] 怪物从固定出生横线上的配置位置向下移动，到达接近线后向最近的有效士兵槽位移动；初始横向位置不限制后续移动。
- [ ] 初始总人数为 1，并按配置创建对应的上场槽位。
- [ ] 总人数超过最大上场槽位后，槽位代表人数平均分配，余数按槽位顺序分配。
- [ ] 槽位受击只减少当前槽位人数，不主动重新平均其他槽位。
- [ ] 新增人数优先补充受击后人数较少或为空的槽位。
- [ ] 槽位聚合 HP 按 `HpPerSoldier` 计算，伤害按比例转换为槽位人数损失。
- [ ] 每个激活槽位从独立发射点按同一武器间隔发射一枚子弹；改变代表人数不改变单次发射数量、伤害或速度。
- [ ] 本局初始活动槽位在首个 Playing Tick 立即首发；运行中新激活槽位和失活后重新激活的槽位等待完整 `FireInterval` 后首发。
- [ ] 实际切换到不同 WeaponId 后全部激活槽位按新武器完整间隔重置；重复当前 WeaponId 不重置。
- [ ] 单个逻辑帧每槽最多生成一颗子弹；delta 跨过多个 FireInterval 时不补发历史子弹。
- [ ] 每局初始武器为 WeaponId=0，火/冰/雷剩余时间均为 0；三元素可以同时有效并从 Gameplay delta 扣减到不小于 0。
- [ ] 子弹保存发射瞬间的 WeaponId 与 ElementMask；Army 后续换武器、获得元素或元素过期不修改飞行中的子弹。
- [ ] 只有 `Fire|Lightning`、`Ice|Lightning`、`Fire|Ice` 三个精准二元素掩码触发对应效果；None、单元素、三元素、Gate 与 Prop 命中均不误触发。
- [ ] 火雷以直接目标命中时的 BodyCollider 中心为圆心，只在出现帧查询一次；直接目标受到基础伤害加爆炸伤害，范围内其他存活敌人只受爆炸伤害，同一目标可被同帧多个独立爆炸分别命中。
- [ ] 冰雷始终把直接目标作为第一目标，并从半径内其他存活目标中按 `LevelRunId + BulletInstanceId + ComboKind` 的确定性随机顺序补足总目标数；每个入选目标只结算一次，连线刷新不追加伤害。
- [ ] 冰火只造成基础伤害并登记固定世界 `+Y` 位移；同帧多次位移累加，不受道路、Army 或其他敌人阻挡，不改变攻击、动画、目标或冷却。
- [ ] 组合效果在 Prefab 激活前通过显式一次性入口完成伤害或位移登记；`Awake/OnEnable/Start`、LineRenderer 刷新和播放结束均不产生第二次玩法结算。
- [ ] 死亡动画中的敌人不再接受派生伤害或刷新最近元素记录，但已生成的爆炸、闪电或蒸汽显示继续按冻结世界坐标播放到结束。
- [ ] Fire、Ice、Lightning 最近受击记录分别在有效直接/派生伤害时重置为 1 秒，使用 Monster delta 递减；一秒后过期，死亡时不再暴露，回池后不残留。
- [ ] 三种 Monster Prefab 都有默认关闭且显式绑定的 Fire/Ice/Lightning 效果根节点；最近元素有效时对应节点独立开启，多个元素可以同时显示，计时归零或死亡时立即关闭，池复用不残留旧状态。
- [ ] 正式进入 Playing 时，初始活动槽位在 Attack/Move 动画第 1 帧立即发射；运行中新激活槽位仍等待完整 `FireInterval`。
- [ ] Attack、MoveLeft、MoveRight 连续切换保持已经播放的攻击周期进度，不从第 0 帧重启，也不改变下一次发射边界。
- [ ] 实际 WeaponId 改变时活动槽位从新动画第 0 帧立即发射；重复相同 WeaponId 不重播、不发射。
- [ ] 大帧每槽最多产生一颗周期弹且保留跨周期余量；换武器回调中新生成的子弹不在 BulletManager 当前 Tick 内移动或连锁命中。
- [ ] Gate 接触发生在 Army 发射阶段之后，本帧新增元素从下一逻辑帧子弹开始生效。
- [ ] Gameplay 固定使用 `Bullet`、`EnemyBody`、`EnemyAttack`、`ArmySlot`、`Gate`、`Prop` 六个职责 Layer；道路边界不使用 Collider 或 Layer。
- [ ] 三类敌人 Prefab 均不包含 `TargetSensor`；攻击起始只比较怪物与锁定槽位目标位置的 XY 距离。
- [ ] 子弹、敌人 BodyCollider、Hen/Rooster AttackCollider、Army 槽位、Gate 和 Prop 的 Prefab 均配置职责明确的 Collider2D 和 Layer。
- [ ] Chick/Hen/Rooster、Additive/Element Gate 和 WeaponProp 根节点均提供 ADR-055 的 Kinematic Rigidbody2D 查询适配：Simulated、关闭 Full Kinematic Contacts、零重力、Discrete、无插值并冻结旋转；Bullet 保持无 Rigidbody2D，任一目标适配缺失或误配都会阻止 Gameplay Ready。
- [ ] 子弹沿上一位置到期望位置执行 Collider Cast，在 MVP 约定速度、Collider 尺寸和测试帧率范围内稳定命中且一次只结算一次；不测试双方任意高速相对运动或严重掉帧下的绝对不穿透。
- [ ] 母鸡/公鸡的 AttackCollider 只在攻击判定帧执行一次显式重叠查询，每个 Army 槽位最多受击一次。
- [ ] 三种敌人的非循环 Attack Clip 在命中关键帧恰好调用一次 `OnAttackFrame()`，末帧调用一次 `OnAttackAnimationFinished()`；AnimationEvent 不直接修改 Army，实际伤害只在 `EnemyManager.ResolveAttacks` 中发生。
- [ ] AttackCooldown 从每次攻击开始时刻计算且在攻击动画期间继续递减；动画结束时若已到期，下一次 EnemyManager.TickMovement 重新验证成功后立即起攻，不由动画回调直接递归起攻。
- [ ] 三种非循环 Death Clip 末帧恰好调用一次 `OnDeathAnimationFinished()`；它只登记回收，重复/过期回调不重复计数、发布 MonsterKilled 或归还对象池，StopRun 不等待动画。
- [ ] 同一 AttackSequenceId 的重复关键帧事件只登记一次；事件前目标失效、事件后但结算前死亡、StopRun 或回池都会使待结算请求无效。
- [ ] AnimationEvent 在当帧 ResolveAttacks 之后触发时，请求安全保留到下一次 ResolveAttacks，且不会因为跨帧而重复结算。
- [ ] Gate/Prop 在移动并同步 Transform 后只以终点姿态执行一次 `OverlapCollider`；不执行接触 Cast/扫掠，接触状态和运行时 ID 保证一次性结算。
- [ ] 三种敌人 Prefab 各自提供有限且非负的 `blockingGap`；BodyCollider Cast 只查询上一同步姿态，后方敌人在命中前方敌人时截断位移。在 MVP 参数下验证明显穿透和重叠风险可接受，但不要求同帧移动后的绝对不重叠或事后分离。
- [ ] Monster 移动不查询 `ArmySlot`，Army 横向移动不查询 `EnemyBody`；士兵与敌人部分或完全重合时不发生推挤、接触伤害或位移修正。
- [ ] 士兵与敌人重合且目标仍有效时，小鸡敌人可以对锁定槽位结算伤害，母鸡/公鸡可以通过 `AttackCollider` 正常命中范围内槽位。
- [ ] 敌人进入 Dead 后退出受击和阻挡查询；死亡动画不会阻塞后方敌人。
- [ ] 首版不会因敌人受阻而执行侧向绕行、通道预留或局部导航。
- [ ] ArmyRoot 移动受当前激活槽位 AABB 限制，阵型变化后边界更新。
- [ ] ArmyRoot 的可移动范围同时受固定道路左右边界限制，任何激活槽位都不能越过道路边界。
- [ ] LevelConfig 只序列化有限且大于 0 的 `roadWidth`、`roadHeight`；道路中心固定为世界原点，四边由半宽和半高派生，道路没有玩法 Collider。
- [ ] ArmyRoot 每局准确重置到 `armySpawnPosition` 并保持配置 Y；初始阵型越界时 Preparing 失败，无道路 Collider 时仍能用槽位合并 AABB 完成后续左右限位。
- [ ] 多个槽位同时接触同一门时只应用一次门接触结果。
- [ ] `ObstacleManager` 能登记、查询、注销和回收 Gate/Prop，且相同生成参数的多个 Gate 或相同配置的多个 Prop 拥有不同运行时 ID。
- [ ] Gate/Prop 根中心 `y <= DespawnY` 时离场，Collider/Renderer 尺寸不改变阈值；离场对象不再结算接触。
- [ ] 子弹根中心严格大于派生 `TopBoundary` 时回收；等于上边界时不因 Sprite 或 Collider 边缘提前回收。
- [ ] 军队人数为 0 时进入 GameOver。
- [ ] 所有敌人生成项处理完且 `AliveEnemyCount == 0` 后进入 Victory。
- [ ] Victory 不等待 Gate/Prop 时间轴或活动实例完成；未来 Gate/Prop 停止生成，活动对象在 StopRun 中回收，且不会补发接触、击破或奖励效果。
- [ ] Victory 携带当前关卡 ID 和 ConfigService 已按 ADR-044 过滤的 `unlockedLevelIds`，不包含当前目录缺失 ID。
- [ ] Victory 的 `unlockedLevelIds` 既作为本局结果传递，也由 GameStateService 合并到玩家进度；当前 LevelId 同时进入完成和解锁集合，集合变化时在发布 Victory 前保存。
- [ ] 首次启动无存档时使用 `initiallyUnlocked`；重启后恢复已完成与已解锁集合，已完成关卡始终可选，失败和主动退出不修改存档。
- [ ] 主存档损坏时读取有效备份；主文件与备份均不可读、版本不支持、ID 为负数或目录中不存在时记录诊断并继续启动，不让存档错误冒充 ConfigService 致命错误。
- [ ] 写入失败保留运行期完成/解锁状态并继续 GameplayResult；重复通关不产生重复 ID，后续成功写入仍可恢复完整状态。
- [ ] Army 归零后进入 GameOver。
- [ ] 同一帧最后一只敌人死亡且 Army 归零时进入 GameOver。
- [ ] Victory 和 GameOver 停止本局逻辑并冻结结算数据；返回 LevelSelect 后等待玩家再次选择，重试或下一关则清理旧会话并以新 LevelRunId 启动目标关卡。

## 时间系统

- [ ] `Gameplay`、`Bullet`、`Gate`、`Monster` 和 `VFX` 都返回倍率为 `1` 的正常未缩放步进；同一帧输入下与 `RealTime` 数值一致。
- [ ] LevelManager 集中读取 `RealTime`、`Gameplay`、`Bullet`、`Monster`、`Gate` delta：Input 使用 RealTime，Army 和本局计时使用 Gameplay，BulletManager 使用 Bullet，EnemyManager 使用 Monster，ObstacleManager 使用 Gate；SpawnManager 只消费累计的 `elapsedTime`，Gameplay 世界特效使用 VFX。
- [ ] 单次移动、计时或调度只读取一个最具体的时间域，不把 `Gameplay` delta 与子系统域 delta 重复累计。
- [ ] MainMenu 和 LevelSelect 都等待 UI 命令，不创建自动跳过定时器，也不依赖 Gameplay 推进。
- [ ] `TimerHandle.Cancel()` 幂等；状态离开、加载失败、终局或会话失效后，旧回调不会执行或推进流程。
- [ ] MVP 不暴露父域图、重叠归属、倍率修改、暂停令牌、减速、加速或局部时停接口。

## Deferred 能力

- 声音、AudioService、音量设置和 AudioClip 绑定不属于 MVP 验收。
- SaveService、DebugService、暂停、减速、加速和局部时停不属于 MVP 验收。

## 配置系统

- [ ] 初始化阶段可以加载 `LevelCatalog`、Luban `cfg.Tables` 和资源注册表，不依赖 Gameplay 场景。
- [ ] `LevelCatalog` 中每个引用的 `LevelConfig.levelId` 唯一，第一关默认解锁且存在有效引用。
- [ ] LevelSelect 选定 `LevelId` 后，ConfigService 返回已校验的 `LevelConfigSnapshot`；SceneService 不重复查询配置，并将同一关卡 ID、快照和新的 `LevelRunId` 传入 Gameplay。
- [ ] ConfigService 启动时完整校验目录内全部 LevelConfig、五类表的主键/枚举/数值和当前跨表引用，并复制为关卡快照及五类数值快照字典；`unlockedLevelIds` 按 ADR-044 先校验重复和自引用，再警告并过滤目录缺失 ID；运行中修改资产不能改变当前快照。
- [ ] `IConfigService` 只包含查询，不暴露 `Initialize`、LevelConfig 资产或 `cfg.Tables`；Composition 直接初始化 Foundation 的具体 ConfigService，Contracts/Foundation 不引用 Gameplay。
- [ ] Luban 生成代码编入 `Game.ConfigGenerated`；它不引用领域层，生成类型只被 Foundation 配置实现和 Composition 装配使用，Contracts/Gameplay/Presentation 的接口不出现 `cfg.*`。
- [ ] Army、Weapon、Bullet、Enemy、Prop Provider 只在 Ready 后接受已校验 ID；所有表 ID 非负且允许 `0`，相同 ID 重复查询返回相同值语义的快照，不暴露 Luban 生成行或可变集合。
- [ ] 任一 Luban 表、LevelCatalog、LevelConfig 或必需引用错误时只输出首个包含 `ConfigErrorCode`、稳定来源、字段或条目索引和原因的 `Debug.LogError`，ConfigService 进入 Failed，Player 退出、Editor 停止 Play Mode；不发布配置恢复事件、不重试、不继续加载 MainMenu。ADR-044 的 `unlockedLevelIds` 目录缺失 ID 是唯一非致命引用例外。
- [ ] `unlockedLevelIds` 为空时初始化成功，快照和 Victory 结果均为空。
- [ ] `unlockedLevelIds` 包含重复 ID 时，无论该 ID 是否存在于目录，都报告首个重复条目的 `InvalidLevelConfig` 并终止应用；不自动去重。
- [ ] `unlockedLevelIds` 包含当前 `levelId` 时报告自引用的 `InvalidLevelConfig` 并终止应用；不把自引用解释为保持当前关卡解锁。
- [ ] 目录为 `[1,2,3]`、关卡 1 的 `unlockedLevelIds=[2,99,3]` 时，对缺失 ID `99` 只记录一次包含稳定 Source 和条目索引的 `Debug.LogWarning`，ConfigService 仍进入 Ready，快照和 Victory 保持原顺序 `[2,3]`。
- [ ] 把此前缺失的 ID 加入 LevelCatalog 后，下次初始化不再警告，并按原位置进入 `LevelConfigSnapshot.UnlockedLevelIds`。
- [ ] Gameplay Manager 不为缺失配置编写第二套 `TryGet`、默认值、日志或 StopRun 恢复分支；非 Ready 查询或未登记 ID 直接暴露为程序不变量异常。
- [ ] `LevelConfig` 引用的敌人和 Prop 配置 ID 全部存在；Gate 生成项不含 ConfigId，并按类型正确填写 InitialValue，或 ElementType 与 MaxHp；EnemyManager 的三个敌人规范 Prefab、ObstacleManager 的 Gate/Prop 规范 Prefab、BulletManager 的子弹规范 Prefab、阵型槽位和发射点绑定完整。
- [ ] `enemySpawns` 为空时报告 `InvalidLevelConfig` 并退出应用；`gateSpawns` 或 `propSpawns` 为空仍可正常完成配置初始化。
- [ ] 当前不存在 `TbGate` 或 Gate 配置 Provider；包含元素门时 `elementDurationSecondsPerDamage` 有限且大于 0，不包含元素门时为 0；`TbProp` 的生命值、伤害及武器引用符合配置契约。
- [ ] `TbArmy`、`TbWeapon`、`TbEnemy`、`TbProp`、`TbBullet` 的首行均为 `Id=0`，固定 `TbWeapon.Id=0`～`9` 均存在且同 ID 的 `BulletId` 引用有效；当前不建立 TbElement，缺失必需首行、固定行或引用时启动失败。
- [ ] `TbArmy.ArmyCountLimit = 0` 时不限制人数，大于 0 时正确应用上限；初始人数始终为固定值 1。
- [ ] `TbArmy.MoveSpeed` 是 Army 横向基础速度；实际位移按 `horizontalInput × MoveSpeed × 有效玩法 delta` 计算，触屏系数通过输入倍率影响最终速度但不改写配置。
- [ ] GameplaySceneEntry 的序列化 ArmyPrefabBinding 包含唯一 ArmyId=0；Prefab 根为 ArmyController，槽位数组非空、无空项或重复引用，SlotCapacity 准确等于数组长度。
- [ ] MVP Luban 表不要求 `PrefabKey`、`FormationKey`、`SoldierPrefabKey`、`PropType`、`AttackType`、代表人数缩放字段或 `CollisionBehavior`。
- [ ] 缺少必需的 Unity Prefab、Collider2D、阵型槽位或发射点绑定时阻止进入 Gameplay，并报告稳定来源。
- [ ] `ChickMonster`、`HenMonster`、`RoosterMonster`、`AdditiveGate`、`ElementGate`、`WeaponProp` 和 `Bullet` 的具体根类型与规范 Prefab 一一匹配；同类型不同 Prefab 注册被拒绝。
- [ ] AdditiveGate Prefab 只提供所有加法门共用的速度；ElementGate Prefab 提供所有元素门共用的速度与接触伤害。逐门初始数字、元素类型和 MaxHp 不重复配置在 Prefab。
- [ ] AdditiveGate 和 ElementGate 各自只用一个显式绑定的 TMP_Text 显示已结算状态；缺失文本引用时阻止 Gameplay Ready，首轮不依赖 HUD 或最终 Gate 美术。
- [ ] Chick/Hen/Rooster 的阻挡间距只来自各自 Prefab 的 `blockingGap`，不在 TbEnemy 或 LevelConfig 保存第二份数值。
- [ ] 三种武器箱共用 `WeaponProp` 规范 Prefab 并按 `WeaponId` 绑定正确表现；MVP 子弹共用 `Bullet` 规范 Prefab 并按 `BulletId` 取得正确数值和表现。
- [ ] 修改 Luban 数据并重新生成后，Unity 使用新数值且未编辑生成代码。
- [ ] 三类生成列表的时间、数量、配置 ID 和 `[0,1]` 横向出生位置与 `LevelConfig` 一致；`0`、`1` 和中间值正确映射到固定 `spawnY` 横线。
- [ ] `BottomBoundary <= despawnY < armySpawnPosition.y < enemyApproachY < spawnY <= TopBoundary`；无效宽高、非有限或越界 Army 坐标、Y 线顺序错误时报告 `InvalidLevelConfig` 并退出应用。
- [ ] 相同 `spawnPosition` 的不同尺寸敌人、Gate 和 Prop 使用相同中心点坐标，不按碰撞体或渲染尺寸内缩。
- [ ] 任一生成项的 `spawnPosition` 越界、为 NaN 或无穷值时报告 `InvalidLevelConfig` 并退出应用。
- [ ] 配置源不被运行时人数、生命值、门数字、道具 HP、生成游标或关卡计时覆盖。

## Spawn 与公共契约

- [ ] 三个时间轴游标只存在于 SpawnManager，LevelManager 不直接访问生成列表。
- [ ] LevelManager 每帧传入当前 `LevelRunId` 和 `elapsedTime`，生成条目只消费一次；过期会话的 Tick 不会推进新会话。
- [ ] 进入 Playing 时先执行一次 `SpawnManager.Tick(LevelRunId, 0)`；时间为 0 的生成项在首帧移动前只出现一次。
- [ ] 终局后 SpawnManager 停止消费；Victory 允许 Gate/Prop 游标尚未结束并直接截断；新会话只调用一次 `StartRun(LevelConfigSnapshot, RoadLayoutSnapshot, LevelRunId)`，并重置全部游标。
- [ ] 过期 `LevelRunId` 的生成请求、管理器操作和结果事件不会影响新会话。
- [ ] Config、Scene、Spawn、Enemy、Obstacle、EventBus、Time 和 Pool 接口均有明确输入、输出和失败语义；Pool 遵守 ADR-031 的类型身份、重复归还、业务重置和防御性失活契约。

## 类型对象池

- [ ] 同一具体类型以同一规范 Prefab 重复请求时返回同一类型池；同一类型绑定不同 Prefab 时明确失败，不创建第二个池。
- [ ] `RentInactive` 对首次创建和复用实例都返回未激活对象；首次实例化不会在 Manager 注入本次租用上下文前执行依赖上下文的 `OnEnable` 逻辑。
- [ ] Manager 在激活前依次设置活动父节点、位置、旋转、`RuntimeInstanceId`、配置、`LevelRunId` 和回调，并完成活动集合登记。
- [ ] Manager 归还前先注销活动实例、完成计数和事实事件、调用 `PrepareForPool()` 并主动失活；类型池随后防御性失活并移动到正确的空闲子节点。
- [ ] 第一次合法归还返回成功；`null`、未知实例、其他类型池实例和重复归还返回失败且不改变池状态。
- [ ] 池对象不持有 PoolService 或类型池，不在 `OnDisable`、`OnDestroy` 中归还自身；失活不会递归归还。
- [ ] `ChickMonster`、`HenMonster`、`RoosterMonster` 使用三个不同具体类型池；共有移动、受伤和目标查询规则的复用不改变类型池身份。
- [ ] Gameplay 终局和场景卸载前各 Manager 归还全部活动实例；下一局复用已有 PoolService、PersistentPoolRoot 和类型池，不保留上一局借出状态。
- [ ] PoolService 懒创建实例，不要求预热、容量配置或公共统计；应用清理时销毁全部已知实例并清除类型注册。

## 确定性与基础设施语义

- [ ] MVP 所有时间域和对象局部倍率均为 `1`，未启用运行时倍率调整。
- [ ] 所有包含 Army 身份的事件使用 `ArmyId = 0`。
- [ ] LevelManager 是同一帧阶段顺序的唯一协调者，按生成、移动阻挡、首次物理同步、子弹与组合结算、冰火位移、第二次物理同步、Gate/Prop、敌人攻击、回收、终局判断的顺序同步调用；Manager/池对象独立 Update 不推进核心玩法。
- [ ] LevelManager 在帧开始读取 Gameplay、Bullet、Gate、Monster delta 并分别传入对应阶段；具体池对象不直接访问 TimeService。
- [ ] Physics2D Auto Sync Transforms 关闭时，每个 Playing 帧由 LevelManager 准确调用两次 SyncTransforms：常规移动后一次、冰火组合位移后一次；其他 Manager 和效果对象不重复调用。已起手范围攻击在第二次同步后按新位置查询。
- [ ] Gameplay Layer 的自动物理矩阵默认全部关闭；目标 Unity 版本中，显式 ContactFilter2D/LayerMask 查询仍只能命中 ADR-037 规定的目标，不产生自动碰撞回调或刚体推挤。
- [ ] Kinematic 查询适配不通过 Rigidbody2D 速度、MovePosition、力或自动接触推进对象；Monster 阻挡仍只由 ApplyBlockedMovement 的 Cast 距离截断决定，Gate/Prop 接触仍只由终点 OverlapCollider 结算。
- [ ] `LevelRunStarted` 只让匹配会话进入 Playing，不作为逐帧命令广播；增删事件监听者不改变阶段调用。
- [ ] Bullet Cast 同距离目标按 `Enemy > Gate > Prop > RuntimeInstanceId` 稳定选择。
- [ ] EventBus 按注册顺序同步调用；发布期间使用订阅快照，异常隔离，重复订阅独立 Token，取消幂等。
- [ ] EventBus 只按准确消息类型分发；基类或接口订阅不会收到具体子类型消息。
- [ ] EventBus 支持同步嵌套发布，每层发布使用独立快照；`LevelIntroFinished` 处理器发布的 `LevelRunStarted` 不会因重入丢失或重复。
- [ ] 发布期间新增或取消订阅只影响下一次发布；当前快照中的处理器仍按原注册顺序完成。
- [ ] 默认、未知、重复使用和其他 EventBus 实例的 `SubscriptionToken` 取消时无副作用。
- [ ] 单个处理器和异常报告委托抛出异常时，当前快照中的其他处理器仍继续执行。
- [ ] 零监听者发布安全；移除 UI/VFX 监听者不会改变玩法状态、敌人死亡计数或终局结果。
- [ ] 应用服务在配置初始化前完成订阅；各 SceneEntry 在对应 `AppSceneReady` 前完成场景订阅，并在场景卸载前取消；旧场景监听者不会接收新场景或新会话事件。
- [ ] 池对象不直接订阅或查找全局 EventBus；复用前注入本局回调，回收时清除，下一局不会重复发布。
- [ ] Monster 通过必执行回调报告死亡，EnemyManager 先完成死亡去重和 `AliveEnemyCount` 更新再发布一次 `MonsterKilled`；零监听者不影响计数和回收。
- [ ] Unity 资源注册表按 `类别/身份` 键解析，缺失或类型不匹配在进入 Gameplay 前失败。
