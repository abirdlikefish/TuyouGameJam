using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
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
        private const int DefaultFrameRate = 8;
        private const int DefaultMaxTextureSize = 512;
        private const float DefaultPixelsPerUnit = 512f;
        private const string SpritePropertyName = "m_Sprite";
        private const string ArmyBaseControllerPath =
            "Assets/Animations/Army/Base/AC_Army_Base.controller";
        private const string MonsterBaseControllerPath =
            "Assets/Animations/Monsters/Base/AC_Monster_Base.controller";
        private const string BulletControllerPath =
            "Assets/Animations/Bullets/Controllers/AC_Bullet.controller";
        private const string BasketballControllerPath =
            "Assets/Animations/Props/Basketball/Controllers/AC_Prop_Basketball.controller";
        private const string WeaponPropControllerPath =
            "Assets/Animations/Props/WeaponProp/Controllers/AC_Prop_Weapon.controller";
        private const string GooseCageControllerPath =
            "Assets/Animations/Props/GooseCage/Controllers/AC_Prop_GooseCage.controller";
        private const string ArmyPrefabPath = "Assets/Prefabs/Army/PF_Army_000.prefab";
        private const string BasketballPrefabPath =
            "Assets/Prefabs/Prop/PF_Prop_Basketball.prefab";
        private const string WeaponPropPrefabPath =
            "Assets/Prefabs/Prop/PF_Prop_Weapon.prefab";
        private const string GooseCagePrefabPath =
            "Assets/Prefabs/Prop/PF_Prop_GooseCage.prefab";
        private const int WeaponPropCount = 3;

        private sealed class WeaponDefinition
        {
            public WeaponDefinition(int weaponId, string folder, string shortId, string assetName)
            {
                WeaponId = weaponId;
                Folder = folder;
                ShortId = shortId;
                AssetName = assetName;
            }

            public int WeaponId { get; }
            public string Folder { get; }
            public string ShortId { get; }
            public string AssetName { get; }
        }

        private sealed class ArmyActionDefinition
        {
            public ArmyActionDefinition(string name, bool loop, bool completesDeath = false)
            {
                Name = name;
                Loop = loop;
                CompletesDeath = completesDeath;
            }

            public string Name { get; }
            public bool Loop { get; }
            public bool CompletesDeath { get; }
            public string CompletionEventName => CompletesDeath
                ? "OnDeathAnimationFinished"
                : null;
        }

        private sealed class MonsterDefinition
        {
            public MonsterDefinition(string technicalName, string prefabPath)
            {
                TechnicalName = technicalName;
                PrefabPath = prefabPath;
            }

            public string TechnicalName { get; }
            public string PrefabPath { get; }
        }

        private sealed class MonsterActionDefinition
        {
            public MonsterActionDefinition(
                string name,
                string spriteSubfolder,
                string stateName,
                bool loop,
                int? deathVariant = null,
                bool ikunOnly = false)
            {
                Name = name;
                SpriteSubfolder = spriteSubfolder;
                StateName = stateName;
                Loop = loop;
                DeathVariant = deathVariant;
                IkunOnly = ikunOnly;
            }

            public string Name { get; }
            public string SpriteSubfolder { get; }
            public string StateName { get; }
            public bool Loop { get; }
            public int? DeathVariant { get; }
            public bool IkunOnly { get; }
            public string CompletionEventName => DeathVariant.HasValue
                ? "OnDeathAnimationFinished"
                : null;

            public bool AppliesTo(MonsterDefinition monster)
            {
                return !IkunOnly || monster.TechnicalName == "Ikun";
            }
        }

        private static readonly WeaponDefinition[] Weapons =
        {
            new WeaponDefinition(0, "Weapon_000_Slingshot", "W000", "Slingshot"),
            new WeaponDefinition(1, "Weapon_001_Bow", "W001", "Bow"),
            new WeaponDefinition(2, "Weapon_002_Staff", "W002", "Staff"),
            new WeaponDefinition(3, "Weapon_003_FireStaff", "W003", "FireStaff"),
            new WeaponDefinition(4, "Weapon_004_IceStaff", "W004", "IceStaff"),
            new WeaponDefinition(5, "Weapon_005_LightningStaff", "W005", "LightningStaff"),
            new WeaponDefinition(6, "Weapon_006_FireIceStaff", "W006", "FireIceStaff"),
            new WeaponDefinition(7, "Weapon_007_FireLightningStaff", "W007", "FireLightningStaff"),
            new WeaponDefinition(8, "Weapon_008_IceLightningStaff", "W008", "IceLightningStaff"),
            new WeaponDefinition(9, "Weapon_009_FireIceLightningStaff", "W009", "FireIceLightningStaff")
        };

        private static readonly ArmyActionDefinition[] ArmyActions =
        {
            new ArmyActionDefinition("Idle", true),
            new ArmyActionDefinition("Victory", false),
            new ArmyActionDefinition("Attack", true),
            new ArmyActionDefinition("MoveLeft", true),
            new ArmyActionDefinition("MoveRight", true),
            new ArmyActionDefinition("Death", false, true)
        };

        private static readonly MonsterDefinition[] Monsters =
        {
            new MonsterDefinition("Normal", "Assets/Prefabs/Monster/PF_Monster_Chick.prefab"),
            new MonsterDefinition("Elite", "Assets/Prefabs/Monster/PF_Monster_Hen.prefab"),
            new MonsterDefinition("Boss", "Assets/Prefabs/Monster/PF_Monster_Rooster.prefab"),
            new MonsterDefinition("Ikun", "Assets/Prefabs/Monster/PF_Monster_Ikun.prefab")
        };

        private static readonly MonsterActionDefinition[] MonsterActions =
        {
            new MonsterActionDefinition("Move", "Move", "Move", true),
            new MonsterActionDefinition("Attack", "Attack", "Attack", false),
            new MonsterActionDefinition(
                "RangedAttack",
                "RangedAttack",
                "RangedAttack",
                false,
                ikunOnly: true),
            new MonsterActionDefinition("Death", "Death", "Death", false, 0),
            new MonsterActionDefinition("Death_Fire", "Death/Fire", "DeathFire", false, 1),
            new MonsterActionDefinition("Death_Ice", "Death/Ice", "DeathIce", false, 2),
            new MonsterActionDefinition(
                "Death_Lightning",
                "Death/Lightning",
                "DeathLightning",
                false,
                3)
        };

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
            public string CompletionEventName { get; }

            public SequenceDefinition(
                string id,
                string label,
                string spriteFolder,
                string clipPath,
                string spritePrefix,
                string bindingPath,
                bool loop,
                int frameRate = DefaultFrameRate,
                string completionEventName = null)
            {
                Id = id;
                Label = label;
                SpriteFolder = spriteFolder;
                ClipPath = clipPath;
                SpritePrefix = spritePrefix;
                BindingPath = bindingPath;
                Loop = loop;
                FrameRate = frameRate;
                CompletionEventName = completionEventName;
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

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Apply Ikun Attack Pending")]
        public static void ApplyIkunAttackPending()
        {
            Apply(new[]
            {
                "Monster_Ikun_Attack",
                "Monster_Ikun_RangedAttack"
            });
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Create Missing Registered Assets")]
        public static void CreateMissingRegisteredAssets()
        {
            try
            {
                var createdFolderCount = 0;
                var createdClipCount = 0;
                var createdControllerCount = 0;

                foreach (var definition in Definitions)
                {
                    createdFolderCount += EnsureFolder(definition.SpriteFolder);
                    createdFolderCount += EnsureFolder(
                        (Path.GetDirectoryName(definition.ClipPath) ?? string.Empty).Replace('\\', '/'));
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath) == null)
                    {
                        CreateEmptyClip(definition);
                        createdClipCount++;
                    }
                }

                EnsureMonsterAnimationAssets(
                    ref createdFolderCount,
                    ref createdClipCount,
                    ref createdControllerCount);

                EnsureArmyAnimationAssets(
                    ref createdFolderCount,
                    ref createdClipCount,
                    ref createdControllerCount);

                EnsureBulletControllerStates();
                EnsurePropAnimationAssets(ref createdFolderCount, ref createdControllerCount);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[SequenceAnimationBuilder] Registered animation assets ready: " +
                    $"{createdFolderCount} folders, {createdClipCount} clips and " +
                    $"{createdControllerCount} controllers created.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Create Missing Ikun Ranged Attack Assets")]
        public static void CreateMissingIkunRangedAttackAssets()
        {
            try
            {
                var createdFolderCount = 0;
                var createdClipCount = 0;
                var createdControllerCount = 0;
                var definition = Definitions.Single(candidate =>
                    candidate.Id == "Monster_Ikun_RangedAttack");
                var action = MonsterActions.Single(candidate =>
                    candidate.Name == "RangedAttack");
                var ikun = Monsters.Single(monster => monster.TechnicalName == "Ikun");

                createdFolderCount += EnsureFolder(definition.SpriteFolder);
                createdFolderCount += EnsureFolder(
                    (Path.GetDirectoryName(definition.ClipPath) ?? string.Empty).Replace('\\', '/'));
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath) == null)
                {
                    CreateEmptyClip(definition);
                    createdClipCount++;
                }

                createdFolderCount += EnsureFolder("Assets/Animations/Monsters/Base");
                var baseClipPath = GetMonsterBaseClipPath(action);
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(baseClipPath) == null)
                {
                    CreateMonsterBasePlaceholderClip(action, baseClipPath);
                    createdClipCount++;
                }

                var baseController = EnsureMonsterBaseController();
                var ikunFolder = "Assets/Animations/Monsters/Types/Ikun";
                createdFolderCount += EnsureFolder(ikunFolder);
                createdFolderCount += EnsureFolder($"{ikunFolder}/Clips");
                var controllerPath = $"{ikunFolder}/AOC_Monster_Ikun.overrideController";
                if (EnsureMonsterOverrideController(ikun, baseController, controllerPath))
                {
                    createdControllerCount++;
                }

                EnsureMonsterPrefabController(ikun, controllerPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[SequenceAnimationBuilder] Ikun ranged attack assets ready: " +
                    $"{createdFolderCount} folders, {createdClipCount} clips and " +
                    $"{createdControllerCount} override controllers created.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Create Missing Prop Animation Assets")]
        public static void CreateMissingPropAnimationAssets()
        {
            try
            {
                var createdFolderCount = 0;
                var createdClipCount = 0;
                var createdControllerCount = 0;
                foreach (var definition in Definitions.Where(
                             candidate => candidate.Id.StartsWith("Prop_", StringComparison.Ordinal)))
                {
                    createdFolderCount += EnsureFolder(definition.SpriteFolder);
                    createdFolderCount += EnsureFolder(
                        (Path.GetDirectoryName(definition.ClipPath) ?? string.Empty).Replace('\\', '/'));
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath) == null)
                    {
                        CreateEmptyClip(definition);
                        createdClipCount++;
                    }
                }

                EnsurePropAnimationAssets(ref createdFolderCount, ref createdControllerCount);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[SequenceAnimationBuilder] Prop animation assets ready: " +
                    $"{createdFolderCount} folders, {createdClipCount} clips and " +
                    $"{createdControllerCount} controllers created.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Apply Prop Pending")]
        public static void ApplyPropPending()
        {
            var results = Definitions
                .Where(definition => definition.Id.StartsWith("Prop_", StringComparison.Ordinal))
                .Select(Scan)
                .ToArray();
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

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Create Missing Army Death Assets")]
        public static void CreateMissingArmyDeathAssets()
        {
            try
            {
                var createdFolderCount = 0;
                var createdClipCount = 0;
                var createdControllerCount = 0;
                foreach (var definition in Definitions.Where(candidate =>
                             candidate.Id.StartsWith("Army_", StringComparison.Ordinal) &&
                             !string.IsNullOrEmpty(candidate.CompletionEventName)))
                {
                    createdFolderCount += EnsureFolder(definition.SpriteFolder);
                    createdFolderCount += EnsureFolder(
                        (Path.GetDirectoryName(definition.ClipPath) ?? string.Empty).Replace('\\', '/'));
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath) == null)
                    {
                        CreateEmptyClip(definition);
                        createdClipCount++;
                    }
                }

                EnsureArmyAnimationAssets(
                    ref createdFolderCount,
                    ref createdClipCount,
                    ref createdControllerCount);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[SequenceAnimationBuilder] Army death animation assets ready: " +
                    $"{createdFolderCount} folders, {createdClipCount} clips and " +
                    $"{createdControllerCount} override controllers created or updated.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Game Jam/Sequence Animation Builder/Create Missing Monster Death Assets")]
        public static void CreateMissingMonsterDeathAssets()
        {
            try
            {
                var createdFolderCount = 0;
                var createdClipCount = 0;
                var createdControllerCount = 0;
                foreach (var definition in Definitions.Where(
                             candidate => candidate.Id.StartsWith("Monster_", StringComparison.Ordinal)))
                {
                    createdFolderCount += EnsureFolder(definition.SpriteFolder);
                    createdFolderCount += EnsureFolder(
                        (Path.GetDirectoryName(definition.ClipPath) ?? string.Empty).Replace('\\', '/'));
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath) == null)
                    {
                        CreateEmptyClip(definition);
                        createdClipCount++;
                    }
                }

                EnsureMonsterAnimationAssets(
                    ref createdFolderCount,
                    ref createdClipCount,
                    ref createdControllerCount);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[SequenceAnimationBuilder] Monster animation assets ready: " +
                    $"{createdFolderCount} folders, {createdClipCount} clips and " +
                    $"{createdControllerCount} override controllers created.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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

            if (!HasExpectedCompletionEvent(definition, clip))
            {
                return false;
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
                // 默认查询会命中 Unity 为“刚删除资源”保留的 GUID；替换整组序列帧后，
                // 目标文件虽然已不存在，仍可能因此被误判为重名。
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(
                        targetPath,
                        AssetPathToGUIDOptions.OnlyExistingAssets)))
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

            // 事件由设计人员维护；重建只原样写回，不移动、删除或补充攻击事件。
            // 先恢复事件，再写视觉帧的停止时间，避免旧事件时间把新 Clip 长度自动撑长。
            AnimationUtility.SetAnimationEvents(
                clip,
                NormalizeCompletionEvents(
                    definition,
                    existingEvents,
                    (float)frames.Count / definition.FrameRate));

            var serializedClip = new SerializedObject(clip);
            var clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            clipSettings.FindPropertyRelative("m_StartTime").floatValue = 0f;
            clipSettings.FindPropertyRelative("m_StopTime").floatValue = (float)frames.Count / definition.FrameRate;
            clipSettings.FindPropertyRelative("m_LoopTime").boolValue = definition.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
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

        private static int EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path is required.", nameof(folderPath));
            }

            var normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) &&
                !string.Equals(normalized, "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Folder must be under Assets: {normalized}");
            }

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return 0;
            }

            var parts = normalized.Split('/');
            var current = parts[0];
            var created = 0;
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, parts[index]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new InvalidOperationException($"Failed to create folder: {next}");
                    }

                    created++;
                }

                current = next;
            }

            return created;
        }

        private static void CreateEmptyClip(SequenceDefinition definition)
        {
            var clip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(definition.ClipPath),
                frameRate = definition.FrameRate
            };

            // 武器道具的正式帧会后续导入；空 Clip 不写入 null Sprite，避免过渡期清空 Prefab 占位图。
            if (!definition.Id.StartsWith("Prop_Weapon_", StringComparison.Ordinal) &&
                !definition.Id.StartsWith("Prop_GooseCage_", StringComparison.Ordinal))
            {
                var binding = new EditorCurveBinding
                {
                    path = definition.BindingPath,
                    type = typeof(SpriteRenderer),
                    propertyName = SpritePropertyName
                };
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    binding,
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f,
                            value = null
                        }
                    });
            }

            AssetDatabase.CreateAsset(clip, definition.ClipPath);

            var serializedClip = new SerializedObject(clip);
            var clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            clipSettings.FindPropertyRelative("m_StartTime").floatValue = 0f;
            clipSettings.FindPropertyRelative("m_StopTime").floatValue =
                1f / definition.FrameRate;
            clipSettings.FindPropertyRelative("m_LoopTime").boolValue = definition.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            AnimationUtility.SetAnimationEvents(
                clip,
                NormalizeCompletionEvents(
                    definition,
                    Array.Empty<AnimationEvent>(),
                    1f / definition.FrameRate));
            EditorUtility.SetDirty(clip);
        }

        private static void ClearEmptyWeaponPropPlaceholderCurves()
        {
            foreach (var definition in Definitions.Where(candidate =>
                         candidate.Id.StartsWith("Prop_Weapon_", StringComparison.Ordinal) &&
                         Scan(candidate).Status == SequenceStatus.Empty))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);
                if (clip == null)
                {
                    continue;
                }

                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)
                             .Where(IsSpriteBinding))
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keyframes.Length != 1 || keyframes[0].value != null)
                    {
                        continue;
                    }

                    AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                    EditorUtility.SetDirty(clip);
                }
            }
        }

        private static bool HasExpectedCompletionEvent(
            SequenceDefinition definition,
            AnimationClip clip)
        {
            if (string.IsNullOrEmpty(definition.CompletionEventName))
            {
                return true;
            }

            var expectedTime = new SerializedObject(clip)
                .FindProperty("m_AnimationClipSettings")
                .FindPropertyRelative("m_StopTime")
                .floatValue;
            var matchingEvents = AnimationUtility.GetAnimationEvents(clip)
                .Where(animationEvent =>
                    animationEvent.functionName == definition.CompletionEventName)
                .ToArray();
            return matchingEvents.Length == 1 &&
                   Mathf.Abs(matchingEvents[0].time - expectedTime) <= 0.0001f;
        }

        private static AnimationEvent[] NormalizeCompletionEvents(
            SequenceDefinition definition,
            IEnumerable<AnimationEvent> existingEvents,
            float completionTime)
        {
            var events = (existingEvents ?? Array.Empty<AnimationEvent>()).ToList();
            if (string.IsNullOrEmpty(definition.CompletionEventName))
            {
                return events.ToArray();
            }

            events.RemoveAll(animationEvent =>
                animationEvent.functionName == definition.CompletionEventName);
            events.Add(new AnimationEvent
            {
                functionName = definition.CompletionEventName,
                time = completionTime
            });
            return events.OrderBy(animationEvent => animationEvent.time).ToArray();
        }

        private static void EnsureArmyAnimationAssets(
            ref int createdFolderCount,
            ref int createdClipCount,
            ref int createdControllerCount)
        {
            createdFolderCount += EnsureFolder("Assets/Animations/Army/Base");
            foreach (var action in ArmyActions)
            {
                var baseClipPath = GetArmyBaseClipPath(action);
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(baseClipPath) == null)
                {
                    CreateArmyBasePlaceholderClip(action, baseClipPath);
                    createdClipCount++;
                }
            }

            foreach (var definition in Definitions.Where(candidate =>
                         candidate.Id.StartsWith("Army_", StringComparison.Ordinal) &&
                         !string.IsNullOrEmpty(candidate.CompletionEventName)))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);
                if (clip == null)
                {
                    throw new InvalidOperationException(
                        $"Army death clip is missing: {definition.ClipPath}");
                }

                EnsureCompletionEvent(definition, clip);
            }

            var baseController = EnsureArmyBaseController();
            foreach (var weapon in Weapons)
            {
                var weaponFolder = $"Assets/Animations/Army/Weapons/{weapon.Folder}";
                createdFolderCount += EnsureFolder(weaponFolder);
                createdFolderCount += EnsureFolder($"{weaponFolder}/Clips");
                var controllerPath =
                    $"{weaponFolder}/AOC_Army_{weapon.ShortId}_{weapon.AssetName}.overrideController";
                if (EnsureArmyOverrideController(weapon, baseController, controllerPath))
                {
                    createdControllerCount++;
                }
            }

            EnsureArmyPrefabBindings();
        }

        private static void CreateArmyBasePlaceholderClip(
            ArmyActionDefinition action,
            string clipPath)
        {
            var clip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(clipPath),
                frameRate = DefaultFrameRate
            };
            AssetDatabase.CreateAsset(clip, clipPath);

            var serializedClip = new SerializedObject(clip);
            var clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            clipSettings.FindPropertyRelative("m_StartTime").floatValue = 0f;
            clipSettings.FindPropertyRelative("m_StopTime").floatValue =
                1f / DefaultFrameRate;
            clipSettings.FindPropertyRelative("m_LoopTime").boolValue = action.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
        }

        private static AnimatorController EnsureArmyBaseController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ArmyBaseControllerPath);
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Army controller is missing or malformed: {ArmyBaseControllerPath}");
            }

            var stateMachine = controller.layers[0].stateMachine;
            for (var index = 0; index < ArmyActions.Length; index++)
            {
                var action = ArmyActions[index];
                var state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == action.Name);
                if (state == null)
                {
                    var column = index % 3;
                    var row = index / 3;
                    state = stateMachine.AddState(
                        action.Name,
                        new Vector3(180f + column * 220f, 60f + row * 100f, 0f));
                }

                var baseClipPath = GetArmyBaseClipPath(action);
                var baseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(baseClipPath);
                if (baseClip == null)
                {
                    throw new InvalidOperationException(
                        $"Army base animation clip is missing: {baseClipPath}");
                }

                state.motion = baseClip;
                state.writeDefaultValues = true;
                if (action.Name == "Idle")
                {
                    stateMachine.defaultState = state;
                }
            }

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static bool EnsureArmyOverrideController(
            WeaponDefinition weapon,
            RuntimeAnimatorController baseController,
            string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                controllerPath);
            var created = controller == null;
            if (created)
            {
                controller = new AnimatorOverrideController(baseController)
                {
                    name = Path.GetFileNameWithoutExtension(controllerPath)
                };
            }
            else if (controller.runtimeAnimatorController != baseController)
            {
                controller.runtimeAnimatorController = baseController;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (var index = 0; index < overrides.Count; index++)
            {
                var baseClip = overrides[index].Key;
                var action = ArmyActions.FirstOrDefault(
                    candidate => baseClip != null &&
                                 baseClip.name.EndsWith($"_{candidate.Name}", StringComparison.Ordinal));
                if (action == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot map Army base clip {baseClip?.name ?? "<null>"}.");
                }

                var clipPath =
                    $"Assets/Animations/Army/Weapons/{weapon.Folder}/Clips/" +
                    $"AN_Army_{weapon.ShortId}_{action.Name}.anim";
                var overrideClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (overrideClip == null)
                {
                    throw new InvalidOperationException($"Army override clip is missing: {clipPath}");
                }

                overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(
                    baseClip,
                    overrideClip);
            }

            controller.ApplyOverrides(overrides);
            if (created)
            {
                AssetDatabase.CreateAsset(controller, controllerPath);
            }
            else
            {
                EditorUtility.SetDirty(controller);
            }

            return created;
        }

        private static string GetArmyBaseClipPath(ArmyActionDefinition action)
        {
            return $"Assets/Animations/Army/Base/AN_Army_Base_{action.Name}.anim";
        }

        private static void EnsureMonsterAnimationAssets(
            ref int createdFolderCount,
            ref int createdClipCount,
            ref int createdControllerCount)
        {
            createdFolderCount += EnsureFolder("Assets/Animations/Monsters/Base");
            foreach (var action in MonsterActions)
            {
                var baseClipPath = GetMonsterBaseClipPath(action);
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(baseClipPath) == null)
                {
                    CreateMonsterBasePlaceholderClip(action, baseClipPath);
                    createdClipCount++;
                }
            }

            foreach (var definition in Definitions.Where(
                         candidate => !string.IsNullOrEmpty(candidate.CompletionEventName)))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);
                if (clip == null)
                {
                    throw new InvalidOperationException(
                        $"Monster death clip is missing: {definition.ClipPath}");
                }

                EnsureCompletionEvent(definition, clip);
            }

            var baseController = EnsureMonsterBaseController();
            foreach (var monster in Monsters)
            {
                var monsterFolder =
                    $"Assets/Animations/Monsters/Types/{monster.TechnicalName}";
                createdFolderCount += EnsureFolder(monsterFolder);
                createdFolderCount += EnsureFolder($"{monsterFolder}/Clips");
                var controllerPath =
                    $"{monsterFolder}/AOC_Monster_{monster.TechnicalName}.overrideController";
                if (EnsureMonsterOverrideController(monster, baseController, controllerPath))
                {
                    createdControllerCount++;
                }

                EnsureMonsterPrefabController(monster, controllerPath);
            }
        }

        private static void CreateMonsterBasePlaceholderClip(
            MonsterActionDefinition action,
            string clipPath)
        {
            var clip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(clipPath),
                frameRate = DefaultFrameRate
            };
            AssetDatabase.CreateAsset(clip, clipPath);

            var serializedClip = new SerializedObject(clip);
            var clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            clipSettings.FindPropertyRelative("m_StartTime").floatValue = 0f;
            clipSettings.FindPropertyRelative("m_StopTime").floatValue =
                1f / DefaultFrameRate;
            clipSettings.FindPropertyRelative("m_LoopTime").boolValue = action.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
        }

        private static AnimatorController EnsureMonsterBaseController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                MonsterBaseControllerPath);
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Monster controller is missing or malformed: {MonsterBaseControllerPath}");
            }

            var deathVariantParameter = controller.parameters.FirstOrDefault(
                parameter => parameter.name == "DeathVariant");
            if (deathVariantParameter == null)
            {
                controller.AddParameter("DeathVariant", AnimatorControllerParameterType.Int);
            }
            else if (deathVariantParameter.type != AnimatorControllerParameterType.Int)
            {
                throw new InvalidOperationException(
                    "Monster controller DeathVariant parameter must be Int.");
            }

            var rangedAttackParameter = controller.parameters.FirstOrDefault(
                parameter => parameter.name == "RangedAttack");
            if (rangedAttackParameter == null)
            {
                controller.AddParameter("RangedAttack", AnimatorControllerParameterType.Trigger);
            }
            else if (rangedAttackParameter.type != AnimatorControllerParameterType.Trigger)
            {
                throw new InvalidOperationException(
                    "Monster controller RangedAttack parameter must be Trigger.");
            }

            var stateMachine = controller.layers[0].stateMachine;
            EnsureMonsterRangedAttackState(stateMachine);
            var unusedDeathTransitions = stateMachine.anyStateTransitions
                .Where(candidate => candidate.conditions.Any(
                    condition => condition.parameter == "Death"))
                .ToList();

            var deathActions = MonsterActions.Where(action => action.DeathVariant.HasValue).ToArray();
            for (var index = 0; index < deathActions.Length; index++)
            {
                var action = deathActions[index];
                var state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == action.StateName);
                if (state == null)
                {
                    state = stateMachine.AddState(
                        action.StateName,
                        new Vector3(520f, 40f + index * 90f, 0f));
                }

                var baseClipPath = GetMonsterBaseClipPath(action);
                var baseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(baseClipPath);
                if (baseClip == null)
                {
                    throw new InvalidOperationException(
                        $"Monster base animation clip is missing: {baseClipPath}");
                }

                state.motion = baseClip;
                state.writeDefaultValues = true;
                var transition = unusedDeathTransitions.FirstOrDefault(candidate =>
                    candidate.destinationState == state &&
                    IsMonsterDeathTransition(candidate, action.DeathVariant.Value));
                if (transition == null)
                {
                    transition = stateMachine.AddAnyStateTransition(state);
                    transition.AddCondition(AnimatorConditionMode.If, 0f, "Death");
                    transition.AddCondition(
                        AnimatorConditionMode.Equals,
                        action.DeathVariant.Value,
                        "DeathVariant");
                }
                else
                {
                    unusedDeathTransitions.Remove(transition);
                }

                transition.duration = 0f;
                transition.hasExitTime = false;
                transition.canTransitionToSelf = false;
            }

            foreach (var transition in unusedDeathTransitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureMonsterRangedAttackState(AnimatorStateMachine stateMachine)
        {
            var moveState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == "Move");
            if (moveState == null)
            {
                throw new InvalidOperationException("Monster controller Move state is missing.");
            }

            var rangedAction = MonsterActions.Single(action => action.Name == "RangedAttack");
            var rangedState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == rangedAction.StateName);
            if (rangedState == null)
            {
                rangedState = stateMachine.AddState(
                    rangedAction.StateName,
                    new Vector3(380f, -40f, 0f));
            }

            var baseClipPath = GetMonsterBaseClipPath(rangedAction);
            var baseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(baseClipPath);
            if (baseClip == null)
            {
                throw new InvalidOperationException(
                    $"Monster ranged attack base clip is missing: {baseClipPath}");
            }

            rangedState.motion = baseClip;
            rangedState.writeDefaultValues = true;

            var enterTransitions = moveState.transitions
                .Where(candidate => candidate.conditions.Any(condition =>
                    condition.parameter == "RangedAttack"))
                .ToList();
            var enterTransition = enterTransitions.FirstOrDefault(candidate =>
                candidate.destinationState == rangedState);
            if (enterTransition == null)
            {
                enterTransition = moveState.AddTransition(rangedState);
                enterTransition.AddCondition(AnimatorConditionMode.If, 0f, "RangedAttack");
            }

            foreach (var staleTransition in enterTransitions.Where(candidate =>
                         candidate != enterTransition))
            {
                moveState.RemoveTransition(staleTransition);
            }

            enterTransition.duration = 0f;
            enterTransition.hasExitTime = false;
            enterTransition.canTransitionToSelf = false;

            var exitTransitions = rangedState.transitions
                .Where(candidate => candidate.destinationState == moveState)
                .ToList();
            var exitTransition = exitTransitions.FirstOrDefault(candidate =>
                candidate.conditions.Length == 0);
            if (exitTransition == null)
            {
                exitTransition = rangedState.AddTransition(moveState);
            }

            foreach (var staleTransition in exitTransitions.Where(candidate =>
                         candidate != exitTransition))
            {
                rangedState.RemoveTransition(staleTransition);
            }

            exitTransition.duration = 0f;
            exitTransition.exitTime = 1f;
            exitTransition.hasExitTime = true;
            exitTransition.hasFixedDuration = true;
            exitTransition.canTransitionToSelf = false;
            EditorUtility.SetDirty(rangedState);
        }

        private static bool IsMonsterDeathTransition(
            AnimatorStateTransition transition,
            int deathVariant)
        {
            var conditions = transition.conditions;
            return conditions.Length == 2 &&
                   conditions.Any(condition =>
                       condition.parameter == "Death" &&
                       condition.mode == AnimatorConditionMode.If) &&
                   conditions.Any(condition =>
                       condition.parameter == "DeathVariant" &&
                       condition.mode == AnimatorConditionMode.Equals &&
                       Mathf.Approximately(condition.threshold, deathVariant));
        }

        private static bool EnsureMonsterOverrideController(
            MonsterDefinition monster,
            RuntimeAnimatorController baseController,
            string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                controllerPath);
            var created = controller == null;
            if (created)
            {
                controller = new AnimatorOverrideController(baseController)
                {
                    name = Path.GetFileNameWithoutExtension(controllerPath)
                };
            }
            else if (controller.runtimeAnimatorController != baseController)
            {
                controller.runtimeAnimatorController = baseController;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (var index = 0; index < overrides.Count; index++)
            {
                var baseClip = overrides[index].Key;
                var action = MonsterActions.FirstOrDefault(candidate =>
                    baseClip != null &&
                    baseClip.name == $"AN_Monster_Base_{candidate.Name}");
                if (action == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot map Monster base clip {baseClip?.name ?? "<null>"}.");
                }

                if (!action.AppliesTo(monster))
                {
                    continue;
                }

                var clipPath =
                    $"Assets/Animations/Monsters/Types/{monster.TechnicalName}/Clips/" +
                    $"AN_Monster_{monster.TechnicalName}_{action.Name}.anim";
                var overrideClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (overrideClip == null)
                {
                    throw new InvalidOperationException(
                        $"Monster override clip is missing: {clipPath}");
                }

                overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(
                    baseClip,
                    overrideClip);
            }

            controller.ApplyOverrides(overrides);
            if (created)
            {
                AssetDatabase.CreateAsset(controller, controllerPath);
            }
            else
            {
                EditorUtility.SetDirty(controller);
            }

            return created;
        }

        private static void EnsureMonsterPrefabController(
            MonsterDefinition monster,
            string controllerPath)
        {
            var expectedController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                controllerPath);
            if (expectedController == null)
            {
                throw new InvalidOperationException(
                    $"Monster override controller is missing: {controllerPath}");
            }

            var root = PrefabUtility.LoadPrefabContents(monster.PrefabPath);
            try
            {
                var animator = root.GetComponent<Animator>();
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        $"Animator is missing from monster prefab root: {monster.PrefabPath}");
                }

                if (animator.runtimeAnimatorController == expectedController)
                {
                    return;
                }

                animator.runtimeAnimatorController = expectedController;
                EditorUtility.SetDirty(animator);
                PrefabUtility.SaveAsPrefabAsset(root, monster.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureCompletionEvent(
            SequenceDefinition definition,
            AnimationClip clip)
        {
            if (HasExpectedCompletionEvent(definition, clip))
            {
                return;
            }

            var clipSettings = new SerializedObject(clip)
                .FindProperty("m_AnimationClipSettings");
            var stopTime = clipSettings.FindPropertyRelative("m_StopTime").floatValue;
            AnimationUtility.SetAnimationEvents(
                clip,
                NormalizeCompletionEvents(
                    definition,
                    AnimationUtility.GetAnimationEvents(clip),
                    stopTime));
            EditorUtility.SetDirty(clip);
        }

        private static string GetMonsterBaseClipPath(MonsterActionDefinition action)
        {
            return $"Assets/Animations/Monsters/Base/AN_Monster_Base_{action.Name}.anim";
        }

        private static void EnsureBulletControllerStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BulletControllerPath);
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Bullet controller is missing or malformed: {BulletControllerPath}");
            }

            var stateMachine = controller.layers[0].stateMachine;
            for (var bulletId = 0; bulletId < Weapons.Length; bulletId++)
            {
                var stateName = $"Bullet_{bulletId:000}_Loop";
                var state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == stateName);
                if (state == null)
                {
                    var column = bulletId % 4;
                    var row = bulletId / 4;
                    state = stateMachine.AddState(
                        stateName,
                        new Vector3(180f + column * 220f, 120f + row * 120f, 0f));
                }

                var clipPath = $"Assets/Animations/Bullets/Clips/AN_Bullet_{bulletId:000}_Loop.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    throw new InvalidOperationException($"Bullet animation clip is missing: {clipPath}");
                }

                state.motion = clip;
                state.writeDefaultValues = true;
                var hasTransition = stateMachine.anyStateTransitions.Any(
                    transition => transition.destinationState == state &&
                                  transition.conditions.Any(
                                      condition => condition.mode == AnimatorConditionMode.Equals &&
                                                   condition.parameter == "BulletId" &&
                                                   Mathf.Approximately(condition.threshold, bulletId)));
                if (!hasTransition)
                {
                    var transition = stateMachine.AddAnyStateTransition(state);
                    transition.duration = 0f;
                    transition.hasExitTime = false;
                    transition.canTransitionToSelf = false;
                    transition.AddCondition(AnimatorConditionMode.Equals, bulletId, "BulletId");
                }
            }

            EditorUtility.SetDirty(controller);
        }

        private static void EnsurePropAnimationAssets(
            ref int createdFolderCount,
            ref int createdControllerCount)
        {
            createdFolderCount += EnsureFolder("Assets/Animations/Props/Basketball/Controllers");
            createdFolderCount += EnsureFolder("Assets/Animations/Props/WeaponProp/Controllers");
            createdFolderCount += EnsureFolder("Assets/Animations/Props/GooseCage/Controllers");
            ClearEmptyWeaponPropPlaceholderCurves();

            var basketballController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                BasketballControllerPath);
            if (basketballController == null)
            {
                basketballController = AnimatorController.CreateAnimatorControllerAtPath(
                    BasketballControllerPath);
                createdControllerCount++;
            }

            EnsureBasketballControllerState(basketballController);

            var weaponController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                WeaponPropControllerPath);
            if (weaponController == null)
            {
                weaponController = AnimatorController.CreateAnimatorControllerAtPath(
                    WeaponPropControllerPath);
                createdControllerCount++;
            }

            EnsureWeaponPropControllerStates(weaponController);
            var gooseCageController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                GooseCageControllerPath);
            if (gooseCageController == null)
            {
                gooseCageController = AnimatorController.CreateAnimatorControllerAtPath(
                    GooseCageControllerPath);
                createdControllerCount++;
            }

            EnsureSinglePropControllerState(
                gooseCageController,
                GooseCageControllerPath,
                "GooseCage_Loop",
                "Assets/Animations/Props/GooseCage/Clips/AN_Prop_GooseCage_Loop.anim");
            EnsurePropPrefabBindings(
                BasketballPrefabPath,
                basketballController,
                typeof(BasketballProp));
            EnsurePropPrefabBindings(
                WeaponPropPrefabPath,
                weaponController,
                typeof(WeaponProp));
            EnsurePropPrefabBindings(
                GooseCagePrefabPath,
                gooseCageController,
                typeof(GooseCageProp));
        }

        private static void EnsureSinglePropControllerState(
            AnimatorController controller,
            string controllerPath,
            string stateName,
            string clipPath)
        {
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Prop controller is missing or malformed: {controllerPath}");
            }

            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            if (state == null)
            {
                state = stateMachine.AddState(stateName, new Vector3(240f, 120f, 0f));
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"Prop loop clip is missing: {clipPath}");
            }

            state.motion = clip;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureBasketballControllerState(AnimatorController controller)
        {
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Basketball controller is missing or malformed: {BasketballControllerPath}");
            }

            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == "Basketball_Loop");
            if (state == null)
            {
                state = stateMachine.AddState("Basketball_Loop", new Vector3(240f, 120f, 0f));
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Animations/Props/Basketball/Clips/AN_Prop_Basketball_Loop.anim");
            if (clip == null)
            {
                throw new InvalidOperationException("Basketball loop clip is missing.");
            }

            state.motion = clip;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureWeaponPropControllerStates(AnimatorController controller)
        {
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Weapon prop controller is missing or malformed: {WeaponPropControllerPath}");
            }

            var weaponParameter = controller.parameters.FirstOrDefault(
                parameter => parameter.name == "WeaponId");
            if (weaponParameter == null)
            {
                controller.AddParameter("WeaponId", AnimatorControllerParameterType.Int);
            }
            else if (weaponParameter.type != AnimatorControllerParameterType.Int)
            {
                throw new InvalidOperationException("Weapon prop controller WeaponId parameter must be Int.");
            }

            var stateMachine = controller.layers[0].stateMachine;
            for (var weaponId = 0; weaponId < WeaponPropCount; weaponId++)
            {
                var stateName = $"Weapon_{weaponId:000}_Loop";
                var state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == stateName);
                if (state == null)
                {
                    state = stateMachine.AddState(
                        stateName,
                        new Vector3(200f + weaponId * 220f, 120f, 0f));
                }

                var clipPath =
                    $"Assets/Animations/Props/WeaponProp/Clips/AN_Prop_Weapon_{weaponId:000}_Loop.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    throw new InvalidOperationException($"Weapon prop animation clip is missing: {clipPath}");
                }

                state.motion = clip;
                state.writeDefaultValues = true;
                var hasTransition = stateMachine.anyStateTransitions.Any(
                    transition => transition.destinationState == state &&
                                  transition.conditions.Any(
                                      condition => condition.mode == AnimatorConditionMode.Equals &&
                                                   condition.parameter == "WeaponId" &&
                                                   Mathf.Approximately(condition.threshold, weaponId)));
                if (!hasTransition)
                {
                    var transition = stateMachine.AddAnyStateTransition(state);
                    transition.duration = 0f;
                    transition.hasExitTime = false;
                    transition.canTransitionToSelf = false;
                    transition.AddCondition(AnimatorConditionMode.Equals, weaponId, "WeaponId");
                }

                if (weaponId == 0)
                {
                    stateMachine.defaultState = state;
                }
            }

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsurePropPrefabBindings(
            string prefabPath,
            RuntimeAnimatorController controller,
            Type expectedPropType)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var prop = root.GetComponent(expectedPropType) as BreakablePropBase;
                if (prop == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} requires a {expectedPropType.Name} root component.");
                }

                var animator = root.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = root.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var serializedProp = new SerializedObject(prop);
                var animatorProperty = serializedProp.FindProperty("animator");
                var visualProperty = serializedProp.FindProperty("visual");
                var visual = visualProperty?.objectReferenceValue as SpriteRenderer;
                if (animatorProperty == null || visual == null)
                {
                    throw new InvalidOperationException(
                        $"{expectedPropType.Name}.animator and visual must be serialized.");
                }

                animatorProperty.objectReferenceValue = animator;
                serializedProp.ApplyModifiedPropertiesWithoutUndo();
                visual.color = Color.white;
                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(visual);
                EditorUtility.SetDirty(prop);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureArmyPrefabBindings()
        {
            var root = PrefabUtility.LoadPrefabContents(ArmyPrefabPath);
            try
            {
                var army = root.GetComponent<ArmyController>();
                if (army == null)
                {
                    throw new InvalidOperationException(
                        $"ArmyController is missing from prefab root: {ArmyPrefabPath}");
                }

                var slotViews = root.GetComponentsInChildren<ArmySlotView>(true);
                for (var index = 0; index < slotViews.Length; index++)
                {
                    var slotView = slotViews[index];
                    var serializedSlot = new SerializedObject(slotView);
                    var animatorProperty = serializedSlot.FindProperty("soldierAnimator");
                    var proxyProperty = serializedSlot.FindProperty("animationEventProxy");
                    var animator = animatorProperty?.objectReferenceValue as Animator;
                    if (animator == null || proxyProperty == null)
                    {
                        throw new InvalidOperationException(
                            $"Army slot animation bindings are missing: {slotView.name}");
                    }

                    var proxy = animator.GetComponent<ArmySlotAnimationEventProxy>();
                    if (proxy == null)
                    {
                        proxy = animator.gameObject.AddComponent<ArmySlotAnimationEventProxy>();
                    }

                    var serializedProxy = new SerializedObject(proxy);
                    serializedProxy.FindProperty("target").objectReferenceValue = slotView;
                    serializedProxy.ApplyModifiedPropertiesWithoutUndo();
                    proxyProperty.objectReferenceValue = proxy;
                    serializedSlot.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(proxy);
                    EditorUtility.SetDirty(slotView);
                }

                var serializedArmy = new SerializedObject(army);
                var bindings = serializedArmy.FindProperty("weaponAnimatorControllers");
                if (bindings == null)
                {
                    throw new InvalidOperationException(
                        "ArmyController.weaponAnimatorControllers cannot be serialized.");
                }

                bindings.arraySize = Weapons.Length;
                for (var index = 0; index < Weapons.Length; index++)
                {
                    var weapon = Weapons[index];
                    var controllerPath =
                        $"Assets/Animations/Army/Weapons/{weapon.Folder}/" +
                        $"AOC_Army_{weapon.ShortId}_{weapon.AssetName}.overrideController";
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                        controllerPath);
                    if (controller == null)
                    {
                        throw new InvalidOperationException(
                            $"Army override controller is missing: {controllerPath}");
                    }

                    var binding = bindings.GetArrayElementAtIndex(index);
                    binding.FindPropertyRelative("weaponId").intValue = weapon.WeaponId;
                    binding.FindPropertyRelative("controller").objectReferenceValue = controller;
                }

                serializedArmy.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ArmyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

            foreach (var weapon in Weapons)
            {
                foreach (var action in ArmyActions)
                {
                    definitions.Add(new SequenceDefinition(
                        $"Army_{weapon.ShortId}_{action.Name}",
                        $"Army {weapon.ShortId} {action.Name}",
                        $"Assets/Art/Sprites/Army/Weapons/{weapon.Folder}/{action.Name}",
                        $"Assets/Animations/Army/Weapons/{weapon.Folder}/Clips/AN_Army_{weapon.ShortId}_{action.Name}.anim",
                        $"SPR_Army_{weapon.ShortId}_{action.Name}",
                        string.Empty,
                        action.Loop,
                        DefaultFrameRate,
                        action.CompletionEventName));
                }
            }

            foreach (var monster in Monsters)
            {
                foreach (var action in MonsterActions)
                {
                    if (!action.AppliesTo(monster))
                    {
                        continue;
                    }

                    definitions.Add(new SequenceDefinition(
                        $"Monster_{monster.TechnicalName}_{action.Name}",
                        $"Monster {monster.TechnicalName} {action.Name}",
                        $"Assets/Art/Sprites/Monsters/{monster.TechnicalName}/{action.SpriteSubfolder}",
                        $"Assets/Animations/Monsters/Types/{monster.TechnicalName}/Clips/AN_Monster_{monster.TechnicalName}_{action.Name}.anim",
                        $"SPR_Monster_{monster.TechnicalName}_{action.Name}",
                        "Visual",
                        action.Loop,
                        DefaultFrameRate,
                        action.CompletionEventName));
                }
            }

            for (var bulletId = 0; bulletId < Weapons.Length; bulletId++)
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

            definitions.Add(new SequenceDefinition(
                "Prop_Basketball_Loop",
                "Prop Basketball Loop",
                "Assets/Art/Sprites/Props/Basketball/Loop",
                "Assets/Animations/Props/Basketball/Clips/AN_Prop_Basketball_Loop.anim",
                "SPR_Prop_Basketball_Loop",
                "Visual",
                true));

            definitions.Add(new SequenceDefinition(
                "Prop_GooseCage_Loop",
                "Prop Goose Cage Loop",
                "Assets/Art/Sprites/Props/GooseCage/Loop",
                "Assets/Animations/Props/GooseCage/Clips/AN_Prop_GooseCage_Loop.anim",
                "SPR_Prop_GooseCage_Loop",
                "Visual",
                true));

            for (var weaponId = 0; weaponId < WeaponPropCount; weaponId++)
            {
                var weapon = Weapons[weaponId];
                definitions.Add(new SequenceDefinition(
                    $"Prop_Weapon_{weaponId:000}_Loop",
                    $"Prop Weapon {weaponId:000} Loop",
                    $"Assets/Art/Sprites/Props/WeaponProp/{weapon.Folder}/Loop",
                    $"Assets/Animations/Props/WeaponProp/Clips/AN_Prop_Weapon_{weaponId:000}_Loop.anim",
                    $"SPR_Prop_Weapon_{weaponId:000}_Loop",
                    "Visual",
                    true));
            }

            return definitions;
        }
    }
}
