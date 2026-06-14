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
        private PlayerSkillController skills;
        private Canvas canvas;
        private Text ammoLabel;
        private Text moneyLabel;
        private Image[] skillSlots;
        private Text[] skillCooldownLabels;
        private Text[] skillKeyLabels;
        private Transform skillRowTransform;
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
            runtime.skills = playerResources != null
                ? playerResources.GetComponent<PlayerSkillController>()
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
                skills = resources != null ? resources.GetComponent<PlayerSkillController>() : null;
            }
            else if (skills == null)
            {
                skills = resources.GetComponent<PlayerSkillController>();
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
            moneyLabel.text = $"{resources.Money}G";
            UpdateSkillUi();
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
            rootRect.sizeDelta = new Vector2(220f, 152f);
            rootRect.anchoredPosition = new Vector2(-28f, 28f);

            var rootLayout = root.GetComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8f;
            rootLayout.childAlignment = TextAnchor.LowerRight;
            rootLayout.childControlHeight = false;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            ammoLabel = CreateLabel("Ammo Label", root.transform, uiFont, 24, new Vector2(0f, 28f));
            var ammoOutline = ammoLabel.gameObject.AddComponent<Outline>();
            ammoOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            ammoOutline.effectDistance = new Vector2(2f, -2f);

            var skillRow = new GameObject("Skill Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            skillRow.transform.SetParent(root.transform, false);
            skillRowTransform = skillRow.transform;
            skillRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
            var skillLayout = skillRow.GetComponent<HorizontalLayoutGroup>();
            skillLayout.spacing = 8f;
            skillLayout.childAlignment = TextAnchor.MiddleRight;
            skillLayout.childControlHeight = false;
            skillLayout.childControlWidth = false;
            skillLayout.childForceExpandHeight = false;
            skillLayout.childForceExpandWidth = false;

            EnsureSkillSlotCount(PlayerSkillController.SkillSlotCount, uiFont);

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

        private void UpdateSkillUi()
        {
            if (skillSlots == null || skillCooldownLabels == null || skillKeyLabels == null)
            {
                return;
            }

            for (var i = 0; i < skillSlots.Length; i++)
            {
                var hasSkills = skills != null && skills.isActiveAndEnabled;
                var remaining = hasSkills ? skills.GetSkillCooldownRemaining(i) : 0f;
                var duration = hasSkills ? skills.GetSkillCooldownDuration(i) : 0f;
                var isReady = hasSkills && remaining <= 0.001f;
                var cooldownProgress = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;

                skillSlots[i].color = isReady
                    ? new Color(0.32f, 0.24f, 0.42f, 0.95f)
                    : Color.Lerp(new Color(0.1f, 0.08f, 0.13f, 0.92f), new Color(0.48f, 0.16f, 0.72f, 0.92f), 1f - cooldownProgress);

                if (skillCooldownLabels[i] != null)
                {
                    skillCooldownLabels[i].text = hasSkills && remaining > 0.001f
                        ? Mathf.CeilToInt(remaining).ToString()
                        : string.Empty;
                }

                if (skillKeyLabels[i] != null)
                {
                    skillKeyLabels[i].text = hasSkills ? FormatKeyLabel(skills.GetSkillKey(i)) : string.Empty;
                }
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

        private void EnsureSkillSlotCount(int count, Font font)
        {
            count = Mathf.Max(1, count);
            if (skillRowTransform == null)
            {
                return;
            }

            if (skillSlots != null && skillSlots.Length == count)
            {
                return;
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (skillSlots != null)
            {
                for (var i = 0; i < skillSlots.Length; i++)
                {
                    if (skillSlots[i] != null)
                    {
                        Destroy(skillSlots[i].gameObject);
                    }
                }
            }

            skillSlots = new Image[count];
            skillCooldownLabels = new Text[count];
            skillKeyLabels = new Text[count];
            for (var i = 0; i < skillSlots.Length; i++)
            {
                CreateSkillSlot($"Skill {i + 1}", skillRowTransform, font, out skillSlots[i], out skillCooldownLabels[i], out skillKeyLabels[i]);
            }
        }

        private static void CreateSkillSlot(string objectName, Transform parent, Font font, out Image slotImage, out Text cooldownLabel, out Text keyLabel)
        {
            var slotObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Outline));
            slotObject.transform.SetParent(parent, false);
            slotObject.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 44f);
            slotImage = slotObject.GetComponent<Image>();
            slotImage.color = new Color(0.32f, 0.24f, 0.42f, 0.95f);

            var outline = slotObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var cooldownObject = new GameObject("Cooldown", typeof(RectTransform), typeof(Text), typeof(Outline));
            cooldownObject.transform.SetParent(slotObject.transform, false);
            cooldownLabel = cooldownObject.GetComponent<Text>();
            cooldownLabel.font = font;
            cooldownLabel.fontSize = 24;
            cooldownLabel.alignment = TextAnchor.MiddleCenter;
            cooldownLabel.color = Color.white;
            cooldownLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            cooldownLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var cooldownOutline = cooldownObject.GetComponent<Outline>();
            cooldownOutline.effectColor = Color.black;
            cooldownOutline.effectDistance = new Vector2(2f, -2f);
            var cooldownRect = cooldownLabel.rectTransform;
            cooldownRect.anchorMin = Vector2.zero;
            cooldownRect.anchorMax = Vector2.one;
            cooldownRect.offsetMin = Vector2.zero;
            cooldownRect.offsetMax = Vector2.zero;

            var keyObject = new GameObject("Key", typeof(RectTransform), typeof(Text), typeof(Outline));
            keyObject.transform.SetParent(slotObject.transform, false);
            keyLabel = keyObject.GetComponent<Text>();
            keyLabel.font = font;
            keyLabel.fontSize = 14;
            keyLabel.alignment = TextAnchor.LowerRight;
            keyLabel.color = new Color(1f, 0.86f, 0.1f, 1f);
            keyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            keyLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var keyOutline = keyObject.GetComponent<Outline>();
            keyOutline.effectColor = Color.black;
            keyOutline.effectDistance = new Vector2(1f, -1f);
            var keyRect = keyLabel.rectTransform;
            keyRect.anchorMin = Vector2.zero;
            keyRect.anchorMax = Vector2.one;
            keyRect.offsetMin = new Vector2(3f, 2f);
            keyRect.offsetMax = new Vector2(-4f, -2f);
        }

        private static string FormatKeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha0:
                case KeyCode.Keypad0:
                    return "0";
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    return "1";
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    return "2";
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    return "3";
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    return "4";
                case KeyCode.Alpha5:
                case KeyCode.Keypad5:
                    return "5";
                case KeyCode.Alpha6:
                case KeyCode.Keypad6:
                    return "6";
                case KeyCode.Alpha7:
                case KeyCode.Keypad7:
                    return "7";
                case KeyCode.Alpha8:
                case KeyCode.Keypad8:
                    return "8";
                case KeyCode.Alpha9:
                case KeyCode.Keypad9:
                    return "9";
                default:
                    return key == KeyCode.None ? string.Empty : key.ToString();
            }
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
