using System.IO; // 대상 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트 이름 검색 기능 참조
using ProjectI.Diagnostics; // F1 전력 진단 페이지 기능 참조
using ProjectI.Lighting; // 고정·휴대 조명 상태 관리자 연결 참조
using ProjectI.Power; // 발전기·배전반·Day13 상태 관리자 기능 참조
using UnityEditor; // 유니티 에디터 메뉴와 저장 기능 참조
using UnityEditor.SceneManagement; // 씬 열기와 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day13 상태 복구·최적화 시스템 자동 구성
    public static class Phase3Day13Setup // 조명·전력 상태 복구와 F1 진단 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string SystemRootName = "===Day13 Power Recovery System==="; // Day13 런타임 상태 관리자 루트 이름
        private const string ReadyMarkerName = "===Day13 Power Recovery Ready==="; // Day13 자동 적용 완료 마커 이름

        static Phase3Day13Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 자동 구성 예약
        }

        [MenuItem("Tools/Project I/Day 13/Apply Power Recovery + Optimization")] // 수동 Day13 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day13 전체 구성 실행
        {
            ApplyDay13(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        [MenuItem("Tools/Project I/Day 13/Capture Runtime Snapshot")] // Play Mode 상태 캡처 메뉴 등록
        public static void CaptureRuntimeSnapshot() // 에디터 메뉴에서 현재 상태 캡처 실행
        {
            PowerLightingStateManager manager = Object.FindFirstObjectByType<PowerLightingStateManager>(); // 현재 실행 씬 Day13 상태 관리자 조회

            if (!EditorApplication.isPlaying || manager == null) // Play Mode와 관리자 존재 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "Play Mode에서 Day13 상태 관리자가 필요합니다.", "확인"); // 실행 조건 안내
                return; // 상태 캡처 중단
            }

            manager.CaptureSnapshot(); // 현재 조명·전력 상태 메모리 캡처
        }

        [MenuItem("Tools/Project I/Day 13/Restore Runtime Snapshot")] // Play Mode 상태 복구 메뉴 등록
        public static void RestoreRuntimeSnapshot() // 에디터 메뉴에서 마지막 상태 복구 실행
        {
            PowerLightingStateManager manager = Object.FindFirstObjectByType<PowerLightingStateManager>(); // 현재 실행 씬 Day13 상태 관리자 조회

            if (!EditorApplication.isPlaying || manager == null) // Play Mode와 관리자 존재 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "Play Mode에서 Day13 상태 관리자가 필요합니다.", "확인"); // 실행 조건 안내
                return; // 상태 복구 중단
            }

            manager.RestoreSnapshot(); // 마지막 조명·전력 스냅샷 복구
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // Batch 또는 Play 전환 중에는 구성 중단
            }

            ApplyDay13(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay13(bool showDialog, bool force) // Day13 상태 복구·최적화 시스템 전체 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 대상 씬 누락 시 자동 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day13 완료 마커 조회

            if (!force && existingMarker != null) // 이미 Day13 자동 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            GeneratorController generator = Object.FindFirstObjectByType<GeneratorController>(); // 기존 Day11 발전기 조회
            MainDistributionBoardController board = Object.FindFirstObjectByType<MainDistributionBoardController>(); // 기존 Day12 중앙 배전반 조회

            if (generator == null || board == null) // Day11·12 핵심 전력 시스템 존재 여부 확인
            {
                Debug.LogError("[Project I] Day13 구성 전에 Day11 발전기와 Day12 중앙 배전반이 필요합니다."); // 선행 구조 누락 오류 출력
                return; // Day13 구성 중단
            }

            RemoveExistingRoot(scene, SystemRootName); // 기존 Day13 상태 관리자 루트 제거
            RemoveExistingRoot(scene, ReadyMarkerName); // 이전 완료 마커 제거
            FixedLightController[] fixedLights = Object.FindObjectsByType<FixedLightController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // Day9 벽 횃불·화로 전체 조회
            PortableLightItem[] portableLights = Object.FindObjectsByType<PortableLightItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // Day8 휴대 횃불·랜턴 전체 조회
            GameObject systemRoot = new GameObject(SystemRootName); // Day13 런타임 관리 전용 루트 생성
            PowerLightingStateManager manager = systemRoot.AddComponent<PowerLightingStateManager>(); // 상태 캡처·복구 관리자 추가
            manager.Configure(generator, board, fixedLights, portableLights); // 발전기·배전반·조명 전체 상태 관리자 연결
            PowerSystemDebugPage debugPage = systemRoot.AddComponent<PowerSystemDebugPage>(); // F1 전력 통합 진단 페이지 추가
            debugPage.Configure(manager); // 진단 페이지와 상태 관리자 연결
            GameObject marker = new GameObject(ReadyMarkerName); // Day13 자동 적용 완료 마커 생성
            EditorUtility.SetDirty(manager); // 상태 관리자 씬 저장 대상으로 표시
            EditorUtility.SetDirty(debugPage); // F1 진단 페이지 씬 저장 대상으로 표시
            EditorUtility.SetDirty(marker); // 완료 마커 씬 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // Day13 상태 관리자 구성을 탐사 씬에 저장
            AssetDatabase.SaveAssets(); // 직렬화된 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            bool validationPassed = Phase3Day13Validator.Validate(false); // Day13 구조 정적 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day13 조명·전력 상태 복구 및 이벤트 기반 최적화 구성이 완료되었습니다." : "Day13 검증 실패 - Console을 확인하세요."; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 완료 또는 실패 안내 표시
            }
        }

        private static void RemoveExistingRoot(Scene scene, string rootName) // 지정 이름의 기존 씬 루트 제거
        {
            GameObject existingRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 대상 이름의 기존 루트 검색

            if (existingRoot != null) // 기존 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot); // 강제 재적용을 위한 기존 루트 제거
            }
        }
    }
}
