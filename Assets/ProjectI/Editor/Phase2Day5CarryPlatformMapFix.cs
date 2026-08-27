using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 루트 오브젝트 검색 기능 참조
using ProjectI.World; // 이동 플랫폼 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 갱신 후 기존 5일차 씬 자동 보정
    public static class Phase2Day5CarryPlatformMapFix // 한손·양손 포즈·이동 플랫폼·맵 겹침 일괄 수정
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string FixMarkerName = "===Day5 Carry Platform Map Fix==="; // 이번 수정 적용 완료 마커

        static Phase2Day5CarryPlatformMapFix() // 자동 수정 등록
        {
            EditorApplication.delayCall += TryApplyFix; // 스크립트 컴파일 완료 후 수정 실행 예약
        }

        [MenuItem("Tools/Project I/Day 5/Apply Carry Platform Map Fix")] // 수동 재적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 수정 실행
        {
            ApplyFix(true, true); // 기존 마커와 관계없이 수동 재적용
        }

        private static void TryApplyFix() // 자동 수정 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 수정 중단
            }

            ApplyFix(false, false); // 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyFix(bool showDialog, bool force) // 세 가지 수정 사항을 현재 씬에 반영
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬이 없으면 수정 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == FixMarkerName); // 기존 수정 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 수정된 씬인지 확인
            {
                return; // 반복 자동 수정을 방지
            }

            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 공용 테스트 맵 조회

            if (player == null || mapRoot == null) // 선행 테스트 구조 누락 확인
            {
                Debug.LogError("[Project I] Day 5 Carry/Platform/Map Fix에 필요한 Player 또는 Test Map이 없습니다."); // 선행 구조 누락 오류 출력
                return; // 수정 중단
            }

            Phase2Day5Setup.RefreshPlayerCarrySetup(player); // 화면 하단 한손·양손 CarryPoint 종속 운반 포즈 재구성
            Phase2Day5Setup.RebuildInteractionTestZone(mapRoot); // 07 구역과 겹치지 않는 위치로 08 시험 구역 재생성
            ReconnectMovingPlatforms(); // 기존 이동 플랫폼 탑승 감지기를 새 이동량 전달 구조에 연결
            EnsureMarker(scene); // 수정 완료 마커 확보
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 수정된 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool success = Phase2Day5Validator.Validate(false); // 변경된 5일차 구성 검증

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = success ? "Day 5 Carry / Platform / Map Fix 적용 후 Validator가 통과했습니다." : "Day 5 수정 후 Validator 실패 - Console을 확인하세요."; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }
        }

        private static void ReconnectMovingPlatforms() // 씬의 이동 플랫폼과 탑승 트리거 다시 연결
        {
            MovingPlatform[] platforms = Object.FindObjectsByType<MovingPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬 이동 플랫폼 목록 조회

            foreach (MovingPlatform platform in platforms) // 각 이동 플랫폼 순회
            {
                MovingPlatformPassengerTrigger[] triggers = platform.GetComponentsInChildren<MovingPlatformPassengerTrigger>(true); // 플랫폼 자식 탑승 트리거 조회

                foreach (MovingPlatformPassengerTrigger trigger in triggers) // 플랫폼의 탑승 트리거 순회
                {
                    trigger.Configure(platform); // 새 이동량 전달 방식의 플랫폼 참조 연결
                    EditorUtility.SetDirty(trigger); // 변경 내용을 씬 저장 대상으로 표시
                }
            }
        }

        private static void EnsureMarker(Scene scene) // 수정 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == FixMarkerName); // 기존 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(FixMarkerName); // 새 수정 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 마커 숨김
            }
        }
    }
}
