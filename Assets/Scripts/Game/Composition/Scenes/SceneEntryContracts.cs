using Game.Contracts;
using Game.Foundation;

namespace Game.Composition
{
    internal interface IAppSceneEntry
    {
        AppSceneId SceneId { get; }
        void Initialize(SceneRuntimeLoadRequest request, SceneRuntimeDependencies dependencies);
        void Cleanup();
    }
}
