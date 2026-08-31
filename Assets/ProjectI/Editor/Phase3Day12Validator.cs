using System.Linq; // 장치 목록 조건 검증 기능 참조
using ProjectI.Power; // 12일차 방 전력·철제문 기능 참조
using UnityEditor; // 에디터 검증 메뉴 참조
using UnityEditor.SceneManagement; // 검증 대상 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day12Validator // Day 12 방 전력·배전반·철제문 구성 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 탐사 씬 경로
        private const string ReadyMarkerName = "===Day12 Room Power Ready v3==="; // 글자 방향 수정 버전 자동 적용 완료 마커 이름
        private const string Day12RootName = "12_PowerControlTest"; // 12일차 시험 루트 이름

        [MenuItem("Tools/Project I/Day 12/Validate")] // 수동 Day 12 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴에서 Day 12 검증 실행
        {
            Validate(true); // 전체 검증과 결과 대화상자 표시
        }

        public static bool Validate(bool showDialog) // 방 전력·배전반·철제문 연결 정적 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            MainDistributionBoardController board = Object.FindFirstObjectByType<MainDistributionBoardController>(); // 중앙 배전반 조회
            GeneratorController generator = Object.FindFirstObjectByType<GeneratorController>(); // 기존 11일차 발전기 조회
            RoomPowerZone[] zones = Object.FindObjectsByType<RoomPowerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 활성·비활성 방 전력 구역 조회
            PoweredIronDoor[] doors = Object.FindObjectsByType<PoweredIronDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 활성·비활성 전동 철제문 조회
            DistributionBoardButton[] buttons = Object.FindObjectsByType<DistributionBoardButton>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 배전반과 로컬 버튼 전체 조회
            ElectricLightController[] lights = Object.FindObjectsByType<ElectricLightController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 전기등 전체 조회
            bool markerPass = scene.GetRootGameObjects().Any(root => root.name == ReadyMarkerName); // 완료 마커 존재 여부 검사
            bool rootPass = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(target => target.name == Day12RootName); // 12일차 시험 루트 존재 여부 검사
            bool boardPass = board != null && board.Generator == generator && board.RoomZoneCount >= 3 && board.ControlledDoorCount >= 3; // 배전반과 발전기·방·문 연결 검사
            bool zonePass = zones.Length >= 3 && zones.Count(zone => zone != null && zone.ConsumerCount >= 2) >= 3; // 각 방에 전등과 문 소비자 2개 이상 연결 검사
            bool doorPass = doors.Length >= 3 && doors.Count(door => door != null && door.PowerConsumer != null) >= 3; // 전동 철제문과 공통 전력 소비자 연결 검사
            bool roomLightPass = lights.Count(light => light != null && light.GetComponent<PowerConsumer>() != null) >= 3; // 방 전기등의 공통 전력 소비자 연결 검사
            bool buttonPass = buttons.Count(button => button != null && button.Action == DistributionBoardButtonAction.MainPowerToggle) >= 1 && buttons.Count(button => button != null && button.Action == DistributionBoardButtonAction.RoomPowerToggle) >= 3 && buttons.Count(button => button != null && button.Action == DistributionBoardButtonAction.DoorToggle) >= 6; // 메인 1 + 방 3 + 원격/로컬 문 토글 6개 구성 검사
            bool localButtonPass = buttons.Length >= 10; // 메인 1 + 방 3 + 원격 문 3 + 로컬 문 3의 단일 스위치 전체 수 검사
            bool initialPowerPass = zones.Where(zone => zone != null).Take(3).All(zone => zone.RequestedPower); // 방 3개가 시작 시 ON 요청 상태인지 검사
            bool passed = markerPass && rootPass && boardPass && zonePass && doorPass && roomLightPass && buttonPass && localButtonPass && initialPowerPass; // 전체 정적 검증 결과 계산

            if (!markerPass || !rootPass) // 자동 구성 루트 또는 마커 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 시험 루트 또는 완료 마커가 없습니다."); // 자동 구성 누락 오류 출력
            }

            if (!boardPass) // 중앙 배전반 연결 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 배전반이 발전기·방 3개·철제문 3개와 올바르게 연결되지 않았습니다."); // 배전반 연결 오류 출력
            }

            if (!zonePass) // 방 전력 소비자 연결 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 각 방에 전등과 철제문 PowerConsumer가 연결되지 않았습니다."); // 방 전력 연결 오류 출력
            }

            if (!doorPass) // 철제문 전력 연결 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 전동 철제문 또는 문 PowerConsumer 구성이 부족합니다."); // 철제문 구성 오류 출력
            }

            if (!roomLightPass) // 방 전등 전력 연결 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 방 전등 3개가 공통 PowerConsumer 구조를 사용하지 않습니다."); // 전등 구성 오류 출력
            }

            if (!buttonPass || !localButtonPass) // 배전반 또는 로컬 버튼 수 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 방/문 단일 토글 스위치 또는 문 옆 로컬 스위치 구성이 부족합니다."); // 스위치 구성 오류 출력
            }

            if (!initialPowerPass) // 방 시작 요청 상태 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day12] 방 3개의 시작 전원 요청 상태가 ON이 아닙니다."); // 시작 방 전원 상태 오류 출력
            }

            if (passed) // 전체 검증 성공 여부 확인
            {
                Debug.Log("[Project I][Day12] 방 전력·벽면형 소형 배전반·단일 토글 스위치·전등·전동 철제문 구성이 정적으로 정상입니다."); // 검증 성공 로그 출력
            }

            if (showDialog) // 수동 검증 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", passed ? "Day 12 검증 통과" : "Day 12 검증 실패 - Console 확인", "확인"); // 검증 결과 대화상자 표시
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
