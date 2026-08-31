using System.Collections.Generic; // 검증 실패 항목 목록 기능 참조
using System.IO; // 대상 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트 이름 검색 기능 참조
using ProjectI.Diagnostics; // F1 전력 진단 페이지 검증 참조
using ProjectI.Lighting; // 고정·휴대 조명 개수 검증 참조
using ProjectI.Power; // 발전기·배전반·상태 관리자 검증 참조
using UnityEditor; // 유니티 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day13Validator // Day13 조명·전력 복구·최적화 정적 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string SystemRootName = "===Day13 Power Recovery System==="; // Day13 상태 관리자 루트 이름
        private const string ReadyMarkerName = "===Day13 Power Recovery Ready==="; // Day13 완료 마커 이름

        [MenuItem("Tools/Project I/Day 13/Validate")] // 수동 Day13 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            Validate(true); // 결과 대화상자를 포함한 전체 검증 실행
        }

        public static bool Validate(bool showDialog) // Day13 구성 정적 검증 실행
        {
            List<string> failures = new List<string>(); // 검증 실패 항목 목록 생성

            if (!File.Exists(ExplorationOfficeScenePath)) // 탐사 씬 파일 존재 여부 확인
            {
                failures.Add("ExplorationOffice.unity 누락"); // 대상 씬 누락 실패 기록
                return FinishValidation(failures, showDialog); // 즉시 검증 결과 반환
            }

            Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회

            if (scene.path != ExplorationOfficeScenePath) // 현재 씬이 탐사 사무소인지 확인
            {
                scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 검증 대상 탐사 씬 열기
            }

            GameObject systemRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == SystemRootName); // Day13 상태 관리자 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // Day13 완료 마커 조회
            PowerLightingStateManager manager = systemRoot == null ? null : systemRoot.GetComponent<PowerLightingStateManager>(); // Day13 상태 관리자 컴포넌트 조회
            PowerSystemDebugPage debugPage = systemRoot == null ? null : systemRoot.GetComponent<PowerSystemDebugPage>(); // F1 전력 통합 페이지 조회
            GeneratorController generator = Object.FindFirstObjectByType<GeneratorController>(); // 발전기 조회
            MainDistributionBoardController board = Object.FindFirstObjectByType<MainDistributionBoardController>(); // 중앙 배전반 조회
            FixedLightController[] fixedLights = Object.FindObjectsByType<FixedLightController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 고정 조명 전체 조회
            PortableLightItem[] portableLights = Object.FindObjectsByType<PortableLightItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 휴대 조명 전체 조회
            DistributionBoardButton[] switches = Object.FindObjectsByType<DistributionBoardButton>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 중앙·로컬 토글 스위치 전체 조회

            Require(marker != null, "Day13 완료 마커 누락", failures); // 완료 마커 존재 검증
            Require(systemRoot != null, "Day13 상태 관리자 루트 누락", failures); // Day13 시스템 루트 존재 검증
            Require(manager != null, "PowerLightingStateManager 누락", failures); // 상태 관리자 컴포넌트 검증
            Require(debugPage != null, "PowerSystemDebugPage 누락", failures); // F1 전력 페이지 컴포넌트 검증
            Require(generator != null, "GeneratorController 누락", failures); // 발전기 존재 검증
            Require(board != null, "MainDistributionBoardController 누락", failures); // 중앙 배전반 존재 검증
            Require(manager != null && manager.IsConfigured, "Day13 상태 관리자 핵심 참조 미구성", failures); // 상태 관리자 발전기·배전반 연결 검증
            Require(board != null && board.RoomZoneCount >= 3, "배전반 연결 방 3개 미만", failures); // 방 단위 전력 구역 개수 검증
            Require(board != null && board.ControlledDoorCount >= 3, "배전반 연결 철제문 3개 미만", failures); // 원격 제어 철제문 개수 검증
            Require(fixedLights.Length >= 4, "Day9 고정 횃불·화로 4개 미만", failures); // 고정 조명 복구 대상 개수 검증
            Require(portableLights.Length >= 2, "Day8 휴대 횃불·랜턴 2개 미만", failures); // 휴대 조명 복구 대상 개수 검증
            Require(switches.Length >= 7, "Day12 토글 스위치 7개 미만", failures); // 이벤트 기반 토글 대상 기본 개수 검증
            return FinishValidation(failures, showDialog); // 최종 검증 결과 반환
        }

        private static void Require(bool condition, string failureMessage, List<string> failures) // 단일 검증 조건 처리
        {
            if (!condition) // 검증 조건 실패 여부 확인
            {
                failures.Add(failureMessage); // 실패 항목 목록에 원인 기록
            }
        }

        private static bool FinishValidation(List<string> failures, bool showDialog) // 검증 결과 로그와 대화상자 출력
        {
            bool passed = failures.Count == 0; // 전체 검증 통과 여부 계산

            if (passed) // 전체 조건 정상 여부 확인
            {
                Debug.Log("[Project I][Day13] 조명·전력 상태 복구·이벤트 기반 최적화·F1 진단 구성이 정적으로 정상입니다."); // 성공 로그 출력
            }
            else // 하나 이상의 검증 실패 처리
            {
                Debug.LogError($"[Project I][Day13] 검증 실패\n- {string.Join("\n- ", failures)}"); // 실패 항목 전체 Console 출력
            }

            if (showDialog) // 수동 검증 결과 대화상자 표시 여부 확인
            {
                string message = passed ? "Day13 정적 검증을 통과했습니다." : $"Day13 검증 실패\n\n- {string.Join("\n- ", failures)}"; // 대화상자 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 출력
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
