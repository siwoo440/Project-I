using System.IO; // 씬 파일 상태 확인 기능 참조
using System.Linq; // 목록 검색 기능 참조
using ProjectI.Player; // 플레이어 기능 참조
using ProjectI.World; // 월드 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 자동 업그레이드 등록
    public static class Phase2Day4Setup // 4일차 플레이어와 테스트 맵 자동 업그레이드
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 기존 공용 테스트 맵 루트 이름
        private const string Day4MarkerName = "===Day4 Ready==="; // 4일차 적용 완료 마커 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 테스트 재질 폴더 경로

        static Phase2Day4Setup() // 자동 설정 생성자
        {
            EditorApplication.delayCall += TryAutoUpgrade; // 스크립트 컴파일 후 자동 업그레이드 예약
        }

        private static void TryAutoUpgrade() // 자동 업그레이드 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 업그레이드 중단
            }

            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 확인
            {
                return; // 대상 씬이 없으면 중단
            }

            string sceneText = File.ReadAllText(ExplorationOfficeScenePath); // 현재 씬 YAML 읽기

            if (sceneText.Contains(Day4MarkerName)) // 4일차 업그레이드 적용 여부 확인
            {
                return; // 이미 적용된 씬 유지
            }

            ApplyDay4Upgrade(false); // 첫 실행 자동 업그레이드
        }

        [MenuItem("Tools/Project I/Day 4/Apply Day 4 Upgrade")] // 수동 업그레이드 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 업그레이드 실행
        {
            ApplyDay4Upgrade(true); // 대화상자 포함 업그레이드 실행
        }

        private static void ApplyDay4Upgrade(bool showDialog) // 4일차 전체 업그레이드
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 테스트 맵 루트 조회

            if (player == null || mapRoot == null) // 3일차 기본 구조 누락 확인
            {
                Debug.LogError("[Project I] Day 4 적용 전에 Day 3 Player/Test Map 구성이 필요합니다."); // 선행 구성 누락 오류 출력

                if (showDialog) // 수동 실행 여부 확인
                {
                    EditorUtility.DisplayDialog("Project I", "Day 3 Player/Test Map 구성을 먼저 확인하세요.", "확인"); // 선행 구성 안내 출력
                }

                return; // 업그레이드 중단
            }

            UpgradePlayer(player); // 플레이어 이동·체력·웅크리기 구성 확장
            UpgradeTestMap(mapRoot); // 테스트 맵 4일차 구역 확장
            EnsureDay4Marker(scene); // 4일차 완료 마커 생성
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool success = Phase2Day4Validator.Validate(false); // 4일차 구성 검증

            if (showDialog) // 수동 실행 결과 대화상자 여부 확인
            {
                string message = success ? "Day 4 플레이어 확장과 테스트 맵 구성이 완료되었습니다." : "Day 4 구성 후 검증 실패 - Console을 확인하세요."; // 결과 메시지 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }
        }

        private static void UpgradePlayer(GameObject player) // 플레이어 4일차 기능 추가
        {
            CharacterController controller = GetOrAddComponent<CharacterController>(player); // 캐릭터 컨트롤러 확보
            PlayerHealth health = GetOrAddComponent<PlayerHealth>(player); // 체력 컴포넌트 확보
            PlayerCrouch crouch = GetOrAddComponent<PlayerCrouch>(player); // 웅크리기 컴포넌트 확보
            PlayerFallDamage fallDamage = GetOrAddComponent<PlayerFallDamage>(player); // 추락 피해 컴포넌트 확보
            GetOrAddComponent<PlayerDebugHud>(player); // 4일차 디버그 HUD 확보
            Camera playerCamera = player.GetComponentInChildren<Camera>(true); // 플레이어 카메라 조회
            controller.height = 1.8f; // 서 있는 캐릭터 높이 설정
            controller.center = new Vector3(0f, 0.9f, 0f); // 발 기준 캐릭터 중심 설정
            controller.radius = 0.35f; // 캐릭터 반지름 설정
            controller.stepOffset = 0.3f; // 계단 허용 높이 설정
            controller.slopeLimit = 50f; // 오를 수 있는 경사 제한 설정
            controller.skinWidth = 0.035f; // 캐릭터 반지름의 약 10% 스킨 너비 설정

            if (playerCamera != null) // 카메라 존재 확인
            {
                crouch.Configure(playerCamera.transform); // 웅크리기 카메라 기준 연결
            }

            EditorUtility.SetDirty(controller); // 컨트롤러 변경 저장 대상으로 표시
            EditorUtility.SetDirty(health); // 체력 컴포넌트 저장 대상으로 표시
            EditorUtility.SetDirty(crouch); // 웅크리기 컴포넌트 저장 대상으로 표시
            EditorUtility.SetDirty(fallDamage); // 추락 피해 컴포넌트 저장 대상으로 표시
        }

        private static void UpgradeTestMap(GameObject mapRoot) // 테스트 맵 구역 확장
        {
            Material wallMaterial = LoadMaterial("Test_Wall"); // 벽 테스트 재질 조회
            Material yellowMaterial = LoadMaterial("Test_Yellow"); // 웅크리기 구역 재질 조회
            Material redMaterial = LoadMaterial("Test_Red"); // 추락 위험 구역 재질 조회
            Material blueMaterial = LoadMaterial("Test_Blue"); // 이동 플랫폼 구역 재질 조회
            Material metalMaterial = LoadMaterial("Test_Metal"); // 금속 테스트 재질 조회
            Transform oldCrouch = mapRoot.transform.Find("05_CrouchGate_Future"); // 기존 웅크리기 예비 구역 조회
            Transform oldFall = mapRoot.transform.Find("06_FallTest_Future"); // 기존 추락 예비 구역 조회
            Transform currentCrouch = mapRoot.transform.Find("05_CrouchTest"); // 현재 웅크리기 구역 조회
            Transform currentFall = mapRoot.transform.Find("06_FallTest"); // 현재 추락 구역 조회
            Transform currentPlatform = mapRoot.transform.Find("07_MovingPlatformTest"); // 현재 이동 플랫폼 구역 조회

            DestroyIfExists(oldCrouch); // 기존 웅크리기 예비 구역 삭제
            DestroyIfExists(oldFall); // 기존 추락 예비 구역 삭제
            DestroyIfExists(currentCrouch); // 기존 4일차 웅크리기 구역 삭제
            DestroyIfExists(currentFall); // 기존 4일차 추락 구역 삭제
            DestroyIfExists(currentPlatform); // 기존 4일차 이동 플랫폼 구역 삭제
            CreateCrouchTest(mapRoot.transform, yellowMaterial, wallMaterial); // 웅크리기와 천장 검사 구역 생성
            CreateFallTest(mapRoot.transform, redMaterial, metalMaterial); // 추락 거리와 피해 시험 구역 생성
            CreateMovingPlatformTest(mapRoot.transform, blueMaterial, metalMaterial); // 이동 플랫폼 시험 구역 생성
            UpgradeStairRamp(mapRoot.transform, redMaterial, wallMaterial); // 기존 계단·경사 구역에 급경사 시험 추가
        }

        private static void CreateCrouchTest(Transform parent, Material zoneMaterial, Material wallMaterial) // 웅크리기 시험 구역 생성
        {
            GameObject zone = new GameObject("05_CrouchTest"); // 웅크리기 구역 루트 생성
            zone.transform.SetParent(parent); // 테스트 맵 아래에 구역 배치
            CreateBox(zone.transform, "CrouchFloor", new Vector3(19f, 0.03f, -19f), new Vector3(14f, 0.06f, 6f), zoneMaterial, false); // 구역 바닥 표식 생성
            CreateBox(zone.transform, "TunnelWall_North", new Vector3(19f, 1.2f, -21f), new Vector3(10f, 2.4f, 0.35f), wallMaterial, true); // 통로 북쪽 벽 생성
            CreateBox(zone.transform, "TunnelWall_South", new Vector3(19f, 1.2f, -17f), new Vector3(10f, 2.4f, 0.35f), wallMaterial, true); // 통로 남쪽 벽 생성
            CreateBox(zone.transform, "LowCeiling", new Vector3(19f, 1.55f, -19f), new Vector3(8f, 0.5f, 3.5f), wallMaterial, true); // 웅크려야 통과 가능한 낮은 천장 생성
            CreateBox(zone.transform, "StandCheckCeiling", new Vector3(23.5f, 1.6f, -19f), new Vector3(1.5f, 0.4f, 3.5f), wallMaterial, true); // 천장 아래 일어서기 차단 시험 구역 생성
        }

        private static void CreateFallTest(Transform parent, Material zoneMaterial, Material metalMaterial) // 추락 피해 시험 구역 생성
        {
            GameObject zone = new GameObject("06_FallTest"); // 추락 구역 루트 생성
            zone.transform.SetParent(parent); // 테스트 맵 아래에 구역 배치
            CreateBox(zone.transform, "FallLandingFloor", new Vector3(31f, 0.03f, -15f), new Vector3(16f, 0.06f, 26f), zoneMaterial, false); // 추락 착지 바닥 표식 생성
            CreateFallStaircase(zone.transform, new Vector3(34f, 0f, -25f), metalMaterial); // 고도별 추락용 계단 생성
            CreateBox(zone.transform, "SafeDrop_2m", new Vector3(30.5f, 1.9f, -21.4f), new Vector3(7f, 0.2f, 2.5f), metalMaterial, true); // 안전 추락 높이 발판 생성
            CreateBox(zone.transform, "DamageDrop_4m", new Vector3(30.5f, 3.9f, -17.8f), new Vector3(7f, 0.2f, 2.5f), metalMaterial, true); // 피해 발생 추락 발판 생성
            CreateBox(zone.transform, "HighDrop_7m", new Vector3(30.5f, 6.9f, -12.4f), new Vector3(7f, 0.2f, 2.5f), metalMaterial, true); // 고위험 추락 발판 생성
        }

        private static void CreateFallStaircase(Transform parent, Vector3 origin, Material material) // 추락 발판 접근용 계단 생성
        {
            const int stepCount = 28; // 총 계단 수 지정
            const float stepHeight = 0.25f; // 한 계단 높이 지정
            const float stepDepth = 0.45f; // 한 계단 전진 거리 지정

            for (int index = 0; index < stepCount; index++) // 계단 반복 생성
            {
                float height = (index + 1) * stepHeight; // 현재 계단 누적 높이 계산
                float zPosition = origin.z + (index * stepDepth); // 현재 계단 Z 위치 계산
                CreateBox(parent, $"FallStair_{index + 1:00}", new Vector3(origin.x, height * 0.5f, zPosition), new Vector3(3f, height, 0.5f), material, true); // 누적 블록 방식 계단 생성
            }
        }

        private static void CreateMovingPlatformTest(Transform parent, Material zoneMaterial, Material metalMaterial) // 이동 플랫폼 시험 구역 생성
        {
            GameObject zone = new GameObject("07_MovingPlatformTest"); // 이동 플랫폼 구역 루트 생성
            zone.transform.SetParent(parent); // 테스트 맵 아래에 구역 배치
            CreateBox(zone.transform, "PlatformLane", new Vector3(0f, 0.03f, 22f), new Vector3(22f, 0.06f, 8f), zoneMaterial, false); // 플랫폼 이동 구역 바닥 표식 생성
            CreateBox(zone.transform, "StartDock", new Vector3(-10f, 0.5f, 22f), new Vector3(3f, 1f, 6f), metalMaterial, true); // 시작 승강장 생성
            CreateBox(zone.transform, "EndDock", new Vector3(10f, 0.5f, 22f), new Vector3(3f, 1f, 6f), metalMaterial, true); // 도착 승강장 생성
            GameObject platformRoot = new GameObject("MovingPlatform"); // 이동 플랫폼 루트 생성
            platformRoot.transform.SetParent(zone.transform); // 이동 플랫폼 구역 아래에 배치
            platformRoot.transform.position = new Vector3(-7f, 1.1f, 22f); // 이동 플랫폼 시작 위치 설정
            Rigidbody body = platformRoot.AddComponent<Rigidbody>(); // 키네마틱 플랫폼 리지드바디 추가
            body.isKinematic = true; // 외부 힘 영향 차단
            body.useGravity = false; // 중력 영향 차단
            MovingPlatform movingPlatform = platformRoot.AddComponent<MovingPlatform>(); // 왕복 이동 컴포넌트 추가
            movingPlatform.Configure(new Vector3(14f, 0f, 0f), 2.2f, 0.5f); // 플랫폼 왕복 거리와 속도 설정
            CreateBox(platformRoot.transform, "PlatformBody", Vector3.zero, new Vector3(4f, 0.4f, 4f), metalMaterial, true); // 실제 탑승 플랫폼 본체 생성
            GameObject passengerTrigger = new GameObject("PassengerTrigger"); // 플레이어 탑승 감지 오브젝트 생성
            passengerTrigger.transform.SetParent(platformRoot.transform); // 플랫폼 루트 아래에 트리거 배치
            passengerTrigger.transform.localPosition = new Vector3(0f, 0.8f, 0f); // 플랫폼 위쪽에 탑승 감지 영역 배치
            BoxCollider triggerCollider = passengerTrigger.AddComponent<BoxCollider>(); // 탑승 감지 박스 콜라이더 추가
            triggerCollider.isTrigger = true; // 충돌 대신 트리거로 설정
            triggerCollider.size = new Vector3(3.8f, 1.2f, 3.8f); // 플랫폼 상단 탑승 감지 크기 설정
            MovingPlatformPassengerTrigger passenger = passengerTrigger.AddComponent<MovingPlatformPassengerTrigger>(); // 탑승자 연결 컴포넌트 추가
            passenger.Configure(movingPlatform); // 실제 이동 플랫폼 기능 연결
        }

        private static void UpgradeStairRamp(Transform parent, Material dangerMaterial, Material wallMaterial) // 기존 계단·경사 구역 보강
        {
            Transform zone = parent.Find("04_StairRamp"); // 기존 계단·경사 시험 구역 조회

            if (zone == null) // 기존 구역 누락 확인
            {
                return; // 보강 처리 중단
            }

            Transform oldSteepRamp = zone.Find("SteepRamp_Day4"); // 기존 급경사 시험 오브젝트 조회
            DestroyIfExists(oldSteepRamp); // 중복 급경사 시험 오브젝트 삭제
            GameObject steepRamp = CreateBox(zone, "SteepRamp_Day4", new Vector3(31f, 1.6f, 9f), new Vector3(5f, 0.5f, 8f), wallMaterial, true); // 급경사 시험 경사로 생성
            steepRamp.transform.rotation = Quaternion.Euler(-58f, 0f, 0f); // Slope Limit보다 큰 경사 적용
            CreateBox(zone, "SteepRampWarning", new Vector3(31f, 0.03f, 4.5f), new Vector3(6f, 0.06f, 3f), dangerMaterial, false); // 급경사 위험 구역 바닥 표식 생성
        }

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 테스트용 박스 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 큐브 오브젝트 생성
            box.name = objectName; // 오브젝트 이름 지정
            box.transform.SetParent(parent); // 부모 트랜스폼 지정
            box.transform.position = position; // 월드 위치 지정
            box.transform.localScale = scale; // 크기 지정
            Renderer renderer = box.GetComponent<Renderer>(); // 렌더러 참조 획득

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 테스트 재질 적용
            }

            if (!keepCollider) // 충돌이 필요 없는 표식 확인
            {
                Collider collider = box.GetComponent<Collider>(); // 기본 콜라이더 조회

                if (collider != null) // 콜라이더 존재 확인
                {
                    Object.DestroyImmediate(collider); // 표식용 콜라이더 제거
                }
            }

            return box; // 생성한 오브젝트 반환
        }

        private static Material LoadMaterial(string materialName) // 기존 Day 3 테스트 재질 조회
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 테스트 재질 전체 경로 생성
            return AssetDatabase.LoadAssetAtPath<Material>(path); // 재질 에셋 반환
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 컴포넌트 확보 헬퍼
        {
            T component = target.GetComponent<T>(); // 기존 컴포넌트 조회
            return component != null ? component : target.AddComponent<T>(); // 기존 또는 새 컴포넌트 반환
        }

        private static void DestroyIfExists(Transform target) // 기존 자동 생성 오브젝트 삭제 헬퍼
        {
            if (target != null) // 삭제 대상 존재 확인
            {
                Object.DestroyImmediate(target.gameObject); // 대상 오브젝트 즉시 삭제
            }
        }

        private static void EnsureDay4Marker(Scene scene) // 4일차 완료 마커 보장
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == Day4MarkerName); // 기존 완료 마커 조회

            if (marker == null) // 완료 마커 누락 확인
            {
                marker = new GameObject(Day4MarkerName); // 새로운 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서는 마커 숨김
            }
        }
    }
}
