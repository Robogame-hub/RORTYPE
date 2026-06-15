using UnityEngine;

namespace RorType.Gameplay.Combat
{
    [RequireComponent(typeof(TextMesh))]
    [RequireComponent(typeof(WorldBillboard))]
    public sealed class FloatingWorldText : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 0.6f;
        [SerializeField, Min(0f)] private float riseSpeed = 1.45f;
        [SerializeField, Min(0f)] private float fadeSharpness = 10f;
        [SerializeField, Min(0.01f)] private float minimumReadableScale = 0.9f;
        [SerializeField, Min(1f)] private float readableScaleMultiplier = 3f;
        [SerializeField, Min(0f)] private float outlineDistance = 0.018f;

        private TextMesh textMesh;
        private Color baseColor = Color.white;
        private float age;

        public static FloatingWorldText Spawn(Vector3 worldPosition, string content, Color color, float scale = 0.16f)
        {
            var textObject = new GameObject("FloatingWorldText");
            textObject.transform.position = worldPosition;

            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.13f;
            textMesh.fontStyle = FontStyle.Bold;
            TextMeshOutline.AddTo(textObject, 0.018f);

            textObject.AddComponent<WorldBillboard>();
            var floatingText = textObject.AddComponent<FloatingWorldText>();
            floatingText.Initialize(content, color, scale);
            return floatingText;
        }

        private void Awake()
        {
            textMesh = GetComponent<TextMesh>();
        }

        public void Initialize(string content, Color color, float scale)
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMesh>();
            }

            textMesh.text = content;
            textMesh.color = color;
            TextMeshOutline.AddTo(gameObject, outlineDistance);
            baseColor = color;
            transform.localScale = Vector3.one * Mathf.Max(minimumReadableScale, scale * readableScaleMultiplier);
            age = 0f;
            CombatRuntimeBudget.Register(gameObject, CombatRuntimeObjectKind.FloatingText);
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            var alpha = Mathf.Clamp01(1f - (age / lifetime));
            var targetColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            if (textMesh != null)
            {
                textMesh.color = Color.Lerp(textMesh.color, targetColor, 1f - Mathf.Exp(-fadeSharpness * Time.deltaTime));
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }

    public sealed class TextMeshOutline : MonoBehaviour
    {
        private static readonly Vector3[] OutlineOffsets =
        {
            new Vector3(-1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(-1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(1f, 1f, 0f),
        };

        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Min(0f)] private float outlineDistance = 0.018f;

        private TextMesh source;
        private TextMesh[] outlineMeshes;

        public static TextMeshOutline AddTo(GameObject target, float distance)
        {
            if (target == null)
            {
                return null;
            }

            var outline = target.GetComponent<TextMeshOutline>();
            if (outline == null)
            {
                outline = target.AddComponent<TextMeshOutline>();
            }

            outline.outlineDistance = distance;
            outline.EnsureOutlineMeshes();
            outline.SyncOutlineMeshes();
            return outline;
        }

        private void Awake()
        {
            source = GetComponent<TextMesh>();
            EnsureOutlineMeshes();
        }

        private void LateUpdate()
        {
            SyncOutlineMeshes();
        }

        private void EnsureOutlineMeshes()
        {
            if (source == null)
            {
                source = GetComponent<TextMesh>();
            }

            if (source == null)
            {
                return;
            }

            if (outlineMeshes != null && outlineMeshes.Length == OutlineOffsets.Length)
            {
                return;
            }

            outlineMeshes = new TextMesh[OutlineOffsets.Length];

            for (var i = 0; i < OutlineOffsets.Length; i++)
            {
                var outlineObject = new GameObject($"TextOutline_{i}");
                outlineObject.transform.SetParent(transform, false);
                outlineObject.transform.localRotation = Quaternion.identity;
                outlineObject.transform.localScale = Vector3.one;

                var outlineMesh = outlineObject.AddComponent<TextMesh>();
                outlineMeshes[i] = outlineMesh;
            }
        }

        private void SyncOutlineMeshes()
        {
            if (source == null)
            {
                source = GetComponent<TextMesh>();
            }

            if (source == null)
            {
                return;
            }

            EnsureOutlineMeshes();

            var sourceRenderer = source.GetComponent<MeshRenderer>();

            for (var i = 0; i < outlineMeshes.Length; i++)
            {
                var outlineMesh = outlineMeshes[i];
                if (outlineMesh == null)
                {
                    continue;
                }

                outlineMesh.transform.localPosition = (OutlineOffsets[i] * outlineDistance) + new Vector3(0f, 0f, 0.001f);
                outlineMesh.text = source.text;
                outlineMesh.font = source.font;
                outlineMesh.fontSize = source.fontSize;
                outlineMesh.fontStyle = source.fontStyle;
                outlineMesh.characterSize = source.characterSize;
                outlineMesh.anchor = source.anchor;
                outlineMesh.alignment = source.alignment;
                outlineMesh.richText = source.richText;
                outlineMesh.lineSpacing = source.lineSpacing;
                outlineMesh.tabSize = source.tabSize;
                outlineMesh.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, source.color.a);

                var outlineRenderer = outlineMesh.GetComponent<MeshRenderer>();
                if (outlineRenderer == null)
                {
                    continue;
                }

                if (source.font != null)
                {
                    outlineRenderer.sharedMaterial = source.font.material;
                }
                else if (sourceRenderer != null)
                {
                    outlineRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                }

                if (sourceRenderer != null)
                {
                    outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                    outlineRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
                }
            }

            if (sourceRenderer != null)
            {
                sourceRenderer.sortingOrder = Mathf.Max(sourceRenderer.sortingOrder, 1);
            }
        }
    }
}
