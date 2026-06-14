using System;
using System.Collections.Generic;
using RorType.Gameplay.Player;
using RorType.Gameplay.UI;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ShopInteractable : MonoBehaviour
    {
        public enum ShopKind
        {
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
        public sealed class ShopItem
        {
            [SerializeField] private ShopItemType itemType;
            [SerializeField] private string displayName;
            [SerializeField, Min(0)] private int cost = 1;
            [SerializeField, Min(0)] private int amount = 1;
            [SerializeField] private bool hideWhenUnavailable = true;

            public ShopItemType ItemType => itemType;
            public int Cost => Mathf.Max(0, cost);
            public int Amount => Mathf.Max(0, amount);
            public bool HideWhenUnavailable => hideWhenUnavailable;

            public ShopItem(ShopItemType itemType, string displayName, int cost, int amount, bool hideWhenUnavailable = true)
            {
                this.itemType = itemType;
                this.displayName = displayName;
                this.cost = cost;
                this.amount = amount;
                this.hideWhenUnavailable = hideWhenUnavailable;
            }

            public string GetDisplayName()
            {
                return string.IsNullOrWhiteSpace(displayName) ? ResolveDefaultName(itemType) : displayName;
            }

            private static string ResolveDefaultName(ShopItemType itemType)
            {
                return itemType switch
                {
                    ShopItemType.Ammo => "\u041F\u0430\u0442\u0440\u043E\u043D\u044B",
                    ShopItemType.Health => "\u041B\u0435\u0447\u0435\u043D\u0438\u0435",
                    ShopItemType.FullHeal => "\u041F\u043E\u043B\u043D\u043E\u0435 \u043B\u0435\u0447\u0435\u043D\u0438\u0435",
                    ShopItemType.ShieldUnlock => "\u0429\u0438\u0442",
                    ShopItemType.ShieldRestore => "\u0412\u043E\u0441\u0441\u0442. \u0449\u0438\u0442",
                    ShopItemType.ShieldUpgrade => "\u0429\u0438\u0442 +100",
                    ShopItemType.MaxHealthUpgrade => "HP +100",
                    ShopItemType.DamageUpgrade => "\u0423\u0440\u043E\u043D x2",
                    ShopItemType.ExtraDashCharge => "\u0414\u043E\u043F. \u0440\u044B\u0432\u043E\u043A",
                    _ => "\u0422\u043E\u0432\u0430\u0440"
                };
            }
        }

        private static readonly List<ShopInteractable> RegisteredShops = new();

        [SerializeField] private ShopKind shopKind;
        [SerializeField, Min(1f)] private float interactionRadius = 7f;
        [SerializeField] private string interactionPrompt = "\u041C\u0430\u0433\u0430\u0437\u0438\u043D: \u043D\u0430\u0436\u043C\u0438\u0442\u0435 E";
        [SerializeField] private ShopUiPanel shopUi;
        [SerializeField] private List<ShopItem> shopItems = new();

        private readonly HashSet<ScenePortalInteractionController> touchingInteractors = new();
        private Collider interactionTrigger;

        public static IReadOnlyList<ShopInteractable> ActiveShops => RegisteredShops;
        public bool IsAvailable => isActiveAndEnabled;

        private void Awake()
        {
            EnsureDefaults();
            EnsureInteractionTrigger();
        }

        private void OnEnable()
        {
            if (!RegisteredShops.Contains(this))
            {
                RegisteredShops.Add(this);
            }
        }

        private void OnDisable()
        {
            RegisteredShops.Remove(this);
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
            return interactionPrompt;
        }

        public void Interact(ScenePortalInteractionController interactor)
        {
            var resources = interactor != null ? interactor.GetComponent<PlayerResourceController>() : null;
            if (resources == null)
            {
                ShowFeedback("\u041D\u0435\u0442 \u0438\u0433\u0440\u043E\u043A\u0430");
                return;
            }

            if (shopUi == null)
            {
                shopUi = ShopUiPanel.ResolveDefault();
            }

            if (shopUi == null)
            {
                Debug.LogWarning($"Shop '{name}' cannot open because the scene has no authored ShopUiPanel.", this);
                return;
            }

            var entries = BuildEntries(resources);
            if (entries.Count == 0)
            {
                ShowFeedback("\u041D\u0435\u0442 \u0442\u043E\u0432\u0430\u0440\u043E\u0432");
                return;
            }

            shopUi.Show(ResolveTitle(), entries);
        }

        private List<ShopUiPanel.Entry> BuildEntries(PlayerResourceController resources)
        {
            var entries = new List<ShopUiPanel.Entry>();
            var items = ResolveShopItems();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !ShouldShow(resources, item))
                {
                    continue;
                }

                entries.Add(new ShopUiPanel.Entry(
                    item.GetDisplayName(),
                    ResolveIcon(item.ItemType),
                    ResolveDetail(resources, item),
                    $"{item.Cost}G",
                    ResolveHint(resources, item),
                    () => TryBuy(resources, item)));
            }

            return entries;
        }

        private IReadOnlyList<ShopItem> ResolveShopItems()
        {
            if (shopItems != null && shopItems.Count > 0)
            {
                return shopItems;
            }

            return shopKind == ShopKind.Blacksmith
                ? CreateBlacksmithDefaults()
                : CreateMerchantDefaults();
        }

        private static List<ShopItem> CreateMerchantDefaults()
        {
            return new List<ShopItem>
            {
                new(ShopItemType.Ammo, "\u041F\u0430\u0442\u0440\u043E\u043D\u044B", 10, 10, false),
                new(ShopItemType.Health, "\u041B\u0435\u0447\u0435\u043D\u0438\u0435", 20, 20, false),
                new(ShopItemType.FullHeal, "\u041F\u043E\u043B\u043D\u043E\u0435 \u043B\u0435\u0447\u0435\u043D\u0438\u0435", 500, 0, false),
                new(ShopItemType.ShieldUnlock, "\u0414\u043E\u043F. \u0449\u0438\u0442", 100, 100),
                new(ShopItemType.ShieldRestore, "\u0412\u043E\u0441\u0441\u0442. \u0449\u0438\u0442", 100, 0)
            };
        }

        private static List<ShopItem> CreateBlacksmithDefaults()
        {
            return new List<ShopItem>
            {
                new(ShopItemType.Ammo, "\u041F\u0430\u0442\u0440\u043E\u043D\u044B", 10, 10, false),
                new(ShopItemType.ExtraDashCharge, "\u0414\u043E\u043F. \u0440\u044B\u0432\u043E\u043A", 100, 1),
                new(ShopItemType.DamageUpgrade, "\u0423\u0440\u043E\u043D x2", 100, 0),
                new(ShopItemType.ShieldUnlock, "\u0429\u0438\u0442", 100, 100),
                new(ShopItemType.ShieldUpgrade, "\u0429\u0438\u0442 +100", 500, 100),
                new(ShopItemType.MaxHealthUpgrade, "HP +100", 500, 100)
            };
        }

        private bool TryBuy(PlayerResourceController resources, ShopItem item)
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

            if (success && ShouldRefreshAfterPurchase(item))
            {
                shopUi.Show(ResolveTitle(), BuildEntries(resources));
            }

            return success;
        }

        private bool TryBuyAmmo(PlayerResourceController resources, int cost, int amount)
        {
            if (resources.Ammo >= resources.MaxAmmo)
            {
                ShowFeedback("\u041F\u0430\u0442\u0440\u043E\u043D\u044B \u043F\u043E\u043B\u043D\u044B\u0435");
                return false;
            }

            if (!Spend(resources, cost))
            {
                return false;
            }

            resources.AddAmmo(amount);
            ShowFeedback($"+{amount} \u043F\u0430\u0442\u0440\u043E\u043D");
            return true;
        }

        private bool TryBuyHealth(PlayerResourceController resources, int cost, int amount)
        {
            if (resources.Health >= resources.MaxHealth)
            {
                ShowFeedback("HP \u043F\u043E\u043B\u043D\u043E\u0435");
                return false;
            }

            if (!Spend(resources, cost))
            {
                return false;
            }

            resources.AddHealth(amount);
            ShowFeedback($"+{amount} HP");
            return true;
        }

        private bool TryBuyFullHeal(PlayerResourceController resources, int cost)
        {
            if (resources.Health >= resources.MaxHealth)
            {
                ShowFeedback("HP \u043F\u043E\u043B\u043D\u043E\u0435");
                return false;
            }

            if (!Spend(resources, cost))
            {
                return false;
            }

            resources.FullHeal();
            ShowFeedback("\u041F\u043E\u043B\u043D\u043E\u0435 HP");
            return true;
        }

        private bool TryBuyShieldUnlock(PlayerResourceController resources, int cost, int amount)
        {
            if (!resources.TryPurchaseShieldUnlock(cost, amount))
            {
                ShowFeedback(resources.HasShield ? "\u0429\u0438\u0442 \u0443\u0436\u0435 \u043A\u0443\u043F\u043B\u0435\u043D" : "\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowFeedback($"+{amount} \u0449\u0438\u0442");
            return true;
        }

        private bool TryBuyShieldRestore(PlayerResourceController resources, int cost)
        {
            if (!resources.HasShield)
            {
                ShowFeedback("\u0429\u0438\u0442 \u043D\u0435 \u043A\u0443\u043F\u043B\u0435\u043D");
                return false;
            }

            if (resources.Shield >= resources.MaxShield)
            {
                ShowFeedback("\u0429\u0438\u0442 \u043F\u043E\u043B\u043D\u044B\u0439");
                return false;
            }

            if (!Spend(resources, cost))
            {
                return false;
            }

            resources.RestoreShield();
            ShowFeedback("\u0429\u0438\u0442 \u0432\u043E\u0441\u0441\u0442.");
            return true;
        }

        private bool TryBuyShieldUpgrade(PlayerResourceController resources, int cost, int amount)
        {
            if (!resources.TryPurchaseShieldUpgrade(cost, amount))
            {
                ShowFeedback(resources.HasShield ? "\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430" : "\u0429\u0438\u0442 \u043D\u0435 \u043A\u0443\u043F\u043B\u0435\u043D");
                return false;
            }

            ShowFeedback($"+{amount} \u0449\u0438\u0442");
            return true;
        }

        private bool TryBuyMaxHealthUpgrade(PlayerResourceController resources, int cost, int amount)
        {
            if (!resources.TryPurchaseHealthUpgrade(cost, amount))
            {
                ShowFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowFeedback($"+{amount} max HP");
            return true;
        }

        private bool TryBuyDamageUpgrade(PlayerResourceController resources, int cost)
        {
            if (!resources.TryPurchaseDamageUpgrade(cost))
            {
                ShowFeedback(resources.HasDamageUpgrade ? "\u0423\u0440\u043E\u043D \u0443\u0436\u0435 \u0443\u0441\u0438\u043B\u0435\u043D" : "\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowFeedback("x2 \u0443\u0440\u043E\u043D");
            return true;
        }

        private bool TryBuyExtraDashUpgrade(PlayerResourceController resources, int cost)
        {
            if (!resources.TryPurchaseExtraDashUpgrade(cost))
            {
                ShowFeedback(resources.HasExtraDashUpgrade ? "\u0420\u044B\u0432\u043E\u043A \u0443\u0436\u0435 \u043A\u0443\u043F\u043B\u0435\u043D" : "\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return false;
            }

            ShowFeedback("+1 \u0440\u044B\u0432\u043E\u043A");
            return true;
        }

        private bool Spend(PlayerResourceController resources, int cost)
        {
            if (resources.TrySpendMoney(cost))
            {
                return true;
            }

            ShowFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
            return false;
        }

        private void ShowFeedback(string message)
        {
            if (shopUi != null)
            {
                shopUi.SetFeedback(message);
            }
        }

        private static bool ShouldRefreshAfterPurchase(ShopItem item)
        {
            return item != null && (item.HideWhenUnavailable || item.ItemType is
                ShopItemType.ShieldUnlock or
                ShopItemType.DamageUpgrade or
                ShopItemType.ExtraDashCharge);
        }

        private static bool ShouldShow(PlayerResourceController resources, ShopItem item)
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

        private string ResolveTitle()
        {
            return shopKind == ShopKind.Blacksmith ? "\u041A\u0443\u0437\u043D\u0435\u0446" : "\u0422\u043E\u0440\u0433\u043E\u0432\u0435\u0446";
        }

        private static string ResolveIcon(ShopItemType itemType)
        {
            return itemType switch
            {
                ShopItemType.Ammo => "|||",
                ShopItemType.Health => "+",
                ShopItemType.FullHeal => "++",
                ShopItemType.ShieldUnlock => "[]",
                ShopItemType.ShieldRestore => "[+]",
                ShopItemType.ShieldUpgrade => "[++]",
                ShopItemType.MaxHealthUpgrade => "HP+",
                ShopItemType.DamageUpgrade => "x2",
                ShopItemType.ExtraDashCharge => ">>",
                _ => "?"
            };
        }

        private static string ResolveDetail(PlayerResourceController resources, ShopItem item)
        {
            var amount = item.Amount;
            return item.ItemType switch
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
        }

        private static string ResolveHint(PlayerResourceController resources, ShopItem item)
        {
            var amount = Mathf.Max(1, item.Amount);
            return item.ItemType switch
            {
                ShopItemType.Ammo => $"\u041F\u0430\u0442\u0440\u043E\u043D\u044B: +{amount} \u043A \u0437\u0430\u043F\u0430\u0441\u0443 \u0432\u044B\u0441\u0442\u0440\u0435\u043B\u043E\u0432.",
                ShopItemType.Health => $"\u041B\u0435\u0447\u0435\u043D\u0438\u0435: +{amount} HP, \u043D\u0435 \u0432\u044B\u0448\u0435 \u043C\u0430\u043A\u0441\u0438\u043C\u0443\u043C\u0430.",
                ShopItemType.FullHeal => resources != null ? $"\u041F\u043E\u043B\u043D\u043E\u0435 \u043B\u0435\u0447\u0435\u043D\u0438\u0435: HP \u0434\u043E {resources.MaxHealth:0}." : "\u041F\u043E\u043B\u043D\u043E\u0435 \u043B\u0435\u0447\u0435\u043D\u0438\u0435.",
                ShopItemType.ShieldUnlock => $"\u0429\u0438\u0442: \u043E\u0442\u043A\u0440\u044B\u0432\u0430\u0435\u0442 {amount} \u0449\u0438\u0442\u0430.",
                ShopItemType.ShieldRestore => "\u0412\u043E\u0441\u0441\u0442. \u0449\u0438\u0442: \u0437\u0430\u043F\u043E\u043B\u043D\u044F\u0435\u0442 \u0449\u0438\u0442.",
                ShopItemType.ShieldUpgrade => $"\u0429\u0438\u0442 +{amount}: \u0443\u0432\u0435\u043B\u0438\u0447\u0438\u0432\u0430\u0435\u0442 \u043C\u0430\u043A\u0441. \u0449\u0438\u0442.",
                ShopItemType.MaxHealthUpgrade => $"HP +{amount}: \u0443\u0432\u0435\u043B\u0438\u0447\u0438\u0432\u0430\u0435\u0442 \u043C\u0430\u043A\u0441. HP.",
                ShopItemType.DamageUpgrade => "\u0423\u0440\u043E\u043D x2: \u043E\u0434\u043D\u043E\u0440\u0430\u0437\u043E\u0432\u043E\u0435 \u0443\u0441\u0438\u043B\u0435\u043D\u0438\u0435 \u0430\u0442\u0430\u043A.",
                ShopItemType.ExtraDashCharge => "\u0414\u043E\u043F. \u0440\u044B\u0432\u043E\u043A: +1 \u0437\u0430\u0440\u044F\u0434 \u0440\u044B\u0432\u043A\u0430.",
                _ => item.GetDisplayName()
            };
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
            if (TryResolveInteractor(other, out var interactor))
            {
                touchingInteractors.Remove(interactor);
            }
        }

        private void TrackInteractor(Collider other)
        {
            if (TryResolveInteractor(other, out var interactor))
            {
                touchingInteractors.Add(interactor);
            }
        }

        private static bool TryResolveInteractor(Collider other, out ScenePortalInteractionController interactor)
        {
            interactor = other != null ? other.GetComponentInParent<ScenePortalInteractionController>() : null;
            return interactor != null && interactor.isActiveAndEnabled;
        }

        private void EnsureInteractionTrigger()
        {
            interactionTrigger = FindInteractionTrigger();
            if (interactionTrigger == null)
            {
                Debug.LogWarning($"Shop '{name}' has no authored trigger collider. Add a SphereCollider trigger on the shop prefab or scene object.", this);
                return;
            }

            SyncAuthoredTriggerSize();
        }

        private Collider FindInteractionTrigger()
        {
            var colliders = GetComponents<Collider>();
            for (var i = 0; i < colliders.Length; i++)
            {
                var candidate = colliders[i];
                if (candidate != null && candidate.isTrigger)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void SyncAuthoredTriggerSize()
        {
            if (interactionTrigger is SphereCollider sphereCollider)
            {
                sphereCollider.radius = interactionRadius;
            }
        }

        private void OnValidate()
        {
            EnsureDefaults();
        }

        private void EnsureDefaults()
        {
            interactionRadius = Mathf.Max(1f, interactionRadius);
            if (shopItems == null)
            {
                shopItems = new List<ShopItem>();
            }
        }
    }
}
