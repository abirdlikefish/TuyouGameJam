# Save 模块

## 状态

InTest（Unity 编译与临时目录文件往返/备份恢复通过，目标 Android/iOS 设备待验收）

## 职责

- 以版本化 JSON 保存和读取单机玩家关卡进度。
- 在同一持久目录中使用临时文件和备份文件降低写入中断造成的数据损坏风险。
- 把文件不存在、主文件损坏、备份恢复和写入失败转换为明确结果与诊断文本。

## 非职责

- 不判断胜负、下一关目标或关卡是否应该解锁。
- 不拥有运行期进度集合，不直接订阅 Victory/GameOver。
- 不修改 `LevelCatalog`、`LevelConfig` 或 Luban 数据。
- 不提供云同步、多槽位、加密、防篡改、跨卸载恢复或设置持久化。

## 输入与输出

- 输入：`PlayerProgressSnapshot`，包含已完成与已解锁 LevelId。
- 输出：读取成功的不可变快照，或包含可定位原因的失败结果。
- 路径：`Application.persistentDataPath/player-progress.json`；同目录保留 `.tmp` 与 `.bak` 辅助文件。

## 生命周期

`GlobalBootstrap` 创建唯一 `JsonPlayerProgressStore` 并以 `IPlayerProgressStore` 注入 `GameStateService`。ConfigService Ready 后首次读取；只有接受 Victory 且集合发生变化时同步写入。服务不持有 Scene、View 或 Gameplay 对象引用。

## 移动端边界

- Android/iOS 正常关闭、重启与同包标识覆盖安装后使用同一应用持久目录。
- 卸载、清除应用数据或改变包标识会失去原目录，本模块不承诺恢复。
- 所有文件操作必须捕获异常；读取错误回退默认进度，写入错误不阻断结算。

## 验收

- 首次启动、胜利保存、重启恢复、重复通关、失败不保存均符合 ADR-062。
- 主文件损坏时可读取有效备份；主文件与备份均损坏时继续启动。
- JSON 使用稳定版本字段，数组按 LevelId 升序且无重复。
