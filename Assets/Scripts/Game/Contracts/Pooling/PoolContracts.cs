using UnityEngine;

namespace Game.Contracts
{
    public interface IPoolService
    {
        IComponentPool<T> GetOrCreatePool<T>(T prefab) where T : MonoBehaviour;
    }

    public interface IComponentPool<T> where T : MonoBehaviour
    {
        T RentInactive();
        bool Return(T instance);
    }
}
