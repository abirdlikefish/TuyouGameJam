# MVP 测试与验证策略

## 当前策略

- 当前不创建任何 `.asmdef`，也不生成 EditMode 或 PlayMode 自动测试代码。
- `IntegrationTests.md`、`ObstacleTests.md`、`PerformanceTests.md` 和各模块 README 中的测试条目是验收规格，不限定必须由自动化执行。
- 测试复选框只有在对应行为被实际复现并确认后才能勾选；仅完成代码编写、静态阅读或推测结果不能视为通过。

## 当前验证层级

1. **文件与差异检查**：确认只修改目标目录，没有编辑 Luban 生成文件或未授权配置；动画批次额外检查源包未混入 Assets、运行时 Sprite/Clip/Controller/Prefab 路径和 `.meta` 完整。
2. **Unity 编译检查**：每批脚本导入后必须无编译错误，再开始下一批依赖代码。
3. **规则边界验证**：按测试清单使用最小配置、结构化日志、Inspector 状态和可复现操作验证代表性输入、边界值、重复调用与过期会话。
4. **资源与场景装配验证**：由用户完成 Sprite 导入、Clip/Controller、Prefab、Collider、Layer、Animator 参数与 Event、ScriptableObject 和场景引用后，验证 ID 到动画映射、Preparing/Ready、池复用、失败阻断和清理。
5. **完整游玩闭环**：验证启动、MainMenu、LevelSelect、Gameplay、Victory/GameOver、返回选关和重新开始，且上一局对象与回调不残留。

## 为未来自动化保留的代码边界

- 人数分配、伤害、计时、生成游标、状态转换和结果优先级优先放入纯 C# 类。
- MonoBehaviour 不隐藏核心计算，只承担序列化引用、Unity 生命周期、Transform/Physics2D/Animator 适配和阶段入口。
- 服务与 Manager 依赖最小接口和不可变快照，不通过静态单例、`Find` 或通用 Service Locator 取得依赖。
- 同一输入、配置和 `LevelRunId` 应产生可重复结果；随机性若后续加入，必须先定义可注入来源。

## 正式程序集阶段的自动化优先级

### 第一优先级：EditMode

- Army 人数分配、聚合 HP、补位与负数门减员。
- Gate/Prop 成功、失败、奖励锁定和一次性结算。
- Spawn 时间轴游标、零时刻条目、终局截断与旧会话隔离。
- Victory/GameOver 同帧冲突时的失败优先级。
- EventBus 分发顺序、订阅快照、异常隔离和 Token 幂等。
- 类型池的未激活借出、业务重置、重复归还与跨池归还。
- ConfigService 的快照复制、引用校验和首错失败。

### 第二优先级：PlayMode

- UGUI Pointer、坐标转换、Raycast、禁用与失焦清理。
- Collider2D Cast/Overlap、LayerMask 和单帧 `Physics2D.SyncTransforms`。
- Animator 攻击/死亡事件桥接及重复、过期回调。
- Army 武器 OverrideController 切换、BulletId/ElementType Animator 状态选择以及池复用重置。
- Additive 场景加载、SceneEntry Ready/清理、失败恢复和完整重开。

## 关联决策

- [ADR-026：程序集边界与跨层通信](../06_Decisions/ADR-026-AssemblyBoundariesAndCommunication.md)
- [ADR-045：自动化测试延后至正式程序集阶段](../06_Decisions/ADR-045-DeferAutomatedTestsUntilAssemblyDefinitions.md)
- [ADR-048：序列帧动画资源管线与 Prefab 预绑定](../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md)
