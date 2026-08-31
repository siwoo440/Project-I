using System.Collections.Generic; // 검증 실패 항목 목록 기능 참조
using System.IO; // 대상 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트와 진영 대상 검색 기능 참조
using ProjectI.Combat; // Day14 공통 전투 기능 검증 참조
using ProjectI.Diagnostics; // F1 Combat 페이지 검증 참조
using ProjectI.Items; // 기존 WorldItem 검증 참조
using ProjectI.Player; // 기존 플레이어 체력·스태미나·이동 검증 참조
using UnityEditor; // 유니티 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 오브젝트 검색 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 검증 도구 네임스페이스
{
    public static class Phase4Day14Validator // Day14 공통 전투 기반 정적·순수 규칙 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string CombatRootName = "===Day14 Combat Foundation==="; // Day14 전투 시험장 루트 이름
        private const string ReadyMarkerName = "===Day14 Combat Foundation Ready==="; // Day14 완료 마커 이름
        private const string AttackAssetPath = "Assets/ProjectI/Resources/Combat/Day14_TestSword.asset"; // Day14 테스트 검 공격 데이터 경로

        [MenuItem("Tools/Project I/Day 14/Validate")] // 수동 Day14 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 Day14 전체 검증 실행
        {
            Validate(true); // 결과 대화상자를 포함한 전체 검증 실행
        }

        public static bool Validate(bool showDialog) // Day14 공통 전투 기반 검증 실행
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

            GameObject combatRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == CombatRootName); // Day14 전투 시험장 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // Day14 완료 마커 조회
            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 기존 플레이어 입력 래퍼 조회
            GameObject player = inputReader == null ? null : inputReader.gameObject; // 플레이어 루트 조회
            PlayerHealth playerHealth = player == null ? null : player.GetComponent<PlayerHealth>(); // 기존 플레이어 체력 조회
            PlayerStamina playerStamina = player == null ? null : player.GetComponent<PlayerStamina>(); // 기존 플레이어 스태미나 조회
            PlayerMovement playerMovement = player == null ? null : player.GetComponent<PlayerMovement>(); // 기존 플레이어 이동 조회
            PlayerDamageReceiver damageReceiver = player == null ? null : player.GetComponent<PlayerDamageReceiver>(); // 공통 Damage Pipeline 플레이어 어댑터 조회
            CombatController combatController = player == null ? null : player.GetComponent<CombatController>(); // 플레이어 공통 전투 제어기 조회
            CombatDebugPage debugPage = Object.FindFirstObjectByType<CombatDebugPage>(); // F1 Combat 진단 페이지 조회
            CombatHealth[] combatTargets = Object.FindObjectsByType<CombatHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 공통 피해 시험 대상 전체 조회
            MeleeWeaponItem[] meleeWeapons = Object.FindObjectsByType<MeleeWeaponItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 근접 무기 전체 조회
            GameObject blockerWall = combatRoot == null ? null : FindChildRecursive(combatRoot.transform, "Combat_BlockerWall"); // 벽 충돌 시험 오브젝트 조회
            AttackDefinition attackDefinition = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AttackAssetPath); // Day14 테스트 검 공격 데이터 조회

            Require(marker != null, "Day14 완료 마커 누락", failures); // 완료 마커 존재 검증
            Require(combatRoot != null, "Day14 Combat Foundation 루트 누락", failures); // 전투 시험장 루트 존재 검증
            Require(player != null, "Player 누락", failures); // 기존 플레이어 존재 검증
            Require(playerHealth != null, "PlayerHealth 누락", failures); // 기존 체력 시스템 존재 검증
            Require(playerStamina != null, "PlayerStamina 누락", failures); // 기존 스태미나 시스템 존재 검증
            Require(playerMovement != null, "PlayerMovement 누락", failures); // 기존 이동 시스템 존재 검증
            Require(damageReceiver != null && damageReceiver.Health != null, "PlayerDamageReceiver 연결 누락", failures); // 플레이어 공통 피해 어댑터 검증
            Require(combatController != null, "CombatController 누락", failures); // 플레이어 공통 전투 제어기 검증
            Require(debugPage != null, "F1 CombatDebugPage 누락", failures); // F1 전투 진단 페이지 검증
            Require(combatTargets.Length >= 3, "CombatHealth 테스트 더미 3개 미만", failures); // 적·아군·벽 뒤 적 더미 개수 검증
            Require(combatTargets.Any(target => target != null && target.Faction == CombatFaction.Enemy), "Enemy 진영 테스트 대상 누락", failures); // 적 진영 대상 검증
            Require(combatTargets.Any(target => target != null && target.Faction == CombatFaction.Ally), "Ally 진영 테스트 대상 누락", failures); // 아군 진영 대상 검증
            Require(meleeWeapons.Length >= 1, "MeleeWeaponItem 테스트 검 누락", failures); // 테스트 근접 무기 존재 검증
            Require(meleeWeapons.Any(weapon => weapon != null && weapon.GetComponent<WorldItem>() != null), "테스트 검 WorldItem 연동 누락", failures); // 기존 아이템 획득 체계 연동 검증
            Require(meleeWeapons.Any(weapon => weapon != null && weapon.WeaponTrace != null && weapon.WeaponTrace.TraceStart != null && weapon.WeaponTrace.TraceEnd != null), "테스트 검 궤적 기준점 누락", failures); // 근접 무기 궤적 구성 검증
            Require(attackDefinition != null, "Day14_TestSword AttackDefinition 누락", failures); // 공격 데이터 에셋 존재 검증
            Require(attackDefinition != null && Mathf.Approximately(attackDefinition.BaseDamage, 25f), "테스트 검 피해량 25 미구성", failures); // 기본 피해량 구성 검증
            Require(attackDefinition != null && Mathf.Approximately(attackDefinition.StaminaCost, 12f), "테스트 검 스태미나 비용 12 미구성", failures); // 공격 스태미나 비용 구성 검증
            Require(blockerWall != null && blockerWall.GetComponent<Collider>() != null, "근접 공격 벽 차단 시험 Collider 누락", failures); // 벽 충돌 시험 구조 검증
            ValidateFactionRules(failures); // 공통 진영 피해 규칙 검증
            ValidateStaminaSpendRule(failures); // 공격 즉시 스태미나 소비 순수 규칙 검증
            return FinishValidation(failures, showDialog); // 최종 검증 결과 반환
        }

        private static void ValidateFactionRules(List<string> failures) // 핵심 공통 진영 피해 규칙 검증
        {
            Require(CombatFactionRules.CanDamage(CombatFaction.Player, CombatFaction.Enemy), "Player → Enemy 피해가 차단됨", failures); // 플레이어 대 적 피해 허용 검증
            Require(!CombatFactionRules.CanDamage(CombatFaction.Player, CombatFaction.Ally), "Player → Ally Friendly Fire가 허용됨", failures); // 플레이어 대 아군 피해 차단 검증
            Require(!CombatFactionRules.CanDamage(CombatFaction.Enemy, CombatFaction.Enemy), "Enemy → Enemy Friendly Fire가 허용됨", failures); // 적 동일 진영 피해 차단 검증
            Require(CombatFactionRules.CanDamage(CombatFaction.Enemy, CombatFaction.Player), "Enemy → Player 피해가 차단됨", failures); // 적 대 플레이어 피해 허용 검증
            Require(CombatFactionRules.CanDamage(CombatFaction.Environment, CombatFaction.Player), "Environment → Player 피해가 차단됨", failures); // 환경 대 플레이어 피해 허용 검증
        }

        private static void ValidateStaminaSpendRule(List<string> failures) // 기존 StaminaState 공격 소비 규칙 검증
        {
            StaminaState state = new StaminaState(100f, 18f, 25f, 0.75f, 15f); // 테스트용 순수 스태미나 상태 생성
            bool spendSucceeded = state.TrySpend(12f); // 테스트 검 공격 비용 12 소비 시도
            Require(spendSucceeded, "StaminaState 공격 비용 12 소비 실패", failures); // 정상 공격 비용 소비 성공 검증
            Require(Mathf.Abs(state.CurrentValue - 88f) < 0.001f, "StaminaState 공격 후 88 값 불일치", failures); // 공격 후 남은 스태미나 값 검증
            bool overSpendSucceeded = state.TrySpend(200f); // 보유량 초과 공격 비용 소비 시도
            Require(!overSpendSucceeded, "StaminaState 보유량 초과 소비가 허용됨", failures); // 스태미나 부족 공격 차단 검증
            Require(Mathf.Abs(state.CurrentValue - 88f) < 0.001f, "실패한 스태미나 소비가 현재값을 변경함", failures); // 실패 소비 시 값 유지 검증
        }

        private static GameObject FindChildRecursive(Transform root, string childName) // 지정 루트 아래 이름 기반 자식 오브젝트 검색
        {
            if (root == null) // 검색 루트 존재 여부 확인
            {
                return null; // 검색 대상 없음 반환
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true); // 지정 루트 전체 자식 Transform 조회
            Transform match = children.FirstOrDefault(child => child != null && child.name == childName); // 지정 이름과 일치하는 첫 자식 조회
            return match == null ? null : match.gameObject; // 일치 자식 GameObject 반환
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
                Debug.Log("[Project I][Day14] 공통 Damage Pipeline·진영 규칙·공격 단계·스태미나·근접 궤적·벽 차단·F1 Combat 구성이 정적으로 정상입니다."); // 성공 로그 출력
            }
            else // 하나 이상의 검증 실패 처리
            {
                Debug.LogError($"[Project I][Day14] 검증 실패\n- {string.Join("\n- ", failures)}"); // 실패 항목 전체 Console 출력
            }

            if (showDialog) // 수동 검증 결과 대화상자 표시 여부 확인
            {
                string message = passed ? "Day14 정적 검증을 통과했습니다." : $"Day14 검증 실패\n\n- {string.Join("\n- ", failures)}"; // 대화상자 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 출력
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
