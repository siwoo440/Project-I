using ProjectI.Items;
using ProjectI.Loop;
using ProjectI.Wagon;
using UnityEditor;
using UnityEngine;

namespace ProjectI.EditorTools
{
    [InitializeOnLoad]
    public static class Phase5Day24Step1WagonItemSetup
    {
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab";
        private const string GeneratedMaterialFolder = "Assets/ProjectI/Materials/Day24";
        private static readonly Vector3 CargoLocalPosition = new Vector3(0f, 2.55f, -1.25f);
        private static readonly Vector3 CargoSize = new Vector3(3.65f, 2.60f, 9.50f);
        private static readonly Vector3 BellLocalPosition = new Vector3(1.62f, 2.82f, 2.55f);

        static Phase5Day24Step1WagonItemSetup()
        {
            EditorApplication.delayCall += ApplyStep1Automatically;
        }

        [MenuItem("Tools/Project I/Day 24/Apply Step 1 - Wagon And Item")]
        public static void ApplyStep1()
        {
            PatchWorldItemPrefabs();
            PatchWagonPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Project I] 24일차 1단계 적용 / 드롭 물리 + CargoArea + 마차 종");
        }

        private static void ApplyStep1Automatically()
        {
            EditorApplication.delayCall -= ApplyStep1Automatically;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            ApplyStep1();
        }

        private static void PatchWorldItemPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ProjectI/Prefabs" });

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;

                try
                {
                    WorldItem[] items = prefabRoot.GetComponentsInChildren<WorldItem>(true);

                    foreach (WorldItem item in items)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        WorldItemDropProfile profile = item.GetComponent<WorldItemDropProfile>();

                        if (profile == null)
                        {
                            profile = item.gameObject.AddComponent<WorldItemDropProfile>();
                            profile.Configure(Vector3.zero, InferStability(item.DisplayName));
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static ItemStabilityMode InferStability(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return ItemStabilityMode.Free;
            }

            string normalized = displayName.ToLowerInvariant();

            if (normalized.Contains("검") ||
                normalized.Contains("도끼") ||
                normalized.Contains("곡괭이") ||
                normalized.Contains("석궁") ||
                normalized.Contains("리볼버") ||
                normalized.Contains("손전등") ||
                normalized.Contains("횃불") ||
                normalized.Contains("랜턴") ||
                normalized.Contains("열쇠"))
            {
                return ItemStabilityMode.Free;
            }

            return ItemStabilityMode.Upright;
        }

        private static void PatchWagonPrefab()
        {
            GameObject wagonRoot = PrefabUtility.LoadPrefabContents(WagonPrefabPath);

            try
            {
                WagonCargoArea cargoArea = wagonRoot.GetComponentInChildren<WagonCargoArea>(true);

                if (cargoArea != null)
                {
                    cargoArea.transform.localPosition = CargoLocalPosition;
                    BoxCollider cargoTrigger = cargoArea.GetComponent<BoxCollider>();

                    if (cargoTrigger == null)
                    {
                        cargoTrigger = cargoArea.gameObject.AddComponent<BoxCollider>();
                    }

                    cargoTrigger.center = Vector3.zero;
                    cargoTrigger.size = CargoSize;
                    cargoTrigger.isTrigger = true;
                    cargoArea.Configure(cargoTrigger);
                }
                else
                {
                    Debug.LogWarning("[Project I] Wagon.prefab에서 WagonCargoArea를 찾지 못했습니다.");
                }

                ConfigureTravelBell(wagonRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(wagonRoot, WagonPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(wagonRoot);
            }
        }

        private static void ConfigureTravelBell(Transform wagonRoot)
        {
            Material brassMaterial = GetOrCreateMaterial("Wagon_Bell_Brass.mat", new Color(0.66f, 0.43f, 0.12f, 1f), 0.65f, 0.15f);
            Material ropeMaterial = GetOrCreateMaterial("Wagon_Bell_Rope.mat", new Color(0.18f, 0.09f, 0.035f, 1f), 0.05f, 0.55f);
            Transform bellRoot = FindOrCreateEmpty(wagonRoot, "Day24_WagonTravelBell");
            bellRoot.localPosition = BellLocalPosition;
            bellRoot.localRotation = Quaternion.identity;
            bellRoot.localScale = Vector3.one;

            BoxCollider interactionCollider = bellRoot.GetComponent<BoxCollider>();

            if (interactionCollider == null)
            {
                interactionCollider = bellRoot.gameObject.AddComponent<BoxCollider>();
            }

            interactionCollider.center = new Vector3(0f, -0.38f, 0f);
            interactionCollider.size = new Vector3(0.72f, 1.55f, 0.72f);
            interactionCollider.isTrigger = false;

            Transform mount = FindOrCreatePrimitive(bellRoot, "BellMount", PrimitiveType.Cube, brassMaterial);
            SetLocalTransform(mount, new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(0.18f, 0.46f, 0.18f));

            Transform arm = FindOrCreatePrimitive(bellRoot, "BellArm", PrimitiveType.Cube, brassMaterial);
            SetLocalTransform(arm, new Vector3(-0.18f, -0.02f, 0f), Quaternion.identity, new Vector3(0.52f, 0.09f, 0.09f));

            Transform pivot = FindOrCreateEmpty(bellRoot, "BellPivot");
            SetLocalTransform(pivot, new Vector3(-0.40f, -0.24f, 0f), Quaternion.identity, Vector3.one);

            Transform body = FindOrCreatePrimitive(pivot, "BellBody", PrimitiveType.Cylinder, brassMaterial);
            SetLocalTransform(body, new Vector3(0f, -0.18f, 0f), Quaternion.identity, new Vector3(0.25f, 0.20f, 0.25f));

            Transform lip = FindOrCreatePrimitive(pivot, "BellLip", PrimitiveType.Sphere, brassMaterial);
            SetLocalTransform(lip, new Vector3(0f, -0.39f, 0f), Quaternion.identity, new Vector3(0.31f, 0.09f, 0.31f));

            Transform rope = FindOrCreatePrimitive(pivot, "Rope", PrimitiveType.Cylinder, ropeMaterial);
            SetLocalTransform(rope, new Vector3(0f, -0.75f, 0f), Quaternion.identity, new Vector3(0.035f, 0.36f, 0.035f));

            WagonTravelBellInteractable interactable = bellRoot.GetComponent<WagonTravelBellInteractable>();

            if (interactable == null)
            {
                interactable = bellRoot.gameObject.AddComponent<WagonTravelBellInteractable>();
            }

            interactable.Configure(pivot, rope);
        }

        private static Transform FindOrCreateEmpty(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);

            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform FindOrCreatePrimitive(Transform parent, string childName, PrimitiveType primitiveType, Material material)
        {
            Transform existing = parent.Find(childName);
            GameObject target;

            if (existing == null)
            {
                target = GameObject.CreatePrimitive(primitiveType);
                target.name = childName;
                target.transform.SetParent(parent, false);
            }
            else
            {
                target = existing.gameObject;
            }

            Collider primitiveCollider = target.GetComponent<Collider>();

            if (primitiveCollider != null)
            {
                Object.DestroyImmediate(primitiveCollider);
            }

            Renderer renderer = target.GetComponent<Renderer>();

            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return target.transform;
        }

        private static void SetLocalTransform(Transform target, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            target.localPosition = localPosition;
            target.localRotation = localRotation;
            target.localScale = localScale;
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

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
