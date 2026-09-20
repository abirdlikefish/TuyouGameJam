using System;
using Game.Contracts;

namespace Game.Foundation
{
    public readonly struct SceneRuntimeDependencies
    {
        public SceneRuntimeDependencies(
            IGameStateService gameStateService,
            ITimeService timeService,
            IEventBus eventBus,
            IConfigService configService,
            IPoolService poolService)
        {
            GameStateService = gameStateService ?? throw new ArgumentNullException(nameof(gameStateService));
            TimeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            ConfigService = configService ?? throw new ArgumentNullException(nameof(configService));
            PoolService = poolService ?? throw new ArgumentNullException(nameof(poolService));
        }

        public IGameStateService GameStateService { get; }
        public ITimeService TimeService { get; }
        public IEventBus EventBus { get; }
        public IConfigService ConfigService { get; }
        public IPoolService PoolService { get; }
    }

    public readonly struct SceneRuntimeLoadRequest
    {
        public SceneRuntimeLoadRequest(
            AppSceneId sceneId,
            int levelId,
            int levelRunId,
            LevelConfigSnapshot levelConfig)
        {
            SceneId = sceneId;
            LevelId = levelId;
            LevelRunId = levelRunId;
            LevelConfig = levelConfig;
        }

        public AppSceneId SceneId { get; }
        public int LevelId { get; }
        public int LevelRunId { get; }
        public LevelConfigSnapshot LevelConfig { get; }
    }

    public readonly struct SceneRuntimeLoadResult
    {
        private SceneRuntimeLoadResult(
            bool succeeded,
            SceneLoadErrorCode errorCode,
            bool requiresCleanup)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            RequiresCleanup = requiresCleanup;
        }

        public bool Succeeded { get; }
        public SceneLoadErrorCode ErrorCode { get; }
        public bool RequiresCleanup { get; }

        public static SceneRuntimeLoadResult Success()
        {
            return new SceneRuntimeLoadResult(true, default(SceneLoadErrorCode), false);
        }

        public static SceneRuntimeLoadResult Failure(
            SceneLoadErrorCode errorCode,
            bool requiresCleanup)
        {
            return new SceneRuntimeLoadResult(false, errorCode, requiresCleanup);
        }
    }

    public interface ISceneRuntime
    {
        void Load(SceneRuntimeLoadRequest request, Action<SceneRuntimeLoadResult> completed);
        void UnloadCurrent(Action<bool> completed);
    }
}
