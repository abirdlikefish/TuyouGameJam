using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Contracts;
using UnityEngine;

namespace Game.Foundation
{
    public sealed class JsonPlayerProgressStore : IPlayerProgressStore
    {
        private const int CurrentSchemaVersion = 1;
        private const string FileName = "player-progress.json";
        private const string TemporaryFileName = "player-progress.tmp";
        private const string BackupFileName = "player-progress.bak";

        private readonly string directoryPath;
        private readonly string filePath;
        private readonly string temporaryFilePath;
        private readonly string backupFilePath;

        public JsonPlayerProgressStore(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException(
                    "Persistent data path must not be null or whitespace.",
                    nameof(persistentDataPath));
            }

            directoryPath = persistentDataPath;
            filePath = Path.Combine(directoryPath, FileName);
            temporaryFilePath = Path.Combine(directoryPath, TemporaryFileName);
            backupFilePath = Path.Combine(directoryPath, BackupFileName);
        }

        public bool TryLoad(out PlayerProgressSnapshot progress, out string diagnostic)
        {
            var primaryExists = File.Exists(filePath);
            var primaryError = string.Empty;
            if (primaryExists && TryRead(filePath, out progress, out primaryError))
            {
                diagnostic = string.Empty;
                return true;
            }

            var backupExists = File.Exists(backupFilePath);
            var backupError = string.Empty;
            if (backupExists && TryRead(backupFilePath, out progress, out backupError))
            {
                diagnostic = primaryExists
                    ? $"Primary progress file is invalid; loaded backup instead. PrimaryError={primaryError}"
                    : "Primary progress file is missing; loaded backup instead.";
                return true;
            }

            progress = PlayerProgressSnapshot.Empty;
            if (!primaryExists && !backupExists)
            {
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = primaryExists && backupExists
                ? $"Progress files are unreadable. PrimaryError={primaryError}; BackupError={backupError}"
                : primaryExists
                    ? $"Primary progress file is unreadable and no backup exists. Error={primaryError}"
                    : $"Backup progress file is unreadable and no primary exists. Error={backupError}";
            return false;
        }

        public bool TrySave(PlayerProgressSnapshot progress, out string diagnostic)
        {
            if (progress == null)
            {
                diagnostic = "Progress snapshot is null.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(directoryPath);
                DeleteIfExists(temporaryFilePath);

                var data = new ProgressFileData
                {
                    schemaVersion = CurrentSchemaVersion,
                    completedLevelIds = CopySortedUnique(progress.CompletedLevelIds),
                    unlockedLevelIds = CopySortedUnique(progress.UnlockedLevelIds)
                };
                var json = JsonUtility.ToJson(data, false);
                File.WriteAllText(temporaryFilePath, json, new UTF8Encoding(false));

                if (File.Exists(filePath))
                {
                    if (TryRead(filePath, out _, out _))
                    {
                        File.Copy(filePath, backupFilePath, true);
                    }

                    File.Delete(filePath);
                }

                File.Move(temporaryFilePath, filePath);
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteTemporaryFile();
                diagnostic = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static List<int> CopySortedUnique(IReadOnlyList<int> source)
        {
            var uniqueIds = new HashSet<int>();
            for (var index = 0; index < source.Count; index++)
            {
                var levelId = source[index];
                if (levelId < 0)
                {
                    throw new InvalidDataException($"Progress contains negative LevelId {levelId}.");
                }

                uniqueIds.Add(levelId);
            }

            var sortedIds = new List<int>(uniqueIds);
            sortedIds.Sort();
            return sortedIds;
        }

        private static bool TryRead(
            string path,
            out PlayerProgressSnapshot progress,
            out string error)
        {
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<ProgressFileData>(json);
                if (data == null)
                {
                    throw new InvalidDataException("JSON root is null.");
                }

                if (data.schemaVersion != CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported schema version {data.schemaVersion}; expected {CurrentSchemaVersion}.");
                }

                if (data.completedLevelIds == null || data.unlockedLevelIds == null)
                {
                    throw new InvalidDataException("Progress level ID arrays must not be null.");
                }

                progress = new PlayerProgressSnapshot(
                    data.completedLevelIds,
                    data.unlockedLevelIds);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                progress = PlayerProgressSnapshot.Empty;
                error = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void TryDeleteTemporaryFile()
        {
            try
            {
                DeleteIfExists(temporaryFilePath);
            }
            catch
            {
                // 临时文件只用于下一次写入前清理，删除失败不能覆盖原始存储错误。
            }
        }

        [Serializable]
        private sealed class ProgressFileData
        {
            public int schemaVersion;
            public List<int> completedLevelIds;
            public List<int> unlockedLevelIds;
        }
    }
}
