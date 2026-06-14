using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TotemPedestal : MonoBehaviour
    {
        private static readonly List<TotemPedestal> RegisteredPedestals = new();

        [SerializeField] private TotemDoorController controller;
        [SerializeField] private Transform totemSlot;
        [SerializeField] private Renderer[] indicatorRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light[] indicatorLights = System.Array.Empty<Light>();
        [SerializeField] private string installPrompt = "\u041D\u0430\u0436\u043C\u0438\u0442\u0435 E \u0447\u0442\u043E\u0431\u044B \u0443\u0441\u0442\u0430\u043D\u043E\u0432\u0438\u0442\u044C \u0442\u043E\u0442\u0435\u043C";
        [SerializeField] private string missingTotemPrompt = "\u041D\u0443\u0436\u0435\u043D \u0442\u043E\u0442\u0435\u043C";
        [SerializeField] private string completedPrompt = "\u0422\u043E\u0442\u0435\u043C \u0443\u0441\u0442\u0430\u043D\u043E\u0432\u043B\u0435\u043D";
        [SerializeField] private Color idleColor = new(1f, 0.08f, 0.05f, 1f);
        [SerializeField] private Color filledColor = new(0.08f, 1f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float emissionIntensity = 1.5f;

        private readonly HashSet<ScenePortalInteractionController> touchingInteractors = new();
        private MaterialPropertyBlock propertyBlock;
        private bool hasTotem;

        public static IReadOnlyList<TotemPedestal> ActivePedestals => RegisteredPedestals;
        public bool IsAvailable => isActiveAndEnabled && !hasTotem;
        public bool HasTotem => hasTotem;

        private void Awake()
        {
            EnsureTriggerCollider();
            EnsureReferences();
            ApplyIndicatorState(force: true);
        }

        private void OnEnable()
        {
            if (!RegisteredPedestals.Contains(this))
            {
                RegisteredPedestals.Add(this);
            }
        }

        private void OnDisable()
        {
            RegisteredPedestals.Remove(this);
            touchingInteractors.Clear();
        }

        public void Bind(TotemDoorController owner)
        {
            controller = owner;
        }

        public bool IsTouchedBy(ScenePortalInteractionController interactor)
        {
            return IsAvailable && interactor != null && touchingInteractors.Contains(interactor);
        }

        public float GetSqrDistanceTo(Vector3 worldPosition)
        {
            return (transform.position - worldPosition).sqrMagnitude;
        }

        public string GetInteractionPrompt(ScenePortalInteractionController interactor)
        {
            if (hasTotem)
            {
                return completedPrompt;
            }

            var carrier = interactor != null ? interactor.GetComponent<TotemCarrier>() : null;
            return carrier != null && carrier.CarriedCount > 0 ? installPrompt : missingTotemPrompt;
        }

        public void Interact(ScenePortalInteractionController interactor)
        {
            if (hasTotem || interactor == null)
            {
                return;
            }

            var carrier = interactor.GetComponent<TotemCarrier>();
            var totem = carrier != null ? carrier.RemoveForPlacement() : null;
            if (totem == null)
            {
                return;
            }

            EnsureReferences();
            hasTotem = true;
            touchingInteractors.Clear();
            totem.PlaceOn(totemSlot != null ? totemSlot : transform);
            ApplyIndicatorState(force: false);
            controller?.NotifyPedestalChanged();
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

        private void EnsureTriggerCollider()
        {
            var trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void EnsureReferences()
        {
            if (totemSlot == null)
            {
                totemSlot = transform;
            }

            if (indicatorRenderers == null || indicatorRenderers.Length == 0)
            {
                indicatorRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (indicatorLights == null || indicatorLights.Length == 0)
            {
                indicatorLights = GetComponentsInChildren<Light>(true);
            }
        }

        private void ApplyIndicatorState(bool force)
        {
            EnsureReferences();
            var targetColor = hasTotem ? filledColor : idleColor;
            propertyBlock ??= new MaterialPropertyBlock();
            var emissionColor = targetColor * emissionIntensity;

            for (var index = 0; index < indicatorRenderers.Length; index++)
            {
                var indicator = indicatorRenderers[index];
                if (indicator == null)
                {
                    continue;
                }

                if (!force && (!indicator.enabled || !indicator.gameObject.activeInHierarchy))
                {
                    continue;
                }

                indicator.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", targetColor);
                propertyBlock.SetColor("_Color", targetColor);
                propertyBlock.SetColor("_EmissionColor", emissionColor);
                indicator.SetPropertyBlock(propertyBlock);
            }

            for (var index = 0; index < indicatorLights.Length; index++)
            {
                var indicatorLight = indicatorLights[index];
                if (indicatorLight == null)
                {
                    continue;
                }

                indicatorLight.color = targetColor;
                indicatorLight.intensity = Mathf.Max(0f, emissionIntensity);
            }
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
            EnsureReferences();
            ApplyIndicatorState(force: true);
        }
    }
}
