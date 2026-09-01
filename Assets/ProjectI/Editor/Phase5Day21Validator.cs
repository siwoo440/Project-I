using System.Linq; // 프리팹 계층과 씬 루트 집계 기능 참조
using ProjectI.Items; // 기존 PlayerInventory 구조 검증 참조
using ProjectI.Wagon; // Day21 마차 런타임 기능 검증 참조
using UnityEditor; // 프리팹 로드와 검증 메뉴 기능 참조
using UnityEditor.SceneManagement; // 테스트 씬 열기 기능 참조
using UnityEngine; // GameObject·Transform 검사 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 검증 도구 네임스페이스
{
    public static class Phase5Day21Validator // 공통 Wagon.prefab과 Day21 적재·보관 구조 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // Day21 테스트 씬 경로
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // 공통 마차 프리팹 경로
        private const string SceneRootName = "===Day21 Wagon System==="; // Day21 씬 시험장 루트 이름
        private const string ReadyMarkerName = "===Day21 Wagon Ready v1==="; // Day21 적용 완료 마커 이름

        [MenuItem("Tools/Project I/Day 21/Validate Wagon System")] // 수동 Day21 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            bool success = Validate(true); // 전체 검증 실행
            Debug.Log(success ? "[Project I] Day21 마차 시스템 검증 PASS" : "[Project I] Day21 마차 시스템 검증 FAIL"); // 최종 검증 결과 Console 출력
        }

        public static bool Validate(bool showDialog) // 프리팹과 씬 구조 전체 검증
        {
            bool success = true; // 전체 검증 결과 초기화
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // 공통 Wagon.prefab 로드
            success &= Check(prefab != null, "모든 맵이 공유할 Wagon.prefab 존재"); // 프리팹 에셋 존재 검증

            if (prefab != null) // 프리팹이 존재할 때 내부 구조 검증
            {
                Transform warehouse = prefab.transform.Find("Visual/LargeCargoWarehouse"); // 대형 후방 창고 루트 조회
                Transform warehouseFloor = prefab.transform.Find("Visual/LargeCargoWarehouse/Warehouse_Floor"); // 창고 바닥 조회
                Transform horseBody = prefab.transform.Find("Visual/Horse/Horse_Body"); // 말 몸통 조회
                WagonCargoArea cargoArea = prefab.GetComponentInChildren<WagonCargoArea>(true); // 창고 확보 Trigger 기능 조회
                WagonSharedStorage sharedStorage = prefab.GetComponentInChildren<WagonSharedStorage>(true); // 공동 보관함 기능 조회
                Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true); // 프리팹 전체 Transform 조회
                int wheelCount = transforms.Count(item => item.name.StartsWith("Wheel_Left_") || item.name.StartsWith("Wheel_Right_")); // 좌우 대형 바퀴 개수 집계
                success &= Check(warehouse != null && warehouseFloor != null && warehouseFloor.localScale.z >= 9.5f && warehouseFloor.localScale.z > warehouseFloor.localScale.x * 2f, "세로형 대형 후방 창고 길이 비율 확보"); // 폭보다 길이가 두 배 이상인 창고 검증
                success &= Check(wheelCount == 8, "긴 차체에 맞는 총 8개 바퀴 구성"); // 좌우 4개씩 총 8개 바퀴 검증
                success &= Check(horseBody != null, "마차 앞 말 형태 모델링 존재"); // 말 외형 핵심 몸통 검증
                success &= Check(cargoArea != null && cargoArea.GetComponent<BoxCollider>() != null && cargoArea.GetComponent<BoxCollider>().isTrigger, "대형 후방 창고 CargoArea 확보 Trigger 존재"); // 회수품 적재 Trigger 검증
                success &= Check(sharedStorage != null && sharedStorage.Capacity >= 12, "공동 보관함과 기본 12칸 용량 구성"); // 공동 보관함 기능 검증
            }

            success &= Check(typeof(PlayerInventory).GetMethod("TryStoreSelectedItem") != null, "기존 PlayerInventory 외부 보관 전송 기능 연결"); // 기존 빠른 슬롯 보관 확장 검증
            success &= Check(typeof(PlayerInventory).GetMethod("TryReceiveStoredItem") != null, "기존 PlayerInventory 공동 보관 회수 기능 연결"); // 기존 빠른 슬롯 회수 확장 검증
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 테스트 씬 단독 열기
            GameObject sceneRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == SceneRootName); // Day21 시험장 루트 검색
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // Day21 완료 마커 검색
            success &= Check(sceneRoot != null, "ExplorationOffice에 Day21 Wagon.prefab 테스트 인스턴스 구역 존재"); // 씬 시험장 존재 검증
            success &= Check(marker != null, "Day21 적용 완료 마커 존재"); // 완료 마커 검증

            if (sceneRoot != null) // 시험장 존재 시 실제 프리팹 인스턴스 검증
            {
                Transform wagonTransform = sceneRoot.transform.Find("Wagon_Day21_Test"); // 테스트 마차 인스턴스 검색
                GameObject wagonInstance = wagonTransform == null ? null : wagonTransform.gameObject; // GameObject 참조 변환
                Object source = wagonInstance == null ? null : PrefabUtility.GetCorrespondingObjectFromSource(wagonInstance); // 실제 원본 프리팹 참조 조회
                success &= Check(wagonInstance != null && source == prefab, "씬 마차가 복제 오브젝트가 아닌 공통 Wagon.prefab 인스턴스"); // 공통 프리팹 인스턴스 사용 검증
            }

            if (showDialog) // 수동 검증 결과 대화상자 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day21 Wagon 시스템 검증 PASS" : "Day21 검증 FAIL - Console을 확인하세요.", "확인"); // 최종 검증 결과 표시
            }

            return success; // 전체 결과 반환
        }

        private static bool Check(bool condition, string label) // 개별 검증 로그 처리
        {
            if (condition) // 검증 통과 여부 확인
            {
                Debug.Log("[Project I][Day21] PASS - " + label); // 통과 항목 Console 출력
                return true; // 성공 반환
            }

            Debug.LogError("[Project I][Day21] FAIL - " + label); // 실패 항목 Console 출력
            return false; // 실패 반환
        }
    }
}
