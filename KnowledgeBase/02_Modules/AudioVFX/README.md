# AudioVFX 音效与特效模块

## 模块信息

- ID：`MOD-AUDIO-VFX`
- 层级：Presentation
- 状态：`Planned`
- 依赖：EventBus、PoolService、AudioMixer

## 职责

- 监听射击、子弹命中、敌人受击/攻击/死亡、数字变化、人数变化、胜负事件。
- 播放音乐、音效、粒子和屏幕反馈。
- 管理音量和特效开关。

## 约束

表现层不修改核心玩法数据，只响应事件。
