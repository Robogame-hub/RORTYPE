using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    public sealed class ShopItemCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Text priceLabel;
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Color goldColor = new Color(1f, 0.86f, 0.08f, 1f);

        private string hintText;
        private Func<bool> purchaseCallback;
        private Action<ShopItemCard, string> pointerEntered;
        private Action pointerExited;

        public Button Button => button;
        public bool HasHint => !string.IsNullOrWhiteSpace(hintText);

        private void Awake()
        {
            ResolveMissingReferences();
        }

        public void ConfigureRuntime(Button runtimeButton, Text runtimeSummaryLabel)
        {
            button = runtimeButton != null ? runtimeButton : button;
            summaryLabel = runtimeSummaryLabel != null ? runtimeSummaryLabel : summaryLabel;
            ResolveMissingReferences();
        }

        public void Bind(
            string icon,
            string itemName,
            string detail,
            string price,
            string hint,
            Func<bool> purchaseCallback,
            Action<ShopItemCard, string> onPointerEntered,
            Action onPointerExited)
        {
            ResolveMissingReferences();
            hintText = hint ?? string.Empty;
            this.purchaseCallback = purchaseCallback;
            pointerEntered = onPointerEntered;
            pointerExited = onPointerExited;

            if (iconLabel != null)
            {
                iconLabel.text = string.IsNullOrWhiteSpace(icon) ? "?" : icon;
            }

            if (nameLabel != null)
            {
                nameLabel.text = itemName ?? string.Empty;
            }

            if (detailLabel != null)
            {
                detailLabel.text = detail ?? string.Empty;
                detailLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(detail));
            }

            if (priceLabel != null)
            {
                priceLabel.text = price ?? string.Empty;
                priceLabel.color = goldColor;
            }

            if (summaryLabel != null)
            {
                summaryLabel.supportRichText = true;
                summaryLabel.text = FormatSummary(icon, itemName, detail, price);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { purchaseCallback?.Invoke(); });
            }

            gameObject.SetActive(true);
        }

        public void Clear()
        {
            hintText = string.Empty;
            purchaseCallback = null;
            pointerEntered = null;
            pointerExited = null;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }

            gameObject.SetActive(false);
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            var rectTransform = transform as RectTransform;
            return rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint);
        }

        public void ShowHint()
        {
            if (!string.IsNullOrWhiteSpace(hintText))
            {
                pointerEntered?.Invoke(this, hintText);
            }
        }

        public void HideHint()
        {
            pointerExited?.Invoke();
        }

        public void TryPurchase()
        {
            purchaseCallback?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrWhiteSpace(hintText))
            {
                pointerEntered?.Invoke(this, hintText);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerExited?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button == null)
            {
                purchaseCallback?.Invoke();
            }
        }

        private void OnValidate()
        {
            ResolveMissingReferences();
        }

        private void ResolveMissingReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureFont(iconLabel, uiFont);
            EnsureFont(nameLabel, uiFont);
            EnsureFont(detailLabel, uiFont);
            EnsureFont(priceLabel, uiFont);
            EnsureFont(summaryLabel, uiFont);
        }

        private static void EnsureFont(Text label, Font font)
        {
            if (label != null && label.font == null)
            {
                label.font = font;
            }
        }

        private static string FormatSummary(string icon, string itemName, string detail, string price)
        {
            var resolvedIcon = string.IsNullOrWhiteSpace(icon) ? "?" : icon;
            var resolvedName = itemName ?? string.Empty;
            var resolvedPrice = price ?? string.Empty;
            var resolvedDetail = detail ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedDetail))
            {
                return $"{resolvedIcon}\n<color=#FFD60A>{resolvedPrice}</color>\n{resolvedName}";
            }

            return $"{resolvedIcon}\n<color=#FFD60A>{resolvedPrice}</color>\n{resolvedName}\n{resolvedDetail}";
        }
    }
}
