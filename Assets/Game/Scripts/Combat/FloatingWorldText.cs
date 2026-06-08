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

        private TextMesh textMesh;
        private MeshRenderer meshRenderer;
        private Color baseColor = Color.white;
        private float age;

        private void Awake()
        {
            textMesh = GetComponent<TextMesh>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        public void Initialize(string content, Color color, float scale)
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMesh>();
            }

            textMesh.text = content;
            textMesh.color = color;
            baseColor = color;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            age = 0f;
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

            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = textMesh != null ? textMesh.color : targetColor;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
