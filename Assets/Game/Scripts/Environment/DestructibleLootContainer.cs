using System.Collections;
using System.Collections.Generic;
using RorType.Gameplay.Combat;
using RorType.Gameplay.Interaction;
using RorType.Gameplay.Player;
using UnityEngine;

namespace RorType.Gameplay.Environment
{
    [DisallowMultipleComponent]
    public sealed class DestructibleLootContainer : MonoBehaviour, IDamageable
    {
        [Header("Durability")]
        [SerializeField, Min(1f)] private float maxHealth = 1f;
        [SerializeField, Min(0.1f)] private float debrisLifetime = 2.5f;
        [SerializeField, Min(0f)] private float debrisImpulseMultiplier = 1f;
        [SerializeField, Min(0f)] private float debrisUpwardBoost = 0.65f;
        [SerializeField, Min(0f)] private float debrisRandomSpread = 0.35f;
        [SerializeField, Min(0f)] private float zeroImpulseDebrisScatter = 0.6f;
        [SerializeField, Min(0f)] private float dashImpactDamage = 1f;
        [SerializeField, Min(0f)] private float dashImpactImpulse = 3f;

        [Header("Feedback")]
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Min(1)] private int hitFlashCount = 3;
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;

        [Header("Loot Drops")]
        [SerializeField] private bool dropsLoot;
        [SerializeField] private bool canDropAmmo = true;
        [SerializeField, Min(0)] private int minResourceDrops = 1;
        [SerializeField, Min(0)] private int maxResourceDrops = 3;
        [SerializeField] private ResourcePickupCollectible moneyPickupPrefab;
        [SerializeField] private ResourcePickupCollectible ammoPickupPrefab;
        [SerializeField] private ResourcePickupCollectible healthPickupPrefab;
        [SerializeField, Min(1)] private int moneyPerPickup = 10;
        [SerializeField, Min(1)] private int ammoPerPickup = 10;
        [SerializeField, Min(1)] private int healthPerPickup = 150;
        [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float ammoDropChance = 0.45f;
        [SerializeField, Min(0f)] private float dropLaunchSpeed = 4f;

        private float health;
        private bool destroyed;
        private readonly List<Renderer> visualRenderers = new();
        private Color[] visualBaseColors;
        private Coroutine hitFlashRoutine;

        public CombatTeam Team => CombatTeam.Neutral;
        public bool IsAlive => !destroyed && health > 0f;

        private void Awake()
        {
            health = maxHealth;
            CacheVisualRenderers();
            if (transform.childCount > 0)
            {
                return;
            }

            Debug.LogWarning($"{nameof(DestructibleLootContainer)} on {name} has no authored child debris meshes/colliders.", this);
        }

        public bool ReceiveHit(in CombatHitInfo hitInfo)
        {
            if (!IsAlive || hitInfo.Damage <= 0f)
            {
                return false;
            }

            health = Mathf.Max(0f, health - hitInfo.Damage);
            FloatingWorldText.Spawn(hitInfo.Point + Vector3.up * 0.35f, hitInfo.Damage.ToString("0"), Color.white, 0.1f);
            PlayHitFlash();
            if (health <= 0f)
            {
                BreakApart(hitInfo.Direction, hitInfo.Impulse);
            }

            return true;
        }

        public bool DestroyImmediately(in CombatHitInfo hitInfo)
        {
            if (!IsAlive)
            {
                return false;
            }

            health = 0f;
            BreakApart(hitInfo.Direction, hitInfo.Impulse);
            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryReceiveDashCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryReceiveDashCollision(collision);
        }

        private void TryReceiveDashCollision(Collision collision)
        {
            if (!IsAlive || dashImpactDamage <= 0f || collision == null)
            {
                return;
            }

            var playerMotor = collision.gameObject != null
                ? collision.gameObject.GetComponentInParent<TopDownPlayerMotor>()
                : null;
            if (playerMotor == null && collision.collider != null)
            {
                playerMotor = collision.collider.GetComponentInParent<TopDownPlayerMotor>();
            }

            if (playerMotor == null || !playerMotor.IsDashing)
            {
                return;
            }

            var direction = playerMotor.LastWorldMoveDirection.sqrMagnitude > 0.0001f
                ? playerMotor.LastWorldMoveDirection
                : transform.position - playerMotor.transform.position;
            var point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position + Vector3.up;

            ReceiveHit(new CombatHitInfo(
                dashImpactDamage,
                point,
                direction,
                dashImpactImpulse,
                playerMotor.gameObject,
                CombatTeam.Player));
        }

        private void BreakApart(Vector3 hitDirection, float impulse)
        {
            if (destroyed)
            {
                return;
            }

            destroyed = true;
            var pushDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : transform.forward;
            StopHitFlash();
            RestoreVisualColors();
            SpawnLoot();

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.SetParent(null, true);
                var zeroImpulseDebris = debrisImpulseMultiplier <= 0f;

                var childCollider = child.GetComponent<Collider>();
                if (childCollider != null)
                {
                    childCollider.enabled = true;
                    childCollider.isTrigger = zeroImpulseDebris;
                }

                var body = child.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = child.gameObject.AddComponent<Rigidbody>();
                }

                body.useGravity = true;
                body.isKinematic = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (zeroImpulseDebris)
                {
                    var scatter = Random.insideUnitCircle * zeroImpulseDebrisScatter;
                    body.AddForce(new Vector3(scatter.x, 0f, scatter.y), ForceMode.Impulse);
                    body.AddTorque(Random.insideUnitSphere * zeroImpulseDebrisScatter, ForceMode.Impulse);
                }
                else
                {
                    var debrisForce = Mathf.Max(0f, impulse + 2f) * debrisImpulseMultiplier;
                    body.AddForce((pushDirection + Vector3.up * debrisUpwardBoost + Random.insideUnitSphere * debrisRandomSpread) * debrisForce, ForceMode.Impulse);
                }

                Destroy(child.gameObject, debrisLifetime);
                i--;
            }

            Destroy(gameObject);
        }

        private void SpawnLoot()
        {
            if (!dropsLoot)
            {
                return;
            }

            var horizontalOffset = Random.insideUnitCircle * 0.35f;
            var origin = transform.position + new Vector3(horizontalOffset.x, 0.55f, horizontalOffset.y);
            ResourcePickupCollectible.SpawnEnemyStyleDrops(
                origin,
                canDropAmmo,
                minResourceDrops,
                maxResourceDrops,
                moneyPickupPrefab,
                ammoPickupPrefab,
                healthPickupPrefab,
                moneyPerPickup,
                ammoPerPickup,
                healthPerPickup,
                healthDropChance,
                ammoDropChance,
                dropLaunchSpeed);
        }

        private void CacheVisualRenderers()
        {
            visualRenderers.Clear();
            GetComponentsInChildren(true, visualRenderers);
            visualBaseColors = new Color[visualRenderers.Count];
            for (var i = 0; i < visualRenderers.Count; i++)
            {
                visualBaseColors[i] = ResolveRendererBaseColor(visualRenderers[i]);
            }
        }

        private static Color ResolveRendererBaseColor(Renderer renderer)
        {
            if (renderer == null)
            {
                return Color.white;
            }

            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    return material.GetColor("_BaseColor");
                }

                if (material.HasProperty("_Color"))
                {
                    return material.GetColor("_Color");
                }
            }

            return Color.white;
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
                RestoreVisualColors();
                yield return new WaitForSeconds(hitFlashInterval);
            }

            hitFlashRoutine = null;
        }

        private void StopHitFlash()
        {
            if (hitFlashRoutine == null)
            {
                return;
            }

            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }

        private void SetVisualColor(Color color)
        {
            for (var i = 0; i < visualRenderers.Count; i++)
            {
                RuntimeRendererUtility.SetColor(visualRenderers[i], color);
            }
        }

        private void RestoreVisualColors()
        {
            for (var i = 0; i < visualRenderers.Count; i++)
            {
                var color = visualBaseColors != null && i < visualBaseColors.Length
                    ? visualBaseColors[i]
                    : Color.white;
                RuntimeRendererUtility.SetColor(visualRenderers[i], color);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            debrisLifetime = Mathf.Max(0.1f, debrisLifetime);
            debrisImpulseMultiplier = Mathf.Max(0f, debrisImpulseMultiplier);
            debrisUpwardBoost = Mathf.Max(0f, debrisUpwardBoost);
            debrisRandomSpread = Mathf.Max(0f, debrisRandomSpread);
            zeroImpulseDebrisScatter = Mathf.Max(0f, zeroImpulseDebrisScatter);
            dashImpactDamage = Mathf.Max(0f, dashImpactDamage);
            dashImpactImpulse = Mathf.Max(0f, dashImpactImpulse);
            hitFlashCount = Mathf.Max(1, hitFlashCount);
            hitFlashInterval = Mathf.Max(0.01f, hitFlashInterval);
            minResourceDrops = Mathf.Max(0, minResourceDrops);
            maxResourceDrops = Mathf.Max(minResourceDrops, maxResourceDrops);
            moneyPerPickup = Mathf.Max(1, moneyPerPickup);
            ammoPerPickup = Mathf.Max(1, ammoPerPickup);
            healthPerPickup = Mathf.Max(1, healthPerPickup);
            healthDropChance = Mathf.Clamp01(healthDropChance);
            ammoDropChance = Mathf.Clamp01(ammoDropChance);
            dropLaunchSpeed = Mathf.Max(0f, dropLaunchSpeed);
        }
    }
}
