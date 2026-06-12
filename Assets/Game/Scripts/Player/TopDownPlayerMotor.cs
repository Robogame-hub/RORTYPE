using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(TopDownInputAdapter))]
    [RequireComponent(typeof(TopDownGroundProbe))]
    [RequireComponent(typeof(PlayerResourceController))]
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
        [SerializeField, Min(0f)] private float wallSkinWidth = 0.05f;
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
        [SerializeField, Min(1)] private int maxDashCharges = 2;
        [SerializeField, Min(0.01f)] private float dashChargeRecoveryTime = 5f;
        [SerializeField] private bool allowAirDash = true;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float knockbackDamping = 18f;
        [SerializeField, Min(0f)] private float maxExternalPlanarSpeed = 10f;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private TopDownInputAdapter inputAdapter;
        private TopDownGroundProbe groundProbe;
        private PlayerResourceController resources;
        private Vector3 planarVelocity;
        private Vector3 externalPlanarVelocity;
        private Vector3 dashDirection = Vector3.forward;
        private float verticalVelocity;
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float groundSnapLockTimer;
        private float dashTimer;
        private float dashCooldownTimer;
        private float dashChargeRecoveryTimer;
        private int dashCharges;
        private bool dashQueued;
        private bool airDashConsumed;
        private bool isGroundedForLocomotion;
        private bool hasVisualBasePose;
        private bool hasVisualPosition;
        private Vector3 visualBaseLocalPosition;
        private Vector3 smoothedVisualWorldPosition;
        private readonly RaycastHit[] movementCastHits = new RaycastHit[16];
        private readonly Collider[] penetrationHits = new Collider[16];

        public Vector3 LastWorldMoveDirection { get; private set; } = Vector3.forward;
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => isGroundedForLocomotion;
        public bool IsSprinting { get; private set; }
        public bool IsDashing => dashTimer > 0f;
        public int DashCharges => dashCharges;
        public int MaxDashCharges => maxDashCharges;
        public Vector3 RenderPosition => visualRoot != null && visualRoot != transform && hasVisualPosition
            ? smoothedVisualWorldPosition
            : transform.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            groundProbe = GetComponent<TopDownGroundProbe>();
            resources = GetComponent<PlayerResourceController>();
            visualRoot = ResolveVisualRoot();
            CacheVisualBasePose();
            dashCharges = Mathf.Max(1, maxDashCharges);

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

            IsSprinting = !IsDashing && inputMagnitude > 0.1f && inputAdapter.SprintHeld && CanSprint(Time.fixedDeltaTime);
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
                var currentPosition = body.position;
                var targetPosition = currentPosition + (combinedPlanarVelocity * Time.fixedDeltaTime);
                targetPosition.y = ResolveGroundedBodyPositionY();
                targetPosition = ResolveCollisionAwareGroundedPosition(currentPosition, targetPosition);
                targetPosition = ResolvePenetrationFreePosition(targetPosition);
                var resolvedPlanarDelta = targetPosition - currentPosition;
                resolvedPlanarDelta.y = 0f;
                planarVelocity = Time.fixedDeltaTime > 0f
                    ? resolvedPlanarDelta / Time.fixedDeltaTime
                    : Vector3.zero;
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
            dashChargeRecoveryTimer = 0f;
            dashCharges = Mathf.Max(1, maxDashCharges);
            dashQueued = false;
            airDashConsumed = false;
            externalPlanarVelocity = Vector3.zero;
            hasVisualPosition = false;
            if (visualRoot != null)
            {
                smoothedVisualWorldPosition = transform.TransformPoint(visualBaseLocalPosition);
                visualRoot.position = smoothedVisualWorldPosition;
                hasVisualPosition = true;
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
            maxDashCharges = Mathf.Max(1, maxDashCharges);
            dashChargeRecoveryTime = Mathf.Max(0.01f, dashChargeRecoveryTime);
            wallSkinWidth = Mathf.Max(0f, wallSkinWidth);
        }

        private float ResolveGroundedBodyPositionY()
        {
            var lossyScale = transform.lossyScale;
            var halfHeight = capsuleCollider.height * Mathf.Abs(lossyScale.y) * 0.5f;
            var centerOffset = capsuleCollider.center.y * Mathf.Abs(lossyScale.y);
            var bottomToPivot = halfHeight - centerOffset;
            return groundProbe.GroundPoint.y + bottomToPivot + groundSnapOffset;
        }

        private Vector3 ResolveCollisionAwareGroundedPosition(Vector3 currentPosition, Vector3 targetPosition)
        {
            var planarDelta = targetPosition - currentPosition;
            planarDelta.y = 0f;
            var distance = planarDelta.magnitude;
            if (distance <= 0.0001f || body == null)
            {
                return targetPosition;
            }

            var direction = planarDelta / distance;
            if (!TryGetMovementBlocker(currentPosition, direction, distance + wallSkinWidth, out var hit))
            {
                return targetPosition;
            }

            var allowedDistance = Mathf.Max(0f, hit.distance - wallSkinWidth);
            var resolvedPosition = currentPosition + (direction * Mathf.Min(distance, allowedDistance));
            resolvedPosition.y = targetPosition.y;
            return resolvedPosition;
        }

        private bool TryGetMovementBlocker(Vector3 castPosition, Vector3 direction, float castDistance, out RaycastHit closestHit)
        {
            closestHit = default;
            if (capsuleCollider == null || castDistance <= 0f)
            {
                return false;
            }

            GetCapsuleWorldPoints(castPosition, out var pointA, out var pointB, out var radius);
            var hitCount = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                radius,
                direction,
                movementCastHits,
                castDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            var foundHit = false;
            var closestDistance = float.MaxValue;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = movementCastHits[i];
                movementCastHits[i] = default;

                if (candidate.collider == null || candidate.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (candidate.distance < closestDistance)
                {
                    closestDistance = candidate.distance;
                    closestHit = candidate;
                    foundHit = true;
                }
            }

            return foundHit;
        }

        private Vector3 ResolvePenetrationFreePosition(Vector3 targetPosition)
        {
            if (capsuleCollider == null)
            {
                return targetPosition;
            }

            GetCapsuleWorldPoints(targetPosition, out var pointA, out var pointB, out var radius);
            var boundsCenter = (pointA + pointB) * 0.5f;
            var overlapRadius = Vector3.Distance(pointA, pointB) * 0.5f + radius + wallSkinWidth;
            var hitCount = Physics.OverlapSphereNonAlloc(
                boundsCenter,
                overlapRadius,
                penetrationHits,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            var resolvedPosition = targetPosition;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = penetrationHits[i];
                penetrationHits[i] = null;

                if (candidate == null || candidate.transform.root == transform.root)
                {
                    continue;
                }

                GetCapsuleWorldPoints(resolvedPosition, out pointA, out pointB, out radius);
                if (!Physics.ComputePenetration(
                        capsuleCollider,
                        resolvedPosition,
                        transform.rotation,
                        candidate,
                        candidate.transform.position,
                        candidate.transform.rotation,
                        out var separationDirection,
                        out var separationDistance))
                {
                    continue;
                }

                separationDirection.y = 0f;
                if (separationDirection.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                resolvedPosition += separationDirection.normalized * (separationDistance + wallSkinWidth);
            }

            resolvedPosition.y = targetPosition.y;
            return resolvedPosition;
        }

        private void GetCapsuleWorldPoints(Vector3 bodyPosition, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            var scale = transform.lossyScale;
            var planarScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var verticalScale = Mathf.Abs(scale.y);
            radius = Mathf.Max(0.01f, capsuleCollider.radius * planarScale);
            var height = Mathf.Max(radius * 2f, capsuleCollider.height * verticalScale);
            var center = bodyPosition + Vector3.Scale(capsuleCollider.center, scale);
            var halfSegment = Mathf.Max(0f, (height * 0.5f) - radius);
            pointA = center + (Vector3.up * halfSegment);
            pointB = center - (Vector3.up * halfSegment);
        }

        private void TickTimers(float deltaTime)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
            groundSnapLockTimer = Mathf.Max(0f, groundSnapLockTimer - deltaTime);
            dashTimer = Mathf.Max(0f, dashTimer - deltaTime);
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);
            TickDashChargeRecovery(deltaTime);
        }

        private void UpdateVisualSmoothing()
        {
            if (visualRoot == null || visualRoot == transform)
            {
                return;
            }

            var targetPosition = transform.TransformPoint(visualBaseLocalPosition);

            if (!hasVisualPosition)
            {
                smoothedVisualWorldPosition = targetPosition;
                visualRoot.position = smoothedVisualWorldPosition;
                hasVisualPosition = true;
                return;
            }

            var blend = 1f - Mathf.Exp(-visualPositionSharpness * Time.deltaTime);
            smoothedVisualWorldPosition = Vector3.Lerp(smoothedVisualWorldPosition, targetPosition, blend);
            visualRoot.position = smoothedVisualWorldPosition;
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

            if (dashCharges <= 0)
            {
                return;
            }

            if (!allowAirDash && !isGroundedForLocomotion && coyoteTimer <= 0f)
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
            dashCharges = Mathf.Max(0, dashCharges - 1);
            if (dashCharges < maxDashCharges && dashChargeRecoveryTimer <= 0f)
            {
                dashChargeRecoveryTimer = dashChargeRecoveryTime;
            }

            if (!isGroundedForLocomotion && coyoteTimer <= 0f)
            {
                airDashConsumed = true;
            }
        }

        private bool CanSprint(float deltaTime)
        {
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            return resources == null || resources.TryConsumeSprint(deltaTime);
        }

        private void TickDashChargeRecovery(float deltaTime)
        {
            maxDashCharges = Mathf.Max(1, maxDashCharges);
            if (dashCharges >= maxDashCharges)
            {
                dashCharges = maxDashCharges;
                dashChargeRecoveryTimer = 0f;
                return;
            }

            dashChargeRecoveryTimer -= deltaTime;
            if (dashChargeRecoveryTimer > 0f)
            {
                return;
            }

            dashCharges = Mathf.Min(maxDashCharges, dashCharges + 1);
            dashChargeRecoveryTimer = dashCharges < maxDashCharges ? dashChargeRecoveryTime : 0f;
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

        private Transform ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return visualRoot;
            }

            var childRenderer = GetComponentInChildren<Renderer>();
            if (childRenderer != null && childRenderer.transform != transform)
            {
                return childRenderer.transform;
            }

            var rootRenderer = GetComponent<MeshRenderer>();
            var rootMeshFilter = GetComponent<MeshFilter>();
            if (rootRenderer != null && rootMeshFilter != null && rootMeshFilter.sharedMesh != null)
            {
                var runtimeVisual = transform.Find("RuntimeVisual");
                if (runtimeVisual == null)
                {
                    var runtimeVisualObject = new GameObject("RuntimeVisual");
                    runtimeVisualObject.transform.SetParent(transform, false);
                    runtimeVisualObject.transform.localPosition = Vector3.zero;
                    runtimeVisualObject.transform.localRotation = Quaternion.identity;
                    runtimeVisualObject.transform.localScale = Vector3.one;

                    var runtimeFilter = runtimeVisualObject.AddComponent<MeshFilter>();
                    runtimeFilter.sharedMesh = rootMeshFilter.sharedMesh;

                    var runtimeRenderer = runtimeVisualObject.AddComponent<MeshRenderer>();
                    runtimeRenderer.sharedMaterials = rootRenderer.sharedMaterials;
                    runtimeVisual = runtimeVisualObject.transform;
                }

                rootRenderer.enabled = false;
                return runtimeVisual;
            }

            return visualRoot;
        }

        private void CacheVisualBasePose()
        {
            if (hasVisualBasePose)
            {
                return;
            }

            if (visualRoot == null || visualRoot == transform)
            {
                visualBaseLocalPosition = Vector3.zero;
                hasVisualBasePose = true;
                return;
            }

            visualBaseLocalPosition = transform.InverseTransformPoint(visualRoot.position);
            hasVisualBasePose = true;
        }
    }
}
