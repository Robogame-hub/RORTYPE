using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    public sealed class PortalUiRuntime : MonoBehaviour
    {
        public readonly struct ChoiceOption
        {
            public readonly string Label;
            public readonly Action Callback;

            public ChoiceOption(string label, Action callback)
            {
                Label = label;
                Callback = callback;
            }
        }

        private sealed class ChoiceButton
        {
            public GameObject Root;
            public Button Button;
            public Text Label;
            public Action Callback;
        }

        private static PortalUiRuntime instance;

        private readonly List<ChoiceButton> choiceButtons = new();
        private Canvas canvas;
        private GameObject promptRoot;
        private Text promptLabel;
        private GameObject choiceRoot;
        private Text choiceTitle;
        private RectTransform choiceButtonContainer;

        public static bool IsChoiceOpen => instance != null && instance.choiceRoot != null && instance.choiceRoot.activeSelf;

        public static void ShowPrompt(string promptText)
        {
            var runtime = EnsureInstance();
            runtime.SetPromptVisible(!string.IsNullOrWhiteSpace(promptText));
            if (runtime.promptLabel != null)
            {
                runtime.promptLabel.text = promptText ?? string.Empty;
            }
        }

        public static void HidePrompt()
        {
            if (instance == null)
            {
                return;
            }

            instance.SetPromptVisible(false);
        }

        public static void ShowChoice(string title, IReadOnlyList<ChoiceOption> options)
        {
            if (options == null || options.Count == 0)
            {
                return;
            }

            var runtime = EnsureInstance();
            runtime.BuildChoiceUi(title, options);
        }

        public static void HideChoice()
        {
            if (instance == null || instance.choiceRoot == null)
            {
                return;
            }

            instance.choiceRoot.SetActive(false);
        }

        private static PortalUiRuntime EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            var runtimeObject = new GameObject("PortalUiRuntime");
            DontDestroyOnLoad(runtimeObject);
            instance = runtimeObject.AddComponent<PortalUiRuntime>();
            instance.BuildUi();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUi();
        }

        private void Update()
        {
            if (!IsChoiceOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideChoice();
                return;
            }

            for (var index = 0; index < choiceButtons.Count && index < 9; index++)
            {
                if (!choiceButtons[index].Root.activeSelf)
                {
                    continue;
                }

                var keyCode = (KeyCode)((int)KeyCode.Alpha1 + index);
                if (!Input.GetKeyDown(keyCode))
                {
                    continue;
                }

                choiceButtons[index].Callback?.Invoke();
                return;
            }
        }

        private void BuildUi()
        {
            if (canvas != null)
            {
                return;
            }

            EnsureEventSystem();
            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("Portal Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            promptRoot = CreatePanel("Portal Prompt", canvas.transform, new Vector2(0.5f, 0.13f), new Vector2(300f, 56f), new Color(0f, 0f, 0f, 0.72f));
            promptLabel = CreateLabel("Prompt Label", promptRoot.transform, uiFont, 24, TextAnchor.MiddleCenter, Color.white);
            promptLabel.rectTransform.anchorMin = Vector2.zero;
            promptLabel.rectTransform.anchorMax = Vector2.one;
            promptLabel.rectTransform.offsetMin = new Vector2(12f, 8f);
            promptLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);
            promptRoot.SetActive(false);

            choiceRoot = CreatePanel("Portal Choice", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(420f, 280f), new Color(0.03f, 0.05f, 0.08f, 0.92f));
            choiceTitle = CreateLabel("Choice Title", choiceRoot.transform, uiFont, 26, TextAnchor.MiddleCenter, Color.white);
            choiceTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            choiceTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            choiceTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            choiceTitle.rectTransform.sizeDelta = new Vector2(0f, 40f);
            choiceTitle.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            var buttonContainer = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonContainer.transform.SetParent(choiceRoot.transform, false);
            choiceButtonContainer = buttonContainer.GetComponent<RectTransform>();
            choiceButtonContainer.anchorMin = new Vector2(0f, 0f);
            choiceButtonContainer.anchorMax = new Vector2(1f, 1f);
            choiceButtonContainer.offsetMin = new Vector2(28f, 28f);
            choiceButtonContainer.offsetMax = new Vector2(-28f, -76f);

            var layoutGroup = buttonContainer.GetComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 12f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;

            choiceRoot.SetActive(false);
        }

        private void BuildChoiceUi(string title, IReadOnlyList<ChoiceOption> options)
        {
            HidePrompt();
            choiceRoot.SetActive(true);
            choiceTitle.text = title ?? "Portal";
            EnsureChoiceButtonCount(options.Count);

            for (var index = 0; index < choiceButtons.Count; index++)
            {
                var button = choiceButtons[index];
                var isVisible = index < options.Count;
                button.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var option = options[index];
                button.Label.text = $"{index + 1}. {option.Label}";
                button.Callback = option.Callback;
                button.Button.onClick.RemoveAllListeners();
                button.Button.onClick.AddListener(() => button.Callback?.Invoke());
            }
        }

        private void EnsureChoiceButtonCount(int count)
        {
            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (choiceButtons.Count < count)
            {
                var buttonRoot = new GameObject(
                    $"Choice Button {choiceButtons.Count + 1}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                buttonRoot.transform.SetParent(choiceButtonContainer, false);

                var rectTransform = buttonRoot.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(0f, 48f);

                var image = buttonRoot.GetComponent<Image>();
                image.color = new Color(0.13f, 0.22f, 0.31f, 1f);

                var button = buttonRoot.GetComponent<Button>();
                var colors = button.colors;
                colors.normalColor = image.color;
                colors.highlightedColor = new Color(0.2f, 0.32f, 0.43f, 1f);
                colors.pressedColor = new Color(0.08f, 0.14f, 0.2f, 1f);
                button.colors = colors;

                var label = CreateLabel("Label", buttonRoot.transform, uiFont, 22, TextAnchor.MiddleCenter, Color.white);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(8f, 4f);
                label.rectTransform.offsetMax = new Vector2(-8f, -4f);

                choiceButtons.Add(new ChoiceButton
                {
                    Root = buttonRoot,
                    Button = button,
                    Label = label
                });
            }
        }

        private void SetPromptVisible(bool isVisible)
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(isVisible && !IsChoiceOpen);
            }
        }

        private void EnsureEventSystem()
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
