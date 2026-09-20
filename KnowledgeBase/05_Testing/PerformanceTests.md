# 性能测试清单

当前阶段不把移动 Collider2D 的 broadphase 或 Transform 同步成本作为设计阻塞，也不提前以纯几何碰撞替换 Collider2D；进入性能阶段后再依据目标设备实测。

首轮也不实现双方相对运动扫掠、子步进或连续碰撞求解；使用 MVP 速度、Collider 尺寸和目标帧率进行验证。若实测仍发生穿透，再以复现数据单独决定算法升级。

- [ ] 同屏大量子弹时帧率稳定。
- [ ] 怪物、子弹和特效优先使用对象池。
- [ ] MVP 类型池采用懒创建且空闲实例跨 Gameplay 重开保留；在没有实测依据前不预热、不配置容量上限。
- [ ] 记录首次创建、稳定复用和场景重开后的实例数量；只有数据表明有必要时才新增预热、容量或溢出策略。
- [ ] 军队逻辑人数增加时，不创建等量 GameObject。
- [ ] 士兵 GameObject、碰撞体和发射点数量不超过所选 Army Prefab 的 `SlotCapacity = slots.Length`。
- [ ] 总人数增大时，子弹数量按激活槽位和武器发射规则受控，不按逻辑人数线性创建无限对象。
- [ ] 场景重开后没有重复的 GlobalRoot。
- [ ] 终局回到 LevelSelect 并重新进入同一关后，上一局对象已清理。
- [ ] 在目标移动设备上测试发热和内存占用。

## 序列帧动画

- [ ] 记录 Army 三套武器、三种 Monster、三个 BulletId 和四种 Gate 动画在目标平台的纹理内存、总加载时间和首帧尖峰；不以 PNG 文件体积代替运行时数据。
- [ ] 动画 Sprite 关闭 Mipmap 与 Read/Write；同一资源没有同时导入单图、供应方图集、预览 GIF 和原始绿底帧。
- [ ] 默认 Max Size 512 能满足实际屏幕显示；确需提升分辨率的对象单独记录显示尺寸、平台和清晰度依据。
- [ ] SpriteAtlas 只在多实例不同帧导致的批次或纹理切换有实测收益时建立，页面优先不超过 2048；不采用只有少量帧却占满 4K 页面的图集。
- [ ] 多个 Army 槽位、怪物和子弹同时播放时 Animator 更新开销在目标设备可接受；如成为瓶颈，再单独评估轻量 Sprite 播放器，不在批次 7 提前替换。

### 2026-09-21 批次 7.6 基线记录

- 平台口径：Unity 2022.3 Editor，Active Build Target 为 `StandaloneWindows64`；不是最终 Player 构建或移动设备数据。
- 资源口径：已收到的 12 个正式动作，共 554 张独立 PNG。全部通过 Sprite/Single、Clamp、Bilinear、Normal 压缩、无 Mipmap、Read/Write 关闭、Max Size 512 导入校验。
- 使用 `Profiler.GetRuntimeMemorySizeLong(Texture2D)` 对已加载纹理逐张求和为约 `156.17 MiB`。三组 61 帧 Bullet、Normal Move、Elite Attack、Boss Move 各约 `17.20 MiB`；W000 Attack 51 帧约 `14.38 MiB`；Elite Move 49 帧约 `13.81 MiB`；其余组约 `2.82～8.46 MiB`。
- 结论：当前数值足以说明不能仅凭 PNG 磁盘大小判断内存；在全部 31 个动作补齐前不外推最终总量。是否降到 Max Size 256、公共裁切或建立 SpriteAtlas，必须结合 9:16 实际显示尺寸、目标 Player 构建和多实例帧数据再决定。
