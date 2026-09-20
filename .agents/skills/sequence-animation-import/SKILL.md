---
name: sequence-animation-import
description: Scan, rename, import, rebuild, and verify this Unity repository's Army, Monster, Bullet, or Gate sequence-frame animations after PNG frames are added or replaced under Assets/Art/Sprites. Do not use for skeletal animation, runtime loading, or unrelated Unity assets.
---

# Sequence Animation Import

Use the project Editor tool at `Assets/Scripts/Tools/Editor/SequenceAnimationBuilder.cs`; do not edit `.anim` YAML or `.meta` GUIDs directly.

## Workflow

1. Read `KnowledgeBase/04_Assets/AnimationPipeline.md` and inspect `git status --short` so existing user changes remain intact.
2. Confirm Unity is not compiling or playing. Run `Tools/Game Jam/Sequence Animation Builder/Scan Report` and read the Console.
3. Report populated pending groups, frame counts, gaps, collisions, or invalid groups. Empty registered folders are expected and should be skipped.
4. Treat scanning as read-only. Run `Apply All Pending` only when the user has authorized asset mutation in the current request; otherwise stop after the report.
5. After applying, wait for import and compilation to finish, then run `Scan Report` again. Every processed group must be `UpToDate`.
6. Check the Console for errors and warnings, and verify the modified Clip frame count, 24 FPS, loop setting, binding path, first/last frame, and duration.
7. Review `git diff --check` and the scoped diff. Do not modify Controllers, OverrideControllers, Prefabs, scenes, or gameplay code unless separately requested.

## Invariants

- Rename through Unity `AssetDatabase` so Sprite GUIDs and `.meta` files are preserved.
- Canonical frame numbering starts at `0001` and is continuous. Do not guess around gaps or duplicate numbers.
- Army Sprite binding path is empty; Monster, Bullet, and Gate binding path is `Visual`.
- The tool may replace only `SpriteRenderer.m_Sprite` curves. Preserve AnimationEvents and any unrelated curves.
- Keep base placeholder Clips curve-free. Only the 31 registered formal Clips may receive frames.
- Preserve an already synchronized Clip unless the user explicitly requests a rebuild.
- If PNG contents change in place while paths and GUIDs remain stable, a Unity reimport may be sufficient. Rebuild when frame count, ordering, names, or referenced Sprite GUIDs change.
- Monster hit and completion event frame choices remain manual design inputs; never infer them from image content.
