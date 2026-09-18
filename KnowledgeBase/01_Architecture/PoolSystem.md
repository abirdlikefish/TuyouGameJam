# 对象池系统

## 状态

InDesign。`PoolService` 属于 MVP 必需服务，但以下未决语义确认前不能把对象池单项视为 `ContractReady`。

## 已确认职责

- 按 Unity 资源注册表中的稳定资源键获取可复用 GameObject。
- 将已使用实例归还其来源池；对象获取和归还不属于 SpawnManager。
- 在 `GlobalRoot` 下持有跨 Gameplay 场景保留的 `PersistentPoolRoot`。
- 支持 Bullet、Monster、Gate、Prop 和 VFX 等高频实例复用；MVP 不因音频创建池。
- 只管理实例复用，不拥有敌人、道路对象或子弹的业务活动集合。

## 已确认调用边界

```text
SpawnManager 发出业务生成请求
→ EnemyManager / ObstacleManager 查询配置与资源键
→ PoolService.Get(key, position, rotation)
→ 对应 Manager 分配新的 RuntimeInstanceId 并初始化本局状态

对象结束业务生命周期
→ 对应 Manager 注销活动实例
→ 清除本局状态并停止主动逻辑
→ PoolService.Release(instance)
```

每次复用前，数字、HP、接触状态、位置、会话数据和 `RuntimeInstanceId` 必须恢复为新实例状态。`RuntimeInstanceId` 属于对应 Manager，不由 PoolService 保留或复用。

## 非职责

- 不决定生成时间、世界坐标、配置 ID、敌人存活统计或 Gate/Prop 接触结果。
- 不替代 EnemyManager、ObstacleManager 或未来 BulletManager 的活动实例所有权。
- 不通过对象是否 Active 推断胜利、失败或时间轴是否完成。
- 不在释放时发布玩法死亡、离场或终局事件。

## 待决问题

以下问题需要在实现前新增或更新 ADR，并同步 `IPoolService`：

1. 资源键缺失、类型错误或实例化失败时，`Get` 返回失败结果还是抛出受控异常。
2. 未知实例、跨池实例和重复 `Release` 的处理方式。
3. 统一重置接口的名称、调用顺序，以及重置责任由 Manager 还是池对象适配器承担。
4. 是否预热、每类初始容量、容量上限和溢出策略。
5. Gameplay 卸载时保留空闲实例还是按类别清理，以及退出应用时的最终销毁顺序。
6. PoolService 是否需要暴露只读统计；不得因此提前引入 DebugService。

## 当前验收基线

- 同一配置的多个活动实例拥有不同 `RuntimeInstanceId`。
- 归还实例不再出现在 Manager 活动集合中。
- 复用实例不会携带上一局 HP、接触状态、位置、计时器、订阅或会话 ID。
- 过期 `LevelRunId` 的对象不会被重新登记进新会话。
- 场景重开不创建第二个 PersistentPoolRoot。
- 对象池存在与否不改变玩法事实事件和胜负结果。

## 关联文档

- `GlobalServices.md`
- `SceneStructure.md`
- `../02_Modules/Spawn/README.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Obstacle/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`
- `../05_Testing/PerformanceTests.md`
