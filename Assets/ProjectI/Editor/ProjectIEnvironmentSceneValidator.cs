using System.Collections.Generic; // 결과 목록 기능 참조
using ProjectI.Diagnostics; // F1 디버그 창 관리자 참조
using ProjectI.Items; // 플레이어 인벤토리·HUD 참조
using ProjectI.Loop; // 환경 씬 규칙 검사 참조
using ProjectI.Persistence; // 저장 서비스 참조
using ProjectI.Wagon; // 마차 적재칸 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 도구 네임스페이스
{
    public static class ProjectIEnvironmentSceneValidator // 새 던전·환경 씬 추가 시 전역 시스템 혼입을 막는 검증 도구
    {
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity"; // Persistent 씬 경로
        private static readonly HashSet<string> NonEnvironmentScenes = new HashSet<string> // 환경 씬이 아닌 빌드 씬
        {
            "Assets/ProjectI/Scenes/Boot.unity", // 부트
            "Assets/ProjectI/Scenes/MainMenu.unity", // 메인 메뉴
            PersistentScenePath // Persistent 마차 씬
        };

        [MenuItem("Tools/Project I/Validate Environment Scenes")] // 검증 메뉴
        public static bool ValidateFromMenu() // 메뉴 실행
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 씬 보호
            {
                return false; // 취소
            }

            bool success = Validate(); // 검증 실행
            EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // 시작 씬으로 복귀

            if (!Application.isBatchMode) // 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "환경 씬 규칙 검증 통과" : "환경 씬 규칙 위반 — Console 확인", "확인"); // 결과 표시
            }

            return success; // 결과 반환
        }

        public static bool Validate() // Persistent 필수 구성 + 모든 환경 씬 규칙 검사
        {
            Scene persistent = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // Persistent 씬 열기
            int errors = 0; // 오류 개수
            errors += Require<PlayerInventory>(persistent, "플레이어"); // 플레이어 필수
            errors += Require<WagonCargoArea>(persistent, "마차"); // 마차 필수
            errors += Require<PersistentMapLoader>(persistent, "맵 로더"); // 맵 로더 필수
            errors += Require<DailySnapshotService>(persistent, "저장 서비스"); // 저장 서비스 필수
            errors += Require<DebugPageManager>(persistent, "F1 디버그 창"); // F1 창 필수

            if (!HasRootNamed(persistent, QuickSlotHud.CanvasName)) // 빠른 슬롯 HUD 필수
            {
                Debug.LogError($"[Project I] 00_WagonPersistent에 {QuickSlotHud.CanvasName}이 없습니다. Tools > Project I > Day 26 > Move Player HUD 메뉴를 실행하세요."); // 오류
                errors++; // 집계
            }

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes) // 빌드 씬 순회
            {
                if (!buildScene.enabled || NonEnvironmentScenes.Contains(buildScene.path)) // 환경 씬만 검사
                {
                    continue; // 다음 씬
                }

                Scene environment = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive); // 환경 씬 추가 열기
                List<string> violations = EnvironmentSceneGuard.Inspect(environment, persistent, false); // 규칙 검사 (수정 없음)

                foreach (string violation in violations) // 위반 출력
                {
                    Debug.LogError($"[Project I] 환경 씬 규칙 위반 / {buildScene.path} / {violation}"); // 오류
                }

                errors += violations.Count; // 집계
                Debug.Log($"[Project I] 환경 씬 검사 / {buildScene.path} / 위반 {violations.Count}건"); // 씬별 결과
                EditorSceneManager.CloseScene(environment, true); // 환경 씬 닫기
            }

            Debug.Log($"[Project I] 환경 씬 규칙 검증 완료 / 오류 {errors}건"); // 전체 결과
            return errors == 0; // 결과 반환
        }

        private static int Require<T>(Scene scene, string label) where T : Component // Persistent 씬 필수 컴포넌트 확인
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (root.GetComponentInChildren<T>(true) != null) // 존재 확인
                {
                    return 0; // 정상
                }
            }

            Debug.LogError($"[Project I] 00_WagonPersistent에 {label}({typeof(T).Name})이 없습니다."); // 오류
            return 1; // 오류 1건
        }

        private static bool HasRootNamed(Scene scene, string name) // 이름으로 루트 확인
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (root.name == name) // 이름 비교
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }
    }
}
