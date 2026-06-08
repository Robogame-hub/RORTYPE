using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public sealed class TransientScaleEffect : MonoBehaviour
    {
        private Vector3 startScale;
        private Vector3 endScale;
        private float lifetime;
        private float age;

        public void Initialize(Vector3 initialScale, Vector3 targetScale, float durationSeconds)
        {
            startScale = initialScale;
            endScale = targetScale;
            lifetime = Mathf.Max(0.01f, durationSeconds);
            age = 0f;
            transform.localScale = startScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var progress = Mathf.Clamp01(age / lifetime);
            transform.localScale = Vector3.Lerp(startScale, endScale, progress);

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
