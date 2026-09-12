using System.Collections.Generic; // 목록 기능 참조
using ProjectI.Dungeon; // 실내 생성기 참조
using ProjectI.Wagon; // 마차 적재칸·천막 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 29일차 에디터 구성 네임스페이스
{
    public static class Phase8Day29Setup // 29일차 구성 (마차 천막 · 던전 벽 두께)
    {
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // 공용 마차 프리팹
        private const string TestDungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity"; // 테스트 던전 씬
        private const string GeneratedFolder = "Assets/ProjectI/Art/Generated/Day29"; // 29일차 생성 에셋 폴더
        private const string CoverRootName = "Day29_TravelCover"; // 천막 루트 이름
        private const float WallThickness = 0.25f; // 29일차 던전 벽 두께
        private const float DoorWidth = 2.4f; // 29일차 던전 문 폭
        private const float StairEntryInset = 0.9f; // 계단 앞 평지 길이
        private const float StairTopLanding = 1.2f; // 계단 꼭대기 착지 공간
        private const float PanelThickness = 0.06f; // 천막 천 두께
        private const float PanelMargin = 0.05f; // 적재칸 바깥 여유
        private const float HeadroomMargin = 0.45f; // 천막 지붕과 플레이어 머리 사이 여유

        [MenuItem("Tools/Project I/Day 29/Apply All (Wagon Cover + Dungeon Geometry)")] // 29일차 전체 구성
        public static void ApplyAll() // 마차 천막과 던전 벽 두께를 한 번에 적용
        {
            BuildWagonTravelCover(); // 천막 생성
            ApplyDungeonGeometry(); // 던전 벽 두께 적용
        }

        [MenuItem("Tools/Project I/Day 29/Build Wagon Travel Cover")] // 마차 천막 생성
        public static void BuildWagonTravelCover() // 마차 적재칸을 덮는 천막을 프리팹에 생성
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 씬 보호
            {
                return; // 취소
            }

            EnsureFolder(GeneratedFolder); // 폴더 확보
            Material canvas = CreateLitMaterial("Wagon_TravelCanvas", new Color(0.24f, 0.21f, 0.16f)); // 천막 천 재질
            GameObject root = PrefabUtility.LoadPrefabContents(WagonPrefabPath); // 프리팹 편집 시작

            if (root == null) // 로드 확인
            {
                Debug.LogError($"[Project I] 마차 프리팹을 찾지 못했습니다 / {WagonPrefabPath}"); // 오류
                return; // 종료
            }

            try
            {
                WagonCargoArea cargoArea = root.GetComponentInChildren<WagonCargoArea>(true); // 적재칸 조회
                BoxCollider cargoBox = cargoArea == null ? null : cargoArea.GetComponent<BoxCollider>(); // 적재칸 판정 박스

                if (cargoBox == null) // 누락 확인
                {
                    Debug.LogError("[Project I] 마차 적재칸(CargoArea) BoxCollider를 찾지 못했습니다."); // 오류
                    return; // 종료
                }

                Transform existing = root.transform.Find(CoverRootName); // 기존 천막 조회

                if (existing != null) // 재생성
                {
                    Object.DestroyImmediate(existing.gameObject); // 제거
                }

                Vector3 center = root.transform.InverseTransformPoint(cargoBox.transform.TransformPoint(cargoBox.center)); // 마차 기준 적재칸 중심
                Vector3 scaled = cargoBox.transform.TransformVector(cargoBox.size); // 월드 크기
                Vector3 size = root.transform.InverseTransformVector(scaled); // 마차 기준 크기
                float width = Mathf.Abs(size.x) + (PanelMargin * 2f); // 좌우 폭
                float height = Mathf.Abs(size.y) + HeadroomMargin; // 높이 (플레이어 머리 위 여유 포함)
                float length = Mathf.Abs(size.z) + (PanelMargin * 2f); // 앞뒤 길이
                float bottom = center.y - (Mathf.Abs(size.y) * 0.5f); // 적재칸 바닥 높이
                Transform coverRoot = new GameObject(CoverRootName).transform; // 천막 루트
                coverRoot.SetParent(root.transform, false); // 마차 아래
                coverRoot.localPosition = new Vector3(center.x, bottom + (height * 0.5f), center.z); // 적재칸 바닥 기준 중심
                coverRoot.localRotation = Quaternion.identity; // 축 정렬
                coverRoot.localScale = Vector3.one; // 기준 크기
                List<Transform> pivots = new List<Transform>(); // 말리는 기준점 목록
                pivots.Add(CreateSidePanel(coverRoot, "Panel_Left", new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(PanelThickness, height, length), canvas)); // 왼쪽
                pivots.Add(CreateSidePanel(coverRoot, "Panel_Right", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(PanelThickness, height, length), canvas)); // 오른쪽
                pivots.Add(CreateSidePanel(coverRoot, "Panel_Rear", new Vector3(0f, height * 0.5f, -length * 0.5f), new Vector3(width, height, PanelThickness), canvas)); // 뒤
                pivots.Add(CreateSidePanel(coverRoot, "Panel_Front", new Vector3(0f, height * 0.5f, length * 0.5f), new Vector3(width, height, PanelThickness), canvas)); // 앞
                pivots.Add(CreateTopPanel(coverRoot, "Panel_Top", new Vector3(0f, height * 0.5f, length * 0.5f), width, length, canvas)); // 위 (앞쪽으로 말림)
                WagonTravelCover cover = root.GetComponentInChildren<WagonTravelCover>(true); // 기존 기능 조회

                if (cover == null) // 없으면 추가
                {
                    cover = root.AddComponent<WagonTravelCover>(); // 마차 루트에 추가
                }

                cover.Configure(pivots.ToArray()); // 패널 연결
                cover.SetClosedImmediate(false); // 기본은 걷힌 상태
                PrefabUtility.SaveAsPrefabAsset(root, WagonPrefabPath); // 프리팹 저장
                Debug.Log($"[Project I] 29일차 마차 천막 생성 완료 / 적재칸 {width:F2} x {height:F2} x {length:F2} / 패널 {pivots.Count}개"); // 완료
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root); // 편집 종료
            }

            AssetDatabase.SaveAssets(); // 에셋 저장
        }

        [MenuItem("Tools/Project I/Day 29/Apply Dungeon Geometry (Thinner Walls)")] // 던전 벽 두께 적용
        public static void ApplyDungeonGeometry() // 테스트 던전 생성기의 벽 두께·문 폭을 29일차 값으로 적용
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 씬 보호
            {
                return; // 취소
            }

            Scene scene = EditorSceneManager.OpenScene(TestDungeonScenePath, OpenSceneMode.Single); // 테스트 던전 열기
            ProceduralInteriorGenerator generator = ProceduralInteriorGenerator.FindInScene(scene); // 생성기 조회

            if (generator == null) // 누락 확인
            {
                Debug.LogError("[Project I] 02_TestDungeon에 ProceduralInteriorGenerator가 없습니다."); // 오류
                return; // 종료
            }

            generator.Clear(); // 미리보기 생성물 제거
            generator.ConfigureGeometry(WallThickness, DoorWidth); // 벽 두께·문 폭 적용
            generator.ConfigureStairs(StairEntryInset, StairTopLanding); // 계단 앞 평지·꼭대기 착지 공간 적용
            EnsureFolder(GeneratedFolder); // 폴더 확보
            generator.ConfigureBreakableMaterial(CreateLitMaterial("Dungeon_CrackedWall", new Color(0.46f, 0.42f, 0.36f))); // 금 간 벽 재질
            EditorUtility.SetDirty(generator); // 변경 기록
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 기록
            EditorSceneManager.SaveScene(scene, TestDungeonScenePath); // 저장
            AssetDatabase.SaveAssets(); // 에셋 저장
            Debug.Log($"[Project I] 29일차 던전 형상 적용 완료 / 벽 두께 {generator.WallThickness:F2}m · 문 폭 {generator.DoorWidth:F2}m · 층 높이 {generator.FloorStep:F2}m"); // 완료
        }

        public static void ApplyFromCommandLine() // 배치 모드 실행 진입점
        {
            ApplyAll(); // 전체 적용
        }

        private static Transform CreateSidePanel(Transform coverRoot, string name, Vector3 pivotLocalPosition, Vector3 panelSize, Material material) // 위에서 아래로 내려오는 천막 (위쪽 기준점에서 말림)
        {
            Transform pivot = new GameObject(name).transform; // 기준점
            pivot.SetParent(coverRoot, false); // 천막 루트 아래
            pivot.localPosition = pivotLocalPosition; // 윗변 위치
            pivot.localRotation = Quaternion.identity; // 로컬 +Y = 위쪽
            pivot.localScale = Vector3.one; // 기준 크기
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube); // 천막 판
            panel.name = "Canvas"; // 이름
            panel.transform.SetParent(pivot, false); // 기준점 아래
            panel.transform.localPosition = new Vector3(0f, -panelSize.y * 0.5f, 0f); // 기준점 아래로 내려 달림
            panel.transform.localRotation = Quaternion.identity; // 축 정렬
            panel.transform.localScale = panelSize; // 크기
            panel.GetComponent<Renderer>().sharedMaterial = material; // 재질
            return pivot; // 기준점 반환
        }

        private static Transform CreateTopPanel(Transform coverRoot, string name, Vector3 pivotLocalPosition, float width, float length, Material material) // 지붕 천막 (앞쪽 기준점에서 뒤로 말림)
        {
            Transform pivot = new GameObject(name).transform; // 기준점
            pivot.SetParent(coverRoot, false); // 천막 루트 아래
            pivot.localPosition = pivotLocalPosition; // 앞쪽 윗변
            pivot.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward); // 로컬 +Y = 마차 앞쪽
            pivot.localScale = Vector3.one; // 기준 크기
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube); // 천막 판
            panel.name = "Canvas"; // 이름
            panel.transform.SetParent(pivot, false); // 기준점 아래
            panel.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f); // 기준점에서 뒤쪽으로 펼쳐짐
            panel.transform.localRotation = Quaternion.identity; // 축 정렬
            panel.transform.localScale = new Vector3(width, length, PanelThickness); // 로컬 Y가 길이, 로컬 Z가 두께
            panel.GetComponent<Renderer>().sharedMaterial = material; // 재질
            return pivot; // 기준점 반환
        }

        private static Material CreateLitMaterial(string name, Color color) // URP Lit 단색 재질 생성·재사용
        {
            string path = $"{GeneratedFolder}/{name}.mat"; // 재질 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

            if (material == null) // 새로 만들어야 하는지 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); // URP Lit 셰이더
                material = new Material(shader); // 재질 생성
                AssetDatabase.CreateAsset(material, path); // 재질 저장
            }

            material.color = color; // 기본 색
            material.SetColor("_BaseColor", color); // URP 기본 색
            EditorUtility.SetDirty(material); // 변경 기록
            return material; // 재질 반환
        }

        private static void EnsureFolder(string path) // 폴더 확보
        {
            if (AssetDatabase.IsValidFolder(path)) // 이미 존재
            {
                return; // 종료
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'); // 상위 폴더
            EnsureFolder(parent); // 상위 먼저
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); // 폴더 생성
        }
    }
}
