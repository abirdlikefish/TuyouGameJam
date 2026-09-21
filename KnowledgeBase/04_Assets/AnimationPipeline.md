# 序列帧动画导入与装配流程

## 范围与当前状态

本文是批次 7 正式动画资源的执行入口，覆盖 Army、Monster、Bullet 和 Gate 的源资源整理、Sprite 导入、AnimationClip、Animator Controller、Prefab 预绑定与手工验证。

- 状态：`InProgress`（81 个 Clip、5 个 Controller、13 个 OverrideController、7 个动画 Prefab 与三类 Monster AnimationEvent 已完成绑定；当前 39 个正式 Clip 共 695 帧已按 8 FPS 导入，剩余 34 个动作素材、竖屏目标分辨率与 Player 内存验收待完成）
- 决策：[ADR-048](../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md)、[ADR-049](../06_Decisions/ADR-049-ContinuousArmyCombatAnimationAndVictoryPresentation.md)、[ADR-057](../06_Decisions/ADR-057-ElementalStaffWeaponVariants.md)
- 不包含：运行时资源下载、Addressables、Resources 路径加载、骨骼动画、音频、VFX、WeaponProp 正式动画。

## 目录边界

```text
Reference/AnimationSource/             原始导出包，不由 Unity 导入

Assets/Art/Sprites/                    只保存最终透明 PNG 帧
├── Army/Weapons/Weapon_000_Slingshot/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_001_Bow/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_002_Staff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_003_FireStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_004_IceStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_005_LightningStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_006_FireIceStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_007_FireLightningStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_008_IceLightningStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Army/Weapons/Weapon_009_FireIceLightningStaff/{Idle,Victory,Attack,MoveLeft,MoveRight}
├── Monsters/{Normal,Elite,Boss}/{Move,Attack,Death}
├── Bullets/Bullet_000/Loop
├── Bullets/Bullet_001/Loop
├── Bullets/Bullet_002/Loop
├── Bullets/Bullet_003/Loop
├── Bullets/Bullet_004/Loop
├── Bullets/Bullet_005/Loop
├── Bullets/Bullet_006/Loop
├── Bullets/Bullet_007/Loop
├── Bullets/Bullet_008/Loop
├── Bullets/Bullet_009/Loop
├── Gates/Additive/Loop
└── Gates/Element/{Fire,Ice,Lightning}/Loop

Assets/Animations/
├── Army/Base                         5 个基础占位 Clip + AC_Army_Base
├── Army/Weapons/Weapon_000_Slingshot/Clips
├── Army/Weapons/Weapon_001_Bow/Clips
├── Army/Weapons/Weapon_002_Staff/Clips
├── Army/Weapons/Weapon_003_FireStaff/Clips
├── Army/Weapons/Weapon_004_IceStaff/Clips
├── Army/Weapons/Weapon_005_LightningStaff/Clips
├── Army/Weapons/Weapon_006_FireIceStaff/Clips
├── Army/Weapons/Weapon_007_FireLightningStaff/Clips
├── Army/Weapons/Weapon_008_IceLightningStaff/Clips
├── Army/Weapons/Weapon_009_FireIceLightningStaff/Clips
├── Army/Weapons/Weapon_XXX_*         每种武器目录根保存一个 AOC
├── Monsters/Base                     3 个基础占位 Clip + AC_Monster_Base
├── Monsters/Types/{Normal,Elite,Boss}/Clips
├── Monsters/Types/{Normal,Elite,Boss} 每种怪物目录根保存一个 AOC
├── Bullets/{Clips,Controllers}
└── Gates/{Clips,Controllers}

Assets/Art/SpriteAtlases/{Army,Monsters,Bullets,Gates}
```

原始包中的 `original/`、`preview.gif`、`animation.json` 和供应方 `atlases/` 只用于核对，不复制到 `Assets`。只把经过尺寸、透明边缘与命名检查的 `transparent/` 帧复制到对应 Sprite 目录。

敌人领域身份现为 `Chick`、`Hen`、`Rooster`，但本动画管线继续保留 `Normal`、`Elite`、`Boss` 技术身份，映射依次为 Chick→Normal、Hen→Elite、Rooster→Boss。现有 Sprite、Clip、AOC、目录和 Sequence Animation Builder 字符串不随领域命名重构。

## 命名

```text
SPR_Army_W000_Attack_0001.png
AN_Army_W000_Attack.anim
AOC_Army_W000_Slingshot.overrideController
AC_Army_Base.controller

SPR_Monster_Normal_Attack_0001.png
AN_Monster_Normal_Attack.anim

SPR_Bullet_000_Loop_0001.png
AN_Bullet_000_Loop.anim

SPR_Gate_Fire_Loop_0001.png
AN_Gate_Fire_Loop.anim
```

- `SPR_`：Sprite 源帧。
- `AN_`：AnimationClip。
- `AC_`：Animator Controller。
- `AOC_`：AnimatorOverrideController。
- `W000`～`W009` 与当前 `WeaponId 0`～`9` 对应。
- `Bullet_000`～`009` 与当前 `BulletId 0`～`9` 对应。
- 帧编号固定四位，从 `0001` 连续递增，不允许缺号或自然排序歧义。

## 统一导入设置

对同一动作的全部 PNG 一次性设置：

| 设置 | 基线值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Alpha Source | Input Texture Alpha |
| Alpha Is Transparency | 开启 |
| Read/Write Enabled | 关闭 |
| Generate Mip Maps | 关闭 |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Compression | Normal；出现色边时再比较 High Quality |
| Max Size | 先用 512；实机不清晰时有证据地上调 |
| Pixels Per Unit | 512 |
| Pivot | 同一对象家族保持同一基准；Army 优先保持脚底/根位置稳定 |

导入后在深、浅背景上检查透明边缘，尤其是绿幕移除素材。不得单独自动裁切每一帧后仍使用相同中心 Pivot；需要裁切时应使用全动作公共裁切框，或按原画布偏移重建等价 Pivot。

## 重复导入工具

`Assets/Scripts/Tools/Editor/SequenceAnimationBuilder.cs` 登记当前 73 个正式动作，提供以下 Unity 菜单：

- `Tools/Game Jam/Sequence Animation Builder/Open`：打开可视化窗口，扫描后选择性应用。
- `Tools/Game Jam/Sequence Animation Builder/Scan Report`：只读扫描并把每个动作的状态、帧数和问题写入 Console。
- `Tools/Game Jam/Sequence Animation Builder/Apply All Pending`：处理所有非空且待更新的有效动作；空目录、无效动作和已同步动作不会修改。
- `Tools/Game Jam/Sequence Animation Builder/Create Missing Registered Assets`：为已登记身份补齐目录、空 Clip、Army AOC、Bullet 状态与 Army Prefab 映射；重复执行保持幂等。

工具按帧号而非字符串排序，要求编号从 `0001` 连续递增；发现缺号、重复号、不同尺寸、目标 Clip 缺失或无法导入时停止该次操作。应用时通过 `AssetDatabase.RenameAsset` 保留 Sprite GUID，统一本页导入参数，并只替换目标 Clip 的 `SpriteRenderer.m_Sprite` 曲线；Clip GUID、AnimationEvent、其他曲线、Controller、OverrideController、Prefab 和场景不变。

如果只在原路径覆盖 PNG 内容且保留 `.meta`、帧数和顺序不变，Unity 重新导入即可；帧数、顺序、名称或 Sprite GUID 变化时，应先运行 Scan Report，再通过窗口或 Apply All Pending 重建。仓库内 `.agents/skills/sequence-animation-import/SKILL.md` 记录 Codex 重复执行此流程时的授权和验证边界。

截至 2026-09-21，工具已同步 W000～W009 的 Attack、MoveLeft、MoveRight，Normal/Elite/Boss 的 Move 与 Attack，以及 BulletId 0/1/2 Loop；共 39 个正式 Clip、695 帧。各武器的 Idle/Victory、三种 Monster Death、BulletId 3～9 Loop 和四种 Gate Loop 共 34 个动作保持空轨道。

## Clip 与循环规则

| 对象 | Clip | Loop Time | 额外要求 |
|---|---|---:|---|
| Army 每种武器 | Idle | 是 | 默认静止状态 |
| Army 每种武器 | MoveLeft / MoveRight | 是 | 方向不可串换 |
| Army 每种武器 | Attack | 是 | 原地持续攻击；不通过 AnimationEvent 发射子弹 |
| Army 每种武器 | Victory | 否 | 无出口并停留末帧；素材明确可循环时再改 |
| 每种 Monster | Move | 是 | 默认状态 |
| 每种 Monster | Attack | 否 | 一个命中事件和一个结束事件 |
| 每种 Monster | Death | 否 | 末帧一个回收登记事件 |
| 每个 BulletId | Loop | 是 | 不含玩法事件 |
| AdditiveGate | Loop | 是 | 不含玩法事件 |
| 每种 ElementType | Loop | 是 | 不含玩法事件 |

Clip 的 Samples 当前统一为 8 FPS。生成后必须核对最后一帧持有一个完整采样间隔：第 `N` 帧关键帧时间为 `(N-1)/8` 秒，Clip 停止时间为 `N/8` 秒。

当前动画骨架统一使用 8 FPS。73 个正式 Clip 已预建一个时间 `0`、值为 `None` 的 `SpriteRenderer.Sprite` 轨道：Army 的绑定路径为空字符串，因为 Animator 与 SpriteRenderer 同在 `SoldierVisual`；Monster、Bullet、Gate 的绑定路径为 `Visual`。8 个基础占位 Clip 不含任何曲线，只作为 OverrideController 的稳定替换键，不拖入正式图片。

填帧时打开对应 Prefab，在 Animation 窗口选中正式 Clip，把按帧号排序的 Sprite 从第 0 帧拖到已存在的 Sprite 轨道；若 Unity 没有覆盖初始 `None` 键，则手动删除该键。不得向 `AN_Army_Base_*` 或 `AN_Monster_Base_*` 填帧。

## Controller 与 Prefab 映射

### Army

`AC_Army_Base` 固定包含 `Idle`、`MoveLeft`、`MoveRight`、`Attack`、`Victory`，不包含 Animator 参数或自动 Transition。ArmyController 只在状态变化时通过固定状态哈希显式播放：Preparing 为 Idle，Playing 原地为 Attack，实际左/右位移为 MoveLeft/MoveRight，Victory 为终态。Idle、Attack、MoveLeft、MoveRight 循环，Victory 非循环。每个武器创建一个预制 `AnimatorOverrideController`，完整覆盖五个基础占位 Clip。

`PF_Army_000` 保存唯一的 `WeaponId -> AnimatorOverrideController` 数组，完整覆盖 WeaponId 0～9；每个 `ArmySlotView` 显式绑定本槽位 `soldierAnimator`。初始 `WeaponId=0` 和每次实际换武器时，ArmyController 把选中的 Controller 应用到所有槽位。Attack、MoveLeft、MoveRight 的 Clip 时长必须与该 WeaponId 的 `FireInterval` 一致；三种战斗状态切换时按已有攻击周期 normalized time 进入新状态。战斗中换武器后从当前战斗状态第 0 帧重播并立即发射，不先回 Idle；隐藏槽位保存当前武器与状态，激活时再从第 0 帧播放。

### Monster

`AC_Monster_Base` 的公共状态结构为默认 `Move`，`Attack` Trigger 进入非循环 Attack 并在结束后回 Move，`Death` Trigger 从任意状态进入非循环 Death 且不退出。Normal、Elite、Boss 各使用一个预创建 OverrideController，完整覆盖 Move、Attack、Death 三个基础占位 Clip。

Attack Clip：

- 命中关键帧恰好一个 `OnAttackFrame()`。
- 末帧恰好一个 `OnAttackAnimationFinished()`。

Death Clip：

- 末帧恰好一个 `OnDeathAnimationFinished()`。

### Bullet

`PF_Bullet` 保持一个规范 Prefab。`AC_Bullet` 以整数参数 `BulletId` 选择 `Bullet_000_Loop`～`Bullet_009_Loop`。初始化池对象时先设置 ID 和目标状态，再激活对象。

### Gate

`PF_Gate_Additive` 绑定 `AC_Gate_Additive` 的单循环状态。`PF_Gate_Element` 绑定 `AC_Gate_Element`，以整数参数 `ElementType` 选择 `Fire`、`Ice`、`Lightning`。元素门每次从池中借出时先写入当前类型并重置动画。

### 当前 Prefab 动画外壳

- `PF_Army_000` 已建立 `Slots/Slot_00/SoldierVisual`；SoldierVisual 挂载 SpriteRenderer、Animator 并绑定 W000 OverrideController。
- 三个 Monster、Bullet 和两个 Gate Prefab 均在根节点挂载 Animator，并建立 `Visual` 子节点与 SpriteRenderer；Animator 已绑定对应 Controller 或 OverrideController，Root Motion 关闭。
- 上述内容只保证 Animation 窗口拥有正确绑定上下文，不代表 Prefab 已完成玩法脚本、Collider、代理、文本、槽位数组或批次 7.4 字段装配。

## 批次 7 执行顺序

### 7.1 源资源清点

- 按动画资产矩阵列出已收到、缺失和需要返工的动作。
- 对每个导出包核对帧数、分辨率、Alpha、帧率、循环意图和首尾连续性。
- 原始导出包归档到 `Reference/AnimationSource`，不直接拖入 Assets。

### 7.2 Sprite 导入

- 只复制最终透明帧到 `Assets/Art/Sprites` 对应目录。
- 使用 Sequence Animation Builder 先扫描，再统一命名、导入设置、PPU 和 Clip Sprite 曲线；Pivot 仍按素材家族人工核对。
- 在 Unity 中 Apply 后检查 Console、Sprite 预览、透明边缘与内存估算。

### 7.3 Clip 与 Controller 制作

- 动画骨架已完成：81 个 Clip、Army/Monster 基础状态机、十个 Army OverrideController、三个 Monster OverrideController、Bullet Controller 和两个 Gate Controller 已建立。
- 当前 39 个正式 Clip 已填入 695 帧并通过工具核对；后续素材继续用相同工具写入剩余 34 个正式 Clip，并核对 Samples、Loop Time、首尾持帧与 Sprite 绑定路径。
- 正式帧位置确认后再添加并核对 Monster AnimationEvent；当前空 Clip 不预设事件。

### 7.4 动画表现适配代码

- Army：已增加 WeaponId 动画映射、槽位 Animator 引用、全槽位 Controller 切换、按实际位移显式选择持续战斗状态，以及 Victory 表现阶段；Prefab 字段仍待 7.5 绑定。
- Bullet：已增加 Animator 引用、`BulletId 0`～`9` 显式状态选择、激活时从第 0 帧起播和池复用重置。
- ElementGate：已增加 Animator 引用、Fire/Ice/Lightning 显式状态选择、激活时从第 0 帧起播和池复用重置。
- AdditiveGate：已增加 Animator 必需引用验证、激活时重播 `Additive_Loop` 和池复用重置；默认循环不依赖运行时参数。
- Monster：已在每次借出时清除 Attack/Death Trigger 并从 Move 第 0 帧起播，回池时重绑 Animator，避免复用死亡状态和旧 Sprite。
- 表现代码只读取已有运行时 ID/状态，不更改玩法权威值，不加载资源。

### 7.5 Prefab 与场景绑定

- 将 Animator、Controller、OverrideController 和映射全部绑定到规范 Prefab。
- 检查池化对象首次借出、归还、再次借出不会残留旧状态。
- 检查 PF_Army_000 的未激活槽位也能预先切换到当前武器 Controller。

### 7.6 手工验收

- 分别触发 Army 静止、左右移动、攻击、胜利与十种 WeaponId 切换，额外验证元素法杖的组合与降级。
- 分别生成三种怪物并完成移动、攻击判定和死亡回收。
- 分别生成十个 BulletId，确认循环动画、方向、命中和回池。
- 分别生成加法门及三种元素门，确认动画与运行时类型匹配。
- 使用 Profiler/Memory Profiler 或 Unity Inspector 记录目标平台的纹理内存；发现单动作过大时优先降低 Max Size、统一裁切或重新打包，不直接启用供应方 4K 图集。

2026-09-21 执行记录：

- 扫描结果为 39 个已同步、0 个待更新、34 个空目录、0 个非法动作；已有内容总计 695 帧。
- Controller、AOC、Prefab 必需引用无 Missing；Army 已绑定十种武器 AOC，Bullet Controller 已绑定十种 ID。既有 Army 三武器、Bullet 三 ID、Monster 三类型和 Gate 四身份的运行态验证仍有效；新增元素法杖已填 Attack/MoveLeft/MoveRight 并待运行态视觉验收，BulletId 3～9 仍待序列帧。
- 现有 695 张 Sprite 已统一为 Sprite/Single、PPU 512、Normal 压缩、关闭 Mipmap/ReadWrite、Clamp、Bilinear，导入最大尺寸 512；素材变更后的纹理内存需重新采样，旧的 554 帧内存记录不再作为当前基线。
- 当前结果证明资源身份、导入参数、Clip 曲线与绑定关系正确；34 个空动作不能验收画面连续性，新增元素法杖也仍需运行态视觉验收，最终 Player 构建内存、9:16 与多分辨率仍待验证。

## 完成门

- 所有必需动作有连续帧、正确 Clip、正确循环标记和唯一资源名称。
- 所有 Controller 参数、状态和 Override 项完整，无 Missing Motion。
- 所有 Prefab 显式引用通过 Preparing 校验，不存在运行时搜索或默认回退。
- WeaponId、BulletId、EnemyType、ElementType 与表现一一对应。
- Monster AnimationEvent 次数、方法名和关键帧位置正确。
- Unity 无导入和编译错误；核心动画状态、对象池复用和完整游玩闭环完成手工验证。

## 关联文档

- [实施计划](../00_Project/ImplementationPlan.md)
- [Prefab 规格](PrefabSpecifications.md)
- [美术资源清单](ArtList.md)
- [集成测试](../05_Testing/IntegrationTests.md)
- [ADR-048](../06_Decisions/ADR-048-AnimationAssetPipelineAndPrefabBindings.md)
- [ADR-049](../06_Decisions/ADR-049-ContinuousArmyCombatAnimationAndVictoryPresentation.md)
