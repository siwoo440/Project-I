using System.Linq; // 씬 루트와 프리팹 신체 개수 검사 기능 참조
using ProjectI.Expedition; // 원정 결과 컨트롤러 검증 참조
using ProjectI.Player; // PlayerDeathController 검증 참조
using ProjectI.Wagon; // 회수품·죽은 플레이어 공통 WagonCargoArea 검증 참조
using UnityEditor; // 프리팹 로드·수동 검증 메뉴 기능 참조
using UnityEditor.SceneManagement; // ExplorationOffice 씬 열기 기능 참조
using UnityEngine; // GameObject·Transform·물리 컴포넌트 검사 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 검증 도구 네임스페이스
{
    public static class Phase5Day22Validator // Player 본체 래그돌 사망·마차 공통 회수·원정 손실 구조 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // Day22 테스트 씬 경로
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // 공통 마차 프리팹 경로
        private const string PlayerRootName = "Player"; // 기존 플레이어 루트 이름
        private const string SystemRootName = "===Day22 Death Expedition System==="; // Day22 테스트 시스템 루트 이름
        private const string ReadyMarkerName = "===Day22 Death Expedition Ready v1==="; // Day22 적용 완료 마커 이름

        [MenuItem("Tools/Project I/Day 22/Validate Ragdoll Death + Expedition Loss")] // 수동 전체 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 Day22 검증 실행
        {
            bool success = Validate(true); // 전체 구조 검증 실행
            Debug.Log(success ? "[Project I] Day22 사망·원정 손실 검증 PASS" : "[Project I] Day22 사망·원정 손실 검증 FAIL"); // Console 최종 결과 출력
        }

        public static bool Validate(bool showDialog) // Day22 Player·Wagon·씬 구조 전체 검증
        {
            bool success = true; // 전체 결과 초기화
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 테스트 씬 단독 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 기존 Player 루트 조회
            success &= Check(player != null, "기존 Player 루트 유지"); // 별도 시체 오브젝트 교체가 아닌 기존 Player 사용 검증

            if (player != null) // Player 존재 시 내부 사망 구조 검사
            {
                PlayerDeathController deathController = player.GetComponent<PlayerDeathController>(); // 사망 전환 컨트롤러 조회
                Transform ragdollRoot = player.transform.Find("DeathRagdoll"); // Player 내부 사망 래그돌 조회
                success &= Check(deathController != null, "PlayerHealth.Died 연결용 PlayerDeathController 존재"); // 사망 컨트롤러 부착 검증
                success &= Check(ragdollRoot != null, "별도 Corpse Prefab 없이 Player 내부 DeathRagdoll 존재"); // 동일 Player 시체 구조 검증

                if (ragdollRoot != null) // 래그돌 존재 시 신체 물리 구성 검사
                {
                    int rigidbodyCount = ragdollRoot.GetComponentsInChildren<Rigidbody>(true).Length; // 비활성 신체 포함 Rigidbody 개수 계산
                    int jointCount = ragdollRoot.GetComponentsInChildren<CharacterJoint>(true).Length; // 관절 개수 계산
                    success &= Check(!ragdollRoot.gameObject.activeSelf, "생존 상태에서 DeathRagdoll 비활성"); // 1인칭 플레이 중 몸체 숨김 검증
                    success &= Check(rigidbodyCount >= 11, "머리·몸통·팔·다리 래그돌 Rigidbody 11개 이상"); // 인체 물리 부위 개수 검증
                    success &= Check(jointCount >= 10, "힘이 풀리는 관절 연결 CharacterJoint 10개 이상"); // 신체 연결 관절 개수 검증
                    success &= Check(ragdollRoot.Find("Pelvis") != null && ragdollRoot.Find("Chest") != null && ragdollRoot.Find("Head") != null, "골반·가슴·머리 핵심 신체 존재"); // 중심 신체 구성 검증
                    success &= Check(ragdollRoot.Find("UpperArm_L") != null && ragdollRoot.Find("UpperArm_R") != null && ragdollRoot.Find("LowerLeg_L") != null && ragdollRoot.Find("LowerLeg_R") != null, "좌우 팔·다리 신체 구성 존재"); // 사지 구성 검증
                }

                CharacterController controller = player.GetComponent<CharacterController>(); // 기존 생존 CharacterController 조회
                success &= Check(controller != null, "생존 상태 기존 CharacterController 유지"); // 기존 이동 시스템 보존 검증
            }

            GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // 공통 Wagon.prefab 로드
            success &= Check(wagonPrefab != null, "Day21 공통 Wagon.prefab 유지"); // 모든 맵 동일 마차 원본 존재 검증

            if (wagonPrefab != null) // 마차 프리팹 존재 시 Day22 공통 회수 영역 검사
            {
                Transform cargoTransform = wagonPrefab.transform.Find("CargoArea"); // 회수품과 죽은 플레이어가 함께 사용하는 단일 영역 조회
                Transform corpseTransform = wagonPrefab.transform.Find("CorpseArea"); // 이전 분리 영역이 남아 있는지 확인
                WagonCargoArea cargoArea = cargoTransform == null ? null : cargoTransform.GetComponent<WagonCargoArea>(); // 공통 적재·회수 기능 조회
                BoxCollider cargoCollider = cargoTransform == null ? null : cargoTransform.GetComponent<BoxCollider>(); // 공통 창고 Trigger 조회
                success &= Check(corpseTransform == null, "별도 CorpseArea 없이 하나의 CargoArea만 사용"); // 사용자 요구대로 영역 분리 제거 검증
                success &= Check(cargoArea != null && cargoCollider != null && cargoCollider.isTrigger, "Wagon.prefab 단일 CargoArea 공통 회수 Trigger 존재"); // 하나의 기능·Trigger 검증
                success &= Check(cargoCollider != null && cargoCollider.size.z >= 7.7f && Mathf.Abs(cargoCollider.center.z) <= 0.01f, "대형 후방 창고 전체 7.8m를 동일 회수 영역으로 사용"); // 창고 전체 범위 복구 검증
                success &= Check(typeof(WagonCargoArea).GetProperty("RecoveredPlayerCount") != null && typeof(WagonCargoArea).GetMethod("IsRecovered", new[] { typeof(PlayerDeathController) }) != null, "동일 CargoArea가 회수품과 죽은 플레이어를 함께 판정"); // 공통 기능 API 검증
            }

            GameObject systemRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == SystemRootName); // Day22 테스트 시스템 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // Day22 완료 마커 조회
            success &= Check(systemRoot != null, "Day22 사망·원정 결과 테스트 시스템 루트 존재"); // 테스트 구역 구성 검증
            success &= Check(marker != null, "Day22 적용 완료 마커 존재"); // 자동 적용 완료 검증

            if (systemRoot != null) // 시스템 루트 존재 시 런타임 컨트롤러 검사
            {
                ExpeditionOutcomeController outcome = systemRoot.GetComponent<ExpeditionOutcomeController>(); // 원정 결과 컨트롤러 조회
                Transform lethal = systemRoot.transform.Find("Day22_Lethal_Ragdoll_Test"); // 치명 피해 테스트 상자 조회
                Transform terminal = systemRoot.transform.Find("Day22_Return_Result_Terminal"); // 귀환 판정 단말 조회
                success &= Check(outcome != null, "NormalReturn·PartialReturn·Failed 원정 결과 컨트롤러 존재"); // 원정 결과 판정 구조 검증
                success &= Check(lethal != null && lethal.GetComponent<Day22LethalTester>() != null, "F 입력 치명 피해·래그돌 테스트 오브젝트 존재"); // 사망 테스트 수단 검증
                success &= Check(terminal != null && terminal.GetComponent<ExpeditionReturnTerminal>() != null, "F 입력 원정 귀환·손실 판정 단말 존재"); // 귀환 결과 테스트 수단 검증
            }

            GameObject forbiddenCorpsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectI/Prefabs/Player/PlayerCorpse.prefab"); // 별도 시체 Prefab 생성 여부 조회
            success &= Check(forbiddenCorpsePrefab == null, "별도 PlayerCorpse.prefab 미사용"); // 사용자 요구대로 Player 본체가 시체인지 검증

            if (showDialog) // 수동 검증 결과 대화상자 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day22 래그돌 사망·원정 손실 검증 PASS" : "Day22 검증 FAIL - Console을 확인하세요.", "확인"); // 최종 결과 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool Check(bool condition, string label) // 개별 검증 로그 출력
        {
            if (condition) // 검증 통과 여부 확인
            {
                Debug.Log("[Project I][Day22] PASS - " + label); // 통과 항목 Console 출력
                return true; // 성공 반환
            }

            Debug.LogError("[Project I][Day22] FAIL - " + label); // 실패 항목 Console 출력
            return false; // 실패 반환
        }
    }
}
