using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

        public static ShopUiPanel GetOrCreateDefault()
        {
            if (defaultPanel != null)
            {
                return defaultPanel;
            }

            var panelObject = new GameObject("ShopUiRuntime");
            DontDestroyOnLoad(panelObject);
            return panelObject.AddComponent<ShopUiPanel>();
        }

        private void Awake()
        {
            if (root == null)
            {
                BuildRuntimeUi();
            }

            RegisterDefaultPanel();
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

        private void BuildRuntimeUi()
        {
            EnsureEventSystem();
            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("Shop Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            root = CreatePanel("Shop Panel", canvasObject.transform, new Vector2(0.5f, 0.5f), new Vector2(620f, 440f), new Color(0.025f, 0.03f, 0.035f, 0.94f));

            titleLabel = CreateLabel("Title", root.transform, uiFont, 28, TextAnchor.MiddleCenter, Color.white);
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleLabel.rectTransform.sizeDelta = new Vector2(0f, 44f);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, -18f);

            feedbackLabel = CreateLabel("Feedback", root.transform, uiFont, 18, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.08f, 1f));
            feedbackLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            feedbackLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            feedbackLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            feedbackLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
            feedbackLabel.rectTransform.anchoredPosition = new Vector2(0f, 16f);

            var gridObject = new GameObject("Cards", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(root.transform, false);
            var gridTransform = gridObject.GetComponent<RectTransform>();
            gridTransform.anchorMin = Vector2.zero;
            gridTransform.anchorMax = Vector2.one;
            gridTransform.offsetMin = new Vector2(28f, 58f);
            gridTransform.offsetMax = new Vector2(-28f, -78f);

            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(174f, 104f);
            grid.spacing = new Vector2(18f, 18f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;

            cards = new ShopItemCard[9];
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i] = CreateRuntimeCard($"Shop Card {i + 1}", gridObject.transform, uiFont);
            }

            tooltipRoot = CreatePanel("Tooltip", root.transform, new Vector2(0.5f, 0f), new Vector2(560f, 42f), new Color(0.08f, 0.1f, 0.12f, 0.96f));
            var tooltipTransform = tooltipRoot.GetComponent<RectTransform>();
            tooltipTransform.anchoredPosition = new Vector2(0f, -52f);
            tooltipLabel = CreateLabel("Tooltip Label", tooltipRoot.transform, uiFont, 16, TextAnchor.MiddleCenter, Color.white);
            tooltipLabel.rectTransform.anchorMin = Vector2.zero;
            tooltipLabel.rectTransform.anchorMax = Vector2.one;
            tooltipLabel.rectTransform.offsetMin = new Vector2(12f, 4f);
            tooltipLabel.rectTransform.offsetMax = new Vector2(-12f, -4f);
            tooltipRoot.SetActive(false);
        }

        private static ShopItemCard CreateRuntimeCard(string objectName, Transform parent, Font font)
        {
            var cardObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ShopItemCard));
            cardObject.transform.SetParent(parent, false);

            var image = cardObject.GetComponent<Image>();
            image.color = new Color(0.11f, 0.14f, 0.16f, 1f);

            var button = cardObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.18f, 0.23f, 0.26f, 1f);
            colors.pressedColor = new Color(0.06f, 0.08f, 0.09f, 1f);
            button.colors = colors;

            var label = CreateLabel("Summary", cardObject.transform, font, 17, TextAnchor.MiddleCenter, Color.white);
            label.supportRichText = true;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 8f);
            label.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var card = cardObject.GetComponent<ShopItemCard>();
            card.ConfigureRuntime(button, label);
            cardObject.SetActive(false);
            return card;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static GameObject CreatePanel(string objectName, Transform parent, Vector2 anchor, Vector2 size, Color color)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = anchor;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = Vector2.zero;

            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateLabel(string objectName, Transform parent, Font font, int fontSize, TextAnchor alignment, Color color)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
    }
}
