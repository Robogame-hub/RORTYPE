using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RorType.Gameplay.Player;

namespace RorType.Gameplay.Editor
{
    public static class TopDownMovementSceneBuilder
    {
        private const string SourceScenePath = "Assets/Game/Scene/Level_1.unity";
        private const string TargetScenePath = "Assets/Game/Scene/PlayerMovementTest.unity";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/TopDownPlayer.prefab";
        private const string PlayerMaterialPath = "Assets/Game/Other/Mat_red.mat";

        [MenuItem("RORTYPE/Build Top Down Movement Test Scene")]
        public static void BuildFromMenu()
        {
            BuildTopDownMovementTestScene();
        }

        public static void BuildTopDownMovementTestScene()
        {
            EnsureFolder("Assets/Game", "Prefabs");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath))
                {
                    throw new System.InvalidOperationException($"Failed to copy scene from {SourceScenePath} to {TargetScenePath}.");
                }
            }

            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            var player = CreateOrUpdatePlayer();
            var cameraRig = CreateOrUpdateCamera(player.transform);

            var motor = player.GetComponent<TopDownPlayerMotor>();
            motor.SetMovementReference(cameraRig.transform);

            SavePlayerPrefab(player);
            EnsureBuildSettings(TargetScenePath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateOrUpdatePlayer()
        {
            var player = GameObject.Find("TopDownPlayer");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "TopDownPlayer";
            }

            player.transform.position = new Vector3(0f, 1.1f, 0f);
            player.transform.rotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;

            var material = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
            var meshRenderer = player.GetComponent<MeshRenderer>();
            if (meshRenderer != null && material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            var rigidbody = player.GetComponent<Rigidbody>() ?? player.AddComponent<Rigidbody>();
            rigidbody.mass = 70f;
            rigidbody.useGravity = false;
            rigidbody.linearDamping = 0f;
            rigidbody.angularDamping = 0.05f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var capsuleCollider = player.GetComponent<CapsuleCollider>() ?? player.AddComponent<CapsuleCollider>();
            capsuleCollider.center = Vector3.zero;
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 2f;

            EnsureComponent<TopDownInputAdapter>(player);
            EnsureComponent<TopDownGroundProbe>(player);
            EnsureComponent<TopDownPlayerMotor>(player);
            EnsureComponent<TopDownFacingController>(player);
            EnsureComponent<PlayerRespawnController>(player);

            return player;
        }

        private static GameObject CreateOrUpdateCamera(Transform target)
        {
            var cameraObject = Camera.main != null
                ? Camera.main.gameObject
                : GameObject.Find("TopDownCamera");

            if (cameraObject == null)
            {
                cameraObject = new GameObject("TopDownCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            cameraObject.name = "TopDownCamera";
            cameraObject.tag = "MainCamera";

            var cameraComponent = cameraObject.GetComponent<Camera>() ?? cameraObject.AddComponent<Camera>();
            cameraComponent.fieldOfView = 55f;
            cameraComponent.nearClipPlane = 0.1f;
            cameraComponent.farClipPlane = 500f;

            EnsureComponent<AudioListener>(cameraObject);

            var rig = cameraObject.GetComponent<TopDownCameraRig>() ?? cameraObject.AddComponent<TopDownCameraRig>();
            rig.SetTarget(target);

            cameraObject.transform.position = target.position + new Vector3(-10f, 16f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation((target.position + Vector3.up) - cameraObject.transform.position, Vector3.up);

            return cameraObject;
        }

        private static void SavePlayerPrefab(GameObject player)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                player,
                PlayerPrefabPath,
                InteractionMode.AutomatedAction);
        }

        private static void EnsureBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            var combinedPath = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(combinedPath))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }
    }
}
