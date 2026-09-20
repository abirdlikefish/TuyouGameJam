using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    internal enum BulletTargetKind
    {
        Enemy = 0,
        Gate = 1,
        Prop = 2
    }

    internal interface IRuntimeBulletTarget : IBulletHittable
    {
        int RuntimeInstanceId { get; }
        BulletTargetKind BulletTargetKind { get; }
    }

    public sealed class BulletHitProxy : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour target;

        public MonoBehaviour TargetBehaviour => target;

        internal bool TryGetTarget(out IRuntimeBulletTarget runtimeTarget)
        {
            runtimeTarget = target as IRuntimeBulletTarget;
            return runtimeTarget != null;
        }

        internal bool TryValidate(out string error)
        {
            if (target == null)
            {
                error = $"{name}.target is not assigned.";
                return false;
            }

            if (!(target is IRuntimeBulletTarget))
            {
                error = $"{name}.target must implement the runtime bullet target contract.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
