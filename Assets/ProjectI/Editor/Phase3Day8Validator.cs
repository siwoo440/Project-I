using System.Linq; // 씬 루트와 휴대 조명 검색 기능 참조
using ProjectI.Brightness; // 이동형 광원과 내부 방 기능 참조
using ProjectI.Items; // WorldItem과 CarryType 검증 기능 참조
using ProjectI.Lighting; // 휴대 조명과 F1 디버그 페이지 기능 참조
using UnityEditor; // 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day8Validator // Day 8 휴대 조명·연료·이동형 밝기 구조 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 씬 경로

        [MenuItem("Tools/Project I/Day 8/Validate")] // 수동 Day 8 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            bool success = Validate(true); // 전체 Day 8 검증 실행

            if (!success) // 실패 여부 확인
            {
                Debug.LogError("[Project I] Day 8 검증 실패 - 위 FAIL 항목을 확인하세요."); // 실패 안내 출력
            }
        }

        public static bool Validate(bool showDialog) // 휴대 조명 구성과 핵심 위치 기반 공간 판정 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "===Day3 Test Map==="); // 공용 테스트 맵 루트 조회
            Transform brightnessZone = mapRoot == null ? null : mapRoot.transform.Find("10_BrightnessTest"); // 7일차 밝기 시험 모듈 조회
            Transform portableRoot = brightnessZone == null ? null : brightnessZone.Find("PortableLightTest"); // 8일차 휴대 조명 시험 루트 조회
            PortableLightItem torch = portableRoot == null ? null : portableRoot.Find("TestTorch")?.GetComponent<PortableLightItem>(); // 시험용 횃불 조회
            PortableLightItem lantern = portableRoot == null ? null : portableRoot.Find("TestLantern")?.GetComponent<PortableLightItem>(); // 시험용 랜턴 조회
            IndoorBrightnessArea indoorArea = brightnessZone == null ? null : brightnessZone.GetComponentInChildren<IndoorBrightnessArea>(true); // 위치 이동 검증용 건물 내부 영역 조회
            bool itemsPass = torch != null && lantern != null; // 횃불과 랜턴 존재 검증
            bool oneHandPass = ValidateOneHand(torch) && ValidateOneHand(lantern); // 기존 6칸 인벤토리의 OneHand 규칙 검증
            bool portableTypePass = ValidatePortableSource(torch) && ValidatePortableSource(lantern); // 두 광원의 Portable 종류 검증
            bool fuelPass = torch != null && lantern != null && Mathf.Approximately(torch.MaxFuel, 60f) && Mathf.Approximately(lantern.MaxFuel, 120f); // 횃불 60·랜턴 120 기본 연료 검증
            bool startOffPass = torch != null && lantern != null && !torch.IsIgnited && !lantern.IsIgnited && !torch.IsEmitting && !lantern.IsEmitting; // 시작 소화 상태 검증
            bool positionAreaPass = ValidatePortableAreaSwitch(torch, indoorArea); // 이동형 광원이 실제 위치에 따라 Outdoor/Indoor를 바꾸는지 검증
            bool debugPagePass = player != null && player.GetComponent<PortableLightDebugPage>() != null; // F1 통합 목록용 휴대 조명 페이지 검증
            bool torchProfilePass = ValidateTorchProfile(torch); // 횃불 근거리 Point Light와 불꽃 끝부분 광원 위치 프로필 검증
            bool lanternProfilePass = ValidateLanternProfile(lantern); // 랜턴 장거리 Spot Light와 중앙 조준 프로필 검증
            bool success = itemsPass && oneHandPass && portableTypePass && fuelPass && startOffPass && positionAreaPass && debugPagePass && torchProfilePass && lanternProfilePass; // 전체 Day 8 검증 결과 계산

            LogResult("Torch + Lantern", itemsPass); // 휴대 조명 두 종류 결과 출력
            LogResult("OneHand Inventory Rule", oneHandPass); // 한손 인벤토리 규칙 결과 출력
            LogResult("Portable BrightnessSource", portableTypePass); // 이동형 광원 종류 결과 출력
            LogResult("Fuel 60 / 120", fuelPass); // 연료 기본값 결과 출력
            LogResult("Start Extinguished", startOffPass); // 시작 소화 상태 결과 출력
            LogResult("Portable Outdoor / Indoor Position Switch", positionAreaPass); // 실제 위치 기반 방 소속 변경 결과 출력
            LogResult("F1 Portable Light Debug Page", debugPagePass); // 공통 디버그 목록 등록 결과 출력
            LogResult("Torch Tip Ambient Profile", torchProfilePass); // 횃불 끝부분 주변광 프로필 결과 출력
            LogResult("Lantern Center Long Beam Profile", lanternProfilePass); // 랜턴 정중앙 장거리 빔 프로필 결과 출력
            Debug.Log(success ? "[Project I] PASS - Day 8 휴대 조명·연료·이동형 밝기 검증 완료" : "[Project I] FAIL - Day 8 검증 항목을 확인하세요."); // 전체 결과 출력

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I Day 8", success ? "Day 8 검증 PASS" : "Day 8 검증 FAIL - Console을 확인하세요.", "확인"); // 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateOneHand(PortableLightItem light) // 휴대 조명이 기존 빠른 슬롯 OneHand 아이템인지 검증
        {
            if (light == null || light.WorldItem == null) // 휴대 조명과 WorldItem 참조 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            return light.WorldItem.CarryType == CarryType.OneHand; // 한손 운반 규칙 일치 결과 반환
        }

        private static bool ValidatePortableSource(PortableLightItem light) // 게임용 광원이 Portable 타입인지 검증
        {
            if (light == null || light.BrightnessSource == null) // 휴대 조명과 BrightnessSource 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            return light.BrightnessSource.SourceType == BrightnessSourceType.Portable; // 이동형 광원 종류 일치 결과 반환
        }

        private static bool ValidatePortableAreaSwitch(PortableLightItem light, IndoorBrightnessArea indoorArea) // 같은 휴대 광원이 위치에 따라 외부·내부 소속을 변경하는지 검증
        {
            if (light == null || light.BrightnessSource == null || indoorArea == null || indoorArea.Volume == null) // 검증에 필요한 구성 요소 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            Transform lightTransform = light.transform; // 시험용 횃불 Transform 조회
            Vector3 originalPosition = lightTransform.position; // 검증 후 복원할 기존 외부 위치 저장
            IndoorBrightnessArea outdoorArea = light.BrightnessSource.GetEffectiveArea(); // 현재 OutdoorPlaza 위치에서 유효 방 조회
            Vector3 indoorPosition = indoorArea.Volume.transform.TransformPoint(indoorArea.Volume.center); // 건물 내부 BoxCollider 중심 월드 위치 계산
            lightTransform.position = indoorPosition; // 동일 횃불을 잠시 건물 내부 중심으로 이동
            IndoorBrightnessArea resolvedIndoorArea = light.BrightnessSource.GetEffectiveArea(); // 새 월드 위치 기준 내부 방 조회
            lightTransform.position = originalPosition; // 씬 저장 값에 영향이 없도록 즉시 기존 외부 위치 복원
            bool startedOutdoor = outdoorArea == null; // 원래 시험 위치가 외부였는지 확인
            bool movedIndoor = resolvedIndoorArea == indoorArea; // 내부 이동 후 현재 방으로 정확히 판정되었는지 확인
            return startedOutdoor && movedIndoor; // 두 위치 판정이 모두 올바를 때 성공 반환
        }

        private static bool ValidateTorchProfile(PortableLightItem torch) // 횃불 주변광 중심이 불꽃 끝부분에 위치하는지 검증
        {
            if (torch == null || torch.BrightnessSource == null) // 횃불과 대표 BrightnessSource 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            BrightnessSource source = torch.BrightnessSource; // 횃불 대표 게임용 광원 조회
            Light visualLight = source.VisualLight; // 실제 화면용 횃불 Light 조회

            if (visualLight == null) // 화면 Light 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            bool pointPass = visualLight.type == LightType.Point && source.EmissionShape == BrightnessEmissionShape.Omnidirectional; // 횃불이 모든 방향 Point 광원인지 확인
            bool rangePass = visualLight.range >= 6.5f && visualLight.range <= 7.5f && source.Range >= 6.5f && source.Range <= 7.5f; // 근거리 약 7m 범위인지 확인
            bool tipHeightPass = visualLight.transform.localPosition.y >= 0.48f && visualLight.transform.localPosition.y <= 0.56f; // 횃불 불꽃 끝부분 높이와 광원 중심이 일치하는지 확인
            bool centeredPass = Mathf.Abs(visualLight.transform.localPosition.z) <= 0.05f && Mathf.Abs(visualLight.transform.localPosition.x) <= 0.05f; // 광원이 앞이나 옆으로 밀리지 않고 횃불 중심축에 있는지 확인
            return pointPass && rangePass && tipHeightPass && centeredPass; // 횃불 프로필 전체 결과 반환
        }

        private static bool ValidateLanternProfile(PortableLightItem lantern) // 랜턴이 플레이어 화면 정중앙 장거리 빔 구조인지 검증
        {
            if (lantern == null || lantern.BrightnessSource == null) // 랜턴과 대표 BrightnessSource 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            BrightnessSource source = lantern.BrightnessSource; // 랜턴 주 빔 게임용 광원 조회
            Light visualLight = source.VisualLight; // 실제 화면용 Spot Light 조회
            PortableLightAim aim = lantern.GetComponent<PortableLightAim>(); // 손 운반 중 중앙 조준 기능 조회
            BrightnessSource[] allSources = lantern.GetComponentsInChildren<BrightnessSource>(true); // 주 빔과 주변 보조광 전체 조회

            if (visualLight == null) // 실제 화면용 주 빔 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            bool spotPass = visualLight.type == LightType.Spot && visualLight.range >= 20f && visualLight.spotAngle >= 48f && visualLight.spotAngle <= 56f; // 정면 장거리 Spot Light 기본값 검증
            bool conePass = source.EmissionShape == BrightnessEmissionShape.Cone && source.Range >= 20f && source.ConeAngle >= 48f && source.ConeAngle <= 56f; // 게임 밝기도 같은 방향성 원뿔인지 검증
            bool aimPass = aim != null; // 플레이어 정중앙 조준 보정 컴포넌트 존재 검증
            bool ambientPass = allSources.Length >= 2 && allSources.Any(candidate => candidate != null && candidate != source && candidate.EmissionShape == BrightnessEmissionShape.Omnidirectional && candidate.Range <= 5f); // 랜턴 몸 주변의 약한 보조 Point 밝기 존재 검증
            return spotPass && conePass && aimPass && ambientPass; // 랜턴 프로필 전체 결과 반환
        }

        private static void LogResult(string label, bool passed) // 개별 검증 결과 Console 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 8 {label}"); // PASS 또는 FAIL 문구 출력
        }
    }
}
