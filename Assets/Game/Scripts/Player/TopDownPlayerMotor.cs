using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(TopDownInputAdapter))]
    [RequireComponent(typeof(TopDownGroundProbe))]
    public sealed class TopDownPlayerMotor : MonoBehaviour, IKnockbackReceiver
    {
        [Header("References")]
        [SerializeField] private Transform movementReference;
        [SerializeField] private Transform visualRoot;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 10f;
        [SerializeField, Min(0f)] private float sprintSpeed = 15f;
        [SerializeField, Min(0f)] private float acceleration = 60f;
        [SerializeField, Min(0f)] private float deceleration = 80f;
        [SerializeField, Range(0f, 1f)] private float airControlPercent = 0.5f;
        [SerializeField, Min(0f)] private float extraFallGravity = 20f;
        [SerializeField, Min(0f)] private float groundSnapOffset = 0.02f;
        [SerializeField, Min(0.01f)] private float visualPositionSharpness = 30f;

        [Header("Jump")]
        [SerializeField, Min(0f)] private float jumpSpeed = 8f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpGroundSnapLockTime = 0.16f;

        [Header("Dash")]
        [SerializeField, Min(0.1f)] private float dashDistance = 5f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.16f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.65f;
        [SerializeField] private bool allowAirDash = true;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float knockbackDamping = 18f;
        [SerializeField, Min(0f)] private float maxExternalPlanarSpeed = 10f;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private TopDownInputAdapter inputAdapter;
        private TopDownGroundProbe groundProbe;
        private Vector3 planarVelocity;
        private Vector3 externalPlanarVelocity;
        private Vector3 dashDirection = Vector3.forward;
        private float verticalVelocity;
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float groundSnapLockTimer;
        private float dashTimer;
        private float dashCooldownTimer;
        private bool dashQueued;
        private bool airDashConsumed;
        private bool isGroundedForLocomotion;
        private bool hasVisualPosition;

        public Vector3 LastWorldMoveDirection { get; private set; } = Vector3.forward;
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => isGroundedForLocomotion;
        public bool IsSprinting { get; private set; }
        public bool IsDashing => dashTimer > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            groundProbe = GetComponent<TopDownGroundProbe>();

            body.useGravity = false;
            body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void Update()
        {
            if (inputAdapter.JumpPressed)
            {
                jumpBufferTimer = jumpBufferTime;
                inputAdapter.ConsumeJumpPressed();
            }

            if (inputAdapter.DashPressed)
            {
                dashQueued = true;
                inputAdapter.ConsumeDashPressed();
            }
        }

        private void LateUpdate()
        {
            UpdateVisualSmoothing();
        }

        private void FixedUpdate()
        {
            groundProbe.Probe();
            TickTimers(Time.fixedDeltaTime);
            RefreshGroundedState();
            TryStartJump();
            TryStartDash();

            var moveInput = inputAdapter.MoveInput;
            var inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            var desiredDirection = IsDashing
                ? dashDirection
                : ResolveWorldMoveDirection(moveInput);

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                LastWorldMoveDirection = desiredDirection;
            }

            IsSprinting = !IsDashing && inputMagnitude > 0.1f && inputAdapter.SprintHeld;
            var targetSpeed = IsDashing
                ? GetDashSpeed()
                : (IsSprinting ? sprintSpeed : walkSpeed) * inputMagnitude;
            var targetPlanarVelocity = new Vector3(desiredDirection.x, 0f, desiredDirection.z) * targetSpeed;

            if (IsDashing)
            {
                planarVelocity = targetPlanarVelocity;
            }
            else
            {
                var controlFactor = isGroundedForLocomotion ? 1f : airControlPercent;
                var moveRate = targetPlanarVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
                planarVelocity = Vector3.MoveTowards(
                    planarVelocity,
                    targetPlanarVelocity,
                    moveRate * controlFactor * Time.fixedDeltaTime);
            }

            externalPlanarVelocity = Vector3.MoveTowards(
                externalPlanarVelocity,
                Vector3.zero,
                knockbackDamping * Time.fixedDeltaTime);

            if (externalPlanarVelocity.sqrMagnitude > maxExternalPlanarSpeed * maxExternalPlanarSpeed)
            {
                externalPlanarVelocity = externalPlanarVelocity.normalized * maxExternalPlanarSpeed;
            }

            var combinedPlanarVelocity = planarVelocity + externalPlanarVelocity;

            if (targetPlanarVelocity.sqrMagnitude > 0.0001f)
            {
                body.WakeUp();
            }

            var useGroundSnap = isGroundedForLocomotion && verticalVelocity <= 0f;
            if (useGroundSnap)
            {
                var targetPosition = body.position + (combinedPlanarVelocity * Time.fixedDeltaTime);
                targetPosition.y = ResolveGroundedBodyPositionY();
                body.linearVelocity = Vector3.zero;
                body.MovePosition(targetPosition);
            }
            else
            {
                verticalVelocity += -extraFallGravity * Time.fixedDeltaTime;
                body.linearVelocity = new Vector3(combinedPlanarVelocity.x, verticalVelocity, combinedPlanarVelocity.z);
            }

            CurrentSpeed = combinedPlanarVelocity.magnitude;
        }

        public void SetMovementReference(Transform reference)
        {
            movementReference = reference;
        }

        public void ResetMotionState()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            groundSnapLockTimer = 0f;
            dashTimer = 0f;
            dashCooldownTimer = 0f;
            dashQueued = false;
            airDashConsumed = false;
            externalPlanarVelocity = Vector3.zero;
            hasVisualPosition = false;
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
            }
        }

        public void ApplyKnockback(Vector3 direction, float force)
        {
            var planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude <= 0.0001f || force <= 0f)
            {
                return;
            }

            externalPlanarVelocity += planarDirection.normalized * force;
            if (externalPlanarVelocity.sqrMagnitude > maxExternalPlanarSpeed * maxExternalPlanarSpeed)
            {
                externalPlanarVelocity = externalPlanarVelocity.normalized * maxExternalPlanarSpeed;
            }

            body.WakeUp();
        }

        private Vector3 ResolveWorldMoveDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            var referenceTransform = movementReference != null
                ? movementReference
                : (Camera.main != null ? Camera.main.transform : transform);

            var forward = Vector3.ProjectOnPlane(referenceTransform.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var worldDirection = (forward * moveInput.y) + (right * moveInput.x);

            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            worldDirection.Normalize();

            if (groundProbe.IsStableGround)
            {
                worldDirection = Vector3.ProjectOnPlane(worldDirection, groundProbe.GroundNormal).normalized;
            }

            return worldDirection;
        }

        private void OnValidate()
        {
            sprintSpeed = Mathf.Max(sprintSpeed, walkSpeed);
            dashDuration = Mathf.Max(0.01f, dashDuration);
        }

        private float ResolveGroundedBodyPositionY()
        {
            var lossyScale = transform.lossyScale;
            var halfHeight = capsuleCollider.height * Mathf.Abs(lossyScale.y) * 0.5f;
            var centerOffset = capsuleCollider.center.y * Mathf.Abs(lossyScale.y);
            var bottomToPivot = halfHeight - centerOffset;
            return groundProbe.GroundPoint.y + bottomToPivot + groundSnapOffset;
        }

        private void TickTimers(float deltaTime)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
            groundSnapLockTimer = Mathf.Max(0f, groundSnapLockTimer - deltaTime);
            dashTimer = Mathf.Max(0f, dashTimer - deltaTime);
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);
        }

        private void UpdateVisualSmoothing()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (!hasVisualPosition)
            {
                visualRoot.position = transform.position;
                hasVisualPosition = true;
                return;
            }

            var blend = 1f - Mathf.Exp(-visualPositionSharpness * Time.deltaTime);
            visualRoot.position = Vector3.Lerp(visualRoot.position, transform.position, blend);
        }

        private void RefreshGroundedState()
        {
            var probeStable = groundProbe.IsStableGround;
            isGroundedForLocomotion = probeStable && groundSnapLockTimer <= 0f && verticalVelocity <= 0.01f;

            if (isGroundedForLocomotion)
            {
                coyoteTimer = coyoteTime;
                airDashConsumed = false;
                if (!IsDashing)
                {
                    verticalVelocity = 0f;
                }
            }
        }

        private void TryStartJump()
        {
            if (jumpBufferTimer <= 0f)
            {
                return;
            }

            if (!CanJump())
            {
                return;
            }

            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            groundSnapLockTimer = Mathf.Max(groundSnapLockTimer, jumpGroundSnapLockTime);
            verticalVelocity = jumpSpeed;
            body.linearVelocity = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
            body.WakeUp();
        }

        private bool CanJump()
        {
            return isGroundedForLocomotion || coyoteTimer > 0f;
        }

        private void TryStartDash()
        {
            if (!dashQueued)
            {
                return;
            }

            dashQueued = false;
            if (dashCooldownTimer > 0f || IsDashing)
            {
                return;
            }

            if (!allowAirDash && !isGroundedForLocomotion && coyoteTimer <= 0f)
            {
                return;
            }

            if (!isGroundedForLocomotion && coyoteTimer <= 0f && airDashConsumed)
            {
                return;
            }

            var direction = ResolveDashDirection();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            dashDirection = direction.normalized;
            LastWorldMoveDirection = dashDirection;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            if (!isGroundedForLocomotion && coyoteTimer <= 0f)
            {
                airDashConsumed = true;
            }
        }

        private Vector3 ResolveDashDirection()
        {
            var desiredDirection = ResolveWorldMoveDirection(inputAdapter.MoveInput);
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                return desiredDirection;
            }

            return LastWorldMoveDirection;
        }

        private float GetDashSpeed()
        {
            return dashDistance / dashDuration;
        }
    }
}
