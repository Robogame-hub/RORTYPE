using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    public sealed class ShopUiPanel : MonoBehaviour
    {
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly string Icon;
            public readonly string Detail;
            public readonly string Price;
            public readonly string Hint;
            public readonly Func<bool> Purchase;

            public Entry(string name, string icon, string detail, string price, string hint, Func<bool> purchase)
            {
                Name = name;
                Icon = icon;
                Detail = detail;
                Price = price;
                Hint = hint;
                Purchase = purchase;
            }
        }

        private static ShopUiPanel activePanel;
        private static ShopUiPanel defaultPanel;
        private static bool missingUiWarned;

        [SerializeField] private GameObject root;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private GameObject tooltipRoot;
        [SerializeField] private Text tooltipLabel;
        [SerializeField] private ShopItemCard[] cards = Array.Empty<ShopItemCard>();

        private ShopItemCard hoveredCard;

        public static bool IsAnyOpen => activePanel != null && activePanel.IsOpen;
        public static ShopUiPanel DefaultPanel => defaultPanel;
        public bool IsOpen => root != null && root.activeSelf;

        public static ShopUiPanel ResolveDefault()
        {
            if (defaultPanel != null)
            {
                return defaultPanel;
            }

            defaultPanel = FindFirstObjectByType<ShopUiPanel>(FindObjectsInactive.Include);
            if (defaultPanel == null && !missingUiWarned)
            {
                missingUiWarned = true;
                Debug.LogWarning("No scene-authored ShopUiPanel found. Add Assets/Game/Prefabs/UI/InteractionUi.prefab to the active scene.");
            }

            return defaultPanel;
        }

        private void Awake()
        {
            RegisterDefaultPanel();
            EnsureAuthoredFonts();
            Hide();
        }

        private void OnEnable()
        {
            RegisterDefaultPanel();
        }

        private void OnDisable()
        {
            if (activePanel == this)
            {
                activePanel = null;
            }

            if (defaultPanel == this)
            {
                defaultPanel = null;
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            UpdateMouseCards();
        }

        public void Show(string title, IReadOnlyList<Entry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            if (root == null || cards == null || cards.Length == 0)
            {
                Debug.LogWarning($"Shop UI in scene '{gameObject.scene.name}' is missing authored root or card references.", this);
                return;
            }

            if (activePanel != null && activePanel != this)
            {
                activePanel.Hide();
            }

            activePanel = this;

            if (root != null)
            {
                root.SetActive(true);
            }

            if (titleLabel != null)
            {
                titleLabel.text = title ?? string.Empty;
            }

            SetFeedback(string.Empty);
            HideTooltip();

            var cardCount = cards != null ? cards.Length : 0;
            for (var i = 0; i < cardCount; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                if (i >= entries.Count)
                {
                    card.Clear();
                    continue;
                }

                var entry = entries[i];
                card.Bind(
                    entry.Icon,
                    entry.Name,
                    entry.Detail,
                    entry.Price,
                    entry.Hint,
                    entry.Purchase,
                    ShowTooltip,
                    HideTooltip);
            }
        }

        public void Hide()
        {
            hoveredCard?.HideHint();
            hoveredCard = null;
            HideTooltip();
            if (root != null)
            {
                root.SetActive(false);
            }

            if (activePanel == this)
            {
                activePanel = null;
            }
        }

        public void SetFeedback(string message)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = message ?? string.Empty;
            feedbackLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void ShowTooltip(ShopItemCard card, string text)
        {
            if (tooltipRoot == null || tooltipLabel == null)
            {
                return;
            }

            tooltipLabel.text = text ?? string.Empty;
            tooltipRoot.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        private void HideTooltip()
        {
            if (tooltipRoot != null)
            {
                tooltipRoot.SetActive(false);
            }
        }

        private void UpdateMouseCards()
        {
            var screenPoint = (Vector2)Input.mousePosition;
            var card = ResolveCardAt(screenPoint);
            if (card != hoveredCard)
            {
                hoveredCard?.HideHint();
                hoveredCard = card;
                hoveredCard?.ShowHint();
            }
        }

        private ShopItemCard ResolveCardAt(Vector2 screenPoint)
        {
            var cardCount = cards != null ? cards.Length : 0;
            for (var i = 0; i < cardCount; i++)
            {
                var card = cards[i];
                if (card != null && card.gameObject.activeInHierarchy && card.ContainsScreenPoint(screenPoint))
                {
                    return card;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            if (root == null && transform.childCount > 0)
            {
                root = transform.GetChild(0).gameObject;
            }
        }

        private void RegisterDefaultPanel()
        {
            if (defaultPanel == null)
            {
                defaultPanel = this;
            }
        }

        private void EnsureAuthoredFonts()
        {
            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureFont(titleLabel, uiFont);
            EnsureFont(feedbackLabel, uiFont);
            EnsureFont(tooltipLabel, uiFont);
        }

        private static void EnsureFont(Text label, Font font)
        {
            if (label != null && label.font == null)
            {
                label.font = font;
            }
        }
    }
}
