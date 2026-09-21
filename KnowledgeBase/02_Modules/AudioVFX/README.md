# AudioVFX 音频与特效模块

## 模块信息

- ID：`MOD-AUDIO-VFX`
- 层级：Presentation
- 状态：Audio `Deferred`；VFX `InProgress`（三种元素组合显示已装配，其他反馈仍为 Planned）
- 当前依赖：一般 VFX 使用 EventBus、PoolService；元素组合显示由 ElementComboManager 在玩法结算后激活；需要自主帧计时的 Gameplay 世界 VFX 使用 TimeService

## MVP 范围

- MVP 完全无声音，不创建 `AudioService`、`AudioRoot`、`AudioMixer`、音频事件监听器、音量设置或 AudioClip 绑定。
- 火雷爆炸、冰雷闪电链和冰火蒸汽已经提供最小 LineRenderer 显示；射击、一般命中、敌人受击/攻击/死亡、数字变化、人数变化和胜负反馈仍可后续监听事实事件实现。
- 需要复用的 VFX 遵守 ADR-031：一个具体池化根类型只绑定一个规范 Prefab，由 VFX 管理器取得未激活实例、完成表现上下文初始化后激活，并在播放结束时清理、主动失活和归还类型池；VFX 对象不自行持有 PoolService。
- 三种元素组合 Prefab 是当前明确的玩法/表现边界适配：根脚本在实例仍失活时通过 `ResolveOnce` 完成唯一一次玩法结算，之后激活并由 `OnEnable` 开始纯显示。LineRenderer、显示计时和回池阶段不得修改 HP、元素历史或位移。
- 跟随 Gameplay 世界推进且需要主动读取 delta 的 VFX 使用 `VFX` 时间域；UI 动画和应用流程表现不使用该域，当前按 `RealTime` 语义推进。
- 没有 Audio 或 VFX 监听者时，核心玩法、状态转换和胜负结果必须保持正确。

## 后续音频范围

声音功能启用前重新确认以下边界：

- 跨场景音乐、AudioMixer 和运行时音量是否由全局 `AudioService` 持有。
- 场景内一次性音效由哪些表现适配器订阅事实事件。
- 音量设置是否持久化；如需持久化，必须与未来 SaveService 契约共同设计。
- AudioClip 的 Inspector/资源注册表绑定、缺失资源行为和音频验收标准。

## 约束

纯表现层不修改核心玩法数据。元素组合根脚本中的显式 `ResolveOnce` 属于 Gameplay 适配入口，不属于显示生命周期；其后的显示层只消费已经冻结的位置与连线数据。未来接入音频不得改变既有事件发布时机或核心结算。单次 VFX 更新只读取 `VFX` 域，不再叠加 `Gameplay` delta。
