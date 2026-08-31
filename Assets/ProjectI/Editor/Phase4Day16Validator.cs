using System.Collections.Generic; // 검증 실패 항목 목록 기능 참조
using System.IO; // 대상 씬 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트·무기 검색 기능 참조
using ProjectI.Combat; // 원거리 표적 공통 체력 검증 참조
using ProjectI.Combat.Ranged; // 석궁·리볼버 기능 검증 참조
using ProjectI.Diagnostics; // F1 Ranged Combat 페이지 검증 참조
using ProjectI.Items; // WorldItem 운반 방식 검증 참조
using ProjectI.Player; // 플레이어 입력 기능 검증 참조
using UnityEditor; // 유니티 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 오브젝트 검색 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 검증 도구 네임스페이스
{
    public static class Phase4Day16Validator // Day16 석궁·리볼버 원거리 전투 정적 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string Day16RootName = "===Day16 Ranged Combat==="; // Day16 시험장 루트 이름
        private const string ReadyMarkerName = "===Day16 Ranged Combat Ready v2==="; // 석궁 탄속·리볼버 외형 보정 버전 완료 마커 이름

        [MenuItem("Tools/Project I/Day 16/Validate")] // 수동 Day16 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 전체 검증 실행
        {
            Validate(true); // 결과 대화상자를 포함한 검증 실행
        }

        public static bool Validate(bool showDialog) // Day16 원거리 전투 구조 검증 실행
        {
            List<string> failures = new List<string>(); // 검증 실패 목록 생성

            if (!File.Exists(ExplorationOfficeScenePath)) // 탐사 씬 파일 존재 여부 확인
            {
                failures.Add("ExplorationOffice.unity 누락"); // 씬 누락 실패 기록
                return FinishValidation(failures, showDialog); // 즉시 결과 반환
            }

            Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회

            if (scene.path != ExplorationOfficeScenePath) // 현재 씬이 탐사 사무소인지 확인
            {
                scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 검증 대상 탐사 씬 열기
            }

            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == Day16RootName); // Day16 시험장 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ReadyMarkerName); // Day16 완료 마커 조회
            CrossbowWeaponItem crossbow = Object.FindFirstObjectByType<CrossbowWeaponItem>(); // 석궁 월드 아이템 조회
            RevolverWeaponItem revolver = Object.FindFirstObjectByType<RevolverWeaponItem>(); // 리볼버 월드 아이템 조회
            RangedCombatDebugPage debugPage = Object.FindFirstObjectByType<RangedCombatDebugPage>(); // F1 원거리 진단 페이지 조회
            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // Aim·Reload 입력 래퍼 조회
            CombatHealth[] targets = Object.FindObjectsByType<CombatHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(target => target != null && target.name.StartsWith("RangedTarget_")).ToArray(); // Day16 원거리 표적만 조회
            Require(root != null, "Day16 Ranged Combat 루트 누락", failures); // 시험장 루트 존재 검증
            Require(marker != null, "Day16 완료 마커 누락", failures); // 완료 마커 존재 검증
            Require(inputReader != null, "PlayerInputReader 누락", failures); // 공통 입력 래퍼 존재 검증
            Require(debugPage != null, "F1 RangedCombatDebugPage 누락", failures); // F1 원거리 페이지 존재 검증
            Require(crossbow != null, "Day16_Crossbow 누락", failures); // 석궁 월드 배치 검증
            Require(revolver != null, "Day16_Revolver 누락", failures); // 리볼버 월드 배치 검증
            Require(crossbow != null && crossbow.GetComponent<WorldItem>() != null && crossbow.GetComponent<WorldItem>().CarryType == CarryType.OneHand, "석궁 무기 슬롯 전환용 WorldItem 운반 규칙 누락", failures); // 석궁 빠른 슬롯 전환을 막지 않는 무기 운반 규칙 검증
            Require(revolver != null && revolver.GetComponent<WorldItem>() != null && revolver.GetComponent<WorldItem>().CarryType == CarryType.OneHand, "리볼버 OneHand WorldItem 연동 누락", failures); // 리볼버 기존 빠른 슬롯·한손 운반 검증
            Require(crossbow != null && crossbow.VisualPivot != null && crossbow.VisualPivot.childCount >= 14, "석궁 상세 모델 파트 수 부족", failures); // 석궁 프리미티브 상세 모델 검증
            Require(revolver != null && revolver.VisualPivot != null && revolver.VisualPivot.childCount >= 12, "리볼버 상세 모델 파트 수 부족", failures); // 리볼버 프리미티브 상세 모델 검증
            Require(crossbow != null && crossbow.Muzzle != null && crossbow.BoltTemplate != null, "석궁 발사 위치 또는 볼트 템플릿 누락", failures); // 실제 포물선 발사 구성 검증
            Require(crossbow != null && crossbow.ProjectileSpeed >= 94.9f && crossbow.BaseDamage >= 50f, "석궁 2.5배 탄속 또는 피해 설정 미달", failures); // 95m/s로 상향된 강한 단발 석궁 기본 수치 검증
            Require(crossbow != null && crossbow.AimFieldOfView <= 45f, "석궁 장거리 확대 조준 FOV가 충분히 좁지 않음", failures); // 석궁 강한 줌 검증
            Require(revolver != null && revolver.CylinderCapacity == 6 && revolver.LoadedRounds == 6, "리볼버 6발 실린더 초기 구성 오류", failures); // 리볼버 6발 규칙 검증
            Require(revolver != null && revolver.CylinderRoot != null && revolver.Muzzle != null, "리볼버 실린더 또는 총구 누락", failures); // 리볼버 장전·발사 시각 구조 검증
            Require(revolver != null && revolver.AimFieldOfView > (crossbow == null ? 0f : crossbow.AimFieldOfView), "리볼버 조준 확대가 석궁보다 강하거나 동일함", failures); // 석궁이 더 강하게 줌되는 역할 차이 검증
            Require(targets.Length >= 3, "Day16 원거리 시험 표적 3개 미만", failures); // 사격 거리 표적 개수 검증
            Require(root == null || FindChildRecursive(root.transform, "Day16_CrossbowBoltTemplate") != null, "석궁 포물선 볼트 템플릿 오브젝트 누락", failures); // 비활성 볼트 템플릿 구조 검증
            Require(root == null || FindChildRecursive(root.transform, "Crossbow_DisplayBase") != null, "석궁 전시대 누락", failures); // 석궁 테스트 맵 위치 검증
            Require(root == null || FindChildRecursive(root.transform, "Revolver_DisplayBase") != null, "리볼버 전시대 누락", failures); // 리볼버 테스트 맵 위치 검증
            return FinishValidation(failures, showDialog); // 최종 검증 결과 반환
        }

        private static GameObject FindChildRecursive(Transform root, string childName) // 지정 루트 아래 이름 기반 자식 검색
        {
            if (root == null) // 검색 루트 누락 확인
            {
                return null; // 대상 없음 반환
            }

            Transform match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child != null && child.name == childName); // 비활성 템플릿 포함 전체 자식에서 이름 검색
            return match == null ? null : match.gameObject; // 검색된 GameObject 반환
        }

        private static void Require(bool condition, string failureMessage, List<string> failures) // 단일 검증 조건 처리
        {
            if (!condition) // 검증 조건 실패 여부 확인
            {
                failures.Add(failureMessage); // 실패 목록에 원인 추가
            }
        }

        private static bool FinishValidation(List<string> failures, bool showDialog) // 검증 결과 로그·대화상자 출력
        {
            bool passed = failures.Count == 0; // 전체 검증 통과 여부 계산

            if (passed) // 모든 검증 조건 정상 여부 확인
            {
                Debug.Log("[Project I][Day16] 석궁 포물선·확대 조준·볼트 회수 및 리볼버 6발·연사 탄퍼짐·장전 구성이 정적으로 정상입니다."); // 성공 로그 출력
            }
            else // 하나 이상의 검증 실패 처리
            {
                Debug.LogError($"[Project I][Day16] 검증 실패\n- {string.Join("\n- ", failures)}"); // 실패 항목 전체 Console 출력
            }

            if (showDialog) // 수동 검증 결과 대화상자 표시 여부 확인
            {
                string message = passed ? "Day16 정적 검증을 통과했습니다." : $"Day16 검증 실패\n\n- {string.Join("\n- ", failures)}"; // 대화상자 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 출력
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
