using System.Collections;
using RorType.Gameplay.Combat;
using RorType.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RorType.Gameplay.AI
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyCapsuleController : MonoBehaviour, IDamageable
    {
        private const int MeleeFistCount = 2;
        private const float DefaultDetectionRadius = 25f;
        private const float DefaultLoseTargetRadius = 30f;
        private const float DefaultPatrolRadius = 4f;
        private const float DefaultShooterAttackRadius = 20f;
        private const float DefaultProjectileMaxDistance = 20f;
        private const float DestinationUpdateThreshold = 0.2f;

        [Header("Identity")]
        [SerializeField] private EnemyCapsuleArchetype archetype = EnemyCapsuleArchetype.Shooter;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private EnemyOverheadPresentation overheadPresentation;
        [SerializeField] private Color baseColor = Color.yellow;

        [Header("Vitals")]
        [SerializeField, Min(1f)] private float maxHealth = 5f;

        [Header("Awareness")]
        [SerializeField, Min(0.1f)] private float detectionRadius = DefaultDetectionRadius;
        [SerializeField, Min(0.1f)] private float loseTargetRadius = DefaultLoseTargetRadius;
        [SerializeField, Min(0f)] private float patrolRadius = DefaultPatrolRadius;
        [SerializeField, Min(0f)] private float patrolPauseDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 2f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 540f;

        [Header("Shooter")]
        [SerializeField, Min(0.1f)] private float shooterAttackRadius = DefaultShooterAttackRadius;
        [SerializeField, Min(0.1f)] private float shooterPreferredDistance = 6f;
        [SerializeField, Min(0.01f)] private float shooterInterval = 0.85f;
        [SerializeField, Min(0.01f)] private float shooterRadialInterval = 3f;
        [SerializeField, Min(3)] private int shooterRadialProjectileCount = 12;
        [SerializeField, Min(0.1f)] private float shooterProjectileSpeed = 18f;
        [SerializeField, Min(0.01f)] private float shooterProjectileLifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float shooterProjectileMaxDistance = DefaultProjectileMaxDistance;
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

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private NavMeshAgent navMeshAgent;
        private Transform target;
        private TopDownPlayerMotor playerMotor;
        private Material runtimeMaterialInstance;
        private Vector3 feedbackBaseLocalScale = Vector3.one;
        private Vector3 patrolAnchor;
        private readonly Vector3[] patrolPoints = new Vector3[2];
        private readonly Transform[] meleeFists = new Transform[MeleeFistCount];
        private readonly float[] meleeFistPunchTimers = new float[MeleeFistCount];
        private Vector3 lastRequestedDestination;
        private float lastRequestedStoppingDistance = -1f;
        private float currentHealth;
        private float attackCooldownTimer;
        private float shooterRadialCooldownTimer;
        private float bounceTimer;
        private float patrolPauseTimer;
        private bool isDead;
        private bool isWarningExplosion;
        private bool hasDetectedTarget;
        private bool hasPatrolRoute;
        private int currentPatrolPointIndex;
        private int nextMeleeFistIndex;
        private Coroutine hitFlashRoutine;
        private Coroutine explosionRoutine;

        public CombatTeam Team => CombatTeam.Enemy;
        public bool IsAlive => !isDead;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            navMeshAgent = GetComponent<NavMeshAgent>();

            body.useGravity = false;
            body.isKinematic = true;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
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

            ConfigureNavMeshAgent();
            InitializeMaterialInstance();

            currentHealth = maxHealth;
            patrolAnchor = transform.position;
            ApplyRestingColor();
            overheadPresentation?.SetHealth(currentHealth, maxHealth);

            if (archetype == EnemyCapsuleArchetype.Melee)
            {
                EnsureMeleeFistsInitialized();
                UpdateMeleeFistVisuals();
            }
        }

        private void Start()
        {
            RefreshNavMeshPosition();
            SetPatrolAnchor(patrolAnchor);
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
            shooterRadialCooldownTimer = Mathf.Max(0f, shooterRadialCooldownTimer - Time.deltaTime);
            bounceTimer = Mathf.Max(0f, bounceTimer - Time.deltaTime);
            patrolPauseTimer = Mathf.Max(0f, patrolPauseTimer - Time.deltaTime);

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

            RefreshNavMeshPosition();

            if (target == null)
            {
                ResolveTarget();
            }

            if (target == null)
            {
                hasDetectedTarget = false;
                TickPatrol();
                RotateVisual(GetPatrolFacingDirection());
                return;
            }

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            var distanceToTarget = toTarget.magnitude;
            var directionToTarget = distanceToTarget > 0.0001f ? toTarget / distanceToTarget : GetPatrolFacingDirection();

            UpdateAwareness(distanceToTarget);

            if (!hasDetectedTarget)
            {
                TickPatrol();
                RotateVisual(GetPatrolFacingDirection());
                return;
            }

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

            RotateVisual(directionToTarget);
        }

        public void SetPatrolAnchor(Vector3 anchor)
        {
            patrolAnchor = TrySampleNavMeshPosition(anchor, navMeshSampleDistance, out var sampledAnchor)
                ? sampledAnchor
                : anchor;

            BuildPatrolRoute();
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

            var hitSource = hitInfo.Instigator != null ? hitInfo.Instigator.transform : null;
            if (hitSource != null)
            {
                target = hitSource.root;
                playerMotor = target.GetComponentInParent<TopDownPlayerMotor>();
                hasDetectedTarget = true;
            }

            if (currentHealth <= 0f)
            {
                Die();
            }

            return true;
        }

        private void TickShooter(float distanceToTarget, Vector3 directionToTarget)
        {
            var preferredDistance = Mathf.Max(0.1f, shooterPreferredDistance);
            if (distanceToTarget > preferredDistance)
            {
                MoveToWorldPoint(target.position, preferredDistance * 0.9f);
            }
            else if (distanceToTarget < preferredDistance * 0.65f)
            {
                var retreatTarget = transform.position - (directionToTarget * Mathf.Max(1f, preferredDistance - distanceToTarget + 0.75f));
                if (!MoveToWorldPoint(retreatTarget, 0.1f))
                {
                    StopNavigation();
                }
            }
            else
            {
                StopNavigation();
            }

            if (distanceToTarget <= GetShooterAttackRadius() && shooterRadialCooldownTimer <= 0f)
            {
                shooterRadialCooldownTimer = shooterRadialInterval;
                FireRadialVolley(directionToTarget);
            }

            if (distanceToTarget <= GetShooterAttackRadius() && attackCooldownTimer <= 0f)
            {
                attackCooldownTimer = shooterInterval;
                FireProjectile(directionToTarget);
            }
        }

        private void TickMelee(float distanceToTarget, Vector3 directionToTarget)
        {
            if (distanceToTarget > meleeAttackRange)
            {
                MoveToWorldPoint(target.position, meleeAttackRange * 0.85f);
                return;
            }

            StopNavigation();
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
                StopNavigation();
                return;
            }

            if (distanceToTarget > explodeTriggerRange)
            {
                MoveToWorldPoint(target.position, explodeTriggerRange * 0.85f);
                return;
            }

            StopNavigation();
            if (explosionRoutine == null)
            {
                explosionRoutine = StartCoroutine(PlayExplosionWarningRoutine());
            }
        }

        private void TickPatrol()
        {
            if (!hasPatrolRoute)
            {
                BuildPatrolRoute();
            }

            if (!hasPatrolRoute)
            {
                StopNavigation();
                return;
            }

            if (patrolPauseTimer > 0f)
            {
                StopNavigation();
                return;
            }

            var targetPatrolPoint = patrolPoints[currentPatrolPointIndex];
            if (!MoveToWorldPoint(targetPatrolPoint, 0.1f))
            {
                patrolPauseTimer = patrolPauseDuration;
                currentPatrolPointIndex = (currentPatrolPointIndex + 1) % patrolPoints.Length;
                return;
            }

            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= Mathf.Max(0.15f, navMeshAgent.stoppingDistance + 0.05f))
            {
                StopNavigation();
                patrolPauseTimer = patrolPauseDuration;
                currentPatrolPointIndex = (currentPatrolPointIndex + 1) % patrolPoints.Length;
            }
        }

        private void FireProjectile(Vector3 directionToTarget)
        {
            SpawnProjectile(directionToTarget, "Projectile");
            TriggerAttackBounce();
        }

        private void FireRadialVolley(Vector3 directionToTarget)
        {
            var projectileCount = Mathf.Max(3, shooterRadialProjectileCount);
            var baseDirection = directionToTarget.sqrMagnitude > 0.0001f ? directionToTarget.normalized : transform.forward;
            var baseAngle = Mathf.Atan2(baseDirection.x, baseDirection.z) * Mathf.Rad2Deg;
            var stepAngle = 360f / projectileCount;

            for (var i = 0; i < projectileCount; i++)
            {
                var shotAngle = baseAngle + (stepAngle * i);
                var radialDirection = Quaternion.Euler(0f, shotAngle, 0f) * Vector3.forward;
                SpawnProjectile(radialDirection, "RadialProjectile");
            }

            TriggerAttackBounce();
        }

        private void SpawnProjectile(Vector3 shotDirection, string projectileNameSuffix)
        {
            var projectileDirection = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : transform.forward;
            var spawnOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            spawnOrigin += projectileDirection * shooterProjectileForwardOffset;
            var effectiveProjectileLifetime = ResolveProjectileLifetime(
                shooterProjectileSpeed,
                shooterProjectileLifetime,
                shooterProjectileMaxDistance);

            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = $"{name}_{projectileNameSuffix}";
            projectile.transform.SetPositionAndRotation(
                spawnOrigin,
                Quaternion.LookRotation(projectileDirection, Vector3.up));
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
            IgnoreEnemyCollisions(projectileCollider);
            projectileSphere.Initialize(
                projectileDirection,
                shooterProjectileSpeed,
                effectiveProjectileLifetime,
                1.6f,
                0.74f,
                10f,
                shooterKnockbackForce,
                transform.root);
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

        private void ConfigureNavMeshAgent()
        {
            if (navMeshAgent == null)
            {
                return;
            }

            navMeshAgent.speed = moveSpeed;
            navMeshAgent.acceleration = Mathf.Max(8f, moveSpeed * 4f);
            navMeshAgent.angularSpeed = turnSpeedDegrees;
            navMeshAgent.autoBraking = true;
            navMeshAgent.updateRotation = false;
            navMeshAgent.radius = capsuleCollider != null ? capsuleCollider.radius : 0.5f;
            navMeshAgent.height = capsuleCollider != null ? capsuleCollider.height : 2f;
            navMeshAgent.baseOffset = GetBottomToPivotDistance();
            navMeshAgent.stoppingDistance = 0f;
        }

        private void RefreshNavMeshPosition()
        {
            if (navMeshAgent == null)
            {
                return;
            }

            ConfigureNavMeshAgent();

            if (navMeshAgent.isOnNavMesh)
            {
                return;
            }

            if (TrySampleNavMeshPosition(transform.position, navMeshSampleDistance, out var navMeshPosition))
            {
                transform.position = navMeshPosition + (Vector3.up * GetBottomToPivotDistance());
            }
        }

        private void UpdateAwareness(float distanceToTarget)
        {
            if (distanceToTarget <= GetDetectionRadius())
            {
                hasDetectedTarget = true;
                return;
            }

            if (hasDetectedTarget && distanceToTarget > GetLoseTargetRadius())
            {
                hasDetectedTarget = false;
                StopNavigation();
            }
        }

        private void BuildPatrolRoute()
        {
            var pointA = TryGetRandomPatrolPoint(out var sampledA) ? sampledA : patrolAnchor;
            var pointB = TryGetRandomPatrolPoint(pointA, out var sampledB) ? sampledB : patrolAnchor;

            patrolPoints[0] = pointA;
            patrolPoints[1] = pointB;
            currentPatrolPointIndex = 0;
            patrolPauseTimer = 0f;
            hasPatrolRoute = true;
        }

        private bool TryGetRandomPatrolPoint(out Vector3 patrolPoint)
        {
            return TryGetRandomPatrolPoint(Vector3.zero, false, out patrolPoint);
        }

        private bool TryGetRandomPatrolPoint(Vector3 avoidPoint, out Vector3 patrolPoint)
        {
            return TryGetRandomPatrolPoint(avoidPoint, true, out patrolPoint);
        }

        private bool TryGetRandomPatrolPoint(Vector3 avoidPoint, bool hasAvoidPoint, out Vector3 patrolPoint)
        {
            var radius = GetPatrolRadius();
            if (radius <= 0f)
            {
                patrolPoint = patrolAnchor;
                return TrySampleNavMeshPosition(patrolAnchor, navMeshSampleDistance, out patrolPoint);
            }

            for (var attempt = 0; attempt < 8; attempt++)
            {
                var randomOffset = Random.insideUnitCircle * radius;
                var candidate = patrolAnchor + new Vector3(randomOffset.x, 0f, randomOffset.y);
                if (!TrySampleNavMeshPosition(candidate, navMeshSampleDistance + radius, out var sampledPoint))
                {
                    continue;
                }

                if (hasAvoidPoint && Vector3.Distance(sampledPoint, avoidPoint) < 1f)
                {
                    continue;
                }

                patrolPoint = sampledPoint;
                return true;
            }

            patrolPoint = patrolAnchor;
            return TrySampleNavMeshPosition(patrolAnchor, navMeshSampleDistance + radius, out patrolPoint);
        }

        private bool MoveToWorldPoint(Vector3 worldPoint, float stoppingDistance)
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            if (!TrySampleNavMeshPosition(worldPoint, navMeshSampleDistance + Mathf.Max(0.25f, stoppingDistance), out var sampledDestination))
            {
                return false;
            }

            var clampedStoppingDistance = Mathf.Max(0f, stoppingDistance);
            if (!navMeshAgent.isStopped
                && Vector3.Distance(lastRequestedDestination, sampledDestination) <= DestinationUpdateThreshold
                && Mathf.Abs(lastRequestedStoppingDistance - clampedStoppingDistance) <= 0.05f)
            {
                return true;
            }

            navMeshAgent.isStopped = false;
            navMeshAgent.stoppingDistance = clampedStoppingDistance;
            if (!navMeshAgent.SetDestination(sampledDestination))
            {
                return false;
            }

            lastRequestedDestination = sampledDestination;
            lastRequestedStoppingDistance = clampedStoppingDistance;
            return true;
        }

        private void StopNavigation()
        {
            if (navMeshAgent == null)
            {
                return;
            }

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
            }

            lastRequestedStoppingDistance = -1f;
            lastRequestedDestination = transform.position;
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

        private static float ResolveProjectileLifetime(float speed, float configuredLifetime, float maxDistance)
        {
            var effectiveLifetime = Mathf.Max(0.01f, configuredLifetime);
            if (speed <= 0f || maxDistance <= 0f)
            {
                return effectiveLifetime;
            }

            return Mathf.Min(effectiveLifetime, maxDistance / speed);
        }

        private bool TrySampleNavMeshPosition(Vector3 sourcePosition, float sampleDistance, out Vector3 sampledPosition)
        {
            if (NavMesh.SamplePosition(sourcePosition, out var navMeshHit, Mathf.Max(0.1f, sampleDistance), NavMesh.AllAreas))
            {
                sampledPosition = navMeshHit.position;
                return true;
            }

            sampledPosition = sourcePosition;
            return false;
        }

        private void RotateVisual(Vector3 direction)
        {
            if (visualRoot == null)
            {
                return;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(
                visualRoot.rotation,
                targetRotation,
                turnSpeedDegrees * Time.fixedDeltaTime);
        }

        private Vector3 GetPatrolFacingDirection()
        {
            if (navMeshAgent != null)
            {
                var velocity = navMeshAgent.velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 0.0001f)
                {
                    return velocity.normalized;
                }

                var desiredVelocity = navMeshAgent.desiredVelocity;
                desiredVelocity.y = 0f;
                if (desiredVelocity.sqrMagnitude > 0.0001f)
                {
                    return desiredVelocity.normalized;
                }
            }

            return visualRoot != null ? visualRoot.forward : transform.forward;
        }

        private void ResolveTarget()
        {
            playerMotor = Object.FindFirstObjectByType<TopDownPlayerMotor>();
            target = playerMotor != null ? playerMotor.transform : null;
        }

        private float GetDetectionRadius()
        {
            return detectionRadius > 0f ? detectionRadius : DefaultDetectionRadius;
        }

        private float GetLoseTargetRadius()
        {
            return loseTargetRadius > 0f ? loseTargetRadius : DefaultLoseTargetRadius;
        }

        private float GetPatrolRadius()
        {
            return patrolRadius >= 0f ? patrolRadius : DefaultPatrolRadius;
        }

        private float GetShooterAttackRadius()
        {
            return shooterAttackRadius > 0f ? shooterAttackRadius : DefaultShooterAttackRadius;
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
            StopNavigation();

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

        private void IgnoreEnemyCollisions(Collider projectileCollider)
        {
            if (projectileCollider == null)
            {
                return;
            }

            var enemies = Object.FindObjectsByType<EnemyCapsuleController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
            {
                var enemy = enemies[enemyIndex];
                if (enemy == null)
                {
                    continue;
                }

                var enemyColliders = enemy.GetComponentsInChildren<Collider>();
                for (var colliderIndex = 0; colliderIndex < enemyColliders.Length; colliderIndex++)
                {
                    var enemyCollider = enemyColliders[colliderIndex];
                    if (enemyCollider == null || enemyCollider == projectileCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(projectileCollider, enemyCollider, true);
                }
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.82f, 0.12f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius > 0f ? detectionRadius : DefaultDetectionRadius);

            Gizmos.color = new Color(1f, 0.45f, 0.12f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, loseTargetRadius > 0f ? loseTargetRadius : DefaultLoseTargetRadius);

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.25f);
            Gizmos.DrawWireSphere(patrolAnchor == Vector3.zero ? transform.position : patrolAnchor, patrolRadius >= 0f ? patrolRadius : DefaultPatrolRadius);
        }
    }
}
