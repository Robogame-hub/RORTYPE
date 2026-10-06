using RorType.Gameplay.Combat;
using RorType.Gameplay.Environment;
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
        [SerializeField, Min(0.1f), YellowInspectorLabel] private float jumpDistance = 8f;
        [SerializeField, Min(0.05f)] private float jumpDuration = 0.5f;
        [SerializeField, Min(0f), RedInspectorLabel] private float jumpArcHeight = 4f;
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
        private Animator visualAnimator;
        private CapsuleCollider capsuleCollider;
        private PhysicMaterial movementPhysicsMaterial;
        private TopDownInputAdapter inputAdapter;
        private TopDownGroundProbe groundProbe;
        private PlayerResourceController resources;
        private Vector3 planarVelocity;
        private Vector3 externalPlanarVelocity;
        private Vector3 pendingRootMotionDelta;
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
        public Vector3 CurrentWorldMoveDirection { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public Vector3 RequestedWorldMoveDirection => inputAdapter != null
            ? ResolveWorldMoveDirection(inputAdapter.MoveInput)
            : Vector3.zero;
        public bool UsesDirectRootMotion => visualAnimator != null && visualAnimator.applyRootMotion
            && walkSpeed <= 0.01f && sprintSpeed <= 0.01f;
        public bool IsGrounded => isGroundedForLocomotion;
        public bool IsSprinting { get; private set; }
        public float RootMotionPlaybackMultiplier => UsesDirectRootMotion && IsSprinting
            ? rootMotionSprintMultiplier : 1f;
        public bool IsDashing => dashRemainingDistance > 0f;
        public int DashCharges => dashCharges;
        public int MaxDashCharges => GetMaxDashCharges();
        public Vector3 MovementVisualScale => movementVisualScale;
        public Vector3 RenderPosition => visualRoot != null && visualRoot != transform && hasVisualPosition
            ? smoothedVisualWorldPosition
            : transform.position;

        [Header("Root-motion sprint")]
        [SerializeField, Min(1f)] private float rootMotionSprintMultiplier = 1.5f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            groundProbe = GetComponent<TopDownGroundProbe>();
            resources = GetComponent<PlayerResourceController>();
            visualRoot = ResolveVisualRoot();
            visualAnimator = visualRoot != null ? visualRoot.GetComponent<Animator>() : null;
            CacheVisualBasePose();
            dashCharges = GetMaxDashCharges();

            body.useGravity = false;
            body.constraints |= visualRoot != null && visualRoot != transform
                ? RigidbodyConstraints.FreezeRotation
                : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (capsuleCollider.sharedMaterial == null)
            {
                // The motor controls stopping. Contact friction must not catch
                // the capsule on the ground while it is trying to move.
                movementPhysicsMaterial = new PhysicMaterial("Player Movement")
                {
                    staticFriction = 0f,
                    dynamicFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicMaterialCombine.Minimum,
                    bounceCombine = PhysicMaterialCombine.Minimum
                };
                capsuleCollider.sharedMaterial = movementPhysicsMaterial;
            }
        }

        private void Start()
        {
            if (UsesDirectRootMotion)
            {
                // One animation sample per physics tick prevents alternating
                // empty/double movement steps when render and physics rates differ.
                visualAnimator.updateMode = AnimatorUpdateMode.AnimatePhysics;
            }
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

            var rootMotionDelta = pendingRootMotionDelta;
            pendingRootMotionDelta = Vector3.zero;

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
            // Consume animation displacement once per physics step, through the
            // same ground snap and collision resolution as speed-driven movement.
            planarVelocity = UsesDirectRootMotion
                ? rootMotionDelta / Time.fixedDeltaTime
                : Vector3.MoveTowards(
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

            if (combinedPlanarVelocity.sqrMagnitude > 0.0001f)
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
                // Keep this dynamic body in the solver's movement path. Teleporting
                // to the snap target makes contact resolution fight the motor.
                body.velocity = (targetPosition - currentPosition) / Time.fixedDeltaTime;
            }
            else
            {
                verticalVelocity += -extraFallGravity * Time.fixedDeltaTime;
                body.velocity = new Vector3(combinedPlanarVelocity.x, verticalVelocity, combinedPlanarVelocity.z);
            }

            var resolvedPlanarVelocity = useGroundSnap
                ? planarVelocity
                : new Vector3(combinedPlanarVelocity.x, 0f, combinedPlanarVelocity.z);
            CurrentSpeed = resolvedPlanarVelocity.magnitude;
            CurrentWorldMoveDirection = CurrentSpeed > 0.001f
                ? resolvedPlanarVelocity / CurrentSpeed
                : Vector3.zero;
        }

        public void SetMovementReference(Transform reference)
        {
            movementReference = reference;
        }

        public void QueueRootMotion(Vector3 animationDelta)
        {
            if (!isActiveAndEnabled || !UsesDirectRootMotion || isJumping || IsDashing)
            {
                return;
            }

            // The clip supplies planar distance, input supplies direction. Clip Y
            // remains in the skeleton pose; grounding and jumping set body height.
            var direction = ResolvePlanarActionDirection(RequestedWorldMoveDirection);
            var planarDistance = new Vector2(animationDelta.x, animationDelta.z).magnitude;
            var inputMagnitude = inputAdapter != null ? Mathf.Clamp01(inputAdapter.MoveInput.magnitude) : 0f;
            pendingRootMotionDelta += direction * planarDistance * inputMagnitude;
        }

        private void OnDisable()
        {
            pendingRootMotionDelta = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (movementPhysicsMaterial != null)
                Destroy(movementPhysicsMaterial);
        }

        public void ResetMotionState()
        {
            planarVelocity = Vector3.zero;
            CurrentWorldMoveDirection = Vector3.zero;
            CurrentSpeed = 0f;
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
            isJumping = false;
            wasAirborne = false;
            externalPlanarVelocity = Vector3.zero;
            pendingRootMotionDelta = Vector3.zero;
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

        public Vector3 ResolveWorldInputDirection(Vector2 input) => ResolveWorldMoveDirection(input);

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

        private float ResolveGroundedBodyPositionY(
            Vector3 bodyPosition, Vector3 groundPoint, Vector3 groundNormal)
        {
            return bodyPosition.y - groundProbe.GetGroundClearance(bodyPosition, groundPoint, groundNormal)
                + groundSnapOffset;
        }

        private Vector3 ResolveGroundedTargetPosition(Vector3 targetPosition)
        {
            if (groundProbe.TrySampleStableGround(
                    targetPosition,
                    groundedSlopeSnapDistance,
                    out var sampledGroundPoint,
                    out var sampledGroundNormal))
            {
                targetPosition.y = ResolveGroundedBodyPositionY(targetPosition, sampledGroundPoint, sampledGroundNormal);
                return targetPosition;
            }

            targetPosition.y = ResolveGroundedBodyPositionY(targetPosition, groundProbe.GroundPoint, groundProbe.GroundNormal);
            return targetPosition;
        }

        private Vector3 ResolveCollisionAwareGroundedPosition(
            Vector3 currentPosition, Vector3 targetPosition, CapsuleCollider queryCollider = null)
        {
            var remainingDelta = targetPosition - currentPosition;
            if (remainingDelta.sqrMagnitude <= 0.00000001f || body == null)
            {
                return targetPosition;
            }

            if (IsDashing)
            {
                // Capsule casts do not report objects already touching/overlapping
                // the character at the start of the dash step.
                ApplyOverlappingDashImpacts(currentPosition, remainingDelta.normalized, queryCollider);
            }

            var resolvedPosition = currentPosition;
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var distance = remainingDelta.magnitude;
                if (distance <= 0.0001f)
                    break;

                var direction = remainingDelta / distance;
                if (!TryGetMovementBlocker(
                        resolvedPosition, direction, distance + wallSkinWidth, out var hit, queryCollider))
                {
                    resolvedPosition += remainingDelta;
                    break;
                }

                TryApplyDashImpact(hit.collider, hit.point, direction);

                var allowedDistance = Mathf.Clamp(hit.distance - wallSkinWidth, 0f, distance);
                var travelledDelta = direction * allowedDistance;
                resolvedPosition += travelledDelta;
                remainingDelta -= travelledDelta;

                // Distance-controlled actions still stop on impact. Walking keeps
                // the tangential part of the step instead of sticking to the wall.
                if (IsDashing || isJumping)
                    break;

                var wallNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (wallNormal.sqrMagnitude <= 0.0001f)
                    break;

                remainingDelta = Vector3.ProjectOnPlane(remainingDelta, wallNormal.normalized);
            }

            resolvedPosition.y = targetPosition.y;
            return resolvedPosition;
        }

        private void ApplyOverlappingDashImpacts(Vector3 position, Vector3 direction, CapsuleCollider queryCollider)
        {
            GetCapsuleWorldPoints(position, out var pointA, out var pointB, out var radius, queryCollider);
            var overlaps = Physics.OverlapCapsule(
                pointA, pointB, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            var center = (pointA + pointB) * 0.5f;
            for (var i = 0; i < overlaps.Length; i++)
            {
                var candidate = overlaps[i];
                if (candidate == null || candidate.transform.root == transform.root)
                    continue;

                TryApplyDashImpact(candidate, candidate.ClosestPoint(center), direction);
            }
        }

        private void TryApplyDashImpact(Collider hitCollider, Vector3 hitPoint, Vector3 direction)
        {
            if (!IsDashing || hitCollider == null)
            {
                return;
            }

            if (!CombatUtility.TryGetDamageable(hitCollider, out var damageable, out var damageableComponent))
            {
                return;
            }

            if (!damageable.IsAlive || damageable.Team == CombatTeam.Player || CombatUtility.SharesRoot(gameObject, damageableComponent))
            {
                return;
            }

            // Destructible scenery breaks on contact, regardless of its remaining HP
            // or the separate damage value used against enemies.
            var impact = new CombatHitInfo(
                dashImpactDamage, hitPoint, direction, dashImpactImpulse, gameObject, CombatTeam.Player);
            if (damageableComponent is DestructibleCover cover)
            {
                cover.DestroyImmediately(impact);
                return;
            }
            if (damageableComponent is DestructibleLootContainer container)
            {
                container.DestroyImmediately(impact);
                return;
            }
            if (dashImpactDamage <= 0f) return;
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
                hitPoint,
                direction,
                dashImpactImpulse,
                gameObject,
                CombatTeam.Player));
        }

        private bool TryGetMovementBlocker(
            Vector3 castPosition, Vector3 direction, float castDistance, out RaycastHit closestHit,
            CapsuleCollider queryCollider = null)
        {
            closestHit = default;
            if (capsuleCollider == null || castDistance <= 0f)
            {
                return false;
            }

            GetCapsuleWorldPoints(castPosition, out var pointA, out var pointB, out var radius, queryCollider);
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

                // Touching a surface must not block travel parallel to or away from it.
                if (Vector3.Dot(direction, candidate.normal) >= -0.0001f)
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

        private Vector3 ResolvePenetrationFreePosition(Vector3 targetPosition, CapsuleCollider queryCollider = null)
        {
            if (capsuleCollider == null)
            {
                return targetPosition;
            }

            var sourceCollider = queryCollider != null ? queryCollider : capsuleCollider;
            GetCapsuleWorldPoints(targetPosition, out var pointA, out var pointB, out var radius, queryCollider);
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

                if (!Physics.ComputePenetration(
                        sourceCollider,
                        resolvedPosition,
                        sourceCollider.transform.rotation,
                        candidate,
                        candidate.transform.position,
                        candidate.transform.rotation,
                        out var separationDirection,
                        out var separationDistance))
                {
                    continue;
                }

                // Ground snap owns walkable slopes. Flattening their upward
                // separation normal into XZ turns tiny floor contacts into shoves.
                if (groundProbe.IsGroundCollider(candidate)
                    && groundProbe.IsStableSurfaceNormal(separationDirection))
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

        private void GetCapsuleWorldPoints(
            Vector3 bodyPosition, out Vector3 pointA, out Vector3 pointB, out float radius,
            CapsuleCollider queryCollider = null)
        {
            if (queryCollider != null)
            {
                var queryTransform = queryCollider.transform;
                var queryScale = queryTransform.lossyScale;
                var axisIndex = queryCollider.direction;
                var axis = axisIndex == 0 ? Vector3.right : axisIndex == 1 ? Vector3.up : Vector3.forward;
                var axisScale = Mathf.Abs(queryScale[axisIndex]);
                var radiusScale = Mathf.Max(
                    Mathf.Abs(queryScale[(axisIndex + 1) % 3]), Mathf.Abs(queryScale[(axisIndex + 2) % 3]));
                radius = Mathf.Max(0.01f, queryCollider.radius * radiusScale);
                var queryHeight = Mathf.Max(radius * 2f, queryCollider.height * axisScale);
                var queryCenter = bodyPosition + queryTransform.TransformVector(queryCollider.center);
                var segment = queryTransform.TransformDirection(axis) * Mathf.Max(0f, queryHeight * 0.5f - radius);
                pointA = queryCenter + segment;
                pointB = queryCenter - segment;
                return;
            }

            var scale = transform.lossyScale;
            var planarScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var verticalScale = Mathf.Abs(scale.y);
            radius = Mathf.Max(0.01f, capsuleCollider.radius * planarScale);
            var height = Mathf.Max(radius * 2f, capsuleCollider.height * verticalScale);
            var center = bodyPosition + transform.TransformVector(capsuleCollider.center);
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

            if (visualAnimator != null && visualAnimator.applyRootMotion)
            {
                // The motor owns translation; Rigidbody interpolation already
                // smooths the parent. Keep the model anchored to its collider.
                smoothedVisualWorldPosition = transform.TransformPoint(visualBaseLocalPosition);
                visualRoot.position = smoothedVisualWorldPosition;
                hasVisualPosition = true;
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
                ? ResolveGroundedBodyPositionY(body.position, groundProbe.GroundPoint, groundProbe.GroundNormal)
                : body.position.y;
            isJumping = true;
            isGroundedForLocomotion = false;
            planarVelocity = Vector3.zero;
            externalPlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            body.velocity = Vector3.zero;
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
                return ResolvePlanarActionDirection(desiredDirection);
            }

            return ResolvePlanarActionDirection(LastWorldMoveDirection);
        }

        private Vector3 ResolveJumpDirection()
        {
            var desiredDirection = ResolveWorldMoveDirection(inputAdapter.MoveInput);
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                return ResolvePlanarActionDirection(desiredDirection);
            }

            return LastWorldMoveDirection.sqrMagnitude > 0.0001f
                ? ResolvePlanarActionDirection(LastWorldMoveDirection)
                : Vector3.forward;
        }

        private Vector3 ResolvePlanarActionDirection(Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.zero;
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
            plannedPlanarStep.y = 0f;

            if (plannedPlanarStep.sqrMagnitude > 0.000001f)
            {
                targetPosition = ResolveCollisionAwareGroundedPosition(
                    currentPosition, targetPosition);
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
                GetDistanceControlledGroundSampleDistance(plannedPlanarStep),
                out var sampledGroundPoint,
                out var sampledGroundNormal);

            if (isJumping)
            {
                var jumpProgress = GetJumpProgress();
                var arcOffset = EvaluateJumpArc(jumpProgress);
                var baseBodyY = hasStableGroundBelow
                    ? ResolveGroundedBodyPositionY(groundSamplePosition, sampledGroundPoint, sampledGroundNormal)
                    : jumpBaseBodyY;
                jumpBaseBodyY = baseBodyY;
                targetPosition.y = baseBodyY + arcOffset;

                if (jumpRemainingDistance <= 0.0001f)
                {
                    if (hasStableGroundBelow)
                    {
                        targetPosition.y = ResolveGroundedBodyPositionY(groundSamplePosition, sampledGroundPoint, sampledGroundNormal);
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
                targetPosition.y = ResolveGroundedBodyPositionY(groundSamplePosition, sampledGroundPoint, sampledGroundNormal);
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

            body.velocity = (targetPosition - currentPosition) / Mathf.Max(0.0001f, deltaTime);

            planarVelocity = Vector3.zero;
            CurrentSpeed = actualPlanarStep.magnitude / Mathf.Max(0.0001f, deltaTime);
            CurrentWorldMoveDirection = CurrentSpeed > 0.001f
                ? actualPlanarStep.normalized
                : Vector3.zero;
        }

        private float GetDistanceControlledGroundSampleDistance(Vector3 plannedPlanarStep)
        {
            var slopeRise = plannedPlanarStep.magnitude * Mathf.Tan(groundProbe.MaxSlopeAngle * Mathf.Deg2Rad);
            return groundedSlopeSnapDistance + Mathf.Max(0f, slopeRise);
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

            var characterAnimator = GetComponentInChildren<Animator>(true);
            if (characterAnimator != null && characterAnimator.transform != transform)
            {
                return characterAnimator.transform;
            }

            return transform.Find("Visual");
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
