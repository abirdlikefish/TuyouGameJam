# ADR-008：门和道具使用统一 ObstacleManager 管理

## 状态

Accepted

## 日期

2026-09-14

## 决策

- 新增 `ObstacleManager`，统一管理道路上的 Gate 和 Prop 实例。
- `SpawnManager` 负责生成时机、数量和生成点；`ObstacleManager` 负责实例获取、登记、查询、注销和回收。
- Gate/Prop 自身负责移动、HP、子弹命中、接触判定和效果应用；`ObstacleManager` 不计算这些规则。
- 管理器以运行时 `RuntimeInstanceId` 区分同一配置生成的多个对象，不能只使用 `GateId` 或 `PropId`。
- 管理器提供按对象类型和实例 ID 查询当前道路对象的只读快照。
- 对象成功回收或从道路下方离场时注销；注销操作必须幂等。

## 影响

- 场景增加 `ObstacleManager` 和道具对象根节点。
- Spawn、Gate、Prop、Bullet、Army 通过公共快照和事件协作，不直接访问管理器内部集合。
- 敌人生成完成和关卡终局条件不在本 ADR 中决定。
