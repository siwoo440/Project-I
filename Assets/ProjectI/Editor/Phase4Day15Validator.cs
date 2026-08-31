using System.Collections.Generic; // 검증 실패 항목 목록 기능 참조
using System.IO; // 대상 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트와 전투 대상 검색 기능 참조
using ProjectI.Combat; // Day15 단발 근접 전투 기능 검증 참조
using ProjectI.Diagnostics; // F1 Combat 페이지 검증 참조
using ProjectI.Items; // 기존 WorldItem 연동 검증 참조
using UnityEditor; // 유니티 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 오브젝트 검색 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 검증 도구 네임스페이스
{
    public static class Phase4Day15Validator // Day15 검·도끼 단발 공격과 경직·넉백 정적 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string Day15RootName = "===Day15 Single Melee Combat==="; // Day15 근접 전투 시험장 루트 이름
        private const string ReadyMarkerName = "===Day15 Single Melee Combat Ready v5==="; // 검·도끼 베기 방향 반전 보정 버전 완료 마커 이름
        private const string SwordAttackPath = "Assets/ProjectI/Resources/Combat/Day15_SwordSlash.asset"; // Day15 검 공격 데이터 경로
        private const string AxeAttackPath = "Assets/ProjectI/Resources/Combat/Day15_AxeSwing.asset"; // Day15 도끼 공격 데이터 경로

        [MenuItem("Tools/Project I/Day 15/Validate")] // 수동 Day15 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 Day15 전체 검증 실행
        {
            Validate(true); // 결과 대화상자를 포함한 전체 검증 실행
        }

        public static bool Validate(bool showDialog) // Day15 단발 근접 전투 기반 검증 실행
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

            GameObject day15Root = scene.GetRootGameObjects().FirstOrDefault(root => root.name == Day15RootName); // Day15 근접 전투 시험장 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // Day15 완료 마커 조회
            CombatController combatController = Object.FindFirstObjectByType<CombatController>(); // 플레이어 공통 전투 제어기 조회
            CombatDebugPage debugPage = Object.FindFirstObjectByType<CombatDebugPage>(); // F1 Combat 진단 페이지 조회
            MeleeWeaponItem[] weapons = Object.FindObjectsByType<MeleeWeaponItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 근접 무기 전체 조회
            CombatReaction[] reactions = Object.FindObjectsByType<CombatReaction>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 경직·넉백 반응 대상 전체 조회
            CombatHealth[] healthTargets = Object.FindObjectsByType<CombatHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 공통 전투 체력 대상 전체 조회
            MeleeWeaponItem sword = weapons.FirstOrDefault(weapon => weapon != null && weapon.gameObject.name == "Day15_IronSword"); // Day15 검 월드 아이템 조회
            MeleeWeaponItem axe = weapons.FirstOrDefault(weapon => weapon != null && weapon.gameObject.name == "Day15_IronAxe"); // Day15 도끼 월드 아이템 조회
            bool hasLegacySword = weapons.Any(weapon => weapon != null && weapon.gameObject.name == "Day14_CombatTestSword"); // Day14 임시 테스트 검 잔존 여부 확인
            AttackDefinition swordAttack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(SwordAttackPath); // Day15 검 공격 데이터 조회
            AttackDefinition axeAttack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AxeAttackPath); // Day15 도끼 공격 데이터 조회
            CombatHealth normalDummy = healthTargets.FirstOrDefault(target => target != null && target.gameObject.name == "CombatDummy_Enemy"); // 일반 경직 시험 더미 조회
            CombatHealth heavyDummy = healthTargets.FirstOrDefault(target => target != null && target.gameObject.name == "CombatDummy_Heavy"); // 중장 경직 시험 더미 조회
            CombatReaction normalReaction = normalDummy == null ? null : normalDummy.GetComponent<CombatReaction>(); // 일반 더미 반응 기능 조회
            CombatReaction heavyReaction = heavyDummy == null ? null : heavyDummy.GetComponent<CombatReaction>(); // 중장 더미 반응 기능 조회

            Require(marker != null, "Day15 완료 마커 누락", failures); // 완료 마커 존재 검증
            Require(day15Root != null, "Day15 Single Melee Combat 루트 누락", failures); // Day15 시험장 루트 존재 검증
            Require(combatController != null, "CombatController 누락", failures); // 공통 전투 제어기 존재 검증
            Require(debugPage != null, "F1 CombatDebugPage 누락", failures); // F1 전투 진단 페이지 검증
            Require(sword != null, "Day15_IronSword 누락", failures); // 검 월드 배치 검증
            Require(axe != null, "Day15_IronAxe 누락", failures); // 도끼 월드 배치 검증
            Require(sword != null && sword.transform.position.x < -28f && sword.transform.position.z < 0f, "Day15 검이 01_SprintLane 파란 구역에 배치되지 않음", failures); // 검 SprintLane 이동 위치 검증
            Require(axe != null && axe.transform.position.x > -26f && axe.transform.position.z < 0f, "Day15 도끼가 01_SprintLane 파란 구역에 배치되지 않음", failures); // 도끼 SprintLane 이동 위치 검증
            Require(!hasLegacySword, "Day14 임시 테스트 검이 Day15 씬에 남아 있음", failures); // 임시 검 정리 여부 검증
            Require(sword != null && sword.GetComponent<WorldItem>() != null, "검 WorldItem 연동 누락", failures); // 검 기존 획득 체계 연동 검증
            Require(axe != null && axe.GetComponent<WorldItem>() != null, "도끼 WorldItem 연동 누락", failures); // 도끼 기존 획득 체계 연동 검증
            Require(sword != null && sword.WeaponTrace != null && sword.WeaponTrace.TraceStart != null && sword.WeaponTrace.TraceEnd != null, "검 궤적 기준점 누락", failures); // 검 궤적 구성 검증
            Require(axe != null && axe.WeaponTrace != null && axe.WeaponTrace.TraceStart != null && axe.WeaponTrace.TraceEnd != null, "도끼 궤적 기준점 누락", failures); // 도끼 궤적 구성 검증
            Require(sword != null && sword.VisualPivot != null && sword.VisualPivot.childCount >= 12, "검 상세 모델 파트 수 부족", failures); // 검 세부 모델링 구성 검증
            Require(axe != null && axe.VisualPivot != null && axe.VisualPivot.childCount >= 10, "도끼 상세 모델 파트 수 부족", failures); // 도끼 세부 모델링 구성 검증
            Require(swordAttack != null, "Day15_SwordSlash AttackDefinition 누락", failures); // 검 공격 데이터 존재 검증
            Require(axeAttack != null, "Day15_AxeSwing AttackDefinition 누락", failures); // 도끼 공격 데이터 존재 검증
            ValidateAttackData(swordAttack, axeAttack, failures); // 검·도끼 단발 공격 수치 관계 검증
            Require(reactions.Length >= 4, "CombatReaction 적용 대상 4개 미만", failures); // 기존 더미와 중장 더미 반응 기능 개수 검증
            Require(normalReaction != null && Mathf.Abs(normalReaction.StaggerThreshold - 30f) < 0.01f, "일반 더미 경직 한계 30 미구성", failures); // 일반 더미 경직 수치 검증
            Require(heavyReaction != null && Mathf.Abs(heavyReaction.StaggerThreshold - 80f) < 0.01f, "중장 더미 경직 한계 80 미구성", failures); // 중장 더미 경직 수치 검증
            Require(heavyReaction != null && normalReaction != null && heavyReaction.KnockbackResistance > normalReaction.KnockbackResistance, "중장 더미 넉백 저항이 일반 더미보다 높지 않음", failures); // 중장 넉백 저항 차별화 검증
            Require(CombatFactionRules.CanDamage(CombatFaction.Player, CombatFaction.Enemy), "Player → Enemy 피해 규칙 오류", failures); // 기존 공통 피해 규칙 유지 검증
            return FinishValidation(failures, showDialog); // 최종 검증 결과 반환
        }

        private static void ValidateAttackData(AttackDefinition swordAttack, AttackDefinition axeAttack, List<string> failures) // 검·도끼 단발 공격 데이터 수치 검증
        {
            if (swordAttack == null || axeAttack == null) // 공격 데이터 누락 여부 확인
            {
                return; // 개별 누락 오류만 유지하고 수치 비교 생략
            }

            Require(Mathf.Abs(swordAttack.BaseDamage - 25f) < 0.01f, "검 피해량 25 불일치", failures); // 검 기본 피해량 검증
            Require(Mathf.Abs(swordAttack.CooldownDuration - 0.65f) < 0.01f, "검 쿨타임 0.65초 불일치", failures); // 검 최소 공격 간격 검증
            Require(Mathf.Abs(axeAttack.BaseDamage - 45f) < 0.01f, "도끼 피해량 45 불일치", failures); // 도끼 기본 피해량 검증
            Require(Mathf.Abs(axeAttack.CooldownDuration - 1.15f) < 0.01f, "도끼 쿨타임 1.15초 불일치", failures); // 도끼 최소 공격 간격 검증
            Require(axeAttack.StaminaCost > swordAttack.StaminaCost, "도끼 스태미나 비용이 검보다 높지 않음", failures); // 무기 자원 차별화 검증
            Require(axeAttack.StaggerPower > swordAttack.StaggerPower, "도끼 경직 힘이 검보다 높지 않음", failures); // 무기 경직 차별화 검증
            Require(axeAttack.KnockbackForce > swordAttack.KnockbackForce, "도끼 넉백 힘이 검보다 높지 않음", failures); // 무기 넉백 차별화 검증
            Require(axeAttack.MovementMultiplier < swordAttack.MovementMultiplier, "도끼 공격 중 이동 제한이 검보다 약함", failures); // 무기 이동 제한 차별화 검증
            Require(swordAttack.CooldownDuration >= swordAttack.TotalAttackDuration, "검 공격 동작 시간이 쿨타임보다 김", failures); // 검 단발 공격 시간 관계 검증
            Require(axeAttack.CooldownDuration >= axeAttack.TotalAttackDuration, "도끼 공격 동작 시간이 쿨타임보다 김", failures); // 도끼 단발 공격 시간 관계 검증
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
                Debug.Log("[Project I][Day15] 검·도끼 단발 공격·쿨타임·경직·넉백·상세 무기 모델 구성이 정적으로 정상입니다."); // 성공 로그 출력
            }
            else // 하나 이상의 검증 실패 처리
            {
                Debug.LogError($"[Project I][Day15] 검증 실패\n- {string.Join("\n- ", failures)}"); // 실패 항목 전체 Console 출력
            }

            if (showDialog) // 수동 검증 결과 대화상자 표시 여부 확인
            {
                string message = passed ? "Day15 정적 검증을 통과했습니다." : $"Day15 검증 실패\n\n- {string.Join("\n- ", failures)}"; // 대화상자 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 출력
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
