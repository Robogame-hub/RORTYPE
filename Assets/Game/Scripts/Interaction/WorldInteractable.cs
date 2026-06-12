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

            if (ResolveShopKind() == ShopKind.Blacksmith)
            {
                PortalUiRuntime.ShowChoice(
                    "\u041A\u0443\u0437\u043D\u0435\u0446",
                    new[]
                    {
                        new PortalUiRuntime.ChoiceOption(
                            $"\u041A\u0443\u043F\u0438\u0442\u044C \u043F\u0430\u0442\u0440\u043E\u043D\u044B ({ammoPurchaseCost}: {ammoPurchaseAmount})",
                            () => TryBuyAmmo(resources)),
                        new PortalUiRuntime.ChoiceOption(
                            resources.HasDamageUpgrade
                                ? "\u0423\u0440\u043E\u043D \u0443\u0436\u0435 \u0443\u0441\u0438\u043B\u0435\u043D"
                                : $"\u0423\u0441\u0438\u043B\u0438\u0442\u044C \u0443\u0440\u043E\u043D ({damageUpgradeCost})",
                            () => TryBuyDamageUpgrade(resources))
                    });
                return;
            }

            PortalUiRuntime.ShowChoice(
                "\u0422\u043E\u0440\u0433\u043E\u0432\u0435\u0446",
                new[]
                {
                    new PortalUiRuntime.ChoiceOption(
                        $"\u041A\u0443\u043F\u0438\u0442\u044C \u043F\u0430\u0442\u0440\u043E\u043D\u044B ({ammoPurchaseCost}: {ammoPurchaseAmount})",
                        () => TryBuyAmmo(resources)),
                    new PortalUiRuntime.ChoiceOption(
                        $"\u041A\u0443\u043F\u0438\u0442\u044C \u0437\u0434\u043E\u0440\u043E\u0432\u044C\u0435 ({healthPurchaseCost}: {healthPurchaseAmount})",
                        () => TryBuyHealth(resources))
                });
        }

        private void TryBuyAmmo(PlayerResourceController resources)
        {
            if (resources == null)
            {
                return;
            }

            if (!resources.TrySpendMoney(ammoPurchaseCost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return;
            }

            resources.AddAmmo(ammoPurchaseAmount);
            ShowShopFeedback($"+{ammoPurchaseAmount} \u043F\u0430\u0442\u0440\u043E\u043D");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{ammoPurchaseAmount} ammo", Color.red, 0.16f);
        }

        private void TryBuyHealth(PlayerResourceController resources)
        {
            if (resources == null)
            {
                return;
            }

            if (!resources.TrySpendMoney(healthPurchaseCost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return;
            }

            resources.AddHealth(healthPurchaseAmount);
            ShowShopFeedback($"+{healthPurchaseAmount} HP");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, $"+{healthPurchaseAmount} HP", Color.green, 0.16f);
        }

        private void TryBuyDamageUpgrade(PlayerResourceController resources)
        {
            if (resources == null)
            {
                return;
            }

            if (resources.HasDamageUpgrade)
            {
                ShowShopFeedback("\u0423\u0441\u0438\u043B\u0435\u043D\u0438\u0435 \u0443\u0436\u0435 \u043A\u0443\u043F\u043B\u0435\u043D\u043E");
                return;
            }

            if (!resources.TryPurchaseDamageUpgrade(damageUpgradeCost))
            {
                ShowShopFeedback("\u041D\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0437\u043E\u043B\u043E\u0442\u0430");
                return;
            }

            ShowShopFeedback("x2 \u0443\u0440\u043E\u043D");
            FloatingWorldText.Spawn(transform.position + Vector3.up * 2.2f, "Damage x2", new Color(1f, 0.86f, 0.08f), 0.18f);
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
            var dropHealth = Random.value < Mathf.Clamp01(containerHealthDropChance);
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
                planarDirection = (planarDirection + Random.insideUnitCircle * 0.35f).normalized;
            }
            else
            {
                planarDirection = Random.insideUnitCircle.normalized;
            }

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                planarDirection = Vector2.right;
            }

            var launchSpeed = Mathf.Max(0f, containerDropLaunchSpeed);
            return new Vector3(planarDirection.x, Random.Range(1.2f, 1.65f), planarDirection.y) * launchSpeed;
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
