using System.Collections.Generic;
using RorType.Gameplay.AI;
using UnityEngine;
using UnityEngine.Rendering;

namespace RorType.Gameplay.Player
{
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TopDownPlayerMotor))]
    [RequireComponent(typeof(TopDownFacingController))]
    public sealed class PlayerVisionFogOfWar : MonoBehaviour
    {
        [Header("Vision")]
        [SerializeField, Min(0.1f)] private float viewDistance = 22f;
        [SerializeField, Range(1f, 180f)] private float viewAngle = 85f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.1f;
        [SerializeField, Min(0f)] private float closeRevealRadius = 0.85f;
        [SerializeField, Min(0.01f)] private float visibilityRefreshInterval = 0.05f;
        [SerializeField, Min(0f)] private float visibilityGraceTime = 0.12f;
        [SerializeField] private LayerMask occlusionMask;
        [SerializeField] private bool hideEnemiesOutsideCone;
        [SerializeField] private bool useVisionConeVisual;

        [Header("Fog visuals")]
        [SerializeField] private bool drawFogVisuals;
        [SerializeField, Min(0.1f)] private float fogRadius = 36f;
        [SerializeField, Min(0f)] private float playerClearRadius = 0.75f;
        [SerializeField, Min(0f)] private float overlayHeightOffset = 0.08f;
        [SerializeField, Range(12, 160)] private int meshSegments = 128;
        [SerializeField] private Color fogColor = new(0.006f, 0.008f, 0.012f, 0.82f);
        [SerializeField] private bool drawFogEdgeEffect;
        [SerializeField, Min(0.1f)] private float fogEdgeWidth = 9f;
        [SerializeField, Min(0f)] private float fogDriftDegreesPerSecond = 7f;
        [SerializeField, Range(0f, 0.5f)] private float fogPulseStrength = 0.16f;
        [SerializeField] private Color visionHighlightColor = new(0.35f, 0.46f, 0.55f, 0.24f);

        private readonly Dictionary<EnemyCapsuleController, float> lastVisibleTimes = new();
        private readonly List<EnemyCapsuleController> staleEnemies = new();
        private TopDownPlayerMotor motor;
        private TopDownFacingController facingController;
        private Mesh fogMesh;
        private Mesh highlightMesh;
        private MeshRenderer fogRenderer;
        private MeshRenderer highlightRenderer;
        private Material fogMaterial;
        private Material highlightMaterial;
        private float visibilityTimer;
        private bool meshesDirty = true;

        private void Awake()
        {
            motor = GetComponent<TopDownPlayerMotor>();
            facingController = GetComponent<TopDownFacingController>();
            EnsureVisualObjects();
        }

        private void OnEnable()
        {
            visibilityTimer = 0f;
            meshesDirty = true;
            EnsureVisualObjects();
        }

        private void OnDisable()
        {
            SetVisualObjectsEnabled(false);
            RevealAllTrackedEnemies();
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(fogMesh);
            DestroyRuntimeObject(highlightMesh);
            DestroyRuntimeObject(fogMaterial);
            DestroyRuntimeObject(highlightMaterial);
        }

        private void LateUpdate()
        {
            var visionOrigin = ResolveVisionOrigin();
            var visionForward = ResolveVisionForward();

            UpdateVisuals(visionOrigin, visionForward);

            visibilityTimer -= Time.deltaTime;
            if (visibilityTimer > 0f)
            {
                return;
            }

            visibilityTimer = visibilityRefreshInterval;
            UpdateEnemyVisibility(ResolveEyePosition(visionOrigin), visionForward);
        }

        private void OnValidate()
        {
            viewDistance = Mathf.Max(0.1f, viewDistance);
            fogRadius = Mathf.Max(viewDistance + 0.1f, fogRadius);
            playerClearRadius = Mathf.Clamp(playerClearRadius, 0f, fogRadius - 0.05f);
            fogEdgeWidth = Mathf.Max(0.1f, fogEdgeWidth);
            fogDriftDegreesPerSecond = Mathf.Max(0f, fogDriftDegreesPerSecond);
            closeRevealRadius = Mathf.Max(0f, closeRevealRadius);
            visibilityRefreshInterval = Mathf.Max(0.01f, visibilityRefreshInterval);
            visibilityGraceTime = Mathf.Max(0f, visibilityGraceTime);
            meshSegments = Mathf.Clamp(meshSegments, 12, 160);
            meshesDirty = true;
        }

        private void UpdateEnemyVisibility(Vector3 eyePosition, Vector3 visionForward)
        {
            if (!hideEnemiesOutsideCone)
            {
                RevealAllTrackedEnemies();
                return;
            }

            var enemies = EnemyCapsuleController.ActiveEnemyInstances;
            var now = Time.time;

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                var target = enemy.GetComponent<EnemyVisionTarget>();
                if (target == null)
                {
                    target = enemy.gameObject.AddComponent<EnemyVisionTarget>();
                }

                var visible = !hideEnemiesOutsideCone || IsEnemyVisible(enemy, eyePosition, visionForward);
                if (visible)
                {
                    lastVisibleTimes[enemy] = now;
                }
                else if (visibilityGraceTime > 0f
                    && lastVisibleTimes.TryGetValue(enemy, out var lastVisibleTime)
                    && now - lastVisibleTime <= visibilityGraceTime)
                {
                    visible = true;
                }

                target.SetVisible(visible);
            }

            PruneStaleVisibilityEntries(enemies);
        }

        private bool IsEnemyVisible(EnemyCapsuleController enemy, Vector3 eyePosition, Vector3 visionForward)
        {
            var targetPosition = enemy.VisionCenter;
            var planarToTarget = targetPosition - eyePosition;
            planarToTarget.y = 0f;
            var sqrDistance = planarToTarget.sqrMagnitude;

            if (closeRevealRadius > 0f && sqrDistance <= closeRevealRadius * closeRevealRadius)
            {
                return !IsOccluded(eyePosition, targetPosition);
            }

            if (sqrDistance > viewDistance * viewDistance || sqrDistance <= 0.0001f)
            {
                return false;
            }

            var directionToTarget = planarToTarget.normalized;
            var halfAngleCosine = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
            if (Vector3.Dot(visionForward, directionToTarget) < halfAngleCosine)
            {
                return false;
            }

            return !IsOccluded(eyePosition, targetPosition);
        }

        private bool IsOccluded(Vector3 eyePosition, Vector3 targetPosition)
        {
            if (occlusionMask.value == 0)
            {
                return false;
            }

            var direction = targetPosition - eyePosition;
            var distance = direction.magnitude;
            return distance > 0.001f
                && Physics.Raycast(eyePosition, direction / distance, distance, occlusionMask, QueryTriggerInteraction.Ignore);
        }

        private void PruneStaleVisibilityEntries(IReadOnlyList<EnemyCapsuleController> activeEnemies)
        {
            staleEnemies.Clear();
            foreach (var entry in lastVisibleTimes)
            {
                if (entry.Key == null || !ContainsEnemy(activeEnemies, entry.Key))
                {
                    staleEnemies.Add(entry.Key);
                }
            }

            for (var i = 0; i < staleEnemies.Count; i++)
            {
                lastVisibleTimes.Remove(staleEnemies[i]);
            }

            staleEnemies.Clear();
        }

        private static bool ContainsEnemy(IReadOnlyList<EnemyCapsuleController> enemies, EnemyCapsuleController target)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 ResolveVisionOrigin()
        {
            return motor != null ? motor.RenderPosition : transform.position;
        }

        private Vector3 ResolveEyePosition(Vector3 visionOrigin)
        {
            if (facingController != null)
            {
                return facingController.AimOrigin;
            }

            return visionOrigin + (Vector3.up * eyeHeight);
        }

        private Vector3 ResolveVisionForward()
        {
            var forward = facingController != null ? facingController.CurrentAimDirection : Vector3.zero;
            if (forward.sqrMagnitude <= 0.0001f && facingController != null && facingController.FacingVisualRoot != null)
            {
                forward = facingController.FacingVisualRoot.forward;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = transform.forward;
            }

            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void UpdateVisuals(Vector3 visionOrigin, Vector3 visionForward)
        {
            EnsureVisualObjects();
            SetVisualObjectsEnabled(drawFogVisuals);
            if (!drawFogVisuals)
            {
                return;
            }

            if (meshesDirty)
            {
                RebuildMeshes();
                meshesDirty = false;
            }

            var rotation = Quaternion.LookRotation(visionForward, Vector3.up);
            var position = visionOrigin + (Vector3.up * overlayHeightOffset);
            ApplyOverlayTransform(fogRenderer, position, rotation);
            var edgeRotation = Quaternion.AngleAxis(Time.time * fogDriftDegreesPerSecond, Vector3.up) * rotation;
            ApplyOverlayTransform(highlightRenderer, position + (Vector3.up * 0.01f), edgeRotation);
            ApplyMaterialColor(fogMaterial, fogColor);
            ApplyMaterialColor(highlightMaterial, EvaluateFogEdgeColor());
        }

        private void EnsureVisualObjects()
        {
            if (fogMesh == null)
            {
                fogMesh = new Mesh
                {
                    name = "PlayerVisionFogMesh",
                    hideFlags = HideFlags.DontSave
                };
                meshesDirty = true;
            }

            if (highlightMesh == null)
            {
                highlightMesh = new Mesh
                {
                    name = "PlayerVisionHighlightMesh",
                    hideFlags = HideFlags.DontSave
                };
                meshesDirty = true;
            }

            if (fogMaterial == null)
            {
                fogMaterial = CreateTransparentMaterial("Player Vision Fog", fogColor, 0);
            }

            if (highlightMaterial == null)
            {
                highlightMaterial = CreateTransparentMaterial("Player Vision Highlight", visionHighlightColor, 1);
            }

            if (fogRenderer == null)
            {
                fogRenderer = CreateMeshRenderer("PlayerVisionFog", fogMesh, fogMaterial);
            }

            if (highlightRenderer == null)
            {
                highlightRenderer = CreateMeshRenderer("PlayerVisionHighlight", highlightMesh, highlightMaterial);
            }
        }

        private MeshRenderer CreateMeshRenderer(string objectName, Mesh mesh, Material material)
        {
            var meshObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
            meshObject.transform.SetParent(transform, false);
            meshObject.layer = gameObject.layer;

            var meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;
            return meshRenderer;
        }

        private void RebuildMeshes()
        {
            BuildFogMesh();
            BuildHighlightMesh();
        }

        private void BuildFogMesh()
        {
            var vertices = new List<Vector3>(meshSegments * 8);
            var triangles = new List<int>(meshSegments * 12);
            var innerRadius = Mathf.Clamp(playerClearRadius, 0.05f, fogRadius - 0.05f);
            var visibleRadius = Mathf.Clamp(viewDistance, innerRadius + 0.05f, fogRadius);
            var halfAngle = viewAngle * 0.5f;

            for (var i = 0; i < meshSegments; i++)
            {
                var angle0 = -180f + (360f * i / meshSegments);
                var angle1 = -180f + (360f * (i + 1) / meshSegments);
                if (!useVisionConeVisual)
                {
                    AddQuad(vertices, triangles, visibleRadius, fogRadius, angle0, angle1);
                    continue;
                }

                var centerAngle = Mathf.DeltaAngle(0f, (angle0 + angle1) * 0.5f);
                var insideVisionCone = Mathf.Abs(centerAngle) <= halfAngle;

                if (!insideVisionCone)
                {
                    AddQuad(vertices, triangles, innerRadius, visibleRadius, angle0, angle1);
                }

                if (fogRadius > visibleRadius + 0.01f)
                {
                    AddQuad(vertices, triangles, visibleRadius, fogRadius, angle0, angle1);
                }
            }

            fogMesh.Clear();
            fogMesh.SetVertices(vertices);
            fogMesh.SetTriangles(triangles, 0);
            fogMesh.RecalculateBounds();
            fogMesh.RecalculateNormals();
        }

        private void BuildHighlightMesh()
        {
            var vertices = new List<Vector3>((meshSegments + 1) * 2);
            var triangles = new List<int>(meshSegments * 6);
            var innerFeather = Mathf.Min(1.2f, fogEdgeWidth * 0.18f);
            var baseInnerRadius = Mathf.Max(0.05f, viewDistance - innerFeather);
            var baseOuterRadius = Mathf.Min(fogRadius, viewDistance + fogEdgeWidth);

            for (var i = 0; i <= meshSegments; i++)
            {
                var angle = -180f + (360f * i / meshSegments);
                var radians = angle * Mathf.Deg2Rad;
                var wave = (Mathf.Sin(radians * 14.5f) * 0.55f) + (Mathf.Sin((radians * 31f) + 1.3f) * 0.45f);
                var innerRadius = Mathf.Max(0.05f, baseInnerRadius + (wave * fogEdgeWidth * 0.035f));
                var outerRadius = Mathf.Min(fogRadius, baseOuterRadius + (wave * fogEdgeWidth * 0.06f));

                vertices.Add(Polar(angle, innerRadius));
                vertices.Add(Polar(angle, Mathf.Max(innerRadius + 0.05f, outerRadius)));
            }

            for (var i = 0; i < meshSegments; i++)
            {
                var startIndex = i * 2;
                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 3);
            }

            highlightMesh.Clear();
            highlightMesh.SetVertices(vertices);
            highlightMesh.SetTriangles(triangles, 0);
            highlightMesh.RecalculateBounds();
            highlightMesh.RecalculateNormals();
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, float innerRadius, float outerRadius, float angle0, float angle1)
        {
            var startIndex = vertices.Count;
            vertices.Add(Polar(angle0, innerRadius));
            vertices.Add(Polar(angle0, outerRadius));
            vertices.Add(Polar(angle1, innerRadius));
            vertices.Add(Polar(angle1, outerRadius));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 3);
        }

        private static Vector3 Polar(float angleDegrees, float radius)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
        }

        private void SetVisualObjectsEnabled(bool visible)
        {
            if (fogRenderer != null)
            {
                fogRenderer.enabled = visible;
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.enabled = visible && drawFogEdgeEffect;
            }
        }

        private Color EvaluateFogEdgeColor()
        {
            var pulse = 1f + (Mathf.Sin(Time.time * 1.7f) * fogPulseStrength);
            var color = visionHighlightColor;
            color.a = Mathf.Clamp01(color.a * pulse);
            return color;
        }

        private static void ApplyOverlayTransform(Renderer targetRenderer, Vector3 position, Quaternion rotation)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.transform.SetPositionAndRotation(position, rotation);
        }

        private static Material CreateTransparentMaterial(string materialName, Color color, int renderQueueOffset)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent + renderQueueOffset
            };

            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ApplyMaterialColor(material, color);
            return material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private void RevealAllTrackedEnemies()
        {
            var enemies = EnemyCapsuleController.ActiveEnemyInstances;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var target = enemy.GetComponent<EnemyVisionTarget>();
                if (target != null)
                {
                    target.SetVisible(true);
                }
            }

            lastVisibleTimes.Clear();
        }

        private static void DestroyRuntimeObject(UnityEngine.Object runtimeObject)
        {
            if (runtimeObject == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(runtimeObject);
        }
    }
}
