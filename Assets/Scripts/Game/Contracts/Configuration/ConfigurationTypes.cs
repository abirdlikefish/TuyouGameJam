namespace Game.Contracts
{
    public enum ConfigLoadState
    {
        Uninitialized = 0,
        Loading = 1,
        Ready = 2,
        Failed = 3
    }

    public enum ConfigErrorCode
    {
        EmptyCatalog = 0,
        DuplicateLevelId = 1,
        LevelNotFound = 2,
        MissingReference = 3,
        InvalidLevelConfig = 4,
        TableLoadFailed = 5,
        ResourceMissing = 6
    }
}
