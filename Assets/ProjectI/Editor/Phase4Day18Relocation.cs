using System.IO; // 대상 씬 파일 존재 확인 참조
using System.Linq; // 씬 루트 이름 검색 참조
using UnityEditor; // Editor 자동 실행·메뉴·Dirty 처리 참조
using UnityEditor.SceneManagement; // 테스트 씬 열기·저장 참조
using UnityEngine; // GameObject·Transform·Vector3 참조
using UnityEngine.SceneManagement; // Scene 자료형 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 컴파일 뒤 기존 Day18 함정 시험장 위치 자동 보정
    public static class Phase4Day18Relocation // Day18 함정 루트의 내부 배치를 유지한 채 빈 공간으로 이동하는 도구
    {
        private const string ScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 이동 대상 탐사 사무소 씬 경로
        private const string RootName = "===Day18 Trap System==="; // 기존 Day18 함정 시험장 루트 이름
        private const string TestFloorName = "Trap_TestFloor"; // 함정 시험장 실제 중심을 판정할 바닥 오브젝트 이름
        private static readonly Vector3 TargetFloorPosition = new Vector3(21f, -0.07f, -24.5f); // CrouchTunnel 남쪽·남쪽 경계 북쪽 사이 빈 공간의 시험 바닥 중심
        private const float PositionTolerance = 0.02f; // 이미 목표 위치인지 판정하는 허용 오차
        private const int MaximumAutoAttempts = 8; // Day18 Setup과 실행 순서가 엇갈릴 때 재시도할 최대 횟수
        private static int autoAttemptCount; // 현재 자동 재시도 횟수

        static Phase4Day18Relocation() // Editor 자동 이동 예약 초기화
        {
            autoAttemptCount = 0; // 도메인 리로드마다 자동 재시도 횟수 초기화
            EditorApplication.delayCall += TryAutoRelocate; // Day18 Setup 이후 위치 보정을 시도하도록 지연 호출 등록
        }

        [MenuItem("Tools/Project I/Day 18/Relocate Trap Test Area")] // 수동 함정 시험장 위치 보정 메뉴 등록
        public static void RelocateFromMenu() // 사용자가 언제든 새 빈 공간 위치를 강제로 다시 적용하는 진입점
        {
            Relocate(true); // 씬 이동 후 결과 대화상자 표시
        }

        private static void TryAutoRelocate() // 컴파일 완료 뒤 Day18 루트가 생성될 때까지 짧게 재시도하는 자동 진입점
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 씬 변경을 수행하면 안 되는 상태에서 중단
            }

            bool relocated = Relocate(false); // 현재 씬의 Day18 함정 시험장 이동 시도

            if (relocated) // 이동 또는 이미 목표 위치 확인 성공 여부 확인
            {
                return; // 추가 재시도 불필요
            }

            autoAttemptCount++; // Day18 Setup이 아직 끝나지 않은 경우 재시도 횟수 증가

            if (autoAttemptCount < MaximumAutoAttempts) // 허용된 자동 재시도 횟수 이내인지 확인
            {
                EditorApplication.delayCall += TryAutoRelocate; // 다음 Editor 지연 호출에서 다시 Day18 루트 검색
            }
        }

        private static bool Relocate(bool showDialog) // Day18 시험장 전체를 새 중심점으로 평행 이동
        {
            if (!File.Exists(ScenePath)) // 대상 ExplorationOffice 씬 존재 여부 확인
            {
                if (showDialog) // 수동 실행에서만 사용자 오류 표시 여부 확인
                {
                    EditorUtility.DisplayDialog("Project I", "ExplorationOffice 씬을 찾을 수 없습니다.", "확인"); // 대상 씬 누락 안내
                }

                return false; // 위치 이동 실패 반환
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // 최신 테스트 씬을 단독으로 열어 안전하게 수정
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == RootName); // Day18 함정 시스템 루트 조회

            if (root == null) // Day18 Setup이 아직 루트를 만들지 않았는지 확인
            {
                if (showDialog) // 수동 실행에서만 사용자 안내 여부 확인
                {
                    EditorUtility.DisplayDialog("Project I", "Day18 Trap System 루트가 없습니다. 먼저 Apply Trap System을 실행하세요.", "확인"); // 선행 Setup 안내
                }

                return false; // 이동 대상 없음 반환
            }

            Transform testFloor = root.transform.Find(TestFloorName); // Day18 시험장의 실제 중심 바닥 Transform 조회

            if (testFloor == null) // 기존 Day18 구조가 예상과 다른지 확인
            {
                if (showDialog) // 수동 실행에서만 구조 오류 안내 여부 확인
                {
                    EditorUtility.DisplayDialog("Project I", "Trap_TestFloor를 찾을 수 없습니다.", "확인"); // 기준 바닥 누락 안내
                }

                return false; // 안전한 이동 기준점 없음 반환
            }

            Vector3 delta = TargetFloorPosition - testFloor.position; // 현재 시험 바닥 중심에서 새 빈 공간 중심까지 필요한 이동량 계산

            if (delta.sqrMagnitude <= PositionTolerance * PositionTolerance) // 이미 새 위치에 충분히 가까운지 확인
            {
                if (showDialog) // 수동 실행에서 현재 위치 상태 표시 여부 확인
                {
                    EditorUtility.DisplayDialog("Project I", "Day18 함정 시험장이 이미 새 테스트 위치에 있습니다.", "확인"); // 중복 이동 불필요 안내
                }

                return true; // 목표 위치 확인 성공 반환
            }

            root.transform.position += delta; // 모든 Day18 함정·압력판·숨은 Trigger의 상대 배치를 유지하며 루트 전체 평행 이동
            EditorUtility.SetDirty(root); // 이동한 Day18 루트를 씬 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 위치가 변경된 씬을 Dirty 상태로 표시
            EditorSceneManager.SaveScene(scene); // 새 함정 시험장 위치를 ExplorationOffice 씬에 저장

            if (showDialog) // 수동 실행 결과 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "Day18 함정 시험장을 테스트맵 남동쪽 빈 공간으로 이동했습니다.", "확인"); // 완료 위치 안내
            }

            Debug.Log($"[Project I] Day18 Trap Test Area relocated to X {TargetFloorPosition.x:0.0}, Z {TargetFloorPosition.z:0.0}"); // Console에 새 시험장 중심 좌표 기록
            return true; // 위치 이동 성공 반환
        }
    }
}
