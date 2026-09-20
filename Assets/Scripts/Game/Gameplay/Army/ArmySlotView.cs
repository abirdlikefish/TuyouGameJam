using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ArmySlotView : MonoBehaviour
    {
        [SerializeField] private GameObject soldierVisual;
        [SerializeField] private Collider2D slotCollider;
        [SerializeField] private ArmySlotHitProxy slotHitProxy;
        [SerializeField] private Transform firePoint;

        private int slotIndex = -1;
        private int representedCount;
        private int currentHp;
        private int maxHp;
        private float fireCooldownRemaining;

        public int SlotIndex => slotIndex;
        public int RepresentedCount => representedCount;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public bool IsActive => representedCount > 0;
        public Vector2 WorldPosition => transform.position;
        public Vector2 FirePosition => firePoint != null ? firePoint.position : transform.position;
        public Collider2D SlotCollider => slotCollider;
        public ArmySlotHitProxy SlotHitProxy => slotHitProxy;
        public float FireCooldownRemaining => fireCooldownRemaining;

        public bool TryValidate(out string error)
        {
            if (soldierVisual == null)
            {
                error = $"{name}.soldierVisual is not assigned.";
                return false;
            }

            if (slotCollider == null)
            {
                error = $"{name}.slotCollider is not assigned.";
                return false;
            }

            if (slotHitProxy == null)
            {
                error = $"{name}.slotHitProxy is not assigned.";
                return false;
            }

            if (firePoint == null)
            {
                error = $"{name}.firePoint is not assigned.";
                return false;
            }

            if (slotHitProxy.gameObject != slotCollider.gameObject)
            {
                error = $"{name}.slotHitProxy must be on the slotCollider GameObject.";
                return false;
            }

            if (slotHitProxy.Target != this)
            {
                error = $"{name}.slotHitProxy must explicitly reference this ArmySlotView.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal void Initialize(int armyId, int index)
        {
            slotIndex = index;
            slotHitProxy.Initialize(armyId, index);
            SetState(0, 0, 0, 0f);
        }

        internal void SetState(int count, int hp, int slotMaxHp, float cooldown)
        {
            representedCount = Mathf.Max(0, count);
            currentHp = Mathf.Max(0, hp);
            maxHp = Mathf.Max(0, slotMaxHp);
            fireCooldownRemaining = Mathf.Max(0f, cooldown);

            var active = representedCount > 0;
            soldierVisual.SetActive(active);
            slotCollider.enabled = active;
        }

        internal void SetFireCooldown(float value)
        {
            fireCooldownRemaining = Mathf.Max(0f, value);
        }

        internal ArmySlotSnapshot CreateSnapshot()
        {
            return new ArmySlotSnapshot(
                slotIndex,
                representedCount,
                currentHp,
                maxHp,
                IsActive);
        }

        internal void PrepareForRunStop()
        {
            representedCount = 0;
            currentHp = 0;
            maxHp = 0;
            fireCooldownRemaining = 0f;
            if (soldierVisual != null)
            {
                soldierVisual.SetActive(false);
            }

            if (slotCollider != null)
            {
                slotCollider.enabled = false;
            }
        }
    }
}
