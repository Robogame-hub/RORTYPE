using RorType.Gameplay.Combat;
using RorType.Gameplay.Interaction;
using RorType.Gameplay.Player;
using UnityEngine;

namespace RorType.Gameplay.Environment
{
    [DisallowMultipleComponent]
    public sealed class DestructibleLootContainer : MonoBehaviour, IDamageable
    {
        private enum LootDropKind
        {
            None = 0,
            Gold = 1,
            Ammo = 2,
            Health = 3
        }

        [Header("Durability")]
        [SerializeField, Min(1f)] private float maxHealth = 1f;
        [SerializeField, Min(0.1f)] private float debrisLifetime = 2.5f;
        [SerializeField, Min(0f)] private float debrisImpulseMultiplier = 1f;
        [SerializeField, Min(0f)] private float debrisUpwardBoost = 0.65f;
        [SerializeField, Min(0f)] private float debrisRandomSpread = 0.35f;
        [SerializeField, Min(0f)] private float zeroImpulseDebrisScatter = 0.6f;
        [SerializeField, Min(0f)] private float dashImpactDamage = 1f;
        [SerializeField, Min(0f)] private float dashImpactImpulse = 3f;

        [Header("Loot Weights")]
        [SerializeField, Min(0f)] private float nothingWeight = 1f;
        [SerializeField, Min(0f)] private float goldWeight = 1f;
        [SerializeField, Min(0f)] private float ammoWeight = 1f;
        [SerializeField, Min(0f)] private float healthWeight = 1f;

        [Header("Loot Counts")]
        [SerializeField, Min(1)] private int minGoldPieces = 1;
        [SerializeField, Min(1)] private int maxGoldPieces = 3;
        [SerializeField, Min(1)] private int minAmmoPieces = 1;
        [SerializeField, Min(1)] private int maxAmmoPieces = 2;
        [SerializeField, Min(0f)] private float dropLaunchSpeed = 4f;

        private float health;
        private bool destroyed;

        public CombatTeam Team => CombatTeam.Neutral;
        public bool IsAlive => !destroyed && health > 0f;

        private void Awake()
        {
            health = maxHealth;
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
            if (health <= 0f)
            {
                BreakApart(hitInfo.Direction, hitInfo.Impulse);
            }

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
            SpawnLoot(pushDirection);

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

        private void SpawnLoot(Vector3 pushDirection)
        {
            var dropKind = RollLootKind();
            switch (dropKind)
            {
                case LootDropKind.Gold:
                    SpawnPickupPieces(ResourcePickupCollectible.PickupKind.Money, Random.Range(minGoldPieces, maxGoldPieces + 1), pushDirection);
                    break;
                case LootDropKind.Ammo:
                    SpawnPickupPieces(ResourcePickupCollectible.PickupKind.Ammo, Random.Range(minAmmoPieces, maxAmmoPieces + 1), pushDirection);
                    break;
                case LootDropKind.Health:
                    SpawnPickupPieces(ResourcePickupCollectible.PickupKind.Health, 1, pushDirection);
                    break;
            }
        }

        private LootDropKind RollLootKind()
        {
            var none = Mathf.Max(0f, nothingWeight);
            var gold = Mathf.Max(0f, goldWeight);
            var ammo = Mathf.Max(0f, ammoWeight);
            var health = Mathf.Max(0f, healthWeight);
            var totalWeight = none + gold + ammo + health;
            if (totalWeight <= 0f)
            {
                return LootDropKind.None;
            }

            var roll = Random.value * totalWeight;
            if (roll < none)
            {
                return LootDropKind.None;
            }

            roll -= none;
            if (roll < gold)
            {
                return LootDropKind.Gold;
            }

            roll -= gold;
            if (roll < ammo)
            {
                return LootDropKind.Ammo;
            }

            return LootDropKind.Health;
        }

        private void SpawnPickupPieces(ResourcePickupCollectible.PickupKind kind, int count, Vector3 pushDirection)
        {
            count = Mathf.Max(0, count);
            for (var i = 0; i < count; i++)
            {
                var horizontalOffset = Random.insideUnitCircle * 0.45f;
                var position = transform.position + new Vector3(horizontalOffset.x, 0.55f, horizontalOffset.y);
                var launchDirection = (pushDirection + Vector3.up * 0.85f + Random.insideUnitSphere * 0.4f).normalized;
                ResourcePickupCollectible.Spawn(kind, 1, position, launchDirection * dropLaunchSpeed);
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
            nothingWeight = Mathf.Max(0f, nothingWeight);
            goldWeight = Mathf.Max(0f, goldWeight);
            ammoWeight = Mathf.Max(0f, ammoWeight);
            healthWeight = Mathf.Max(0f, healthWeight);
            minGoldPieces = Mathf.Max(1, minGoldPieces);
            maxGoldPieces = Mathf.Max(minGoldPieces, maxGoldPieces);
            minAmmoPieces = Mathf.Max(1, minAmmoPieces);
            maxAmmoPieces = Mathf.Max(minAmmoPieces, maxAmmoPieces);
            dropLaunchSpeed = Mathf.Max(0f, dropLaunchSpeed);
        }
    }
}
