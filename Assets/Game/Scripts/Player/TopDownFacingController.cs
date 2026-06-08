using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(TopDownPlayerMotor))]
    [RequireComponent(typeof(TopDownInputAdapter))]
    public sealed class TopDownFacingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform feedbackTransform;

        [Header("Aiming")]
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 720f;
        [SerializeField, Min(0.1f)] private float mouseAimRayDistance = 250f;

        [Header("Shooting")]
        [SerializeField] private bool automaticFire = true;
        [SerializeField, Min(0.01f)] private float shotInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 28f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 1.4f;
        [SerializeField, Min(0.1f)] private float projectileMaxDistance = 20f;
        [SerializeField, Min(0.01f)] private float projectileRadius = 0.2f;
        [SerializeField, Min(0f)] private float projectileSpawnForwardOffset = 0.95f;
        [SerializeField] private Color projectileColor = new Color(0.86f, 0.14f, 0.14f);
        [SerializeField, Min(0.01f)] private float projectileStretchMultiplier = 1.65f;
        [SerializeField, Range(0.1f, 1f)] private float projectileSquashMultiplier = 0.74f;
        [SerializeField, Min(0.01f)] private float projectileScaleRecoverySharpness = 10f;
        [SerializeField, Min(0f)] private float projectileDamage = 1f;
        [SerializeField, Min(0f)] private float projectileImpactImpulse = 1f;

        [Header("Melee")]
        [SerializeField] private bool automaticMelee = true;
        [SerializeField, Min(0.01f)] private float meleeInterval = 0.16f;
        [SerializeField, Min(0.01f)] private float meleePunchDuration = 0.18f;
        [SerializeField, Min(0f)] private float meleeForwardReach = 0.72f;
        [SerializeField, Min(0f)] private float meleeSideOffset = 0.42f;
        [SerializeField, Min(0f)] private float meleeForwardOffset = 0.5f;
        [SerializeField, Min(0f)] private float meleeHeightOffset = 0.12f;
        [SerializeField, Min(0.01f)] private float meleeFistRadius = 0.2f;
        [SerializeField] private Color meleeFistColor = new Color(0.86f, 0.14f, 0.14f);
        [SerializeField, Min(0f)] private float meleeFistScaleBoost = 0.12f;
        [SerializeField, Min(0f)] private float meleeFistForwardStretchBoost = 0.18f;
        [SerializeField, Min(0f)] private float meleeDamage = 2f;
        [SerializeField, Min(0f)] private float meleeImpactImpulse = 2f;
        [SerializeField, Min(1)] private int meleeHitBufferSize = 12;

        [Header("Bounce")]
        [SerializeField, Min(0.01f)] private float bounceDuration = 0.22f;
        [SerializeField, Min(0f)] private float bounceSideScale = 0.13f;
        [SerializeField, Min(0f)] private float bounceHeightScale = 0.2f;
        [SerializeField, Min(0.01f)] private float bounceScaleSharpness = 24f;

        private const int MeleeFistCount = 2;
        private const float MeleeImpactProgress = 0.28f;

        private TopDownPlayerMotor motor;
        private Rigidbody body;
        private TopDownInputAdapter inputAdapter;
        private CapsuleCollider capsuleCollider;
        private float shotCooldownTimer;
        private bool shotQueued;
        private float meleeCooldownTimer;
        private bool meleeQueued;
        private int nextMeleeFistIndex;
        private Vector3 currentAimDirection = Vector3.forward;
        private Vector3 feedbackBaseLocalScale = Vector3.one;
        private float bounceTimer;
        private bool hasFeedbackBasePose;
        private readonly Transform[] meleeFists = new Transform[MeleeFistCount];
        private readonly float[] meleeFistPunchTimers = new float[MeleeFistCount];
        private readonly bool[] meleeFistImpactApplied = new bool[MeleeFistCount];
        private Collider[] meleeHitBuffer;
        private Component[] meleeUniqueHitBuffer;

        private void Awake()
        {
            motor = GetComponent<TopDownPlayerMotor>();
            body = GetComponent<Rigidbody>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            visualRoot = ResolveVisualRoot();
            CacheFeedbackBasePose();
            EnsureMeleeFistsInitialized();
            EnsureMeleeHitBuffers();
        }

        private void Update()
        {
            if (inputAdapter.FirePressed)
            {
                shotQueued = true;
                inputAdapter.ConsumeFirePressed();
            }

            if (inputAdapter.MeleePressed)
            {
                meleeQueued = true;
                inputAdapter.ConsumeMeleePressed();
            }
        }

        private void LateUpdate()
        {
            bounceTimer = Mathf.Max(0f, bounceTimer - Time.deltaTime);
            for (var i = 0; i < meleeFistPunchTimers.Length; i++)
            {
                TryResolveMeleeImpact(i);
                meleeFistPunchTimers[i] = Mathf.Max(0f, meleeFistPunchTimers[i] - Time.deltaTime);
            }

            UpdateFeedbackVisual(Time.deltaTime);
            UpdateMeleeFistVisuals();
        }

        private void FixedUpdate()
        {
            shotCooldownTimer = Mathf.Max(0f, shotCooldownTimer - Time.fixedDeltaTime);
            meleeCooldownTimer = Mathf.Max(0f, meleeCooldownTimer - Time.fixedDeltaTime);

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
                    turnSpeedDegrees * Time.fixedDeltaTime);

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
            TryMeleeAttack();
        }

        private Vector3 ResolveAimDirection()
        {
            var aimOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
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
            var aimOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
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

        private void TryShoot()
        {
            var shouldShoot = shotQueued || (automaticFire && inputAdapter.FireHeld);
            if (!shouldShoot || shotCooldownTimer > 0f)
            {
                return;
            }

            shotQueued = false;
            shotCooldownTimer = shotInterval;
            SpawnProjectile();
        }

        private void TryMeleeAttack()
        {
            var shouldPunch = meleeQueued || (automaticMelee && inputAdapter.MeleeHeld);
            if (!shouldPunch || meleeCooldownTimer > 0f)
            {
                return;
            }

            meleeQueued = false;
            meleeCooldownTimer = meleeInterval;
            TriggerMeleePunch(nextMeleeFistIndex);
            nextMeleeFistIndex = (nextMeleeFistIndex + 1) % MeleeFistCount;
        }

        private void SpawnProjectile()
        {
            var spawnOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            spawnOrigin += currentAimDirection * projectileSpawnForwardOffset;
            var effectiveProjectileLifetime = ResolveProjectileLifetime(projectileSpeed, projectileLifetime, projectileMaxDistance);

            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "PlayerProjectile";
            projectile.transform.SetPositionAndRotation(
                spawnOrigin,
                Quaternion.LookRotation(currentAimDirection, Vector3.up));
            projectile.transform.localScale = Vector3.one * (projectileRadius * 2f);

            var projectileCollider = projectile.GetComponent<SphereCollider>();
            var projectileRenderer = projectile.GetComponent<Renderer>();
            projectile.AddComponent<Rigidbody>();
            var projectileSphere = projectile.AddComponent<TopDownProjectileSphere>();

            if (projectileRenderer != null)
            {
                projectileRenderer.material.color = projectileColor;
            }

            IgnorePlayerCollisions(projectileCollider);

            projectileSphere.Initialize(
                currentAimDirection,
                projectileSpeed,
                effectiveProjectileLifetime,
                projectileStretchMultiplier,
                projectileSquashMultiplier,
                projectileScaleRecoverySharpness,
                projectileDamage,
                projectileImpactImpulse,
                gameObject,
                CombatTeam.Player);

            TriggerBounce();
        }

        private void TriggerMeleePunch(int fistIndex)
        {
            EnsureMeleeFistsInitialized();
            if (fistIndex < 0 || fistIndex >= meleeFists.Length || meleeFists[fistIndex] == null)
            {
                return;
            }

            meleeFistPunchTimers[fistIndex] = meleePunchDuration;
            meleeFistImpactApplied[fistIndex] = false;
            TriggerBounce();
        }

        private void TryResolveMeleeImpact(int fistIndex)
        {
            if (fistIndex < 0 || fistIndex >= meleeFistPunchTimers.Length || meleeFistImpactApplied[fistIndex])
            {
                return;
            }

            if (meleePunchDuration <= 0f || meleeFistPunchTimers[fistIndex] <= 0f)
            {
                return;
            }

            var progress = 1f - (meleeFistPunchTimers[fistIndex] / meleePunchDuration);
            if (progress < MeleeImpactProgress)
            {
                return;
            }

            meleeFistImpactApplied[fistIndex] = true;
            ApplyMeleeHit(fistIndex);
        }

        private void UpdateMeleeFistVisuals()
        {
            EnsureMeleeFistsInitialized();
            for (var i = 0; i < meleeFists.Length; i++)
            {
                var fist = meleeFists[i];
                if (fist == null)
                {
                    continue;
                }

                var basePosition = GetMeleeBaseLocalPosition(i);
                var baseScale = Vector3.one * (meleeFistRadius * 2f);
                var punchAmount = 0f;
                if (meleePunchDuration > 0f && meleeFistPunchTimers[i] > 0f)
                {
                    var progress = 1f - (meleeFistPunchTimers[i] / meleePunchDuration);
                    punchAmount = EvaluatePunchAmount(progress);
                }

                fist.localPosition = basePosition + (Vector3.forward * (meleeForwardReach * punchAmount));
                fist.localRotation = Quaternion.identity;
                fist.localScale = new Vector3(
                    baseScale.x * (1f + (meleeFistScaleBoost * punchAmount)),
                    baseScale.y * (1f + (meleeFistScaleBoost * punchAmount)),
                    baseScale.z * (1f + ((meleeFistScaleBoost + meleeFistForwardStretchBoost) * punchAmount)));
            }
        }

        private void EnsureMeleeFistsInitialized()
        {
            var parent = ResolveCombatVisualParent();
            for (var i = 0; i < meleeFists.Length; i++)
            {
                if (meleeFists[i] != null && meleeFists[i].parent == parent)
                {
                    continue;
                }

                if (meleeFists[i] != null)
                {
                    Destroy(meleeFists[i].gameObject);
                }

                meleeFists[i] = CreateMeleeFist(parent, i);
            }
        }

        private Transform CreateMeleeFist(Transform parent, int fistIndex)
        {
            var fist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fist.name = fistIndex == 0 ? "LeftMeleeFist" : "RightMeleeFist";
            fist.transform.SetParent(parent, false);

            var fistCollider = fist.GetComponent<Collider>();
            if (fistCollider != null)
            {
                fistCollider.enabled = false;
                Destroy(fistCollider);
            }

            var fistRenderer = fist.GetComponent<Renderer>();
            if (fistRenderer != null)
            {
                fistRenderer.material.color = meleeFistColor;
            }

            return fist.transform;
        }

        private void ApplyMeleeHit(int fistIndex)
        {
            EnsureMeleeHitBuffers();
            var attackPoint = GetMeleeAttackWorldPosition(fistIndex);
            var hitCount = Physics.OverlapSphereNonAlloc(
                attackPoint,
                meleeFistRadius,
                meleeHitBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            var uniqueHitCount = 0;
            for (var i = 0; i < hitCount; i++)
            {
                var hitCollider = meleeHitBuffer[i];
                if (hitCollider == null || hitCollider.transform.root == transform.root)
                {
                    continue;
                }

                if (!CombatUtility.TryGetDamageable(hitCollider, out var damageable, out var damageableComponent))
                {
                    continue;
                }

                if (!damageable.IsAlive || damageable.Team == CombatTeam.Player)
                {
                    continue;
                }

                var alreadyHit = false;
                for (var uniqueIndex = 0; uniqueIndex < uniqueHitCount; uniqueIndex++)
                {
                    if (meleeUniqueHitBuffer[uniqueIndex] == damageableComponent)
                    {
                        alreadyHit = true;
                        break;
                    }
                }

                if (alreadyHit)
                {
                    continue;
                }

                meleeUniqueHitBuffer[uniqueHitCount] = damageableComponent;
                uniqueHitCount++;

                damageable.ReceiveHit(new CombatHitInfo(
                    meleeDamage,
                    attackPoint,
                    currentAimDirection,
                    meleeImpactImpulse,
                    gameObject,
                    CombatTeam.Player));
            }

            for (var i = 0; i < hitCount; i++)
            {
                meleeHitBuffer[i] = null;
            }

            for (var i = 0; i < uniqueHitCount; i++)
            {
                meleeUniqueHitBuffer[i] = null;
            }
        }

        private Vector3 GetMeleeBaseLocalPosition(int fistIndex)
        {
            var side = fistIndex == 0 ? -meleeSideOffset : meleeSideOffset;
            return new Vector3(side, meleeHeightOffset, meleeForwardOffset);
        }

        private Vector3 GetMeleeAttackWorldPosition(int fistIndex)
        {
            var parent = ResolveCombatVisualParent();
            var localPoint = GetMeleeBaseLocalPosition(fistIndex) + (Vector3.forward * meleeForwardReach);
            return parent.TransformPoint(localPoint);
        }

        private float EvaluatePunchAmount(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress < 0.28f)
            {
                var extendPhase = progress / 0.28f;
                return Mathf.Sin(extendPhase * Mathf.PI * 0.5f);
            }

            var retractPhase = (progress - 0.28f) / 0.72f;
            return Mathf.Cos(retractPhase * Mathf.PI * 0.5f);
        }

        private void TriggerBounce()
        {
            bounceTimer = bounceDuration;
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
            bounceTimer = 0f;
            shotCooldownTimer = 0f;
            shotQueued = false;
            meleeCooldownTimer = 0f;
            meleeQueued = false;
            nextMeleeFistIndex = 0;
            for (var i = 0; i < meleeFistPunchTimers.Length; i++)
            {
                meleeFistPunchTimers[i] = 0f;
                meleeFistImpactApplied[i] = false;
            }

            var targetTransform = ResolveFeedbackTransform();

            if (!hasFeedbackBasePose || targetTransform == null)
            {
                return;
            }

            targetTransform.localScale = feedbackBaseLocalScale;
            UpdateMeleeFistVisuals();
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
            if (bounceTimer > 0f)
            {
                var progress = 1f - (bounceTimer / bounceDuration);
                targetScale = Vector3.Scale(feedbackBaseLocalScale, EvaluateBounceScale(progress));
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

        private void EnsureMeleeHitBuffers()
        {
            var bufferSize = Mathf.Max(1, meleeHitBufferSize);
            if (meleeHitBuffer == null || meleeHitBuffer.Length != bufferSize)
            {
                meleeHitBuffer = new Collider[bufferSize];
                meleeUniqueHitBuffer = new Component[bufferSize];
            }
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

        private Transform ResolveCombatVisualParent()
        {
            if (visualRoot != null)
            {
                return visualRoot;
            }

            return transform;
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
                    rootRenderer.enabled = false;
                    runtimeVisual = runtimeVisualObject.transform;
                }

                return runtimeVisual;
            }

            return visualRoot;
        }
    }
}
