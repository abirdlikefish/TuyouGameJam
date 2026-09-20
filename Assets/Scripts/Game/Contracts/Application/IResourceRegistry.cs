using UnityEngine;

namespace Game.Contracts
{
    public interface IResourceRegistry
    {
        bool TryGet<T>(string key, out T asset) where T : Object;
    }
}
