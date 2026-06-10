using RorType.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    public sealed class PlayerStatusUiRuntime : MonoBehaviour
    {
        private static PlayerStatusUiRuntime instance;

        private PlayerResourceController resources;
        private TopDownPlayerMotor motor;
        private Canvas canvas;
        private Text ammoLabel;
        private Image[] dashCharges;
        private Image staminaFill;

        public static void Bind(PlayerResourceController playerResources)
        {
            var runtime = EnsureInstance();
            runtime.resources = playerResources;
            runtime.motor = playerResources != null
                ? playerResources.GetComponent<TopDownPlayerMotor>()
                : null;
        }

        private static PlayerStatusUiRuntime EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            var runtimeObject = new GameObject("PlayerStatusUiRuntime");
            DontDestroyOnLoad(runtimeObject);
            instance = runtimeObject.AddComponent<PlayerStatusUiRuntime>();
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

        private void LateUpdate()
        {
            if (resources == null || !resources.isActiveAndEnabled)
            {
                resources = FindFirstObjectByType<PlayerResourceController>();
                motor = resources != null ? resources.GetComponent<TopDownPlayerMotor>() : null;
            }

            if (canvas != null)
            {
                canvas.enabled = resources != null;
            }

            if (resources == null)
            {
                return;
            }

            ammoLabel.text = $"\u041f\u0430\u0442\u0440\u043e\u043d\u044b {resources.Ammo}";
            UpdateDashUi();
            UpdateStaminaUi();
        }

        private void BuildUi()
        {
            if (canvas != null)
            {
                return;
            }

            var uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("Player Status Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var root = new GameObject("Player Status", typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.sizeDelta = new Vector2(220f, 96f);
            rootRect.anchoredPosition = new Vector2(-28f, 28f);

            var rootLayout = root.GetComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8f;
            rootLayout.childAlignment = TextAnchor.LowerRight;
            rootLayout.childControlHeight = false;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            ammoLabel = CreateLabel("Ammo Label", root.transform, uiFont, 24, new Vector2(0f, 28f));

            var dashRow = new GameObject("Dash Charges", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            dashRow.transform.SetParent(root.transform, false);
            dashRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            var dashLayout = dashRow.GetComponent<HorizontalLayoutGroup>();
            dashLayout.spacing = 8f;
            dashLayout.childAlignment = TextAnchor.MiddleRight;
            dashLayout.childControlHeight = false;
            dashLayout.childControlWidth = false;
            dashLayout.childForceExpandHeight = false;
            dashLayout.childForceExpandWidth = false;

            dashCharges = new Image[2];
            for (var i = 0; i < dashCharges.Length; i++)
            {
                dashCharges[i] = CreateDashCharge($"Dash {i + 1}", dashRow.transform);
            }

            var staminaRoot = new GameObject("Stamina", typeof(RectTransform), typeof(Image));
            staminaRoot.transform.SetParent(root.transform, false);
            staminaRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);
            staminaRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(staminaRoot.transform, false);
            staminaFill = fillObject.GetComponent<Image>();
            staminaFill.color = new Color(0.25f, 0.9f, 0.45f, 0.95f);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Horizontal;
            staminaFill.fillOrigin = 0;

            var fillRect = staminaFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
        }

        private void UpdateDashUi()
        {
            if (dashCharges == null || motor == null)
            {
                return;
            }

            for (var i = 0; i < dashCharges.Length; i++)
            {
                var isReady = i < motor.DashCharges;
                dashCharges[i].color = isReady
                    ? new Color(0.18f, 0.75f, 1f, 0.95f)
                    : new Color(0.08f, 0.12f, 0.16f, 0.85f);
            }
        }

        private void UpdateStaminaUi()
        {
            if (staminaFill == null)
            {
                return;
            }

            staminaFill.fillAmount = resources.StaminaNormalized;
        }

        private static Text CreateLabel(string objectName, Transform parent, Font font, int fontSize, Vector2 size)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleRight;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private static Image CreateDashCharge(string objectName, Transform parent)
        {
            var chargeObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            chargeObject.transform.SetParent(parent, false);
            chargeObject.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 18f);
            var image = chargeObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.75f, 1f, 0.95f);
            return image;
        }
    }
}
