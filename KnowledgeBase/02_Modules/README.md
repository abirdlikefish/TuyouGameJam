# 模块总表与并行认领

## 并行编辑约定

每个对话窗口开始工作前，先选择一个模块或一个架构主题，并在回复中说明认领范围。默认只修改该模块的 `README.md`、同目录下新增的设计文档，以及为保持一致性必须更新的共享记录。

共享契约和项目级文档不能被多个窗口同时重写。需要跨模块修改时，先在 `../00_Project/DesignBacklog.md` 记录问题，再通过 `../06_Decisions` 定案。

“负责人”是当前文档主题的认领人，不等同于最终代码负责人；没有明确认领时保持空白。

## 模块状态

| 模块 | 状态 | 设计入口 | 主要依赖 |
|---|---|---|---|
| Army | InDesign | `Army/README.md` | EventBus、IArmyConfigProvider、IWeaponConfigProvider、Level、IBulletManager |
| Gate | InDesign | `Gate/README.md` | EventBus、Army、Bullet、ObstacleManager、GateSpawnRequest；消费 LevelManager 传入的 Gate delta |
| Prop | InDesign | `Prop/README.md` | EventBus、Army、Bullet、ObstacleManager、IPropConfigProvider；消费 LevelManager 传入的 Gate delta |
| Obstacle | InDesign | `Obstacle/README.md` | IPropConfigProvider（仅 Prop）、PoolService、Gate、Prop、EventBus、Level |
| Monster | InDesign | `Monster/README.md` | Level、Bullet、Army、EventBus、IEnemyConfigProvider、PoolService |
| Bullet | Planned | `Bullet/README.md` | Level、IBulletConfigProvider、PoolService、IBulletHittable |
| Level | InDesign | `Level/README.md` | GameStateService、TimeService、Spawn、Monster、Army、BulletManager、ObstacleManager、InputGate、EventBus |
| Spawn | Planned | `Spawn/README.md` | LevelConfigSnapshot、RoadLayoutSnapshot、EnemyManager、ObstacleManager |
| UI | HUD Deferred / Input UI ContractReady | `UI/README.md` | HUD 后续依赖 EventBus、GameStateService；首轮只实现 Input UI |
| AudioVFX | Audio Deferred / VFX Planned | `AudioVFX/README.md` | VFX：EventBus、PoolService；Audio：后续另行设计 |
| Input | ContractReady | `Input/README.md` | IHorizontalInputReceiver、UGUI EventSystem、Level；由 Gameplay 场景装配控制启停与逐帧 Tick |

## 模块交付前检查

- [ ] 职责和非职责明确。
- [ ] 依赖、被依赖模块和未决问题已登记。
- [ ] 输入、输出、接口和事件与 `03_SharedContracts` 一致。
- [ ] 有至少一组可复现的设计验收标准。
- [ ] 变更已写入 `../07_Changes/ChangeLog.md`。
