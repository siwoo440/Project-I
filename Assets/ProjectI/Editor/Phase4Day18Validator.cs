using System.Linq; // 씬 오브젝트·배열 검증 참조
using ProjectI.Combat; // 진영 Damage Pipeline 규칙 검증 참조
using ProjectI.Diagnostics; // Trap Debug 페이지 검증 참조
using ProjectI.Monsters; // 웃는 석상 비피격 규칙 검증 참조
using ProjectI.Traps; // Day18 함정 컴포넌트 검증 참조
using UnityEditor; // 메뉴·Editor 대화상자 기능 참조
using UnityEditor.SceneManagement; // 검증 대상 씬 열기 참조
using UnityEngine; // 오브젝트 검색·Mathf 기능 참조
using UnityEngine.SceneManagement; // Scene 자료형 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 자동 구성 도구 네임스페이스
{
    public static class Phase4Day18Validator // Day18 함정·Damage Pipeline·Trigger 연결 정적 검증 도구
    {
        private const string ScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 씬 경로
        private const string RootName = "===Day18 Trap System==="; // Day18 시험장 루트 이름
        private const string ReadyMarkerName = "===Day18 Trap System Ready==="; // Day18 완료 마커 이름

        [MenuItem("Tools/Project I/Day 18/Validate")] // 수동 Day18 검증 메뉴 등록
        public static void ValidateFromMenu() // 사용자 수동 검증 진입점
        {
            bool passed = Validate(true); // 검증 실행과 결과 대화상자 표시

            if (passed) // 검증 성공 여부 확인
            {
                Debug.Log("[Project I] Day18 Trap Validator PASS"); // Console 성공 로그 출력
            }
        }

        public static bool Validate(bool showDialog) // Day18 시험장 필수 구조·피해 규칙 검사
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // 최신 테스트 씬 단독 열기
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == RootName); // Day18 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ReadyMarkerName); // 완료 마커 조회
            FloorSpikeTrap[] floorSpikes = Object.FindObjectsByType<FloorSpikeTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 바닥 가시 전체 조회
            CeilingSpikeSlamTrap[] ceilingSpikes = Object.FindObjectsByType<CeilingSpikeSlamTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 천장 주기 가시 전체 조회
            SwingingAxeTrap[] axes = Object.FindObjectsByType<SwingingAxeTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None); // Swing 도끼 전체 조회
            PressurePlate[] plates = Object.FindObjectsByType<PressurePlate>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 압력판 전체 조회
            TrapTriggerVolume[] triggers = Object.FindObjectsByType<TrapTriggerVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 숨은 Trigger 전체 조회
            TrapDebugPage[] debugPages = Object.FindObjectsByType<TrapDebugPage>(FindObjectsInactive.Include, FindObjectsSortMode.None); // F1 Trap 페이지 조회
            SmilingStatueBehavior[] statues = Object.FindObjectsByType<SmilingStatueBehavior>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 웃는 석상 비피격 규칙 확인 대상 조회
            bool passed = true; // 전체 검증 초기 성공 상태
            passed &= Require(root != null, "Day18 Trap System 루트 누락"); // Day18 시험장 존재 검증
            passed &= Require(marker != null, "Day18 완료 마커 누락"); // 자동 구성 완료 마커 검증
            passed &= Require(floorSpikes.Length >= 1, "바닥 가시 함정 누락"); // 바닥 가시 존재 검증
            passed &= Require(ceilingSpikes.Length >= 1, "천장 내려찍기 가시 함정 누락"); // 천장 자동 주기 가시 존재 검증
            passed &= Require(axes.Length >= 1, "Swing 도끼 함정 누락"); // 도끼 존재 검증
            passed &= Require(plates.Length >= 1, "압력판 누락"); // 압력판 존재 검증
            passed &= Require(triggers.Length >= 1 && triggers.Any(trigger => trigger.TargetTrap is SwingingAxeTrap), "도끼 통로 Trigger 연결 누락"); // 도끼 자동 Trigger 연결 검증
            passed &= Require(debugPages.Length >= 1, "F1 Trap Debug 페이지 누락"); // 진단 페이지 존재 검증
            passed &= Require(CombatFactionRules.CanDamage(CombatFaction.Environment, CombatFaction.Player), "Environment → Player 피해 규칙 차단됨"); // 플레이어 함정 피해 허용 검증
            passed &= Require(CombatFactionRules.CanDamage(CombatFaction.Environment, CombatFaction.Enemy), "Environment → Enemy 피해 규칙 차단됨"); // 몬스터 함정 피해 허용 검증

            if (floorSpikes.Length > 0) // 바닥 가시 상세 검증 가능 여부 확인
            {
                passed &= Require(floorSpikes[0].Damage >= 34.9f, "바닥 가시 피해량 35 미만"); // 바닥 가시 초기 밸런스 검증
                passed &= Require(floorSpikes[0].DamageSource != null, "바닥 가시 DamageSource 누락"); // 공통 피해 소스 연결 검증
            }

            if (ceilingSpikes.Length > 0) // 천장 가시 상세 검증 가능 여부 확인
            {
                passed &= Require(ceilingSpikes[0].Damage >= 69.9f, "천장 내려찍기 피해량 70 미만"); // 천장 가시 강한 피해 검증
                passed &= Require(ceilingSpikes[0].Damage > (floorSpikes.Length == 0 ? 0f : floorSpikes[0].Damage), "천장 가시가 바닥 가시보다 강하지 않음"); // 두 가시 역할 차이 검증
            }

            if (axes.Length > 0) // 도끼 상세 검증 가능 여부 확인
            {
                passed &= Require(axes[0].Damage >= 54.9f, "도끼 함정 피해량 55 미만"); // 도끼 기본 피해 검증
                passed &= Require(axes[0].DamageSource != null, "도끼 DamageSource 누락"); // 공통 피해 소스 연결 검증
            }

            if (plates.Length > 0) // 압력판 링크 상세 검증 가능 여부 확인
            {
                passed &= Require(plates[0].LinkedTraps != null && plates[0].LinkedTraps.Any(trap => trap is FloorSpikeTrap), "압력판 → 바닥 가시 링크 누락"); // 압력판 연동 가시 검증
            }

            for (int index = 0; index < statues.Length; index++) // 웃는 석상 전체 비피격 규칙 검증
            {
                MonoBehaviour[] behaviours = statues[index].GetComponents<MonoBehaviour>(); // 석상 루트 기능 컴포넌트 조회
                passed &= Require(!behaviours.Any(component => component is IDamageable), "웃는 석상에 IDamageable이 다시 추가됨"); // 함정에도 죽지 않는 불사 규칙 유지 검증
            }

            if (showDialog) // 수동 검증 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", passed ? "Day18 Validator PASS" : "Day18 Validator FAIL - Console 확인", "확인"); // 결과 대화상자 출력
            }

            return passed; // 최종 검증 결과 반환
        }

        private static bool Require(bool condition, string message) // 단일 검증 조건 실패 로그 도우미
        {
            if (condition) // 검증 성공 여부 확인
            {
                return true; // 성공 반환
            }

            Debug.LogError($"[Project I][Day18] {message}"); // 실패 원인을 Console에 출력
            return false; // 실패 반환
        }
    }
}
