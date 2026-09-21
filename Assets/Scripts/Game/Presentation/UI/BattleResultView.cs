using System;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BattleResultView : MonoBehaviour
    {
        [Header("结算信息")]
        [SerializeField] private ImageNumberText totalTimeNumber;
        [SerializeField] private ImageNumberText killedEnemyCountNumber;

        [Header("结算根节点")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private GameObject victoryWithNextRoot;
        [SerializeField] private GameObject victoryWithoutNextRoot;

        [Header("失败操作")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button gameOverReturnButton;

        [Header("有下一关的胜利操作")]
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button victoryWithNextReturnButton;

        [Header("无下一关的胜利操作")]
        [SerializeField] private Button victoryWithoutNextReturnButton;

        private IGameplayHudSource hudSource;
        private IGameStateService gameStateService;
        private IEventBus eventBus;
        private SubscriptionToken victorySubscription;
        private SubscriptionToken gameOverSubscription;
        private ResultPanel activePanel;
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
            retryButton.onClick.AddListener(HandleRetryClicked);
            gameOverReturnButton.onClick.AddListener(HandleReturnClicked);
            nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
            victoryWithNextReturnButton.onClick.AddListener(HandleReturnClicked);
            victoryWithoutNextReturnButton.onClick.AddListener(HandleReturnClicked);
            victorySubscription = eventBus.Subscribe<Victory>(OnVictory);
            gameOverSubscription = eventBus.Subscribe<GameOver>(OnGameOver);
            activePanel = ResultPanel.None;
            resultShown = false;
            actionRequested = false;
            initialized = true;
            SetAllButtonsInteractable(false);
            HideResultRoots();
            gameObject.SetActive(false);
        }

        public bool TryValidate(out string error)
        {
            if (!RequireImageNumber(totalTimeNumber, nameof(totalTimeNumber), out error) ||
                !RequireImageNumber(
                    killedEnemyCountNumber,
                    nameof(killedEnemyCountNumber),
                    out error) ||
                !RequireRoot(gameOverRoot, nameof(gameOverRoot), out error) ||
                !RequireRoot(victoryWithNextRoot, nameof(victoryWithNextRoot), out error) ||
                !RequireRoot(victoryWithoutNextRoot, nameof(victoryWithoutNextRoot), out error))
            {
                return false;
            }

            if (gameOverRoot == victoryWithNextRoot ||
                gameOverRoot == victoryWithoutNextRoot ||
                victoryWithNextRoot == victoryWithoutNextRoot)
            {
                error = "BattleResultView result roots must reference different objects.";
                return false;
            }

            if (!RequireButton(retryButton, gameOverRoot, nameof(retryButton), out error) ||
                !RequireButton(
                    gameOverReturnButton,
                    gameOverRoot,
                    nameof(gameOverReturnButton),
                    out error) ||
                !RequireButton(
                    nextLevelButton,
                    victoryWithNextRoot,
                    nameof(nextLevelButton),
                    out error) ||
                !RequireButton(
                    victoryWithNextReturnButton,
                    victoryWithNextRoot,
                    nameof(victoryWithNextReturnButton),
                    out error) ||
                !RequireButton(
                    victoryWithoutNextReturnButton,
                    victoryWithoutNextRoot,
                    nameof(victoryWithoutNextReturnButton),
                    out error))
            {
                return false;
            }

            var buttons = new HashSet<Button>();
            if (!buttons.Add(retryButton) ||
                !buttons.Add(gameOverReturnButton) ||
                !buttons.Add(nextLevelButton) ||
                !buttons.Add(victoryWithNextReturnButton) ||
                !buttons.Add(victoryWithoutNextReturnButton))
            {
                error = "BattleResultView buttons must reference different objects.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Cleanup()
        {
            RemoveButtonListener(retryButton, HandleRetryClicked);
            RemoveButtonListener(gameOverReturnButton, HandleReturnClicked);
            RemoveButtonListener(nextLevelButton, HandleNextLevelClicked);
            RemoveButtonListener(victoryWithNextReturnButton, HandleReturnClicked);
            RemoveButtonListener(victoryWithoutNextReturnButton, HandleReturnClicked);
            SetAllButtonsInteractable(false);

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
            activePanel = ResultPanel.None;
            levelRunId = 0;
            resultShown = false;
            actionRequested = false;
            initialized = false;
            HideResultRoots();
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnVictory(Victory victory)
        {
            if (victory.LevelRunId != levelRunId)
            {
                return;
            }

            ShowResult(
                victory.UnlockedLevelIds.Count > 0
                    ? ResultPanel.VictoryWithNext
                    : ResultPanel.VictoryWithoutNext);
        }

        private void OnGameOver(GameOver gameOver)
        {
            if (gameOver.LevelRunId == levelRunId)
            {
                ShowResult(ResultPanel.GameOver);
            }
        }

        private void ShowResult(ResultPanel resultPanel)
        {
            if (!initialized || resultShown || resultPanel == ResultPanel.None)
            {
                return;
            }

            var snapshot = hudSource.GetHudSnapshot();
            if (!snapshot.IsCompleted || snapshot.LevelRunId != levelRunId)
            {
                throw new InvalidOperationException(
                    "BattleResultView received a result before the matching HUD snapshot was frozen.");
            }

            totalTimeNumber.SetText(FormatElapsedTime(snapshot.ElapsedTime));
            killedEnemyCountNumber.SetNumber(snapshot.KilledEnemyCount);
            activePanel = resultPanel;
            resultShown = true;
            actionRequested = false;
            HideResultRoots();
            GetPanelRoot(resultPanel).SetActive(true);
            EnableActivePanelButtons();
            gameObject.SetActive(true);
        }

        private void HandleRetryClicked()
        {
            TryRequestAction(gameStateService.TryRetryCurrentGameplay);
        }

        private void HandleNextLevelClicked()
        {
            TryRequestAction(gameStateService.TryStartNextGameplay);
        }

        private void HandleReturnClicked()
        {
            TryRequestAction(gameStateService.TryReturnToLevelSelect);
        }

        private void TryRequestAction(Func<bool> request)
        {
            if (!initialized || !resultShown || actionRequested)
            {
                return;
            }

            actionRequested = true;
            SetAllButtonsInteractable(false);
            if (request())
            {
                return;
            }

            if (initialized)
            {
                actionRequested = false;
                EnableActivePanelButtons();
            }
        }

        private GameObject GetPanelRoot(ResultPanel resultPanel)
        {
            switch (resultPanel)
            {
                case ResultPanel.GameOver:
                    return gameOverRoot;
                case ResultPanel.VictoryWithNext:
                    return victoryWithNextRoot;
                case ResultPanel.VictoryWithoutNext:
                    return victoryWithoutNextRoot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resultPanel), resultPanel, null);
            }
        }

        private void HideResultRoots()
        {
            SetRootActive(gameOverRoot, false);
            SetRootActive(victoryWithNextRoot, false);
            SetRootActive(victoryWithoutNextRoot, false);
        }

        private void EnableActivePanelButtons()
        {
            switch (activePanel)
            {
                case ResultPanel.GameOver:
                    retryButton.interactable = true;
                    gameOverReturnButton.interactable = true;
                    break;
                case ResultPanel.VictoryWithNext:
                    nextLevelButton.interactable = true;
                    victoryWithNextReturnButton.interactable = true;
                    break;
                case ResultPanel.VictoryWithoutNext:
                    victoryWithoutNextReturnButton.interactable = true;
                    break;
            }
        }

        private void SetAllButtonsInteractable(bool interactable)
        {
            SetButtonInteractable(retryButton, interactable);
            SetButtonInteractable(gameOverReturnButton, interactable);
            SetButtonInteractable(nextLevelButton, interactable);
            SetButtonInteractable(victoryWithNextReturnButton, interactable);
            SetButtonInteractable(victoryWithoutNextReturnButton, interactable);
        }

        private bool RequireImageNumber(
            ImageNumberText imageNumber,
            string fieldName,
            out string error)
        {
            if (imageNumber == null || !imageNumber.transform.IsChildOf(transform))
            {
                error = $"BattleResultView.{fieldName} must belong to the BattleResult hierarchy.";
                return false;
            }

            if (!imageNumber.TryValidate(out var imageNumberError))
            {
                error = $"BattleResultView.{fieldName} is invalid. {imageNumberError}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool RequireRoot(GameObject root, string fieldName, out string error)
        {
            if (root == null || root.transform == transform || !root.transform.IsChildOf(transform))
            {
                error = $"BattleResultView.{fieldName} must be a child of the BattleResult hierarchy.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool RequireButton(
            Button button,
            GameObject root,
            string fieldName,
            out string error)
        {
            if (button == null || !button.transform.IsChildOf(root.transform))
            {
                error = $"BattleResultView.{fieldName} must belong to {root.name}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void Unsubscribe(SubscriptionToken token)
        {
            if (token.IsValid)
            {
                eventBus.Unsubscribe(token);
            }
        }

        private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(listener);
            }
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
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

        private enum ResultPanel
        {
            None = 0,
            GameOver = 1,
            VictoryWithNext = 2,
            VictoryWithoutNext = 3
        }
    }
}
