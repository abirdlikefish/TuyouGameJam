using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class GameplayInputAdapter : MonoBehaviour, IGameplayInputController
    {
        [SerializeField] private TouchDragInput touchDragInput;

        private IHorizontalInputReceiver receiver;
        private bool gameplayEnabled;

        public TouchDragInput TouchDragInput => touchDragInput;

        public void Initialize(IHorizontalInputReceiver inputReceiver)
        {
            if (inputReceiver == null)
            {
                throw new ArgumentNullException(nameof(inputReceiver));
            }

            if (receiver != null && !ReferenceEquals(receiver, inputReceiver))
            {
                throw new InvalidOperationException(
                    "GameplayInputAdapter cannot replace its input receiver after initialization.");
            }

            receiver = inputReceiver;
            receiver.SetHorizontalInput(0f);
        }

        public bool TryValidate(out string error)
        {
            if (touchDragInput == null)
            {
                error = $"{name}.touchDragInput is not assigned.";
                return false;
            }

            return touchDragInput.TryValidate(out error);
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (receiver == null)
            {
                if (!enabled)
                {
                    gameplayEnabled = false;
                    touchDragInput?.ResetInput();
                    return;
                }

                throw new InvalidOperationException("GameplayInputAdapter must be initialized before it is enabled.");
            }

            if (gameplayEnabled == enabled)
            {
                if (!enabled)
                {
                    touchDragInput?.ResetInput();
                }

                return;
            }

            gameplayEnabled = enabled;
            if (!enabled)
            {
                touchDragInput?.ResetInput();
                receiver.SetHorizontalInput(0f);
            }
        }

        public void TickInput(float unscaledDeltaTime)
        {
            if (receiver == null)
            {
                throw new InvalidOperationException("GameplayInputAdapter must be initialized before ticking.");
            }

            var value = gameplayEnabled && touchDragInput != null
                ? touchDragInput.ConsumeHorizontalInput(unscaledDeltaTime)
                : 0f;
            receiver.SetHorizontalInput(value);
        }

        private void OnDisable()
        {
            SetGameplayEnabled(false);
        }
    }
}
