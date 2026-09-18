# 模块总表与并行认领

## 并行编辑约定

每个对话窗口开始工作前，先选择一个模块或一个架构主题，并在回复中说明认领范围。默认只修改该模块的 `README.md`、同目录下新增的设计文档，以及为保持一致性必须更新的共享记录。

共享契约和项目级文档不能被多个窗口同时重写。需要跨模块修改时，先在 `../00_Project/DesignBacklog.md` 记录问题，再通过 `../06_Decisions` 定案。

“负责人”是当前文档主题的认领人，不等同于最终代码负责人；没有明确认领时保持空白。

## 模块状态

| 模块 | 状态 | 设计入口 | 主要依赖 |
|---|---|---|---|
| Army | InDesign | `Army/README.md` | TimeService、EventBus、ConfigService、Level、Gate、Prop |
| Gate | InDesign | `Gate/README.md` | TimeService、EventBus、Army、Bullet、ObstacleManager、ConfigService |
| Prop | InDesign | `Prop/README.md` | TimeService、EventBus、Army、Bullet、ObstacleManager、ConfigService |
| Obstacle | InDesign | `Obstacle/README.md` | ConfigService、PoolService、TimeService、Gate、Prop、EventBus |
| Monster | InDesign | `Monster/README.md` | TimeService、Bullet、Army、EventBus、ConfigService、PoolService |
| Bullet | Planned | `Bullet/README.md` | TimeService、PoolService、IDamageable、IBulletDamageable |
| Level | Planned | `Level/README.md` | GameStateService、Spawn、Monster、Army、ObstacleManager、EventBus |
| Spawn | Planned | `Spawn/README.md` | LevelConfig、RoadLayoutSnapshot、EnemyManager、ObstacleManager |
| UI | Planned | `UI/README.md` | EventBus、GameStateService |
| AudioVFX | Audio Deferred / VFX Planned | `AudioVFX/README.md` | VFX：EventBus、PoolService；Audio：后续另行设计 |
| Input | ContractReady | `Input/README.md` | Army、UGUI EventSystem；由 Gameplay 场景装配控制启停 |

## 模块交付前检查

- [ ] 职责和非职责明确。
- [ ] 依赖、被依赖模块和未决问题已登记。
- [ ] 输入、输出、接口和事件与 `03_SharedContracts` 一致。
- [ ] 有至少一组可复现的设计验收标准。
- [ ] 变更已写入 `../07_Changes/ChangeLog.md`。
