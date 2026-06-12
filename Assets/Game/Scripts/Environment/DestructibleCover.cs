using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Environment
{
    [DisallowMultipleComponent]
    public sealed class DestructibleCover : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 15f;
        [SerializeField] private Vector3 coverSize = new Vector3(4f, 2f, 0.6f);
        [SerializeField, Min(0.1f)] private float debrisLifetime = 2.5f;

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

            Debug.LogWarning($"{nameof(DestructibleCover)} on {name} has no authored cover blocks. Add child block meshes/colliders to the prefab.", this);
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

        private void BreakApart(Vector3 hitDirection, float impulse)
        {
            if (destroyed)
            {
                return;
            }

            destroyed = true;
            var pushDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : transform.forward;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var childCollider = child.GetComponent<Collider>();
                if (childCollider != null)
                {
                    childCollider.enabled = true;
                }

                var body = child.gameObject.AddComponent<Rigidbody>();
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.AddForce((pushDirection + Vector3.up * 0.6f + Random.insideUnitSphere * 0.3f) * Mathf.Max(2f, impulse + 2f), ForceMode.Impulse);
                child.SetParent(null, true);
                Destroy(child.gameObject, debrisLifetime);
                i--;
            }

            Destroy(gameObject);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            coverSize.x = Mathf.Max(0.1f, coverSize.x);
            coverSize.y = Mathf.Max(0.1f, coverSize.y);
            coverSize.z = Mathf.Max(0.1f, coverSize.z);
            debrisLifetime = Mathf.Max(0.1f, debrisLifetime);
        }
    }
}
