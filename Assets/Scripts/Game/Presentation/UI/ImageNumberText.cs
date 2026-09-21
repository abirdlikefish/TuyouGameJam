using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ImageNumberText : MonoBehaviour
    {
        private enum HorizontalAlignment
        {
            Left,
            Center,
            Right
        }

        [Header("数字图片")]
        [SerializeField] private Sprite digit0;
        [SerializeField] private Sprite digit1;
        [SerializeField] private Sprite digit2;
        [SerializeField] private Sprite digit3;
        [SerializeField] private Sprite digit4;
        [SerializeField] private Sprite digit5;
        [SerializeField] private Sprite digit6;
        [SerializeField] private Sprite digit7;
        [SerializeField] private Sprite digit8;
        [SerializeField] private Sprite digit9;
        [SerializeField] private Sprite colon;

        [Header("排版")]
        [SerializeField, Min(0f)] private float characterSpacing = 2f;
        [SerializeField] private HorizontalAlignment alignment = HorizontalAlignment.Center;
        [SerializeField] private bool useSpriteAspectRatio = true;

        private readonly List<Image> characterImages = new List<Image>();
        private RectTransform cachedRectTransform;
        private string currentText;
        private bool isLayingOut;

        public void SetNumber(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ImageNumberText only supports non-negative numbers.");
            }

            SetText(value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetDigits(string value)
        {
            ValidateText(value, false);
            SetTextInternal(value);
        }

        public void SetText(string value)
        {
            ValidateText(value, true);
            SetTextInternal(value);
        }

        public bool TryValidate(out string error)
        {
            if (digit0 == null || digit1 == null || digit2 == null || digit3 == null ||
                digit4 == null || digit5 == null || digit6 == null || digit7 == null ||
                digit8 == null || digit9 == null || colon == null)
            {
                error = $"{nameof(ImageNumberText)} on '{name}' requires sprites for digits 0-9 and colon.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void SetTextInternal(string value)
        {
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (string.Equals(currentText, value, StringComparison.Ordinal))
            {
                return;
            }

            currentText = value;
            EnsureCharacterImages(value.Length);
            for (var index = 0; index < characterImages.Count; index++)
            {
                var image = characterImages[index];
                var active = index < value.Length;
                if (image.gameObject.activeSelf != active)
                {
                    image.gameObject.SetActive(active);
                }

                if (active)
                {
                    image.sprite = GetSprite(value[index]);
                }
            }

            RebuildLayout();
        }

        private void EnsureCharacterImages(int requiredCount)
        {
            while (characterImages.Count < requiredCount)
            {
                var index = characterImages.Count;
                var characterObject = new GameObject(
                    $"Character_{index}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                characterObject.layer = gameObject.layer;
                characterObject.transform.SetParent(transform, false);

                var image = characterObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = false;
                characterImages.Add(image);
            }
        }

        private void RebuildLayout()
        {
            if (isLayingOut || string.IsNullOrEmpty(currentText))
            {
                return;
            }

            isLayingOut = true;
            try
            {
                var container = GetRectTransform();
                var height = container.rect.height;
                if (height <= 0f)
                {
                    return;
                }

                var totalWidth = characterSpacing * Mathf.Max(0, currentText.Length - 1);
                for (var index = 0; index < currentText.Length; index++)
                {
                    totalWidth += GetCharacterWidth(characterImages[index].sprite, height);
                }

                var cursor = GetStartPosition(container.rect.width, totalWidth);
                for (var index = 0; index < currentText.Length; index++)
                {
                    var image = characterImages[index];
                    var width = GetCharacterWidth(image.sprite, height);
                    var characterRect = (RectTransform)image.transform;
                    characterRect.anchorMin = new Vector2(0f, 0.5f);
                    characterRect.anchorMax = new Vector2(0f, 0.5f);
                    characterRect.pivot = new Vector2(0f, 0.5f);
                    characterRect.anchoredPosition = new Vector2(cursor, 0f);
                    characterRect.sizeDelta = new Vector2(width, height);
                    cursor += width + characterSpacing;
                }
            }
            finally
            {
                isLayingOut = false;
            }
        }

        private float GetStartPosition(float containerWidth, float contentWidth)
        {
            switch (alignment)
            {
                case HorizontalAlignment.Left:
                    return 0f;
                case HorizontalAlignment.Right:
                    return containerWidth - contentWidth;
                default:
                    return (containerWidth - contentWidth) * 0.5f;
            }
        }

        private float GetCharacterWidth(Sprite sprite, float height)
        {
            if (!useSpriteAspectRatio || sprite == null || sprite.rect.height <= 0f)
            {
                return height;
            }

            return height * sprite.rect.width / sprite.rect.height;
        }

        private Sprite GetSprite(char character)
        {
            switch (character)
            {
                case '0': return digit0;
                case '1': return digit1;
                case '2': return digit2;
                case '3': return digit3;
                case '4': return digit4;
                case '5': return digit5;
                case '6': return digit6;
                case '7': return digit7;
                case '8': return digit8;
                case '9': return digit9;
                case ':': return colon;
                default:
                    throw new ArgumentOutOfRangeException(nameof(character));
            }
        }

        private RectTransform GetRectTransform()
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = (RectTransform)transform;
            }

            return cachedRectTransform;
        }

        private static void ValidateText(string value, bool allowColon)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("ImageNumberText requires at least one character.", nameof(value));
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character >= '0' && character <= '9')
                {
                    continue;
                }

                if (allowColon && character == ':')
                {
                    continue;
                }

                throw new FormatException(
                    $"ImageNumberText only supports digits{(allowColon ? " and colon" : string.Empty)}. " +
                    $"Invalid character '{character}' at index {index}.");
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && currentText != null)
            {
                RebuildLayout();
            }
        }

        private void OnValidate()
        {
            characterSpacing = Mathf.Max(0f, characterSpacing);
            if (Application.isPlaying && currentText != null)
            {
                RebuildLayout();
            }
        }
    }
}
