using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class TopDownGroundProbe : MonoBehaviour
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(0.01f)] private float probeStartOffset = 0.15f;
        [SerializeField, Min(0.01f)] private float probeDistance = 0.35f;
        [SerializeField, Range(0.25f, 1f)] private float probeRadiusScale = 0.9f;
        [SerializeField, Range(1f, 89f)] private float maxSlopeAngle = 55f;

        private CapsuleCollider capsuleCollider;

        public bool IsGrounded { get; private set; }
        public bool IsStableGround { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public Vector3 GroundPoint { get; private set; }
        public float SlopeAngle { get; private set; }

        private void Awake()
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
        }

        public void Probe()
        {
            var lossyScale = transform.lossyScale;
            var radius = capsuleCollider.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)) * probeRadiusScale;
            var halfHeight = Mathf.Max(capsuleCollider.height * Mathf.Abs(lossyScale.y) * 0.5f, radius);
            var center = transform.TransformPoint(capsuleCollider.center);
            var bottomHemisphereCenter = center + (Vector3.down * Mathf.Max(0f, halfHeight - radius));
            var castOrigin = bottomHemisphereCenter + (Vector3.up * (radius + probeStartOffset));
            var castDistance = Mathf.Max(0.01f, (halfHeight - radius) + probeDistance + probeStartOffset);
            var effectiveGroundMask = ResolveGroundMask();

            var hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                radius,
                Vector3.down,
                HitBuffer,
                castDistance,
                effectiveGroundMask,
                QueryTriggerInteraction.Ignore);

            var hasValidHit = false;
            var bestDistance = float.MaxValue;
            var bestHit = default(RaycastHit);

            for (var i = 0; i < hitCount; i++)
            {
                var hit = HitBuffer[i];
                if (hit.collider == null || hit.collider == capsuleCollider || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                {
                    continue;
                }

                hasValidHit = true;
                bestDistance = hit.distance;
                bestHit = hit;
            }

            if (hasValidHit)
            {
                IsGrounded = true;
                GroundPoint = bestHit.point;
                GroundNormal = bestHit.normal.normalized;
                SlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
                IsStableGround = SlopeAngle <= maxSlopeAngle;
                return;
            }

            IsGrounded = false;
            IsStableGround = false;
            GroundPoint = Vector3.zero;
            GroundNormal = Vector3.up;
            SlopeAngle = 90f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!enabled)
            {
                return;
            }

            var colliderToDraw = capsuleCollider != null ? capsuleCollider : GetComponent<CapsuleCollider>();
            if (colliderToDraw == null)
            {
                return;
            }

            var lossyScale = transform.lossyScale;
            var radius = colliderToDraw.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)) * probeRadiusScale;
            var halfHeight = Mathf.Max(colliderToDraw.height * Mathf.Abs(lossyScale.y) * 0.5f, radius);
            var center = transform.TransformPoint(colliderToDraw.center);
            var bottomHemisphereCenter = center + (Vector3.down * Mathf.Max(0f, halfHeight - radius));
            var castOrigin = bottomHemisphereCenter + (Vector3.up * (radius + probeStartOffset));
            var castDistance = Mathf.Max(0.01f, (halfHeight - radius) + probeDistance + probeStartOffset);

            Gizmos.color = IsStableGround ? Color.green : (IsGrounded ? Color.yellow : Color.red);
            Gizmos.DrawWireSphere(castOrigin, radius);
            Gizmos.DrawLine(castOrigin, castOrigin + (Vector3.down * castDistance));
        }

        private int ResolveGroundMask()
        {
            return groundMask.value == 0 ? Physics.AllLayers : groundMask.value;
        }
    }
}
