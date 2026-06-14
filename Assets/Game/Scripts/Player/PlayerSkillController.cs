using RorType.Gameplay.Combat;
using RorType.Gameplay.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RorType.Gameplay.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerResourceController))]
    [RequireComponent(typeof(TopDownFacingController))]
    public sealed class PlayerSkillController : MonoBehaviour
    {
        public const int SkillSlotCount = 2;

        [Header("Radial Burst")]
        [SerializeField] private KeyCode radialBurstKey = KeyCode.Alpha1;
        [SerializeField, Min(0.1f)] private float radialBurstCooldown = 5f;
        [SerializeField, Min(3)] private int radialProjectileCount = 7;
        [SerializeField, Min(0.1f)] private float radialProjectileSpeed = 18f;
        [SerializeField, Min(0.01f)] private float radialProjectileLifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float radialProjectileMaxDistance = 20f;
        [SerializeField, Min(0.01f)] private float radialProjectileRadius = 0.18f;
        [SerializeField, Min(0f)] private float radialProjectileForwardOffset = 0.95f;
        [SerializeField, Min(0f)] private float radialProjectileDamage = 1f;
        [SerializeField, Min(0f)] private float radialProjectileImpactImpulse = 1f;
        [SerializeField] private Color radialProjectileColor = new Color(0.86f, 0.14f, 0.14f);

        [Header("Sticky Bomb")]
        [SerializeField] private KeyCode stickyBombKey = KeyCode.Alpha2;
        [SerializeField, Min(0.1f)] private float stickyBombCooldown = 5f;
        [SerializeField, Min(0.1f)] private float stickyBombSpeed = 28f;
        [SerializeField, Min(0.01f)] private float stickyBombLifetime = 1.4f;
        [SerializeField, Min(0.1f)] private float stickyBombMaxDistance = 20f;
        [SerializeField, Min(0.01f)] private float stickyBombRadius = 0.3f;
        [SerializeField, Min(0f)] private float stickyBombSpawnForwardOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float stickyBombFuse = 1.2f;
        [SerializeField, Min(0.1f)] private float stickyBombExplosionVisualRadius = 3f;
        [SerializeField, Min(0.1f)] private float stickyBombExplosionDamageRadius = 2f;
        [SerializeField, Min(0f)] private float stickyBombExplosionDamage = 30f;
        [SerializeField, Min(0f)] private float stickyBombExplosionImpulse = 4.8f;
        [SerializeField, Min(0.05f)] private float stickyBombExplosionVisualLifetime = 0.16f;
        [SerializeField] private Color stickyBombColor = new Color(0.65f, 0.16f, 1f);

        private CapsuleCollider capsuleCollider;
        private PlayerResourceController resources;
        private TopDownFacingController facingController;
        private float radialBurstCooldownTimer;
        private float stickyBombCooldownTimer;

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
            capsuleCollider = GetComponent<CapsuleCollider>();
            resources = GetComponent<PlayerResourceController>();
            facingController = GetComponent<TopDownFacingController>();
            NormalizeSettings();
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            radialBurstCooldownTimer = Mathf.Max(0f, radialBurstCooldownTimer - deltaTime);
            stickyBombCooldownTimer = Mathf.Max(0f, stickyBombCooldownTimer - deltaTime);

            if (IsSkillInputBlocked())
            {
                return;
            }

            if (Input.GetKeyDown(radialBurstKey))
            {
                TryUseRadialBurst();
            }

            if (Input.GetKeyDown(stickyBombKey))
            {
                TryUseStickyBomb();
            }
        }

        private void TryUseRadialBurst()
        {
            if (radialBurstCooldownTimer > 0f)
            {
                return;
            }

            radialBurstCooldownTimer = radialBurstCooldown;
            FireRadialProjectiles();
        }

        private void TryUseStickyBomb()
        {
            if (stickyBombCooldownTimer > 0f)
            {
                return;
            }

            stickyBombCooldownTimer = stickyBombCooldown;
            FireStickyBomb();
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

            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "PlayerRadialProjectile";
            projectile.transform.SetPositionAndRotation(
                spawnOrigin,
                Quaternion.LookRotation(projectileDirection, Vector3.up));
            projectile.transform.localScale = Vector3.one * (radialProjectileRadius * 2f);

            var projectileCollider = projectile.GetComponent<SphereCollider>();
            var projectileRenderer = projectile.GetComponent<Renderer>();
            projectile.AddComponent<Rigidbody>();
            var projectileSphere = projectile.AddComponent<TopDownProjectileSphere>();

            if (projectileRenderer != null)
            {
                RuntimeRendererUtility.SetColor(projectileRenderer, radialProjectileColor);
            }

            IgnorePlayerCollisions(projectileCollider);

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
            var shotDirection = ResolveAimDirection();
            var spawnOrigin = GetProjectileSpawnOrigin(shotDirection, stickyBombSpawnForwardOffset);
            var effectiveLifetime = ResolveProjectileLifetime(
                stickyBombSpeed,
                stickyBombLifetime,
                stickyBombMaxDistance);

            var bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bomb.name = "PlayerStickyBomb";
            bomb.transform.SetPositionAndRotation(
                spawnOrigin,
                Quaternion.LookRotation(shotDirection, Vector3.up));
            bomb.transform.localScale = Vector3.one * (stickyBombRadius * 2f);

            var bombCollider = bomb.GetComponent<SphereCollider>();
            var bombRenderer = bomb.GetComponent<Renderer>();
            bomb.AddComponent<Rigidbody>();
            var stickyProjectile = bomb.AddComponent<StickyBombProjectile>();

            if (bombRenderer != null)
            {
                RuntimeRendererUtility.SetColor(bombRenderer, stickyBombColor);
            }

            IgnorePlayerCollisions(bombCollider);

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
                stickyBombColor,
                gameObject,
                CombatTeam.Player);
        }

        private Vector3 ResolveAimDirection()
        {
            var origin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
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
            var spawnOrigin = capsuleCollider != null ? capsuleCollider.bounds.center : transform.position;
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

        private static float ResolveProjectileLifetime(float speed, float configuredLifetime, float maxDistance)
        {
            var effectiveLifetime = Mathf.Max(0.01f, configuredLifetime);
            if (speed <= 0f || maxDistance <= 0f)
            {
                return effectiveLifetime;
            }

            return Mathf.Min(effectiveLifetime, maxDistance / speed);
        }

        private static bool IsSkillInputBlocked()
        {
            if (PortalUiRuntime.IsChoiceOpen || ShopUiPanel.IsAnyOpen)
            {
                return true;
            }

            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void NormalizeSettings()
        {
            radialBurstCooldown = Mathf.Max(0.1f, radialBurstCooldown);
            radialProjectileCount = Mathf.Max(3, radialProjectileCount);
            radialProjectileSpeed = Mathf.Max(0.1f, radialProjectileSpeed);
            radialProjectileLifetime = Mathf.Max(0.01f, radialProjectileLifetime);
            radialProjectileMaxDistance = Mathf.Max(0.1f, radialProjectileMaxDistance);
            radialProjectileRadius = Mathf.Max(0.01f, radialProjectileRadius);
            radialProjectileForwardOffset = Mathf.Max(0f, radialProjectileForwardOffset);
            radialProjectileDamage = Mathf.Max(0f, radialProjectileDamage);
            radialProjectileImpactImpulse = Mathf.Max(0f, radialProjectileImpactImpulse);

            stickyBombCooldown = Mathf.Max(0.1f, stickyBombCooldown);
            stickyBombSpeed = Mathf.Max(0.1f, stickyBombSpeed);
            stickyBombLifetime = Mathf.Max(0.01f, stickyBombLifetime);
            stickyBombMaxDistance = Mathf.Max(0.1f, stickyBombMaxDistance);
            stickyBombRadius = Mathf.Max(0.01f, stickyBombRadius);
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
