# ADR-015：路线图状态只表示工程交付

## 状态

Accepted

## 日期

2026-09-14

## 背景

当前处于文档优先阶段。部分玩法规则已经通过 ADR 定案，但路线图仍全部未勾选，容易被误解为决策未完成，或反过来把已接受的设计误认为已有 Unity 实现。

## 决策

- `../00_Project/Roadmap.md` 的复选框只表示对应工程能力已经实现并通过要求的验证。
- ADR 的 `Accepted` 只表示设计决策已定案，不自动勾选路线图。
- 模块的设计成熟度继续使用 `Planned`、`InDesign`、`ContractReady`；工程状态继续使用 `InProgress`、`InTest`、`Integration`、`Done`、`Blocked`。
- 当前没有工程实现授权，因此路线图条目保持未勾选；设计缺口由 `../00_Project/DesignBacklog.md` 和模块状态表表达。

## 影响

- 路线图增加状态说明，避免把设计状态和实现状态混为一谈。
- 后续只有在相应代码、资源和验证完成后才能勾选条目。

## 关联文档

- `../00_Project/Roadmap.md`
- `../00_Project/DesignBacklog.md`
- `../README.md`
