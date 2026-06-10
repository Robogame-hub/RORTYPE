using RorType.Gameplay.UI;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerResourceController : MonoBehaviour
    {
        [Header("Ammo")]
        [SerializeField, Min(0)] private int maxAmmo = 999;
        [SerializeField, Min(0)] private int startingAmmo = 100;

        [Header("Stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 25f;
        [SerializeField, Min(0f)] private float staminaRegenPerSecond = 35f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 0.45f;

        private float staminaRegenDelayTimer;

        public int Ammo { get; private set; }
        public int MaxAmmo => maxAmmo;
        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public float StaminaNormalized => maxStamina > 0f ? Mathf.Clamp01(Stamina / maxStamina) : 0f;
        public bool HasAmmo => Ammo > 0;
        public bool HasSprintStamina => Stamina > 0.01f;

        private void Awake()
        {
            maxAmmo = Mathf.Max(0, maxAmmo);
            startingAmmo = Mathf.Clamp(startingAmmo, 0, maxAmmo);
            maxStamina = Mathf.Max(1f, maxStamina);

            Ammo = startingAmmo;
            Stamina = maxStamina;
        }

        private void OnEnable()
        {
            PlayerStatusUiRuntime.Bind(this);
        }

        private void Update()
        {
            TickStaminaRecovery(Time.deltaTime);
        }

        public bool TryConsumeAmmo(int amount)
        {
            amount = Mathf.Max(1, amount);
            if (Ammo < amount)
            {
                return false;
            }

            Ammo -= amount;
            return true;
        }

        public void AddAmmo(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Ammo = Mathf.Clamp(Ammo + amount, 0, maxAmmo);
        }

        public bool TryConsumeSprint(float deltaTime)
        {
            if (sprintDrainPerSecond <= 0f)
            {
                return true;
            }

            if (!HasSprintStamina)
            {
                return false;
            }

            Stamina = Mathf.Max(0f, Stamina - (sprintDrainPerSecond * Mathf.Max(0f, deltaTime)));
            staminaRegenDelayTimer = staminaRegenDelay;
            return true;
        }

        private void TickStaminaRecovery(float deltaTime)
        {
            if (Stamina >= maxStamina)
            {
                Stamina = maxStamina;
                return;
            }

            if (staminaRegenDelayTimer > 0f)
            {
                staminaRegenDelayTimer = Mathf.Max(0f, staminaRegenDelayTimer - deltaTime);
                return;
            }

            Stamina = Mathf.Min(maxStamina, Stamina + (staminaRegenPerSecond * Mathf.Max(0f, deltaTime)));
        }

        private void OnValidate()
        {
            maxAmmo = Mathf.Max(0, maxAmmo);
            startingAmmo = Mathf.Clamp(startingAmmo, 0, maxAmmo);
            maxStamina = Mathf.Max(1f, maxStamina);
        }
    }
}
