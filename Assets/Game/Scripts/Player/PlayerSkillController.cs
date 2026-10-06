using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerResourceController))]
    [RequireComponent(typeof(TopDownFacingController))]
    public sealed class PlayerSkillController : MonoBehaviour
    {
        public const int SkillSlotCount = 2;

        [Header("Radial Burst")]
        [SerializeField] private KeyCode radialBurstKey = KeyCode.Alpha1;
        [SerializeField] private TopDownProjectileSphere radialProjectilePrefab;
        [SerializeField, Min(0.1f)] private float radialBurstCooldown = 5f;
        [SerializeField, Min(3)] private int radialProjectileCount = 7;
        [SerializeField, Min(0.1f)] private float radialProjectileSpeed = 18f;
        [SerializeField, Min(0.01f)] private float radialProjectileLifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float radialProjectileMaxDistance = 20f;
        [SerializeField, Min(0f)] private float radialProjectileForwardOffset = 0.95f;
        [SerializeField, Min(0f)] private float radialProjectileDamage = 1f;
        [SerializeField, Min(0f)] private float radialProjectileImpactImpulse = 1f;

        [Header("Sticky Bomb")]
        [SerializeField] private KeyCode stickyBombKey = KeyCode.Alpha2;
        [SerializeField] private StickyBombProjectile stickyBombPrefab;
        [SerializeField, Min(0.1f)] private float groundCursorSpeed = 10f;
        [SerializeField, Min(0.1f)] private float initialGroundCursorDistance = 8f;
        [SerializeField, Min(0.1f)] private float stickyBombCooldown = 5f;
        [SerializeField, Min(0.1f)] private float stickyBombSpeed = 28f;
        [SerializeField, Min(0.01f)] private float stickyBombLifetime = 1.4f;
        [SerializeField, Min(0.1f)] private float stickyBombMaxDistance = 20f;
        [SerializeField, Min(0f)] private float stickyBombSpawnForwardOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float stickyBombFuse = 1.2f;
        [SerializeField, Min(0.1f)] private float stickyBombExplosionVisualRadius = 3f;
        [SerializeField, Min(0.1f)] private float stickyBombExplosionDamageRadius = 2f;
        [SerializeField, Min(0f)] private float stickyBombExplosionDamage = 30f;
        [SerializeField, Min(0f)] private float stickyBombExplosionImpulse = 4.8f;
        [SerializeField, Min(0.05f)] private float stickyBombExplosionVisualLifetime = 0.16f;
        private PlayerResourceController resources;
        private TopDownFacingController facingController;
        private TopDownInputAdapter inputAdapter;
        private TopDownPlayerMotor motor;
        private TopDownGroundProbe groundProbe;
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        private Collider[] playerColliders;
        private float radialBurstCooldownTimer;
        private float stickyBombCooldownTimer;
        public bool IsSelectingPoint { get; private set; }
        public Vector3 GroundAimPoint { get; private set; }
        public bool HasValidGroundPoint { get; private set; }

        public KeyCode GetSkillKey(int slotIndex)
        {
            return slotIndex == 0 ? radialBurstKey : stickyBombKey;
        }

        public float GetSkillCooldownRemaining(int slotIndex)
        {
            return slotIndex == 0 ? radialBurstCooldownTimer : stickyBombCooldownTimer;
        }

        public float GetSkillCooldownDuration(int slotIndex)
        {
            return slotIndex == 0 ? radialBurstCooldown : stickyBombCooldown;
        }

        private void Awake()
        {
            resources = GetComponent<PlayerResourceController>();
            facingController = GetComponent<TopDownFacingController>();
            inputAdapter = GetComponent<TopDownInputAdapter>();
            motor = GetComponent<TopDownPlayerMotor>();
            groundProbe = GetComponent<TopDownGroundProbe>();
            playerColliders = GetComponentsInChildren<Collider>();
            NormalizeSettings();
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            radialBurstCooldownTimer = Mathf.Max(0f, radialBurstCooldownTimer - deltaTime);
            stickyBombCooldownTimer = Mathf.Max(0f, stickyBombCooldownTimer - deltaTime);

            if (inputAdapter.CombatInputBlocked || !resources.IsAlive)
            {
                CancelPointSelection();
                return;
            }
            if (inputAdapter.ReloadPressed)
            {
                CancelPointSelection();
                return;
            }

            if (inputAdapter.RadialSkillPressed || (inputAdapter.KeyboardAndMouseEnabled
                && !inputAdapter.UsesGamepad && Input.GetKeyDown(radialBurstKey)))
            {
                TryUseRadialBurst();
            }

            if (inputAdapter.PointSkillPressed || (inputAdapter.KeyboardAndMouseEnabled
                && !inputAdapter.UsesGamepad && Input.GetKeyDown(stickyBombKey)))
            {
                TryUseStickyBomb();
            }
            if (!IsSelectingPoint) return;
            if (inputAdapter.CancelPressed || inputAdapter.DashPressed)
            {
                CancelPointSelection();
                return;
            }
            UpdateGroundCursor(deltaTime);
            if (inputAdapter.FirePressed)
            {
                inputAdapter.SuppressFireUntilRelease();
                if (HasValidGroundPoint && !facingController.IsReloading)
                {
                    stickyBombCooldownTimer = stickyBombCooldown;
                    FireStickyBomb();
                    IsSelectingPoint = false;
                }
            }
        }

        private void TryUseRadialBurst()
        {
            if (radialBurstCooldownTimer > 0f || radialProjectilePrefab == null
                || IsSelectingPoint || facingController.IsReloading)
            {
                return;
            }

            radialBurstCooldownTimer = radialBurstCooldown;
            FireRadialProjectiles();
        }

        private void TryUseStickyBomb()
        {
            if (stickyBombCooldownTimer > 0f || stickyBombPrefab == null || facingController.IsReloading)
            {
                return;
            }

            if (IsSelectingPoint) { CancelPointSelection(); return; }
            IsSelectingPoint = true;
            var origin = motor.RenderPosition;
            GroundAimPoint = origin + facingController.CurrentAimDirection
                * Mathf.Min(initialGroundCursorDistance, stickyBombMaxDistance);
            inputAdapter.SuppressFireUntilRelease();
        }

        public void CancelPointSelection()
        {
            if (IsSelectingPoint) inputAdapter.SuppressFireUntilRelease();
            IsSelectingPoint = HasValidGroundPoint = false;
        }

        private void UpdateGroundCursor(float deltaTime)
        {
            var point = GroundAimPoint;
            var origin = motor.RenderPosition;
            var groundHeight = groundProbe.IsGrounded ? groundProbe.GroundPoint.y : origin.y;
            if (inputAdapter.UsesGamepad)
            {
                point += motor.ResolveWorldInputDirection(inputAdapter.AimInput)
                    * inputAdapter.AimInput.magnitude * groundCursorSpeed * deltaTime;
            }
            else if (Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(inputAdapter.MouseScreenPosition);
                var plane = new Plane(Vector3.up, new Vector3(origin.x, groundHeight, origin.z));
                if (plane.Raycast(ray, out var distance)) point = ray.GetPoint(distance);
            }
            var offset = Vector3.ProjectOnPlane(point - origin, Vector3.up);
            point = origin + Vector3.ClampMagnitude(offset, stickyBombMaxDistance);
            point.y = groundHeight;
            HasValidGroundPoint = false;
            var count = Physics.RaycastNonAlloc(point + Vector3.up * 10f, Vector3.down,
                groundHits, 30f, groundProbe.GroundLayerMask, QueryTriggerInteraction.Ignore);
            var closest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider.transform.IsChildOf(transform) || !groundProbe.IsGroundCollider(hit.collider)
                    || !groundProbe.IsStableSurfaceNormal(hit.normal) || hit.distance >= closest) continue;
                closest = hit.distance;
                point.y = hit.point.y;
                HasValidGroundPoint = true;
            }
            GroundAimPoint = point;
        }

        private void FireRadialProjectiles()
        {
            var projectileCount = Mathf.Max(3, radialProjectileCount);
            var aimDirection = ResolveAimDirection();
            var baseAngle = Mathf.Atan2(aimDirection.x, aimDirection.z) * Mathf.Rad2Deg;
            var stepAngle = 360f / projectileCount;

            for (var i = 0; i < projectileCount; i++)
            {
                var shotAngle = baseAngle + (stepAngle * i);
                var shotDirection = Quaternion.Euler(0f, shotAngle, 0f) * Vector3.forward;
                SpawnRadialProjectile(shotDirection);
            }
        }

        private void SpawnRadialProjectile(Vector3 direction)
        {
            var projectileDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            var spawnOrigin = GetProjectileSpawnOrigin(projectileDirection, radialProjectileForwardOffset);
            var effectiveLifetime = ResolveProjectileLifetime(
                radialProjectileSpeed,
                radialProjectileLifetime,
                radialProjectileMaxDistance);

            var projectileSphere = Instantiate(radialProjectilePrefab, spawnOrigin,
                Quaternion.LookRotation(projectileDirection, Vector3.up));
            IgnorePlayerCollisions(projectileSphere.GetComponent<SphereCollider>());

            projectileSphere.Initialize(
                projectileDirection,
                radialProjectileSpeed,
                effectiveLifetime,
                1.6f,
                0.74f,
                10f,
                GetModifiedDamage(radialProjectileDamage),
                radialProjectileImpactImpulse,
                gameObject,
                CombatTeam.Player);
        }

        private void FireStickyBomb()
        {
            var shotDirection = Vector3.ProjectOnPlane(GroundAimPoint - facingController.AimOrigin, Vector3.up).normalized;
            if (shotDirection.sqrMagnitude <= 0.0001f) shotDirection = facingController.CurrentAimDirection;
            var spawnOrigin = GetProjectileSpawnOrigin(shotDirection, stickyBombSpawnForwardOffset);
            var effectiveLifetime = ResolveProjectileLifetime(
                stickyBombSpeed,
                stickyBombLifetime,
                stickyBombMaxDistance);

            var stickyProjectile = Instantiate(stickyBombPrefab, spawnOrigin,
                Quaternion.LookRotation(shotDirection, Vector3.up));
            IgnorePlayerCollisions(stickyProjectile.GetComponent<SphereCollider>());

            stickyProjectile.Initialize(
                shotDirection,
                stickyBombSpeed,
                effectiveLifetime,
                stickyBombFuse,
                stickyBombExplosionVisualRadius,
                stickyBombExplosionDamageRadius,
                GetModifiedDamage(stickyBombExplosionDamage),
                stickyBombExplosionImpulse,
                stickyBombExplosionVisualLifetime,
                gameObject,
                CombatTeam.Player);
            stickyProjectile.SetGroundDestination(GroundAimPoint);
        }

        private Vector3 ResolveAimDirection()
        {
            var origin = facingController.AimOrigin;
            if (facingController != null && facingController.TryGetAimPoint(out var aimPoint))
            {
                var direction = aimPoint - origin;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }

            var fallback = transform.forward;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        private Vector3 GetProjectileSpawnOrigin(Vector3 direction, float forwardOffset)
        {
            var spawnOrigin = facingController.AimOrigin;
            return spawnOrigin + (direction * Mathf.Max(0f, forwardOffset));
        }

        private float GetModifiedDamage(float baseDamage)
        {
            if (resources == null)
            {
                resources = GetComponent<PlayerResourceController>();
            }

            return Mathf.Max(0f, baseDamage) * (resources != null ? resources.DamageMultiplier : 1f);
        }

        private void IgnorePlayerCollisions(Collider projectileCollider)
        {
            if (projectileCollider == null)
            {
                return;
            }

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

        private static float ResolveProjectileLifetime(float speed, float configuredLifetime, float maxDistance)
        {
            var effectiveLifetime = Mathf.Max(0.01f, configuredLifetime);
            if (speed <= 0f || maxDistance <= 0f)
            {
                return effectiveLifetime;
            }

            return Mathf.Min(effectiveLifetime, maxDistance / speed);
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void OnDisable() => CancelPointSelection();

        private void NormalizeSettings()
        {
            radialBurstCooldown = Mathf.Max(0.1f, radialBurstCooldown);
            radialProjectileCount = Mathf.Max(3, radialProjectileCount);
            radialProjectileSpeed = Mathf.Max(0.1f, radialProjectileSpeed);
            radialProjectileLifetime = Mathf.Max(0.01f, radialProjectileLifetime);
            radialProjectileMaxDistance = Mathf.Max(0.1f, radialProjectileMaxDistance);
            radialProjectileForwardOffset = Mathf.Max(0f, radialProjectileForwardOffset);
            radialProjectileDamage = Mathf.Max(0f, radialProjectileDamage);
            radialProjectileImpactImpulse = Mathf.Max(0f, radialProjectileImpactImpulse);

            stickyBombCooldown = Mathf.Max(0.1f, stickyBombCooldown);
            stickyBombSpeed = Mathf.Max(0.1f, stickyBombSpeed);
            stickyBombLifetime = Mathf.Max(0.01f, stickyBombLifetime);
            stickyBombMaxDistance = Mathf.Max(0.1f, stickyBombMaxDistance);
            stickyBombSpawnForwardOffset = Mathf.Max(0f, stickyBombSpawnForwardOffset);
            stickyBombFuse = Mathf.Max(0.01f, stickyBombFuse);
            stickyBombExplosionVisualRadius = Mathf.Max(0.1f, stickyBombExplosionVisualRadius);
            stickyBombExplosionDamageRadius = Mathf.Max(0.1f, stickyBombExplosionDamageRadius);
            stickyBombExplosionDamage = Mathf.Max(0f, stickyBombExplosionDamage);
            stickyBombExplosionImpulse = Mathf.Max(0f, stickyBombExplosionImpulse);
            stickyBombExplosionVisualLifetime = Mathf.Max(0.05f, stickyBombExplosionVisualLifetime);
        }
    }
}
