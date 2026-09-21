using System;
using System.Globalization;
using Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BattleHudView : MonoBehaviour
    {
        [Header("战斗信息")]
        [SerializeField] private ImageNumberText levelNameNumber;
        [SerializeField] private ImageNumberText elapsedTimeNumber;
        [SerializeField] private TMP_Text fireDurationText;
        [SerializeField] private TMP_Text iceDurationText;
        [SerializeField] private TMP_Text lightningDurationText;
        [FormerlySerializedAs("killProgressImage")]
        [SerializeField] private Image remainingEnemyProgressImage;
        [FormerlySerializedAs("killedEnemyCountNumber")]
        [SerializeField] private ImageNumberText remainingEnemyCountNumber;

        [Header("退出")]
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject exitConfirmationRoot;
        [SerializeField] private Button confirmExitButton;
        [SerializeField] private Button cancelExitButton;

        private IGameplayHudSource hudSource;
        private IGameStateService gameStateService;
        private string cachedFireDuration;
        private string cachedIceDuration;
        private string cachedLightningDuration;
        private float cachedRemainingEnemyProgress = -1f;
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
            if (!RequireImageNumber(levelNameNumber, nameof(levelNameNumber), out error) ||
                !RequireImageNumber(elapsedTimeNumber, nameof(elapsedTimeNumber), out error) ||
                !RequireText(fireDurationText, nameof(fireDurationText), out error) ||
                !RequireText(iceDurationText, nameof(iceDurationText), out error) ||
                !RequireText(lightningDurationText, nameof(lightningDurationText), out error) ||
                !RequireFilledImage(
                    remainingEnemyProgressImage,
                    nameof(remainingEnemyProgressImage),
                    out error) ||
                !RequireImageNumber(
                    remainingEnemyCountNumber,
                    nameof(remainingEnemyCountNumber),
                    out error) ||
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
            var remainingEnemyCount = CalculateRemainingEnemyCount(
                snapshot.KilledEnemyCount,
                snapshot.TotalEnemyCount);
            levelNameNumber.SetNumber(snapshot.LevelId);
            elapsedTimeNumber.SetText(FormatElapsedTime(snapshot.ElapsedTime));
            SetText(fireDurationText, FormatDuration(snapshot.FireRemainingDuration), ref cachedFireDuration, force);
            SetText(iceDurationText, FormatDuration(snapshot.IceRemainingDuration), ref cachedIceDuration, force);
            SetText(
                lightningDurationText,
                FormatDuration(snapshot.LightningRemainingDuration),
                ref cachedLightningDuration,
                force);
            SetProgress(
                remainingEnemyProgressImage,
                CalculateRemainingEnemyProgress(remainingEnemyCount, snapshot.TotalEnemyCount),
                ref cachedRemainingEnemyProgress,
                force);
            remainingEnemyCountNumber.SetNumber(remainingEnemyCount);
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

        private bool RequireImageNumber(
            ImageNumberText imageNumber,
            string fieldName,
            out string error)
        {
            if (imageNumber == null || !imageNumber.transform.IsChildOf(transform))
            {
                error = $"BattleHudView.{fieldName} must belong to the BattleHud hierarchy.";
                return false;
            }

            if (!imageNumber.TryValidate(out var imageNumberError))
            {
                error = $"BattleHudView.{fieldName} is invalid. {imageNumberError}";
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

        private bool RequireFilledImage(Image image, string fieldName, out string error)
        {
            if (image == null || !image.transform.IsChildOf(transform))
            {
                error = $"BattleHudView.{fieldName} must belong to the BattleHud hierarchy.";
                return false;
            }

            if (image.type != Image.Type.Filled)
            {
                error = $"BattleHudView.{fieldName} must use Image.Type.Filled.";
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

        private static int CalculateRemainingEnemyCount(int killedEnemyCount, int totalEnemyCount)
        {
            if (totalEnemyCount <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(totalEnemyCount - killedEnemyCount, 0, totalEnemyCount);
        }

        private static float CalculateRemainingEnemyProgress(
            int remainingEnemyCount,
            int totalEnemyCount)
        {
            if (totalEnemyCount <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)remainingEnemyCount / totalEnemyCount);
        }

        private static void SetProgress(
            Image target,
            float value,
            ref float cachedValue,
            bool force)
        {
            if (!force && Mathf.Approximately(cachedValue, value))
            {
                return;
            }

            cachedValue = value;
            target.fillAmount = value;
        }

        private void ClearTextCache()
        {
            cachedFireDuration = null;
            cachedIceDuration = null;
            cachedLightningDuration = null;
            cachedRemainingEnemyProgress = -1f;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
