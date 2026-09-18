# ADR-002：MVP 使用乘法门

## 状态

Superseded by `ADR-006-AdditiveGateAndContactResolution.md` and `ADR-038-LevelConfiguredDamageDrivenGates.md`

> 当前数字门只保留加法门，明确不恢复乘法门或除法门；以下内容仅保留历史决策轨迹，不属于实现范围。

## 历史决策

第一版只实现乘法门。普通子弹命中使门数字 `+1`，军队接触后执行：

```text
NewArmyCount = Clamp(ArmyCount × GateValue, 0, ArmyCountLimit)
```

门效果应用一次后回收。

## 后续扩展

历史上曾考虑增加 `+N`、`-N` 和 `÷N` 门；现行范围已由 ADR-038 收敛为只有加法门与元素门。
