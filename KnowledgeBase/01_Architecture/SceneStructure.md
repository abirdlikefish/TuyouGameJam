# 场景结构

```text
GameScene
├── MainCamera
├── Road
├── ArmyController
├── GateRoot
├── PropRoot
├── ObstacleManager
├── MonsterRoot
├── BulletRoot
├── LevelManager
├── SpawnManager
├── UI
└── VFXRoot
```

`GlobalRoot` 不放入普通关卡层级，建议由启动场景或 `GlobalBootstrap` 创建。

当前只有 Gameplay 使用实际关卡场景。MainMenu 和 LevelSelect 暂时由常驻的应用流程状态表示，各自等待 1 秒后自动跳过；后续可以在不改变应用状态契约的前提下接入独立 UI 或场景。

军队固定在屏幕下方；怪物、Gate 和 Prop 从道路上方生成并通过自身移动向下推进。
`ObstacleManager` 维护 Gate/Prop 的运行时实例登记和回收；具体移动、HP 与接触规则仍由对象自身负责。
玩法 Prefab 的 `Collider2D` 由 Inspector 绑定并按职责配置 Layer；对象移动和碰撞结算不依赖 Dynamic Rigidbody2D 的自动回调。
