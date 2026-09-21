# 门与道具规则测试

> 当前不创建自动测试程序集或测试代码。本清单先作为 Gate、Prop 与 ObstacleManager 的手工和集成验收规格；实际验证通过后才能勾选。未来自动化优先级见 [测试与验证策略](TestingStrategy.md)。

## Gate 规则

- [ ] 加法门没有 HP；每次有效子弹命中只按 `BulletDamageContext.Damage` 增加一次门值，并正常消费子弹。
- [ ] Gate 的 BodyCollider 可被 Bullet Cast 命中；命中结果不依赖自动碰撞回调顺序。
- [ ] 加法门正数和零调用 AddArmy 并受 ArmyCountLimit 限制；返回的 `ArmyAdditionResult` 分别记录请求增员、实际增员和剩余总人数；负数只调用 `RemoveArmy(Abs(GateValue))`。
- [ ] 加法门负数接触结果为失败；Army 按 `Abs(GateValue) × HpPerSoldier` 生成伤害预算，优先由当前 HP 最少、同 HP 时 SlotIndex 最小的槽位承担。
- [ ] 请求减员、实际伤害和实际人数损失分别记录；多个受伤单兵槽位可以使实际人数损失大于请求减员。
- [ ] 同一门与同一 Army 多槽位、多帧碰撞只产生一次接触结果。
- [ ] Gate 移动后只用终点 `OverlapCollider` 查询 ArmySlot，不执行接触 Cast；路径穿过但终点未重叠时不结算。
- [ ] 门先通过 `IArmyController` 应用效果或槽位伤害，再发布一次事实事件；ArmyController 不订阅该事件重复结算。
- [ ] 元素门每个生成项使用 LevelConfig 中各自的 `MaxHp` 和 `ElementType`；最后一发清空 HP 时只有超过剩余 HP 的部分进入 `PostDepletionDamage`。
- [ ] HP 已为 0 且仍处于 Pending 的元素门继续是合法子弹目标；后续命中消费子弹，并把全部实际伤害累计到 `PostDepletionDamage`。
- [ ] 元素门成功接触时只按 `PostDepletionDamage × elementDurationSecondsPerDamage` 计算一次持续时间；同类型累加且当前不设上限。
- [ ] `PostDepletionDamage == 0` 时接触仍成功，但不调用 `AddElementDuration`，也不发布 `ArmyElementDurationChanged`。
- [ ] 元素门接触失败后奖励永久锁定；后续命中正常消费子弹但 HP 最低锁在 `1`，不增加可兑换的 `PostDepletionDamage`、不归零且不增加任何元素持续时间。
- [ ] 元素门 HP 未清空时，每个接触槽位受到相同伤害。
- [ ] 元素门失败后继续向下移动并在离开道路后注销；负数加法门沿用普通加法门接触后流程回收。
- [ ] 未接触离场不发布成功或失败接触事件。

## Prop 规则

- [ ] 弹弓箱、弓箭箱、法杖箱分别引用正确的 `WeaponId`。
- [ ] Prop 的 BodyCollider 可被 Bullet Cast 命中，并按运行时实例 ID 去重。
- [ ] 道具在接触前 HP 清空时只触发一次配置的击破效果并发布一次击破事件；当前 MVP 的效果为武器更新。
- [ ] 道具未击破接触时，每个接触槽位受到相同伤害。
- [ ] 同一道具不会因多帧碰撞重复伤害同一 Army。
- [ ] Prop 移动后只用终点 `OverlapCollider` 查询 ArmySlot，不执行接触 Cast；路径穿过但终点未重叠时不结算。
- [ ] 未击破且未接触的道具离场不触发击破效果、不造成接触伤害。
- [ ] 道具接触失败后，后续子弹正常命中但 HP 最低锁在 `1`，不发布 PropBroken、不发放击破效果或因伤害回收；当前 MVP 验证武器不变并最终只从 DespawnY 离场。
- [ ] 道具先通过 `IArmyController` 应用武器或槽位伤害，再发布一次事实事件；监听者数量和订阅顺序不影响 Army 状态。
- [ ] 元素门接触失败后，后续子弹不能把 HP 降到 `0`，不会发放元素奖励或因伤害回收。

## ObstacleManager 生命周期

- [ ] 每个生成实例拥有唯一 `RuntimeInstanceId`。
- [ ] 相同生成参数的多个 Gate 以及相同配置的多个 Prop 可以同时登记和查询。
- [ ] 成功回收、失败后离场和未接触离场都只注销一次。
- [ ] Gate/Prop 都以根 GameObject 中心 `y <= DespawnY` 判定离场，Collider/Renderer 尺寸不参与阈值计算。
- [ ] 已注销对象不再出现在活动快照中。
- [ ] 归还对象池前数字、HP、`PostDepletionDamage`、奖励锁定状态、接触状态和运行时 ID 已重置。
- [ ] `AdditiveGate`、`ElementGate`、`WeaponProp`、`BasketballProp`、`GooseCageProp` 类型池均返回未激活对象；ObstacleManager 完成 Transform、配置、ID、回调和登记后才激活。
- [x] 鹅笼 Pending 状态 HP 首次归零只调用一次 `AddArmy`；`GooseCageBroken` 正确记录请求量、实际量和剩余人数（2026-09-21 编辑器瞬态验证通过；真实 Army 上限截断仍纳入完整玩法手测）。
- [x] 鹅笼未击破接触时逐槽伤害一次并进入 Failed；后续子弹锁 HP 至少为 1，不发布 `GooseCageBroken` 且不增员（2026-09-21 编辑器瞬态验证通过）。
- [x] ConfigService 已验证配置 ID 3/4 分别为 Basketball/GooseCage，且 Level_020 的 ikun 只引用 Basketball 配置 ID 3（2026-09-21 初始化验证通过；时间轴实际生成画面待加入具体条目后手测）。
- [ ] Gate/Prop 不在 `OnDisable`、`OnDestroy` 中归还自身；ObstacleManager 主动失活后归还，类型池再次防御性失活且重复归还不改变状态。
