using RorType.Gameplay.AI;
using UnityEditor;
using UnityEngine;

namespace RorType.Gameplay.Editor
{
    public static class SpawnZonePrefabBuilder
    {
        private const string SpawningFolderPath = "Assets/Game/Prefabs/Spawning";
        private const string SpawnZonePrefabPath = "Assets/Game/Prefabs/Spawning/EnemySpawnZone.prefab";
        private const string EnemyMeleePrefabPath = "Assets/Game/Prefabs/Enemies/EnemyMelee.prefab";
        private const string EnemyShooterPrefabPath = "Assets/Game/Prefabs/Enemies/EnemyShooter.prefab";
        private const string EnemyExploderPrefabPath = "Assets/Game/Prefabs/Enemies/EnemyExploder.prefab";

        [MenuItem("RORTYPE/Build Enemy Spawn Zone Prefab")]
        public static void BuildSpawnZonePrefab()
        {
            EnemyPrefabBuilder.BuildEnemyPrefabs();
            EnsureFolder("Assets/Game/Prefabs", "Spawning");

            var root = new GameObject("Enemy Spawn Zone");

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(18f, 4f, 18f);

            CreateSpawnPoint(root.transform, "SpawnPoint_01", new Vector3(-5.5f, 0f, -5.5f), 2f);
            CreateSpawnPoint(root.transform, "SpawnPoint_02", new Vector3(5.5f, 0f, -5.5f), 2f);
            CreateSpawnPoint(root.transform, "SpawnPoint_03", new Vector3(-5.5f, 0f, 5.5f), 2f);
            CreateSpawnPoint(root.transform, "SpawnPoint_04", new Vector3(5.5f, 0f, 5.5f), 2f);

            var spawnZone = root.AddComponent<EnemySpawnZone>();
            ApplySpawnZoneConfiguration(spawnZone);
            spawnZone.RefreshSpawnPoints();

            PrefabUtility.SaveAsPrefabAsset(root, SpawnZonePrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildSpawnZonePrefabBatchMode()
        {
            BuildSpawnZonePrefab();
        }

        private static void ApplySpawnZoneConfiguration(EnemySpawnZone spawnZone)
        {
            var serializedObject = new SerializedObject(spawnZone);
            serializedObject.FindProperty("maxActiveEnemies").intValue = 40;
            serializedObject.FindProperty("minSpawnInterval").floatValue = 3.5f;
            serializedObject.FindProperty("maxSpawnInterval").floatValue = 6f;
            serializedObject.FindProperty("navMeshSampleDistance").floatValue = 2f;
            serializedObject.FindProperty("spawnPointBlockedRadius").floatValue = 0.8f;
            serializedObject.FindProperty("meleePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyCapsuleController>(EnemyMeleePrefabPath);
            serializedObject.FindProperty("shooterPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyCapsuleController>(EnemyShooterPrefabPath);
            serializedObject.FindProperty("exploderPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyCapsuleController>(EnemyExploderPrefabPath);
            serializedObject.FindProperty("meleeWeight").floatValue = 50f;
            serializedObject.FindProperty("shooterWeight").floatValue = 30f;
            serializedObject.FindProperty("exploderWeight").floatValue = 20f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawnZone);
        }

        private static void CreateSpawnPoint(Transform parent, string objectName, Vector3 localPosition, float uniformScale)
        {
            var spawnPoint = new GameObject(objectName);
            spawnPoint.transform.SetParent(parent, false);
            spawnPoint.transform.localPosition = localPosition;
            spawnPoint.transform.localRotation = Quaternion.identity;
            spawnPoint.transform.localScale = Vector3.one * Mathf.Max(1f, uniformScale);
            spawnPoint.AddComponent<EnemySpawnPoint>();
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            var combinedPath = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(combinedPath))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }
    }
}
