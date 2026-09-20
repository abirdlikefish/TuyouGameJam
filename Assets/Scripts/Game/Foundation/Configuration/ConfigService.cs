using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;
using RuntimeEnemyType = Game.Contracts.EnemyType;

namespace Game.Foundation
{
    public sealed class ConfigService : IConfigService
    {
        private static readonly IReadOnlyList<LevelDescriptor> EmptyLevelDescriptors =
            Array.AsReadOnly(new LevelDescriptor[0]);

        private ConfigLoadState state = ConfigLoadState.Uninitialized;
        private IReadOnlyList<LevelDescriptor> levelDescriptors = EmptyLevelDescriptors;
        private Dictionary<int, LevelConfigSnapshot> levelsById =
            new Dictionary<int, LevelConfigSnapshot>();
        private Dictionary<int, ArmyConfigSnapshot> armiesById =
            new Dictionary<int, ArmyConfigSnapshot>();
        private Dictionary<int, WeaponConfigSnapshot> weaponsById =
            new Dictionary<int, WeaponConfigSnapshot>();
        private Dictionary<int, BulletConfigSnapshot> bulletsById =
            new Dictionary<int, BulletConfigSnapshot>();
        private Dictionary<int, EnemyConfigSnapshot> enemiesById =
            new Dictionary<int, EnemyConfigSnapshot>();
        private Dictionary<int, PropConfigSnapshot> propsById =
            new Dictionary<int, PropConfigSnapshot>();

        public void Initialize(LevelCatalog catalog, cfg.Tables tables, IResourceRegistry resourceRegistry)
        {
            if (state != ConfigLoadState.Uninitialized)
            {
                throw new InvalidOperationException("ConfigService does not support initialization retries.");
            }

            state = ConfigLoadState.Loading;

            try
            {
                ValidateInitializationInputs(catalog, tables, resourceRegistry);

                // 全部快照先在局部构建，只有完整校验通过后才一次性发布 Ready 状态。
                var newArmies = BuildArmySnapshots(tables);
                var newWeapons = BuildWeaponSnapshots(tables);
                var newBullets = BuildBulletSnapshots(tables);
                var newEnemies = BuildEnemySnapshots(tables);
                var newProps = BuildPropSnapshots(tables);

                ValidateFixedWeapons(newWeapons);
                ValidateTableReferences(tables, newWeapons, newBullets, newProps);

                BuildLevelSnapshots(
                    catalog,
                    newEnemies,
                    newProps,
                    out var newLevelDescriptors,
                    out var newLevels);

                armiesById = newArmies;
                weaponsById = newWeapons;
                bulletsById = newBullets;
                enemiesById = newEnemies;
                propsById = newProps;
                levelDescriptors = newLevelDescriptors;
                levelsById = newLevels;
                state = ConfigLoadState.Ready;
            }
            catch (ConfigValidationException exception)
            {
                FailAndTerminate(exception.Code, exception.ConfigSource, exception.Message);
            }
            catch (Exception exception)
            {
                FailAndTerminate(
                    ConfigErrorCode.TableLoadFailed,
                    "ConfigService.Initialize",
                    $"Unexpected configuration initialization failure: {exception.GetType().Name}: {exception.Message}");
            }
        }

        public ConfigLoadState GetConfigLoadState()
        {
            return state;
        }

        public IReadOnlyList<LevelDescriptor> GetLevelDescriptors()
        {
            EnsureReady();
            return levelDescriptors;
        }

        public bool TryGetLevelConfig(int levelId, out LevelConfigSnapshot levelConfig)
        {
            EnsureReady();
            return levelsById.TryGetValue(levelId, out levelConfig);
        }

        public ArmyConfigSnapshot GetArmyConfig(int armyId)
        {
            EnsureReady();
            return GetRequired(armiesById, armyId, "Army");
        }

        public WeaponConfigSnapshot GetWeaponConfig(int weaponId)
        {
            EnsureReady();
            return GetRequired(weaponsById, weaponId, "Weapon");
        }

        public BulletConfigSnapshot GetBulletConfig(int bulletId)
        {
            EnsureReady();
            return GetRequired(bulletsById, bulletId, "Bullet");
        }

        public EnemyConfigSnapshot GetEnemyConfig(int enemyId)
        {
            EnsureReady();
            return GetRequired(enemiesById, enemyId, "Enemy");
        }

        public PropConfigSnapshot GetPropConfig(int propId)
        {
            EnsureReady();
            return GetRequired(propsById, propId, "Prop");
        }

        private static void ValidateInitializationInputs(
            LevelCatalog catalog,
            cfg.Tables tables,
            IResourceRegistry resourceRegistry)
        {
            if (catalog == null)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.EmptyCatalog,
                    "LevelCatalog",
                    "The LevelCatalog reference is null.");
            }

            if (tables == null)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.TableLoadFailed,
                    "cfg.Tables",
                    "The generated Luban Tables reference is null.");
            }

            if (resourceRegistry == null)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.ResourceMissing,
                    "IResourceRegistry",
                    "The resource registry reference is null.");
            }

            if (catalog.Entries == null || catalog.Entries.Count == 0)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.EmptyCatalog,
                    "LevelCatalog.entries",
                    "The level catalog must contain at least one entry.");
            }
        }

        private static Dictionary<int, ArmyConfigSnapshot> BuildArmySnapshots(cfg.Tables tables)
        {
            var rows = tables.TbArmy.DataList;
            ValidateFirstTableRow(rows.Count, rows.Count > 0 ? rows[0].Id : -1, "TbArmy");

            var snapshots = new Dictionary<int, ArmyConfigSnapshot>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var source = $"TbArmy[{index}]";
                ValidateNonNegativeId(row.Id, source);
                if (row.ArmyCountLimit < 0)
                {
                    ThrowInvalidTableValue(source, "ArmyCountLimit must be zero or a positive integer.");
                }

                if (row.HpPerSoldier <= 0)
                {
                    ThrowInvalidTableValue(source, "HpPerSoldier must be greater than zero.");
                }

                ValidateFiniteNonNegative(row.MoveSpeed, source, "MoveSpeed");
                AddUnique(
                    snapshots,
                    row.Id,
                    new ArmyConfigSnapshot(row.Id, row.ArmyCountLimit, row.HpPerSoldier, row.MoveSpeed),
                    source);
            }

            return snapshots;
        }

        private static Dictionary<int, WeaponConfigSnapshot> BuildWeaponSnapshots(cfg.Tables tables)
        {
            var rows = tables.TbWeapon.DataList;
            ValidateFirstTableRow(rows.Count, rows.Count > 0 ? rows[0].Id : -1, "TbWeapon");

            var snapshots = new Dictionary<int, WeaponConfigSnapshot>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var source = $"TbWeapon[{index}]";
                ValidateNonNegativeId(row.Id, source);
                ValidateFinitePositive(row.FireInterval, source, "FireInterval");
                if (row.BulletId < 0)
                {
                    ThrowInvalidTableValue(source, "BulletId must be non-negative.");
                }

                AddUnique(
                    snapshots,
                    row.Id,
                    new WeaponConfigSnapshot(row.Id, row.FireInterval, row.BulletId),
                    source);
            }

            return snapshots;
        }

        private static Dictionary<int, BulletConfigSnapshot> BuildBulletSnapshots(cfg.Tables tables)
        {
            var rows = tables.TbBullet.DataList;
            ValidateFirstTableRow(rows.Count, rows.Count > 0 ? rows[0].Id : -1, "TbBullet");

            var snapshots = new Dictionary<int, BulletConfigSnapshot>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var source = $"TbBullet[{index}]";
                ValidateNonNegativeId(row.Id, source);
                if (row.Damage <= 0)
                {
                    ThrowInvalidTableValue(source, "Damage must be greater than zero.");
                }

                ValidateFiniteNonNegative(row.MoveSpeed, source, "MoveSpeed");
                AddUnique(
                    snapshots,
                    row.Id,
                    new BulletConfigSnapshot(row.Id, row.Damage, row.MoveSpeed),
                    source);
            }

            return snapshots;
        }

        private static Dictionary<int, EnemyConfigSnapshot> BuildEnemySnapshots(cfg.Tables tables)
        {
            var rows = tables.TbEnemy.DataList;
            ValidateFirstTableRow(rows.Count, rows.Count > 0 ? rows[0].Id : -1, "TbEnemy");

            var snapshots = new Dictionary<int, EnemyConfigSnapshot>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var source = $"TbEnemy[{index}]";
                ValidateNonNegativeId(row.Id, source);
                var enemyType = ConvertEnemyType(row.EnemyType, source);
                if (row.MaxHp <= 0)
                {
                    ThrowInvalidTableValue(source, "MaxHp must be greater than zero.");
                }

                if (row.AttackPower <= 0)
                {
                    ThrowInvalidTableValue(source, "AttackPower must be greater than zero.");
                }

                ValidateFiniteNonNegative(row.MoveSpeed, source, "MoveSpeed");
                ValidateFiniteNonNegative(row.AttackStartRange, source, "AttackStartRange");
                ValidateFiniteNonNegative(row.AttackCooldown, source, "AttackCooldown");

                AddUnique(
                    snapshots,
                    row.Id,
                    new EnemyConfigSnapshot(
                        row.Id,
                        enemyType,
                        row.MaxHp,
                        row.AttackPower,
                        row.MoveSpeed,
                        row.AttackStartRange,
                        row.AttackCooldown),
                    source);
            }

            return snapshots;
        }

        private static Dictionary<int, PropConfigSnapshot> BuildPropSnapshots(cfg.Tables tables)
        {
            var rows = tables.TbProp.DataList;
            ValidateFirstTableRow(rows.Count, rows.Count > 0 ? rows[0].Id : -1, "TbProp");

            var snapshots = new Dictionary<int, PropConfigSnapshot>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var source = $"TbProp[{index}]";
                ValidateNonNegativeId(row.Id, source);
                if (row.WeaponId < 0)
                {
                    ThrowInvalidTableValue(source, "WeaponId must be non-negative.");
                }

                if (row.MaxHp <= 0)
                {
                    ThrowInvalidTableValue(source, "MaxHp must be greater than zero.");
                }

                if (row.ContactDamage <= 0)
                {
                    ThrowInvalidTableValue(source, "ContactDamage must be greater than zero.");
                }

                ValidateFiniteNonNegative(row.MoveSpeed, source, "MoveSpeed");
                AddUnique(
                    snapshots,
                    row.Id,
                    new PropConfigSnapshot(
                        row.Id,
                        row.WeaponId,
                        row.MaxHp,
                        row.ContactDamage,
                        row.MoveSpeed),
                    source);
            }

            return snapshots;
        }

        private static void ValidateFixedWeapons(Dictionary<int, WeaponConfigSnapshot> weapons)
        {
            for (var weaponId = 0; weaponId <= 2; weaponId++)
            {
                if (!weapons.ContainsKey(weaponId))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        $"TbWeapon.Id={weaponId}",
                        $"Required MVP weapon row {weaponId} is missing.");
                }
            }
        }

        private static void ValidateTableReferences(
            cfg.Tables tables,
            Dictionary<int, WeaponConfigSnapshot> weapons,
            Dictionary<int, BulletConfigSnapshot> bullets,
            Dictionary<int, PropConfigSnapshot> props)
        {
            var weaponRows = tables.TbWeapon.DataList;
            for (var index = 0; index < weaponRows.Count; index++)
            {
                var row = weaponRows[index];
                if (!bullets.ContainsKey(row.BulletId))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        $"TbWeapon[{index}].BulletId",
                        $"Bullet ID {row.BulletId} does not exist in TbBullet.");
                }
            }

            var propRows = tables.TbProp.DataList;
            for (var index = 0; index < propRows.Count; index++)
            {
                var row = propRows[index];
                if (!props.ContainsKey(row.Id))
                {
                    continue;
                }

                if (!weapons.ContainsKey(row.WeaponId))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        $"TbProp[{index}].WeaponId",
                        $"Weapon ID {row.WeaponId} does not exist in TbWeapon.");
                }
            }
        }

        private static void BuildLevelSnapshots(
            LevelCatalog catalog,
            Dictionary<int, EnemyConfigSnapshot> enemies,
            Dictionary<int, PropConfigSnapshot> props,
            out IReadOnlyList<LevelDescriptor> descriptors,
            out Dictionary<int, LevelConfigSnapshot> levels)
        {
            var entries = catalog.Entries;
            var catalogLevelIds = new HashSet<int>();
            var configs = new LevelConfig[entries.Count];

            // 先冻结目录 ID 集，再校验关卡内容，避免 unlockedLevelIds 依赖目录遍历顺序。
            for (var index = 0; index < entries.Count; index++)
            {
                var entrySource = $"LevelCatalog.entries[{index}]";
                var entry = entries[index];
                if (entry == null)
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        entrySource,
                        "The catalog entry is null.");
                }

                var levelConfig = entry.LevelConfig;
                if (levelConfig == null)
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        $"{entrySource}.levelConfig",
                        "The LevelConfig reference is null.");
                }

                if (levelConfig.LevelId < 0)
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.InvalidLevelConfig,
                        $"{entrySource}.levelConfig.levelId",
                        "LevelId must be non-negative.");
                }

                if (!catalogLevelIds.Add(levelConfig.LevelId))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.DuplicateLevelId,
                        $"{entrySource}.levelConfig.levelId",
                        $"LevelId {levelConfig.LevelId} is duplicated in the catalog.");
                }

                if (index == 0 && !entry.InitiallyUnlocked)
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.InvalidLevelConfig,
                        $"{entrySource}.initiallyUnlocked",
                        "The first MVP level must be initially unlocked.");
                }

                configs[index] = levelConfig;
            }

            var descriptorList = new List<LevelDescriptor>(entries.Count);
            levels = new Dictionary<int, LevelConfigSnapshot>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var levelConfig = configs[index];
                var source = $"LevelCatalog.entries[{index}].levelConfig(LevelId={levelConfig.LevelId})";
                var snapshot = BuildLevelSnapshot(
                    levelConfig,
                    source,
                    catalogLevelIds,
                    enemies,
                    props);

                descriptorList.Add(
                    new LevelDescriptor(levelConfig.LevelId, levelConfig.DisplayName, entry.InitiallyUnlocked));
                levels.Add(levelConfig.LevelId, snapshot);
            }

            descriptors = Array.AsReadOnly(descriptorList.ToArray());
        }

        private static LevelConfigSnapshot BuildLevelSnapshot(
            LevelConfig levelConfig,
            string source,
            HashSet<int> catalogLevelIds,
            Dictionary<int, EnemyConfigSnapshot> enemies,
            Dictionary<int, PropConfigSnapshot> props)
        {
            ValidateRoadConfiguration(levelConfig, source);
            ValidateLevelLines(levelConfig, source);

            var unlockedLevelIds = BuildUnlockedLevelIds(levelConfig, source, catalogLevelIds);
            var enemySpawns = BuildEnemySpawnSnapshots(levelConfig, source, enemies);
            var gateSpawns = BuildGateSpawnSnapshots(levelConfig, source, out var containsElementGate);
            var propSpawns = BuildPropSpawnSnapshots(levelConfig, source, props);

            var durationFactor = levelConfig.ElementDurationSecondsPerDamage;
            if (containsElementGate)
            {
                if (!IsFinite(durationFactor) || durationFactor <= 0f)
                {
                    ThrowInvalidLevel(
                        $"{source}.elementDurationSecondsPerDamage",
                        "A level with element gates requires a finite value greater than zero.");
                }
            }
            else if (!IsFinite(durationFactor) || durationFactor != 0f)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.InvalidLevelConfig,
                    $"{source}.elementDurationSecondsPerDamage",
                    "A level without element gates must use the neutral value zero.");
            }

            return new LevelConfigSnapshot(
                levelConfig.LevelId,
                levelConfig.DisplayName,
                unlockedLevelIds,
                levelConfig.RoadWidth,
                levelConfig.RoadHeight,
                levelConfig.ArmySpawnPosition,
                levelConfig.SpawnY,
                levelConfig.EnemyApproachY,
                levelConfig.DespawnY,
                enemySpawns,
                gateSpawns,
                propSpawns,
                durationFactor);
        }

        private static int[] BuildUnlockedLevelIds(
            LevelConfig levelConfig,
            string source,
            HashSet<int> catalogLevelIds)
        {
            var values = levelConfig.UnlockedLevelIds;
            if (values == null)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.InvalidLevelConfig,
                    $"{source}.unlockedLevelIds",
                    "UnlockedLevelIds cannot be null.");
            }

            var seen = new HashSet<int>();
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                var itemSource = $"{source}.unlockedLevelIds[{index}]";
                if (value < 0)
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.InvalidLevelConfig,
                        itemSource,
                        "Unlocked LevelId must be non-negative.");
                }

                if (!seen.Add(value))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.InvalidLevelConfig,
                        itemSource,
                        $"Unlocked LevelId {value} is duplicated.");
                }

                if (value == levelConfig.LevelId)
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.InvalidLevelConfig,
                        itemSource,
                        "A level cannot unlock itself.");
                }
            }

            var filtered = new List<int>(values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                if (catalogLevelIds.Contains(value))
                {
                    filtered.Add(value);
                    continue;
                }

                // ADR-044 唯一允许的非致命配置引用：保留警告，但不把缺失 ID 带入运行时。
                Debug.LogWarning(
                    $"[ConfigService] Code={ConfigErrorCode.LevelNotFound}; " +
                    $"Source={source}.unlockedLevelIds[{index}]; " +
                    $"Reason=LevelId {value} is not present in the current catalog and was filtered from the snapshot.");
            }

            return filtered.ToArray();
        }

        private static EnemySpawnEntrySnapshot[] BuildEnemySpawnSnapshots(
            LevelConfig levelConfig,
            string source,
            Dictionary<int, EnemyConfigSnapshot> enemies)
        {
            var entries = levelConfig.EnemySpawns;
            if (entries == null || entries.Count == 0)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.InvalidLevelConfig,
                    $"{source}.enemySpawns",
                    "EnemySpawns must contain at least one entry.");
            }

            var snapshots = new EnemySpawnEntrySnapshot[entries.Count];
            var previousSpawnTime = 0f;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var itemSource = $"{source}.enemySpawns[{index}]";
                if (entry == null)
                {
                    ThrowInvalidLevel(itemSource, "The spawn entry is null.");
                }

                ValidateSpawnCoordinates(entry.SpawnTime, entry.SpawnPosition, index, previousSpawnTime, itemSource);
                if (entry.ConfigId < 0)
                {
                    ThrowInvalidLevel($"{itemSource}.configId", "ConfigId must be non-negative.");
                }

                if (!enemies.ContainsKey(entry.ConfigId))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        $"{itemSource}.configId",
                        $"Enemy config ID {entry.ConfigId} does not exist in TbEnemy.");
                }

                snapshots[index] =
                    new EnemySpawnEntrySnapshot(entry.SpawnTime, entry.SpawnPosition, entry.ConfigId);
                previousSpawnTime = entry.SpawnTime;
            }

            return snapshots;
        }

        private static GateSpawnEntrySnapshot[] BuildGateSpawnSnapshots(
            LevelConfig levelConfig,
            string source,
            out bool containsElementGate)
        {
            var entries = levelConfig.GateSpawns;
            if (entries == null)
            {
                ThrowInvalidLevel($"{source}.gateSpawns", "GateSpawns cannot be null.");
            }

            containsElementGate = false;
            var snapshots = new GateSpawnEntrySnapshot[entries.Count];
            var previousSpawnTime = 0f;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var itemSource = $"{source}.gateSpawns[{index}]";
                if (entry == null)
                {
                    ThrowInvalidLevel(itemSource, "The spawn entry is null.");
                }

                ValidateSpawnCoordinates(entry.SpawnTime, entry.SpawnPosition, index, previousSpawnTime, itemSource);
                switch (entry.GateType)
                {
                    case GateType.Additive:
                        if (entry.InitialValue == int.MinValue)
                        {
                            ThrowInvalidLevel(
                                $"{itemSource}.initialValue",
                                "Additive gate InitialValue cannot be Int32.MinValue.");
                        }

                        if (entry.ElementType != ElementType.None || entry.MaxHp != 0)
                        {
                            ThrowInvalidLevel(
                                itemSource,
                                "An additive gate requires ElementType=None and MaxHp=0.");
                        }

                        break;
                    case GateType.Element:
                        containsElementGate = true;
                        if (entry.InitialValue != 0)
                        {
                            ThrowInvalidLevel(
                                $"{itemSource}.initialValue",
                                "An element gate requires InitialValue=0.");
                        }

                        if (entry.ElementType != ElementType.Fire &&
                            entry.ElementType != ElementType.Ice &&
                            entry.ElementType != ElementType.Lightning)
                        {
                            ThrowInvalidLevel(
                                $"{itemSource}.elementType",
                                "An element gate requires Fire, Ice, or Lightning.");
                        }

                        if (entry.MaxHp <= 0)
                        {
                            ThrowInvalidLevel(
                                $"{itemSource}.maxHp",
                                "An element gate requires MaxHp greater than zero.");
                        }

                        break;
                    default:
                        ThrowInvalidLevel($"{itemSource}.gateType", "GateType is not supported.");
                        break;
                }

                snapshots[index] = new GateSpawnEntrySnapshot(
                    entry.SpawnTime,
                    entry.SpawnPosition,
                    entry.GateType,
                    entry.InitialValue,
                    entry.ElementType,
                    entry.MaxHp);
                previousSpawnTime = entry.SpawnTime;
            }

            return snapshots;
        }

        private static PropSpawnEntrySnapshot[] BuildPropSpawnSnapshots(
            LevelConfig levelConfig,
            string source,
            Dictionary<int, PropConfigSnapshot> props)
        {
            var entries = levelConfig.PropSpawns;
            if (entries == null)
            {
                ThrowInvalidLevel($"{source}.propSpawns", "PropSpawns cannot be null.");
            }

            var snapshots = new PropSpawnEntrySnapshot[entries.Count];
            var previousSpawnTime = 0f;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var itemSource = $"{source}.propSpawns[{index}]";
                if (entry == null)
                {
                    ThrowInvalidLevel(itemSource, "The spawn entry is null.");
                }

                ValidateSpawnCoordinates(entry.SpawnTime, entry.SpawnPosition, index, previousSpawnTime, itemSource);
                if (entry.ConfigId < 0)
                {
                    ThrowInvalidLevel($"{itemSource}.configId", "ConfigId must be non-negative.");
                }

                if (!props.ContainsKey(entry.ConfigId))
                {
                    throw new ConfigValidationException(
                        ConfigErrorCode.MissingReference,
                        $"{itemSource}.configId",
                        $"Prop config ID {entry.ConfigId} does not exist in TbProp.");
                }

                snapshots[index] =
                    new PropSpawnEntrySnapshot(entry.SpawnTime, entry.SpawnPosition, entry.ConfigId);
                previousSpawnTime = entry.SpawnTime;
            }

            return snapshots;
        }

        private static void ValidateRoadConfiguration(LevelConfig levelConfig, string source)
        {
            var width = levelConfig.RoadWidth;
            var height = levelConfig.RoadHeight;
            if (!IsFinite(width) || width <= 0f)
            {
                ThrowInvalidLevel($"{source}.roadWidth", "RoadWidth must be finite and greater than zero.");
            }

            if (!IsFinite(height) || height <= 0f)
            {
                ThrowInvalidLevel($"{source}.roadHeight", "RoadHeight must be finite and greater than zero.");
            }

            var armySpawnPosition = levelConfig.ArmySpawnPosition;
            if (!IsFinite(armySpawnPosition.x) || !IsFinite(armySpawnPosition.y))
            {
                ThrowInvalidLevel(
                    $"{source}.armySpawnPosition",
                    "ArmySpawnPosition components must be finite.");
            }

            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            if (armySpawnPosition.x < -halfWidth || armySpawnPosition.x > halfWidth ||
                armySpawnPosition.y < -halfHeight || armySpawnPosition.y > halfHeight)
            {
                ThrowInvalidLevel(
                    $"{source}.armySpawnPosition",
                    "ArmySpawnPosition must be within the centered road bounds.");
            }
        }

        private static void ValidateLevelLines(LevelConfig levelConfig, string source)
        {
            var spawnY = levelConfig.SpawnY;
            var enemyApproachY = levelConfig.EnemyApproachY;
            var despawnY = levelConfig.DespawnY;
            if (!IsFinite(spawnY) || !IsFinite(enemyApproachY) || !IsFinite(despawnY))
            {
                ThrowInvalidLevel(source, "SpawnY, EnemyApproachY, and DespawnY must be finite.");
            }

            var armyY = levelConfig.ArmySpawnPosition.y;
            var bottomBoundary = levelConfig.RoadHeight * -0.5f;
            var topBoundary = levelConfig.RoadHeight * 0.5f;
            if (despawnY < bottomBoundary || despawnY >= armyY ||
                enemyApproachY <= armyY || enemyApproachY >= spawnY ||
                spawnY > topBoundary)
            {
                ThrowInvalidLevel(
                    source,
                    "Level lines must satisfy BottomBoundary <= DespawnY < ArmySpawnPosition.y < EnemyApproachY < SpawnY <= TopBoundary.");
            }
        }

        private static void ValidateSpawnCoordinates(
            float spawnTime,
            float spawnPosition,
            int index,
            float previousSpawnTime,
            string source)
        {
            if (!IsFinite(spawnTime) || spawnTime < 0f)
            {
                ThrowInvalidLevel($"{source}.spawnTime", "SpawnTime must be finite and non-negative.");
            }

            if (index > 0 && spawnTime < previousSpawnTime)
            {
                ThrowInvalidLevel($"{source}.spawnTime", "Spawn entries must be sorted by non-decreasing SpawnTime.");
            }

            if (!IsFinite(spawnPosition) || spawnPosition < 0f || spawnPosition > 1f)
            {
                ThrowInvalidLevel($"{source}.spawnPosition", "SpawnPosition must be finite and within [0,1].");
            }
        }

        private static void ValidateFirstTableRow(int count, int firstId, string tableName)
        {
            if (count == 0)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.TableLoadFailed,
                    tableName,
                    "The table must contain at least one row.");
            }

            if (firstId != 0)
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.TableLoadFailed,
                    $"{tableName}[0].Id",
                    "The first row must use ID 0.");
            }
        }

        private static void ValidateNonNegativeId(int id, string source)
        {
            if (id < 0)
            {
                ThrowInvalidTableValue($"{source}.Id", "ID must be non-negative.");
            }
        }

        private static void ValidateFinitePositive(float value, string source, string fieldName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                ThrowInvalidTableValue(source, $"{fieldName} must be finite and greater than zero.");
            }
        }

        private static void ValidateFiniteNonNegative(float value, string source, string fieldName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                ThrowInvalidTableValue(source, $"{fieldName} must be finite and non-negative.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static RuntimeEnemyType ConvertEnemyType(cfg.game.EnemyType value, string source)
        {
            switch (value)
            {
                case cfg.game.EnemyType.Normal:
                    return RuntimeEnemyType.Normal;
                case cfg.game.EnemyType.Elite:
                    return RuntimeEnemyType.Elite;
                case cfg.game.EnemyType.Boss:
                    return RuntimeEnemyType.Boss;
                default:
                    ThrowInvalidTableValue($"{source}.EnemyType", $"EnemyType value {(int)value} is not supported.");
                    return default;
            }
        }

        private static void AddUnique<T>(
            Dictionary<int, T> snapshots,
            int id,
            T snapshot,
            string source)
        {
            if (snapshots.ContainsKey(id))
            {
                throw new ConfigValidationException(
                    ConfigErrorCode.TableLoadFailed,
                    $"{source}.Id",
                    $"ID {id} is duplicated.");
            }

            snapshots.Add(id, snapshot);
        }

        private static T GetRequired<T>(Dictionary<int, T> snapshots, int id, string configType)
        {
            if (!snapshots.TryGetValue(id, out var snapshot))
            {
                throw new KeyNotFoundException(
                    $"{configType} config ID {id} was not registered during configuration initialization.");
            }

            return snapshot;
        }

        private static void ThrowInvalidTableValue(string source, string reason)
        {
            throw new ConfigValidationException(ConfigErrorCode.TableLoadFailed, source, reason);
        }

        private static void ThrowInvalidLevel(string source, string reason)
        {
            throw new ConfigValidationException(ConfigErrorCode.InvalidLevelConfig, source, reason);
        }

        private void EnsureReady()
        {
            if (state != ConfigLoadState.Ready)
            {
                throw new InvalidOperationException(
                    $"ConfigService queries require Ready state. Current state: {state}.");
            }
        }

        private void FailAndTerminate(ConfigErrorCode code, string source, string reason)
        {
            ClearSnapshots();
            state = ConfigLoadState.Failed;
            Debug.LogError($"[ConfigService] Code={code}; Source={source}; Reason={reason}");
            TerminateApplication();
        }

        private void ClearSnapshots()
        {
            levelDescriptors = EmptyLevelDescriptors;
            levelsById = new Dictionary<int, LevelConfigSnapshot>();
            armiesById = new Dictionary<int, ArmyConfigSnapshot>();
            weaponsById = new Dictionary<int, WeaponConfigSnapshot>();
            bulletsById = new Dictionary<int, BulletConfigSnapshot>();
            enemiesById = new Dictionary<int, EnemyConfigSnapshot>();
            propsById = new Dictionary<int, PropConfigSnapshot>();
        }

        private static void TerminateApplication()
        {
#if UNITY_EDITOR
            // 配置失败属于应用启动失败；Editor 停止 Play，Player 直接退出。
            if (Application.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#else
            Application.Quit();
#endif
        }

        private sealed class ConfigValidationException : Exception
        {
            public ConfigValidationException(ConfigErrorCode code, string source, string message)
                : base(message)
            {
                Code = code;
                ConfigSource = source;
            }

            public ConfigErrorCode Code { get; }
            public string ConfigSource { get; }
        }
    }
}
