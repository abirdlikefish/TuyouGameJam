# AudioVFX 音频与特效模块

## 模块信息

- ID：`MOD-AUDIO-VFX`
- 层级：Presentation
- 状态：Audio `Deferred`；VFX `Planned`
- 当前依赖：VFX 使用 EventBus、PoolService

## MVP 范围

- MVP 完全无声音，不创建 `AudioService`、`AudioRoot`、`AudioMixer`、音频事件监听器、音量设置或 AudioClip 绑定。
- VFX 后续可以监听射击、子弹命中、敌人受击/攻击/死亡、数字变化、人数变化和胜负事件，播放粒子与屏幕反馈。
- 没有 Audio 或 VFX 监听者时，核心玩法、状态转换和胜负结果必须保持正确。

## 后续音频范围

声音功能启用前重新确认以下边界：

- 跨场景音乐、AudioMixer 和运行时音量是否由全局 `AudioService` 持有。
- 场景内一次性音效由哪些表现适配器订阅事实事件。
- 音量设置是否持久化；如需持久化，必须与未来 SaveService 契约共同设计。
- AudioClip 的 Inspector/资源注册表绑定、缺失资源行为和音频验收标准。

## 约束

表现层不修改核心玩法数据，只响应事件。未来接入音频不得改变既有事件发布时机或核心结算。
