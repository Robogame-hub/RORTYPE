using RorType.Gameplay.Player;
using UnityEngine;

namespace RorType.Gameplay.Rts
{
    internal sealed class RtsInfantryProjectile : MonoBehaviour
    {
        private static readonly Color ProjectileColor = new Color(0.86f, 0.14f, 0.14f, 1f);

        private RtsUnit target;
        private RtsUnit source;
        private GameObject impactPrefab;
        private Material projectileMaterial;
        private float damage;
        private float speed;
        private float impactScale;
        private float expireAt;
        private bool hasImpacted;

        public static void Spawn(
            Vector3 position,
            Vector3 direction,
            RtsUnit target,
            RtsUnit source,
            float damage,
            float speed,
            float lifetime,
            float radius,
            Material trailMaterial,
            GameObject impactPrefab,
            float impactScale)
        {
            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "RtsBolterProjectile";
            projectileObject.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(direction, Vector3.up));
            projectileObject.transform.localScale = Vector3.one * (radius * 2f);

            var collider = projectileObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var projectile = projectileObject.AddComponent<RtsInfantryProjectile>();
            projectile.Initialize(target, source, damage, speed, lifetime, trailMaterial, impactPrefab, impactScale);
            projectile.ConfigureRenderer(projectileObject.GetComponent<Renderer>());
        }

        private void Initialize(
            RtsUnit projectileTarget,
            RtsUnit projectileSource,
            float projectileDamage,
            float projectileSpeed,
            float lifetime,
            Material trailMaterial,
            GameObject projectileImpactPrefab,
            float projectileImpactScale)
        {
            target = projectileTarget;
            source = projectileSource;
            damage = Mathf.Max(0f, projectileDamage);
            speed = Mathf.Max(0.1f, projectileSpeed);
            impactPrefab = projectileImpactPrefab;
            impactScale = Mathf.Max(0.01f, projectileImpactScale);
            expireAt = Time.time + Mathf.Max(0.01f, lifetime);

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.04f;
            trail.startWidth = 0.055f;
            trail.endWidth = 0f;
            trail.startColor = ProjectileColor;
            trail.endColor = new Color(ProjectileColor.r, ProjectileColor.g, ProjectileColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            if (trailMaterial != null)
            {
                trail.sharedMaterial = trailMaterial;
            }
        }

        private void ConfigureRenderer(Renderer projectileRenderer)
        {
            if (projectileRenderer == null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            projectileMaterial = new Material(shader);
            if (projectileMaterial.HasProperty("_BaseColor"))
            {
                projectileMaterial.SetColor("_BaseColor", ProjectileColor);
            }
            if (projectileMaterial.HasProperty("_Color"))
            {
                projectileMaterial.SetColor("_Color", ProjectileColor);
            }

            projectileRenderer.sharedMaterial = projectileMaterial;
        }

        private void Update()
        {
            if (Time.time >= expireAt || target == null || !target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            var targetPoint = target.transform.position + Vector3.up * 1.1f;
            var offset = targetPoint - transform.position;
            var distance = offset.magnitude;
            var step = speed * Time.deltaTime;
            if (distance <= step)
            {
                transform.position = targetPoint;
                Impact();
                return;
            }

            var direction = offset / distance;
            transform.SetPositionAndRotation(
                transform.position + direction * step,
                Quaternion.LookRotation(direction, Vector3.up));
        }

        private void Impact()
        {
            if (hasImpacted)
            {
                return;
            }

            hasImpacted = true;
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(damage, source);
            }

            PlayerWeaponVfx.Spawn(impactPrefab, transform.position, Quaternion.identity, impactScale, null, 2f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (projectileMaterial != null)
            {
                Destroy(projectileMaterial);
            }
        }
    }
}
