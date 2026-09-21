# 模块总表与并行认领

## 并行编辑约定

每个对话窗口开始工作前，先选择一个模块或一个架构主题，并在回复中说明认领范围。默认只修改该模块的 `README.md`、同目录下新增的设计文档，以及为保持一致性必须更新的共享记录。

共享契约和项目级文档不能被多个窗口同时重写。需要跨模块修改时，先在 `../00_Project/DesignBacklog.md` 记录问题，再通过 `../06_Decisions` 定案。

“负责人”是当前文档主题的认领人，不等同于最终代码负责人；没有明确认领时保持空白。

## 工程实现并行约定

代码并行以 `../00_Project/ImplementationPlan.md` 为权威入口。Contracts、Foundation 和配置 Provider 编译稳定后，才允许 Army/Input、Bullet/Monster、Gate/Prop/Obstacle 三组按目录并行；Contracts、Foundation、Spawn、Level、Composition 和共享状态由单一集成窗口独占。模块窗口发现契约缺口时只报告，不直接修改共享类型或其他模块目录。

## 模块状态

| 模块 | 状态 | 设计入口 | 主要依赖 |
|---|---|---|---|
| Army | InProgress（动画/Prefab 装配中） | `Army/README.md` | EventBus、IArmyConfigProvider、IWeaponConfigProvider、Level、IBulletManager |
| Gate | InProgress（动画/Prefab 装配中） | `Gate/README.md` | EventBus、Army、Bullet、ObstacleManager、GateSpawnRequest；消费 LevelManager 传入的 Gate delta |
| Prop | ContractReady | `Prop/README.md` | EventBus、Army、Bullet、ObstacleManager、IPropConfigProvider；消费 LevelManager 传入的 Gate delta |
| Obstacle | ContractReady | `Obstacle/README.md` | IPropConfigProvider（仅 Prop）、PoolService、Gate、Prop、EventBus、Level |
| Monster | InProgress（动画/Prefab 与元素组合适配已装配，运行验证待完成） | `Monster/README.md` | Level、Bullet、Army、ElementCombo、EventBus、IEnemyConfigProvider、PoolService |
| Bullet | InProgress（动画/Prefab 与元素组合适配已装配，运行验证待完成） | `Bullet/README.md` | Level、IBulletConfigProvider、PoolService、IBulletHittable、ElementComboManager |
| Level | InProgress | `Level/README.md` | GameStateService、TimeService、Spawn、Monster、Army、BulletManager、ObstacleManager、InputGate、EventBus |
| Spawn | InProgress | `Spawn/README.md` | LevelConfigSnapshot、RoadLayoutSnapshot、EnemyManager、ObstacleManager |
| UI | Gameplay HUD/Result InTest / Input UI ContractReady | `UI/README.md` | HUD 读取只读快照，结果 UI 监听胜负事实并通过 GameStateService 返回选关 |
| AudioVFX | Audio Deferred / VFX InProgress（三种元素组合最小显示已装配） | `AudioVFX/README.md` | VFX：EventBus、PoolService、ElementComboManager、TimeService；Audio：后续另行设计 |
| Input | ContractReady | `Input/README.md` | IHorizontalInputReceiver、UGUI EventSystem、Level；由 Gameplay 场景装配控制启停与逐帧 Tick |

## 模块交付前检查

- [ ] 职责和非职责明确。
- [ ] 依赖、被依赖模块和未决问题已登记。
- [ ] 输入、输出、接口和事件与 `03_SharedContracts` 一致。
- [ ] 有至少一组可复现的设计验收标准。
- [ ] 变更已写入 `../07_Changes/ChangeLog.md`。
