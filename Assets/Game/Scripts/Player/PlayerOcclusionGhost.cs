using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RorType.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerOcclusionGhost : MonoBehaviour
    {
        private const string ResourceMaterialPath = "Materials/PlayerGhostFresnel";
        private const string ShaderName = "RorType/Player/GhostFresnelThroughOccluders";
        private const string OverlayNamePrefix = "OcclusionGhostOverlay";

        [Header("Occlusion")]
        [SerializeField] private LayerMask occluderMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0f)] private float targetHeightOffset = 1f;
        [SerializeField, Min(0f)] private float cameraNearPadding = 0.15f;
        [SerializeField, Min(0f)] private float targetNearPadding = 0.3f;

        [Header("Ghost")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color ghostColor = new Color(0.18f, 0.72f, 1f, 0.85f);
        [SerializeField, Min(0.01f)] private float fadeSpeed = 10f;

        private readonly List<Renderer> overlayRenderers = new();
        private readonly List<Collider> playerColliders = new();
        private readonly List<MeshRenderer> sourceMeshRenderers = new();
        private readonly List<SkinnedMeshRenderer> sourceSkinnedRenderers = new();
        private Material runtimeGhostMaterial;
        private MaterialPropertyBlock propertyBlock;
        private TopDownPlayerMotor motor;
        private float visibility;

        private void Awake()
        {
            motor = GetComponent<TopDownPlayerMotor>();
            propertyBlock = new MaterialPropertyBlock();
            CachePlayerColliders();
            EnsureMaterial();
            RebuildOverlays();
            SetOverlayVisibility(0f, true);
        }

        private void OnEnable()
        {
            if (overlayRenderers.Count == 0)
            {
                RebuildOverlays();
            }
        }

        private void OnDisable()
        {
            SetOverlayVisibility(0f, true);
        }

        private void LateUpdate()
        {
            var targetVisibility = IsPlayerOccluded() ? 1f : 0f;
            var step = Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, step);
            SetOverlayVisibility(visibility, false);
        }

        public static bool IsGhostOverlayRenderer(Renderer renderer)
        {
            return renderer != null && renderer.name.StartsWith(OverlayNamePrefix, System.StringComparison.Ordinal);
        }

        private void CachePlayerColliders()
        {
            playerColliders.Clear();
            GetComponentsInChildren(true, playerColliders);
        }

        private void EnsureMaterial()
        {
            if (ghostMaterial == null)
            {
                ghostMaterial = Resources.Load<Material>(ResourceMaterialPath);
            }

            if (ghostMaterial != null)
            {
                runtimeGhostMaterial = ghostMaterial;
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                enabled = false;
                return;
            }

            runtimeGhostMaterial = new Material(shader)
            {
                name = "Runtime Player Ghost Fresnel"
            };
        }

        private void RebuildOverlays()
        {
            overlayRenderers.Clear();
            if (runtimeGhostMaterial == null)
            {
                EnsureMaterial();
                if (runtimeGhostMaterial == null)
                {
                    return;
                }
            }

            sourceMeshRenderers.Clear();
            GetComponentsInChildren(true, sourceMeshRenderers);
            for (var i = 0; i < sourceMeshRenderers.Count; i++)
            {
                var sourceRenderer = sourceMeshRenderers[i];
                if (!IsValidSourceRenderer(sourceRenderer))
                {
                    continue;
                }

                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    continue;
                }

                var overlay = CreateOverlayObject(sourceRenderer.transform);
                var overlayFilter = overlay.AddComponent<MeshFilter>();
                overlayFilter.sharedMesh = sourceFilter.sharedMesh;

                var overlayRenderer = overlay.AddComponent<MeshRenderer>();
                CopyRendererSettings(sourceRenderer, overlayRenderer);
                AssignGhostMaterials(overlayRenderer, sourceRenderer.sharedMaterials.Length);
                overlayRenderers.Add(overlayRenderer);
            }

            sourceSkinnedRenderers.Clear();
            GetComponentsInChildren(true, sourceSkinnedRenderers);
            for (var i = 0; i < sourceSkinnedRenderers.Count; i++)
            {
                var sourceRenderer = sourceSkinnedRenderers[i];
                if (!IsValidSourceRenderer(sourceRenderer) || sourceRenderer.sharedMesh == null)
                {
                    continue;
                }

                var overlay = CreateOverlayObject(sourceRenderer.transform);
                var overlayRenderer = overlay.AddComponent<SkinnedMeshRenderer>();
                CopyRendererSettings(sourceRenderer, overlayRenderer);
                overlayRenderer.sharedMesh = sourceRenderer.sharedMesh;
                overlayRenderer.rootBone = sourceRenderer.rootBone;
                overlayRenderer.bones = sourceRenderer.bones;
                overlayRenderer.quality = sourceRenderer.quality;
                overlayRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
                AssignGhostMaterials(overlayRenderer, sourceRenderer.sharedMaterials.Length);
                overlayRenderers.Add(overlayRenderer);
            }
        }

        private static bool IsValidSourceRenderer(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && !IsGhostOverlayRenderer(renderer)
                && renderer.sharedMaterials != null
                && renderer.sharedMaterials.Length > 0;
        }

        private static GameObject CreateOverlayObject(Transform sourceTransform)
        {
            var overlay = new GameObject($"{OverlayNamePrefix}_{sourceTransform.name}");
            overlay.transform.SetParent(sourceTransform, false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;
            return overlay;
        }

        private static void CopyRendererSettings(Renderer source, Renderer overlay)
        {
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlay.allowOcclusionWhenDynamic = false;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 20;
        }

        private void AssignGhostMaterials(Renderer renderer, int sourceMaterialCount)
        {
            var materialCount = Mathf.Max(1, sourceMaterialCount);
            var materials = new Material[materialCount];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = runtimeGhostMaterial;
            }

            renderer.sharedMaterials = materials;
        }

        private bool IsPlayerOccluded()
        {
            var currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return false;
            }

            var cameraPosition = currentCamera.transform.position;
            var targetPosition = GetProbeTargetPosition();
            var offset = targetPosition - cameraPosition;
            var distance = offset.magnitude;
            if (distance <= cameraNearPadding + targetNearPadding)
            {
                return false;
            }

            var direction = offset / distance;
            var castDistance = distance - cameraNearPadding - targetNearPadding;
            var origin = cameraPosition + direction * cameraNearPadding;
            if (!Physics.Raycast(origin, direction, out var hit, castDistance, occluderMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.collider != null && !IsPlayerCollider(hit.collider);
        }

        private Vector3 GetProbeTargetPosition()
        {
            var basePosition = motor != null ? motor.RenderPosition : transform.position;
            return basePosition + Vector3.up * targetHeightOffset;
        }

        private bool IsPlayerCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.transform.root == transform.root)
            {
                return true;
            }

            for (var i = 0; i < playerColliders.Count; i++)
            {
                if (playerColliders[i] == collider)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetOverlayVisibility(float value, bool forceDisabled)
        {
            var active = !forceDisabled && value > 0.001f;
            for (var i = 0; i < overlayRenderers.Count; i++)
            {
                var overlayRenderer = overlayRenderers[i];
                if (overlayRenderer == null)
                {
                    continue;
                }

                overlayRenderer.enabled = active;
                if (!active)
                {
                    continue;
                }

                overlayRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_GhostColor", ghostColor);
                propertyBlock.SetFloat("_GhostVisibility", value);
                overlayRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void OnValidate()
        {
            targetHeightOffset = Mathf.Max(0f, targetHeightOffset);
            cameraNearPadding = Mathf.Max(0f, cameraNearPadding);
            targetNearPadding = Mathf.Max(0f, targetNearPadding);
            fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
        }
    }
}
