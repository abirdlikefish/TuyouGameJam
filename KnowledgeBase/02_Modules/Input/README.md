# Input 输入模块

## 模块信息

- ID：`MOD-INPUT`
- 层级：Presentation / Gameplay Adapter
- 状态：`InDesign`
- 依赖：Army、TimeService

## 职责

- 读取桌面端键盘或手柄的横向输入，归一化为 `[-1, 1]`。
- 将输入作为 Army 的移动命令，不直接修改 ArmyRoot 的 Transform。
- 只传递归一化方向和强度；Army 使用 `TbArmy.MoveSpeed` 计算实际横向速度。
- 在暂停或 Army 不可操作时停止发送移动命令。

## 非职责

- 不决定军队移动速度、阵型宽度或左右边界。
- 不读取或修改槽位人数、生命值和武器元素配置。

## 测试标准

- 左右输入只改变 ArmyRoot 的横向位置。
- 输入值超出范围时被归一化或截断到 `[-1, 1]`。
- ArmyRoot 的边界限制由 Army 根据 LevelConfig 固定道路边界和当前激活槽位 AABB 执行；Input 不直接读取或修改道路坐标。
