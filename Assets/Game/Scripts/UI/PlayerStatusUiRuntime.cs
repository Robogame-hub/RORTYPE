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
        private PlayerStatusHudView view;
        private int lastAmmo = -1;
        private int lastMoney = -1;
        private float ammoPulse, moneyPulse;
        private bool ownsCursor;
        private bool previousCursorVisible;
        private static readonly Color Ivory = new Color(0.89f, 0.86f, 0.77f);
        private static readonly Color Brass = new Color(0.74f, 0.59f, 0.35f);
        private static readonly Color Warning = new Color(0.94f, 0.28f, 0.2f);

        public static void Bind(PlayerResourceController playerResources)
        {
            if (instance == null)
            {
                var runtime = new GameObject("PlayerStatusUiRuntime");
                runtime.AddComponent<PlayerStatusUiRuntime>();
            }
            instance.SetPlayer(playerResources);
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
            var prefab = Resources.Load<GameObject>("UI/PlayerStatusHud");
            if (prefab == null)
            {
                Debug.LogError("Missing authored UI/PlayerStatusHud prefab.", this);
                enabled = false;
                return;
            }
            view = Instantiate(prefab, transform).GetComponent<PlayerStatusHudView>();
        }

        private void SetPlayer(PlayerResourceController player)
        {
            resources = player;
            motor = player != null ? player.GetComponent<TopDownPlayerMotor>() : null;
            skills = player != null ? player.GetComponent<PlayerSkillController>() : null;
            lastAmmo = lastMoney = -1;
            ammoPulse = moneyPulse = 0;
        }

        private void LateUpdate()
        {
            if (view == null) return;
            if (resources == null || !resources.isActiveAndEnabled)
                SetPlayer(PlayerResourceController.ActivePlayer);
            view.canvas.enabled = resources != null;
            UpdateCursor(resources != null);
            if (resources == null) return;

            if (lastAmmo >= 0 && lastAmmo != resources.Ammo) ammoPulse = 0.16f;
            if (lastMoney >= 0 && lastMoney != resources.Money) moneyPulse = 0.2f;
            lastAmmo = resources.Ammo;
            lastMoney = resources.Money;
            ammoPulse = Mathf.Max(0, ammoPulse - Time.deltaTime);
            moneyPulse = Mathf.Max(0, moneyPulse - Time.deltaTime);
            view.ammoLabel.text = resources.Ammo.ToString();
            view.ammoLabel.color = resources.Ammo <= 5 ? Warning : Ivory;
            view.moneyLabel.text = resources.Money.ToString("N0") + " G";
            view.ammoLabel.rectTransform.localScale = Vector3.one * (1f + ammoPulse * 0.3f);
            view.moneyLabel.rectTransform.localScale = Vector3.one * (1f + moneyPulse * 0.2f);

            view.healthLabel.text = $"{Mathf.CeilToInt(resources.Health)} / {Mathf.CeilToInt(resources.MaxHealth)}";
            Fill(view.healthFill, resources.HealthNormalized);
            view.healthFill.color = resources.HealthNormalized < 0.25f
                ? Color.Lerp(new Color(0.5f, 0.07f, 0.06f), Warning, 0.5f + 0.5f * Mathf.Sin(Time.time * 5f))
                : new Color(0.18f, 0.79f, 0.69f);
            // Shield gauge removed from the HUD. Stamina is a visual placeholder
            // until the shared sprint/shield resource is implemented.
            view.shieldRoot.SetActive(false);
            view.shieldFill.transform.parent.gameObject.SetActive(false);
            Fill(view.staminaFill, 1f);

            for (var i = 0; i < view.skillSlots.Length; i++)
            {
                var available = skills != null && skills.isActiveAndEnabled;
                var remaining = available ? skills.GetSkillCooldownRemaining(i) : 0f;
                var ready = available && remaining <= 0.001f;
                view.skillSlots[i].color = ready
                    ? new Color(0.06f, 0.12f, 0.12f, 0.3f)
                    : new Color(0.025f, 0.035f, 0.04f, 0.65f);
                if (i < view.skillIcons.Length)
                    view.skillIcons[i].color = ready ? Ivory : new Color(0.4f, 0.46f, 0.45f, 0.3f);
                view.skillCooldownLabels[i].text = available && !ready ? Mathf.CeilToInt(remaining).ToString() : "";
                view.skillStatusLabels[i].text = ready ? "ГОТОВО" : available ? "" : "НЕДОСТУПНО";
                view.skillKeyLabels[i].text = available ? KeyLabel(skills.GetSkillKey(i)) : "-";
            }
            for (var i = 0; i < view.dashCharges.Length; i++)
            {
                var visible = motor != null && i < motor.MaxDashCharges;
                view.dashCharges[i].gameObject.SetActive(visible);
                if (visible) view.dashCharges[i].color = i < motor.DashCharges
                    ? new Color(0.18f, 0.79f, 0.69f) : new Color(0.18f, 0.25f, 0.24f);
            }
        }

        private static void Fill(Image image, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            image.enabled = fraction > 0.001f;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(fraction, 1f);
            rect.offsetMin = new Vector2(3, 2);
            rect.offsetMax = new Vector2(-3, -2);
        }

        private static string KeyLabel(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return ((int)key - (int)KeyCode.Alpha0).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
                return ((int)key - (int)KeyCode.Keypad0).ToString();
            return key == KeyCode.None ? "-" : key.ToString();
        }

        private void UpdateCursor(bool hasPlayer)
        {
            if (view.reticle == null) return;
            var mouse = Input.mousePosition;
            var show = hasPlayer && Application.isFocused && Time.timeScale > 0f &&
                !ShopUiPanel.IsAnyOpen && !PortalUiRuntime.IsChoiceOpen &&
                Cursor.lockState == CursorLockMode.None &&
                mouse.x >= 0 && mouse.y >= 0 && mouse.x < Screen.width && mouse.y < Screen.height;
            view.reticle.gameObject.SetActive(show);
            if (show)
            {
                if (!ownsCursor) { previousCursorVisible = Cursor.visible; ownsCursor = true; }
                Cursor.visible = false;
                view.reticle.position = mouse;
            }
            else RestoreCursor();
        }

        private void RestoreCursor()
        {
            if (!ownsCursor) return;
            Cursor.visible = previousCursorVisible;
            ownsCursor = false;
        }

        private void OnDisable() => RestoreCursor();

        private void OnDestroy()
        {
            RestoreCursor();
            if (instance == this) instance = null;
        }
    }
}
