using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(TopDownPlayerMotor))]
    public sealed class TopDownFacingController : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 720f;

        private TopDownPlayerMotor motor;
        private Rigidbody body;

        private void Awake()
        {
            motor = GetComponent<TopDownPlayerMotor>();
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            var facingDirection = motor.LastWorldMoveDirection;
            facingDirection.y = 0f;

            if (facingDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            var targetTransform = visualRoot != null ? visualRoot : transform;
            var nextRotation = Quaternion.RotateTowards(
                targetTransform.rotation,
                targetRotation,
                turnSpeedDegrees * Time.fixedDeltaTime);

            if (targetTransform == transform)
            {
                body.MoveRotation(nextRotation);
                return;
            }

            targetTransform.rotation = nextRotation;
        }
    }
}
