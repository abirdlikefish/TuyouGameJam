using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ArmySlotHitProxy : MonoBehaviour
    {
        [SerializeField] private ArmySlotView target;

        private int armyId = -1;
        private int slotIndex = -1;

        public ArmySlotView Target => target;
        public int ArmyId => armyId;
        public int SlotIndex => slotIndex;
        public bool IsInitialized => target != null && armyId >= 0 && slotIndex >= 0;

        internal void Initialize(int initializedArmyId, int initializedSlotIndex)
        {
            armyId = initializedArmyId;
            slotIndex = initializedSlotIndex;
        }

        internal void ResetRuntimeIdentity()
        {
            armyId = -1;
            slotIndex = -1;
        }
    }
}
