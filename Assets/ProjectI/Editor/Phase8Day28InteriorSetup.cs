using System; // 동작 위임 참조
using System.Linq; // 목록 조회 참조
using ProjectI.Dungeon; // 실내 생성기 참조
using ProjectI.Generation; // 생성 규칙·세로형 방 종류 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 28일차 에디터 구성 네임스페이스
{
    public static class Phase8Day28InteriorSetup // 28일차 다층 실내 던전 구성 (계단·사다리 재질, 세로형 방 테스트 모드)
    {
        private const string TestDungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity"; // 테스트 던전 씬
        private const string GeneratedFolder = "Assets/ProjectI/Art/Generated/Day28"; // 28일차 생성 에셋 폴더

        [MenuItem("Tools/Project I/Day 28/Apply Stair And Ladder Materials To Test Dungeon")] // 계단·사다리 재질 연결
        public static void ApplyStairAndLadderMaterials() // 테스트 던전 생성기에 계단·사다리 재질을 연결하고 저장
        {
            EnsureFolder(GeneratedFolder); // 폴더 확보
            Material stair = CreateLitMaterial("Dungeon_StairStone", new Color(0.34f, 0.33f, 0.31f)); // 계단 석재
            Material ladder = CreateLitMaterial("Dungeon_LadderWood", new Color(0.38f, 0.25f, 0.12f)); // 사다리 목재
            WithGenerator(generator => generator.ConfigureStairMaterials(stair, ladder), "계단·사다리 재질 연결 완료"); // 적용
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Force Stairwell Only (계단통만)")] // 계단통 고정
        public static void ForceStairwell() // 모든 세로형 방을 계단통으로 고정
        {
            ForceVerticalKind(VerticalKind.Stairwell); // 적용
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Force Ladder Room Only (사다리 방만)")] // 사다리 방 고정
        public static void ForceLadder() // 모든 세로형 방을 사다리 방으로 고정
        {
            ForceVerticalKind(VerticalKind.Ladder); // 적용
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Force Shaft Only (수직 통로만)")] // 수직 통로 고정
        public static void ForceShaft() // 모든 세로형 방을 수직 통로로 고정
        {
            ForceVerticalKind(VerticalKind.Shaft); // 적용
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Use Random Vertical Rooms (기본값 복귀)")] // 기본값 복귀
        public static void UseRandomVerticalRooms() // 세로형 방 종류 고정 해제
        {
            ForceVerticalKind(VerticalKind.None); // 해제
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Quick Stairwell Layout (1F + 2F, 계단통 1개)")] // 계단통 하나만 있는 빠른 테스트 구성
        public static void QuickStairwellLayout() // 위 1개 층만 만들어 계단통 하나만 생성 (찾기 쉬움)
        {
            WithGenerator(generator =>
            {
                generator.ConfigureFloorRange(1, 0); // 1층 + 2층
                generator.ConfigureTestOverrides(generator.SeedOverride, VerticalKind.Stairwell); // 계단통 고정
            }, "빠른 계단통 테스트 구성 완료 (1F + 2F, 세로형 방 1개 = 계단통)"); // 안내
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Quick Ladder Layout (1F + 2F, 사다리 1개)")] // 사다리 하나만 있는 빠른 테스트 구성
        public static void QuickLadderLayout() // 위 1개 층만 만들어 사다리 방 하나만 생성
        {
            WithGenerator(generator =>
            {
                generator.ConfigureFloorRange(1, 0); // 1층 + 2층
                generator.ConfigureTestOverrides(generator.SeedOverride, VerticalKind.Ladder); // 사다리 고정
            }, "빠른 사다리 테스트 구성 완료 (1F + 2F, 세로형 방 1개 = 사다리 방)"); // 안내
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Find Seed With Stairwell And Ladder (혼합 시드 고정)")] // 계단통과 사다리가 함께 나오는 시드 찾기
        public static void FindMixedSeed() // 계단통·사다리가 모두 있는 시드를 찾아 고정 시드로 저장
        {
            WithGenerator(generator =>
            {
                int subDoors = CountExteriorSubDoors(); // 외부 서브문 수
                DungeonGenerationConfig config = generator.BuildConfig(subDoors); // 생성 규칙
                config.ForcedVerticalKind = VerticalKind.None; // 혼합을 찾으므로 고정 해제

                for (int attempt = 1; attempt <= 400; attempt++) // 시드 탐색
                {
                    int seed = attempt * 7919; // 후보 시드
                    DungeonLayout layout = DungeonLayoutGenerator.Generate(config, seed); // 생성

                    if (layout == null) // 실패
                    {
                        continue; // 다음
                    }

                    bool hasStair = layout.VerticalRooms.Any(room => room.Vertical == VerticalKind.Stairwell); // 계단통 여부
                    bool hasLadder = layout.VerticalRooms.Any(room => room.Vertical != VerticalKind.Stairwell); // 사다리 계열 여부

                    if (!hasStair || !hasLadder) // 혼합 아님
                    {
                        continue; // 다음
                    }

                    generator.ConfigureTestOverrides(seed, VerticalKind.None); // 고정 시드 저장
                    Debug.Log($"[Project I] 혼합 시드 {seed} 고정 — {string.Join(", ", layout.VerticalRooms.Select(r => $"{r.Vertical} {GridPoint.FloorName(r.LowerFloor)}↔{GridPoint.FloorName(r.UpperFloor)} {r.Cell}"))}"); // 결과
                    return; // 종료
                }

                Debug.LogWarning("[Project I] 혼합 시드를 찾지 못했습니다. 층 수를 늘리거나 다시 실행하세요."); // 실패
            }, "혼합 시드 탐색 완료"); // 안내
        }

        [MenuItem("Tools/Project I/Day 28/Test Vertical Rooms/Clear Test Overrides (고정 시드·종류 해제)")] // 테스트 설정 해제
        public static void ClearTestOverrides() // 고정 시드·종류·층 수를 기본값으로 되돌림
        {
            WithGenerator(generator =>
            {
                generator.ConfigureTestOverrides(0, VerticalKind.None); // 고정 해제
                generator.ConfigureFloorRange(2, 2); // 기본 층 수 (3F ~ B2)
            }, "테스트 설정 해제 완료 (고정 시드 없음 · 종류 무작위 · 3F ~ B2)"); // 안내
        }

        public static void ApplyFromCommandLine() // 배치 모드 실행 진입점
        {
            ApplyStairAndLadderMaterials(); // 재질 연결
        }

        private static void ForceVerticalKind(VerticalKind kind) // 세로형 방 종류 고정 적용
        {
            WithGenerator(generator => generator.ConfigureTestOverrides(generator.SeedOverride, kind), kind == VerticalKind.None ? "세로형 방 종류 고정 해제" : $"세로형 방을 {kind}로 고정"); // 적용
        }

        private static void WithGenerator(Action<ProceduralInteriorGenerator> action, string doneMessage) // 테스트 던전을 열어 생성기를 수정하고 저장
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 씬 보호
            {
                return; // 취소
            }

            Scene scene = EditorSceneManager.OpenScene(TestDungeonScenePath, OpenSceneMode.Single); // 테스트 던전 열기
            ProceduralInteriorGenerator generator = ProceduralInteriorGenerator.FindInScene(scene); // 생성기 조회

            if (generator == null) // 누락 확인
            {
                Debug.LogError("[Project I] 02_TestDungeon에 ProceduralInteriorGenerator가 없습니다. Day 27 > Rebuild Test Dungeon을 먼저 실행하세요."); // 오류
                return; // 종료
            }

            generator.Clear(); // 에디터 미리보기 생성물 제거 (씬 용량 방지)
            action(generator); // 설정 변경
            EditorUtility.SetDirty(generator); // 변경 기록
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 기록
            EditorSceneManager.SaveScene(scene, TestDungeonScenePath); // 저장
            AssetDatabase.SaveAssets(); // 에셋 저장
            Debug.Log($"[Project I] 28일차 {doneMessage} / 현재 설정: 고정 시드 {(generator.SeedOverride == 0 ? "없음" : generator.SeedOverride.ToString())} · 세로형 방 {(generator.ForcedVerticalKind == VerticalKind.None ? "무작위" : generator.ForcedVerticalKind.ToString())} · 층 위 {generator.FloorsAboveSetting} 아래 {generator.FloorsBelowSetting}"); // 완료
        }

        private static int CountExteriorSubDoors() // 열린 씬의 외부 서브문 수
        {
            return UnityEngine.Object.FindObjectsByType<DungeonTeleportDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(door => door.Side == DungeonDoorSide.Exterior && door.Kind == DungeonDoorKind.Sub); // 집계
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
