using System;
using Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BattleResultView : MonoBehaviour
    {
        [Header("结算信息")]
        [SerializeField] private TMP_Text totalTimeText;
        [SerializeField] private TMP_Text killedEnemyText;
        [SerializeField] private Button returnButton;

        private IGameplayHudSource hudSource;
        private IGameStateService gameStateService;
        private IEventBus eventBus;
        private SubscriptionToken victorySubscription;
        private SubscriptionToken gameOverSubscription;
        private int levelRunId;
        private bool resultShown;
        private bool actionRequested;
        private bool initialized;

        public void Initialize(
            int initializedLevelRunId,
            IGameplayHudSource initializedHudSource,
            IGameStateService initializedGameStateService,
            IEventBus initializedEventBus)
        {
            if (initialized)
            {
                throw new InvalidOperationException("BattleResultView is already initialized.");
            }

            if (initializedLevelRunId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initializedLevelRunId));
            }

            hudSource = initializedHudSource ?? throw new ArgumentNullException(nameof(initializedHudSource));
            gameStateService = initializedGameStateService ??
                throw new ArgumentNullException(nameof(initializedGameStateService));
            eventBus = initializedEventBus ?? throw new ArgumentNullException(nameof(initializedEventBus));
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            levelRunId = initializedLevelRunId;
            returnButton.onClick.AddListener(HandleReturnClicked);
            victorySubscription = eventBus.Subscribe<Victory>(OnVictory);
            gameOverSubscription = eventBus.Subscribe<GameOver>(OnGameOver);
            resultShown = false;
            actionRequested = false;
            initialized = true;
            returnButton.interactable = false;
            gameObject.SetActive(false);
        }

        public bool TryValidate(out string error)
        {
            if (totalTimeText == null || !totalTimeText.transform.IsChildOf(transform))
            {
                error = "BattleResultView.totalTimeText must belong to the BattleResult hierarchy.";
                return false;
            }

            if (killedEnemyText == null || !killedEnemyText.transform.IsChildOf(transform))
            {
                error = "BattleResultView.killedEnemyText must belong to the BattleResult hierarchy.";
                return false;
            }

            if (returnButton == null || !returnButton.transform.IsChildOf(transform))
            {
                error = "BattleResultView.returnButton must belong to the BattleResult hierarchy.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Cleanup()
        {
            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(HandleReturnClicked);
                returnButton.interactable = false;
            }

            if (eventBus != null)
            {
                Unsubscribe(gameOverSubscription);
                Unsubscribe(victorySubscription);
            }

            gameOverSubscription = default(SubscriptionToken);
            victorySubscription = default(SubscriptionToken);
            hudSource = null;
            gameStateService = null;
            eventBus = null;
            levelRunId = 0;
            resultShown = false;
            actionRequested = false;
            initialized = false;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnVictory(Victory victory)
        {
            if (victory.LevelRunId == levelRunId)
            {
                ShowResult();
            }
        }

        private void OnGameOver(GameOver gameOver)
        {
            if (gameOver.LevelRunId == levelRunId)
            {
                ShowResult();
            }
        }

        private void ShowResult()
        {
            if (!initialized || resultShown)
            {
                return;
            }

            var snapshot = hudSource.GetHudSnapshot();
            if (!snapshot.IsCompleted || snapshot.LevelRunId != levelRunId)
            {
                throw new InvalidOperationException(
                    "BattleResultView received a result before the matching HUD snapshot was frozen.");
            }

            totalTimeText.text = $"TOTAL TIME: {FormatElapsedTime(snapshot.ElapsedTime)}";
            killedEnemyText.text = $"KILLS: {snapshot.KilledEnemyCount}";
            resultShown = true;
            returnButton.interactable = true;
            gameObject.SetActive(true);
        }

        private void HandleReturnClicked()
        {
            if (!initialized || !resultShown || actionRequested)
            {
                return;
            }

            actionRequested = true;
            returnButton.interactable = false;
            if (gameStateService.TryReturnToLevelSelect())
            {
                return;
            }

            if (initialized)
            {
                actionRequested = false;
                returnButton.interactable = true;
            }
        }

        private void Unsubscribe(SubscriptionToken token)
        {
            if (token.IsValid)
            {
                eventBus.Unsubscribe(token);
            }
        }

        private static string FormatElapsedTime(float elapsedTime)
        {
            var totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, elapsedTime));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
