using System;
using System.Collections.Generic;

namespace Game.Contracts
{
    public sealed class PlayerProgressSnapshot
    {
        private static readonly IReadOnlyList<int> EmptyIds = Array.AsReadOnly(new int[0]);

        public PlayerProgressSnapshot(
            IReadOnlyList<int> completedLevelIds,
            IReadOnlyList<int> unlockedLevelIds)
        {
            CompletedLevelIds = Copy(completedLevelIds, nameof(completedLevelIds));
            UnlockedLevelIds = Copy(unlockedLevelIds, nameof(unlockedLevelIds));
        }

        public IReadOnlyList<int> CompletedLevelIds { get; }
        public IReadOnlyList<int> UnlockedLevelIds { get; }

        public static PlayerProgressSnapshot Empty =>
            new PlayerProgressSnapshot(EmptyIds, EmptyIds);

        private static IReadOnlyList<int> Copy(IReadOnlyList<int> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new int[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public interface IPlayerProgressStore
    {
        bool TryLoad(out PlayerProgressSnapshot progress, out string diagnostic);
        bool TrySave(PlayerProgressSnapshot progress, out string diagnostic);
    }
}

