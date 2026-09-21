using UnityEngine;

namespace Game.Gameplay
{
    public sealed class ArmySlotAnimationEventProxy : MonoBehaviour
    {
        [SerializeField] private ArmySlotView target;

        public ArmySlotView Target => target;

        public void OnDeathAnimationFinished()
        {
            if (target != null)
            {
                target.HandleDeathAnimationFinished();
            }
        }
    }
}
