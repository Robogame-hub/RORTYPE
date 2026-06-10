using System;
using System.Collections.Generic;
using RorType.Gameplay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ScenePortal : MonoBehaviour
    {
        [Serializable]
        public struct Destination
        {
            public string buttonLabel;
            public string sceneName;

            public Destination(string buttonLabel, string sceneName)
            {
                this.buttonLabel = buttonLabel;
                this.sceneName = sceneName;
            }
        }

        private static readonly List<ScenePortal> RegisteredPortals = new();
        [SerializeField, Min(1f)] private float interactionRadius = 7f;
        [SerializeField] private string interactionPrompt = "\u041d\u0430\u0436\u043c\u0438\u0442\u0435 E \u0434\u043b\u044f \u043f\u0435\u0440\u0435\u0445\u043e\u0434\u0430";
        [SerializeField] private bool autoCreateInteractionTrigger = true;
        [SerializeField] private Destination[] destinations = Array.Empty<Destination>();
        [SerializeField] private Color idleSphereColor = new(0.2f, 0.55f, 1f, 0.28f);
        [SerializeField] private Color activeSphereColor = new(0.35f, 1f, 0.72f, 0.58f);

        public static IReadOnlyList<ScenePortal> ActivePortals => RegisteredPortals;
        public float InteractionRadius => interactionRadius;

        private readonly List<Renderer> sphereRenderers = new();
        private readonly HashSet<ScenePortalInteractionController> touchingInteractors = new();
        private MaterialPropertyBlock propertyBlock;
        private Collider interactionTrigger;
        private bool isHighlighted;

        private void Awake()
        {
            EnsureInteractionTrigger();
            EnsurePropertyBlock();
            CacheSphereRenderers();
            ApplyHighlightState(force: true);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!RegisteredPortals.Contains(this))
            {
                RegisteredPortals.Add(this);
            }

            ApplyHighlightState(force: true);
        }

        private void OnDisable()
        {
            RegisteredPortals.Remove(this);
            touchingInteractors.Clear();
            SetHighlighted(false, true);
        }

        public bool IsWithinInteractionRange(Vector3 worldPosition)
        {
            return (transform.position - worldPosition).sqrMagnitude <= interactionRadius * interactionRadius;
        }

        public float GetSqrDistanceTo(Vector3 worldPosition)
        {
            return (transform.position - worldPosition).sqrMagnitude;
        }

        public bool IsTouchedBy(ScenePortalInteractionController interactor)
        {
            return interactor != null && touchingInteractors.Contains(interactor);
        }

        public string GetInteractionPrompt()
        {
            return interactionPrompt;
        }

        public void SetHighlighted(bool highlighted)
        {
            SetHighlighted(highlighted, false);
        }

        public void Interact()
        {
            var destinations = GetResolvedDestinations();
            if (destinations.Count == 0)
            {
                Debug.LogWarning($"Portal '{name}' in scene '{gameObject.scene.name}' has no valid destinations.");
                return;
            }

            if (destinations.Count == 1)
            {
                TravelToDestination(destinations[0]);
                return;
            }

            var choiceOptions = new List<PortalUiRuntime.ChoiceOption>(destinations.Count);
            for (var index = 0; index < destinations.Count; index++)
            {
                var destination = destinations[index];
                choiceOptions.Add(new PortalUiRuntime.ChoiceOption(
                    GetDestinationLabel(destination),
                    () => TravelToDestination(destination)));
            }

            PortalUiRuntime.ShowChoice(
                $"{gameObject.scene.name} Portal",
                choiceOptions);
        }

        private IReadOnlyList<Destination> GetResolvedDestinations()
        {
            if (destinations == null || destinations.Length == 0)
            {
                return Array.Empty<Destination>();
            }

            return destinations;
        }

        private static void TravelToDestination(Destination destination)
        {
            PortalUiRuntime.HideChoice();
            PortalUiRuntime.HidePrompt();

            if (string.IsNullOrWhiteSpace(destination.sceneName))
            {
                Debug.LogWarning("Portal destination has an empty scene name.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(destination.sceneName))
            {
                Debug.LogWarning($"Portal destination scene '{destination.sceneName}' is not available in build settings.");
                return;
            }

            SceneManager.LoadScene(destination.sceneName);
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

        private static string GetDestinationLabel(Destination destination)
        {
            return string.IsNullOrWhiteSpace(destination.buttonLabel)
                ? destination.sceneName
                : destination.buttonLabel;
        }

        private void SetHighlighted(bool highlighted, bool force)
        {
            if (!force && isHighlighted == highlighted)
            {
                return;
            }

            isHighlighted = highlighted;
            ApplyHighlightState(force);
        }

        private void ApplyHighlightState(bool force)
        {
            if (sphereRenderers.Count == 0)
            {
                CacheSphereRenderers();
            }

            if (sphereRenderers.Count == 0)
            {
                return;
            }

            var targetColor = isHighlighted ? activeSphereColor : idleSphereColor;
            if (!TryGetPropertyBlock(out var block))
            {
                return;
            }

            for (var index = sphereRenderers.Count - 1; index >= 0; index--)
            {
                var renderer = sphereRenderers[index];
                if (renderer == null)
                {
                    sphereRenderers.RemoveAt(index);
                    continue;
                }

                if (!force && (!renderer.enabled || !renderer.gameObject.activeInHierarchy))
                {
                    continue;
                }

                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", targetColor);
                block.SetColor("_Color", targetColor);
                renderer.SetPropertyBlock(block);
            }
        }

        private void EnsurePropertyBlock()
        {
            TryGetPropertyBlock(out _);
        }

        private bool TryGetPropertyBlock(out MaterialPropertyBlock block)
        {
            if (propertyBlock == null)
            {
                if (!Application.isPlaying)
                {
                    block = null;
                    return false;
                }

                propertyBlock = new MaterialPropertyBlock();
            }

            block = propertyBlock;
            return true;
        }

        private void CacheSphereRenderers()
        {
            sphereRenderers.Clear();

            var childRenderers = GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < childRenderers.Length; index++)
            {
                var candidate = childRenderers[index];
                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.transform.name.Contains("Sphere", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sphereRenderers.Add(candidate);
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CacheSphereRenderers();
                ApplyHighlightState(force: true);
            }
        }
    }
}
