using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    public sealed class PortalUiRuntime : MonoBehaviour
    {
        public readonly struct ChoiceOption
        {
            public readonly string Label;
            public readonly Func<bool> Callback;

            public ChoiceOption(string label, Action callback)
            {
                Label = label;
                Callback = () =>
                {
                    callback?.Invoke();
                    return true;
                };
            }

            public ChoiceOption(string label, Func<bool> callback)
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
            public Func<bool> Callback;
        }

        private static PortalUiRuntime instance;
        private static bool missingUiWarned;

        private readonly List<ChoiceButton> choiceButtons = new();
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptLabel;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private Text choiceTitle;
        [SerializeField] private Button[] choiceButtonsAuthored = Array.Empty<Button>();
        [SerializeField] private Text[] choiceLabelsAuthored = Array.Empty<Text>();

        public static bool IsChoiceOpen => instance != null && instance.choiceRoot != null && instance.choiceRoot.activeSelf;

        public static void ShowPrompt(string promptText)
        {
            var runtime = ResolveInstance();
            if (runtime == null)
            {
                WarnMissingUi();
                return;
            }

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

            var runtime = ResolveInstance();
            if (runtime == null)
            {
                WarnMissingUi();
                return;
            }

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

        private static PortalUiRuntime ResolveInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<PortalUiRuntime>(FindObjectsInactive.Include);
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
            CacheAuthoredChoiceButtons();
            EnsureAuthoredFonts();
            HidePrompt();
            HideChoice();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
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

                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index)))
                {
                    InvokeChoice(choiceButtons[index]);
                }
            }
        }

        private static bool InvokeChoice(ChoiceButton choiceButton)
        {
            return choiceButton?.Callback?.Invoke() ?? false;
        }

        private void CacheAuthoredChoiceButtons()
        {
            choiceButtons.Clear();
            var count = Mathf.Min(
                choiceButtonsAuthored != null ? choiceButtonsAuthored.Length : 0,
                choiceLabelsAuthored != null ? choiceLabelsAuthored.Length : 0);
            for (var index = 0; index < count; index++)
            {
                var button = choiceButtonsAuthored[index];
                var label = choiceLabelsAuthored[index];
                if (button == null || label == null)
                {
                    continue;
                }

                choiceButtons.Add(new ChoiceButton
                {
                    Root = button.gameObject,
                    Button = button,
                    Label = label
                });
                button.gameObject.SetActive(false);
            }
        }

        private void EnsureAuthoredFonts()
        {
            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureFont(promptLabel, uiFont);
            EnsureFont(choiceTitle, uiFont);
            for (var index = 0; index < choiceButtons.Count; index++)
            {
                EnsureFont(choiceButtons[index].Label, uiFont);
            }
        }

        private void BuildChoiceUi(string title, IReadOnlyList<ChoiceOption> options)
        {
            if (choiceRoot == null || choiceTitle == null || choiceButtons.Count == 0)
            {
                Debug.LogWarning($"Portal UI in scene '{gameObject.scene.name}' is missing authored choice references.", this);
                return;
            }

            HidePrompt();
            choiceRoot.SetActive(true);
            choiceTitle.text = title ?? "Portal";

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
                button.Button.onClick.AddListener(() => { InvokeChoice(button); });
            }

            if (options.Count > choiceButtons.Count)
            {
                Debug.LogWarning($"Portal UI has {choiceButtons.Count} authored buttons but needs {options.Count}. Add more button slots to the scene UI.", this);
            }
        }

        private void SetPromptVisible(bool isVisible)
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(isVisible && !IsChoiceOpen && !ShopUiPanel.IsAnyOpen);
            }
        }

        private static void WarnMissingUi()
        {
            if (missingUiWarned)
            {
                return;
            }

            missingUiWarned = true;
            Debug.LogWarning("No scene-authored PortalUiRuntime found. Add Assets/Game/Prefabs/UI/InteractionUi.prefab to the active scene.");
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
