# ADR-048：序列帧动画资源管线与 Prefab 预绑定

- 状态：Accepted
- 日期：2026-09-20
- 关联：ADR-007、ADR-031、ADR-040、ADR-043、ADR-047

> ADR-049 已修订 Army 动画语义：Attack/MoveLeft/MoveRight 都是持续战斗循环状态，由代码显式播放；Army Controller 不再使用参数或自动 Transition，Victory 使用独立表现阶段延迟最终清理。Monster、Bullet、Gate 规则不变。

## 背景

工程已完成代码生成批次 1～6并进入批次 7 的用户资源装配。原批次 7 只要求创建占位 Animator、怪物 Attack/Death Clip 与 AnimationEvent，没有覆盖正式序列帧的源文件整理、Unity 导入、Clip/Controller 组织、Army 武器动画映射、BulletId 表现选择和 ElementType 门动画选择。

当前运行时身份已经固定：Army 使用一个 `PF_Army_000` 并在本局切换 `WeaponId`；所有 `BulletId` 共用一个 `PF_Bullet`；火、冰、雷元素门共用一个 `PF_Gate_Element`；三种怪物各有一个规范 Prefab。为动画种类复制玩法 Prefab 会破坏现有 Prefab、类型池和配置边界，因此需要在不增加运行时资源加载的前提下定义表现资源预绑定方式。

## 决策

### 资源层级

1. 原始导出包保存在 `Reference/AnimationSource`，不进入 Unity 导入和构建。
2. 仅将优化后的透明 PNG 帧放入 `Assets/Art/Sprites`；不把原始绿底帧、预览 GIF、导出 JSON 或供应方 4K 图集复制进 `Assets`。
3. `.anim`、`.controller` 和 `.overrideController` 放入 `Assets/Animations`，按 Army、Monsters、Bullets、Gates 分域。
4. 可选 SpriteAtlas 放入 `Assets/Art/SpriteAtlases`；先完成单图导入和动画验收，再按实际批次与显存结果建立，不使用当前含大量透明空白的供应方 4K 图集。
5. 文件夹、Sprite、Clip、Controller 和 Prefab 使用 ASCII 名称，并在需要配置身份时包含稳定 ID。

完整目录、命名和导入设置见 `../04_Assets/AnimationPipeline.md`。

### 动画资产矩阵

1. 每个 `WeaponId` 提供一套士兵与武器合成序列帧：`Idle`、`Victory`、`Attack`、`MoveLeft`、`MoveRight`。
2. `Idle`、`Attack`、`MoveLeft`、`MoveRight` 循环；Playing 期间后三者都表示持续自动攻击，`Victory` 非循环并停留在末帧。
3. `Normal`、`Elite`、`Boss` 各提供 `Move`、`Attack`、`Death`。`Move` 循环，`Attack`、`Death` 非循环。Death 虽未出现在本轮新增素材描述中，仍是现有怪物回收契约的必需资源。
4. 每个 `BulletId` 提供一个循环 Clip。
5. 加法门提供一个循环 Clip；`Fire`、`Ice`、`Lightning` 各提供一个元素门循环 Clip。

### Prefab 与 Animator 绑定

1. 不因动画种类增加玩法 Prefab：继续保持一个 Army Prefab、一个 Bullet Prefab、两个 Gate Prefab和三种 Monster Prefab。
2. Army 使用一个稳定基础状态机和预创建的 `AnimatorOverrideController`。`PF_Army_000` 序列化唯一的 `WeaponId -> AnimatorOverrideController` 映射；换武器时把已绑定的 Controller 应用到所有槽位，包括当前未激活槽位。不得运行时构造 OverrideController 或按路径加载。
3. Monster 使用相同的 `Move/Attack/Death` 状态与既有 `Attack`、`Death` Trigger；每个怪物 Prefab 的 Animator 直接绑定本类型 Controller 或基于公共状态机的预创建 OverrideController。
4. `PF_Bullet` 保持单 Prefab，通过 Animator 的整数 `BulletId` 参数选择对应循环状态；池对象每次借出时在激活前写入 ID 并重置到该状态，不能继承上一次借用的动画状态。
5. `PF_Gate_Additive` 直接绑定单循环 Controller。`PF_Gate_Element` 通过 Animator 的整数 `ElementType` 参数选择 Fire、Ice、Lightning；每次借出时在激活前写入并重置状态。
6. 所有 Animator、Controller、OverrideController 与 ID 映射均通过 Inspector 显式引用。禁止 `Resources.Load`、StreamingAssets 路径加载、AssetDatabase 运行时查询、`Find` 或缺失时默认回退。
7. Army、Bullet、Gate 当前实现缺少上述表现适配字段和状态写入；批次 7 增加一个受控的“动画表现适配代码”子步骤。该代码只选择已序列化资源和设置 Animator 参数，不拥有玩法状态，不新增资源加载系统。
8. Army 使用 5 个无曲线的基础占位 Clip 作为稳定 Override 键；`AC_Army_Base` 保留五个无参数、无 Transition 的状态，由 ArmyController 通过固定状态哈希显式播放。三种武器分别以预创建 OverrideController 完整替换五个占位 Clip，缺少任一映射即为装配错误。
9. Monster 使用 3 个无曲线的基础占位 Clip、一个 `AC_Monster_Base` 和 Normal/Elite/Boss 三个预创建 OverrideController；不再为三种怪物复制三套状态机。
10. 31 个正式 Clip 预建 `SpriteRenderer.Sprite` 空轨道。Army 的绑定路径为空，Monster/Bullet/Gate 的绑定路径固定为 `Visual`；初始 `None` 键只用于提供可直接拖帧的编辑轨道，不是正式动画帧。
11. 批次 7.3 可先建立只含 Animator/SpriteRenderer 的 Prefab 动画外壳：Army 使用 `Slots/Slot_00/SoldierVisual`，其他对象使用根 Animator 与 `Visual` 子节点。该外壳不代表玩法脚本、Collider、代理、文本或序列化字段已经完成。

### AnimationEvent 边界

1. Monster Attack Clip 保持恰好一个 `OnAttackFrame()` 和末帧一个 `OnAttackAnimationFinished()`；Death Clip 末帧保持一个 `OnDeathAnimationFinished()`。
2. Army 不使用玩法 AnimationEvent；持续战斗动画与实际发射冷却解耦，子弹生成仍由 ArmyController 调度。
3. Bullet、Gate 循环 Clip 不包含玩法 AnimationEvent。
4. 任何 AnimationEvent 都不能直接修改 Army、Gate、Bullet 或 Monster 的权威玩法数值。

### 导入与性能基线

1. 透明 PNG 以 `Sprite (2D and UI)`、Single、Clamp、Bilinear、关闭 Mipmap、关闭 Read/Write、启用 Alpha Is Transparency 导入。
2. 同一对象家族使用一致 PPU、Pivot 和画布基准；帧按四位编号连续排序。帧率读取源说明，当前已检查素材为 24 FPS。
3. 默认先以平台 Max Size 512 验证清晰度；确需更高分辨率时记录实际屏幕尺寸和理由后上调。不能只根据 PNG 磁盘体积判断运行时内存。
4. 每个 Clip 验证帧数、顺序、持续时间、循环标记、边缘污染和 Pivot 稳定性；每个 Atlas 页面建议不超过 2048，除非目标平台验证支持并证明 4K 的收益。
5. 重复导入使用项目内 Sequence Animation Builder：先只读扫描，确认后通过 Unity AssetDatabase 重命名并重建现有正式 Clip 的 Sprite 曲线。工具必须保留 Sprite/Clip GUID、AnimationEvent 和非 Sprite 曲线，跳过空目录和已同步动作；不得直接批量改写 `.anim` YAML 或 `.meta`。

## 后果

- 批次 7 不再只是 Prefab/Scene 手工装配，还包含正式动画导入、Clip/Controller 制作、少量表现适配代码和专项验收。
- 预绑定意味着 Prefab 加载时其可达动画资源可能一并进入内存；换武器和按 ID 选择动画不会发生磁盘或网络加载，但需要按目标平台检查总体内存。
- Army、Bullet、ElementGate 的 Prefab 数量保持不变，现有对象池和配置身份不被动画资源侵入。
- 正式动画资源完成前仍可使用占位 Clip，但所有必需 Controller、参数、映射与 Monster AnimationEvent 必须完整，不能用缺失回退掩盖装配错误。
- 基础占位 Clip 永久保持无曲线；用户只向 31 个正式 Clip 填充 Sprite。空轨道与 Prefab 动画外壳使填帧可以在最终相对路径下完成，避免后续因 Animator 层级不同导致绑定丢失。

## 验收标准

- `AnimationPipeline.md` 给出源资源、Sprite、Clip、Controller、Prefab 的目录、命名、导入与装配顺序。
- `ImplementationPlan.md` 的批次 7 明确动画导入、表现适配代码、Prefab 绑定和手工验证检查点。
- 三个 WeaponId、三个 BulletId、三个 EnemyType、加法门与三种 ElementType 都有唯一且可追踪的动画绑定。
- 换武器后全部 Army 槽位使用新动画；后续激活槽位不会回到旧武器表现。
- 池化 Bullet/ElementGate 复用时不会残留上一实例的 Animator 参数、状态或帧。
- Monster Attack/Death 事件数量和位置符合既有契约；Army/Bullet/Gate 动画不承担玩法结算。
- 未使用 `Resources.Load`、运行时路径、运行时创建 OverrideController 或新增动画专用玩法 Prefab。

## 关联文档

- `../00_Project/ImplementationPlan.md`
- `../02_Modules/Army/README.md`
- `../02_Modules/Bullet/README.md`
- `../02_Modules/Monster/README.md`
- `../02_Modules/Gate/README.md`
- `../04_Assets/AnimationPipeline.md`
- `../04_Assets/PrefabSpecifications.md`
- `../05_Testing/IntegrationTests.md`
- `../07_Changes/ChangeLog.md`
