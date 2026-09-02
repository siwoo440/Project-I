using System.Collections.Generic;
using System.IO;
using ProjectI.Items;
using ProjectI.Loop;
using ProjectI.Wagon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectI.EditorTools
{
    [InitializeOnLoad]
    public static class Phase5Day24Step2AdditiveMapSetup
    {
        private const string SourceOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity";
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity";
        private const string OfficeScenePath = "Assets/ProjectI/Scenes/01_Office.unity";
        private const string TestDungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity";
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab";
        private const string GeneratedPrefabFolder = "Assets/ProjectI/Prefabs/Day24";
        private const string GeneratedPlayerPrefabPath = "Assets/ProjectI/Prefabs/Day24/PlayerPersistent.prefab";
        private const string GeneratedMaterialFolder = "Assets/ProjectI/Materials/Day24";
        private const float OfficeEntryDistance = 12f;
        private const float DungeonEntryDistance = 18f;

        static Phase5Day24Step2AdditiveMapSetup()
        {
            EditorApplication.delayCall += ApplyStep2Automatically;
        }

        [MenuItem("Tools/Project I/Day 24/Apply Step 2 - Persistent Wagon And Additive Maps")]
        public static void ApplyStep2()
        {
            if (!File.Exists(SourceOfficeScenePath))
            {
                Debug.LogError($"[Project I] 23일차 Office 원본 씬이 없습니다 / {SourceOfficeScenePath}");
                return;
            }

            Phase5Day24Step1WagonItemSetup.ApplyStep1();
            SourceSceneData sourceData = ReadSourceSceneDataAndSavePlayerPrefab();

            if (!sourceData.IsValid)
            {
                return;
            }

            CreateOfficeScene(sourceData);
            CreateTestDungeonScene(sourceData);
            CreatePersistentScene(sourceData);
            PatchBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
            Debug.Log("[Project I] 24일차 2단계 적용 / 00_WagonPersistent + 01_Office + 02_TestDungeon");
        }

        private static void ApplyStep2Automatically()
        {
            EditorApplication.delayCall -= ApplyStep2Automatically;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (AreGeneratedScenesPresent())
            {
                PatchBuildSettings();
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && activeScene.isDirty)
            {
                Debug.LogWarning("[Project I] 저장되지 않은 씬 변경이 있어 24일차 2단계 자동 생성을 건너뜁니다. 저장 후 Tools > Project I > Day 24 메뉴를 실행하세요.");
                return;
            }

            ApplyStep2();
        }

        private static bool AreGeneratedScenesPresent()
        {
            return File.Exists(PersistentScenePath) &&
                   File.Exists(OfficeScenePath) &&
                   File.Exists(TestDungeonScenePath);
        }

        private static SourceSceneData ReadSourceSceneDataAndSavePlayerPrefab()
        {
            EnsureFolder(GeneratedPrefabFolder);
            Scene sourceScene = EditorSceneManager.OpenScene(SourceOfficeScenePath, OpenSceneMode.Single);
            PlayerCarryController carryController = FindComponentInScene<PlayerCarryController>(sourceScene);
            WagonCargoArea cargoArea = FindComponentInScene<WagonCargoArea>(sourceScene);

            if (carryController == null)
            {
                Debug.LogError("[Project I] ExplorationOffice에서 PlayerCarryController를 찾지 못했습니다.");
                return default;
            }

            if (cargoArea == null)
            {
                Debug.LogError("[Project I] ExplorationOffice에서 WagonCargoArea를 찾지 못했습니다.");
                return default;
            }

            GameObject playerRoot = carryController.transform.root.gameObject;
            GameObject wagonRoot = cargoArea.transform.root.gameObject;

            if (playerRoot == wagonRoot)
            {
                Debug.LogError("[Project I] Player와 Wagon 루트가 동일하게 감지되어 2단계 생성을 중단합니다.");
                return default;
            }

            GameObject savedPlayerPrefab = PrefabUtility.SaveAsPrefabAsset(playerRoot, GeneratedPlayerPrefabPath);

            if (savedPlayerPrefab == null)
            {
                Debug.LogError("[Project I] Persistent Player prefab 생성에 실패했습니다.");
                return default;
            }

            SourceSceneData data = new SourceSceneData
            {
                IsValid = true,
                PlayerPosition = playerRoot.transform.position,
                PlayerRotation = playerRoot.transform.rotation,
                WagonPosition = wagonRoot.transform.position,
                WagonRotation = wagonRoot.transform.rotation
            };
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void CreateOfficeScene(SourceSceneData sourceData)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OfficeScenePath) != null)
            {
                AssetDatabase.DeleteAsset(OfficeScenePath);
            }

            if (!AssetDatabase.CopyAsset(SourceOfficeScenePath, OfficeScenePath))
            {
                Debug.LogError("[Project I] 01_Office 씬 복사에 실패했습니다.");
                return;
            }

            AssetDatabase.Refresh();
            Scene officeScene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single);
            RemovePersistentRootsFromOffice(officeScene);
            CreateTravelAnchor(
                officeScene,
                TravelDestination.Office,
                sourceData.WagonPosition - (sourceData.WagonRotation * Vector3.forward * OfficeEntryDistance),
                sourceData.WagonPosition,
                sourceData.WagonRotation);
            EditorSceneManager.SaveScene(officeScene, OfficeScenePath);
        }

        private static void RemovePersistentRootsFromOffice(Scene officeScene)
        {
            List<GameObject> rootsToRemove = new List<GameObject>();
            PlayerCarryController carryController = FindComponentInScene<PlayerCarryController>(officeScene);
            WagonCargoArea cargoArea = FindComponentInScene<WagonCargoArea>(officeScene);

            if (carryController != null)
            {
                AddUniqueRoot(rootsToRemove, carryController.transform.root.gameObject);
            }

            if (cargoArea != null)
            {
                AddUniqueRoot(rootsToRemove, cargoArea.transform.root.gameObject);
            }

            foreach (GameObject root in rootsToRemove)
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreatePersistentScene(SourceSceneData sourceData)
        {
            Scene persistentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPlayerPrefabPath);
            GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath);

            if (playerPrefab == null || wagonPrefab == null)
            {
                Debug.LogError("[Project I] Persistent 씬 생성에 필요한 Player/Wagon prefab이 없습니다.");
                return;
            }

            GameObject playerInstance = PrefabUtility.InstantiatePrefab(playerPrefab, persistentScene) as GameObject;
            GameObject wagonInstance = PrefabUtility.InstantiatePrefab(wagonPrefab, persistentScene) as GameObject;

            if (playerInstance == null || wagonInstance == null)
            {
                Debug.LogError("[Project I] Persistent Player/Wagon 인스턴스 생성에 실패했습니다.");
                return;
            }

            playerInstance.name = "Day24_PersistentPlayer";
            wagonInstance.name = "Day24_PersistentWagon";
            playerInstance.transform.SetPositionAndRotation(sourceData.PlayerPosition, sourceData.PlayerRotation);
            wagonInstance.transform.SetPositionAndRotation(sourceData.WagonPosition, sourceData.WagonRotation);

            CanvasGroup fadeGroup = CreateFadeOverlay();
            GameObject loaderObject = new GameObject("Day24_PersistentMapLoader");
            SceneManager.MoveGameObjectToScene(loaderObject, persistentScene);
            PersistentMapLoader loader = loaderObject.AddComponent<PersistentMapLoader>();
            loader.Configure(wagonInstance.transform, playerInstance.transform, fadeGroup);

            EditorSceneManager.SaveScene(persistentScene, PersistentScenePath);
        }

        private static CanvasGroup CreateFadeOverlay()
        {
            GameObject canvasObject = new GameObject(
                "Day24_TransitionCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = false;

            GameObject panelObject = new GameObject("FadePanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image image = panelObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return group;
        }

        private static void CreateTestDungeonScene(SourceSceneData sourceData)
        {
            Scene dungeonScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material floorMaterial = GetOrCreateMaterial("Dungeon_StoneFloor.mat", new Color(0.18f, 0.18f, 0.19f, 1f), 0f, 0.2f);
            Material wallMaterial = GetOrCreateMaterial("Dungeon_StoneWall.mat", new Color(0.12f, 0.13f, 0.14f, 1f), 0f, 0.15f);
            Material metalMaterial = GetOrCreateMaterial("Dungeon_Metal.mat", new Color(0.18f, 0.16f, 0.14f, 1f), 0.55f, 0.22f);
            Transform environmentRoot = new GameObject("Day24_TestDungeonEnvironment").transform;

            CreateCube(environmentRoot, "EntryFloor", new Vector3(0f, -0.25f, -9f), new Vector3(12f, 0.5f, 22f), floorMaterial);
            CreateCube(environmentRoot, "MainFloor", new Vector3(0f, -0.25f, 12f), new Vector3(18f, 0.5f, 20f), floorMaterial);
            CreateCube(environmentRoot, "LeftCorridorWall", new Vector3(-6f, 2.5f, -9f), new Vector3(0.6f, 5f, 22f), wallMaterial);
            CreateCube(environmentRoot, "RightCorridorWall", new Vector3(6f, 2.5f, -9f), new Vector3(0.6f, 5f, 22f), wallMaterial);
            CreateCube(environmentRoot, "LeftRoomWall", new Vector3(-9f, 2.5f, 12f), new Vector3(0.6f, 5f, 20f), wallMaterial);
            CreateCube(environmentRoot, "RightRoomWall", new Vector3(9f, 2.5f, 12f), new Vector3(0.6f, 5f, 20f), wallMaterial);
            CreateCube(environmentRoot, "BackRoomWall", new Vector3(0f, 2.5f, 22f), new Vector3(18f, 5f, 0.6f), wallMaterial);
            CreateCube(environmentRoot, "InnerWallLeft", new Vector3(-5.6f, 2.5f, 6f), new Vector3(6.8f, 5f, 0.6f), wallMaterial);
            CreateCube(environmentRoot, "InnerWallRight", new Vector3(5.6f, 2.5f, 6f), new Vector3(6.8f, 5f, 0.6f), wallMaterial);
            CreateCube(environmentRoot, "PillarA", new Vector3(-5.5f, 2f, 14f), new Vector3(1.2f, 4f, 1.2f), metalMaterial);
            CreateCube(environmentRoot, "PillarB", new Vector3(5.5f, 2f, 14f), new Vector3(1.2f, 4f, 1.2f), metalMaterial);
            CreateCube(environmentRoot, "PillarC", new Vector3(-5.5f, 2f, 19f), new Vector3(1.2f, 4f, 1.2f), metalMaterial);
            CreateCube(environmentRoot, "PillarD", new Vector3(5.5f, 2f, 19f), new Vector3(1.2f, 4f, 1.2f), metalMaterial);
            CreateDungeonLights(environmentRoot);

            Vector3 stopPosition = new Vector3(0f, sourceData.WagonPosition.y, 0f);
            Quaternion stopRotation = Quaternion.identity;
            Vector3 entryPosition = stopPosition - (stopRotation * Vector3.forward * DungeonEntryDistance);
            CreateTravelAnchor(dungeonScene, TravelDestination.TestDungeon, entryPosition, stopPosition, stopRotation);
            EditorSceneManager.SaveScene(dungeonScene, TestDungeonScenePath);
        }

        private static void CreateDungeonLights(Transform parent)
        {
            GameObject directionalObject = new GameObject("Dungeon Directional Light");
            directionalObject.transform.SetParent(parent, false);
            directionalObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light directional = directionalObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 0.45f;
            directional.color = new Color(0.65f, 0.72f, 0.85f, 1f);

            CreatePointLight(parent, "Entry Lamp", new Vector3(0f, 3.5f, -5f), 18f);
            CreatePointLight(parent, "Hall Lamp", new Vector3(0f, 3.5f, 8f), 18f);
            CreatePointLight(parent, "Deep Lamp", new Vector3(0f, 3.5f, 18f), 18f);
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 position, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = 5f;
            light.color = new Color(1f, 0.58f, 0.28f, 1f);
        }

        private static void CreateTravelAnchor(
            Scene scene,
            TravelDestination destination,
            Vector3 entryPosition,
            Vector3 stopPosition,
            Quaternion rotation)
        {
            GameObject anchorRoot = new GameObject($"Day24_{destination}_TravelAnchor");
            SceneManager.MoveGameObjectToScene(anchorRoot, scene);
            GameObject entryObject = new GameObject("WagonEntryPoint");
            entryObject.transform.SetParent(anchorRoot.transform, false);
            entryObject.transform.SetPositionAndRotation(entryPosition, rotation);
            GameObject stopObject = new GameObject("WagonStopPoint");
            stopObject.transform.SetParent(anchorRoot.transform, false);
            stopObject.transform.SetPositionAndRotation(stopPosition, rotation);
            MapTravelAnchor anchor = anchorRoot.AddComponent<MapTravelAnchor>();
            anchor.Configure(destination, entryObject.transform, stopObject.transform);
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = scale;
            Renderer renderer = cube.GetComponent<Renderer>();

            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return cube;
        }

        private static void PatchBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null || string.IsNullOrWhiteSpace(scene.path))
                {
                    continue;
                }

                if (scene.path == SourceOfficeScenePath ||
                    scene.path == PersistentScenePath ||
                    scene.path == OfficeScenePath ||
                    scene.path == TestDungeonScenePath)
                {
                    continue;
                }

                scenes.Add(scene);
            }

            scenes.Add(new EditorBuildSettingsScene(PersistentScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(OfficeScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(TestDungeonScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void AddUniqueRoot(List<GameObject> roots, GameObject candidate)
        {
            if (candidate != null && !roots.Contains(candidate))
            {
                roots.Add(candidate);
            }
        }

        private static Material GetOrCreateMaterial(string fileName, Color color, float metallic, float smoothness)
        {
            EnsureFolder("Assets/ProjectI/Materials");
            EnsureFolder(GeneratedMaterialFolder);
            string path = $"{GeneratedMaterialFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private struct SourceSceneData
        {
            public bool IsValid;
            public Vector3 PlayerPosition;
            public Quaternion PlayerRotation;
            public Vector3 WagonPosition;
            public Quaternion WagonRotation;
        }
    }
}
