using ProjectI.Dungeon; // 생성기 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 편집 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase9Day30SceneSetup // 테스트 던전 씬에 모듈 던전 생성기를 올려 둡니다 (기존 격자 생성기는 그대로 둠)
    {
        private const string TestDungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity"; // 테스트 던전 씬
        private const string LibraryPath = "Assets/ProjectI/Prefabs/Dungeon/ModuleLibrary/DungeonModuleLibrary.asset"; // 모듈 목록
        private const string GeneratorName = "Day30_ModuleDungeon"; // 생성기 오브젝트 이름

        [MenuItem("Project I/Day 30/Add Module Generator To Test Dungeon")] // 메뉴
        public static void AddGenerator() // 씬에 생성기 배치
        {
            Scene scene = EditorSceneManager.OpenScene(TestDungeonScenePath, OpenSceneMode.Single); // 씬 열기 (씬을 연 뒤에 에셋을 불러야 함)
            DungeonModuleLibrary library = AssetDatabase.LoadAssetAtPath<DungeonModuleLibrary>(LibraryPath); // 목록

            if (library == null) // 없음
            {
                Debug.LogError("[Project I] 모듈 목록이 없습니다. Build Module Set 을 먼저 실행하세요."); // 오류
                return; // 종료
            }

            GameObject existing = null; // 기존 오브젝트

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (root.name == GeneratorName) // 일치
                {
                    existing = root; // 기록
                    break; // 종료
                }
            }

            GameObject holder = existing != null ? existing : new GameObject(GeneratorName); // 생성기 오브젝트
            ModuleDungeonGenerator generator = holder.GetComponent<ModuleDungeonGenerator>(); // 생성기

            if (generator == null) // 없으면 추가
            {
                generator = holder.AddComponent<ModuleDungeonGenerator>(); // 추가
            }

            generator.Clear(); // 남은 생성물 제거
            generator.ConfigureLibrary(library, holder.transform); // 모듈 목록·지형 기준
            SerializedObject serialized = new SerializedObject(generator); // 직렬화 접근
            serialized.FindProperty("exteriorDoorCount").intValue = CountExteriorSubDoors(scene); // 외부 씬 서브문 수를 읽어 반영
            serialized.ApplyModifiedPropertiesWithoutUndo(); // 적용
            EditorUtility.SetDirty(generator); // 변경 기록
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 기록
            EditorSceneManager.SaveScene(scene, TestDungeonScenePath); // 저장
            AssetDatabase.SaveAssets(); // 에셋 저장
            Debug.Log($"[Project I] 30일차 모듈 생성기 배치 완료 / {GeneratorName} / 외부 서브문 {serialized.FindProperty("exteriorDoorCount").intValue}개를 읽어 반영"); // 완료
        }

        public static void AddFromCommandLine() // 배치모드 실행용
        {
            AddGenerator(); // 배치
        }

        private static int CountExteriorSubDoors(Scene scene) // 외부 씬에 놓인 서브문 개수 (규칙: 실내 서브문은 외부 개수와 1:1)
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
