using System;
using System.Globalization;
using Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BattleHudView : MonoBehaviour
    {
        [Header("战斗信息")]
        [SerializeField] private TMP_Text levelNameText;
        [SerializeField] private TMP_Text elapsedTimeText;
        [SerializeField] private TMP_Text fireDurationText;
        [SerializeField] private TMP_Text iceDurationText;
        [SerializeField] private TMP_Text lightningDurationText;
        [SerializeField] private TMP_Text killProgressText;

        [Header("退出")]
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject exitConfirmationRoot;
        [SerializeField] private Button confirmExitButton;
        [SerializeField] private Button cancelExitButton;

        private IGameplayHudSource hudSource;
        private IGameStateService gameStateService;
        private string cachedLevelName;
        private string cachedElapsedTime;
        private string cachedFireDuration;
        private string cachedIceDuration;
        private string cachedLightningDuration;
        private string cachedKillProgress;
        private bool actionRequested;
        private bool initialized;

        public void Initialize(
            IGameplayHudSource initializedHudSource,
            IGameStateService initializedGameStateService)
        {
            if (initialized)
            {
                throw new InvalidOperationException("BattleHudView is already initialized.");
            }

            hudSource = initializedHudSource ?? throw new ArgumentNullException(nameof(initializedHudSource));
            gameStateService = initializedGameStateService ??
                throw new ArgumentNullException(nameof(initializedGameStateService));
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            exitButton.onClick.AddListener(HandleExitClicked);
            confirmExitButton.onClick.AddListener(HandleConfirmExitClicked);
            cancelExitButton.onClick.AddListener(HandleCancelExitClicked);
            exitConfirmationRoot.SetActive(false);
            actionRequested = false;
            initialized = true;
            Refresh(hudSource.GetHudSnapshot(), true);
        }

        public bool TryValidate(out string error)
        {
            if (!RequireText(levelNameText, nameof(levelNameText), out error) ||
                !RequireText(elapsedTimeText, nameof(elapsedTimeText), out error) ||
                !RequireText(fireDurationText, nameof(fireDurationText), out error) ||
                !RequireText(iceDurationText, nameof(iceDurationText), out error) ||
                !RequireText(lightningDurationText, nameof(lightningDurationText), out error) ||
                !RequireText(killProgressText, nameof(killProgressText), out error) ||
                !RequireButton(exitButton, nameof(exitButton), out error) ||
                !RequireButton(confirmExitButton, nameof(confirmExitButton), out error) ||
                !RequireButton(cancelExitButton, nameof(cancelExitButton), out error))
            {
                return false;
            }

            if (exitConfirmationRoot == null ||
                !exitConfirmationRoot.transform.IsChildOf(transform))
            {
                error = "BattleHudView.exitConfirmationRoot must belong to the BattleHud hierarchy.";
                return false;
            }

            if (!confirmExitButton.transform.IsChildOf(exitConfirmationRoot.transform) ||
                !cancelExitButton.transform.IsChildOf(exitConfirmationRoot.transform))
            {
                error = "BattleHudView confirmation buttons must belong to exitConfirmationRoot.";
                return false;
            }

            if (exitButton == confirmExitButton || exitButton == cancelExitButton ||
                confirmExitButton == cancelExitButton)
            {
                error = "BattleHudView buttons must reference different objects.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Cleanup()
        {
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(HandleExitClicked);
                exitButton.interactable = false;
            }

            if (confirmExitButton != null)
            {
                confirmExitButton.onClick.RemoveListener(HandleConfirmExitClicked);
                confirmExitButton.interactable = false;
            }

            if (cancelExitButton != null)
            {
                cancelExitButton.onClick.RemoveListener(HandleCancelExitClicked);
                cancelExitButton.interactable = false;
            }

            if (exitConfirmationRoot != null)
            {
                exitConfirmationRoot.SetActive(false);
            }

            hudSource = null;
            gameStateService = null;
            actionRequested = false;
            initialized = false;
            ClearTextCache();
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            var snapshot = hudSource.GetHudSnapshot();
            if (snapshot.IsCompleted && exitConfirmationRoot.activeSelf)
            {
                exitConfirmationRoot.SetActive(false);
                exitButton.interactable = false;
            }

            Refresh(snapshot, false);
        }

        private void Refresh(GameplayHudSnapshot snapshot, bool force)
        {
            SetText(levelNameText, snapshot.DisplayName, ref cachedLevelName, force);
            SetText(elapsedTimeText, FormatElapsedTime(snapshot.ElapsedTime), ref cachedElapsedTime, force);
            SetText(fireDurationText, FormatDuration(snapshot.FireRemainingDuration), ref cachedFireDuration, force);
            SetText(iceDurationText, FormatDuration(snapshot.IceRemainingDuration), ref cachedIceDuration, force);
            SetText(
                lightningDurationText,
                FormatDuration(snapshot.LightningRemainingDuration),
                ref cachedLightningDuration,
                force);
            SetText(
                killProgressText,
                $"KILLS: {snapshot.KilledEnemyCount} / {snapshot.TotalEnemyCount}",
                ref cachedKillProgress,
                force);
        }

        private void HandleExitClicked()
        {
            if (!initialized || actionRequested || hudSource.GetHudSnapshot().IsCompleted)
            {
                return;
            }

            exitButton.interactable = false;
            confirmExitButton.interactable = true;
            cancelExitButton.interactable = true;
            exitConfirmationRoot.SetActive(true);
        }

        private void HandleCancelExitClicked()
        {
            if (!initialized || actionRequested)
            {
                return;
            }

            exitConfirmationRoot.SetActive(false);
            exitButton.interactable = true;
        }

        private void HandleConfirmExitClicked()
        {
            if (!initialized || actionRequested)
            {
                return;
            }

            actionRequested = true;
            confirmExitButton.interactable = false;
            cancelExitButton.interactable = false;
            if (gameStateService.TryReturnToLevelSelect())
            {
                return;
            }

            if (initialized)
            {
                actionRequested = false;
                confirmExitButton.interactable = true;
                cancelExitButton.interactable = true;
            }
        }

        private bool RequireText(TMP_Text text, string fieldName, out string error)
        {
            if (text == null || !text.transform.IsChildOf(transform))
            {
                error = $"BattleHudView.{fieldName} must belong to the BattleHud hierarchy.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool RequireButton(Button button, string fieldName, out string error)
        {
            if (button == null || !button.transform.IsChildOf(transform))
            {
                error = $"BattleHudView.{fieldName} must belong to the BattleHud hierarchy.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void SetText(
            TMP_Text target,
            string value,
            ref string cachedValue,
            bool force)
        {
            if (!force && string.Equals(cachedValue, value, StringComparison.Ordinal))
            {
                return;
            }

            cachedValue = value;
            target.text = value;
        }

        private static string FormatElapsedTime(float elapsedTime)
        {
            var totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, elapsedTime));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string FormatDuration(float duration)
        {
            return Mathf.Max(0f, duration).ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        private void ClearTextCache()
        {
            cachedLevelName = null;
            cachedElapsedTime = null;
            cachedFireDuration = null;
            cachedIceDuration = null;
            cachedLightningDuration = null;
            cachedKillProgress = null;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
