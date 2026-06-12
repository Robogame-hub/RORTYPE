using System.Collections;
using System.Collections.Generic;
using RorType.Gameplay.Combat;
using RorType.Gameplay.Interaction;
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
        private const float DefaultExplosionEffectRadius = 3f;
        private const float DefaultExplosionDamageRadius = 2f;
        private const float DefaultExplosionDamage = 30f;
        private const int ExplosionHitBufferSize = 16;
        private static readonly List<EnemyCapsuleController> ActiveEnemies = new();
        private static readonly List<Collider> SharedColliderBuffer = new();

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
        [SerializeField, Min(0.01f)] private float shooterRadialMinInterval = 3f;
        [SerializeField, Min(0.01f)] private float shooterRadialMaxInterval = 6f;
        [SerializeField, Min(3)] private int shooterRadialMinProjectileCount = 5;
        [SerializeField, Min(3)] private int shooterRadialMaxProjectileCount = 7;
        [SerializeField, Min(0.1f)] private float shooterProjectileSpeed = 18f;
        [SerializeField, Min(0.01f)] private float shooterProjectileLifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float shooterProjectileMaxDistance = DefaultProjectileMaxDistance;
        [SerializeField, Min(0.01f)] private float shooterProjectileRadius = 0.18f;
        [SerializeField, Min(0f)] private float shooterProjectileForwardOffset = 0.95f;
        [SerializeField, Min(0f)] private float shooterKnockbackForce = 2.6f;
        [SerializeField, Min(0f)] private float shooterProjectileDamage = 20f;
        [SerializeField] private Color shooterProjectileColor = new Color(1f, 0.86f, 0.18f);

        [Header("Melee")]
        [SerializeField, Min(0.1f)] private float meleeAttackRange = 1.65f;
        [SerializeField, Min(0.01f)] private float meleeInterval = 0.5f;
        [SerializeField, Min(0f)] private float meleeKnockbackForce = 3.15f;
        [SerializeField, Min(0f)] private float meleeDamage = 10f;
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
        [SerializeField, Min(0.1f)] private float explodeVisualRadius = 5.2f;
        [SerializeField, Min(0.1f)] private float explodeEffectRadius = DefaultExplosionEffectRadius;
        [SerializeField, Min(0.1f)] private float explodeDamageRadius = DefaultExplosionDamageRadius;
        [SerializeField, Min(0f)] private float explodeDamage = DefaultExplosionDamage;
        [SerializeField, Min(0f)] private float explodeKnockbackForce = 4.8f;
        [SerializeField] private Color explodeWarningColor = new Color(1f, 0.16f, 0.16f);

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float visualPositionSharpness = 30f;
        [SerializeField, Min(0.01f)] private float bounceDuration = 0.22f;
        [SerializeField, Min(0f)] private float bounceSideScale = 0.12f;
        [SerializeField, Min(0f)] private float bounceHeightScale = 0.18f;
        [SerializeField, Min(0.01f)] private float bounceScaleSharpness = 24f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Min(1)] private int hitFlashCount = 3;
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;

        [Header("Drops")]
        [SerializeField, Min(0)] private int minResourceDrops = 1;
        [SerializeField, Min(0)] private int maxResourceDrops = 3;
        [SerializeField] private ResourcePickupCollectible moneyPickupPrefab;
        [SerializeField] private ResourcePickupCollectible ammoPickupPrefab;
        [SerializeField] private ResourcePickupCollectible healthPickupPrefab;
        [SerializeField, Min(1)] private int moneyPerPickup = 10;
        [SerializeField, Min(1)] private int ammoPerPickup = 10;
        [SerializeField, Min(1)] private int healthPerPickup = 150;
        [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float shooterAmmoDropChance = 0.45f;
        [SerializeField, Min(0.1f)] private float dropLaunchSpeed = 3.2f;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private NavMeshAgent navMeshAgent;
        private Transform target;
        private TopDownPlayerMotor playerMotor;
        private Vector3 feedbackBaseLocalScale = Vector3.one;
        private Vector3 patrolAnchor;
        private readonly Vector3[] patrolPoints = new Vector3[2];
        private readonly Transform[] meleeFists = new Transform[MeleeFistCount];
        private readonly float[] meleeFistPunchTimers = new float[MeleeFistCount];
        private readonly Collider[] explosionHitBuffer = new Collider[ExplosionHitBufferSize];
        private readonly Component[] explosionUniqueHitBuffer = new Component[ExplosionHitBufferSize];
        private Vector3 lastRequestedDestination;
        private float lastRequestedStoppingDistance = -1f;
        private float currentHealth;
        private float attackCooldownTimer;
        private float shooterRadialCooldownTimer;
        private float bounceTimer;
        private float patrolPauseTimer;
        private bool isDead;
        private bool hasScheduledShooterRadialVolley;
        private bool hasVisualBasePose;
        private bool hasVisualPosition;
        private bool isWarningExplosion;
        private bool hasDetectedTarget;
        private bool hasPatrolRoute;
        private int currentPatrolPointIndex;
        private int nextMeleeFistIndex;
        private Coroutine hitFlashRoutine;
        private Coroutine explosionRoutine;
        private Vector3 visualBaseLocalPosition;
        private Vector3 smoothedVisualWorldPosition;

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

            visualRoot = ResolveVisualRoot();

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
            CacheVisualBasePose();

            ConfigureNavMeshAgent();

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

        private void OnEnable()
        {
            if (!ActiveEnemies.Contains(this))
            {
                ActiveEnemies.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
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

            TickMovement(Time.deltaTime);
            UpdateFeedbackVisual(Time.deltaTime);
            UpdateMeleeFistVisuals();
        }

        private void LateUpdate()
        {
            UpdateVisualSmoothing();
        }

        private void TickMovement(float deltaTime)
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
                hasScheduledShooterRadialVolley = false;
                TickPatrol();
                RotateVisual(GetPatrolFacingDirection(), deltaTime);
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
                RotateVisual(GetPatrolFacingDirection(), deltaTime);
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

            RotateVisual(directionToTarget, deltaTime);
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
            if (isDead || isWarningExplosion || hitInfo.Team == Team)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - hitInfo.Damage);
            overheadPresentation?.SetHealth(currentHealth, maxHealth);
            overheadPresentation?.SpawnDamageNumber(hitInfo.Damage, hitInfo.Point);
            var reachedZeroHealth = currentHealth <= 0f;
            var shouldExplodeOnDeath = reachedZeroHealth && archetype == EnemyCapsuleArchetype.Exploder;
            var hitByExploder = IsExploderInstigator(hitInfo.Instigator);
            if (!shouldExplodeOnDeath || hitByExploder)
            {
                PlayHitFlash();
            }

            var hitSource = hitInfo.Instigator != null ? hitInfo.Instigator.transform : null;
            if (hitSource != null)
            {
                target = hitSource.root;
                playerMotor = target.GetComponentInParent<TopDownPlayerMotor>();
                hasDetectedTarget = true;
            }

            if (reachedZeroHealth)
            {
                if (shouldExplodeOnDeath)
                {
                    BeginExplosionSequence(hitByExploder);
                }
                else
                {
                    Die();
                }
            }

            return true;
        }

        private void TickShooter(float distanceToTarget, Vector3 directionToTarget)
        {
            if (!hasScheduledShooterRadialVolley)
            {
                shooterRadialCooldownTimer = GetRandomizedRadialInterval();
                hasScheduledShooterRadialVolley = true;
            }

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
                shooterRadialCooldownTimer = GetRandomizedRadialInterval();
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
                TryApplyPlayerDamage(directionToTarget, meleeDamage, meleeAttackRange + 0.55f);
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
            BeginExplosionSequence();
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
            var projectileCount = GetRandomizedRadialProjectileCount();
            var baseDirection = directionToTarget.sqrMagnitude > 0.0001f ? directionToTarget.normalized : transform.forward;
            var baseAngle = Mathf.Atan2(baseDirection.x, baseDirection.z) * Mathf.Rad2Deg;
            var randomAngleOffset = Random.Range(0f, 360f);
            var stepAngle = 360f / projectileCount;

            for (var i = 0; i < projectileCount; i++)
            {
                var shotAngle = baseAngle + randomAngleOffset + (stepAngle * i);
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
                RuntimeRendererUtility.SetColor(projectileRenderer, shooterProjectileColor);
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
                shooterProjectileDamage,
                transform.root);
        }

        private IEnumerator PlayExplosionWarningRoutine(bool preserveHitFlashFrame)
        {
            isWarningExplosion = true;
            attackCooldownTimer = 0f;

            if (preserveHitFlashFrame && hitFlashRoutine != null)
            {
                yield return null;
            }

            if (hitFlashRoutine != null)
            {
                StopCoroutine(hitFlashRoutine);
                hitFlashRoutine = null;
            }

            for (var i = 0; i < explodeWarningFlashCount; i++)
            {
                SetVisualColor(explodeWarningColor);
                yield return new WaitForSeconds(explodeWarningFlashInterval);
                ApplyRestingColor();
                yield return new WaitForSeconds(explodeWarningFlashInterval);
            }

            explosionRoutine = null;
            TriggerAttackBounce();
            ApplyExplosionDamage();
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
                RuntimeRendererUtility.SetColor(renderer, explodeWarningColor);
            }

            var effect = explosion.AddComponent<TransientScaleEffect>();
            effect.Initialize(
                Vector3.one * 0.1f,
                Vector3.one * GetExplosionEffectRadius(),
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
                hasScheduledShooterRadialVolley = false;
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

        private void RotateVisual(Vector3 direction, float deltaTime)
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
                turnSpeedDegrees * Mathf.Max(0f, deltaTime));
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
            var activePlayer = PlayerResourceController.ActivePlayer;
            playerMotor = activePlayer != null ? activePlayer.GetComponent<TopDownPlayerMotor>() : null;
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

        private float GetRandomizedRadialInterval()
        {
            var minInterval = Mathf.Max(0.01f, shooterRadialMinInterval);
            var maxInterval = Mathf.Max(minInterval, shooterRadialMaxInterval);
            return Random.Range(minInterval, maxInterval);
        }

        private int GetRandomizedRadialProjectileCount()
        {
            var minProjectileCount = Mathf.Max(3, shooterRadialMinProjectileCount);
            var maxProjectileCount = Mathf.Max(minProjectileCount, shooterRadialMaxProjectileCount);
            return Random.Range(minProjectileCount, maxProjectileCount + 1);
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
            SpawnResourceDrops();

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

            isWarningExplosion = false;
            Destroy(gameObject);
        }

        private void SpawnResourceDrops()
        {
            var minDrops = Mathf.Max(0, minResourceDrops);
            var maxDrops = Mathf.Max(minDrops, maxResourceDrops);
            if (maxDrops <= 0)
            {
                return;
            }

            var dropCount = Random.Range(minDrops, maxDrops + 1);
            var origin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position + Vector3.up;
            for (var i = 0; i < dropCount; i++)
            {
                var canDropAmmo = archetype == EnemyCapsuleArchetype.Shooter;
                var dropHealth = Random.value < healthDropChance;
                var dropAmmo = !dropHealth && canDropAmmo && Random.value < shooterAmmoDropChance;
                var pickupKind = ResolveDropKind(dropHealth, dropAmmo);
                var amount = ResolveDropAmount(pickupKind);
                var planarDirection = Random.insideUnitCircle.normalized;
                if (planarDirection.sqrMagnitude <= 0.0001f)
                {
                    planarDirection = Vector2.right;
                }

                var launchVelocity = new Vector3(planarDirection.x, 1.6f, planarDirection.y) * dropLaunchSpeed;
                ResourcePickupCollectible.Spawn(
                    pickupKind,
                    ResolveDropPrefab(pickupKind),
                    amount,
                    origin + new Vector3(0f, 0.35f, 0f),
                    launchVelocity);
            }
        }

        private ResourcePickupCollectible ResolveDropPrefab(ResourcePickupCollectible.PickupKind pickupKind)
        {
            switch (pickupKind)
            {
                case ResourcePickupCollectible.PickupKind.Health:
                    return healthPickupPrefab;
                case ResourcePickupCollectible.PickupKind.Ammo:
                    return ammoPickupPrefab;
                default:
                    return moneyPickupPrefab;
            }
        }

        private ResourcePickupCollectible.PickupKind ResolveDropKind(bool dropHealth, bool dropAmmo)
        {
            if (dropHealth)
            {
                return ResourcePickupCollectible.PickupKind.Health;
            }

            return dropAmmo
                ? ResourcePickupCollectible.PickupKind.Ammo
                : ResourcePickupCollectible.PickupKind.Money;
        }

        private int ResolveDropAmount(ResourcePickupCollectible.PickupKind pickupKind)
        {
            switch (pickupKind)
            {
                case ResourcePickupCollectible.PickupKind.Health:
                    return healthPerPickup;
                case ResourcePickupCollectible.PickupKind.Ammo:
                    return ammoPerPickup;
                default:
                    return moneyPerPickup;
            }
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

        private void ApplyRestingColor()
        {
            SetVisualColor(baseColor);
        }

        private void SetVisualColor(Color color)
        {
            RuntimeRendererUtility.SetColor(visualRenderer, color);
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

            return transform;
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

            for (var enemyIndex = ActiveEnemies.Count - 1; enemyIndex >= 0; enemyIndex--)
            {
                var enemy = ActiveEnemies[enemyIndex];
                if (enemy == null)
                {
                    ActiveEnemies.RemoveAt(enemyIndex);
                    continue;
                }

                SharedColliderBuffer.Clear();
                enemy.GetComponentsInChildren(false, SharedColliderBuffer);
                for (var colliderIndex = 0; colliderIndex < SharedColliderBuffer.Count; colliderIndex++)
                {
                    var enemyCollider = SharedColliderBuffer[colliderIndex];
                    if (enemyCollider == null || enemyCollider == projectileCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(projectileCollider, enemyCollider, true);
                }
            }

            SharedColliderBuffer.Clear();
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
                RuntimeRendererUtility.SetColor(fistRenderer, meleeFistColor);
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

        private bool TryApplyPlayerDamage(Vector3 direction, float damageAmount, float maxDistance)
        {
            if (damageAmount <= 0f)
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

            var playerResources = playerMotor.GetComponent<PlayerResourceController>();
            if (playerResources == null || !playerResources.IsAlive)
            {
                return false;
            }

            var enemyCenter = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
            var offsetToPlayer = playerMotor.transform.position - enemyCenter;
            offsetToPlayer.y = 0f;
            if (maxDistance > 0f && offsetToPlayer.magnitude > maxDistance)
            {
                return false;
            }

            var hitDirection = direction;
            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = offsetToPlayer.sqrMagnitude > 0.0001f ? offsetToPlayer.normalized : transform.forward;
            }

            return playerResources.ReceiveHit(new CombatHitInfo(
                damageAmount,
                playerMotor.transform.position,
                hitDirection,
                0f,
                gameObject,
                CombatTeam.Enemy));
        }

        private void BeginExplosionSequence(bool preserveHitFlashFrame = false)
        {
            if (isDead || explosionRoutine != null)
            {
                return;
            }

            StopNavigation();
            explosionRoutine = StartCoroutine(PlayExplosionWarningRoutine(preserveHitFlashFrame));
        }

        private void ApplyExplosionDamage()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(
                GetExplosionCenter(),
                GetExplosionDamageRadius(),
                explosionHitBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var uniqueHitCount = 0;

            for (var i = 0; i < hitCount && uniqueHitCount < explosionUniqueHitBuffer.Length; i++)
            {
                var hitCollider = explosionHitBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                if (!CombatUtility.TryGetDamageable(hitCollider, out var damageable, out var damageableComponent))
                {
                    continue;
                }

                if (!damageable.IsAlive || ReferenceEquals(damageableComponent, this))
                {
                    continue;
                }

                var alreadyHit = false;
                for (var uniqueIndex = 0; uniqueIndex < uniqueHitCount; uniqueIndex++)
                {
                    if (explosionUniqueHitBuffer[uniqueIndex] == damageableComponent)
                    {
                        alreadyHit = true;
                        break;
                    }
                }

                if (alreadyHit)
                {
                    continue;
                }

                explosionUniqueHitBuffer[uniqueHitCount] = damageableComponent;
                uniqueHitCount++;

                var explosionCenter = GetExplosionCenter();
                var hitPoint = hitCollider.ClosestPoint(explosionCenter);
                var hitDirection = damageableComponent.transform.position - explosionCenter;
                hitDirection.y = 0f;
                if (hitDirection.sqrMagnitude <= 0.0001f)
                {
                    hitDirection = transform.forward;
                }

                damageable.ReceiveHit(new CombatHitInfo(
                    explodeDamage,
                    hitPoint,
                    hitDirection,
                    0f,
                    gameObject,
                    CombatTeam.Neutral));
            }

            for (var i = 0; i < hitCount && i < explosionHitBuffer.Length; i++)
            {
                explosionHitBuffer[i] = null;
            }

            for (var i = 0; i < uniqueHitCount; i++)
            {
                explosionUniqueHitBuffer[i] = null;
            }
        }

        private bool IsExploderInstigator(GameObject instigator)
        {
            if (instigator == null)
            {
                return false;
            }

            var instigatorEnemy = instigator.GetComponentInParent<EnemyCapsuleController>();
            return instigatorEnemy != null && instigatorEnemy.archetype == EnemyCapsuleArchetype.Exploder;
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

            var explosionCenter = GetExplosionCenter();
            var directionToPlayer = playerMotor.transform.position - explosionCenter;
            TryApplyPlayerKnockback(directionToPlayer, explodeKnockbackForce, GetExplosionEffectRadius());
        }

        private Vector3 GetExplosionCenter()
        {
            return capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
        }

        private float GetExplosionEffectRadius()
        {
            var configuredEffectRadius = Mathf.Max(0.1f, explodeEffectRadius);
            if (explodeVisualRadius <= 0f)
            {
                return configuredEffectRadius;
            }

            return Mathf.Min(configuredEffectRadius, explodeVisualRadius);
        }

        private float GetExplosionDamageRadius()
        {
            return Mathf.Min(GetExplosionEffectRadius(), Mathf.Max(0.1f, explodeDamageRadius));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.82f, 0.12f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius > 0f ? detectionRadius : DefaultDetectionRadius);

            Gizmos.color = new Color(1f, 0.45f, 0.12f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, loseTargetRadius > 0f ? loseTargetRadius : DefaultLoseTargetRadius);

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.25f);
            Gizmos.DrawWireSphere(patrolAnchor == Vector3.zero ? transform.position : patrolAnchor, patrolRadius >= 0f ? patrolRadius : DefaultPatrolRadius);

            if (archetype != EnemyCapsuleArchetype.Exploder)
            {
                return;
            }

            var explosionCenter = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;

            Gizmos.color = new Color(1f, 0.16f, 0.16f, 0.35f);
            Gizmos.DrawWireSphere(explosionCenter, GetExplosionEffectRadius());

            Gizmos.color = new Color(1f, 0.45f, 0.12f, 0.5f);
            Gizmos.DrawWireSphere(explosionCenter, GetExplosionDamageRadius());
        }
    }
}
