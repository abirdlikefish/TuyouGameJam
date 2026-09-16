# 系统总览

## 分层

```text
全局基础层：Bootstrap、时间、状态、事件、场景、配置
        ↓
玩法层：军队、加法门、元素门、道具、子弹、怪物、关卡、生成、道路对象管理
        ↓
表现层：UI、摄像机、音效、特效
        ↓
后续扩展层：本地存档和设置（当前 MVP 不启用）
```

## 依赖原则

- 全局服务不依赖具体关卡对象。
- 玩法模块通过接口和事件通信，不直接访问其他模块的私有字段。
- UI、音效和特效只监听事件，不参与核心数值计算。
- 关卡道路、三路生成点和按时间轴的生成编排放入 `LevelConfig` ScriptableObject，不散落在 MonoBehaviour 中。
- 角色/军队、敌人、Gate、Prop 和子弹的可复用玩法数值由 Luban 表提供；当前不建立独立的全局平衡参数表，Unity 资源引用通过资源侧稳定键绑定。
- `ObstacleManager` 只管理 Gate/Prop 的生成实例、活动登记、查询和回收，不参与门数字、HP 或 Army 效果计算。
- 配置来源和运行时状态分离，运行时状态不得回写配置资产或 Luban 数据。
- 纯计算规则优先使用纯 C# 类，便于 EditMode 测试。
- 玩法碰撞对象统一使用 Inspector 配置的 `Collider2D`；核心检测通过显式 Cast/Overlap 查询完成，不以 Dynamic Rigidbody2D 的自动移动、推挤或碰撞回调作为规则来源。
- 碰撞查询必须服从模块的自定义时间和会话状态；局部时停只停止对象主动逻辑，不默认移除其碰撞形状。

## 常驻对象

`GlobalRoot` 使用 `DontDestroyOnLoad` 跨场景保留，并由 `GlobalBootstrap` 初始化全局服务。单场景玩法对象由场景自己的 `LevelManager` 管理。
