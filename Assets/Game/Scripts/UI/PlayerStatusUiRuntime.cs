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
        private Text moneyLabel;
        private Image[] dashCharges;
        private Transform dashRowTransform;
        private Image staminaFill;
        private Image healthFill;
        private Image shieldFill;
        private GameObject shieldRoot;
        private float ammoPulseTimer;
        private float moneyPulseTimer;
        private float healthPulseTimer;
        private float shieldPulseTimer;
        private int lastAmmo = -1;
        private int lastMoney = -1;
        private float lastHealth = -1f;
        private float lastShield = -1f;

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
                resources = PlayerResourceController.ActivePlayer;
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

            UpdateTextFeedback();
            ammoLabel.text = $"\u041f\u0430\u0442\u0440\u043e\u043d\u044b {resources.Ammo}";
            moneyLabel.text = $"\u0417\u043E\u043B\u043E\u0442\u043E {resources.Money}";
            UpdateDashUi();
            UpdateStaminaUi();
            UpdateHealthUi();
            UpdateShieldUi();
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
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

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
            dashRowTransform = dashRow.transform;
            dashRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            var dashLayout = dashRow.GetComponent<HorizontalLayoutGroup>();
            dashLayout.spacing = 8f;
            dashLayout.childAlignment = TextAnchor.MiddleRight;
            dashLayout.childControlHeight = false;
            dashLayout.childControlWidth = false;
            dashLayout.childForceExpandHeight = false;
            dashLayout.childForceExpandWidth = false;

            EnsureDashChargeCount(2);

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

            var healthShieldRow = new GameObject("Health Shield Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            healthShieldRow.transform.SetParent(root.transform, false);
            healthShieldRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);
            var healthShieldLayout = healthShieldRow.GetComponent<HorizontalLayoutGroup>();
            healthShieldLayout.spacing = 6f;
            healthShieldLayout.childAlignment = TextAnchor.MiddleRight;
            healthShieldLayout.childControlHeight = true;
            healthShieldLayout.childControlWidth = true;
            healthShieldLayout.childForceExpandHeight = true;
            healthShieldLayout.childForceExpandWidth = true;

            shieldRoot = CreateBarRoot("Shield", healthShieldRow.transform);
            shieldFill = CreateBarFill("Fill", shieldRoot.transform, new Color(0.2f, 0.62f, 1f, 0.96f));
            shieldRoot.SetActive(false);

            var healthRoot = CreateBarRoot("Health", healthShieldRow.transform);

            healthFill = CreateBarFill("Fill", healthRoot.transform, new Color(0.95f, 0.14f, 0.14f, 0.96f));

            var moneyRoot = new GameObject("Money Status", typeof(RectTransform));
            moneyRoot.transform.SetParent(canvas.transform, false);
            var moneyRootRect = moneyRoot.GetComponent<RectTransform>();
            moneyRootRect.anchorMin = new Vector2(1f, 1f);
            moneyRootRect.anchorMax = new Vector2(1f, 1f);
            moneyRootRect.pivot = new Vector2(1f, 1f);
            moneyRootRect.sizeDelta = new Vector2(320f, 44f);
            moneyRootRect.anchoredPosition = new Vector2(-28f, -548f);

            moneyLabel = CreateLabel("Money Label", moneyRoot.transform, uiFont, 32, new Vector2(320f, 44f));
            moneyLabel.color = new Color(1f, 0.86f, 0.08f, 1f);
            var outline = moneyLabel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void UpdateDashUi()
        {
            if (dashCharges == null || motor == null)
            {
                return;
            }

            EnsureDashChargeCount(motor.MaxDashCharges);
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

            SetBarFill(staminaFill, resources.StaminaNormalized);
        }

        private void UpdateHealthUi()
        {
            if (healthFill == null)
            {
                return;
            }

            SetBarFill(healthFill, resources.HealthNormalized);
        }

        private void UpdateShieldUi()
        {
            if (shieldRoot == null || shieldFill == null)
            {
                return;
            }

            var hasShield = resources.HasShield;
            shieldRoot.SetActive(hasShield);
            if (!hasShield)
            {
                return;
            }

            SetBarFill(shieldFill, resources.ShieldNormalized);
        }

        private void UpdateTextFeedback()
        {
            if (lastAmmo >= 0 && lastAmmo != resources.Ammo)
            {
                ammoPulseTimer = 0.18f;
            }

            if (lastMoney >= 0 && lastMoney != resources.Money)
            {
                moneyPulseTimer = 0.22f;
            }

            if (lastHealth >= 0f && !Mathf.Approximately(lastHealth, resources.Health))
            {
                healthPulseTimer = 0.18f;
            }

            if (lastShield >= 0f && !Mathf.Approximately(lastShield, resources.Shield))
            {
                shieldPulseTimer = 0.18f;
            }

            lastAmmo = resources.Ammo;
            lastMoney = resources.Money;
            lastHealth = resources.Health;
            lastShield = resources.Shield;

            ammoPulseTimer = Mathf.Max(0f, ammoPulseTimer - Time.deltaTime);
            moneyPulseTimer = Mathf.Max(0f, moneyPulseTimer - Time.deltaTime);
            healthPulseTimer = Mathf.Max(0f, healthPulseTimer - Time.deltaTime);
            shieldPulseTimer = Mathf.Max(0f, shieldPulseTimer - Time.deltaTime);

            ApplyPulse(ammoLabel != null ? ammoLabel.rectTransform : null, ammoPulseTimer);
            ApplyPulse(moneyLabel != null ? moneyLabel.rectTransform : null, moneyPulseTimer);
            ApplyPulse(healthFill != null ? healthFill.rectTransform.parent as RectTransform : null, healthPulseTimer);
            ApplyPulse(shieldFill != null ? shieldFill.rectTransform.parent as RectTransform : null, shieldPulseTimer);
        }

        private static void ApplyPulse(RectTransform target, float timer)
        {
            if (target == null)
            {
                return;
            }

            var amount = timer > 0f ? 1f + (Mathf.Sin((timer / 0.22f) * Mathf.PI) * 0.12f) : 1f;
            target.localScale = Vector3.one * amount;
        }

        private static void SetBarFill(Image fill, float normalizedAmount)
        {
            if (fill == null)
            {
                return;
            }

            var clampedAmount = Mathf.Clamp01(normalizedAmount);
            fill.fillAmount = clampedAmount;
            fill.enabled = clampedAmount > 0.001f;
            fill.rectTransform.localScale = new Vector3(clampedAmount, 1f, 1f);
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

        private static GameObject CreateBarRoot(string objectName, Transform parent)
        {
            var barRoot = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            barRoot.transform.SetParent(parent, false);
            barRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);
            barRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            return barRoot;
        }

        private static Image CreateBarFill(string objectName, Transform parent, Color color)
        {
            var fillObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(parent, false);
            var fill = fillObject.GetComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;

            var fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }

        private void EnsureDashChargeCount(int count)
        {
            count = Mathf.Max(1, count);
            if (dashRowTransform == null)
            {
                return;
            }

            if (dashCharges != null && dashCharges.Length == count)
            {
                return;
            }

            if (dashCharges != null)
            {
                for (var i = 0; i < dashCharges.Length; i++)
                {
                    if (dashCharges[i] != null)
                    {
                        Destroy(dashCharges[i].gameObject);
                    }
                }
            }

            dashCharges = new Image[count];
            for (var i = 0; i < dashCharges.Length; i++)
            {
                dashCharges[i] = CreateDashCharge($"Dash {i + 1}", dashRowTransform);
            }
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
