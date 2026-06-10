using System.Collections.Generic;
using RorType.Gameplay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RorType.Gameplay.Editor
{
    public static class MinimapBuilder
    {
        private const string LevelScenePath = "Assets/Game/Scene/Level_1.unity";
        private const string MinimapPrefabPath = "Assets/Game/Prefabs/UI/Minimap.prefab";
        private const string LevelMapSpritePath = "Assets/Game/Minimap_var_2.png";
        private const string TopDownPlayerPrefabPath = "Assets/Game/Prefabs/TopDownPlayer.prefab";
        private const string HammerPrefabPath = "Assets/Game/Prefabs/HAMMER.prefab";
        private const string StorePrefabPath = "Assets/Game/Prefabs/Store.prefab";
        private const string CapsulePrefabPath = "Assets/Game/Prefabs/Capsule.prefab";
        private const string PortalPrefabPath = "Assets/Game/Prefabs/Portal.prefab";
        private const string ChestPrefabPath = "Assets/Game/Prefabs/Chest.prefab";
        private const string EnemyShooterPrefabPath = "Assets/Game/Prefabs/Enemies/EnemyShooter.prefab";
        private const string EnemyMeleePrefabPath = "Assets/Game/Prefabs/Enemies/EnemyMelee.prefab";
        private const string EnemyExploderPrefabPath = "Assets/Game/Prefabs/Enemies/EnemyExploder.prefab";

        private static readonly Color PlayerColor = new(0.15f, 0.9f, 0.25f, 1f);
        private static readonly Color EnemyColor = new(0.95f, 0.2f, 0.2f, 1f);
        private static readonly Color PointOfInterestColor = new(0.2f, 0.55f, 1f, 1f);
        private static readonly Color LootColor = new(0.2f, 0.55f, 1f, 1f);

        [MenuItem("RORTYPE/Build Minimap")]
        public static void BuildMinimap()
        {
            EnsureFolder("Assets/Game/Prefabs", "UI");

            var levelMapSprite = EnsureSingleSprite(LevelMapSpritePath);
            BuildOrUpdateMinimapPrefab(levelMapSprite);
            ConfigureTrackablePrefabs();
            AddMinimapToLevelScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildMinimapBatchMode()
        {
            BuildMinimap();
        }

        private static void BuildOrUpdateMinimapPrefab(Sprite levelMapSprite)
        {
            var root = new GameObject("MinimapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasRect = root.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateUiObject("MinimapPanel", root.transform, new Vector2(500f, 500f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24f, -24f);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetDefaultUiSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.75f);
            panelImage.raycastTarget = false;

            var viewport = CreateUiObject("MapViewport", panel.transform, new Vector2(452f, 452f));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = Vector2.zero;

            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = GetDefaultUiSprite();
            viewportImage.type = Image.Type.Sliced;
            viewportImage.color = new Color(0f, 0f, 0f, 0.2f);
            viewportImage.raycastTarget = false;
            viewport.AddComponent<RectMask2D>();

            var mapImageObject = CreateUiObject("MapImage", viewport.transform, viewportRect.sizeDelta);
            var mapImageRect = mapImageObject.GetComponent<RectTransform>();
            mapImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapImageRect.pivot = new Vector2(0.5f, 0.5f);
            mapImageRect.anchoredPosition = Vector2.zero;

            var mapImage = mapImageObject.AddComponent<Image>();
            mapImage.sprite = levelMapSprite;
            mapImage.color = new Color(1f, 1f, 1f, 0.75f);
            mapImage.preserveAspect = true;
            mapImage.raycastTarget = false;

            var iconsRoot = CreateUiObject("Markers", viewport.transform, viewportRect.sizeDelta);
            var iconsRootRect = iconsRoot.GetComponent<RectTransform>();
            iconsRootRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconsRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconsRootRect.pivot = new Vector2(0.5f, 0.5f);
            iconsRootRect.anchoredPosition = Vector2.zero;

            var playerMarker = CreateMarker("PlayerMarker", iconsRoot.transform, 18f, MinimapMarkerShape.Arrow, PlayerColor);
            var iconSlots = CreateIconSlots(iconsRoot.transform, 96);

            var controller = panel.AddComponent<MinimapController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("mapViewport").objectReferenceValue = viewportRect;
            serializedController.FindProperty("mapImage").objectReferenceValue = mapImage;
            serializedController.FindProperty("playerMarker").objectReferenceValue = playerMarker;
            serializedController.FindProperty("playerMarkerGraphic").objectReferenceValue = playerMarker.GetComponent<MinimapMarkerGraphic>();
            serializedController.FindProperty("worldSizeMeters").vector2Value = new Vector2(500f, 500f);
            serializedController.FindProperty("worldCenter").vector2Value = Vector2.zero;
            serializedController.FindProperty("iconEdgePadding").floatValue = 10f;
            serializedController.FindProperty("clampIconsToMapBounds").boolValue = true;

            var iconSlotsProperty = serializedController.FindProperty("iconSlots");
            iconSlotsProperty.arraySize = iconSlots.Count;
            for (var index = 0; index < iconSlots.Count; index++)
            {
                var slotProperty = iconSlotsProperty.GetArrayElementAtIndex(index);
                slotProperty.FindPropertyRelative("root").objectReferenceValue = iconSlots[index].root;
                slotProperty.FindPropertyRelative("graphic").objectReferenceValue = iconSlots[index].graphic;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            PrefabUtility.SaveAsPrefabAsset(root, MinimapPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void ConfigureTrackablePrefabs()
        {
            ConfigureTrackablePrefab(TopDownPlayerPrefabPath, MinimapIconGroup.Player, MinimapMarkerShape.Arrow, PlayerColor, 20f, true);
            ConfigureTrackablePrefab(HammerPrefabPath, MinimapIconGroup.PointOfInterest, MinimapMarkerShape.Triangle, PointOfInterestColor, 16f, false);
            ConfigureTrackablePrefab(StorePrefabPath, MinimapIconGroup.PointOfInterest, MinimapMarkerShape.Square, PointOfInterestColor, 16f, false);
            ConfigureTrackablePrefab(CapsulePrefabPath, MinimapIconGroup.Loot, MinimapMarkerShape.Cross, LootColor, 14f, false);
            ConfigureTrackablePrefab(PortalPrefabPath, MinimapIconGroup.PointOfInterest, MinimapMarkerShape.Circle, PointOfInterestColor, 16f, false);
            ConfigureTrackablePrefab(ChestPrefabPath, MinimapIconGroup.Loot, MinimapMarkerShape.Cross, LootColor, 14f, false);
            ConfigureTrackablePrefab(EnemyShooterPrefabPath, MinimapIconGroup.Enemy, MinimapMarkerShape.Cross, EnemyColor, 14f, false);
            ConfigureTrackablePrefab(EnemyMeleePrefabPath, MinimapIconGroup.Enemy, MinimapMarkerShape.Cross, EnemyColor, 14f, false);
            ConfigureTrackablePrefab(EnemyExploderPrefabPath, MinimapIconGroup.Enemy, MinimapMarkerShape.Cross, EnemyColor, 14f, false);
        }

        private static void ConfigureTrackablePrefab(
            string prefabPath,
            MinimapIconGroup iconGroup,
            MinimapMarkerShape markerShape,
            Color iconColor,
            float markerSize,
            bool rotateWithWorldYaw)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var trackable = prefabRoot.GetComponent<MinimapTrackable>();
            if (trackable == null)
            {
                trackable = prefabRoot.AddComponent<MinimapTrackable>();
            }

            ConfigureTrackableSerialized(new SerializedObject(trackable), prefabRoot.transform, iconGroup, markerShape, iconColor, markerSize, rotateWithWorldYaw);
            EditorUtility.SetDirty(trackable);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void AddMinimapToLevelScene()
        {
            var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MinimapPrefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Minimap prefab was not created at {MinimapPrefabPath}.");
            }

            var existingCanvas = GameObject.Find("MinimapCanvas");
            GameObject canvasInstance;
            if (existingCanvas == null)
            {
                canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                canvasInstance.name = "MinimapCanvas";
            }
            else if (PrefabUtility.IsPartOfPrefabInstance(existingCanvas))
            {
                PrefabUtility.RevertPrefabInstance(existingCanvas, InteractionMode.AutomatedAction);
                canvasInstance = existingCanvas;
            }
            else
            {
                Object.DestroyImmediate(existingCanvas);
                canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                canvasInstance.name = "MinimapCanvas";
            }

            EnsureSceneTrackables(scene);

            var controller = canvasInstance.GetComponentInChildren<MinimapController>(true);
            if (controller != null)
            {
                var serializedController = new SerializedObject(controller);
                var bounds = CalculateSceneTrackableBounds(scene);
                serializedController.FindProperty("worldSizeMeters").vector2Value = bounds.size;
                serializedController.FindProperty("worldCenter").vector2Value = bounds.center;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Bounds2D CalculateSceneTrackableBounds(Scene scene)
        {
            var hasAny = false;
            var min = Vector2.zero;
            var max = Vector2.zero;

            foreach (var trackable in EnumerateSceneTrackables(scene))
            {
                if (trackable == null)
                {
                    continue;
                }

                var position = trackable.TrackedTransform.position + trackable.WorldOffset;
                var point = new Vector2(position.x, position.z);
                if (!hasAny)
                {
                    min = point;
                    max = point;
                    hasAny = true;
                    continue;
                }

                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            if (!hasAny)
            {
                return new Bounds2D(Vector2.zero, new Vector2(500f, 500f));
            }

            var size = max - min;
            size.x = Mathf.Max(500f, size.x + 40f);
            size.y = Mathf.Max(500f, size.y + 40f);
            return new Bounds2D((min + max) * 0.5f, size);
        }

        private static void EnsureSceneTrackables(Scene scene)
        {
            EnsureSceneTrackable(scene, "TopDownPlayer", MinimapIconGroup.Player, MinimapMarkerShape.Arrow, PlayerColor, 20f, true, false);
            EnsureSceneTrackable(scene, "HAMMER", MinimapIconGroup.PointOfInterest, MinimapMarkerShape.Triangle, PointOfInterestColor, 16f, false, true);
            EnsureSceneTrackable(scene, "Store", MinimapIconGroup.PointOfInterest, MinimapMarkerShape.Square, PointOfInterestColor, 16f, false, false);
            EnsureSceneTrackable(scene, "Portal", MinimapIconGroup.PointOfInterest, MinimapMarkerShape.Circle, PointOfInterestColor, 16f, false, false);
            EnsureSceneTrackable(scene, "Chest", MinimapIconGroup.Loot, MinimapMarkerShape.Cross, LootColor, 14f, false, true);
            EnsureSceneTrackable(scene, "Capsule", MinimapIconGroup.Loot, MinimapMarkerShape.Cross, LootColor, 14f, false, true);
        }

        private static void EnsureSceneTrackable(
            Scene scene,
            string nameToken,
            MinimapIconGroup iconGroup,
            MinimapMarkerShape markerShape,
            Color iconColor,
            float markerSize,
            bool rotateWithWorldYaw,
            bool prefixMatch)
        {
            foreach (var gameObject in EnumerateSceneObjects(scene))
            {
                if (!NameMatches(gameObject.name, nameToken, prefixMatch))
                {
                    continue;
                }

                var trackable = gameObject.GetComponent<MinimapTrackable>();
                if (trackable == null)
                {
                    trackable = gameObject.AddComponent<MinimapTrackable>();
                }

                ConfigureTrackableSerialized(new SerializedObject(trackable), gameObject.transform, iconGroup, markerShape, iconColor, markerSize, rotateWithWorldYaw);
                EditorUtility.SetDirty(trackable);
            }
        }

        private static void ConfigureTrackableSerialized(
            SerializedObject serializedTrackable,
            Transform trackedTransform,
            MinimapIconGroup iconGroup,
            MinimapMarkerShape markerShape,
            Color iconColor,
            float markerSize,
            bool rotateWithWorldYaw)
        {
            serializedTrackable.FindProperty("trackedTransform").objectReferenceValue = trackedTransform;
            serializedTrackable.FindProperty("iconGroup").enumValueIndex = (int)iconGroup;
            serializedTrackable.FindProperty("markerShape").enumValueIndex = (int)markerShape;
            serializedTrackable.FindProperty("iconColor").colorValue = iconColor;
            serializedTrackable.FindProperty("markerSize").floatValue = markerSize;
            serializedTrackable.FindProperty("rotateWithWorldYaw").boolValue = rotateWithWorldYaw;
            serializedTrackable.FindProperty("worldOffset").vector3Value = Vector3.zero;
            serializedTrackable.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerable<MinimapTrackable> EnumerateSceneTrackables(Scene scene)
        {
            foreach (var gameObject in EnumerateSceneObjects(scene))
            {
                var trackable = gameObject.GetComponent<MinimapTrackable>();
                if (trackable != null)
                {
                    yield return trackable;
                }
            }
        }

        private static IEnumerable<GameObject> EnumerateSceneObjects(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                foreach (var gameObject in EnumerateGameObjectRecursive(roots[index].transform))
                {
                    yield return gameObject;
                }
            }
        }

        private static IEnumerable<GameObject> EnumerateGameObjectRecursive(Transform root)
        {
            yield return root.gameObject;

            for (var index = 0; index < root.childCount; index++)
            {
                foreach (var child in EnumerateGameObjectRecursive(root.GetChild(index)))
                {
                    yield return child;
                }
            }
        }

        private static bool NameMatches(string candidate, string nameToken, bool prefixMatch)
        {
            return prefixMatch
                ? candidate == nameToken || candidate.StartsWith($"{nameToken} (", System.StringComparison.Ordinal)
                : candidate == nameToken;
        }

        private static Sprite EnsureSingleSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException($"Texture importer not found for {assetPath}.");
            }

            var requiresReimport = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                requiresReimport = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                requiresReimport = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                requiresReimport = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                requiresReimport = true;
            }

            if (requiresReimport)
            {
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new System.InvalidOperationException($"Sprite was not imported from {assetPath}.");
            }

            return sprite;
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return gameObject;
        }

        private static RectTransform CreateMarker(string name, Transform parent, float size, MinimapMarkerShape shape, Color color)
        {
            var marker = CreateUiObject(name, parent, new Vector2(size, size));
            var graphic = marker.AddComponent<MinimapMarkerGraphic>();
            graphic.raycastTarget = false;
            graphic.SetAppearance(shape, color);
            return marker.GetComponent<RectTransform>();
        }

        private static List<(RectTransform root, MinimapMarkerGraphic graphic)> CreateIconSlots(Transform parent, int count)
        {
            var slots = new List<(RectTransform root, MinimapMarkerGraphic graphic)>(count);
            for (var index = 0; index < count; index++)
            {
                var root = CreateUiObject($"MarkerSlot_{index:00}", parent, new Vector2(14f, 14f)).GetComponent<RectTransform>();
                var graphic = root.gameObject.AddComponent<MinimapMarkerGraphic>();
                graphic.raycastTarget = false;
                graphic.SetAppearance(MinimapMarkerShape.Square, Color.white);
                root.gameObject.SetActive(false);
                slots.Add((root, graphic));
            }

            return slots;
        }

        private static Sprite GetDefaultUiSprite()
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null)
            {
                return sprite;
            }

            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void EnsureFolder(string parentPath, string folderName)
        {
            var fullPath = $"{parentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        private readonly struct Bounds2D
        {
            public Bounds2D(Vector2 center, Vector2 size)
            {
                this.center = center;
                this.size = size;
            }

            public Vector2 center { get; }
            public Vector2 size { get; }
        }
    }
}
