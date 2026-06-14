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
            LegacyPurchase = 0,
            ResourcePickup = 1
        }

        private static readonly List<WorldInteractable> RegisteredInteractables = new();

        [SerializeField] private InteractionMode mode = InteractionMode.ResourcePickup;
        [SerializeField, Min(1f)] private float interactionRadius = 7f;
        [SerializeField] private string interactionPrompt = "Open: press E";
        [SerializeField] private string completedPrompt = "Opened";
        [SerializeField, Min(0)] private int ammoReward;
        [SerializeField, Min(0)] private int moneyReward;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private bool disableMinimapMarkerOnComplete = true;
        [SerializeField] private bool autoCreateInteractionTrigger = true;
        [SerializeField, Min(0f)] private float feedbackDuration = 1.2f;
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
        public bool IsAvailable => isActiveAndEnabled && mode == InteractionMode.ResourcePickup && (!oneShot || !isCompleted);

        private void Awake()
        {
            NormalizeSettings();
            minimapTrackable = GetComponent<MinimapTrackable>();
            EnsureInteractionTrigger();
        }

        private void OnEnable()
        {
            if (mode != InteractionMode.ResourcePickup)
            {
                return;
            }

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
            return mode == InteractionMode.ResourcePickup && interactor != null && touchingInteractors.Contains(interactor);
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

            ResolveResourcePickup(interactor);
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

        private static string FormatRewardText(int money, int ammo, int health)
        {
            if (money > 0 && ammo > 0 && health > 0)
            {
                return $"+{money}G  +{ammo} ammo  +{health} HP";
            }

            if (money > 0 && ammo > 0)
            {
                return $"+{money}G  +{ammo} ammo";
            }

            if (money > 0 && health > 0)
            {
                return $"+{money}G  +{health} HP";
            }

            if (ammo > 0 && health > 0)
            {
                return $"+{ammo} ammo  +{health} HP";
            }

            if (money > 0)
            {
                return $"+{money}G";
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
            if (interactionTrigger != null)
            {
                SyncAuthoredTriggerSize();
                return;
            }

            if (!autoCreateInteractionTrigger)
            {
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

        private void SyncAuthoredTriggerSize()
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
