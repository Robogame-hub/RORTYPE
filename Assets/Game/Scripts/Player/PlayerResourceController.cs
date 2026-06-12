using System;
using RorType.Gameplay.Combat;
using RorType.Gameplay.UI;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerResourceController : MonoBehaviour, IDamageable
    {
        private struct PersistentState
        {
            public int Ammo;
            public int Money;
            public float Health;
            public bool DamageUpgradePurchased;
        }

        private static PersistentState persistentState;
        private static bool hasPersistentState;

        private const int DefaultMaxAmmo = 999;
        private const int DefaultStartingAmmo = 100;
        private const int DefaultMaxMoney = 999999;
        private const float DefaultMaxHealth = 500f;
        private const float DefaultMaxStamina = 100f;
        private const float DefaultSprintDrainPerSecond = 10f;
        private const float DefaultStaminaRegenPerSecond = 35f;
        private const float DefaultStaminaRegenDelay = 0.45f;

        [Header("Ammo")]
        [SerializeField, Min(0)] private int maxAmmo = 999;
        [SerializeField, Min(0)] private int startingAmmo = 100;

        [Header("Money")]
        [SerializeField, Min(0)] private int startingMoney;
        [SerializeField, Min(0)] private int maxMoney = 999999;

        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 500f;
        [SerializeField, Min(1f)] private float startingHealth = 500f;

        [Header("Stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 10f;
        [SerializeField, Min(0f)] private float staminaRegenPerSecond = 35f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 0.45f;

        private float staminaRegenDelayTimer;

        public int Ammo { get; private set; }
        public int MaxAmmo => maxAmmo;
        public int Money { get; private set; }
        public int MaxMoney => maxMoney;
        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(Health / maxHealth) : 0f;
        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public float StaminaNormalized => maxStamina > 0f ? Mathf.Clamp01(Stamina / maxStamina) : 0f;
        public bool HasAmmo => Ammo > 0;
        public bool HasSprintStamina => Stamina > 0.01f;
        public bool HasDamageUpgrade { get; private set; }
        public float DamageMultiplier => HasDamageUpgrade ? 2f : 1f;
        public CombatTeam Team => CombatTeam.Player;
        public bool IsAlive => Health > 0f;

        public event Action<int> AmmoChanged;
        public event Action<int> MoneyChanged;
        public event Action<float, float> HealthChanged;
        public event Action<float, float> StaminaChanged;

        private void Awake()
        {
            NormalizeSettings();

            if (hasPersistentState)
            {
                Ammo = Mathf.Clamp(persistentState.Ammo, 0, maxAmmo);
                Money = Mathf.Clamp(persistentState.Money, 0, maxMoney);
                Health = Mathf.Clamp(persistentState.Health, 0f, maxHealth);
                HasDamageUpgrade = persistentState.DamageUpgradePurchased;
            }
            else
            {
                Ammo = startingAmmo;
                Money = startingMoney;
                Health = startingHealth;
                HasDamageUpgrade = false;
                SavePersistentState();
            }

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
            SavePersistentState();
            AmmoChanged?.Invoke(Ammo);
            return true;
        }

        public void AddAmmo(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Ammo = Mathf.Clamp(Ammo + amount, 0, maxAmmo);
            SavePersistentState();
            AmmoChanged?.Invoke(Ammo);
        }

        public bool TrySpendMoney(int amount)
        {
            amount = Mathf.Max(1, amount);
            if (Money < amount)
            {
                return false;
            }

            Money -= amount;
            SavePersistentState();
            MoneyChanged?.Invoke(Money);
            return true;
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Money = Mathf.Clamp(Money + amount, 0, maxMoney);
            SavePersistentState();
            MoneyChanged?.Invoke(Money);
        }

        public void AddHealth(float amount)
        {
            if (amount <= 0f || Health >= maxHealth)
            {
                return;
            }

            Health = Mathf.Clamp(Health + amount, 0f, maxHealth);
            SavePersistentState();
            HealthChanged?.Invoke(Health, maxHealth);
        }

        public bool TryPurchaseDamageUpgrade(int cost)
        {
            if (HasDamageUpgrade || !TrySpendMoney(cost))
            {
                return false;
            }

            HasDamageUpgrade = true;
            SavePersistentState();
            return true;
        }

        public bool ReceiveHit(in CombatHitInfo hitInfo)
        {
            if (!IsAlive || hitInfo.Team == CombatTeam.Player || hitInfo.Damage <= 0f)
            {
                return false;
            }

            Health = Mathf.Max(0f, Health - hitInfo.Damage);
            SavePersistentState();
            HealthChanged?.Invoke(Health, maxHealth);
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.1f, $"-{hitInfo.Damage:0} HP", Color.red, 0.14f);
            return true;
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
            StaminaChanged?.Invoke(Stamina, maxStamina);
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

            var previousStamina = Stamina;
            Stamina = Mathf.Min(maxStamina, Stamina + (staminaRegenPerSecond * Mathf.Max(0f, deltaTime)));
            if (!Mathf.Approximately(previousStamina, Stamina))
            {
                StaminaChanged?.Invoke(Stamina, maxStamina);
            }
        }

        private void SavePersistentState()
        {
            persistentState = new PersistentState
            {
                Ammo = Ammo,
                Money = Money,
                Health = Health,
                DamageUpgradePurchased = HasDamageUpgrade
            };
            hasPersistentState = true;
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void NormalizeSettings()
        {
            if (maxAmmo <= 0)
            {
                maxAmmo = DefaultMaxAmmo;
            }

            if (startingAmmo <= 0)
            {
                startingAmmo = DefaultStartingAmmo;
            }

            if (maxMoney <= 0)
            {
                maxMoney = DefaultMaxMoney;
            }

            if (maxHealth <= 1f)
            {
                maxHealth = DefaultMaxHealth;
            }

            if (startingHealth <= 1f)
            {
                startingHealth = maxHealth;
            }

            if (maxStamina <= 1f)
            {
                maxStamina = DefaultMaxStamina;
            }

            if (sprintDrainPerSecond <= 0f)
            {
                sprintDrainPerSecond = DefaultSprintDrainPerSecond;
            }

            if (staminaRegenPerSecond <= 0f)
            {
                staminaRegenPerSecond = DefaultStaminaRegenPerSecond;
            }

            if (staminaRegenDelay < 0f)
            {
                staminaRegenDelay = DefaultStaminaRegenDelay;
            }

            maxAmmo = Mathf.Max(1, maxAmmo);
            startingAmmo = Mathf.Clamp(startingAmmo, 0, maxAmmo);
            maxMoney = Mathf.Max(1, maxMoney);
            startingMoney = Mathf.Clamp(startingMoney, 0, maxMoney);
            maxHealth = Mathf.Max(1f, maxHealth);
            startingHealth = Mathf.Clamp(startingHealth, 1f, maxHealth);
            maxStamina = Mathf.Max(1f, maxStamina);
        }
    }
}
