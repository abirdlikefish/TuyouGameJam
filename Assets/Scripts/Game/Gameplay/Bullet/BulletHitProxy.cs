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

    internal static class BulletTargetPhysicsAdapter
    {
        internal static bool TryValidate(
            Collider2D bodyCollider,
            GameObject targetRoot,
            out string error)
        {
            var rigidbody = bodyCollider.attachedRigidbody;
            if (rigidbody == null || rigidbody.gameObject != targetRoot)
            {
                error = $"{targetRoot.name} requires a root Rigidbody2D attached to its body collider.";
                return false;
            }

            if (rigidbody.bodyType != RigidbodyType2D.Kinematic ||
                !rigidbody.simulated ||
                rigidbody.useFullKinematicContacts ||
                !Mathf.Approximately(rigidbody.gravityScale, 0f) ||
                rigidbody.collisionDetectionMode != CollisionDetectionMode2D.Discrete ||
                rigidbody.interpolation != RigidbodyInterpolation2D.None ||
                (rigidbody.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
            {
                error = $"{targetRoot.name}.Rigidbody2D must be a simulated Kinematic query adapter " +
                        "with zero gravity, Discrete detection, no interpolation, frozen rotation and " +
                        "Full Kinematic Contacts disabled.";
                return false;
            }

            if (!bodyCollider.isTrigger)
            {
                error = $"{bodyCollider.name} must remain a Trigger collider.";
                return false;
            }

            error = string.Empty;
            return true;
        }
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
