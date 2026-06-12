using RorType.Gameplay.Combat;
using RorType.Gameplay.Player;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ResourcePickupCollectible : MonoBehaviour
    {
        public enum PickupKind
        {
            Money = 0,
            Ammo = 1,
            Health = 2
        }

        private const float DefaultMagnetRadius = 2f;
        private const float DefaultMagnetSpeed = 8f;
        private const float DefaultMagnetCollectDistance = 0.35f;

        [SerializeField] private PickupKind kind;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(0.1f)] private float lifetime = 18f;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 180f;
        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 4f;
        [SerializeField, Min(0f)] private float magnetRadius = DefaultMagnetRadius;
        [SerializeField, Min(0f)] private float magnetSpeed = DefaultMagnetSpeed;
        [SerializeField, Min(0.01f)] private float magnetCollectDistance = DefaultMagnetCollectDistance;

        private Vector3 basePosition;
        private Rigidbody body;
        private PlayerResourceController magnetTarget;
        private float age;
        private bool collected;

        public static ResourcePickupCollectible Spawn(
            PickupKind pickupKind,
            int pickupAmount,
            Vector3 position,
            Vector3 launchVelocity)
        {
            var pickup = pickupKind == PickupKind.Health
                ? CreateHealthCrossPickup()
                : CreatePrimitivePickup(pickupKind);
            pickup.transform.position = position;

            var trigger = pickup.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.85f;

            var body = pickup.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = launchVelocity;
            body.angularVelocity = Random.insideUnitSphere * 4f;

            var collectible = pickup.AddComponent<ResourcePickupCollectible>();
            collectible.Initialize(pickupKind, pickupAmount);
            return collectible;
        }

        private static GameObject CreatePrimitivePickup(PickupKind pickupKind)
        {
            var shape = Random.value < 0.5f ? PrimitiveType.Sphere : PrimitiveType.Cube;
            var pickup = GameObject.CreatePrimitive(shape);
            pickup.name = pickupKind == PickupKind.Money ? "MoneyPickup" : "AmmoPickup";
            pickup.transform.localScale = Vector3.one * 0.34f;

            var renderer = pickup.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = pickupKind == PickupKind.Money
                    ? new Color(1f, 0.88f, 0.05f)
                    : new Color(1f, 0.12f, 0.08f);
            }

            var collider = pickup.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }

            return pickup;
        }

        private static GameObject CreateHealthCrossPickup()
        {
            var pickup = new GameObject("HealthPickup");
            CreateCrossBar(pickup.transform, "VerticalBar", new Vector3(0.16f, 0.08f, 0.62f));
            CreateCrossBar(pickup.transform, "HorizontalBar", new Vector3(0.62f, 0.08f, 0.16f));
            return pickup;
        }

        private static void CreateCrossBar(Transform parent, string name, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = Vector3.zero;
            bar.transform.localScale = localScale;

            var renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.08f, 0.08f);
            }
        }

        public void Initialize(PickupKind pickupKind, int pickupAmount)
        {
            kind = pickupKind;
            amount = Mathf.Max(1, pickupAmount);
            body = GetComponent<Rigidbody>();
            basePosition = transform.position;
            age = 0f;
            collected = false;
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            age += Time.deltaTime;
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);

            if (age > 0.45f)
            {
                if (body != null && !body.isKinematic)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }

                if (TryUpdateMagnetMovement(Time.deltaTime))
                {
                    return;
                }

                var position = transform.position;
                basePosition = new Vector3(position.x, Mathf.Lerp(basePosition.y, position.y, 4f * Time.deltaTime), position.z);
                position.y = basePosition.y + (Mathf.Sin(age * bobSpeed) * bobHeight);
                transform.position = position;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryCollect(other);
        }

        private void TryCollect(Collider other)
        {
            if (collected || other == null)
            {
                return;
            }

            var resources = other.GetComponentInParent<PlayerResourceController>();
            if (resources == null)
            {
                return;
            }

            Collect(resources);
        }

        private bool TryUpdateMagnetMovement(float deltaTime)
        {
            if (magnetRadius <= 0f || magnetSpeed <= 0f)
            {
                return false;
            }

            if (magnetTarget == null || !IsTargetWithinMagnetRadius(magnetTarget))
            {
                magnetTarget = FindMagnetTarget();
            }

            if (magnetTarget == null)
            {
                return false;
            }

            var targetPosition = GetTargetCollectPosition(magnetTarget);
            var toTarget = targetPosition - transform.position;
            if (toTarget.magnitude <= magnetCollectDistance)
            {
                Collect(magnetTarget);
                return true;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                Mathf.Max(0f, magnetSpeed) * deltaTime);
            basePosition = transform.position;
            return true;
        }

        private PlayerResourceController FindMagnetTarget()
        {
            var playerResources = Object.FindFirstObjectByType<PlayerResourceController>();
            return playerResources != null && IsTargetWithinMagnetRadius(playerResources)
                ? playerResources
                : null;
        }

        private bool IsTargetWithinMagnetRadius(PlayerResourceController resources)
        {
            if (resources == null)
            {
                return false;
            }

            var offset = resources.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= magnetRadius * magnetRadius;
        }

        private static Vector3 GetTargetCollectPosition(PlayerResourceController resources)
        {
            return resources.transform.position + Vector3.up * 0.8f;
        }

        private void Collect(PlayerResourceController resources)
        {
            if (collected || resources == null)
            {
                return;
            }

            collected = true;
            if (kind == PickupKind.Money)
            {
                resources.AddMoney(amount);
                FloatingWorldText.Spawn(transform.position + Vector3.up * 0.6f, $"+{amount} gold", new Color(1f, 0.88f, 0.05f), 0.12f);
            }
            else if (kind == PickupKind.Ammo)
            {
                resources.AddAmmo(amount);
                FloatingWorldText.Spawn(transform.position + Vector3.up * 0.6f, $"+{amount} ammo", Color.red, 0.12f);
            }
            else
            {
                resources.AddHealth(amount);
                FloatingWorldText.Spawn(transform.position + Vector3.up * 0.6f, $"+{amount} HP", Color.green, 0.12f);
            }

            Destroy(gameObject);
        }
    }
}
