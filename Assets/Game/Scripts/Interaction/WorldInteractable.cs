using System;
using System.Collections;
using System.Collections.Generic;
using RorType.Gameplay.Combat;
using RorType.Gameplay.Player;
using RorType.Gameplay.UI;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class WorldInteractable : MonoBehaviour
    {
        public enum InteractionMode
        {
            Purchase = 0,
            ResourcePickup = 1
        }

        public enum ShopKind
        {
            Auto = 0,
            Merchant = 1,
            Blacksmith = 2
        }

        public enum ShopItemType
        {
            Ammo = 0,
            Health = 1,
            FullHeal = 2,
            ShieldUnlock = 3,
            ShieldRestore = 4,
            ShieldUpgrade = 5,
            MaxHealthUpgrade = 6,
            DamageUpgrade = 7,
            ExtraDashCharge = 8
        }

        [Serializable]
        private sealed class ShopItem
        {
            [SerializeField] private ShopItemType itemType;
            [SerializeField] private string displayName;
            [SerializeField, Min(0)] private int cost = 1;
            [SerializeField, Min(0)] private int amount = 1;
            [SerializeField] private bool repeatWhileHeld;
            [SerializeField] private bool hideWhenUnavailable = true;

            public ShopItemType ItemType => itemType;
            public int Cost => Mathf.Max(0, cost);
            public int Amount => Mathf.Max(0, amount);
            public bool RepeatWhileHeld => repeatWhileHeld;
            public bool HideWhenUnavailable => hideWhenUnavailable;

            public ShopItem(
                ShopItemType itemType,
                string displayName,
                int cost,
                int amount,
                bool repeatWhileHeld,
                bool hideWhenUnavailable = true)
            {
                this.itemType = itemType;
                this.displayName = displayName;
                this.cost = cost;
                this.amount = amount;
                this.repeatWhileHeld = repeatWhileHeld;
                this.hideWhenUnavailable = hideWhenUnavailable;
            }

            public string GetDisplayName()
            {
                return string.IsNullOrWhiteSpace(displayName)
                    ? ResolveDefaultName(itemType)
                    : displayName;
            }

            private static string ResolveDefaultName(ShopItemType itemType)
            {
                return itemType switch
                {
                    ShopItemType.Ammo => "\u041F\u0430\u0442\u0440\u043E\u043D\u044B",
                    ShopItemType.Health => "\u041B\u0435\u0447\u0435\u043D\u0438\u0435",
                    ShopItemType.FullHeal => "\u041F\u043E\u043B\u043D\u043E\u0435 \u043B\u0435\u0447\u0435\u043D\u0438\u0435",
                    ShopItemType.ShieldUnlock => "\u0429\u0438\u0442",
                    ShopItemType.ShieldRestore => "\u0412\u043E\u0441\u0441\u0442\u0430\u043D\u043E\u0432\u0438\u0442\u044C \u0449\u0438\u0442",
                    ShopItemType.ShieldUpgrade => "\u0423\u0441\u0438\u043B\u0435\u043D\u0438\u0435 \u0449\u0438\u0442\u0430",
                    ShopItemType.MaxHealthUpgrade => "\u0423\u0441\u0438\u043B\u0435\u043D\u0438\u0435 HP",
                    ShopItemType.DamageUpgrade => "\u0423\u0441\u0438\u043B\u0438\u0442\u044C \u0443\u0440\u043E\u043D",
                    ShopItemType.ExtraDashCharge => "\u0414\u043E\u043F. \u0440\u044B\u0432\u043E\u043A",
                    _ => "\u0422\u043E\u0432\u0430\u0440"
                };
            }
        }

        private static readonly List<WorldInteractable> RegisteredInteractables = new();

        [SerializeField] private InteractionMode mode = InteractionMode.Purchase;
        [SerializeField] private ShopKind shopKind = ShopKind.Auto;
        [SerializeField, Min(1f)] private float interactionRadius = 7f;
        [SerializeField] private string interactionPrompt = "Buy: press E";
        [SerializeField] private string completedPrompt = "Bought";
        [SerializeField, Min(0)] private int ammoReward;
        [SerializeField, Min(0)] private int moneyReward;
        [SerializeField] private bool oneShot;
        [SerializeField] private bool disableMinimapMarkerOnComplete = true;
        [SerializeField] private bool autoCreateInteractionTrigger = true;
        [SerializeField, Min(0f)] private float feedbackDuration = 1.2f;
        [SerializeField, Min(1)] private int ammoPurchaseAmount = 1;
        [SerializeField, Min(1)] private int ammoPurchaseCost = 1;
        [SerializeField, Min(1)] private int healthPurchaseAmount = 20;
        [SerializeField, Min(1)] private int healthPurchaseCost = 20;
        [SerializeField, Min(1)] private int damageUpgradeCost = 1000;
        [Header("Shop Items")]
        [SerializeField] private List<ShopItem> shopItems = new();
        [SerializeField, Min(0f)] private float pickupDisappearDelay = 0.7f;
        [Header("Container Drops")]
        [SerializeField] private ResourcePickupCollectible containerMoneyPickupPrefab;
        [SerializeField] private ResourcePickupCollectible containerAmmoPickupPrefab;
        [SerializeField] private ResourcePickupCollectible containerHealthPickupPrefab;
        [SerializeField, Min(0)] private int containerMoneyPickupCount = 10;
        [SerializeField, Min(1)] private int containerMoneyPerPickup = 10;
        [SerializeField, Min(0)] private int containerAmmoPickupCount = 5;
        [SerializeField, Min(1)] private int containerAmmoPerPickup = 10;
        [SerializeField, Range(0f, 1f)] private float containerHealthDropChance = 0.6f;
        [SerializeField, Min(1)] private int containerHealthAmount = 150;
        [SerializeField, Min(0f)] private float containerDropLaunchSpeed = 4.2f;

        private const float DefaultFeedbackDuration = 1.2f;
        private const int DefaultAmmoPurchaseAmount = 1;
        private const int DefaultAmmoPurchaseCost = 1;
        private const int DefaultHealthPurchaseAmount = 20;
        private const int DefaultHealthPurchaseCost = 20;
        private const int DefaultDamageUpgradeCost = 1000;
        private const float DefaultPickupDisappearDelay = 0.7f;
        private const int DefaultContainerMoneyPickupCount = 10;
        private const int DefaultContainerMoneyPerPickup = 10;
        private const int DefaultContainerAmmoPickupCount = 5;
        private const int DefaultContainerAmmoPerPickup = 10;
        private const float DefaultContainerHealthDropChance = 0.6f;
        private const int DefaultContainerHealthAmount = 150;
        private const float DefaultContainerDropLaunchSpeed = 4.2f;

        private readonly HashSet<ScenePortalInteractionController> touchingInteractors = new();
        private Collider interactionTrigger;
        private MinimapTrackable minimapTrackable;
        private bool isCompleted;
        private bool isCompleting;
        private float feedbackUntilTime;
        private string currentFeedbackPrompt;

        public static IReadOnlyList<WorldInteractable> ActiveInteractables => RegisteredInteractables;
        public bool IsAvailable => isActiveAndEnabled && (!oneShot || !isCompleted);

        private void Awake()
        {
            NormalizeSettings();
            minimapTrackable = GetComponent<MinimapTrackable>();
            EnsureInteractionTrigger();
        }

        private void OnEnable()
        {
            if (!RegisteredInteractables.Contains(this))
            {
                RegisteredInteractables.Add(this);
            }
        }

        private void OnDisable()
        {
            RegisteredInteractables.Remove(this);
            touchingInteractors.Clear();
        }

        public bool IsTouchedBy(ScenePortalInteractionController interactor)
        {
            return interactor != null && touchingInteractors.Contains(interactor);
        }

        public float GetSqrDistanceTo(Vector3 worldPosition)
        {
            return (transform.position - worldPosition).sqrMagnitude;
        }

        public string GetInteractionPrompt()
        {
            if (Time.time < feedbackUntilTime)
            {
                return string.IsNullOrWhiteSpace(currentFeedbackPrompt) ? completedPrompt : currentFeedbackPrompt;
            }

            return interactionPrompt;
        }

        public void Interact(ScenePortalInteractionController interactor)
        {
            if (!IsAvailable)
            {
                return;
            }

            switch (mode)
            {
                case InteractionMode.ResourcePickup:
                    ResolveResourcePickup(interactor);
                    break;
                default:
                    OpenShop(interactor);
                    break;
            }
        }

        private void ResolveResourcePickup(ScenePortalInteractionController interactor)
        {
            if (isCompleting)
            {
                return;
            }

            ResolvePickupRewards(out var resolvedAmmoReward, out var resolvedMoneyReward);
            var resolvedHealthReward = 0;
            if (IsContainerDropResource())
            {
                SpawnContainerDrops(out resolvedMoneyReward, out resolvedAmmoReward, out resolvedHealthReward);
            }
            else
            {
                var resources = interactor != null
                    ? interactor.GetComponent<PlayerResourceController>()
                    : null;

                if (resources != null)
                {
                    resources.AddMoney(resolvedMoneyReward);
                    resources.AddAmmo(resolvedAmmoReward);
                }
            }

            isCompleted = true;
            isCompleting = true;
            currentFeedbackPrompt = FormatRewardText(resolvedMoneyReward, resolvedAmmoReward, resolvedHealthReward);
            feedbackUntilTime = Time.time + feedbackDuration;
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, currentFeedbackPrompt, new Color(1f, 0.86f, 0.08f), 0.18f);

            if (disableMinimapMarkerOnComplete && minimapTrackable != null)
            {
                minimapTrackable.enabled = false;
            }

            if (interactionTrigger != null)
            {
                interactionTrigger.enabled = false;
            }

            RegisteredInteractables.Remove(this);
            touchingInteractors.Clear();
            StartCoroutine(DisappearAfterFeedback());
        }

        private void OpenShop(ScenePortalInteractionController interactor)
        {
            var resources = interactor != null
                ? interactor.GetComponent<PlayerResourceController>()
                : null;

            if (resources == null)
            {
                currentFeedbackPrompt = "\u041D\u0435\u0442 \u0438\u0433\u0440\u043E\u043A\u0430";
                feedbackUntilTime = Time.time + feedbackDuration;
                return;
            }

            var title = ResolveShopKind() == ShopKind.Blacksmith
                ? "\u041A\u0443\u0437\u043D\u0435\u0446"
                : "\u0422\u043E\u0440\u0433\u043E\u0432\u0435\u0446";

            var options = BuildShopOptions(resources);
            if (options.Count == 0)
            {
                ShowShopFeedback("\u041D\u0435\u0442 \u0442\u043E\u0432\u0430\u0440\u043E\u0432");
                return;
            }

            PortalUiRuntime.ShowChoice(title, options);
        }

        private List<PortalUiRuntime.ChoiceOption> BuildShopOptions(PlayerResourceController resources)
        {
            var options = new List<PortalUiRuntime.ChoiceOption>();
            var items = ResolveShopItems();
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (item == null)
                {
                    continue;
                }

                if (!ShouldShowShopItem(resources, item))
                {
                    continue;
                }

                var label = FormatShopItemLabel(resources, item);
                options.Add(new PortalUiRuntime.ChoiceOption(
                    label,
                    () => TryBuyShopItem(resources, item),
                    item.RepeatWhileHeld));
            }

            return options;
        }

        private IReadOnlyList<ShopItem> ResolveShopItems()
        {
            if (shopItems != null && shopItems.Count > 0)
            {
                return shopItems;
            }

            return ResolveShopKind() == ShopKind.Blacksmith
                ? CreateFallbackBlacksmithItems()
                : CreateFallbackMerchantItems();
        }

        private List<ShopItem> CreateFallbackMerchantItems()
        {
            return new List<ShopItem>
            {
                new(ShopItemType.Ammo, "\u041F\u0430\u0442\u0440\u043E\u043D\u044B", ammoPurchaseCost, ammoPurchaseAmount, true, false),
                new(ShopItemType.Health, "\u041B\u0435\u0447\u0435\u043D\u0438\u0435", healthPurchaseCost, healthPurchaseAmount, true, false),
                new(ShopItemType.FullHeal, "\u041F\u043E\u043B\u043D\u043E\u0435 \u043B\u0435\u0447\u0435\u043D\u0438\u0435", 500, 0, false, false),
                new(ShopItemType.ShieldUnlock, "\u0414\u043E\u043F. \u0449\u0438\u0442", 1000, 100, false),
                new(ShopItemType.ShieldRestore, "\u0412\u043E\u0441\u0441\u0442. \u0449\u0438\u0442", 100, 0, true)
            };
        }

        private List<ShopItem> CreateFallbackBlacksmithItems()
        {
            return new List<ShopItem>
            {
                new(ShopItemType.Ammo, "\u041F\u0430\u0442\u0440\u043E\u043D\u044B", ammoPurchaseCost, ammoPurchaseAmount, true, false),
                new(ShopItemType.ExtraDashCharge, "\u0414\u043E\u043F. \u0440\u044B\u0432\u043E\u043A", 1000, 1, false),
                new(ShopItemType.DamageUpgrade, "\u0423\u0441\u0438\u043B\u0438\u0442\u044C \u0443\u0440\u043E\u043D", damageUpgradeCost, 0, false),
                new(ShopItemType.ShieldUnlock, "\u0429\u0438\u0442", 1000, 100, false),
                new(ShopItemType.ShieldUpgrade, "\u0429\u0438\u0442 +100", 500, 100, false),
                new(ShopItemType.MaxHealthUpgrade, "HP +100", 500, 100, false)
            };
        }

        private bool ShouldShowShopItem(PlayerResourceController resources, ShopItem item)
        {
            if (resources == null || item == null || !item.HideWhenUnavailable)
            {
                return true;
            }

            return item.ItemType switch
            {
                ShopItemType.ShieldUnlock => !resources.HasShield,
                ShopItemType.ShieldRestore => resources.HasShield,
                ShopItemType.ShieldUpgrade => resources.HasShield,
                ShopItemType.DamageUpgrade => !resources.HasDamageUpgrade,
                ShopItemType.ExtraDashCharge => !resources.HasExtraDashUpgrade,
                _ => true
            };
        }

        private static string FormatShopItemLabel(PlayerResourceController resources, ShopItem item)
        {
            var amount = item.Amount;
            var valueText = item.ItemType switch
            {
                ShopItemType.Ammo => $"+{Mathf.Max(1, amount)}",
                ShopItemType.Health => $"+{Mathf.Max(1, amount)} HP",
                ShopItemType.ShieldUnlock => $"+{Mathf.Max(1, amount)}",
                ShopItemType.ShieldUpgrade => $"+{Mathf.Max(1, amount)}",
                ShopItemType.MaxHealthUpgrade => $"+{Mathf.Max(1, amount)} HP",
                ShopItemType.FullHeal => resources != null ? $"{resources.Health:0}/{resources.MaxHealth:0} HP" : string.Empty,
                ShopItemType.ShieldRestore => resources != null ? $"{resources.Shield:0}/{resources.MaxShield:0}" : string.Empty,
                _ => string.Empty
            };

            return string.IsNullOrWhiteSpace(valueText)
                ? $"{item.GetDisplayName()} ({item.Cost})"
                : $"{item.GetDisplayName()} {valueText} ({item.Cost})";
        }

        private bool TryBuyShopItem(PlayerResourceController resources, ShopItem item)
        {
            if (resources == null || item == null)
            {
                return false;
            }

            var success = item.ItemType switch
            {
                ShopItemType.Ammo => TryBuyAmmo(resources, item.Cost, Mathf.Max(1, item.Amount)),
                ShopItemType.Health => TryBuyHealth(resources, item.Cost, Mathf.Max(1, item.Amount)),
                ShopItemType.FullHeal => TryBuyFullHeal(resources, item.Cost),
                ShopItemType.ShieldUnlock => TryBuyShieldUnlock(resources, item.Cost, Mathf.Max(1, item.Amount)),
                ShopItemType.ShieldRestore => TryBuyShieldRestore(resources, item.Cost),
                ShopItemType.ShieldUpgrade => TryBuyShieldUpgrade(resources, item.Cost, Mathf.Max(1, item.Amount)),
                ShopItemType.MaxHealthUpgrade => TryBuyMaxHealthUpgrade(resources, item.Cost, Mathf.Max(1, item.Amount)),
                ShopItemType.DamageUpgrade => TryBuyDamageUpgrade(resources, item.Cost),
                ShopItemType.ExtraDashCharge => TryBuyExtraDashUpgrade(resources, item.Cost),
                _ => false
            };

            if (success && ShouldRefreshShopAfterPurchase(item))
            {
                var title = ResolveShopKind() == ShopKind.Blacksmith
                    ? "\u041A\u0443\u0437\u043D\u0435\u0446"
                    : "\u0422\u043E\u0440\u0433\u043E\u0432\u0435\u0446";
                PortalUiRuntime.ShowChoice(title, BuildShopOptions(resources));
            }

            return success;
        }

        private static bool ShouldRefreshShopAfterPurchase(ShopItem item)
        {
            return item != null && item.ItemType is
                ShopItemType.ShieldUnlock or
                ShopItemType.DamageUpgrade or
                ShopItemType.ExtraDashCharge;
        }

        private bool TryBuyAmmo(PlayerResourceController resources, int cost, int amount)
        {
            if (resources == null)
            {
                return false;
            }

            if (resources.Ammo >= resources.MaxAmmo)
            {
                ShowShopFeedback("\u041F\u0430\u0442\u0440\u043E\u043D\u044B \u043F\u043E\u043B\u043D\u044B\u0435");
                return false;
            }

            if (!resources.TrySpendMoney(cost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            resources.AddAmmo(amount);
            ShowShopFeedback($"+{amount} \u043F\u0430\u0442\u0440\u043E\u043D");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{amount} ammo", Color.red, 0.16f);
            return true;
        }

        private bool TryBuyHealth(PlayerResourceController resources, int cost, int amount)
        {
            if (resources == null)
            {
                return false;
            }

            if (resources.Health >= resources.MaxHealth)
            {
                ShowShopFeedback("HP \u043F\u043E\u043B\u043D\u043E\u0435");
                return false;
            }

            if (!resources.TrySpendMoney(cost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            resources.AddHealth(amount);
            ShowShopFeedback($"+{amount} HP");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{amount} HP", Color.green, 0.16f);
            return true;
        }

        private bool TryBuyFullHeal(PlayerResourceController resources, int cost)
        {
            if (resources == null)
            {
                return false;
            }

            if (resources.Health >= resources.MaxHealth)
            {
                ShowShopFeedback("HP \u043F\u043E\u043B\u043D\u043E\u0435");
                return false;
            }

            if (!resources.TrySpendMoney(cost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            resources.FullHeal();
            ShowShopFeedback("\u041F\u043E\u043B\u043D\u043E\u0435 HP");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, "Full HP", Color.green, 0.18f);
            return true;
        }

        private bool TryBuyShieldUnlock(PlayerResourceController resources, int cost, int amount)
        {
            if (resources == null)
            {
                return false;
            }

            if (resources.HasShield)
            {
                ShowShopFeedback("\u0429\u0438\u0442 \u0443\u0436\u0435 \u043A\u0443\u043F\u043B\u0435\u043D");
                return false;
            }

            if (!resources.TryPurchaseShieldUnlock(cost, amount))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowShopFeedback($"+{amount} \u0449\u0438\u0442");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{amount} shield", new Color(0.35f, 0.75f, 1f), 0.18f);
            return true;
        }

        private bool TryBuyShieldRestore(PlayerResourceController resources, int cost)
        {
            if (resources == null || !resources.HasShield)
            {
                ShowShopFeedback("\u0429\u0438\u0442 \u043D\u0435 \u043A\u0443\u043F\u043B\u0435\u043D");
                return false;
            }

            if (resources.Shield >= resources.MaxShield)
            {
                ShowShopFeedback("\u0429\u0438\u0442 \u043F\u043E\u043B\u043D\u044B\u0439");
                return false;
            }

            if (!resources.TrySpendMoney(cost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            resources.RestoreShield();
            ShowShopFeedback("\u0429\u0438\u0442 \u0432\u043E\u0441\u0441\u0442.");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, "Shield full", new Color(0.35f, 0.75f, 1f), 0.18f);
            return true;
        }

        private bool TryBuyShieldUpgrade(PlayerResourceController resources, int cost, int amount)
        {
            if (resources == null || !resources.HasShield)
            {
                ShowShopFeedback("\u0429\u0438\u0442 \u043D\u0435 \u043A\u0443\u043F\u043B\u0435\u043D");
                return false;
            }

            if (!resources.TryPurchaseShieldUpgrade(cost, amount))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowShopFeedback($"+{amount} \u0449\u0438\u0442");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{amount} shield", new Color(0.35f, 0.75f, 1f), 0.18f);
            return true;
        }

        private bool TryBuyMaxHealthUpgrade(PlayerResourceController resources, int cost, int amount)
        {
            if (resources == null)
            {
                return false;
            }

            if (!resources.TryPurchaseHealthUpgrade(cost, amount))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowShopFeedback($"+{amount} max HP");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{amount} max HP", Color.green, 0.18f);
            return true;
        }

        private bool TryBuyDamageUpgrade(PlayerResourceController resources, int cost)
        {
            if (resources == null)
            {
                return false;
            }

            if (resources.HasDamageUpgrade)
            {
                ShowShopFeedback("\u0423\u0441\u0438\u043B\u0435\u043D\u0438\u0435 \u0443\u0436\u0435 \u043A\u0443\u043F\u043B\u0435\u043D\u043E");
                return false;
            }

            if (!resources.TryPurchaseDamageUpgrade(cost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowShopFeedback("x2 \u0443\u0440\u043E\u043D");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, "Damage x2", new Color(1f, 0.86f, 0.08f), 0.18f);
            return true;
        }

        private bool TryBuyExtraDashUpgrade(PlayerResourceController resources, int cost)
        {
            if (resources == null)
            {
                return false;
            }

            if (resources.HasExtraDashUpgrade)
            {
                ShowShopFeedback("\u0420\u044B\u0432\u043E\u043A \u0443\u0436\u0435 \u043A\u0443\u043F\u043B\u0435\u043D");
                return false;
            }

            if (!resources.TryPurchaseExtraDashUpgrade(cost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowShopFeedback("+1 \u0440\u044B\u0432\u043E\u043A");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, "+1 dash", new Color(0.18f, 0.75f, 1f), 0.18f);
            return true;
        }

        private void ShowShopFeedback(string message)
        {
            currentFeedbackPrompt = message;
            feedbackUntilTime = Time.time + feedbackDuration;
            PortalUiRuntime.ShowPrompt(message);
        }

        private IEnumerator DisappearAfterFeedback()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, pickupDisappearDelay));
            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private void ResolvePickupRewards(out int resolvedAmmoReward, out int resolvedMoneyReward)
        {
            resolvedAmmoReward = Mathf.Max(0, ammoReward);
            resolvedMoneyReward = Mathf.Max(0, moneyReward);
        }

        private bool IsContainerDropResource()
        {
            var objectName = name.ToLowerInvariant();
            return objectName.Contains("chest") || objectName.Contains("capsule");
        }

        private void SpawnContainerDrops(out int resolvedMoneyReward, out int resolvedAmmoReward, out int resolvedHealthReward)
        {
            var moneyCount = Mathf.Max(0, containerMoneyPickupCount);
            var moneyAmount = Mathf.Max(1, containerMoneyPerPickup);
            var ammoCount = Mathf.Max(0, containerAmmoPickupCount);
            var ammoAmount = Mathf.Max(1, containerAmmoPerPickup);
            var healthAmount = Mathf.Max(1, containerHealthAmount);
            var dropHealth = UnityEngine.Random.value < Mathf.Clamp01(containerHealthDropChance);
            var totalDrops = moneyCount + ammoCount + (dropHealth ? 1 : 0);
            var dropIndex = 0;
            var origin = GetDropOrigin();
            resolvedMoneyReward = 0;
            resolvedAmmoReward = 0;
            resolvedHealthReward = 0;

            for (var i = 0; i < moneyCount; i++)
            {
                var pickup = ResourcePickupCollectible.Spawn(
                    ResourcePickupCollectible.PickupKind.Money,
                    containerMoneyPickupPrefab,
                    moneyAmount,
                    origin,
                    ResolveContainerDropVelocity(dropIndex++, totalDrops));
                resolvedMoneyReward += pickup != null ? pickup.Amount : moneyAmount;
            }

            for (var i = 0; i < ammoCount; i++)
            {
                var pickup = ResourcePickupCollectible.Spawn(
                    ResourcePickupCollectible.PickupKind.Ammo,
                    containerAmmoPickupPrefab,
                    ammoAmount,
                    origin,
                    ResolveContainerDropVelocity(dropIndex++, totalDrops));
                resolvedAmmoReward += pickup != null ? pickup.Amount : ammoAmount;
            }

            if (dropHealth)
            {
                var pickup = ResourcePickupCollectible.Spawn(
                    ResourcePickupCollectible.PickupKind.Health,
                    containerHealthPickupPrefab,
                    healthAmount,
                    origin,
                    ResolveContainerDropVelocity(dropIndex, totalDrops));
                resolvedHealthReward += pickup != null ? pickup.Amount : healthAmount;
            }
        }

        private Vector3 GetDropOrigin()
        {
            return interactionTrigger != null
                ? interactionTrigger.bounds.center + Vector3.up * 0.35f
                : transform.position + Vector3.up * 1.1f;
        }

        private Vector3 ResolveContainerDropVelocity(int dropIndex, int totalDrops)
        {
            Vector2 planarDirection;
            if (totalDrops > 0)
            {
                var angle = ((Mathf.PI * 2f) / totalDrops) * dropIndex;
                planarDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                planarDirection = (planarDirection + UnityEngine.Random.insideUnitCircle * 0.35f).normalized;
            }
            else
            {
                planarDirection = UnityEngine.Random.insideUnitCircle.normalized;
            }

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                planarDirection = Vector2.right;
            }

            var launchSpeed = Mathf.Max(0f, containerDropLaunchSpeed);
            return new Vector3(planarDirection.x, UnityEngine.Random.Range(1.2f, 1.65f), planarDirection.y) * launchSpeed;
        }

        private ShopKind ResolveShopKind()
        {
            if (shopKind != ShopKind.Auto)
            {
                return shopKind;
            }

            return name.ToLowerInvariant().Contains("hammer") ? ShopKind.Blacksmith : ShopKind.Merchant;
        }

        private static string FormatRewardText(int money, int ammo, int health)
        {
            if (money > 0 && ammo > 0 && health > 0)
            {
                return $"+{money} gold  +{ammo} ammo  +{health} HP";
            }

            if (money > 0 && ammo > 0)
            {
                return $"+{money} gold  +{ammo} ammo";
            }

            if (money > 0 && health > 0)
            {
                return $"+{money} gold  +{health} HP";
            }

            if (ammo > 0 && health > 0)
            {
                return $"+{ammo} ammo  +{health} HP";
            }

            if (money > 0)
            {
                return $"+{money} gold";
            }

            if (ammo > 0)
            {
                return $"+{ammo} ammo";
            }

            if (health > 0)
            {
                return $"+{health} HP";
            }

            return "+0";
        }

        private void OnTriggerEnter(Collider other)
        {
            TrackInteractor(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TrackInteractor(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryResolveInteractor(other, out var interactor))
            {
                return;
            }

            touchingInteractors.Remove(interactor);
        }

        private void TrackInteractor(Collider other)
        {
            if (!TryResolveInteractor(other, out var interactor))
            {
                return;
            }

            touchingInteractors.Add(interactor);
        }

        private bool TryResolveInteractor(Collider other, out ScenePortalInteractionController interactor)
        {
            interactor = null;
            if (other == null)
            {
                return false;
            }

            interactor = other.GetComponentInParent<ScenePortalInteractionController>();
            return interactor != null && interactor.isActiveAndEnabled;
        }

        private void EnsureInteractionTrigger()
        {
            interactionTrigger = FindInteractionTrigger();
            if (interactionTrigger != null || !autoCreateInteractionTrigger)
            {
                SyncGeneratedTriggerSize();
                return;
            }

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = interactionRadius;
            interactionTrigger = trigger;
        }

        private Collider FindInteractionTrigger()
        {
            var colliders = GetComponents<Collider>();
            for (var index = 0; index < colliders.Length; index++)
            {
                var candidate = colliders[index];
                if (candidate != null && candidate.isTrigger)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void SyncGeneratedTriggerSize()
        {
            if (interactionTrigger is SphereCollider sphereCollider)
            {
                sphereCollider.radius = interactionRadius;
            }
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void NormalizeSettings()
        {
            interactionRadius = Mathf.Max(1f, interactionRadius);

            if (feedbackDuration <= 0f)
            {
                feedbackDuration = DefaultFeedbackDuration;
            }

            if (ammoPurchaseAmount <= 0)
            {
                ammoPurchaseAmount = DefaultAmmoPurchaseAmount;
            }

            if (ammoPurchaseCost <= 0)
            {
                ammoPurchaseCost = DefaultAmmoPurchaseCost;
            }

            if (healthPurchaseAmount <= 0)
            {
                healthPurchaseAmount = DefaultHealthPurchaseAmount;
            }

            if (healthPurchaseCost <= 0)
            {
                healthPurchaseCost = DefaultHealthPurchaseCost;
            }

            if (damageUpgradeCost <= 0)
            {
                damageUpgradeCost = DefaultDamageUpgradeCost;
            }

            if (pickupDisappearDelay <= 0f)
            {
                pickupDisappearDelay = DefaultPickupDisappearDelay;
            }

            if (containerMoneyPickupCount <= 0)
            {
                containerMoneyPickupCount = DefaultContainerMoneyPickupCount;
            }

            if (containerMoneyPerPickup <= 0)
            {
                containerMoneyPerPickup = DefaultContainerMoneyPerPickup;
            }

            if (containerAmmoPickupCount <= 0)
            {
                containerAmmoPickupCount = DefaultContainerAmmoPickupCount;
            }

            if (containerAmmoPerPickup <= 0)
            {
                containerAmmoPerPickup = DefaultContainerAmmoPerPickup;
            }

            containerHealthDropChance = Mathf.Clamp01(containerHealthDropChance);
            if (containerHealthDropChance <= 0f)
            {
                containerHealthDropChance = DefaultContainerHealthDropChance;
            }

            if (containerHealthAmount <= 0)
            {
                containerHealthAmount = DefaultContainerHealthAmount;
            }

            if (containerDropLaunchSpeed <= 0f)
            {
                containerDropLaunchSpeed = DefaultContainerDropLaunchSpeed;
            }
        }
    }
}
