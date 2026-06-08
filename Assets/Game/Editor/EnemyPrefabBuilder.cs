using UnityEditor;
using UnityEngine;
using RorType.Gameplay.AI;
using UnityEngine.AI;

namespace RorType.Gameplay.Editor
{
    public static class EnemyPrefabBuilder
    {
        private const string PrefabFolderPath = "Assets/Game/Prefabs/Enemies";
        private const string MaterialFolderPath = "Assets/Game/Other";
        private const string MaterialTemplatePath = "Assets/Game/Other/Mat_red.mat";

        [MenuItem("RORTYPE/Build Enemy Prefabs")]
        public static void BuildEnemyPrefabs()
        {
            EnsureFolder("Assets/Game/Prefabs", "Enemies");

            BuildEnemyPrefab(
                "EnemyShooter.prefab",
                "Enemy Shooter",
                EnemyCapsuleArchetype.Shooter,
                new Color(1f, 0.84f, 0.1f),
                "Mat_enemy_shooter.mat",
                5f,
                10.4f,
                20f,
                6f,
                0.85f,
                1.65f,
                0.5f,
                1.9f,
                3);

            BuildEnemyPrefab(
                "EnemyMelee.prefab",
                "Enemy Melee",
                EnemyCapsuleArchetype.Melee,
                new Color(1f, 0.48f, 0.08f),
                "Mat_enemy_melee.mat",
                10f,
                10.2f,
                20f,
                6f,
                0.85f,
                1.65f,
                0.5f,
                1.9f,
                3);

            BuildEnemyPrefab(
                "EnemyExploder.prefab",
                "Enemy Exploder",
                EnemyCapsuleArchetype.Exploder,
                new Color(0.6f, 0.22f, 0.92f),
                "Mat_enemy_exploder.mat",
                3f,
                13.6f,
                20f,
                6f,
                0.85f,
                1.65f,
                0.5f,
                1.9f,
                3);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildEnemyPrefabsBatchMode()
        {
            BuildEnemyPrefabs();
        }

        private static void BuildEnemyPrefab(
            string prefabFileName,
            string rootName,
            EnemyCapsuleArchetype archetype,
            Color baseColor,
            string materialFileName,
            float maxHealth,
            float moveSpeed,
            float shooterAttackRadius,
            float shooterPreferredDistance,
            float shooterInterval,
            float meleeAttackRange,
            float meleeInterval,
            float explodeTriggerRange,
            int explodeFlashCount)
        {
            var root = new GameObject(rootName);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = Vector3.zero;
            collider.radius = 0.5f;
            collider.height = 2f;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 55f;
            body.useGravity = false;
            body.isKinematic = true;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var navMeshAgent = root.AddComponent<NavMeshAgent>();
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.acceleration = Mathf.Max(8f, moveSpeed * 4f);
            navMeshAgent.angularSpeed = 540f;
            navMeshAgent.updateRotation = false;
            navMeshAgent.radius = collider.radius;
            navMeshAgent.height = collider.height;
            navMeshAgent.stoppingDistance = 0f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Object.DestroyImmediate(visualCollider);
            }

            var visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer != null)
            {
                visualRenderer.sharedMaterial = CreateOrUpdateMaterial(materialFileName, baseColor);
            }

            var controller = root.AddComponent<EnemyCapsuleController>();
            ApplyControllerConfiguration(
                controller,
                archetype,
                visual.transform,
                visualRenderer,
                baseColor,
                maxHealth,
                moveSpeed,
                shooterAttackRadius,
                shooterPreferredDistance,
                shooterInterval,
                meleeAttackRange,
                meleeInterval,
                explodeTriggerRange,
                explodeFlashCount);

            var prefabPath = $"{PrefabFolderPath}/{prefabFileName}";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static Material CreateOrUpdateMaterial(string materialFileName, Color color)
        {
            var materialPath = $"{MaterialFolderPath}/{materialFileName}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(MaterialTemplatePath);
                material = template != null
                    ? new Material(template)
                    : new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyControllerConfiguration(
            EnemyCapsuleController controller,
            EnemyCapsuleArchetype archetype,
            Transform visualRoot,
            Renderer visualRenderer,
            Color baseColor,
            float maxHealth,
            float moveSpeed,
            float shooterAttackRadius,
            float shooterPreferredDistance,
            float shooterInterval,
            float meleeAttackRange,
            float meleeInterval,
            float explodeTriggerRange,
            int explodeFlashCount)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("archetype").enumValueIndex = (int)archetype;
            serializedObject.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serializedObject.FindProperty("visualRenderer").objectReferenceValue = visualRenderer;
            serializedObject.FindProperty("baseColor").colorValue = baseColor;
            serializedObject.FindProperty("maxHealth").floatValue = maxHealth;
            serializedObject.FindProperty("detectionRadius").floatValue = 25f;
            serializedObject.FindProperty("loseTargetRadius").floatValue = 30f;
            serializedObject.FindProperty("patrolRadius").floatValue = 4f;
            serializedObject.FindProperty("moveSpeed").floatValue = moveSpeed;
            serializedObject.FindProperty("shooterAttackRadius").floatValue = shooterAttackRadius;
            serializedObject.FindProperty("shooterPreferredDistance").floatValue = shooterPreferredDistance;
            serializedObject.FindProperty("shooterInterval").floatValue = shooterInterval;
            serializedObject.FindProperty("shooterRadialInterval").floatValue = 3f;
            serializedObject.FindProperty("shooterRadialProjectileCount").intValue = 12;
            serializedObject.FindProperty("shooterProjectileMaxDistance").floatValue = 20f;
            serializedObject.FindProperty("meleeAttackRange").floatValue = meleeAttackRange;
            serializedObject.FindProperty("meleeInterval").floatValue = meleeInterval;
            serializedObject.FindProperty("explodeTriggerRange").floatValue = explodeTriggerRange;
            serializedObject.FindProperty("explodeWarningFlashCount").intValue = explodeFlashCount;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
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
