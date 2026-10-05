using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsHudController : MonoBehaviour
    {
        [SerializeField] private RtsSelectionController selection;
        [SerializeField] private UnityEngine.UI.Text selectionLabel;
        [SerializeField] private UnityEngine.UI.Text ammoLabel;
        [SerializeField] private UnityEngine.UI.Text commandLabel;
        [SerializeField] private Button[] abilityButtons;
        [SerializeField] private UnityEngine.UI.Text[] abilityButtonLabels;

        private RtsAbility[] activeAbilities = System.Array.Empty<RtsAbility>();

        private void Awake()
        {
            selection ??= FindObjectOfType<RtsSelectionController>();
            for (var i = 0; i < abilityButtons.Length; i++)
            {
                var slot = i;
                if (abilityButtons[i] != null)
                {
                    abilityButtons[i].onClick.AddListener(() => TryUseAbility(slot));
                }
            }
        }

        private void OnEnable()
        {
            if (selection != null)
            {
                selection.SelectionChanged += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (selection != null)
            {
                selection.SelectionChanged -= Refresh;
            }
        }

        private void Update()
        {
            RefreshAbilityState();
        }

        private void Refresh()
        {
            if (selection == null || selection.SelectedUnits.Count == 0)
            {
                activeAbilities = System.Array.Empty<RtsAbility>();
                SetLabel(selectionLabel, "No unit selected");
                SetLabel(ammoLabel, string.Empty);
                SetLabel(commandLabel, string.Empty);
                RefreshAbilityState();
                return;
            }

            if (selection.SelectedUnits.Count > 1)
            {
                activeAbilities = System.Array.Empty<RtsAbility>();
                SetLabel(selectionLabel, $"{selection.SelectedUnits.Count} units selected");
                SetLabel(ammoLabel, "Mixed ammo");
                SetLabel(commandLabel, "Group command ready");
                RefreshAbilityState();
                return;
            }

            var unit = selection.SelectedUnits[0];
            activeAbilities = unit != null ? unit.GetAbilities() : System.Array.Empty<RtsAbility>();
            if (unit == null)
            {
                return;
            }

            SetLabel(selectionLabel, unit.DisplayName);
            SetLabel(ammoLabel, $"Ammo {unit.Ammo} / {unit.MaxAmmo}");
            SetLabel(commandLabel, CommandText(unit.CommandMode));
            RefreshAbilityState();
        }

        private void RefreshAbilityState()
        {
            for (var i = 0; i < abilityButtons.Length; i++)
            {
                var ability = i < activeAbilities.Length ? activeAbilities[i] : null;
                if (abilityButtons[i] != null)
                {
                    abilityButtons[i].gameObject.SetActive(ability != null);
                    abilityButtons[i].interactable = ability != null && ability.IsReady;
                }
                if (i < abilityButtonLabels.Length && abilityButtonLabels[i] != null)
                {
                    abilityButtonLabels[i].text = ability == null
                        ? string.Empty
                        : ability.IsReady
                            ? ability.DisplayName
                            : $"{ability.DisplayName} {Mathf.CeilToInt(ability.CooldownRemaining)}";
                }
            }
        }

        private void TryUseAbility(int slot)
        {
            if (selection != null)
            {
                selection.TryUseAbility(slot);
            }
        }

        private void OnGUI()
        {
            if (selectionLabel != null || selection == null)
            {
                return;
            }

            var panel = new Rect(18f, Screen.height - 160f, 280f, 142f);
            GUI.Box(panel, GUIContent.none);
            if (selection.SelectedUnits.Count == 0)
            {
                GUI.Label(new Rect(panel.x + 12f, panel.y + 12f, 240f, 24f), "No unit selected");
                return;
            }

            if (selection.SelectedUnits.Count > 1)
            {
                GUI.Label(new Rect(panel.x + 12f, panel.y + 12f, 240f, 24f), $"{selection.SelectedUnits.Count} units selected");
                GUI.Label(new Rect(panel.x + 12f, panel.y + 40f, 240f, 24f), "Group command ready");
                return;
            }

            var unit = selection.SelectedUnits[0];
            if (unit == null)
            {
                return;
            }

            GUI.Label(new Rect(panel.x + 12f, panel.y + 12f, 250f, 24f), unit.DisplayName);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 38f, 250f, 24f), $"Ammo {unit.Ammo} / {unit.MaxAmmo}");
            var abilities = unit.GetAbilities();
            for (var i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] == null)
                {
                    continue;
                }

                var label = abilities[i].IsReady
                    ? abilities[i].DisplayName
                    : $"{abilities[i].DisplayName} {Mathf.CeilToInt(abilities[i].CooldownRemaining)}";
                GUI.enabled = abilities[i].IsReady;
                if (GUI.Button(new Rect(panel.x + 12f, panel.y + 70f + (i * 30f), 250f, 25f), label))
                {
                    TryUseAbility(i);
                }
                GUI.enabled = true;
            }
        }

        private static string CommandText(RtsCommandMode mode)
        {
            return mode switch
            {
                RtsCommandMode.ForcedMove => "Forced move",
                RtsCommandMode.AssaultMove => "Assault move",
                RtsCommandMode.AttackTarget => "Attacking",
                _ => "Holding"
            };
        }

        private static void SetLabel(UnityEngine.UI.Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
