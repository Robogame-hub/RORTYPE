using UnityEngine;

namespace RorType.Gameplay.Player
{
    public sealed class TopDownCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(-10f, 16f, -10f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0.01f)] private float followSharpness = 8f;
        [SerializeField, Min(0f)] private float cursorLookAheadDistance = 9f;
        [SerializeField, Min(0.01f)] private float cursorLookAheadSharpness = 9f;
        [SerializeField, Min(0.01f)] private float cursorLookAheadLag = 0.28f;

        private Vector3 smoothedTargetPosition;
        private Vector3 smoothedLookAheadOffset;
        private Vector3 smoothedLookAheadVelocity;
        private bool hasSmoothedTargetPosition;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            hasSmoothedTargetPosition = false;
            smoothedLookAheadOffset = Vector3.zero;
            smoothedLookAheadVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            if (!hasSmoothedTargetPosition)
            {
                smoothedTargetPosition = target.position;
                smoothedLookAheadOffset = ResolveCursorLookAhead();
                smoothedLookAheadVelocity = Vector3.zero;
                hasSmoothedTargetPosition = true;
            }
            else
            {
                smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, target.position, blend);
            }

            var desiredLookAhead = ResolveCursorLookAhead();
            var lookAheadMaxSpeed = Mathf.Max(1f, cursorLookAheadDistance * Mathf.Max(1f, cursorLookAheadSharpness));
            smoothedLookAheadOffset = Vector3.SmoothDamp(
                smoothedLookAheadOffset,
                desiredLookAhead,
                ref smoothedLookAheadVelocity,
                cursorLookAheadLag,
                lookAheadMaxSpeed,
                Time.deltaTime);

            var framedTargetPosition = smoothedTargetPosition + smoothedLookAheadOffset;
            transform.position = framedTargetPosition + followOffset;
            transform.rotation = Quaternion.LookRotation((framedTargetPosition + lookOffset) - transform.position, Vector3.up);
        }

        private Vector3 ResolveCursorLookAhead()
        {
            if (target == null || cursorLookAheadDistance <= 0f)
            {
                return Vector3.zero;
            }

            var facingController = target.GetComponent<TopDownFacingController>();
            if (facingController == null || !facingController.TryGetAimPoint(out var aimPoint))
            {
                return Vector3.zero;
            }

            var lookAheadOffset = aimPoint - target.position;
            lookAheadOffset.y = 0f;
            if (lookAheadOffset.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude(lookAheadOffset, cursorLookAheadDistance);
        }
    }
}
