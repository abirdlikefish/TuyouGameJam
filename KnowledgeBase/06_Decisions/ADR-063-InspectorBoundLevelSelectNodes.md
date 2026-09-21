# ADR-063：选关节点改为 Inspector 显式绑定

## 状态

Accepted

## 日期

2026-09-21

## 背景

ADR-053 让 `LevelSelectView` 根据关卡目录从单一 Prefab 动态生成节点，并由 `GridLayoutGroup` 统一排列。当前选关界面需要使用手工设计的路线或地图布局：节点由场景或界面 Prefab 预先放置，设计者可以独立调整每个节点的位置，并在 Inspector 中明确指定节点对应的 `LevelId`。

玩家进度已由 ADR-062 分为完成集合与解锁集合，节点需要同时表达“已通关”“已解锁但未通关”和“未解锁”三种状态。

## 决策

1. `LevelSelectView` 序列化 `LevelNodeBinding[]`；每项显式保存一个场景内 `LevelSelectNodeView` 引用和对应 `LevelId`。不再序列化节点 Prefab，不在运行时创建或销毁节点。
2. 所有绑定节点必须位于当前 `LevelSelectView` 层级中。节点引用、LevelId 均不得重复，LevelId 必须存在于当前 `LevelDescriptor` 目录，并且目录中的每个关卡必须恰好绑定一次。非法装配阻止 LevelSelect Ready。
3. 节点位置完全由各自 `RectTransform` 决定；节点父容器不得使用会覆盖子节点位置的 LayoutGroup。设计者可以在场景或承载该 View 的界面 Prefab 中自由移动节点。
4. `LevelSelectNodeView` 显式绑定按钮、`UnlockedState` 和 `CompletedState` 两个互不相同的表现根。状态优先级固定为：
   - 已通关：只激活 `CompletedState`；
   - 未通关且已解锁：只激活 `UnlockedState`；
   - 未解锁：两个状态根都不激活。
5. 已通关关卡视为可选择；已解锁或已通关时按钮可交互，未解锁时按钮不可交互。节点点击仍只提交自身绑定的 LevelId，由 `GameStateService.TrySelectLevel` 再次校验并进入对应关卡。
6. 进入 Gameplay 请求被接受后，View 禁用全部节点按钮，防止重复请求。场景清理只移除运行时监听并复位节点，不改变或销毁预放节点。

## 后果

- 增加关卡配置时，必须同时在选关界面新增节点并配置唯一 LevelId；不再自动扩展列表。
- UI 布局可以形成任意路线，不受目录顺序或统一网格限制。
- 完成/解锁显示直接读取 `GameStateService` 已与本地存档合并的运行期集合。

## 验收

- 当前关卡 `0` 使用场景内预放节点并在 `LevelNodeBinding` 中显式绑定；运行时不产生额外节点。
- 修改预放节点的 `RectTransform.anchoredPosition` 后，运行时保持该位置。
- 已通关时只显示 `CompletedState`；仅解锁时只显示 `UnlockedState`；未解锁时两者都隐藏且按钮不可点击。
- 点击可选节点只请求其绑定 LevelId，并只创建一次 Gameplay 会话。
- 空引用、重复节点、重复 LevelId、未知 LevelId 或目录漏配都会阻止选关场景 Ready，并给出包含绑定索引或 LevelId 的错误。

## 关联文档

- `ADR-053-InteractiveLevelSelectFlow.md`
- `ADR-062-MobileLocalPlayerProgress.md`
- `../01_Architecture/SceneStructure.md`
- `../02_Modules/UI/README.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`

