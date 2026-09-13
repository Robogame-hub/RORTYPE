using UnityEngine;

namespace RorType.Gameplay.Player
{
    [DefaultExecutionOrder(100)]
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
        private TopDownPlayerMotor targetMotor;
        private TopDownFacingController targetFacing;
        private bool hasSmoothedTargetPosition;
        private float nextTargetSearchTime;

        private void OnEnable()
        {
            SetTarget(target);
            nextTargetSearchTime = 0f;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            targetMotor = target != null ? target.GetComponentInParent<TopDownPlayerMotor>() : null;
            targetFacing = targetMotor != null
                ? targetMotor.GetComponent<TopDownFacingController>()
                : (target != null ? target.GetComponentInParent<TopDownFacingController>() : null);
            hasSmoothedTargetPosition = false;
            smoothedLookAheadOffset = Vector3.zero;
            smoothedLookAheadVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!TryResolveTarget())
            {
                return;
            }

            var targetPosition = GetTargetPosition();
            var blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            if (!hasSmoothedTargetPosition)
            {
                smoothedTargetPosition = targetPosition;
                smoothedLookAheadOffset = ResolveCursorLookAhead(targetPosition);
                smoothedLookAheadVelocity = Vector3.zero;
                hasSmoothedTargetPosition = true;
            }
            else
            {
                smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, targetPosition, blend);
            }

            var desiredLookAhead = ResolveCursorLookAhead(targetPosition);
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

        private Vector3 GetTargetPosition()
        {
            if (targetMotor == null && target != null)
            {
                targetMotor = target.GetComponentInParent<TopDownPlayerMotor>();
            }

            return targetMotor != null ? targetMotor.RenderPosition : target.position;
        }

        private bool TryResolveTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                return true;
            }

            var activePlayer = PlayerResourceController.ActivePlayer;
            if (activePlayer != null && activePlayer.isActiveAndEnabled)
            {
                SetTarget(activePlayer.transform);
                return true;
            }

            if (Time.unscaledTime < nextTargetSearchTime)
            {
                return false;
            }

            nextTargetSearchTime = Time.unscaledTime + 0.5f;
            var playerMotor = FindFirstObjectByType<TopDownPlayerMotor>();
            if (playerMotor == null || !playerMotor.isActiveAndEnabled)
            {
                return false;
            }

            SetTarget(playerMotor.transform);
            return true;
        }

        private Vector3 ResolveCursorLookAhead(Vector3 targetPosition)
        {
            if (target == null || cursorLookAheadDistance <= 0f)
            {
                return Vector3.zero;
            }

            if (targetFacing == null || !targetFacing.TryGetAimPoint(out var aimPoint))
            {
                return Vector3.zero;
            }

            var lookAheadOffset = aimPoint - targetPosition;
            lookAheadOffset.y = 0f;
            if (lookAheadOffset.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude(lookAheadOffset, cursorLookAheadDistance);
        }
    }
}
