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
        [SerializeField, Min(0f)] private float groundedDistanceTolerance = 0.08f;

        private CapsuleCollider capsuleCollider;

        public bool IsGrounded { get; private set; }
        public bool IsStableGround { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public Vector3 GroundPoint { get; private set; }
        public float GroundDistance { get; private set; } = float.PositiveInfinity;
        public float SlopeAngle { get; private set; }
        public float MaxSlopeAngle => maxSlopeAngle;

        private void Awake()
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
        }

        public void Probe()
        {
            if (TrySampleGround(
                    transform.position,
                    out var sampledPoint,
                    out var sampledNormal,
                    out var sampledDistance,
                    out var sampledSlopeAngle))
            {
                GroundPoint = sampledPoint;
                GroundNormal = sampledNormal;
                GroundDistance = sampledDistance;
                SlopeAngle = sampledSlopeAngle;
                IsGrounded = GroundDistance <= groundedDistanceTolerance;
                IsStableGround = IsGrounded && SlopeAngle <= maxSlopeAngle;
                return;
            }

            IsGrounded = false;
            IsStableGround = false;
            GroundPoint = Vector3.zero;
            GroundNormal = Vector3.up;
            GroundDistance = float.PositiveInfinity;
            SlopeAngle = 90f;
        }

        public bool IsStableSurfaceNormal(Vector3 normal)
        {
            return Vector3.Angle(normal.normalized, Vector3.up) <= maxSlopeAngle;
        }

        public bool IsGroundCollider(Collider candidate)
        {
            return candidate != null && ((1 << candidate.gameObject.layer) & ResolveGroundMask()) != 0;
        }

        public bool TrySampleStableGround(Vector3 bodyPosition, out Vector3 groundPoint, out Vector3 groundNormal)
        {
            return TrySampleStableGround(bodyPosition, groundedDistanceTolerance, out groundPoint, out groundNormal);
        }

        public bool TrySampleStableGround(
            Vector3 bodyPosition,
            float maxGroundDistance,
            out Vector3 groundPoint,
            out Vector3 groundNormal)
        {
            if (TrySampleGround(bodyPosition, out groundPoint, out groundNormal, out var groundDistance, out var slopeAngle)
                && groundDistance <= maxGroundDistance
                && slopeAngle <= maxSlopeAngle)
            {
                return true;
            }

            groundPoint = default;
            groundNormal = Vector3.up;
            return false;
        }

        private bool TrySampleGround(
            Vector3 bodyPosition,
            out Vector3 groundPoint,
            out Vector3 groundNormal,
            out float groundDistance,
            out float slopeAngle)
        {
            var lossyScale = transform.lossyScale;
            var radius = capsuleCollider.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)) * probeRadiusScale;
            var halfHeight = Mathf.Max(capsuleCollider.height * Mathf.Abs(lossyScale.y) * 0.5f, radius);
            var center = bodyPosition + Vector3.Scale(capsuleCollider.center, lossyScale);
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
                HitBuffer[i] = default;
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
                groundPoint = bestHit.point;
                groundNormal = bestHit.normal.normalized;
                groundDistance = Mathf.Max(0f, (bottomHemisphereCenter - (Vector3.up * radius)).y - groundPoint.y);
                slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                return true;
            }

            groundPoint = default;
            groundNormal = Vector3.up;
            groundDistance = float.PositiveInfinity;
            slopeAngle = 90f;
            return false;
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
            if (groundMask.value != 0)
            {
                return groundMask.value;
            }

            var groundLayer = LayerMask.NameToLayer("Ground");
            return groundLayer >= 0 ? 1 << groundLayer : 1 << 0;
        }
    }
}
