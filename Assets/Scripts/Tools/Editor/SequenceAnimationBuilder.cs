using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.Editor
{
    public sealed class SequenceAnimationBuilderWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<SequenceAnimationBuilder.ScanResult> results = new List<SequenceAnimationBuilder.ScanResult>();

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Open")]
        private static void Open()
        {
            var window = GetWindow<SequenceAnimationBuilderWindow>("Sequence Animation Builder");
            window.minSize = new Vector2(760f, 420f);
            window.Scan();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "扫描 Assets/Art/Sprites 中已登记的序列帧。只有选中的待更新项会被重命名、规范化并写入现有 Clip；空目录、无效序列和已同步项不会修改。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描", GUILayout.Width(100f)))
                {
                    Scan();
                }

                if (GUILayout.Button("选择待更新", GUILayout.Width(120f)))
                {
                    foreach (var result in results)
                    {
                        result.Selected = result.Status == SequenceAnimationBuilder.SequenceStatus.Pending;
                    }
                }

                if (GUILayout.Button("清除选择", GUILayout.Width(100f)))
                {
                    foreach (var result in results)
                    {
                        result.Selected = false;
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!results.Any(result => result.Selected)))
                {
                    if (GUILayout.Button("应用选中项", GUILayout.Width(120f)))
                    {
                        ApplySelected();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("选择", GUILayout.Width(42f));

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var result in results)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(result.Status != SequenceAnimationBuilder.SequenceStatus.Pending))
                        {
                            result.Selected = EditorGUILayout.Toggle(result.Selected, GUILayout.Width(24f));
                        }

                        EditorGUILayout.LabelField(result.Definition.Label, GUILayout.Width(230f));
                        EditorGUILayout.LabelField(result.Status.ToString(), GUILayout.Width(85f));
                        EditorGUILayout.LabelField($"{result.Frames.Count} 帧", GUILayout.Width(65f));
                        EditorGUILayout.LabelField(result.Details);
                    }

                    EditorGUILayout.LabelField(result.Definition.SpriteFolder, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(result.Definition.ClipPath, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            results = SequenceAnimationBuilder.ScanAll();
            Repaint();
        }

        private void ApplySelected()
        {
            var selectedIds = results
                .Where(result => result.Selected)
                .Select(result => result.Definition.Id)
                .ToArray();

            var frameCount = results
                .Where(result => result.Selected)
                .Sum(result => result.Frames.Count);

            if (!EditorUtility.DisplayDialog(
                    "更新序列帧动画",
                    $"即将更新 {selectedIds.Length} 个 Clip、{frameCount} 张 Sprite。\n\n操作会保留 Sprite 和 Clip GUID，但会替换目标 Clip 的 Sprite 曲线。是否继续？",
                    "继续",
                    "取消"))
            {
                return;
            }

            SequenceAnimationBuilder.Apply(selectedIds);
            Scan();
        }
    }

    public static class SequenceAnimationBuilder
    {
        private const int DefaultFrameRate = 24;
        private const int DefaultMaxTextureSize = 512;
        private const float DefaultPixelsPerUnit = 512f;
        private const string SpritePropertyName = "m_Sprite";

        private static readonly Regex FrameNumberRegex = new Regex(
            @"(\d+)(?=\.png$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<SequenceDefinition> Definitions = BuildDefinitions();

        public enum SequenceStatus
        {
            Empty,
            Pending,
            UpToDate,
            Invalid
        }

        public sealed class SequenceDefinition
        {
            public string Id { get; }
            public string Label { get; }
            public string SpriteFolder { get; }
            public string ClipPath { get; }
            public string SpritePrefix { get; }
            public string BindingPath { get; }
            public bool Loop { get; }
            public int FrameRate { get; }

            public SequenceDefinition(
                string id,
                string label,
                string spriteFolder,
                string clipPath,
                string spritePrefix,
                string bindingPath,
                bool loop,
                int frameRate = DefaultFrameRate)
            {
                Id = id;
                Label = label;
                SpriteFolder = spriteFolder;
                ClipPath = clipPath;
                SpritePrefix = spritePrefix;
                BindingPath = bindingPath;
                Loop = loop;
                FrameRate = frameRate;
            }
        }

        public sealed class FrameAsset
        {
            public int Number { get; }
            public string Guid { get; }
            public string Path { get; }

            public FrameAsset(int number, string guid, string path)
            {
                Number = number;
                Guid = guid;
                Path = path;
            }
        }

        public sealed class ScanResult
        {
            public SequenceDefinition Definition { get; }
            public List<FrameAsset> Frames { get; }
            public SequenceStatus Status { get; }
            public string Details { get; }
            public bool Selected { get; set; }

            public ScanResult(
                SequenceDefinition definition,
                List<FrameAsset> frames,
                SequenceStatus status,
                string details)
            {
                Definition = definition;
                Frames = frames;
                Status = status;
                Details = details;
                Selected = status == SequenceStatus.Pending;
            }
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Scan Report")]
        public static void LogScanReport()
        {
            var results = ScanAll();
            foreach (var result in results)
            {
                Debug.Log(
                    $"[SequenceAnimationBuilder] {result.Definition.Id}: {result.Status}, {result.Frames.Count} frames, {result.Details}");
            }

            Debug.Log(
                $"[SequenceAnimationBuilder] Scan complete: " +
                $"{results.Count(result => result.Status == SequenceStatus.UpToDate)} up to date, " +
                $"{results.Count(result => result.Status == SequenceStatus.Pending)} pending, " +
                $"{results.Count(result => result.Status == SequenceStatus.Empty)} empty, " +
                $"{results.Count(result => result.Status == SequenceStatus.Invalid)} invalid.");
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Apply All Pending")]
        public static void ApplyAllPending()
        {
            var results = ScanAll();
            var invalid = results.Where(result => result.Status == SequenceStatus.Invalid).ToArray();
            if (invalid.Length > 0)
            {
                Debug.LogError(BuildInvalidMessage(invalid));
                return;
            }

            Apply(results
                .Where(result => result.Status == SequenceStatus.Pending)
                .Select(result => result.Definition.Id));
        }

        public static List<ScanResult> ScanAll()
        {
            return Definitions.Select(Scan).ToList();
        }

        public static void Apply(IEnumerable<string> sequenceIds)
        {
            var requestedIds = new HashSet<string>(sequenceIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (requestedIds.Count == 0)
            {
                Debug.Log("[SequenceAnimationBuilder] No sequences selected.");
                return;
            }

            var unknownIds = requestedIds.Where(id => Definitions.All(definition => definition.Id != id)).ToArray();
            if (unknownIds.Length > 0)
            {
                Debug.LogError($"[SequenceAnimationBuilder] Unknown sequence IDs: {string.Join(", ", unknownIds)}");
                return;
            }

            var initialResults = Definitions
                .Where(definition => requestedIds.Contains(definition.Id))
                .Select(Scan)
                .ToArray();

            var invalid = initialResults
                .Where(result => result.Status == SequenceStatus.Invalid || result.Status == SequenceStatus.Empty)
                .ToArray();
            if (invalid.Length > 0)
            {
                Debug.LogError(BuildInvalidMessage(invalid));
                return;
            }

            var pending = initialResults
                .Where(result => result.Status == SequenceStatus.Pending)
                .ToArray();
            if (pending.Length == 0)
            {
                Debug.Log("[SequenceAnimationBuilder] Selected sequences are already up to date.");
                return;
            }

            var appliedSequenceCount = 0;
            var appliedFrameCount = 0;

            try
            {
                for (var index = 0; index < pending.Length; index++)
                {
                    var result = pending[index];
                    EditorUtility.DisplayProgressBar(
                        "更新序列帧动画",
                        $"{result.Definition.Label} ({index + 1}/{pending.Length})",
                        (float)index / pending.Length);

                    RenameFrames(result.Definition, result.Frames);
                    var renamedResult = Scan(result.Definition);
                    if (renamedResult.Status == SequenceStatus.Invalid || renamedResult.Status == SequenceStatus.Empty)
                    {
                        throw new InvalidOperationException(
                            $"{result.Definition.Id} failed validation after rename: {renamedResult.Details}");
                    }

                    NormalizeImportSettings(renamedResult.Frames);
                    var importedResult = Scan(result.Definition);
                    if (importedResult.Status == SequenceStatus.Invalid || importedResult.Status == SequenceStatus.Empty)
                    {
                        throw new InvalidOperationException(
                            $"{result.Definition.Id} failed validation after import: {importedResult.Details}");
                    }

                    RebuildClip(result.Definition, importedResult.Frames);
                    appliedSequenceCount++;
                    appliedFrameCount += importedResult.Frames.Count;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var verification = Definitions
                    .Where(definition => requestedIds.Contains(definition.Id))
                    .Select(Scan)
                    .Where(result => result.Status != SequenceStatus.UpToDate)
                    .ToArray();

                if (verification.Length > 0)
                {
                    Debug.LogError(BuildInvalidMessage(verification));
                    return;
                }

                Debug.Log(
                    $"[SequenceAnimationBuilder] Applied {appliedSequenceCount} sequences and {appliedFrameCount} frames. All selected clips are up to date.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static ScanResult Scan(SequenceDefinition definition)
        {
            if (!AssetDatabase.IsValidFolder(definition.SpriteFolder))
            {
                return new ScanResult(
                    definition,
                    new List<FrameAsset>(),
                    SequenceStatus.Invalid,
                    "Sprite folder does not exist.");
            }

            var assetGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { definition.SpriteFolder });
            var frames = new List<FrameAsset>();
            foreach (var guid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        (Path.GetDirectoryName(path) ?? string.Empty).Replace('\\', '/'),
                        definition.SpriteFolder,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var match = FrameNumberRegex.Match(path);
                if (!match.Success || !int.TryParse(match.Value, out var number) || number <= 0)
                {
                    return new ScanResult(
                        definition,
                        frames,
                        SequenceStatus.Invalid,
                        $"Cannot read a positive frame number from {Path.GetFileName(path)}.");
                }

                frames.Add(new FrameAsset(number, guid, path));
            }

            frames.Sort((left, right) => left.Number.CompareTo(right.Number));
            if (frames.Count == 0)
            {
                return new ScanResult(definition, frames, SequenceStatus.Empty, "No PNG frames.");
            }

            for (var index = 0; index < frames.Count; index++)
            {
                var expectedNumber = index + 1;
                if (frames[index].Number != expectedNumber)
                {
                    return new ScanResult(
                        definition,
                        frames,
                        SequenceStatus.Invalid,
                        $"Frame numbers must be unique and continuous from 0001; expected {expectedNumber:0000}, found {frames[index].Number:0000}.");
                }
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);
            if (clip == null)
            {
                return new ScanResult(definition, frames, SequenceStatus.Invalid, "Target AnimationClip is missing.");
            }

            var dimensions = new HashSet<Vector2Int>();
            var importerMismatchCount = 0;
            var nameMismatchCount = 0;
            foreach (var frame in frames)
            {
                var expectedName = GetExpectedFileName(definition, frame.Number);
                if (!string.Equals(Path.GetFileName(frame.Path), expectedName, StringComparison.Ordinal))
                {
                    nameMismatchCount++;
                }

                var importer = AssetImporter.GetAtPath(frame.Path) as TextureImporter;
                if (importer == null)
                {
                    return new ScanResult(
                        definition,
                        frames,
                        SequenceStatus.Invalid,
                        $"TextureImporter cannot be loaded: {frame.Path}");
                }

                importer.GetSourceTextureWidthAndHeight(out var sourceWidth, out var sourceHeight);
                dimensions.Add(new Vector2Int(sourceWidth, sourceHeight));

                if (!HasExpectedImportSettings(importer))
                {
                    importerMismatchCount++;
                }
            }

            if (dimensions.Count != 1)
            {
                return new ScanResult(
                    definition,
                    frames,
                    SequenceStatus.Invalid,
                    "All frames in one sequence must have identical imported dimensions.");
            }

            var clipCurrent = IsClipCurrent(definition, frames, clip);
            if (nameMismatchCount == 0 && importerMismatchCount == 0 && clipCurrent)
            {
                return new ScanResult(definition, frames, SequenceStatus.UpToDate, "Names, importer and clip match.");
            }

            var details = new List<string>();
            if (nameMismatchCount > 0)
            {
                details.Add($"rename {nameMismatchCount}");
            }

            if (importerMismatchCount > 0)
            {
                details.Add($"importer {importerMismatchCount}");
            }

            if (!clipCurrent)
            {
                details.Add("rebuild clip");
            }

            return new ScanResult(definition, frames, SequenceStatus.Pending, string.Join(", ", details));
        }

        private static bool IsClipCurrent(
            SequenceDefinition definition,
            IReadOnlyList<FrameAsset> frames,
            AnimationClip clip)
        {
            if (Mathf.Abs(clip.frameRate - definition.FrameRate) > 0.001f)
            {
                return false;
            }

            var clipSettings = new SerializedObject(clip).FindProperty("m_AnimationClipSettings");
            if (clipSettings == null ||
                clipSettings.FindPropertyRelative("m_LoopTime").boolValue != definition.Loop ||
                Mathf.Abs(
                    clipSettings.FindPropertyRelative("m_StopTime").floatValue -
                    (float)frames.Count / definition.FrameRate) > 0.001f)
            {
                return false;
            }

            var spriteBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .Where(IsSpriteBinding)
                .ToArray();
            if (spriteBindings.Length != 1 || spriteBindings[0].path != definition.BindingPath)
            {
                return false;
            }

            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBindings[0]);
            if (keyframes == null || keyframes.Length != frames.Count)
            {
                return false;
            }

            for (var index = 0; index < frames.Count; index++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(frames[index].Path);
                if (sprite == null ||
                    keyframes[index].value != sprite ||
                    Mathf.Abs(keyframes[index].time - (float)index / definition.FrameRate) > 0.0001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RenameFrames(
            SequenceDefinition definition,
            IReadOnlyList<FrameAsset> frames)
        {
            var renameFrames = frames
                .Where(frame => !string.Equals(
                    Path.GetFileName(frame.Path),
                    GetExpectedFileName(definition, frame.Number),
                    StringComparison.Ordinal))
                .ToArray();
            if (renameFrames.Length == 0)
            {
                return;
            }

            foreach (var frame in renameFrames)
            {
                var targetPath = $"{definition.SpriteFolder}/{GetExpectedFileName(definition, frame.Number)}";
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(targetPath)))
                {
                    throw new InvalidOperationException($"Rename target already exists: {targetPath}");
                }
            }

            foreach (var frame in renameFrames)
            {
                var error = AssetDatabase.RenameAsset(frame.Path, $"__seqtmp_{frame.Guid}");
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException($"Temporary rename failed for {frame.Path}: {error}");
                }
            }

            foreach (var frame in renameFrames)
            {
                var currentPath = AssetDatabase.GUIDToAssetPath(frame.Guid);
                var targetNameWithoutExtension = $"{definition.SpritePrefix}_{frame.Number:0000}";
                var error = AssetDatabase.RenameAsset(currentPath, targetNameWithoutExtension);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException($"Final rename failed for {currentPath}: {error}");
                }
            }
        }

        private static void NormalizeImportSettings(IReadOnlyList<FrameAsset> frames)
        {
            foreach (var frame in frames)
            {
                var path = AssetDatabase.GUIDToAssetPath(frame.Guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException($"TextureImporter cannot be loaded: {path}");
                }

                if (HasExpectedImportSettings(importer))
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = DefaultMaxTextureSize;
                importer.spritePixelsPerUnit = DefaultPixelsPerUnit;
                importer.SaveAndReimport();
            }
        }

        private static void RebuildClip(
            SequenceDefinition definition,
            IReadOnlyList<FrameAsset> frames)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"AnimationClip cannot be loaded: {definition.ClipPath}");
            }

            Undo.RegisterCompleteObjectUndo(clip, $"Rebuild {clip.name}");
            var existingEvents = AnimationUtility.GetAnimationEvents(clip);

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip).Where(IsSpriteBinding))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            }

            var targetBinding = new EditorCurveBinding
            {
                path = definition.BindingPath,
                type = typeof(SpriteRenderer),
                propertyName = SpritePropertyName
            };

            var keyframes = new ObjectReferenceKeyframe[frames.Count];
            for (var index = 0; index < frames.Count; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(frames[index].Guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    throw new InvalidOperationException($"Sprite cannot be loaded after import: {path}");
                }

                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = (float)index / definition.FrameRate,
                    value = sprite
                };
            }

            clip.frameRate = definition.FrameRate;
            AnimationUtility.SetObjectReferenceCurve(clip, targetBinding, keyframes);

            var serializedClip = new SerializedObject(clip);
            var clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            clipSettings.FindPropertyRelative("m_StartTime").floatValue = 0f;
            clipSettings.FindPropertyRelative("m_StopTime").floatValue = (float)frames.Count / definition.FrameRate;
            clipSettings.FindPropertyRelative("m_LoopTime").boolValue = definition.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();

            AnimationUtility.SetAnimationEvents(clip, existingEvents);
            EditorUtility.SetDirty(clip);
        }

        private static bool HasExpectedImportSettings(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite &&
                   importer.spriteImportMode == SpriteImportMode.Single &&
                   importer.alphaSource == TextureImporterAlphaSource.FromInput &&
                   importer.alphaIsTransparency &&
                   !importer.isReadable &&
                   !importer.mipmapEnabled &&
                   importer.wrapMode == TextureWrapMode.Clamp &&
                   importer.filterMode == FilterMode.Bilinear &&
                   importer.textureCompression == TextureImporterCompression.Compressed &&
                   importer.maxTextureSize == DefaultMaxTextureSize &&
                   Mathf.Abs(importer.spritePixelsPerUnit - DefaultPixelsPerUnit) < 0.001f;
        }

        private static bool IsSpriteBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SpriteRenderer) && binding.propertyName == SpritePropertyName;
        }

        private static string GetExpectedFileName(SequenceDefinition definition, int frameNumber)
        {
            return $"{definition.SpritePrefix}_{frameNumber:0000}.png";
        }

        private static string BuildInvalidMessage(IEnumerable<ScanResult> invalidResults)
        {
            var lines = invalidResults.Select(
                result => $"{result.Definition.Id}: {result.Status}, {result.Details}");
            return "[SequenceAnimationBuilder] Operation stopped because one or more sequences are not valid:\n" +
                   string.Join("\n", lines);
        }

        private static IReadOnlyList<SequenceDefinition> BuildDefinitions()
        {
            var definitions = new List<SequenceDefinition>();

            var weapons = new[]
            {
                new { Folder = "Weapon_000_Slingshot", Id = "W000" },
                new { Folder = "Weapon_001_Bow", Id = "W001" },
                new { Folder = "Weapon_002_Staff", Id = "W002" }
            };
            var armyActions = new[]
            {
                new { Name = "Idle", Loop = true },
                new { Name = "Victory", Loop = false },
                new { Name = "Attack", Loop = true },
                new { Name = "MoveLeft", Loop = true },
                new { Name = "MoveRight", Loop = true }
            };
            foreach (var weapon in weapons)
            {
                foreach (var action in armyActions)
                {
                    definitions.Add(new SequenceDefinition(
                        $"Army_{weapon.Id}_{action.Name}",
                        $"Army {weapon.Id} {action.Name}",
                        $"Assets/Art/Sprites/Army/Weapons/{weapon.Folder}/{action.Name}",
                        $"Assets/Animations/Army/Weapons/{weapon.Folder}/Clips/AN_Army_{weapon.Id}_{action.Name}.anim",
                        $"SPR_Army_{weapon.Id}_{action.Name}",
                        string.Empty,
                        action.Loop));
                }
            }

            var monsterTypes = new[] { "Normal", "Elite", "Boss" };
            var monsterActions = new[]
            {
                new { Name = "Move", Loop = true },
                new { Name = "Attack", Loop = false },
                new { Name = "Death", Loop = false }
            };
            foreach (var monsterType in monsterTypes)
            {
                foreach (var action in monsterActions)
                {
                    definitions.Add(new SequenceDefinition(
                        $"Monster_{monsterType}_{action.Name}",
                        $"Monster {monsterType} {action.Name}",
                        $"Assets/Art/Sprites/Monsters/{monsterType}/{action.Name}",
                        $"Assets/Animations/Monsters/Types/{monsterType}/Clips/AN_Monster_{monsterType}_{action.Name}.anim",
                        $"SPR_Monster_{monsterType}_{action.Name}",
                        "Visual",
                        action.Loop));
                }
            }

            for (var bulletId = 0; bulletId <= 2; bulletId++)
            {
                var formattedId = bulletId.ToString("000");
                definitions.Add(new SequenceDefinition(
                    $"Bullet_{formattedId}_Loop",
                    $"Bullet {formattedId} Loop",
                    $"Assets/Art/Sprites/Bullets/Bullet_{formattedId}/Loop",
                    $"Assets/Animations/Bullets/Clips/AN_Bullet_{formattedId}_Loop.anim",
                    $"SPR_Bullet_{formattedId}_Loop",
                    "Visual",
                    true));
            }

            definitions.Add(new SequenceDefinition(
                "Gate_Additive_Loop",
                "Gate Additive Loop",
                "Assets/Art/Sprites/Gates/Additive/Loop",
                "Assets/Animations/Gates/Clips/AN_Gate_Additive_Loop.anim",
                "SPR_Gate_Additive_Loop",
                "Visual",
                true));

            foreach (var element in new[] { "Fire", "Ice", "Lightning" })
            {
                definitions.Add(new SequenceDefinition(
                    $"Gate_{element}_Loop",
                    $"Gate {element} Loop",
                    $"Assets/Art/Sprites/Gates/Element/{element}/Loop",
                    $"Assets/Animations/Gates/Clips/AN_Gate_{element}_Loop.anim",
                    $"SPR_Gate_{element}_Loop",
                    "Visual",
                    true));
            }

            return definitions;
        }
    }
}
