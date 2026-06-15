using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TotemPickup : MonoBehaviour
    {
        private static readonly List<TotemPickup> RegisteredTotems = new();

        [SerializeField] private string interactionPrompt = "\u041D\u0430\u0436\u043C\u0438\u0442\u0435 E \u0447\u0442\u043E\u0431\u044B \u0432\u0437\u044F\u0442\u044C \u0442\u043E\u0442\u0435\u043C";
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer[] visualRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light[] visualLights = System.Array.Empty<Light>();
        [SerializeField] private Color visualColor = new(0.55f, 0.12f, 1f, 1f);
        [SerializeField, Min(0f)] private float emissionIntensity = 1.2f;
        [SerializeField, Range(0.1f, 1f)] private float carriedScaleMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float idleBobAmplitude = 0.25f;
        [SerializeField, Min(0f)] private float idleBobSpeed = 2f;
        [SerializeField, Min(0f)] private float idleRotateSpeed = 75f;

        private readonly HashSet<ScenePortalInteractionController> touchingInteractors = new();
        private const float TriggerOverlapFallbackDistance = 0.75f;
        private MaterialPropertyBlock propertyBlock;
        private Collider interactionTrigger;
        private Vector3 visualStartLocalPosition;
        private Vector3 visualStartLocalScale = Vector3.one;
        private bool isCarried;
        private bool isPlaced;

        public static IReadOnlyList<TotemPickup> ActiveTotems => RegisteredTotems;
        public bool IsAvailable => isActiveAndEnabled && !isCarried && !isPlaced;

        private void Awake()
        {
            EnsureTriggerCollider();
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            EnsureVisualReferences();
            visualStartLocalPosition = visualRoot.localPosition;
            visualStartLocalScale = visualRoot.localScale;
            ApplyVisualColor();
        }

        private void OnEnable()
        {
            if (!RegisteredTotems.Contains(this))
            {
                RegisteredTotems.Add(this);
            }
        }

        private void OnDisable()
        {
            RegisteredTotems.Remove(this);
            touchingInteractors.Clear();
        }

        private void Update()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.Rotate(Vector3.up, idleRotateSpeed * Time.deltaTime, Space.World);
            if (!isCarried)
            {
                var bob = Mathf.Sin(Time.time * idleBobSpeed) * idleBobAmplitude;
                visualRoot.localPosition = visualStartLocalPosition + Vector3.up * bob;
                visualRoot.localScale = visualStartLocalScale;
            }
        }

        public bool IsTouchedBy(ScenePortalInteractionController interactor)
        {
            return IsAvailable && interactor != null && touchingInteractors.Contains(interactor);
        }

        public bool IsWithinInteractionRange(Vector3 worldPosition)
        {
            return IsAvailable && IsWithinTriggerOverlapFallback(interactionTrigger, worldPosition);
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
            if (!IsAvailable || interactor == null)
            {
                return;
            }

            var carrier = interactor.GetComponent<TotemCarrier>();
            if (carrier == null)
            {
                carrier = interactor.gameObject.AddComponent<TotemCarrier>();
            }

            if (!carrier.Add(this))
            {
                return;
            }

            isCarried = true;
            isPlaced = false;
            touchingInteractors.Clear();
            if (interactionTrigger != null)
            {
                interactionTrigger.enabled = false;
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = visualStartLocalScale * carriedScaleMultiplier;
            }
        }

        public void MoveAsCarried(Vector3 targetPosition, float followSharpness)
        {
            var t = followSharpness <= 0f ? 1f : 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualStartLocalPosition;
                visualRoot.localScale = visualStartLocalScale * carriedScaleMultiplier;
            }
        }

        public void PlaceOn(Transform slot)
        {
            isCarried = false;
            isPlaced = true;
            touchingInteractors.Clear();
            if (interactionTrigger != null)
            {
                interactionTrigger.enabled = false;
            }

            if (slot != null)
            {
                transform.SetParent(slot, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            if (visualRoot != null)
            {
                visualRoot.localPosition = visualStartLocalPosition;
                visualRoot.localScale = visualStartLocalScale;
            }
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
            interactionTrigger = GetComponent<Collider>();
            if (interactionTrigger != null)
            {
                interactionTrigger.isTrigger = true;
            }
        }

        private static bool IsWithinTriggerOverlapFallback(Collider trigger, Vector3 worldPosition)
        {
            if (trigger == null || !trigger.enabled)
            {
                return false;
            }

            if (trigger.bounds.Contains(worldPosition))
            {
                return true;
            }

            var closestPoint = trigger.ClosestPoint(worldPosition);
            return (closestPoint - worldPosition).sqrMagnitude <=
                   TriggerOverlapFallbackDistance * TriggerOverlapFallbackDistance;
        }

        private void EnsureVisualReferences()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            if (visualRenderers == null || visualRenderers.Length == 0)
            {
                visualRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (visualLights == null || visualLights.Length == 0)
            {
                visualLights = GetComponentsInChildren<Light>(true);
            }
        }

        private void ApplyVisualColor()
        {
            EnsureVisualReferences();
            propertyBlock ??= new MaterialPropertyBlock();
            var emissionColor = visualColor * emissionIntensity;

            for (var index = 0; index < visualRenderers.Length; index++)
            {
                var visualRenderer = visualRenderers[index];
                if (visualRenderer == null)
                {
                    continue;
                }

                visualRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", visualColor);
                propertyBlock.SetColor("_Color", visualColor);
                propertyBlock.SetColor("_EmissionColor", emissionColor);
                visualRenderer.SetPropertyBlock(propertyBlock);
            }

            for (var index = 0; index < visualLights.Length; index++)
            {
                var visualLight = visualLights[index];
                if (visualLight == null)
                {
                    continue;
                }

                visualLight.color = visualColor;
                visualLight.intensity = Mathf.Max(0f, emissionIntensity);
            }
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
            EnsureVisualReferences();
            ApplyVisualColor();
        }
    }
}
