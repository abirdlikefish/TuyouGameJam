# 系统总览

## 分层与允许依赖方向

```text
表现层：UI、Input Adapter、摄像机、特效（音频 Deferred）
        │ 依赖公开玩法契约、同步输入命令与事实事件
        ▼
玩法层：军队、加法门、元素门、道具、子弹、怪物、关卡、生成、道路对象管理
        │ 依赖公开全局服务契约
        ▼
全局基础层：Bootstrap、时间、状态、事件、场景、配置

旁路扩展模块：本地存档和设置（当前 MVP 不启用）
启用后只依赖稳定的公开契约与事实事件，核心层不依赖其具体实现。
```

箭头表示允许的代码依赖方向：`A → B` 表示 A 可以依赖 B 的公开契约，不表示启动顺序、运行时数据流向或事件传播方向。全局基础层不依赖具体玩法或表现实现；玩法层不依赖 UI、音效或特效实现。

程序集划分已确认为后续工程化要求，但当前不创建 `.asmdef`。目标是在实现阶段用粗粒度程序集强制上述单向依赖，并由独立 Composition 边界完成具体实现的组装。详见 [程序集边界](AssemblyBoundaries.md)。

## 依赖原则

- 全局服务不依赖具体关卡对象。
- 表现层可以依赖玩法层公开契约，玩法层可以依赖全局基础层公开契约；反向不得引用上层的具体类。
- 同层玩法模块通过已记录的公开契约通信，不直接访问其他模块的私有字段或持有其具体控制器。
- 需要立即结果或确定顺序的操作使用同步接口；只有已经发生、且零订阅者不影响核心结果的事实才使用事件。
- 必须的反向能力使用依赖倒置接口并由 Composition 注入，不使用请求型事件规避依赖规则。
- 只读 HUD 和特效只监听事实事件，不参与核心数值计算；Gameplay 触屏控件属于 Input Adapter 的采集端，可以通过类型化同步命令提交玩家输入，但不得直接修改玩法对象字段。音频后续接入时遵守只响应事实的约束。
- 本地存档和设置是可选旁路扩展，不是表现层的下游层；实现前必须单独确认持久化数据契约和生命周期。
- 关卡道路、固定出生横线和按时间轴的生成编排放入 `LevelConfig` ScriptableObject；每条生成项使用 `[0,1]` 的归一化横向出生位置，不把坐标散落在 MonoBehaviour 中。
- 角色/军队、敌人、Gate、Prop 和子弹的可复用玩法数值由 Luban 表提供；当前不建立独立的全局平衡参数表，Unity 资源引用通过资源侧稳定键绑定。
- `ObstacleManager` 只管理 Gate/Prop 的生成实例、活动登记、查询和回收，不参与门数字、HP 或 Army 效果计算。
- 配置来源和运行时状态分离，运行时状态不得回写配置资产或 Luban 数据。
- 纯计算规则优先使用纯 C# 类，便于 EditMode 测试。
- 玩法碰撞对象统一使用 Inspector 配置的 `Collider2D`；核心检测通过显式 Cast/Overlap 查询完成，不以 Dynamic Rigidbody2D 的自动移动、推挤或碰撞回调作为规则来源。
- 碰撞查询必须服从模块的有效 delta 和会话状态；局部时停延后，未来启用时再确认其碰撞语义。

## 常驻对象

`GlobalRoot` 使用 `DontDestroyOnLoad` 跨场景保留，并由 `GlobalBootstrap` 初始化全局服务。单场景玩法对象由场景自己的 `LevelManager` 管理。

全局服务在应用生命周期内各有一个活动实例，但不要求实现静态单例。`GlobalBootstrap` / Composition Root 创建服务并把最小接口注入消费者；Gameplay 模块不得通过静态 `Instance`、运行时 `Find` 或通用 Service Locator 绕过分层边界。

## 关联决策

- [ADR-024：分层依赖方向与旁路扩展](../06_Decisions/ADR-024-LayerDependencyDirection.md)
- [ADR-025：场景层级与运行时职责命名](../06_Decisions/ADR-025-SceneHierarchyAndRuntimeRoleNaming.md)
- [ADR-026：程序集边界与跨层通信](../06_Decisions/ADR-026-AssemblyBoundariesAndCommunication.md)
- [ADR-028：MVP 触屏相对拖动输入](../06_Decisions/ADR-028-MvpRelativeDragInput.md)
