using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 루트 오브젝트 검색 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 갱신 후 기존 테스트 아이템 자동 보정
    public static class Phase2Day5ItemDropOverlapFix // 작은 아이템과 새 배치 규칙을 현재 씬에 적용
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string FixMarkerName = "===Day5 Small Item Drop Overlap Fix==="; // 이번 수정 적용 완료 마커

        static Phase2Day5ItemDropOverlapFix() // 자동 수정 등록
        {
            EditorApplication.delayCall += TryApplyFix; // 컴파일 완료 후 수정 예약
        }

        [MenuItem("Tools/Project I/Day 5/Apply Small Item Drop Overlap Fix")] // 수동 재적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 수정 실행
        {
            ApplyFix(true, true); // 수동 실행 시 강제로 다시 적용
        }

        private static void TryApplyFix() // 자동 수정 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 수정 중단
            }

            ApplyFix(false, false); // 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyFix(bool showDialog, bool force) // 작은 시험 아이템과 새 배치 규칙 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬이 없으면 수정 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == FixMarkerName); // 기존 수정 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 수정된 씬인지 확인
            {
                return; // 반복 실행 방지
            }

            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 공용 테스트 맵 루트 조회

            if (mapRoot == null) // 테스트 맵 누락 확인
            {
                Debug.LogError("[Project I] Day 5 Small Item Fix에 필요한 Test Map이 없습니다."); // 선행 구조 누락 오류 출력
                return; // 수정 중단
            }

            Phase2Day5Setup.RebuildInteractionTestZone(mapRoot); // 08 테스트 구역을 작은 검·곡괭이 아이템으로 재생성
            EnsureMarker(scene); // 수정 완료 마커 생성
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 수정된 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "작은 시험 아이템과 새 내려놓기/겹침 규칙을 적용했습니다.", "확인"); // 완료 안내 표시
            }
        }

        private static void EnsureMarker(Scene scene) // 수정 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == FixMarkerName); // 기존 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(FixMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 숨김
            }
        }
    }
}
