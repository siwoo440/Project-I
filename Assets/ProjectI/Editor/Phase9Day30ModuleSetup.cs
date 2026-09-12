using System.Collections.Generic; // 목록 사용
using System.IO; // 폴더 확인
using ProjectI.Combat; // 금 간 벽 체력 참조
using ProjectI.Dungeon; // 모듈 부품 참조
using ProjectI.Generation; // 출입구 규격 참조
using UnityEditor; // 에디터 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase9Day30ModuleSetup // 30일차: 방·복도 모듈 프리팹 세트와 문 프리팹, 모듈 목록 에셋을 만듭니다
    {
        private const string PrefabRoot = "Assets/ProjectI/Prefabs/Dungeon"; // 프리팹 최상위 폴더
        private const string DoorsFolder = "Doors"; // 문 프리팹 폴더
        private const string LibraryFolder = "Library"; // 모듈 목록 폴더
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day30"; // 재질 폴더
        private const string LibraryPath = PrefabRoot + "/" + LibraryFolder + "/DungeonModuleLibrary.asset"; // 목록 에셋 경로

        [MenuItem("Project I/Day 30/Build Module Set")] // 메뉴
        public static void BuildModuleSet() // 모듈 세트 전체 제작
        {
            ModuleMaterials materials = CreateMaterials(); // 재질
            List<DungeonModuleShape> shapes = DungeonModuleCatalog.All(); // 형태 정의
            List<string> problems = new List<string>(); // 규격 문제

            foreach (DungeonModuleShape shape in shapes) // 형태 검사
            {
                string problem = shape.Validate(); // 검사

                if (!string.IsNullOrEmpty(problem)) // 문제 있음
                {
                    problems.Add(problem); // 등록
                }
            }

            if (problems.Count > 0) // 규격 문제
            {
                Debug.LogError($"[Project I] 30일차 모듈 규격 오류 {problems.Count}건\n{string.Join("\n", problems)}"); // 오류
                return; // 중단
            }

            EnsureFolder(PrefabRoot); // 폴더 확보
            EnsureFolder(PrefabRoot + "/" + DungeonModuleCatalog.RoomsFolder); // 방 폴더
            EnsureFolder(PrefabRoot + "/" + DungeonModuleCatalog.CorridorsFolder); // 복도 폴더
            EnsureFolder(PrefabRoot + "/" + DungeonModuleCatalog.VerticalFolder); // 세로형 방 폴더
            EnsureFolder(PrefabRoot + "/" + DoorsFolder); // 문 폴더
            EnsureFolder(PrefabRoot + "/" + LibraryFolder); // 목록 폴더
            List<DungeonModule> saved = new List<DungeonModule>(); // 저장된 모듈
            Dictionary<string, int> folderCount = new Dictionary<string, int>(); // 폴더별 개수

            foreach (DungeonModuleShape shape in shapes) // 형태 순회
            {
                GameObject instance = DungeonModuleBaker.Build(shape, materials); // 모듈 생성
                string path = $"{PrefabRoot}/{shape.Folder}/{shape.Id}.prefab"; // 저장 경로
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path); // 프리팹 저장
                Object.DestroyImmediate(instance); // 임시 오브젝트 제거
                DungeonModule module = prefab == null ? null : prefab.GetComponent<DungeonModule>(); // 모듈 부품

                if (module == null) // 실패
                {
                    Debug.LogError($"[Project I] 모듈 프리팹 저장 실패 / {shape.Id}"); // 오류
                    continue; // 다음
                }

                saved.Add(module); // 등록
                folderCount[shape.Folder] = folderCount.TryGetValue(shape.Folder, out int count) ? count + 1 : 1; // 집계
            }

            foreach (GameObject vertical in new[] { DungeonVerticalModuleBaker.BuildStairwell(materials), DungeonVerticalModuleBaker.BuildLadderRoom(materials) }) // 층을 잇는 세로형 모듈
            {
                string verticalPath = $"{PrefabRoot}/{DungeonModuleCatalog.VerticalFolder}/{vertical.name}.prefab"; // 저장 경로
                GameObject verticalPrefab = PrefabUtility.SaveAsPrefabAsset(vertical, verticalPath); // 저장
                Object.DestroyImmediate(vertical); // 임시 제거
                DungeonModule verticalModule = verticalPrefab == null ? null : verticalPrefab.GetComponent<DungeonModule>(); // 모듈 부품

                if (verticalModule == null) // 실패
                {
                    Debug.LogError($"[Project I] 세로형 모듈 프리팹 저장 실패 / {verticalPath}"); // 오류
                    continue; // 다음
                }

                saved.Add(verticalModule); // 등록
                folderCount[DungeonModuleCatalog.VerticalFolder] = folderCount.TryGetValue(DungeonModuleCatalog.VerticalFolder, out int verticalCount) ? verticalCount + 1 : 1; // 집계
            }

            GameObject door = BuildDoorPrefab("Door_Standard", false, materials); // 여닫이문
            GameObject lockedDoor = BuildDoorPrefab("Door_Locked", true, materials); // 잠긴 문
            GameObject breakable = BuildBreakablePrefab(materials); // 금 간 벽
            GameObject exterior = BuildExteriorDoorPrefab(materials); // 외부 씬 순간이동 문
            GameObject plug = BuildWallPlugPrefab(materials); // 연결되지 않은 출입구를 막는 벽
            DungeonModuleLibrary library = AssetDatabase.LoadAssetAtPath<DungeonModuleLibrary>(LibraryPath); // 기존 목록

            if (library == null) // 없으면 생성
            {
                library = ScriptableObject.CreateInstance<DungeonModuleLibrary>(); // 생성
                AssetDatabase.CreateAsset(library, LibraryPath); // 저장
            }

            library.Configure(saved.ToArray(), door, lockedDoor, breakable, exterior, plug); // 구성
            EditorUtility.SetDirty(library); // 변경 기록
            AssetDatabase.SaveAssets(); // 저장
            AssetDatabase.Refresh(); // 갱신
            List<string> summary = new List<string>(); // 요약

            foreach (KeyValuePair<string, int> pair in folderCount) // 폴더별
            {
                summary.Add($"{pair.Key} {pair.Value}개"); // 등록
            }

            Debug.Log($"[Project I] 30일차 모듈 세트 생성 완료 / {string.Join(" · ", summary)} / 문 프리팹 5종 / 출입구 규격 {ModuleDoorway.Width:F2} x {ModuleDoorway.Height:F2}m"); // 완료
        }

        [MenuItem("Project I/Day 30/Validate Module Shapes")] // 메뉴
        public static void ValidateShapes() // 형태 규격만 검사
        {
            List<string> problems = new List<string>(); // 문제

            foreach (DungeonModuleShape shape in DungeonModuleCatalog.All()) // 형태 순회
            {
                string problem = shape.Validate(); // 검사

                if (!string.IsNullOrEmpty(problem)) // 문제
                {
                    problems.Add(problem); // 등록
                }
            }

            if (problems.Count == 0) // 문제 없음
            {
                Debug.Log($"[Project I] 모듈 형태 규격 검사 통과 / {DungeonModuleCatalog.All().Count}종"); // 완료
                return; // 종료
            }

            Debug.LogError($"[Project I] 모듈 형태 규격 오류 {problems.Count}건\n{string.Join("\n", problems)}"); // 오류
        }

        public static void BuildFromCommandLine() // 배치모드 실행용
        {
            BuildModuleSet(); // 제작
        }

        private static GameObject BuildDoorPrefab(string id, bool locked, ModuleMaterials materials) // 여닫이문 프리팹 (출입구 규격에 맞춤)
        {
            GameObject root = new GameObject(id); // 루트
            Transform hinge = DungeonModuleBaker.CreateChild(root.transform, "Hinge"); // 경첩
            hinge.localPosition = new Vector3(-ModuleDoorway.LeafWidth * 0.5f, 0f, 0f); // 문 한쪽 끝
            Material leafMaterial = locked ? materials.Accent : materials.Trim; // 문짝 재질
            DungeonModuleBaker.CreateBox(hinge, "Leaf", new Vector3(ModuleDoorway.LeafWidth * 0.5f, ModuleDoorway.LeafHeight * 0.5f, 0f), new Vector3(ModuleDoorway.LeafWidth, ModuleDoorway.LeafHeight, ModuleDoorway.LeafThickness), leafMaterial); // 문짝
            DungeonModuleBaker.CreateBox(hinge, "Handle", new Vector3(ModuleDoorway.LeafWidth - 0.22f, 1.05f, 0f), new Vector3(0.09f, 0.09f, 0.34f), materials.Accent); // 손잡이 (문짝과 형제로 두어 문짝 크기에 눌리지 않게 함)
            DungeonDoor component = root.AddComponent<DungeonDoor>(); // 문 기능
            component.Configure(hinge, locked, "key.basic"); // 구성
            string path = $"{PrefabRoot}/{DoorsFolder}/{id}.prefab"; // 저장 경로
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); // 저장
            Object.DestroyImmediate(root); // 임시 제거
            return prefab; // 반환
        }

        private static GameObject BuildBreakablePrefab(ModuleMaterials materials) // 금 간 벽 프리팹
        {
            GameObject root = new GameObject("Wall_Breakable"); // 루트
            GameObject panel = DungeonModuleBaker.CreateBox(root.transform, "Panel", new Vector3(0f, ModuleDoorway.Height * 0.5f, 0f), new Vector3(ModuleDoorway.Width, ModuleDoorway.Height, 0.3f), materials.Wall); // 막는 벽
            panel.AddComponent<CombatHealth>(); // 공통 체력
            BreakableWall breakable = panel.AddComponent<BreakableWall>(); // 파괴 기능
            breakable.Configure(120f); // 내구도
            string path = $"{PrefabRoot}/{DoorsFolder}/Wall_Breakable.prefab"; // 저장 경로
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); // 저장
            Object.DestroyImmediate(root); // 임시 제거
            return prefab; // 반환
        }

        private static GameObject BuildWallPlugPrefab(ModuleMaterials materials) // 연결되지 않은 출입구를 막는 벽 프리팹 (구멍이 밖으로 뚫리는 것을 막음)
        {
            GameObject root = new GameObject("Wall_Plug"); // 루트
            float t = DungeonModuleBaker.WallThickness; // 벽 두께
            float height = DungeonModuleBaker.RoomHeight; // 벽 높이 (문틀 위까지 한 번에 막음)
            DungeonModuleBaker.CreateBox(root.transform, "Panel", new Vector3(0f, height * 0.5f, -t * 0.5f), new Vector3(ModuleDoorway.Width + 0.04f, height, t), materials.Wall); // 구멍을 덮는 벽
            string path = $"{PrefabRoot}/{DoorsFolder}/Wall_Plug.prefab"; // 저장 경로
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); // 저장
            Object.DestroyImmediate(root); // 임시 제거
            return prefab; // 반환
        }

        private static GameObject BuildExteriorDoorPrefab(ModuleMaterials materials) // 외부 씬과 잇는 순간이동 문 프리팹
        {
            GameObject root = new GameObject("Door_Exterior"); // 루트
            GameObject panel = DungeonModuleBaker.CreateBox(root.transform, "DoorPanel", new Vector3(0f, ModuleDoorway.Height * 0.5f, -DungeonModuleBaker.WallThickness * 0.5f), new Vector3(ModuleDoorway.Width, ModuleDoorway.Height, DungeonModuleBaker.WallThickness), materials.Accent); // 문짝 (벽 두께 안에 놓아 틈이 생기지 않게 함)
            Transform arrival = DungeonModuleBaker.CreateChild(root.transform, "ArrivalPoint"); // 도착 지점
            arrival.localPosition = new Vector3(0f, 0.05f, -1.4f); // 문 안쪽 1.4m
            GameObject markerLight = new GameObject("DoorLight"); // 표시등
            markerLight.transform.SetParent(root.transform, false); // 문 아래
            markerLight.transform.localPosition = new Vector3(0f, ModuleDoorway.Height + 0.2f, -0.4f); // 문 위
            Light light = markerLight.AddComponent<Light>(); // 점광원
            light.type = LightType.Point; // 점광원
            light.range = 4f; // 범위
            light.intensity = 1.6f; // 밝기
            light.color = new Color(0.55f, 1f, 0.6f); // 녹색
            light.shadows = LightShadows.None; // 그림자 없음
            panel.AddComponent<DungeonTeleportDoor>(); // 순간이동 문 (생성기에서 짝 번호 구성)
            string path = $"{PrefabRoot}/{DoorsFolder}/Door_Exterior.prefab"; // 저장 경로
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); // 저장
            Object.DestroyImmediate(root); // 임시 제거
            return prefab; // 반환
        }

        private static ModuleMaterials CreateMaterials() // 모듈용 재질
        {
            EnsureFolder(MaterialFolder); // 폴더
            return new ModuleMaterials
            {
                Floor = CreateLitMaterial("Module_Floor", new Color(0.33f, 0.31f, 0.29f)), // 바닥
                Wall = CreateLitMaterial("Module_Wall", new Color(0.42f, 0.40f, 0.37f)), // 벽
                Trim = CreateLitMaterial("Module_Trim", new Color(0.29f, 0.22f, 0.16f)), // 문틀·문짝
                Accent = CreateLitMaterial("Module_Accent", new Color(0.62f, 0.50f, 0.24f)), // 강조
            }; // 반환
        }

        private static Material CreateLitMaterial(string name, Color color) // URP Lit 재질 생성·갱신
        {
            string path = $"{MaterialFolder}/{name}.mat"; // 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존

            if (material == null) // 없으면 생성
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit
                material = new Material(shader == null ? Shader.Find("Standard") : shader); // 재질
                AssetDatabase.CreateAsset(material, path); // 저장
            }

            material.color = color; // 색
            EditorUtility.SetDirty(material); // 변경 기록
            return material; // 반환
        }

        private static void EnsureFolder(string path) // 폴더 확보
        {
            if (AssetDatabase.IsValidFolder(path)) // 이미 있음
            {
                return; // 종료
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); // 상위 폴더
            EnsureFolder(parent); // 상위부터
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); // 생성
        }
    }
}
