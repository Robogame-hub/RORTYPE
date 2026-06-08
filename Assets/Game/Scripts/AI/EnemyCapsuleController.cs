using System.Collections;
using RorType.Gameplay.Combat;
using RorType.Gameplay.Player;
using UnityEngine;

namespace RorType.Gameplay.AI
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class EnemyCapsuleController : MonoBehaviour, IDamageable
    {
        [Header("Identity")]
        [SerializeField] private EnemyCapsuleArchetype archetype = EnemyCapsuleArchetype.Shooter;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private EnemyOverheadPresentation overheadPresentation;
        [SerializeField] private Color baseColor = Color.yellow;

        [Header("Vitals")]
        [SerializeField, Min(1f)] private float maxHealth = 5f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 540f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 2.5f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 6f;
        [SerializeField, Min(0f)] private float groundSnapOffset = 0.02f;

        [Header("Shooter")]
        [SerializeField, Min(0.1f)] private float shooterAttackRadius = 9f;
        [SerializeField, Min(0.1f)] private float shooterPreferredDistance = 6f;
        [SerializeField, Min(0.01f)] private float shooterInterval = 2f;
        [SerializeField, Min(0.1f)] private float shooterProjectileSpeed = 18f;
        [SerializeField, Min(0.01f)] private float shooterProjectileLifetime = 1.8f;
        [SerializeField, Min(0.01f)] private float shooterProjectileRadius = 0.18f;
        [SerializeField, Min(0f)] private float shooterProjectileForwardOffset = 0.95f;
        [SerializeField, Min(0f)] private float shooterKnockbackForce = 2.6f;
        [SerializeField] private Color shooterProjectileColor = new Color(1f, 0.86f, 0.18f);

        [Header("Melee")]
        [SerializeField, Min(0.1f)] private float meleeAttackRange = 1.65f;
        [SerializeField, Min(0.01f)] private float meleeInterval = 0.5f;
        [SerializeField, Min(0f)] private float meleeKnockbackForce = 3.15f;
        [SerializeField, Min(0.01f)] private float meleePunchDuration = 0.18f;
        [SerializeField, Min(0f)] private float meleeForwardReach = 0.7f;
        [SerializeField, Min(0f)] private float meleeSideOffset = 0.38f;
        [SerializeField, Min(0f)] private float meleeForwardOffset = 0.46f;
        [SerializeField, Min(0f)] private float meleeHeightOffset = 0.12f;
        [SerializeField, Min(0.01f)] private float meleeFistRadius = 0.2f;
        [SerializeField] private Color meleeFistColor = new Color(1f, 0.62f, 0.14f);
        [SerializeField, Min(0f)] private float meleeFistScaleBoost = 0.1f;
        [SerializeField, Min(0f)] private float meleeFistForwardStretchBoost = 0.16f;

        [Header("Exploder")]
        [SerializeField, Min(0.1f)] private float explodeTriggerRange = 1.9f;
        [SerializeField, Min(1)] private int explodeWarningFlashCount = 3;
        [SerializeField, Min(0.01f)] private float explodeWarningFlashInterval = 0.11f;
        [SerializeField, Min(0.01f)] private float explodeVisualLifetime = 0.16f;
        [SerializeField, Min(0.1f)] private float explodeVisualRadius = 2.6f;
        [SerializeField, Min(0f)] private float explodeKnockbackForce = 4.8f;
        [SerializeField] private Color explodeWarningColor = new Color(1f, 0.16f, 0.16f);

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float bounceDuration = 0.22f;
        [SerializeField, Min(0f)] private float bounceSideScale = 0.12f;
        [SerializeField, Min(0f)] private float bounceHeightScale = 0.18f;
        [SerializeField, Min(0.01f)] private float bounceScaleSharpness = 24f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Min(1)] private int hitFlashCount = 3;
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;
        private const int MeleeFistCount = 2;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private Transform target;
        private TopDownPlayerMotor playerMotor;
        private Material runtimeMaterialInstance;
        private Vector3 feedbackBaseLocalScale = Vector3.one;
        private float currentHealth;
        private float attackCooldownTimer;
        private float bounceTimer;
        private bool isDead;
        private bool isWarningExplosion;
        private Coroutine hitFlashRoutine;
        private Coroutine explosionRoutine;
        private int nextMeleeFistIndex;
        private readonly Transform[] meleeFists = new Transform[MeleeFistCount];
        private readonly float[] meleeFistPunchTimers = new float[MeleeFistCount];

        public CombatTeam Team => CombatTeam.Enemy;
        public bool IsAlive => !isDead;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            if (visualRenderer == null)
            {
                visualRenderer = GetComponentInChildren<Renderer>();
            }

            if (overheadPresentation == null)
            {
                overheadPresentation = GetComponent<EnemyOverheadPresentation>();
                if (overheadPresentation == null)
                {
                    overheadPresentation = gameObject.AddComponent<EnemyOverheadPresentation>();
                }
            }

            if (visualRoot != null)
            {
                feedbackBaseLocalScale = visualRoot.localScale;
            }

            InitializeMaterialInstance();
            currentHealth = maxHealth;
            ApplyRestingColor();
            overheadPresentation?.SetHealth(currentHealth, maxHealth);

            if (archetype == EnemyCapsuleArchetype.Melee)
            {
                EnsureMeleeFistsInitialized();
                UpdateMeleeFistVisuals();
            }
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
            bounceTimer = Mathf.Max(0f, bounceTimer - Time.deltaTime);

            if (archetype == EnemyCapsuleArchetype.Melee)
            {
                for (var i = 0; i < meleeFistPunchTimers.Length; i++)
                {
                    meleeFistPunchTimers[i] = Mathf.Max(0f, meleeFistPunchTimers[i] - Time.deltaTime);
                }
            }

            if (target == null)
            {
                ResolveTarget();
            }

            UpdateFeedbackVisual(Time.deltaTime);
            UpdateMeleeFistVisuals();
        }

        private void FixedUpdate()
        {
            if (isDead)
            {
                return;
            }

            if (target == null)
            {
                ResolveTarget();
                if (target == null)
                {
                    return;
                }
            }

            var toTarget = target.position - body.position;
            toTarget.y = 0f;
            var distanceToTarget = toTarget.magnitude;
            var directionToTarget = distanceToTarget > 0.0001f ? toTarget / distanceToTarget : Vector3.forward;

            RotateVisual(directionToTarget);

            switch (archetype)
            {
                case EnemyCapsuleArchetype.Shooter:
                    TickShooter(distanceToTarget, directionToTarget);
                    break;
                case EnemyCapsuleArchetype.Melee:
                    TickMelee(distanceToTarget, directionToTarget);
                    break;
                case EnemyCapsuleArchetype.Exploder:
                    TickExploder(distanceToTarget, directionToTarget);
                    break;
            }
        }

        public bool ReceiveHit(in CombatHitInfo hitInfo)
        {
            if (isDead || hitInfo.Team == Team)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - hitInfo.Damage);
            overheadPresentation?.SetHealth(currentHealth, maxHealth);
            overheadPresentation?.SpawnDamageNumber(hitInfo.Damage, hitInfo.Point);
            PlayHitFlash();

            if (currentHealth <= 0f)
            {
                Die();
            }

            return true;
        }

        private void TickShooter(float distanceToTarget, Vector3 directionToTarget)
        {
            var shouldAdvance = distanceToTarget > shooterPreferredDistance;
            var shouldRetreat = distanceToTarget < shooterPreferredDistance * 0.65f;
            var moveDirection = shouldAdvance
                ? directionToTarget
                : (shouldRetreat ? -directionToTarget : Vector3.zero);

            MoveOnGround(moveDirection, moveSpeed);

            if (distanceToTarget <= shooterAttackRadius && attackCooldownTimer <= 0f)
            {
                attackCooldownTimer = shooterInterval;
                FireProjectile(directionToTarget);
            }
        }

        private void TickMelee(float distanceToTarget, Vector3 directionToTarget)
        {
            if (distanceToTarget > meleeAttackRange)
            {
                MoveOnGround(directionToTarget, moveSpeed);
                return;
            }

            MoveOnGround(Vector3.zero, 0f);
            if (attackCooldownTimer <= 0f)
            {
                attackCooldownTimer = meleeInterval;
                TriggerMeleePunch(nextMeleeFistIndex);
                nextMeleeFistIndex = (nextMeleeFistIndex + 1) % MeleeFistCount;
                TryApplyPlayerKnockback(directionToTarget, meleeKnockbackForce, meleeAttackRange + 0.55f);
            }
        }

        private void TickExploder(float distanceToTarget, Vector3 directionToTarget)
        {
            if (isWarningExplosion)
            {
                MoveOnGround(Vector3.zero, 0f);
                return;
            }

            if (distanceToTarget > explodeTriggerRange)
            {
                MoveOnGround(directionToTarget, moveSpeed);
                return;
            }

            explosionRoutine = StartCoroutine(PlayExplosionWarningRoutine());
        }

        private void FireProjectile(Vector3 directionToTarget)
        {
            var spawnOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            spawnOrigin += directionToTarget * shooterProjectileForwardOffset;

            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = $"{name}_Projectile";
            projectile.transform.SetPositionAndRotation(
                spawnOrigin,
                Quaternion.LookRotation(directionToTarget, Vector3.up));
            projectile.transform.localScale = Vector3.one * (shooterProjectileRadius * 2f);

            var projectileCollider = projectile.GetComponent<SphereCollider>();
            var projectileRenderer = projectile.GetComponent<Renderer>();
            projectile.AddComponent<Rigidbody>();
            var projectileSphere = projectile.AddComponent<EnemyProjectileSphere>();

            if (projectileRenderer != null)
            {
                projectileRenderer.material.color = shooterProjectileColor;
            }

            IgnoreOwnCollisions(projectileCollider);
            projectileSphere.Initialize(
                directionToTarget,
                shooterProjectileSpeed,
                shooterProjectileLifetime,
                1.6f,
                0.74f,
                10f,
                shooterKnockbackForce,
                transform.root);

            TriggerAttackBounce();
        }

        private IEnumerator PlayExplosionWarningRoutine()
        {
            isWarningExplosion = true;
            attackCooldownTimer = 0f;

            for (var i = 0; i < explodeWarningFlashCount; i++)
            {
                SetVisualColor(explodeWarningColor);
                yield return new WaitForSeconds(explodeWarningFlashInterval);
                ApplyRestingColor();
                yield return new WaitForSeconds(explodeWarningFlashInterval);
            }

            TriggerAttackBounce();
            ApplyExplosionKnockback();
            SpawnExplosionVisual();
            Die();
        }

        private void SpawnExplosionVisual()
        {
            var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = $"{name}_Explosion";
            explosion.transform.position = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            explosion.transform.localScale = Vector3.one * 0.1f;

            var collider = explosion.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = explosion.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = explodeWarningColor;
            }

            var effect = explosion.AddComponent<TransientScaleEffect>();
            effect.Initialize(
                Vector3.one * 0.1f,
                Vector3.one * explodeVisualRadius,
                explodeVisualLifetime);
        }

        private void MoveOnGround(Vector3 moveDirection, float speed)
        {
            var planarDirection = moveDirection;
            planarDirection.y = 0f;
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                planarDirection.Normalize();
            }

            var nextPosition = body.position + (planarDirection * speed * Time.fixedDeltaTime);
            nextPosition.y = ResolveGroundedBodyPositionY(nextPosition);
            body.MovePosition(nextPosition);
        }

        private void RotateVisual(Vector3 directionToTarget)
        {
            if (directionToTarget.sqrMagnitude <= 0.0001f || visualRoot == null)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(
                visualRoot.rotation,
                targetRotation,
                turnSpeedDegrees * Time.fixedDeltaTime);
        }

        private float ResolveGroundedBodyPositionY(Vector3 referencePosition)
        {
            var probeMask = groundMask.value == 0 ? Physics.DefaultRaycastLayers : groundMask.value;
            var rayOrigin = referencePosition + (Vector3.up * groundProbeHeight);
            if (!Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out var hit,
                    groundProbeHeight + groundProbeDistance,
                    probeMask,
                    QueryTriggerInteraction.Ignore))
            {
                return body.position.y;
            }

            return hit.point.y + GetBottomToPivotDistance() + groundSnapOffset;
        }

        private float GetBottomToPivotDistance()
        {
            if (capsuleCollider == null)
            {
                return 1f;
            }

            var lossyScale = transform.lossyScale;
            var halfHeight = capsuleCollider.height * Mathf.Abs(lossyScale.y) * 0.5f;
            var centerOffset = capsuleCollider.center.y * Mathf.Abs(lossyScale.y);
            return halfHeight - centerOffset;
        }

        private void ResolveTarget()
        {
            playerMotor = Object.FindFirstObjectByType<TopDownPlayerMotor>();
            target = playerMotor != null ? playerMotor.transform : null;
        }

        private void PlayHitFlash()
        {
            if (hitFlashRoutine != null)
            {
                StopCoroutine(hitFlashRoutine);
            }

            hitFlashRoutine = StartCoroutine(PlayHitFlashRoutine());
        }

        private IEnumerator PlayHitFlashRoutine()
        {
            for (var i = 0; i < hitFlashCount; i++)
            {
                SetVisualColor(hitFlashColor);
                yield return new WaitForSeconds(hitFlashInterval);
                ApplyRestingColor();
                yield return new WaitForSeconds(hitFlashInterval);
            }

            hitFlashRoutine = null;
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            if (hitFlashRoutine != null)
            {
                StopCoroutine(hitFlashRoutine);
                hitFlashRoutine = null;
            }

            if (explosionRoutine != null)
            {
                StopCoroutine(explosionRoutine);
                explosionRoutine = null;
            }

            Destroy(gameObject);
        }

        private void TriggerAttackBounce()
        {
            bounceTimer = bounceDuration;
        }

        private void UpdateFeedbackVisual(float deltaTime)
        {
            if (visualRoot == null)
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
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, scaleBlend);
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

        private void InitializeMaterialInstance()
        {
            if (visualRenderer == null)
            {
                return;
            }

            runtimeMaterialInstance = visualRenderer.material;
        }

        private void ApplyRestingColor()
        {
            SetVisualColor(baseColor);
        }

        private void SetVisualColor(Color color)
        {
            if (runtimeMaterialInstance != null)
            {
                runtimeMaterialInstance.color = color;
            }
        }

        private void IgnoreOwnCollisions(Collider projectileCollider)
        {
            if (projectileCollider == null)
            {
                return;
            }

            var colliders = GetComponentsInChildren<Collider>();
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i] == projectileCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(projectileCollider, colliders[i], true);
            }
        }

        private void TriggerMeleePunch(int fistIndex)
        {
            EnsureMeleeFistsInitialized();
            if (fistIndex < 0 || fistIndex >= meleeFists.Length || meleeFists[fistIndex] == null)
            {
                TriggerAttackBounce();
                return;
            }

            meleeFistPunchTimers[fistIndex] = meleePunchDuration;
            TriggerAttackBounce();
        }

        private void UpdateMeleeFistVisuals()
        {
            if (archetype != EnemyCapsuleArchetype.Melee)
            {
                return;
            }

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
            if (archetype != EnemyCapsuleArchetype.Melee)
            {
                return;
            }

            var parent = visualRoot != null ? visualRoot : transform;
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
                Destroy(fistCollider);
            }

            var fistRenderer = fist.GetComponent<Renderer>();
            if (fistRenderer != null)
            {
                fistRenderer.material.color = meleeFistColor;
            }

            return fist.transform;
        }

        private Vector3 GetMeleeBaseLocalPosition(int fistIndex)
        {
            var side = fistIndex == 0 ? -meleeSideOffset : meleeSideOffset;
            return new Vector3(side, meleeHeightOffset, meleeForwardOffset);
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

        private bool TryApplyPlayerKnockback(Vector3 direction, float force, float maxDistance)
        {
            if (force <= 0f)
            {
                return false;
            }

            if (playerMotor == null)
            {
                ResolveTarget();
                if (playerMotor == null)
                {
                    return false;
                }
            }

            var enemyCenter = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            var offsetToPlayer = playerMotor.transform.position - enemyCenter;
            offsetToPlayer.y = 0f;
            if (maxDistance > 0f && offsetToPlayer.magnitude > maxDistance)
            {
                return false;
            }

            var knockbackDirection = direction;
            knockbackDirection.y = 0f;
            if (knockbackDirection.sqrMagnitude <= 0.0001f)
            {
                knockbackDirection = offsetToPlayer.sqrMagnitude > 0.0001f ? offsetToPlayer.normalized : transform.forward;
            }

            playerMotor.ApplyKnockback(knockbackDirection, force);
            return true;
        }

        private void ApplyExplosionKnockback()
        {
            if (playerMotor == null)
            {
                ResolveTarget();
            }

            if (playerMotor == null)
            {
                return;
            }

            var explosionCenter = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            var directionToPlayer = playerMotor.transform.position - explosionCenter;
            TryApplyPlayerKnockback(directionToPlayer, explodeKnockbackForce, explodeVisualRadius);
        }
    }
}
