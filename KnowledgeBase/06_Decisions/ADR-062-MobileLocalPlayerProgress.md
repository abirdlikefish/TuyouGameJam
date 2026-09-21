# ADR-062：移动端本地关卡进度存档

## 状态

Accepted

## 日期

2026-09-21

## 背景

游戏最终运行于 Android/iOS，需要在应用重启后保留已完成关卡和已解锁关卡。当前 `GameStateService` 只维护本次应用生命周期内的解锁集合，冷启动时仅从 `LevelCatalog.initiallyUnlocked` 重建；ADR-017 曾将存档延后，并要求实现前另行确定数据格式、合并规则和生命周期。

## 决策

1. 新增应用级 `IPlayerProgressStore` 与 `JsonPlayerProgressStore`。存储实现只负责版本化 JSON 和文件安全，不拥有玩法规则；`GameStateService` 继续是运行期完成/解锁状态的唯一权威来源。
2. 单存档槽写入 `Application.persistentDataPath/player-progress.json`，同时使用同目录 `.tmp` 和 `.bak` 文件轮换。该目录适用于 Android/iOS 应用私有持久数据；包标识必须保持稳定。卸载应用或用户清除应用数据后不保证保留。
3. 版本 1 数据只包含 `schemaVersion`、`completedLevelIds`、`unlockedLevelIds`。内存使用集合去重，写出前按 ID 升序排序。
4. ConfigService Ready 后、请求 MainMenu 前加载存档。有效完成集合为存档完成 ID 与当前目录 ID 的交集；有效解锁集合为 `initiallyUnlocked ∪ 存档解锁 ∪ 有效完成`。负数和当前目录不存在的 ID 忽略并记录警告，不修改配置资产。
5. 接受当前会话 Victory 时，把当前 `LevelId` 加入完成与解锁集合，并合并当前 `LevelConfigSnapshot.UnlockedLevelIds`。集合发生变化时同步保存，保存尝试发生在发布 Victory 前。GameOver、主动退出、重试和启动下一关不修改进度。
6. 存档不存在时使用默认进度。主文件损坏时尝试 `.bak`；两者都不可读、版本不支持或字段非法时记录警告并使用默认进度。存档读取失败不是配置错误，不阻止应用启动。
7. 写入失败时保留当前运行期进度、记录错误并继续结算；不得因移动端瞬时存储错误卡住胜负流程。下次成功写入覆盖正式文件并保留上一份备份。
8. 存档不是 Victory 事件监听器。持久化是 `GameStateService.CompleteGameplay` 的显式必执行步骤，不能依赖允许零监听者和异常隔离的 EventBus。

## 数据格式

```json
{
  "schemaVersion": 1,
  "completedLevelIds": [0],
  "unlockedLevelIds": [0, 1]
}
```

## 不采用

- 不使用 `PlayerPrefs` 保存集合；它不适合版本化结构、损坏诊断和临时文件替换。
- 不只保存已完成关卡后根据最新配置重新推导解锁；配置更新不能撤销玩家已经获得的解锁。
- 不加密或签名本地 Game Jam 进度；本轮目标是可靠恢复，不承诺防篡改。
- 不增加多存档槽、云存档、账号同步、跨卸载恢复或设置持久化。

## 验收

- 首次启动无文件时只解锁目录的 `initiallyUnlocked` 关卡。
- 胜利后当前关卡进入完成集合，配置目标进入解锁集合；返回选关和重启应用后结果一致。
- 失败和主动退出不新增完成或解锁；重复胜利不产生重复 ID。
- 主文件损坏但备份有效时恢复备份；全部不可读时使用默认进度并继续进入 MainMenu。
- 未知、负数和重复 ID 不进入运行期集合；已完成关卡始终可选。
- 保存失败不阻止 GameplayResult，后续成功胜利仍可再次保存。

## 关联文档

- `ADR-017-DeferScoreAndSave.md`
- `ADR-024-LayerDependencyDirection.md`
- `ADR-053-InteractiveLevelSelectFlow.md`
- `../01_Architecture/GlobalServices.md`
- `../01_Architecture/BootstrapAndComposition.md`
- `../01_Architecture/ApplicationFlow.md`
- `../02_Modules/Save/README.md`
- `../03_SharedContracts/PublicInterfaces.md`
- `../05_Testing/IntegrationTests.md`

