using ProjectI.Dungeon; // 생성기 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 편집 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase9Day30ApplyToDungeon // 테스트 던전이 모듈(소켓) 방식으로 생성되도록 전환합니다
    {
        private const string TestDungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity"; // 테스트 던전 씬
        private const string LibraryPath = "Assets/ProjectI/Prefabs/Dungeon/Library/DungeonModuleLibrary.asset"; // 모듈 목록
        private const string GeneratorName = "Day30_ModuleDungeon"; // 모듈 생성기 오브젝트 이름

        [MenuItem("Project I/Day 30/Use Module Dungeon In Test Dungeon")] // 메뉴
        public static void UseModuleDungeon() // 모듈 방식으로 전환 (이전 격자 생성기는 꺼 둠)
        {
            Switch(true); // 전환
        }

        [MenuItem("Project I/Day 30/Revert To Grid Dungeon")] // 메뉴
        public static void RevertToGrid() // 이전 격자 방식으로 되돌리기
        {
            Switch(false); // 되돌리기
        }

        public static void UseModuleFromCommandLine() // 배치모드 실행용
        {
            Switch(true); // 전환
        }

        private static void Switch(bool useModule) // 두 생성기 중 하나만 켜고 씬 저장
        {
            Scene scene = EditorSceneManager.OpenScene(TestDungeonScenePath, OpenSceneMode.Single); // 씬 열기 (씬을 연 뒤에 에셋을 불러야 함 — Single 모드 전환에서 참조 없는 에셋이 언로드됨)
            DungeonModuleLibrary library = AssetDatabase.LoadAssetAtPath<DungeonModuleLibrary>(LibraryPath); // 모듈 목록

            if (useModule && library == null) // 목록 없음
            {
                Debug.LogError("[Project I] 모듈 목록이 없습니다. Build Module Set 을 먼저 실행하세요."); // 오류
                return; // 종료
            }

            ProceduralInteriorGenerator grid = ProceduralInteriorGenerator.FindInScene(scene); // 이전 격자 생성기
            ModuleDungeonGenerator module = ModuleDungeonGenerator.FindInScene(scene); // 모듈 생성기

            if (module == null) // 없으면 만들기
            {
                GameObject holder = new GameObject(GeneratorName); // 오브젝트
                module = holder.AddComponent<ModuleDungeonGenerator>(); // 생성기
            }

            module.Clear(); // 남은 생성물 제거
            module.ConfigureLibrary(library, module.transform); // 모듈 목록·지형 기준

            if (grid != null) // 이전 생성기의 열쇠·회수품 표를 그대로 넘겨받음
            {
                module.ConfigureContent(grid.KeyDefinition, grid.LootTable); // 내용물
            }
            module.gameObject.SetActive(useModule); // 모듈 방식 켜기·끄기

            if (useModule && module.Library == null) // 연결 확인
            {
                Debug.LogError("[Project I] 모듈 목록 연결에 실패했습니다."); // 오류
                return; // 종료
            }

            if (grid != null) // 이전 생성기
            {
                grid.Clear(); // 남은 생성물 제거
                grid.gameObject.SetActive(!useModule); // 반대로 켜기·끄기
                EditorUtility.SetDirty(grid); // 변경 기록
            }

            EditorUtility.SetDirty(module); // 변경 기록
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 기록
            EditorSceneManager.SaveScene(scene, TestDungeonScenePath); // 저장
            AssetDatabase.SaveAssets(); // 에셋 저장
            int subDoors = CountExteriorSubDoors(scene); // 외부 서브문 수
            Debug.Log($"[Project I] 30일차 던전 생성 방식 전환 완료 / {(useModule ? "모듈(소켓) 방식" : "이전 격자 방식")} / 외부 서브문 {subDoors}개 (실내 서브문도 같은 수로 생성)"); // 완료
        }

        private static int CountExteriorSubDoors(Scene scene) // 외부 씬에 놓인 서브문 개수
        {
            int count = 0; // 집계

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                foreach (DungeonTeleportDoor door in root.GetComponentsInChildren<DungeonTeleportDoor>(true)) // 문 순회
                {
                    if (door.Side == DungeonDoorSide.Exterior && door.Kind == DungeonDoorKind.Sub) // 외부 서브문
                    {
                        count++; // 집계
                    }
                }
            }

            return count; // 반환
        }
    }
}
