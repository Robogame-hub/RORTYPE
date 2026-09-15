using UnityEngine;

namespace RorType.Gameplay.Player
{
    public sealed class FootstepRingVfx : MonoBehaviour
    {
        [SerializeField] private float duration = 0.5f;
        private Vector3 fullScale;
        private float age;
        private Renderer ringRenderer;
        private MaterialPropertyBlock properties;
        private static readonly int Tint = Shader.PropertyToID("_TintColor");

        private void Start()
        {
            fullScale = transform.localScale;
            ringRenderer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            var progress = Mathf.Clamp01(age / duration);
            Apply(progress);
            if (progress >= 1f) Destroy(gameObject);
        }

        private void Apply(float progress)
        {
            transform.localScale = fullScale * Mathf.Lerp(0.75f, 1f, 1f - (1f - progress) * (1f - progress));
            properties.SetColor(Tint, new Color(0.68f, 0.70f, 0.72f, 0.85f * (1f - progress)));
            ringRenderer.SetPropertyBlock(properties);
        }
    }
}