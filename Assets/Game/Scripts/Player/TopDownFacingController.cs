using System;
using RorType.Gameplay.Combat;
using UnityEngine;

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
        [SerializeField] private Transform projectileMuzzle;

        [Header("Aiming")]
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 720f;
        [SerializeField, Min(0.1f)] private float mouseAimRayDistance = 250f;

        [Header("Shooting")]
        [SerializeField] private bool automaticFire = true;
        [SerializeField, Min(0.01f)] private float shotInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 28f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 1.4f;
        [SerializeField, Min(0.1f)] private float projectileMaxDistance = 20f;
        [SerializeField, Min(0.01f)] private float projectileRadius = 0.07f;
        [SerializeField, Min(0f)] private float projectileSpawnForwardOffset = 0.95f;

        [SerializeField] private Color projectileColor = new Color(0.86f, 0.14f, 0.14f);
        [SerializeField, Min(0.01f)] private float projectileStretchMultiplier = 1.65f;
        [SerializeField, Range(0.1f, 1f)] private float projectileSquashMultiplier = 0.74f;
        [SerializeField, Min(0.01f)] private float projectileScaleRecoverySharpness = 10f;
        [SerializeField, Min(0f)] private float projectileDamage = 1f;
        [SerializeField, Min(0f)] private float projectileImpactImpulse = 1f;

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

        [Header("Bolter effects")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject environmentImpactPrefab;
        [SerializeField] private GameObject footstepDustPrefab;
        [SerializeField] private Material boltTrailMaterial;
        [SerializeField, Min(0.01f)] private float muzzleFlashScale = 0.4f;
        [SerializeField, Min(0.01f)] private float impactScale = 0.35f;
        [SerializeField, Min(0.01f)] private float dustScale = 2.2f;
        [SerializeField, Min(0.1f)] private float footstepDistance = 1.3f;
        private float fireClipLength;
        private float footstepTravel;


        private TopDownGroundProbe groundProbe;

        [Header("Bounce")]
        [SerializeField, Min(0.01f)] private float bounceDuration = 0.22f;
        [SerializeField, Min(0f)] private float bounceSideScale = 0.13f;
        [SerializeField, Min(0f)] private float bounceHeightScale = 0.2f;
        [SerializeField, Min(0.01f)] private float bounceScaleSharpness = 24f;

        private TopDownPlayerMotor motor;
        private Rigidbody body;
        private TopDownInputAdapter inputAdapter;
        private PlayerResourceController resources;
        private CapsuleCollider capsuleCollider;
        private float shotCooldownTimer;
        private bool shotQueued;
        private bool projectilePending;
        private Vector3 currentAimDirection = Vector3.forward;
        private Vector3 feedbackBaseLocalScale = Vector3.one;
        private float bounceTimer;
        private bool hasFeedbackBasePose;
        private int upperBodyAnimationLayerIndex = -1;
        private int movementAnimationParameterHash;
        private int fireAnimationWeightParameterHash;
        private int locomotionAnimationFullPathHash;
        private int upperBodyFireBlendFullPathHash;
        private float fireAnimationTimer;
        private bool standingFireActive;
        private static readonly int StandingFireStateHash = Animator.StringToHash("Base Layer.Standing Fire");

        private bool IsMovingForFire()
        {
            return (inputAdapter != null && inputAdapter.HasMovementInput)
                || (motor != null && (motor.CurrentSpeed > 0.05f || motor.IsDashing));
        }

        private void EndStandingFire()
        {
            if (!standingFireActive) return;
            standingFireActive = false;
            characterAnimator.CrossFadeInFixedTime(locomotionAnimationFullPathHash, 0.08f, 0);
            if (upperBodyAnimationLayerIndex >= 0)
                characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 1f);
        }

        private void Awake()
        {
            motor = GetComponent<TopDownPlayerMotor>();
            body = GetComponent<Rigidbody>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            resources = GetComponent<PlayerResourceController>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            groundProbe = GetComponent<TopDownGroundProbe>();
            characterAnimator = ResolveCharacterAnimator();
            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
            {
                foreach (var clip in characterAnimator.runtimeAnimatorController.animationClips)
                    if (clip.name.EndsWith("|Fire", StringComparison.Ordinal) || clip.name == "Fire")
                        fireClipLength = clip.length;
            }
            visualRoot = ResolveVisualRoot();

            projectileMuzzle = ResolveProjectileMuzzle();
            CacheAnimationHashes();
            if (characterAnimator != null)
            {
                // Extract animation travel; CharacterRootMotion steers it on CHARACTER.
                characterAnimator.applyRootMotion = true;
                characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                characterAnimator.speed = animationSpeedMultiplier;
                upperBodyAnimationLayerIndex = characterAnimator.GetLayerIndex(upperBodyAnimationLayer);
                if (upperBodyAnimationLayerIndex >= 0)
                {
                    // Fire Blend Tree uses the masked locomotion pose at FireWeight 0
                    // and blends toward Fire at FireWeight 1.
                    characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 1f);
                }

                characterAnimator.SetFloat(movementAnimationParameterHash, 0f);
                characterAnimator.SetFloat(fireAnimationWeightParameterHash, 0f);
            }

            CacheFeedbackBasePose();
        }

        private void Update()
        {
            if (inputAdapter.FirePressed)
            {
                shotQueued = true;
                inputAdapter.ConsumeFirePressed();
            }

            TickFacingAndAttacks(Time.deltaTime);
            // Supply this frame's input before the Animator evaluates root motion.
            UpdateCharacterAnimation();
        }

        private void LateUpdate()
        {
            UpdateFootstepDust();
            bounceTimer = Mathf.Max(0f, bounceTimer - Time.deltaTime);
            UpdateFeedbackVisual(Time.deltaTime);
            // Spawn after animation evaluation and visual feedback moved the weapon bone.
            if (projectilePending)
            {
                projectilePending = false;
                SpawnProjectile();
            }
        }

        private void TickFacingAndAttacks(float deltaTime)
        {
            shotCooldownTimer = Mathf.Max(0f, shotCooldownTimer - deltaTime);

            var facingDirection = ResolveAimDirection();
            facingDirection.y = 0f;

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                currentAimDirection = facingDirection.normalized;
                var targetRotation = Quaternion.LookRotation(currentAimDirection, Vector3.up);
                var currentRotation = visualRoot != null ? visualRoot.rotation : transform.rotation;
                var nextRotation = Quaternion.RotateTowards(
                    currentRotation,
                    targetRotation,
                    turnSpeedDegrees * Mathf.Max(0f, deltaTime));

                if (visualRoot == null)
                {
                    body.MoveRotation(nextRotation);
                }
                else
                {
                    visualRoot.rotation = nextRotation;
                }
            }

            TryShoot();
        }

        private Vector3 ResolveAimDirection()
        {
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
            var bodyCenter = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            // Direct root motion can move CHARACTER away from the gameplay collider.
            return motor != null && motor.UsesDirectRootMotion && characterAnimator != null
                ? characterAnimator.transform.position + (bodyCenter - transform.position)
                : bodyCenter;
        }

        private void TryShoot()
        {
            var shouldShoot = shotQueued || (automaticFire && inputAdapter.FireHeld);
            if (!shouldShoot || shotCooldownTimer > 0f)
            {
                return;
            }

            shotQueued = false;
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            if (resources != null && !resources.TryConsumeAmmo(1))
            {
                return;
            }

            shotCooldownTimer = ResolveShotCycle();
            TriggerFireAnimation();
            projectilePending = true;
        }

        private void SpawnProjectile()
        {
            var spawnOrigin = ResolveProjectileSpawnOrigin();
            PlayerWeaponVfx.Spawn(muzzleFlashPrefab, projectileMuzzle != null ? projectileMuzzle.position : spawnOrigin,
                Quaternion.LookRotation(currentAimDirection), muzzleFlashScale, projectileMuzzle, 0.25f);
            var effectiveProjectileLifetime = ResolveProjectileLifetime(projectileSpeed, projectileLifetime, projectileMaxDistance);

            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "PlayerProjectile";
            projectile.transform.SetPositionAndRotation(
                spawnOrigin,
                Quaternion.LookRotation(currentAimDirection, Vector3.up));
            projectile.transform.localScale = Vector3.one * (projectileRadius * 2f);

            var projectileCollider = projectile.GetComponent<SphereCollider>();
            var projectileRenderer = projectile.GetComponent<Renderer>();

            // The player's bolt still uses trigger callbacks for hit detection,
            // but must never physically push its owner when it leaves the muzzle.
            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
            }

            projectile.AddComponent<Rigidbody>();
            var projectileSphere = projectile.AddComponent<TopDownProjectileSphere>();

            if (projectileRenderer != null)
            {
                RuntimeRendererUtility.SetColor(projectileRenderer, projectileColor);
            }

            IgnorePlayerCollisions(projectileCollider);

            projectileSphere.Initialize(
                currentAimDirection,
                projectileSpeed,
                effectiveProjectileLifetime,
                projectileStretchMultiplier,
                projectileSquashMultiplier,
                projectileScaleRecoverySharpness,
                GetModifiedDamage(projectileDamage),
                projectileImpactImpulse,
                gameObject,
                CombatTeam.Player);

            projectileSphere.ConfigureBolterEffects(environmentImpactPrefab, impactScale, boltTrailMaterial);
            TriggerBounce();
        }

        private Vector3 ResolveProjectileSpawnOrigin()
        {
            if (projectileMuzzle != null)
            {
                return projectileMuzzle.position;
            }

            var spawnOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            return spawnOrigin + (currentAimDirection * projectileSpawnForwardOffset);
        }

        private void TriggerFireAnimation()
        {
            if (!animateCharacter || characterAnimator == null)
            {
                return;
            }

            fireAnimationTimer = ResolveShotCycle();
            if (!IsMovingForFire())
            {
                standingFireActive = true;
                characterAnimator.Play(StandingFireStateHash, 0, 0f);
                if (upperBodyAnimationLayerIndex >= 0)
                    characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 0f);
            }
            else
            {
                EndStandingFire();
            }
            characterAnimator.SetFloat(fireAnimationWeightParameterHash, 1f);

            if (upperBodyAnimationLayerIndex >= 0)
            {
                // Restart the upper-body blend tree so every shot starts at the
                // beginning of Fire while the base locomotion keeps running.
                characterAnimator.Play(
                    upperBodyFireBlendFullPathHash,
                    upperBodyAnimationLayerIndex,
                    0f);
            }
        }

        private void UpdateCharacterAnimation()
        {
            if (!animateCharacter || characterAnimator == null || !characterAnimator.isActiveAndEnabled)
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

            var targetMoveSpeed = ResolveMovementAnimationValue(isMoving);
            characterAnimator.SetFloat(
                movementAnimationParameterHash,
                targetMoveSpeed,
                movementBlendDamping,
                Time.deltaTime);

            var targetFireWeight = fireAnimationTimer > 0f ? 1f : 0f;
            characterAnimator.SetFloat(
                fireAnimationWeightParameterHash,
                targetFireWeight,
                0.06f,
                Time.deltaTime);
            fireAnimationTimer = Mathf.Max(0f, fireAnimationTimer - Time.deltaTime);

        }

        private float ResolveMovementAnimationValue(bool isMoving)
        {
            if (!isMoving)
            {
                return 0f;
            }

            var movementDirection = motor != null
                ? (motor.UsesDirectRootMotion ? motor.RequestedWorldMoveDirection : motor.CurrentWorldMoveDirection)
                : Vector3.zero;
            movementDirection.y = 0f;

            // A negative value selects the backwards-running branch of the
            // locomotion blend tree whenever movement opposes cursor aim.
            var directionSign = movementDirection.sqrMagnitude > 0.0001f
                && Vector3.Dot(movementDirection.normalized, currentAimDirection) < 0f
                    ? -1f
                    : 1f;

            if (motor == null || motor.WalkSpeed <= 0.01f)
            {
                return directionSign;
            }

            var maximumSpeedRatio = Mathf.Max(1f, motor.SprintSpeed / motor.WalkSpeed);
            var actualSpeedRatio = Mathf.Clamp(
                motor.CurrentSpeed / motor.WalkSpeed,
                0f,
                maximumSpeedRatio);

            return directionSign * actualSpeedRatio;
        }

        private float ResolveShotCycle()
        {
            if (!animateCharacter || characterAnimator == null || upperBodyAnimationLayerIndex < 0)
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

        private void IgnorePlayerCollisions(Collider projectileCollider)
        {
            if (projectileCollider == null)
            {
                return;
            }

            var playerColliders = GetComponentsInChildren<Collider>();
            for (var i = 0; i < playerColliders.Length; i++)
            {
                var playerCollider = playerColliders[i];
                if (playerCollider == null || playerCollider == projectileCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(projectileCollider, playerCollider, true);
            }
        }

        public void ResetFeedbackState()
        {
            standingFireActive = false;
            bounceTimer = 0f;
            shotCooldownTimer = 0f;
            shotQueued = false;

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(movementAnimationParameterHash, 0f);
                characterAnimator.SetFloat(fireAnimationWeightParameterHash, 0f);
                fireAnimationTimer = 0f;

                characterAnimator.Play(locomotionAnimationFullPathHash, 0, 0f);

                if (upperBodyAnimationLayerIndex >= 0)
                {
                    characterAnimator.SetLayerWeight(upperBodyAnimationLayerIndex, 1f);
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

        private void TriggerBounce()
        {
            bounceTimer = bounceDuration;
        }

        private float GetModifiedDamage(float baseDamage)
        {
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            return Mathf.Max(0f, baseDamage) * (resources != null ? resources.DamageMultiplier : 1f);
        }

        private static float ResolveProjectileLifetime(float speed, float configuredLifetime, float maxDistance)
        {
            var effectiveLifetime = Mathf.Max(0.01f, configuredLifetime);
            if (speed <= 0f || maxDistance <= 0f)
            {
                return effectiveLifetime;
            }

            return Mathf.Min(effectiveLifetime, maxDistance / speed);
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

            if (bounceTimer > 0f)
            {
                var progress = 1f - (bounceTimer / bounceDuration);
                targetScale = Vector3.Scale(targetScale, EvaluateBounceScale(progress));
            }

            var scaleBlend = 1f - Mathf.Exp(-bounceScaleSharpness * deltaTime);
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, targetScale, scaleBlend);
        }

        private Vector3 EvaluateBounceScale(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (progress < 0.38f)
            {
                var squashPhase = progress / 0.38f;
                var squashStrength = Mathf.Sin(squashPhase * Mathf.PI * 0.5f);
                return new Vector3(
                    1f + (bounceSideScale * squashStrength),
                    1f - (bounceHeightScale * squashStrength),
                    1f + (bounceSideScale * squashStrength));
            }

            var reboundPhase = (progress - 0.38f) / 0.62f;
            var reboundStrength = Mathf.Sin(reboundPhase * Mathf.PI) * (1f - reboundPhase) * 0.35f;
            return new Vector3(
                1f - (bounceSideScale * reboundStrength),
                1f + (bounceHeightScale * reboundStrength),
                1f - (bounceSideScale * reboundStrength));
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

        private Animator ResolveCharacterAnimator()
        {
            if (characterAnimator != null)
            {
                return characterAnimator;
            }

            return GetComponentInChildren<Animator>(true);
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

        private Transform ResolveProjectileMuzzle()
        {
            if (projectileMuzzle != null)
            {
                return projectileMuzzle;
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
