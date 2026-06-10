using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DoorPressureButton : MonoBehaviour
    {
        [SerializeField] private SlidingDoor[] linkedDoors = System.Array.Empty<SlidingDoor>();
        [SerializeField] private Renderer[] indicatorRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light[] indicatorLights = System.Array.Empty<Light>();
        [SerializeField] private bool latchWhenPressed = true;
        [SerializeField] private Color idleColor = new(1f, 0.08f, 0.05f, 1f);
        [SerializeField] private Color pressedColor = new(0.08f, 1f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float emissionIntensity = 1.5f;

        private readonly HashSet<ScenePortalInteractionController> playersOnButton = new();
        private MaterialPropertyBlock propertyBlock;
        private bool isPressed;

        private void Awake()
        {
            EnsureTriggerCollider();
            EnsureIndicatorRenderers();
            ApplyPressedState(force: true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryResolvePlayer(other, out var player))
            {
                return;
            }

            playersOnButton.Add(player);
            SetPressed(true);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!TryResolvePlayer(other, out var player))
            {
                return;
            }

            playersOnButton.Add(player);
            SetPressed(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryResolvePlayer(other, out var player))
            {
                return;
            }

            playersOnButton.Remove(player);
            if (!latchWhenPressed && playersOnButton.Count == 0)
            {
                SetPressed(false);
            }
        }

        private bool TryResolvePlayer(Collider other, out ScenePortalInteractionController player)
        {
            player = null;
            if (other == null)
            {
                return false;
            }

            player = other.GetComponentInParent<ScenePortalInteractionController>();
            return player != null && player.isActiveAndEnabled;
        }

        private void SetPressed(bool pressed)
        {
            if (isPressed == pressed)
            {
                return;
            }

            isPressed = pressed;
            ApplyPressedState(force: false);
        }

        private void ApplyPressedState(bool force)
        {
            var targetColor = isPressed ? pressedColor : idleColor;
            ApplyIndicatorColor(targetColor, force);
            ApplyIndicatorLights(targetColor);

            if (!Application.isPlaying)
            {
                return;
            }

            for (var index = 0; index < linkedDoors.Length; index++)
            {
                var door = linkedDoors[index];
                if (door == null)
                {
                    continue;
                }

                door.SetOpen(isPressed);
            }
        }

        private void ApplyIndicatorColor(Color targetColor, bool force)
        {
            EnsureIndicatorRenderers();
            if (indicatorRenderers.Length == 0)
            {
                return;
            }

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
        }

        private void ApplyIndicatorLights(Color targetColor)
        {
            EnsureIndicatorLights();
            if (indicatorLights.Length == 0)
            {
                return;
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

        private void EnsureTriggerCollider()
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void EnsureIndicatorRenderers()
        {
            if (indicatorRenderers != null && indicatorRenderers.Length > 0)
            {
                return;
            }

            indicatorRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void EnsureIndicatorLights()
        {
            if (indicatorLights != null && indicatorLights.Length > 0)
            {
                return;
            }

            indicatorLights = GetComponentsInChildren<Light>(true);
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
            EnsureIndicatorRenderers();
            EnsureIndicatorLights();
            ApplyPressedState(force: true);
        }
    }
}
