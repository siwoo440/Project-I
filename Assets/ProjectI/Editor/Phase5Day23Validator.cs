using System.IO; // 수정된 원정 손실 보호 코드 텍스트 검증 기능 참조
using System.Linq; // 거리 건물과 단상 인스턴스 집계 기능 참조
using ProjectI.Economy; // Day23 경제 기능 검증 참조
using ProjectI.Items; // 기존 WorldItem 가격 연결 검증 참조
using UnityEditor; // 프리팹 로드와 검증 메뉴 기능 참조
using UnityEditor.SceneManagement; // ExplorationOffice 씬 열기 기능 참조
using UnityEngine; // GameObject·Transform·Vector3 검사 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 검증 도구 네임스페이스
{
    public static class Phase5Day23Validator // 거리형 테스트 구역·사무소 내부 경제 기능·도로 마차 배치 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // Day23 테스트 씬 경로
        private const string PedestalPrefabPath = "Assets/ProjectI/Prefabs/Office/StoragePedestal.prefab"; // 공통 보관 단상 프리팹 경로
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // 공통 마차 프리팹 경로
        private const string OutcomeScriptPath = "Assets/ProjectI/Scripts/Expedition/ExpeditionOutcomeController.cs"; // 전멸 손실 보호 코드 경로
        private const string DistrictRootName = "===Day23 Office Street District==="; // 새 거리형 테스트 구역 루트 이름
        private const string ReadyMarkerName = "===Day23 Office Street Ready v2==="; // 거리형 완료 마커 이름
        private const string Day21RootName = "===Day21 Wagon System==="; // Day21 마차 시스템 루트 이름
        private static readonly Vector3 ExpectedWagonPosition = new Vector3(44f, 0f, 5f) + new Vector3(0f, 0.05f, -7.2f); // 새 중앙 도로의 마차 기대 위치

        [MenuItem("Tools/Project I/Day 23/Validate Office Street District")] // 수동 Day23 거리형 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 전체 검증 실행
        {
            bool success = Validate(true); // 거리·사무소·경제·마차 구조 전체 검증
            Debug.Log(success ? "[Project I] Day23 거리형 사무소 검증 PASS" : "[Project I] Day23 거리형 사무소 검증 FAIL"); // 최종 결과 Console 출력
        }

        public static bool Validate(bool showDialog) // 거리형 테스트 구역과 기존 경제 기능 통합 검증
        {
            bool success = true; // 전체 검증 결과 초기화
            GameObject pedestalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PedestalPrefabPath); // 공통 StoragePedestal.prefab 로드
            GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // 공통 Wagon.prefab 로드
            success &= Check(pedestalPrefab != null, "공통 StoragePedestal.prefab 존재"); // 보관 단상 공통 규격 검증
            success &= Check(wagonPrefab != null, "공통 Wagon.prefab 존재"); // 기존 Day21 마차 원본 보존 검증

            if (pedestalPrefab != null) // 단상 프리팹이 존재할 때 내부 기능 검증
            {
                OfficeStoragePedestal pedestal = pedestalPrefab.GetComponent<OfficeStoragePedestal>(); // 단상 가격 제한 기능 조회
                Transform displayPoint = pedestalPrefab.transform.Find("DisplayPoint"); // 실제 회수품 표시 위치 조회
                success &= Check(pedestal != null && displayPoint != null, "단상 F 보관 기능과 DisplayPoint 존재"); // 단상 상호작용 핵심 구성 검증
                success &= Check(pedestal != null && pedestal.MaxStorageValue == 1000, "가치 1000 이상 보관 차단 규칙 유지"); // 가격 제한 규칙 검증
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // ExplorationOffice 씬 단독 열기
            GameObject districtRoot = FindRoot(scene, DistrictRootName); // 거리형 Day23 구역 루트 조회
            GameObject marker = FindRoot(scene, ReadyMarkerName); // 새 완료 마커 조회
            success &= Check(districtRoot != null, "독립된 거리형 테스트 구역 존재"); // 새 맵 구역 생성 검증
            success &= Check(marker != null, "거리형 Day23 적용 완료 마커 존재"); // 최신 자동 적용 상태 검증

            if (districtRoot != null) // 거리형 구역 존재 시 모델과 경제 배치 검증
            {
                Transform road = districtRoot.transform.Find("MainRoad"); // 중앙 도로 검색
                Transform sidewalkLeft = districtRoot.transform.Find("Sidewalk_Left"); // 왼쪽 보도 검색
                Transform sidewalkRight = districtRoot.transform.Find("Sidewalk_Right"); // 오른쪽 보도 검색
                Transform office = districtRoot.transform.Find("OfficeBuilding"); // 사무소 건물 검색
                Transform officeInterior = office == null ? null : office.Find("OfficeInterior"); // 사무소 내부 경제 루트 검색
                int genericBuildingCount = districtRoot.GetComponentsInChildren<Transform>(true).Count(item => item.name.StartsWith("Building_Left_") || item.name.StartsWith("Building_Right_")); // 일반 거리 건물 개수 집계
                int streetLampCount = districtRoot.GetComponentsInChildren<Transform>(true).Count(item => item.name.StartsWith("StreetLamp_")); // 가로등 개수 집계
                success &= Check(road != null && road.localScale.z >= 30f, "중앙에 긴 도로 존재"); // 실제 거리 중심 도로 검증
                success &= Check(sidewalkLeft != null && sidewalkRight != null, "도로 양쪽 보도 존재"); // 보행 공간 검증
                success &= Check(genericBuildingCount == 5, "사무소 외 일반 건물 5개가 들어선 거리 구성"); // 사용자 요청 건물 거리 모델 검증
                success &= Check(streetLampCount >= 4, "거리 가로등 4개 이상 배치"); // 거리 시각 요소 검증
                success &= Check(office != null && office.Find("Office_Floor") != null && office.Find("Office_Roof") != null, "출입 가능한 사무소 건물 모델 존재"); // 사무소 외형 검증
                success &= Check(officeInterior != null, "사무소 건물 내부에 경제 기능 루트 존재"); // 경제 기능 실내 배치 검증

                if (officeInterior != null) // 사무소 내부 존재 시 경제 기능 상세 검증
                {
                    CampaignEconomy economy = officeInterior.GetComponent<CampaignEconomy>(); // 공동 자금 상태 조회
                    OfficeStoragePedestal[] pedestals = officeInterior.GetComponentsInChildren<OfficeStoragePedestal>(true); // 사무소 내부 단상 조회
                    OfficeSaleCounter saleCounter = officeInterior.GetComponentInChildren<OfficeSaleCounter>(true); // 판매대 조회
                    DebtLedger debtLedger = officeInterior.GetComponentInChildren<DebtLedger>(true); // 채무 장부 조회
                    success &= Check(economy != null, "사무소 내부 공동 자금 시스템 존재"); // CampaignEconomy 실내 배치 검증
                    success &= Check(pedestals.Length == 6, "사무소 내부 공통 보관 단상 6개 배치"); // 단상 개수 검증
                    success &= Check(pedestals.All(item => item.MaxStorageValue == 1000), "단상 6개 모두 동일한 가격 제한 사용"); // 단상 규칙 통일 검증
                    success &= Check(saleCounter != null, "사무소 내부 회수품 판매대 존재"); // 판매 기능 위치 검증
                    success &= Check(debtLedger != null, "사무소 내부 채무 장부 존재"); // 채무 기능 위치 검증

                    foreach (OfficeStoragePedestal pedestal in pedestals) // 모든 사무소 단상 순회
                    {
                        Object source = PrefabUtility.GetCorrespondingObjectFromSource(pedestal.gameObject); // 각 단상의 원본 프리팹 조회
                        success &= Check(source == pedestalPrefab, pedestal.name + " 공통 StoragePedestal.prefab 인스턴스 사용"); // 개별 복제 대신 공통 규격 사용 검증
                    }
                }
            }

            GameObject day21Root = FindRoot(scene, Day21RootName); // 기존 Day21 마차 시스템 루트 조회
            Transform wagon = day21Root == null ? null : day21Root.transform.Find("Wagon_Day21_Test"); // 기존 공통 마차 인스턴스 조회
            Transform oldWagonFloor = day21Root == null ? null : day21Root.transform.Find("Wagon_TestFloor"); // 예전 독립 마차 바닥 조회
            success &= Check(wagon != null, "마차가 새 거리 도로에 유지"); // 마차 씬 인스턴스 존재 검증
            success &= Check(oldWagonFloor == null, "이전 별도 Wagon_TestFloor 제거"); // 이전 마차 테스트 구역 정리 검증

            if (wagon != null && wagonPrefab != null) // 마차 인스턴스와 공통 프리팹 존재 시 상세 검증
            {
                Object wagonSource = PrefabUtility.GetCorrespondingObjectFromSource(wagon.gameObject); // 마차 원본 프리팹 참조 조회
                success &= Check(wagonSource == wagonPrefab, "도로 마차가 기존 공통 Wagon.prefab 인스턴스"); // 공통 규격 유지 검증
                success &= Check(Vector3.Distance(wagon.position, ExpectedWagonPosition) <= 0.25f, "마차가 거리 중앙 도로 주차 위치로 이동"); // 실제 도로 재배치 검증
            }

            success &= ValidateTreasureValue(scene, "Day20_SilverCoin", 300); // 은 동전 가격 연결 검증
            success &= ValidateTreasureValue(scene, "Day20_MetalOrnament", 750); // 금속 장식 가격 연결 검증
            success &= ValidateTreasureValue(scene, "Day20_Crown", 2250); // 왕관 가격 연결 검증
            success &= ValidateTreasureValue(scene, "Day20_GodsStatue", 1500); // 조각상 가격 연결 검증

            string outcomeSource = File.Exists(OutcomeScriptPath) ? File.ReadAllText(OutcomeScriptPath) : string.Empty; // 원정 결과 소스 텍스트 읽기
            success &= Check(outcomeSource.Contains("officeState.IsOfficeStored"), "단상 보관품 전멸 손실에서 제외"); // 영구 보관 핵심 규칙 검증
            success &= Check(outcomeSource.Contains("recoverableValue.IsSold"), "판매 완료품 원정 판정 재진입 방지"); // 중복 판매·손실 방지 검증

            if (showDialog) // 수동 검증 결과 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day23 거리·사무소·도로 마차 검증 PASS" : "Day23 검증 FAIL - Console을 확인하세요.", "확인"); // 검증 결과 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateTreasureValue(Scene scene, string objectName, int expectedValue) // 기존 Day20 테스트 회수품 가격 연결 검증
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 씬 전체 루트 순회
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true); // 비활성 자식 포함 전체 Transform 조회
                Transform match = transforms.FirstOrDefault(item => item.name == objectName); // 정확한 대상 이름 검색

                if (match == null) // 현재 루트에 대상이 없는지 확인
                {
                    continue; // 다음 루트 검색
                }

                WorldItem worldItem = match.GetComponent<WorldItem>(); // 기존 WorldItem 존재 여부 조회
                RecoverableValue value = match.GetComponent<RecoverableValue>(); // Day23 가격 상태 조회
                return Check(worldItem != null && value != null && value.Value == expectedValue, $"{objectName} 기존 WorldItem 유지 + 가격 {expectedValue} 연결"); // 기존 시스템과 가격 상태 검증
            }

            return Check(false, $"{objectName} 테스트 회수품 존재"); // 대상 누락 실패 출력
        }

        private static GameObject FindRoot(Scene scene, string rootName) // 씬 루트 이름 검색
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 첫 일치 루트 반환
        }

        private static bool Check(bool condition, string label) // 개별 검증 결과 로그 처리
        {
            if (condition) // 검증 통과 여부 확인
            {
                Debug.Log("[Project I][Day23] PASS - " + label); // 통과 항목 Console 출력
                return true; // 성공 반환
            }

            Debug.LogError("[Project I][Day23] FAIL - " + label); // 실패 항목 Console 출력
            return false; // 실패 반환
        }
    }
}
