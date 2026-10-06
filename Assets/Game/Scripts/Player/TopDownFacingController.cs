using System;
using RorType.Gameplay.AI;
using RorType.Gameplay.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(TopDownPlayerMotor))]
    [RequireComponent(typeof(TopDownInputAdapter))]
    [RequireComponent(typeof(PlayerResourceController))]
    public sealed class TopDownFacingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform feedbackTransform;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private RuntimeAnimatorController characterAnimatorController;
        [FormerlySerializedAs("projectileMuzzle")]
        [SerializeField] private Transform shotMuzzle;

        [Header("Aiming")]
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 720f;
        [SerializeField, Range(0f, 30f)] private float firstShotAlignmentDegrees = 3f;
        [SerializeField, Min(0.1f)] private float mouseAimRayDistance = 250f;
        [SerializeField, Min(1f)] private float weaponTurnSpeedDegrees = 360f;
        [SerializeField, Min(1f)] private float gamepadAimTurnSpeedDegrees = 720f;
        [SerializeField, Min(1f)] private float gamepadAimDistance = 12f;
        [SerializeField, Range(0f, 30f)] private float aimSlowdownConeDegrees = 6f;
        [SerializeField, Range(0.1f, 1f)] private float aimSlowdownFactor = 0.45f;

        [Header("Weapon preparation and accuracy")]
        [SerializeField, Min(0f)] private float aimPreparationSeconds = 0.22f;
        [SerializeField, Min(0.01f)] private float stabilizationSeconds = 0.8f;
        [SerializeField, Range(1f, 180f)] private float sharpTurnDegrees = 45f;
        [SerializeField, Range(0f, 20f)] private float settledSpreadDegrees = 0.35f;
        [SerializeField, Range(0f, 30f)] private float movingSpreadDegrees = 3f;
        [SerializeField, Range(0f, 45f)] private float unstableSpreadDegrees = 6f;
        [SerializeField, Range(0f, 1f)] private float shotInstability = 0.12f;
        [SerializeField, Range(0f, 1f)] private float movingStabilityLimit = 0.45f;

        [Header("Magazine")]
        [SerializeField, Min(1)] private int magazineCapacity = 30;
        [SerializeField, Min(0.01f)] private float reloadDuration = 2f;

        [Header("Shooting")]
        [SerializeField] private bool automaticFire = true;
        [SerializeField, Min(0.01f)] private float shotInterval = 0.18f;
        [FormerlySerializedAs("projectileMaxDistance")]
        [SerializeField, Min(0.1f)] private float shotMaxDistance = 20f;
        [SerializeField] private LayerMask shotHitMask = Physics.DefaultRaycastLayers;
        [FormerlySerializedAs("projectileSpawnForwardOffset")]
        [SerializeField, Min(0f)] private float shotOriginForwardOffset = 0.95f;
        [FormerlySerializedAs("projectileDamage")]
        [SerializeField, Min(0f)] private float shotDamage = 1f;
        [FormerlySerializedAs("projectileImpactImpulse")]
        [SerializeField, Min(0f)] private float shotImpactImpulse = 1f;

        [Header("Character animation")]
        [SerializeField] private bool animateCharacter = true;
        [SerializeField, Min(0.01f)] private float animationSpeedMultiplier = 1f;
        [SerializeField] private string movementAnimationParameter = "MoveSpeed";
        [SerializeField] private string fireAnimationWeightParameter = "FireWeight";
        [SerializeField] private string locomotionAnimationState = "Locomotion";
        [SerializeField] private string upperBodyAnimationLayer = "Upper Body";
        [SerializeField] private string upperBodyFireBlendState = "Fire Overlay";
        [SerializeField, Min(0f)] private float movementBlendDamping = 0.12f;
        [SerializeField, Min(0.01f)] private float fireAnimationDuration = 0.42f;

        [Header("Movement effects")]
        [SerializeField] private GameObject footstepDustPrefab;
        [SerializeField, Min(0.01f)] private float dustScale = 2.2f;
        [SerializeField, Min(0.1f)] private float footstepDistance = 1.3f;
        private float fireClipLength;
        private float footstepTravel;


        private TopDownGroundProbe groundProbe;

        [Header("Visual feedback")]
        [FormerlySerializedAs("bounceScaleSharpness")]
        [SerializeField, Min(0.01f)] private float feedbackScaleSharpness = 24f;

        private TopDownPlayerMotor motor;
        private Rigidbody body;
        private TopDownInputAdapter inputAdapter;
        private PlayerResourceController resources;
        private PlayerSkillController skills;
        private CapsuleCollider capsuleCollider;
        private float shotCooldownTimer;
        private bool shotQueued;
        private bool shotPending;
        private RaycastHit[] shotRayHits = new RaycastHit[32];
        private bool weaponAimActive;
        private bool waitingForFirstShot;
        private float preparationTimer;
        private float stability;
        private float reloadTimer;
        private float pendingSpreadAngle;
        private Vector3 currentAimDirection = Vector3.forward;
        private Vector3 feedbackBaseLocalScale = Vector3.one;
        private bool hasFeedbackBasePose;
        private int upperBodyAnimationLayerIndex = -1;
        private int movementAnimationParameterHash;
        private static readonly int MoveStrafeParameterHash = Animator.StringToHash("MoveStrafe");
        private int fireAnimationWeightParameterHash;
        private int locomotionAnimationFullPathHash;
        private int upperBodyFireBlendFullPathHash;
        private float fireAnimationTimer;
        private bool standingFireActive;
        private static readonly int StandingFireStateHash = Animator.StringToHash("Base Layer.Standing Fire");

        public Vector3 CurrentAimDirection => currentAimDirection.sqrMagnitude > 0.0001f
            ? currentAimDirection.normalized
            : Vector3.forward;
        public Vector3 AimOrigin => ResolveAimOrigin();
        public Transform FacingVisualRoot => visualRoot != null ? visualRoot : transform;
        public bool IsReloading => reloadTimer > 0f;
        public float ReloadRemaining => reloadTimer;
        public int MagazineAmmo => resources != null ? resources.MagazineAmmo : 0;
        public int ReserveAmmo => resources != null ? Mathf.Max(0, resources.Ammo - MagazineAmmo) : 0;
        public bool IsInCombatStance => inputAdapter != null && inputAdapter.AimHeld;
        public bool IsFiring => shotQueued || shotPending || fireAnimationTimer > 0f;
        public float ShotSpreadDegrees => Mathf.Lerp(
            inputAdapter != null && inputAdapter.HasMovementInput ? movingSpreadDegrees : settledSpreadDegrees,
            unstableSpreadDegrees, 1f - stability);
        public float AimReadiness => weaponAimActive && !IsReloading && !waitingForFirstShot
            ? stability : 0f;
        public Vector3 ActualAimDirection => Vector3.ProjectOnPlane(FacingVisualRoot.forward, Vector3.up).normalized;
        public Vector3 ShotOrigin => ResolveShotOrigin();
        public Vector3 AimMarkerPoint => ShotOrigin + CurrentAimDirection * gamepadAimDistance;

        private void EndStandingFire()
        {
            if (!standingFireActive) return;
            standingFireActive = false;
            characterAnimator.CrossFadeInFixedTime(locomotionAnimationFullPathHash, 0.08f, 0);
            if (upperBodyAnimationLayerIndex >= 0)
                characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 0f);
        }

        private void Awake()
        {
            NormalizeWeaponSettings();
            motor = GetComponent<TopDownPlayerMotor>();
            body = GetComponent<Rigidbody>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            resources = GetComponent<PlayerResourceController>();
            skills = GetComponent<PlayerSkillController>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            groundProbe = GetComponent<TopDownGroundProbe>();
            characterAnimator = ResolveCharacterAnimator();
            EnsureCharacterAnimatorController();
            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
            {
                foreach (var clip in characterAnimator.runtimeAnimatorController.animationClips)
                    if (clip.name.EndsWith("|Fire", StringComparison.Ordinal) || clip.name == "Fire")
                        fireClipLength = clip.length;
            }
            visualRoot = ResolveVisualRoot();

            shotMuzzle = ResolveShotMuzzle();
            CacheAnimationHashes();
            if (EnsureCharacterAnimatorController())
            {
                // CharacterRootMotion forwards animation travel to the physics motor.
                characterAnimator.applyRootMotion = true;
                characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                characterAnimator.speed = animationSpeedMultiplier;
                upperBodyAnimationLayerIndex = characterAnimator.GetLayerIndex(upperBodyAnimationLayer);
                if (upperBodyAnimationLayerIndex >= 0)
                {
                    // Locomotion comes entirely from the base layer. The masked
                    // upper-body layer contributes only while firing or fading out.
                    characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 0f);
                }

                characterAnimator.SetFloat(movementAnimationParameterHash, 0f);
                characterAnimator.SetFloat(MoveStrafeParameterHash, 0f);
                characterAnimator.SetFloat(fireAnimationWeightParameterHash, 0f);
            }

            CacheFeedbackBasePose();
            currentAimDirection = ActualAimDirection;
        }

        private void Start() => resources.InitializeMagazine(magazineCapacity);

        private void Update()
        {
            if (characterAnimator != null)
                characterAnimator.speed = animationSpeedMultiplier
                    * (inputAdapter.AimHeld || IsFiring
                        ? 1f : motor.RootMotionPlaybackMultiplier);
            if (inputAdapter.ReloadPressed)
            {
                inputAdapter.ConsumeReloadPressed();
                if (!IsReloading && MagazineAmmo < magazineCapacity && ReserveAmmo > 0)
                {
                    reloadTimer = reloadDuration;
                    shotQueued = false;
                    preparationTimer = stability = 0f;
                    fireAnimationTimer = 0f;
                }
            }
            if (!IsInCombatStance || inputAdapter.CombatInputBlocked || IsReloading
                || (skills != null && skills.IsSelectingPoint))
            {
                shotQueued = false;
            }
            else if (inputAdapter.FirePressed)
            {
                shotQueued = true;
                inputAdapter.ConsumeFirePressed();
            }
            inputAdapter.ConsumeFirePressed();

            TickFacingAndAttacks(Time.deltaTime);
            // Supply this frame's input before the Animator evaluates root motion.
            UpdateCharacterAnimation();
        }

        private void LateUpdate()
        {
            UpdateFootstepDust();
            UpdateFeedbackVisual(Time.deltaTime);
            // Resolve the hit in the shot's frame, using the evaluated weapon pose.
            if (shotPending)
            {
                shotPending = false;
                ApplyShotHit();
            }
        }

        private void TickFacingAndAttacks(float deltaTime)
        {
            shotCooldownTimer = Mathf.Max(0f, shotCooldownTimer - deltaTime);
            fireAnimationTimer = Mathf.Max(0f, fireAnimationTimer - deltaTime);
            if (IsReloading)
            {
                reloadTimer = Mathf.Max(0f, reloadTimer - deltaTime);
                if (!IsReloading) resources.ReloadMagazine(magazineCapacity);
            }

            var previousAim = currentAimDirection;
            currentAimDirection = ResolveAimDirection();
            currentAimDirection.y = 0f;
            if (currentAimDirection.sqrMagnitude > 0.0001f)
            {
                currentAimDirection.Normalize();
            }

            var hasAmmo = MagazineAmmo > 0;
            if (!hasAmmo)
            {
                shotQueued = false;
            }
            var canAimWeapon = IsInCombatStance && !inputAdapter.CombatInputBlocked
                && (skills == null || !skills.IsSelectingPoint);
            if (canAimWeapon && !weaponAimActive)
            {
                // A brief click stays queued until the body finishes aiming.
                waitingForFirstShot = true;
                preparationTimer = stability = 0f;
            }
            else if (!canAimWeapon)
            {
                waitingForFirstShot = false;
            }
            weaponAimActive = canAimWeapon;
            if (!weaponAimActive || inputAdapter.SprintHeld || motor.IsDashing
                || Vector3.Angle(previousAim, currentAimDirection) >= sharpTurnDegrees)
            {
                preparationTimer = stability = 0f;
                waitingForFirstShot = true;
            }

            // Outside aiming, every movement direction uses a forward run.
            // With no movement, retain the last body rotation.
            var facingDirection = IsInCombatStance
                ? currentAimDirection : motor.RequestedWorldMoveDirection;
            facingDirection.y = 0f;

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                var currentRotation = visualRoot != null ? visualRoot.rotation : transform.rotation;
                var nextRotation = Quaternion.RotateTowards(
                    currentRotation,
                    targetRotation,
                    (IsInCombatStance ? weaponTurnSpeedDegrees : turnSpeedDegrees) * Mathf.Max(0f, deltaTime));

                if (visualRoot == null)
                {
                    body.MoveRotation(nextRotation);
                }
                else
                {
                    visualRoot.rotation = nextRotation;
                }
            }

            var aligned = Vector3.Angle(ActualAimDirection, currentAimDirection) <= firstShotAlignmentDegrees;
            if (weaponAimActive && aligned && !IsReloading && !motor.IsDashing && !inputAdapter.SprintHeld)
            {
                preparationTimer += deltaTime;
                stability = Mathf.MoveTowards(stability, inputAdapter.HasMovementInput ? movingStabilityLimit : 1f,
                    deltaTime / Mathf.Max(0.01f, stabilizationSeconds));
                waitingForFirstShot = preparationTimer < aimPreparationSeconds;
            }
            else
            {
                preparationTimer = 0f;
                stability = Mathf.MoveTowards(stability, 0f, deltaTime / Mathf.Max(0.01f, stabilizationSeconds));
                waitingForFirstShot = true;
            }

            TryShoot();
        }

        private Vector3 ResolveAimDirection()
        {
            if (skills != null && skills.IsSelectingPoint) return currentAimDirection;
            if (inputAdapter.UsesGamepad)
            {
                if (inputAdapter.AimInput.sqrMagnitude <= 0.0001f) return currentAimDirection;
                var desired = Vector3.ProjectOnPlane(motor.ResolveWorldInputDirection(inputAdapter.AimInput), Vector3.up).normalized;
                var speed = gamepadAimTurnSpeedDegrees * inputAdapter.AimInput.magnitude;
                if ((IsInCombatStance || IsFiring) && IsAimNearEnemy()) speed *= aimSlowdownFactor;
                return Vector3.RotateTowards(currentAimDirection, desired,
                    speed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            }
            var aimOrigin = ResolveAimOrigin();
            var currentCamera = Camera.main;
            if (currentCamera != null)
            {
                var aimRay = currentCamera.ScreenPointToRay(inputAdapter.MouseScreenPosition);
                var aimPlane = new Plane(Vector3.up, aimOrigin);
                if (aimPlane.Raycast(aimRay, out var enter))
                {
                    var worldAimPoint = aimRay.GetPoint(Mathf.Min(enter, mouseAimRayDistance));
                    var directionToAim = worldAimPoint - aimOrigin;
                    directionToAim.y = 0f;
                    if (directionToAim.sqrMagnitude > 0.0001f)
                    {
                        return directionToAim.normalized;
                    }
                }
            }

            var fallbackDirection = motor.LastWorldMoveDirection;
            fallbackDirection.y = 0f;
            if (fallbackDirection.sqrMagnitude > 0.0001f)
            {
                return fallbackDirection.normalized;
            }

            return currentAimDirection;
        }

        public bool TryGetAimPoint(out Vector3 worldAimPoint)
        {
            var aimOrigin = ResolveAimOrigin();
            if (inputAdapter.UsesGamepad)
            {
                worldAimPoint = AimMarkerPoint;
                return true;
            }
            var currentCamera = Camera.main;
            if (currentCamera != null)
            {
                var aimRay = currentCamera.ScreenPointToRay(inputAdapter.MouseScreenPosition);
                var aimPlane = new Plane(Vector3.up, aimOrigin);
                if (aimPlane.Raycast(aimRay, out var enter))
                {
                    worldAimPoint = aimRay.GetPoint(Mathf.Min(enter, mouseAimRayDistance));
                    return true;
                }
            }

            worldAimPoint = aimOrigin + currentAimDirection;
            return false;
        }

        private Vector3 ResolveAimOrigin()
        {
            // Disabled root colliders have empty bounds; their authored geometry
            // still defines the body center while the visual capsule handles contact.
            return capsuleCollider != null
                ? capsuleCollider.transform.TransformPoint(capsuleCollider.center)
                : transform.position;
        }

        private bool IsAimNearEnemy()
        {
            var enemies = EnemyCapsuleController.ActiveEnemyInstances;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                var direction = Vector3.ProjectOnPlane(enemy.VisionCenter - AimOrigin, Vector3.up);
                if (direction.sqrMagnitude <= shotMaxDistance * shotMaxDistance
                    && Vector3.Angle(currentAimDirection, direction) <= aimSlowdownConeDegrees)
                {
                    var line = enemy.VisionCenter - AimOrigin;
                    if (line.sqrMagnitude > 0.0001f
                        && TryGetBlockingHit(AimOrigin, line.normalized, line.magnitude, out var hit)
                        && hit.collider.transform.IsChildOf(enemy.transform)) return true;
                }
            }
            return false;
        }

        private void TryShoot()
        {
            var shouldShoot = shotQueued || (automaticFire && inputAdapter.FireHeld);
            if (!shouldShoot || shotCooldownTimer > 0f || !IsInCombatStance || !weaponAimActive
                || waitingForFirstShot || IsReloading || motor.IsDashing || inputAdapter.SprintHeld
                || MagazineAmmo <= 0 || (skills != null && skills.IsSelectingPoint))
            {
                return;
            }

            shotQueued = false;
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            if (resources != null && !resources.TryConsumeMagazineRound())
            {
                return;
            }

            waitingForFirstShot = false;
            pendingSpreadAngle = UnityEngine.Random.Range(-ShotSpreadDegrees, ShotSpreadDegrees);
            stability = Mathf.Max(0f, stability - shotInstability);
            shotCooldownTimer = ResolveShotCycle();
            TriggerFireAnimation();
            shotPending = true;
        }

        private void ApplyShotHit()
        {
            var shotOrigin = ResolveShotOrigin();
            var shotDirection = Quaternion.Euler(0f, pendingSpreadAngle, 0f) * ResolveShotDirection(shotOrigin);
            // A barrel extending through cover must not bypass that cover.
            var bodyOrigin = AimOrigin;
            bodyOrigin.y = shotOrigin.y;
            var barrelOffset = shotOrigin - bodyOrigin;
            if (barrelOffset.sqrMagnitude > 0.0001f
                && TryGetBlockingHit(bodyOrigin, barrelOffset.normalized, barrelOffset.magnitude, out var barrelHit))
            {
                ApplyDamage(barrelHit, shotDirection);
                return;
            }
            if (TryGetBlockingHit(shotOrigin, shotDirection, shotMaxDistance, out var hit))
                ApplyDamage(hit, shotDirection);
        }

        private bool TryGetBlockingHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            var hitCount = Physics.RaycastNonAlloc(
                origin, direction, shotRayHits, distance,
                shotHitMask, QueryTriggerInteraction.Collide);

            // NonAlloc results are unordered. Grow only on saturation so the
            // closest blocking hit cannot be omitted by a full buffer.
            while (hitCount == shotRayHits.Length)
            {
                Array.Resize(ref shotRayHits, shotRayHits.Length * 2);
                hitCount = Physics.RaycastNonAlloc(
                    origin, direction, shotRayHits, distance,
                    shotHitMask, QueryTriggerInteraction.Collide);
            }

            var closestHitIndex = -1;
            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = shotRayHits[i];
                var hitCollider = candidate.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform)
                    || candidate.distance >= closestDistance)
                {
                    continue;
                }

                // Interaction/pickup volumes do not stop shots. Damageable
                // trigger colliders remain valid targets.
                if (hitCollider.isTrigger
                    && !CombatUtility.TryGetDamageable(hitCollider, out _, out _))
                {
                    continue;
                }

                closestHitIndex = i;
                closestDistance = candidate.distance;
            }

            if (closestHitIndex < 0)
            {
                hit = default;
                return false;
            }

            hit = shotRayHits[closestHitIndex];
            return true;
        }

        private void ApplyDamage(RaycastHit hit, Vector3 shotDirection)
        {
            if (CombatUtility.TryGetDamageable(hit.collider, out var damageable, out _)
                && damageable.IsAlive && damageable.Team != CombatTeam.Player)
            {
                damageable.ReceiveHit(new CombatHitInfo(
                    GetModifiedDamage(shotDamage), hit.point, shotDirection,
                    shotImpactImpulse, gameObject, CombatTeam.Player));
            }
        }

        private Vector3 ResolveShotDirection(Vector3 shotOrigin)
        {
            if (inputAdapter.UsesGamepad) return CurrentAimDirection;
            if (TryGetAimPoint(out var aimPoint))
            {
                var directionToAim = aimPoint - shotOrigin;
                directionToAim.y = 0f;
                if (directionToAim.sqrMagnitude > 0.0001f)
                {
                    return directionToAim.normalized;
                }
            }

            return currentAimDirection.sqrMagnitude > 0.0001f
                ? currentAimDirection.normalized
                : Vector3.forward;
        }

        private Vector3 ResolveShotOrigin()
        {
            if (shotMuzzle != null)
            {
                return shotMuzzle.position;
            }

            return ResolveAimOrigin() + (currentAimDirection * shotOriginForwardOffset);
        }

        private void TriggerFireAnimation()
        {
            if (!animateCharacter || !EnsureCharacterAnimatorController())
            {
                return;
            }

            fireAnimationTimer = ResolveShotCycle();
            if (upperBodyAnimationLayerIndex >= 0)
            {
                EndStandingFire();
                characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 1f);
                characterAnimator.SetFloat(fireAnimationWeightParameterHash, 1f);

                // Restart only the shot clip; the base locomotion phase keeps running.
                characterAnimator.Play(
                    upperBodyFireBlendFullPathHash,
                    upperBodyAnimationLayerIndex,
                    0f);

                return;
            }

            standingFireActive = true;
            characterAnimator.Play(StandingFireStateHash, 0, 0f);
        }

        private void UpdateCharacterAnimation()
        {
            if (!animateCharacter || !EnsureCharacterAnimatorController() || !characterAnimator.isActiveAndEnabled)
            {
                return;
            }

            var isMoving = inputAdapter != null && inputAdapter.HasMovementInput;
            if (!isMoving && motor != null)
            {
                // Use the motor as a second source so animation cannot miss a
                // movement frame because Update/FixedUpdate ran in another order.
                isMoving = motor.CurrentSpeed > 0.05f || motor.IsDashing;
            }

            if (standingFireActive && (isMoving || fireAnimationTimer <= 0f))
                EndStandingFire();

            var targetMovement = ResolveMovementAnimationValues(isMoving);
            // Do not retain side/backward blend values after leaving aiming.
            var movementDamping = IsInCombatStance ? movementBlendDamping : 0f;
            characterAnimator.SetFloat(
                movementAnimationParameterHash,
                targetMovement.y,
                movementDamping,
                Time.deltaTime);
            characterAnimator.SetFloat(
                MoveStrafeParameterHash,
                targetMovement.x,
                movementDamping,
                Time.deltaTime);

            var targetFireWeight = fireAnimationTimer > 0f ? 1f : 0f;
            characterAnimator.SetFloat(
                fireAnimationWeightParameterHash,
                targetFireWeight,
                0.06f,
                Time.deltaTime);
            if (upperBodyAnimationLayerIndex >= 0)
            {
                // Fade back to the current base-layer pose, not a separately
                // restarted copy of locomotion on the upper-body layer.
                var fireWeight = characterAnimator.GetFloat(fireAnimationWeightParameterHash);
                characterAnimator.SetLayerWeight(
                    upperBodyAnimationLayerIndex, fireWeight < 0.001f ? 0f : fireWeight);
            }
        }

        private Vector2 ResolveMovementAnimationValues(bool isMoving)
        {
            if (!isMoving)
            {
                return Vector2.zero;
            }

            var movementDirection = motor != null
                ? (motor.UsesDirectRootMotion ? motor.RequestedWorldMoveDirection : motor.CurrentWorldMoveDirection)
                : Vector3.zero;
            movementDirection.y = 0f;

            if (movementDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            var speedRatio = 1f;
            if (motor != null && motor.WalkSpeed > 0.01f)
            {
                var maximumSpeedRatio = Mathf.Max(1f, motor.SprintSpeed / motor.WalkSpeed);
                speedRatio = Mathf.Clamp(motor.CurrentSpeed / motor.WalkSpeed, 0f, maximumSpeedRatio);
            }

            if (!IsInCombatStance)
            {
                return new Vector2(0f, speedRatio);
            }

            // Side and backward steps are available only while aiming.
            var forward = visualRoot != null ? visualRoot.forward : transform.forward;
            forward.y = 0f;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var direction = movementDirection.normalized;
            var localMovement = new Vector2(
                Vector3.Dot(direction, right),
                Vector3.Dot(direction, forward));

            return localMovement * speedRatio;
        }

        private float ResolveShotCycle()
        {
            if (!animateCharacter || !EnsureCharacterAnimatorController() || upperBodyAnimationLayerIndex < 0)
                return Mathf.Max(shotInterval, fireAnimationDuration);
            var state = characterAnimator.GetCurrentAnimatorStateInfo(upperBodyAnimationLayerIndex);
            var playback = Mathf.Max(0.01f, characterAnimator.speed * Mathf.Abs(state.speed * state.speedMultiplier));
            return Mathf.Max(shotInterval, (fireClipLength > 0f ? fireClipLength : fireAnimationDuration) / playback);
        }

        private void UpdateFootstepDust()
        {
            if (motor == null || groundProbe == null || !motor.IsGrounded || motor.IsDashing || motor.CurrentSpeed < 0.15f)
            {
                footstepTravel = 0f;
                return;
            }
            footstepTravel += motor.CurrentSpeed * Time.deltaTime;
            if (footstepTravel < footstepDistance) return;
            footstepTravel %= footstepDistance;
            // A ring around the body stays visible outside the boots and silhouette.
            var point = groundProbe.GroundPoint + groundProbe.GroundNormal * 0.1f;
            PlayerWeaponVfx.Spawn(footstepDustPrefab, point,
                Quaternion.FromToRotation(Vector3.forward, groundProbe.GroundNormal), dustScale, null, 2f);
        }
        private void CacheAnimationHashes()
        {
            movementAnimationParameterHash = Animator.StringToHash(movementAnimationParameter);
            fireAnimationWeightParameterHash = Animator.StringToHash(fireAnimationWeightParameter);
            locomotionAnimationFullPathHash = Animator.StringToHash($"Base Layer.{locomotionAnimationState}");
            upperBodyFireBlendFullPathHash = Animator.StringToHash($"{upperBodyAnimationLayer}.{upperBodyFireBlendState}");
        }

        public void ResetFeedbackState()
        {
            standingFireActive = false;
            shotCooldownTimer = 0f;
            shotQueued = false;
            shotPending = false;
            weaponAimActive = false;
            waitingForFirstShot = false;
            reloadTimer = preparationTimer = stability = 0f;
            if (skills != null) skills.CancelPointSelection();
            if (inputAdapter != null) inputAdapter.SuppressFireUntilRelease();
            fireAnimationTimer = 0f;

            if (EnsureCharacterAnimatorController())
            {
                characterAnimator.SetFloat(movementAnimationParameterHash, 0f);
                characterAnimator.SetFloat(MoveStrafeParameterHash, 0f);
                characterAnimator.SetFloat(fireAnimationWeightParameterHash, 0f);

                characterAnimator.Play(locomotionAnimationFullPathHash, 0, 0f);

                if (upperBodyAnimationLayerIndex >= 0)
                {
                    characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 0f);
                    characterAnimator.Play(upperBodyFireBlendFullPathHash, upperBodyAnimationLayerIndex, 0f);
                }
            }

            var targetTransform = ResolveFeedbackTransform();
            if (!hasFeedbackBasePose || targetTransform == null)
            {
                return;
            }

            targetTransform.localScale = feedbackBaseLocalScale;
        }

        private float GetModifiedDamage(float baseDamage)
        {
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            return Mathf.Max(0f, baseDamage) * (resources != null ? resources.DamageMultiplier : 1f);
        }

        private void UpdateFeedbackVisual(float deltaTime)
        {
            CacheFeedbackBasePose();
            var targetTransform = ResolveFeedbackTransform();
            if (!hasFeedbackBasePose || targetTransform == null)
            {
                return;
            }

            var targetScale = feedbackBaseLocalScale;
            if (motor != null)
            {
                targetScale = Vector3.Scale(targetScale, motor.MovementVisualScale);
            }

            var scaleBlend = 1f - Mathf.Exp(-feedbackScaleSharpness * deltaTime);
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, targetScale, scaleBlend);
        }

        private void CacheFeedbackBasePose()
        {
            var targetTransform = ResolveFeedbackTransform();
            if (targetTransform == null || hasFeedbackBasePose)
            {
                return;
            }

            feedbackBaseLocalScale = targetTransform.localScale;
            hasFeedbackBasePose = true;
        }

        private Transform ResolveFeedbackTransform()
        {
            if (feedbackTransform != null)
            {
                return feedbackTransform;
            }

            if (visualRoot != null)
            {
                return visualRoot;
            }

            return transform;
        }

        private void OnValidate() => NormalizeWeaponSettings();

        private void NormalizeWeaponSettings()
        {
            magazineCapacity = Mathf.Max(1, magazineCapacity);
            reloadDuration = Mathf.Max(0.01f, reloadDuration);
            aimPreparationSeconds = Mathf.Max(0f, aimPreparationSeconds);
            stabilizationSeconds = Mathf.Max(0.01f, stabilizationSeconds);
            weaponTurnSpeedDegrees = Mathf.Max(1f, weaponTurnSpeedDegrees);
            gamepadAimTurnSpeedDegrees = Mathf.Max(1f, gamepadAimTurnSpeedDegrees);
            gamepadAimDistance = Mathf.Max(1f, gamepadAimDistance);
            movingSpreadDegrees = Mathf.Max(settledSpreadDegrees, movingSpreadDegrees);
            unstableSpreadDegrees = Mathf.Max(movingSpreadDegrees, unstableSpreadDegrees);
        }

        private void OnDisable()
        {
            shotQueued = shotPending = weaponAimActive = false;
            reloadTimer = preparationTimer = stability = 0f;
            if (inputAdapter != null) inputAdapter.SuppressFireUntilRelease();
        }

        private Animator ResolveCharacterAnimator()
        {
            if (characterAnimator != null)
            {
                return characterAnimator;
            }

            return GetComponentInChildren<Animator>(true);
        }

        private bool EnsureCharacterAnimatorController()
        {
            if (characterAnimator == null)
            {
                return false;
            }

            if (characterAnimator.runtimeAnimatorController == null && characterAnimatorController != null)
            {
                characterAnimator.runtimeAnimatorController = characterAnimatorController;
                upperBodyAnimationLayerIndex = characterAnimator.GetLayerIndex(upperBodyAnimationLayer);
                if (upperBodyAnimationLayerIndex >= 0)
                {
                    characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 0f);
                }
            }

            return characterAnimator.runtimeAnimatorController != null;
        }

        private Transform ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return visualRoot;
            }

            if (characterAnimator != null && characterAnimator.transform != transform)
            {
                return characterAnimator.transform;
            }

            var childRenderer = GetComponentInChildren<Renderer>(true);
            if (childRenderer != null && childRenderer.transform != transform)
            {
                return childRenderer.transform;
            }

            return null;
        }

        private Transform ResolveShotMuzzle()
        {
            if (shotMuzzle != null)
            {
                return shotMuzzle;
            }

            var transforms = GetComponentsInChildren<Transform>(true);
            var preferredNames = new[]
            {
                "Muzzle",
                "MuzzlePoint",
                "FirePoint",
                "BarrelEnd",
                "Barrel",
                "Bolter",
                "BoltGun"
            };

            for (var nameIndex = 0; nameIndex < preferredNames.Length; nameIndex++)
            {
                for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    if (string.Equals(transforms[transformIndex].name, preferredNames[nameIndex], StringComparison.OrdinalIgnoreCase))
                    {
                        return transforms[transformIndex];
                    }
                }
            }

            return null;
        }
    }
}
