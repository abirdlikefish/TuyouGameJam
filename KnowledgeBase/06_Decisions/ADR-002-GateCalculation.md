# ADR-002：MVP 使用乘法门

## 状态

Superseded by `ADR-006-AdditiveGateAndContactResolution.md`

## 历史决策

第一版只实现乘法门。普通子弹命中使门数字 `+1`，军队接触后执行：

```text
NewArmyCount = Clamp(ArmyCount × GateValue, 0, ArmyCountLimit)
```

门效果应用一次后回收。

## 后续扩展

可以在不改变 Army 公共接口的情况下增加 `+N`、`-N` 和 `÷N` 门。
