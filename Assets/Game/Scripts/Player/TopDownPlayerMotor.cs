using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    public sealed class YellowInspectorLabelAttribute : PropertyAttribute
    {
    }

    public sealed class RedInspectorLabelAttribute : PropertyAttribute
    {
    }

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
        [SerializeField, Min(0f)] private float groundedSlopeSnapDistance = 0.65f;
        [SerializeField, Min(0f)] private float wallSkinWidth = 0.05f;
        [SerializeField, Min(0.01f)] private float visualPositionSharpness = 30f;

        [Header("Jump")]
        [SerializeField, Min(0.1f), YellowInspectorLabel] private float jumpDistance = 6f;
        [SerializeField, Min(0.05f)] private float jumpDuration = 0.5f;
        [SerializeField, Min(0f), RedInspectorLabel] private float jumpArcHeight = 2f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpGroundSnapLockTime = 0.16f;

        [Header("Dash")]
        [SerializeField, Min(0.1f)] private float dashDistance = 6f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.65f;
        [SerializeField, Min(1)] private int maxDashCharges = 2;
        [SerializeField, Min(0.01f)] private float dashChargeRecoveryTime = 5f;
        [SerializeField] private bool allowAirDash = true;
        [SerializeField, Min(0f)] private float dashImpactDamage = 1f;
        [SerializeField, Min(0f)] private float dashImpactImpulse = 3f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float knockbackDamping = 18f;
        [SerializeField, Min(0f)] private float maxExternalPlanarSpeed = 10f;

        [Header("Squash")]
        [SerializeField, Min(0.01f)] private float movementScaleSharpness = 30f;
        [SerializeField, Min(0f)] private float jumpSideSquash = 0.2f;
        [SerializeField, Min(0f)] private float jumpHeightSquash = 0.32f;
        [SerializeField, Min(0f)] private float jumpStretch = 0.12f;
        [SerializeField, Min(0f)] private float dashSideSquash = 0.25f;
        [SerializeField, Range(0f, 0.9f)] private float dashHeightSquash = 0.5f;
        [SerializeField, Min(0.01f)] private float landingSquashDuration = 0.18f;
        [SerializeField, Range(0f, 0.9f)] private float jumpLandingHeightSquash = 0.16f;
        [SerializeField, Min(0f)] private float fallLandingThreshold = 1f;
        [SerializeField, Range(0f, 0.9f)] private float fallLandingHeightSquash = 0.33f;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private TopDownInputAdapter inputAdapter;
        private TopDownGroundProbe groundProbe;
        private PlayerResourceController resources;
        private Vector3 planarVelocity;
        private Vector3 externalPlanarVelocity;
        private Vector3 dashDirection = Vector3.forward;
        private Vector3 jumpDirection = Vector3.forward;
        private Vector3 movementVisualScale = Vector3.one;
        private float verticalVelocity;
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float groundSnapLockTimer;
        private float dashRemainingDistance;
        private float dashCooldownTimer;
        private float dashChargeRecoveryTimer;
        private float jumpRemainingDistance;
        private float jumpBaseBodyY;
        private float landingSquashTimer;
        private float landingHeightSquash;
        private float highestAirY;
        private int dashCharges;
        private bool dashQueued;
        private bool airDashConsumed;
        private bool isJumping;
        private bool isGroundedForLocomotion;
        private bool wasAirborne;
        private bool hasVisualBasePose;
        private bool hasVisualPosition;
        private Vector3 visualBaseLocalPosition;
        private Vector3 smoothedVisualWorldPosition;
        private readonly RaycastHit[] movementCastHits = new RaycastHit[16];
        private readonly Collider[] penetrationHits = new Collider[16];
        private readonly Component[] dashImpactDamageables = new Component[12];
        private int dashImpactCount;

        public Vector3 LastWorldMoveDirection { get; private set; } = Vector3.forward;
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => isGroundedForLocomotion;
        public bool IsSprinting { get; private set; }
        public bool IsDashing => dashRemainingDistance > 0f;
        public int DashCharges => dashCharges;
        public int MaxDashCharges => GetMaxDashCharges();
        public Vector3 MovementVisualScale => movementVisualScale;
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
            dashCharges = GetMaxDashCharges();

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
            UpdateMovementVisualScale(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            groundProbe.Probe();
            TickTimers(Time.fixedDeltaTime);
            RefreshGroundedState();
            TryStartJump();
            TryStartDash();

            if (isJumping || IsDashing)
            {
                UpdateDistanceControlledMovement(Time.fixedDeltaTime);
                return;
            }

            var moveInput = inputAdapter.MoveInput;
            var inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            var desiredDirection = ResolveWorldMoveDirection(moveInput);

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                LastWorldMoveDirection = desiredDirection;
            }

            IsSprinting = inputMagnitude > 0.1f && inputAdapter.SprintHeld && CanSprint(Time.fixedDeltaTime);
            var targetSpeed = (IsSprinting ? sprintSpeed : walkSpeed) * inputMagnitude;
            var targetPlanarVelocity = new Vector3(desiredDirection.x, 0f, desiredDirection.z) * targetSpeed;

            var controlFactor = isGroundedForLocomotion ? 1f : airControlPercent;
            var moveRate = targetPlanarVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(
                planarVelocity,
                targetPlanarVelocity,
                moveRate * controlFactor * Time.fixedDeltaTime);

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
                var groundedStep = combinedPlanarVelocity * Time.fixedDeltaTime;
                groundedStep.y = 0f;
                var targetPosition = ResolveGroundedTargetPosition(currentPosition + groundedStep);
                targetPosition = ResolveCollisionAwareGroundedPosition(currentPosition, targetPosition);
                targetPosition = ResolveGroundedTargetPosition(targetPosition);
                targetPosition = ResolvePenetrationFreePosition(targetPosition);
                targetPosition = ResolveGroundedTargetPosition(targetPosition);
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
            dashRemainingDistance = 0f;
            dashCooldownTimer = 0f;
            dashChargeRecoveryTimer = 0f;
            jumpRemainingDistance = 0f;
            jumpBaseBodyY = transform.position.y;
            landingSquashTimer = 0f;
            landingHeightSquash = 0f;
            dashCharges = GetMaxDashCharges();
            dashQueued = false;
            airDashConsumed = false;
            isJumping = false;
            wasAirborne = false;
            externalPlanarVelocity = Vector3.zero;
            movementVisualScale = Vector3.one;
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
            jumpDistance = Mathf.Max(0.1f, jumpDistance);
            jumpDuration = Mathf.Max(0.05f, jumpDuration);
            jumpArcHeight = Mathf.Max(0f, jumpArcHeight);
            dashDuration = Mathf.Max(0.01f, dashDuration);
            dashDistance = Mathf.Max(0.1f, dashDistance);
            maxDashCharges = Mathf.Max(1, maxDashCharges);
            dashChargeRecoveryTime = Mathf.Max(0.01f, dashChargeRecoveryTime);
            dashImpactDamage = Mathf.Max(0f, dashImpactDamage);
            dashImpactImpulse = Mathf.Max(0f, dashImpactImpulse);
            groundedSlopeSnapDistance = Mathf.Max(0f, groundedSlopeSnapDistance);
            wallSkinWidth = Mathf.Max(0f, wallSkinWidth);
            movementScaleSharpness = Mathf.Max(0.01f, movementScaleSharpness);
            jumpSideSquash = Mathf.Max(0f, jumpSideSquash);
            jumpHeightSquash = Mathf.Max(0f, jumpHeightSquash);
            jumpStretch = Mathf.Max(0f, jumpStretch);
            dashSideSquash = Mathf.Max(0f, dashSideSquash);
            landingSquashDuration = Mathf.Max(0.01f, landingSquashDuration);
            fallLandingThreshold = Mathf.Max(0f, fallLandingThreshold);
        }

        private float ResolveGroundedBodyPositionY()
        {
            return ResolveGroundedBodyPositionY(groundProbe.GroundPoint);
        }

        private float ResolveGroundedBodyPositionY(Vector3 groundPoint)
        {
            var lossyScale = transform.lossyScale;
            var halfHeight = capsuleCollider.height * Mathf.Abs(lossyScale.y) * 0.5f;
            var centerOffset = capsuleCollider.center.y * Mathf.Abs(lossyScale.y);
            var bottomToPivot = halfHeight - centerOffset;
            return groundPoint.y + bottomToPivot + groundSnapOffset;
        }

        private Vector3 ResolveGroundedTargetPosition(Vector3 targetPosition)
        {
            if (groundProbe.TrySampleStableGround(
                    targetPosition,
                    groundedSlopeSnapDistance,
                    out var sampledGroundPoint,
                    out _))
            {
                targetPosition.y = ResolveGroundedBodyPositionY(sampledGroundPoint);
                return targetPosition;
            }

            targetPosition.y = ResolveGroundedBodyPositionY();
            return targetPosition;
        }

        private Vector3 ResolveCollisionAwareGroundedPosition(Vector3 currentPosition, Vector3 targetPosition)
        {
            var movementDelta = targetPosition - currentPosition;
            var distance = movementDelta.magnitude;
            if (distance <= 0.0001f || body == null)
            {
                return targetPosition;
            }

            var direction = movementDelta / distance;
            if (!TryGetMovementBlocker(currentPosition, direction, distance + wallSkinWidth, out var hit))
            {
                return targetPosition;
            }

            TryApplyDashImpact(hit, direction);

            var allowedDistance = Mathf.Max(0f, hit.distance - wallSkinWidth);
            var resolvedPosition = currentPosition + (direction * Mathf.Min(distance, allowedDistance));
            resolvedPosition.y = targetPosition.y;
            return resolvedPosition;
        }

        private void TryApplyDashImpact(RaycastHit hit, Vector3 direction)
        {
            if (!IsDashing || dashImpactDamage <= 0f || hit.collider == null)
            {
                return;
            }

            if (!CombatUtility.TryGetDamageable(hit.collider, out var damageable, out var damageableComponent))
            {
                return;
            }

            if (!damageable.IsAlive || damageable.Team == CombatTeam.Player || CombatUtility.SharesRoot(gameObject, damageableComponent))
            {
                return;
            }

            for (var i = 0; i < dashImpactCount; i++)
            {
                if (dashImpactDamageables[i] == damageableComponent)
                {
                    return;
                }
            }

            if (dashImpactCount < dashImpactDamageables.Length)
            {
                dashImpactDamageables[dashImpactCount] = damageableComponent;
                dashImpactCount++;
            }
            else
            {
                return;
            }

            damageable.ReceiveHit(new CombatHitInfo(
                dashImpactDamage,
                hit.point,
                direction,
                dashImpactImpulse,
                gameObject,
                CombatTeam.Player));
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

                if (groundProbe.IsGroundCollider(candidate.collider)
                    && groundProbe.IsStableSurfaceNormal(candidate.normal))
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
            landingSquashTimer = Mathf.Max(0f, landingSquashTimer - deltaTime);
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

        private void UpdateMovementVisualScale(float deltaTime)
        {
            var targetScale = Vector3.one;
            if (IsDashing)
            {
                targetScale = EvaluateDashSquashScale();
            }
            else if (isJumping)
            {
                targetScale = EvaluateJumpSquashScale();
            }
            else if (landingSquashTimer > 0f)
            {
                targetScale = EvaluateLandingSquashScale();
            }

            var blend = 1f - Mathf.Exp(-movementScaleSharpness * Mathf.Max(0f, deltaTime));
            movementVisualScale = Vector3.Lerp(movementVisualScale, targetScale, blend);
        }

        private void RefreshGroundedState()
        {
            var probeStable = groundProbe.IsStableGround;
            isGroundedForLocomotion = !isJumping && probeStable && groundSnapLockTimer <= 0f && verticalVelocity <= 0.01f;

            if (isGroundedForLocomotion)
            {
                if (wasAirborne)
                {
                    var fallDistance = Mathf.Max(0f, highestAirY - body.position.y);
                    if (fallDistance >= fallLandingThreshold)
                    {
                        TriggerLandingSquash(fallLandingHeightSquash);
                    }
                }

                wasAirborne = false;
                coyoteTimer = coyoteTime;
                airDashConsumed = false;
                if (!IsDashing)
                {
                    verticalVelocity = 0f;
                }
            }
            else
            {
                if (!wasAirborne)
                {
                    highestAirY = body.position.y;
                    wasAirborne = true;
                }
                else
                {
                    highestAirY = Mathf.Max(highestAirY, body.position.y);
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
            jumpDirection = ResolveJumpDirection();
            jumpRemainingDistance = jumpDistance;
            jumpBaseBodyY = groundProbe.IsStableGround
                ? ResolveGroundedBodyPositionY(groundProbe.GroundPoint)
                : body.position.y;
            isJumping = true;
            isGroundedForLocomotion = false;
            planarVelocity = Vector3.zero;
            externalPlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            body.linearVelocity = Vector3.zero;
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
            dashRemainingDistance = dashDistance;
            dashCooldownTimer = dashCooldown;
            planarVelocity = Vector3.zero;
            externalPlanarVelocity = Vector3.zero;
            ClearDashImpactDamageables();
            dashCharges = Mathf.Max(0, dashCharges - 1);
            var effectiveMaxDashCharges = GetMaxDashCharges();
            if (dashCharges < effectiveMaxDashCharges && dashChargeRecoveryTimer <= 0f)
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
            var effectiveMaxDashCharges = GetMaxDashCharges();
            if (dashCharges >= effectiveMaxDashCharges)
            {
                dashCharges = effectiveMaxDashCharges;
                dashChargeRecoveryTimer = 0f;
                return;
            }

            dashChargeRecoveryTimer -= deltaTime;
            if (dashChargeRecoveryTimer > 0f)
            {
                return;
            }

            dashCharges = Mathf.Min(effectiveMaxDashCharges, dashCharges + 1);
            dashChargeRecoveryTimer = dashCharges < effectiveMaxDashCharges ? dashChargeRecoveryTime : 0f;
        }

        private void ClearDashImpactDamageables()
        {
            for (var i = 0; i < dashImpactCount; i++)
            {
                dashImpactDamageables[i] = null;
            }

            dashImpactCount = 0;
        }

        private int GetMaxDashCharges()
        {
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            return Mathf.Max(1, maxDashCharges + (resources != null && resources.HasExtraDashUpgrade ? 1 : 0));
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

        private Vector3 ResolveJumpDirection()
        {
            var desiredDirection = ResolveWorldMoveDirection(inputAdapter.MoveInput);
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                return desiredDirection.normalized;
            }

            return LastWorldMoveDirection.sqrMagnitude > 0.0001f
                ? LastWorldMoveDirection.normalized
                : Vector3.forward;
        }

        private float GetDashSpeed()
        {
            return dashDistance / dashDuration;
        }

        private float GetJumpSpeed()
        {
            return jumpDistance / jumpDuration;
        }

        private void UpdateDistanceControlledMovement(float deltaTime)
        {
            IsSprinting = false;

            externalPlanarVelocity = Vector3.MoveTowards(
                externalPlanarVelocity,
                Vector3.zero,
                knockbackDamping * deltaTime);

            var currentPosition = body.position;
            var plannedPlanarStep = Vector3.zero;
            var jumpStepDistance = 0f;
            var dashStepDistance = 0f;

            if (isJumping)
            {
                jumpStepDistance = Mathf.Min(jumpRemainingDistance, GetJumpSpeed() * deltaTime);
                plannedPlanarStep += jumpDirection * jumpStepDistance;
            }

            if (IsDashing)
            {
                dashStepDistance = Mathf.Min(dashRemainingDistance, GetDashSpeed() * deltaTime);
                plannedPlanarStep += dashDirection * dashStepDistance;
            }

            var targetPosition = currentPosition + plannedPlanarStep;
            targetPosition.y = currentPosition.y;

            if (plannedPlanarStep.sqrMagnitude > 0.000001f)
            {
                targetPosition = ResolveCollisionAwareGroundedPosition(currentPosition, targetPosition);
            }

            var actualPlanarStep = targetPosition - currentPosition;
            actualPlanarStep.y = 0f;
            var blocked = plannedPlanarStep.sqrMagnitude > 0.000001f
                && actualPlanarStep.magnitude + 0.001f < plannedPlanarStep.magnitude;

            if (isJumping)
            {
                jumpRemainingDistance = blocked
                    ? 0f
                    : Mathf.Max(0f, jumpRemainingDistance - jumpStepDistance);
            }

            if (IsDashing)
            {
                dashRemainingDistance = blocked
                    ? 0f
                    : Mathf.Max(0f, dashRemainingDistance - dashStepDistance);
            }

            var groundSamplePosition = targetPosition;
            if (isJumping)
            {
                groundSamplePosition.y = jumpBaseBodyY;
            }

            var hasStableGroundBelow = groundProbe.TrySampleStableGround(
                groundSamplePosition,
                groundedSlopeSnapDistance,
                out var sampledGroundPoint,
                out _);

            if (isJumping)
            {
                var jumpProgress = GetJumpProgress();
                var arcOffset = EvaluateJumpArc(jumpProgress);
                var baseBodyY = hasStableGroundBelow
                    ? ResolveGroundedBodyPositionY(sampledGroundPoint)
                    : jumpBaseBodyY;
                jumpBaseBodyY = baseBodyY;
                targetPosition.y = baseBodyY + arcOffset;

                if (jumpRemainingDistance <= 0.0001f)
                {
                    if (hasStableGroundBelow)
                    {
                        targetPosition.y = ResolveGroundedBodyPositionY(sampledGroundPoint);
                        TriggerLandingSquash(jumpLandingHeightSquash);
                        wasAirborne = false;
                        highestAirY = targetPosition.y;
                    }

                    isJumping = false;
                    verticalVelocity = hasStableGroundBelow ? 0f : -extraFallGravity * deltaTime;
                    groundSnapLockTimer = 0f;
                }
            }
            else if (isGroundedForLocomotion && hasStableGroundBelow)
            {
                targetPosition.y = ResolveGroundedBodyPositionY(sampledGroundPoint);
            }
            else
            {
                verticalVelocity += -extraFallGravity * deltaTime;
                targetPosition.y += verticalVelocity * deltaTime;
            }

            if (!isJumping)
            {
                targetPosition = ResolvePenetrationFreePosition(targetPosition);
            }

            body.linearVelocity = Vector3.zero;
            body.MovePosition(targetPosition);

            planarVelocity = Vector3.zero;
            CurrentSpeed = actualPlanarStep.magnitude / Mathf.Max(0.0001f, deltaTime);
        }

        private float GetJumpProgress()
        {
            return jumpDistance <= 0.0001f
                ? 1f
                : Mathf.Clamp01(1f - (jumpRemainingDistance / jumpDistance));
        }

        private float GetDashProgress()
        {
            return dashDistance <= 0.0001f
                ? 1f
                : Mathf.Clamp01(1f - (dashRemainingDistance / dashDistance));
        }

        private float EvaluateJumpArc(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return jumpArcHeight * 4f * progress * (1f - progress);
        }

        private Vector3 EvaluateJumpSquashScale()
        {
            var progress = GetJumpProgress();
            if (progress < 0.22f)
            {
                var strength = Mathf.Sin((1f - (progress / 0.22f)) * Mathf.PI * 0.5f);
                return new Vector3(
                    1f + (jumpSideSquash * strength),
                    1f - (jumpHeightSquash * strength),
                    1f + (jumpSideSquash * strength));
            }

            var stretchProgress = Mathf.Clamp01((progress - 0.22f) / 0.42f);
            var stretchStrength = Mathf.Sin(stretchProgress * Mathf.PI) * (1f - stretchProgress);
            return new Vector3(
                1f - (jumpStretch * 0.5f * stretchStrength),
                1f + (jumpStretch * stretchStrength),
                1f - (jumpStretch * 0.5f * stretchStrength));
        }

        private Vector3 EvaluateDashSquashScale()
        {
            var progress = GetDashProgress();
            var strength = Mathf.Sin(progress * Mathf.PI);
            return new Vector3(
                1f + (dashSideSquash * strength),
                1f - (dashHeightSquash * strength),
                1f + (dashSideSquash * strength));
        }

        private Vector3 EvaluateLandingSquashScale()
        {
            var progress = 1f - Mathf.Clamp01(landingSquashTimer / landingSquashDuration);
            var strength = Mathf.Sin((1f - progress) * Mathf.PI * 0.5f);
            var sideSquash = landingHeightSquash * 0.5f;
            return new Vector3(
                1f + (sideSquash * strength),
                1f - (landingHeightSquash * strength),
                1f + (sideSquash * strength));
        }

        private void TriggerLandingSquash(float heightSquash)
        {
            landingHeightSquash = Mathf.Clamp01(heightSquash);
            landingSquashTimer = landingSquashDuration;
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
