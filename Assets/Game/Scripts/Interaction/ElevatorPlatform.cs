using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ElevatorPlatform : MonoBehaviour
    {
        [SerializeField] private Transform movingRoot;
        [SerializeField] private Rigidbody movingBody;
        [SerializeField] private Renderer[] indicatorRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light[] indicatorLights = System.Array.Empty<Light>();
        [SerializeField] private Vector3 travelLocalOffset = Vector3.up * 5f;
        [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
        [SerializeField, Min(0.01f)] private float returnSpeed = 4f;
        [SerializeField, Min(0f)] private float returnDelayAfterExit = 3f;
        [SerializeField] private bool startRaised;
        [SerializeField] private Color idleColor = new(1f, 0.08f, 0.05f, 1f);
        [SerializeField] private Color activeColor = new(0.08f, 1f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float emissionIntensity = 1.5f;
        [SerializeField] private bool carryPlayers = true;

        private readonly HashSet<ScenePortalInteractionController> playersOnPlatform = new();
        private readonly List<ScenePortalInteractionController> stalePlayers = new();
        private MaterialPropertyBlock propertyBlock;
        private Vector3 loweredLocalPosition;
        private bool wantsRaised;
        private float returnTimer;

        private void Awake()
        {
            ResolveReferences();
            ConfigureBody();

            loweredLocalPosition = movingRoot.localPosition;
            wantsRaised = startRaised;
            returnTimer = 0f;
            movingRoot.localPosition = GetTargetLocalPosition();
            ApplyIndicatorState(force: true);
        }

        private void FixedUpdate()
        {
            PruneStalePlayers();
            TickRequest(Time.fixedDeltaTime);
            MovePlatform(Time.fixedDeltaTime);
            ApplyIndicatorState(force: false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryResolvePlayer(other, out var player))
            {
                return;
            }

            playersOnPlatform.Add(player);
            wantsRaised = true;
            returnTimer = returnDelayAfterExit;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!TryResolvePlayer(other, out var player))
            {
                return;
            }

            playersOnPlatform.Add(player);
            wantsRaised = true;
            returnTimer = returnDelayAfterExit;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryResolvePlayer(other, out var player))
            {
                return;
            }

            playersOnPlatform.Remove(player);
            if (playersOnPlatform.Count == 0)
            {
                returnTimer = returnDelayAfterExit;
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

        private void TickRequest(float deltaTime)
        {
            if (playersOnPlatform.Count > 0)
            {
                wantsRaised = true;
                returnTimer = returnDelayAfterExit;
                return;
            }

            if (!wantsRaised)
            {
                return;
            }

            returnTimer -= deltaTime;
            if (returnTimer <= 0f)
            {
                wantsRaised = false;
            }
        }

        private void MovePlatform(float deltaTime)
        {
            if (movingRoot == null)
            {
                return;
            }

            var targetLocalPosition = GetTargetLocalPosition();
            var speed = wantsRaised ? moveSpeed : returnSpeed;
            var nextLocalPosition = Vector3.MoveTowards(
                movingRoot.localPosition,
                targetLocalPosition,
                speed * deltaTime);

            if (Vector3.SqrMagnitude(nextLocalPosition - movingRoot.localPosition) <= 0.000001f)
            {
                return;
            }

            var previousWorldPosition = movingRoot.position;
            var nextWorldPosition = movingRoot.parent != null
                ? movingRoot.parent.TransformPoint(nextLocalPosition)
                : nextLocalPosition;

            if (movingBody != null && movingBody.transform == movingRoot)
            {
                movingBody.MovePosition(nextWorldPosition);
            }
            else
            {
                movingRoot.localPosition = nextLocalPosition;
                nextWorldPosition = movingRoot.position;
            }

            if (carryPlayers)
            {
                CarryPlayers(nextWorldPosition - previousWorldPosition);
            }
        }

        private Vector3 GetTargetLocalPosition()
        {
            return loweredLocalPosition + (wantsRaised ? travelLocalOffset : Vector3.zero);
        }

        private void CarryPlayers(Vector3 worldDelta)
        {
            if (worldDelta.sqrMagnitude <= 0.000001f || playersOnPlatform.Count == 0)
            {
                return;
            }

            foreach (var player in playersOnPlatform)
            {
                if (player == null || !player.isActiveAndEnabled)
                {
                    continue;
                }

                var playerBody = player.GetComponent<Rigidbody>();
                if (playerBody != null && !playerBody.isKinematic)
                {
                    playerBody.MovePosition(playerBody.position + worldDelta);
                    continue;
                }

                player.transform.position += worldDelta;
            }
        }

        private void PruneStalePlayers()
        {
            if (playersOnPlatform.Count == 0)
            {
                return;
            }

            stalePlayers.Clear();
            foreach (var player in playersOnPlatform)
            {
                if (player == null || !player.isActiveAndEnabled)
                {
                    stalePlayers.Add(player);
                }
            }

            for (var index = 0; index < stalePlayers.Count; index++)
            {
                playersOnPlatform.Remove(stalePlayers[index]);
            }
        }

        private void ApplyIndicatorState(bool force)
        {
            var targetColor = wantsRaised ? activeColor : idleColor;
            ApplyIndicatorColor(targetColor, force);
            ApplyIndicatorLights(targetColor);
        }

        private void ApplyIndicatorColor(Color targetColor, bool force)
        {
            ResolveIndicatorRenderers();
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
            ResolveIndicatorLights();
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

        private void ResolveReferences()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            if (movingBody == null)
            {
                movingBody = movingRoot.GetComponent<Rigidbody>();
            }

            if (movingBody == null)
            {
                movingBody = GetComponent<Rigidbody>();
            }

            ResolveIndicatorRenderers();
            ResolveIndicatorLights();
        }

        private void ConfigureBody()
        {
            if (movingBody == null)
            {
                return;
            }

            movingBody.useGravity = false;
            movingBody.isKinematic = true;
            movingBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void ResolveIndicatorRenderers()
        {
            if (indicatorRenderers != null && indicatorRenderers.Length > 0)
            {
                return;
            }

            indicatorRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void ResolveIndicatorLights()
        {
            if (indicatorLights != null && indicatorLights.Length > 0)
            {
                return;
            }

            indicatorLights = GetComponentsInChildren<Light>(true);
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed);
            returnSpeed = Mathf.Max(0.01f, returnSpeed);
            returnDelayAfterExit = Mathf.Max(0f, returnDelayAfterExit);
            ResolveReferences();
            ConfigureBody();
            ApplyIndicatorState(force: true);
        }
    }
}
