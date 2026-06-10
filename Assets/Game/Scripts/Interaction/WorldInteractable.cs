using System.Collections.Generic;
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
            AmmoPickup = 1
        }

        private static readonly List<WorldInteractable> RegisteredInteractables = new();

        [SerializeField] private InteractionMode mode = InteractionMode.Purchase;
        [SerializeField, Min(1f)] private float interactionRadius = 7f;
        [SerializeField] private string interactionPrompt = "Buy: press E";
        [SerializeField] private string completedPrompt = "Bought";
        [SerializeField, Min(0)] private int ammoReward;
        [SerializeField] private bool oneShot;
        [SerializeField] private bool disableMinimapMarkerOnComplete = true;
        [SerializeField] private bool autoCreateInteractionTrigger = true;
        [SerializeField, Min(0f)] private float feedbackDuration = 1.2f;

        private readonly HashSet<ScenePortalInteractionController> touchingInteractors = new();
        private Collider interactionTrigger;
        private MinimapTrackable minimapTrackable;
        private bool isCompleted;
        private float feedbackUntilTime;

        public static IReadOnlyList<WorldInteractable> ActiveInteractables => RegisteredInteractables;
        public bool IsAvailable => isActiveAndEnabled && (!oneShot || !isCompleted);

        private void Awake()
        {
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
                return completedPrompt;
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
                case InteractionMode.AmmoPickup:
                    ResolveAmmoPickup(interactor);
                    break;
                default:
                    feedbackUntilTime = Time.time + feedbackDuration;
                    break;
            }
        }

        private void ResolveAmmoPickup(ScenePortalInteractionController interactor)
        {
            var resources = interactor != null
                ? interactor.GetComponent<PlayerResourceController>()
                : null;

            if (resources != null)
            {
                resources.AddAmmo(ammoReward);
            }

            isCompleted = true;
            feedbackUntilTime = Time.time + feedbackDuration;

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
            interactionRadius = Mathf.Max(1f, interactionRadius);
            feedbackDuration = Mathf.Max(0f, feedbackDuration);
        }
    }
}
