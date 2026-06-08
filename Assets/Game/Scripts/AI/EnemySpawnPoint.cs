using UnityEngine;

namespace RorType.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float baseSpawnRadius = 0.75f;
        [SerializeField, Min(0.05f)] private float gizmoRadius = 0.35f;
        [SerializeField] private Color gizmoColor = new Color(0.18f, 0.9f, 0.32f, 0.9f);

        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
        public float SpawnRadius => Mathf.Max(baseSpawnRadius, baseSpawnRadius * GetHorizontalScaleFactor());
        public int SpawnBurstCount => Mathf.Clamp(Mathf.RoundToInt(GetHorizontalScaleFactor()), 1, 4);

        public Vector3 GetRandomSpawnPosition()
        {
            var radius = SpawnRadius;
            if (radius <= 0.0001f)
            {
                return transform.position;
            }

            var randomOffset = Random.insideUnitCircle * radius;
            return transform.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            var drawRadius = Mathf.Max(gizmoRadius, SpawnRadius);
            Gizmos.DrawWireSphere(transform.position, drawRadius);
            Gizmos.DrawLine(transform.position, transform.position + (transform.forward * (drawRadius * 2f)));
        }

        private float GetHorizontalScaleFactor()
        {
            var lossyScale = transform.lossyScale;
            var scaleX = Mathf.Abs(lossyScale.x);
            var scaleZ = Mathf.Abs(lossyScale.z);
            return Mathf.Max(1f, scaleX, scaleZ);
        }
    }
}
