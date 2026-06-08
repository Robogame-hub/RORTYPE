using System.Collections.Generic;
using RorType.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RorType.Gameplay.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EnemySpawnZone : MonoBehaviour
    {
        private const float DefaultSpawnInterval = 10f;
        private const int DefaultMaxActiveEnemies = 40;

        [Header("Activation")]
        [SerializeField, Min(1)] private int maxActiveEnemies = DefaultMaxActiveEnemies;
        [SerializeField, Min(0.1f)] private float minSpawnInterval = 3.5f;
        [SerializeField, Min(0.1f)] private float maxSpawnInterval = 6f;
        [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 2f;
        [SerializeField, Min(0.1f)] private float spawnPointBlockedRadius = 0.8f;

        [Header("Enemy Prefabs")]
        [SerializeField] private EnemyCapsuleController meleePrefab;
        [SerializeField] private EnemyCapsuleController shooterPrefab;
        [SerializeField] private EnemyCapsuleController exploderPrefab;

        [Header("Spawn Weights")]
        [SerializeField, Min(0f)] private float meleeWeight = 50f;
        [SerializeField, Min(0f)] private float shooterWeight = 30f;
        [SerializeField, Min(0f)] private float exploderWeight = 20f;

        private readonly List<EnemyCapsuleController> activeEnemies = new();
        private readonly List<EnemySpawnPoint> spawnPoints = new();
        private readonly HashSet<Transform> playersInside = new();
        private BoxCollider triggerZone;
        private float spawnTimer;
        private bool spawnTimerInitialized;

        private void Awake()
        {
            triggerZone = GetComponent<BoxCollider>();
            triggerZone.isTrigger = true;
            RefreshSpawnPoints();
        }

        private void OnValidate()
        {
            if (maxSpawnInterval < minSpawnInterval)
            {
                maxSpawnInterval = minSpawnInterval;
            }

            triggerZone = GetComponent<BoxCollider>();
            if (triggerZone != null)
            {
                triggerZone.isTrigger = true;
            }

            RefreshSpawnPoints();
        }

        private void Update()
        {
            CleanupDestroyedEnemies();

            if (!IsPlayerInside() || activeEnemies.Count >= GetActiveEnemyLimit())
            {
                spawnTimerInitialized = false;
                return;
            }

            if (!spawnTimerInitialized)
            {
                spawnTimer = GetNextSpawnInterval();
                spawnTimerInitialized = true;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f)
            {
                return;
            }

            if (TrySpawnEnemy())
            {
                spawnTimer = GetNextSpawnInterval();
                spawnTimerInitialized = true;
            }
            else
            {
                spawnTimer = 1f;
                spawnTimerInitialized = true;
            }
        }

        [ContextMenu("Refresh Spawn Points")]
        public void RefreshSpawnPoints()
        {
            spawnPoints.Clear();
            var discoveredPoints = GetComponentsInChildren<EnemySpawnPoint>(true);
            for (var i = 0; i < discoveredPoints.Length; i++)
            {
                if (discoveredPoints[i] != null)
                {
                    spawnPoints.Add(discoveredPoints[i]);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<TopDownPlayerMotor>();
            if (player == null)
            {
                return;
            }

            playersInside.Add(player.transform.root);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<TopDownPlayerMotor>();
            if (player == null)
            {
                return;
            }

            playersInside.Remove(player.transform.root);
        }

        private bool TrySpawnEnemy()
        {
            if (spawnPoints.Count == 0)
            {
                RefreshSpawnPoints();
                if (spawnPoints.Count == 0)
                {
                    return false;
                }
            }

            var startIndex = Random.Range(0, spawnPoints.Count);
            var availableCapacity = Mathf.Max(0, GetActiveEnemyLimit() - activeEnemies.Count);
            for (var offset = 0; offset < spawnPoints.Count; offset++)
            {
                if (availableCapacity <= 0)
                {
                    break;
                }

                var spawnPoint = spawnPoints[(startIndex + offset) % spawnPoints.Count];
                if (spawnPoint == null)
                {
                    continue;
                }

                var spawnedAnyAtPoint = false;
                var targetBurstCount = Mathf.Min(spawnPoint.SpawnBurstCount, availableCapacity);
                var maxAttempts = Mathf.Max(targetBurstCount * 3, 1);
                for (var attempt = 0; attempt < maxAttempts && targetBurstCount > 0 && activeEnemies.Count < GetActiveEnemyLimit(); attempt++)
                {
                    var prefab = ChooseEnemyPrefab();
                    if (prefab == null)
                    {
                        break;
                    }

                    if (!TryResolveSpawnPosition(spawnPoint, out var spawnPosition))
                    {
                        continue;
                    }

                    if (IsSpawnPointBlocked(spawnPosition))
                    {
                        continue;
                    }

                    var enemy = Instantiate(prefab, spawnPosition, spawnPoint.Rotation, transform);
                    enemy.SetPatrolAnchor(spawnPosition);
                    activeEnemies.Add(enemy);
                    availableCapacity--;
                    targetBurstCount--;
                    spawnedAnyAtPoint = true;
                }

                if (spawnedAnyAtPoint)
                {
                    return true;
                }
            }

            return false;
        }

        private EnemyCapsuleController ChooseEnemyPrefab()
        {
            var totalWeight = 0f;
            totalWeight += meleePrefab != null ? Mathf.Max(0f, meleeWeight) : 0f;
            totalWeight += shooterPrefab != null ? Mathf.Max(0f, shooterWeight) : 0f;
            totalWeight += exploderPrefab != null ? Mathf.Max(0f, exploderWeight) : 0f;

            if (totalWeight <= 0f)
            {
                return null;
            }

            var roll = Random.Range(0f, totalWeight);
            if (meleePrefab != null)
            {
                roll -= Mathf.Max(0f, meleeWeight);
                if (roll <= 0f)
                {
                    return meleePrefab;
                }
            }

            if (shooterPrefab != null)
            {
                roll -= Mathf.Max(0f, shooterWeight);
                if (roll <= 0f)
                {
                    return shooterPrefab;
                }
            }

            return exploderPrefab;
        }

        private bool TryResolveSpawnPosition(EnemySpawnPoint spawnPoint, out Vector3 spawnPosition)
        {
            if (NavMesh.SamplePosition(
                    spawnPoint.GetRandomSpawnPosition(),
                    out var navMeshHit,
                    Mathf.Max(0.1f, navMeshSampleDistance + spawnPoint.SpawnRadius),
                    NavMesh.AllAreas))
            {
                spawnPosition = navMeshHit.position;
                return true;
            }

            spawnPosition = spawnPoint.Position;
            return false;
        }

        private bool IsSpawnPointBlocked(Vector3 spawnPosition)
        {
            var radius = Mathf.Max(0.1f, spawnPointBlockedRadius);
            for (var i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var offset = enemy.transform.position - spawnPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupDestroyedEnemies()
        {
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
            {
                if (activeEnemies[i] == null)
                {
                    activeEnemies.RemoveAt(i);
                }
            }

            playersInside.RemoveWhere(player => player == null);
        }

        private bool IsPlayerInside()
        {
            return playersInside.Count > 0;
        }

        private float GetNextSpawnInterval()
        {
            var minInterval = minSpawnInterval > 0f ? minSpawnInterval : DefaultSpawnInterval;
            var maxInterval = maxSpawnInterval > 0f ? maxSpawnInterval : minInterval;
            if (maxInterval < minInterval)
            {
                maxInterval = minInterval;
            }

            return Random.Range(minInterval, maxInterval);
        }

        private int GetActiveEnemyLimit()
        {
            return Mathf.Max(1, maxActiveEnemies);
        }
    }
}
