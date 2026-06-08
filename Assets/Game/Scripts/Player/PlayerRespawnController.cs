using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(TopDownGroundProbe))]
    [RequireComponent(typeof(TopDownInputAdapter))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float fallDistance = 6f;
        [SerializeField, Min(0f)] private float safePointMinDistance = 1.5f;
        [SerializeField, Min(0.1f)] private float respawnHeightOffset = 1.05f;

        private Rigidbody body;
        private TopDownGroundProbe groundProbe;
        private TopDownInputAdapter inputAdapter;
        private TopDownPlayerMotor motor;

        private Vector3 safePosition;
        private Quaternion safeRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            groundProbe = GetComponent<TopDownGroundProbe>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            motor = GetComponent<TopDownPlayerMotor>();

            safePosition = transform.position;
            safeRotation = transform.rotation;
        }

        private void Update()
        {
            if (inputAdapter.RespawnPressed)
            {
                RespawnToSafePoint();
            }
        }

        private void LateUpdate()
        {
            if (transform.position.y <= safePosition.y - fallDistance)
            {
                RespawnToSafePoint();
            }
            else
            {
                RefreshSafePoint();
            }

            inputAdapter.ConsumeFrameActions();
        }

        public void RespawnToSafePoint()
        {
            body.position = safePosition;
            body.rotation = safeRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (motor != null)
            {
                motor.ResetMotionState();
            }
        }

        private void RefreshSafePoint()
        {
            if (!groundProbe.IsStableGround)
            {
                return;
            }

            var candidatePosition = groundProbe.GroundPoint + (Vector3.up * respawnHeightOffset);
            if ((candidatePosition - safePosition).sqrMagnitude < safePointMinDistance * safePointMinDistance)
            {
                return;
            }

            safePosition = candidatePosition;
            safeRotation = transform.rotation;
        }
    }
}
