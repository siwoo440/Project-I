using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 루트 오브젝트 검색 기능 참조
using ProjectI.Items; // 인벤토리와 월드 아이템 기능 참조
using ProjectI.Player; // 플레이어 입력 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 컴파일 후 Day 6 자동 구성
    public static class Phase2Day6Setup // 빠른 슬롯 6칸과 인벤토리 테스트 구역 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string Day6MarkerName = "===Day6 Ready==="; // Day 6 적용 완료 마커
        private const string MaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 기존 테스트 재질 폴더 경로

        static Phase2Day6Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 컴파일 완료 후 자동 구성 예약
        }

        [MenuItem("Tools/Project I/Day 6/Apply Day 6 Upgrade")] // 수동 Day 6 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 구성 실행
        {
            ApplyDay6(true, true); // 강제 재적용과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 구성 중단
            }

            ApplyDay6(false, false); // 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay6(bool showDialog, bool force) // Day 6 전체 구성 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬이 없으면 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == Day6MarkerName); // 기존 Day 6 마커 조회

            if (!force && existingMarker != null) // 이미 자동 적용된 씬인지 확인
            {
                return; // 반복 적용 방지
            }

            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 공용 테스트 맵 루트 조회

            if (player == null || mapRoot == null) // Day 5 선행 구조 확인
            {
                Debug.LogError("[Project I] Day 6 적용 전에 Player와 Day 3 Test Map 구성이 필요합니다."); // 선행 구조 누락 오류 출력
                return; // Day 6 구성 중단
            }

            UpgradePlayer(player); // 플레이어 빠른 슬롯과 HUD 구성
            UpgradeInventoryTestZone(mapRoot); // 09 인벤토리 시험 구역 구성
            EnsureMarker(scene); // Day 6 완료 마커 생성
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 변경된 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool success = Phase2Day6Validator.Validate(false); // Day 6 전체 구성 검증

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = success ? "Day 6 빠른 슬롯과 인벤토리 구성이 완료되었습니다." : "Day 6 구성 후 검증 실패 - Console을 확인하세요."; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }
        }

        private static void UpgradePlayer(GameObject player) // 플레이어 인벤토리와 빠른 슬롯 HUD 구성
        {
            PlayerInputReader inputReader = GetOrAddComponent<PlayerInputReader>(player); // 기존 입력 래퍼 확보
            PlayerCarryController carryController = GetOrAddComponent<PlayerCarryController>(player); // 기존 CarryPoint 운반 기능 확보
            PlayerInventory inventory = GetOrAddComponent<PlayerInventory>(player); // 빠른 슬롯 6칸 인벤토리 확보
            QuickSlotHud hud = GetOrAddComponent<QuickSlotHud>(player); // 최소 빠른 슬롯 HUD 확보
            Camera playerCamera = player.GetComponentInChildren<Camera>(true); // 플레이어 카메라 조회

            if (playerCamera == null) // 카메라 누락 확인
            {
                Debug.LogError("[Project I] Day 6 Player Camera를 찾을 수 없습니다.", player); // 카메라 누락 오류 출력
                return; // 플레이어 구성 중단
            }

            Transform oneHandCarryPoint = EnsureCarryPoint(playerCamera.transform, "OneHandCarryPoint", new Vector3(0.42f, -0.38f, 0.90f)); // 한손 CarryPoint 확보
            Transform twoHandCarryPoint = EnsureCarryPoint(playerCamera.transform, "TwoHandCarryPoint", new Vector3(0f, -0.48f, 1.05f)); // 양손 CarryPoint 확보
            Transform storageRoot = player.transform.Find("InventoryStorage"); // 기존 숨김 보관 루트 조회

            if (storageRoot == null) // 보관 루트 미생성 확인
            {
                GameObject storageObject = new GameObject("InventoryStorage"); // 새 보관 루트 생성
                storageRoot = storageObject.transform; // 보관 트랜스폼 참조
                storageRoot.SetParent(player.transform, false); // 플레이어 자식으로 연결
                storageRoot.localPosition = Vector3.zero; // 로컬 위치 초기화
                storageRoot.localRotation = Quaternion.identity; // 로컬 회전 초기화
            }

            carryController.Configure(playerCamera.transform, oneHandCarryPoint, twoHandCarryPoint, inputReader); // 기존 운반 기능 참조 재연결
            inventory.Configure(inputReader, carryController, storageRoot); // 인벤토리 입력·운반·보관 루트 연결
            hud.Configure(inventory); // 빠른 슬롯 HUD와 인벤토리 연결
            EditorUtility.SetDirty(carryController); // 운반 기능 변경 저장 대상으로 표시
            EditorUtility.SetDirty(inventory); // 인벤토리 변경 저장 대상으로 표시
            EditorUtility.SetDirty(hud); // HUD 변경 저장 대상으로 표시
        }

        private static Transform EnsureCarryPoint(Transform cameraTransform, string pointName, Vector3 localPosition) // 기존 한손·양손 CarryPoint 확보
        {
            Transform point = cameraTransform.Find(pointName); // 기존 CarryPoint 조회

            if (point == null) // CarryPoint 미생성 확인
            {
                GameObject pointObject = new GameObject(pointName); // 새 CarryPoint 오브젝트 생성
                point = pointObject.transform; // 새 트랜스폼 참조
                point.SetParent(cameraTransform, false); // 카메라 자식으로 연결
            }

            point.localPosition = localPosition; // 화면 기준 운반 위치 적용
            point.localRotation = Quaternion.identity; // 카메라 기준 기본 회전 적용
            return point; // 확보한 CarryPoint 반환
        }

        private static void UpgradeInventoryTestZone(GameObject mapRoot) // 09 인벤토리 시험 구역 생성
        {
            Transform existingZone = mapRoot.transform.Find("09_InventoryTest"); // 기존 Day 6 시험 구역 조회

            if (existingZone != null) // 기존 구역 존재 확인
            {
                Object.DestroyImmediate(existingZone.gameObject); // 중복 방지를 위해 기존 구역 삭제
            }

            Material floorMaterial = LoadMaterial("Test_Blue"); // 시험 바닥 재질 조회
            Material metalMaterial = LoadMaterial("Test_Metal"); // 검·도구 재질 조회
            Material yellowMaterial = LoadMaterial("Test_Yellow"); // 열쇠·회복 아이템 재질 조회
            Material orangeMaterial = LoadMaterial("Test_Orange"); // 손전등·곡괭이 재질 조회
            GameObject zone = new GameObject("09_InventoryTest"); // Day 6 시험 구역 루트 생성
            zone.transform.SetParent(mapRoot.transform); // 공용 테스트 맵 아래 배치
            CreatePrimitive(zone.transform, "InventoryFloor", PrimitiveType.Cube, new Vector3(-10f, -0.05f, -36f), new Vector3(18f, 0.1f, 8f), floorMaterial, true); // 기존 08 아래쪽의 독립 바닥 생성
            CreateWorldItem(zone.transform, "InventoryItem_01_Sword", PrimitiveType.Cube, new Vector3(-17f, 0.10f, -35f), new Vector3(0.12f, 0.08f, 1.10f), metalMaterial, "검", CarryType.OneHand, "검 사용"); // 1번째 한손 시험 아이템 생성
            CreateWorldItem(zone.transform, "InventoryItem_02_Flashlight", PrimitiveType.Cylinder, new Vector3(-14.5f, 0.25f, -35f), new Vector3(0.09f, 0.25f, 0.09f), orangeMaterial, "손전등", CarryType.OneHand, "손전등 사용"); // 2번째 한손 시험 아이템 생성
            CreateWorldItem(zone.transform, "InventoryItem_03_Key", PrimitiveType.Cube, new Vector3(-12f, 0.10f, -35f), new Vector3(0.10f, 0.04f, 0.45f), yellowMaterial, "열쇠", CarryType.OneHand, "열쇠 사용"); // 3번째 한손 시험 아이템 생성
            CreateWorldItem(zone.transform, "InventoryItem_04_Tool", PrimitiveType.Cube, new Vector3(-9.5f, 0.12f, -35f), new Vector3(0.12f, 0.12f, 0.55f), metalMaterial, "작은 도구", CarryType.OneHand, "도구 사용"); // 4번째 한손 시험 아이템 생성
            CreateWorldItem(zone.transform, "InventoryItem_05_Medicine", PrimitiveType.Cube, new Vector3(-7f, 0.15f, -35f), new Vector3(0.22f, 0.28f, 0.12f), yellowMaterial, "회복 아이템", CarryType.OneHand, "회복 아이템 사용"); // 5번째 한손 시험 아이템 생성
            CreateWorldItem(zone.transform, "InventoryItem_06_Crystal", PrimitiveType.Sphere, new Vector3(-4.5f, 0.18f, -35f), new Vector3(0.28f, 0.28f, 0.28f), orangeMaterial, "작은 회수품", CarryType.OneHand, "회수품 확인"); // 6번째 한손 시험 아이템 생성
            CreateWorldItem(zone.transform, "InventoryItem_07_Pickaxe", PrimitiveType.Cylinder, new Vector3(-10f, 0.55f, -38f), new Vector3(0.10f, 0.55f, 0.10f), metalMaterial, "곡괭이", CarryType.TwoHand, "곡괭이 사용"); // 양손 잠금 시험 아이템 생성
        }

        private static GameObject CreateWorldItem(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, string displayName, CarryType carryType, string useLabel) // 빠른 슬롯 시험 아이템 생성
        {
            GameObject item = CreatePrimitive(parent, objectName, primitiveType, position, scale, material, true); // 기본 Primitive와 Collider 생성
            Rigidbody body = item.AddComponent<Rigidbody>(); // 월드 물리 Rigidbody 추가
            body.mass = 1.2f; // 작은 도구 기본 질량 설정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 월드 물리 이동 보간 활성화
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 빠른 이동 충돌 안정성 향상
            WorldItem worldItem = item.AddComponent<WorldItem>(); // 월드 아이템 기능 추가
            float radius = Mathf.Clamp(Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) * 0.75f, 0.08f, 0.22f); // 작은 도구 두께 기준 공간 검사 반지름 계산
            worldItem.Configure(displayName, radius, carryType); // 이름·크기·한손/양손 규칙 설정
            TestUsableItem usableItem = item.AddComponent<TestUsableItem>(); // 좌클릭 Use 시험 기능 추가
            usableItem.Configure(useLabel); // Console 사용 시험 이름 설정
            return item; // 생성한 시험 아이템 반환
        }

        private static GameObject CreatePrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 테스트 Primitive 생성
        {
            GameObject target = GameObject.CreatePrimitive(primitiveType); // 기본 Primitive 생성
            target.name = objectName; // 오브젝트 이름 지정
            target.transform.SetParent(parent); // 시험 구역 아래 배치
            target.transform.position = position; // 월드 위치 지정
            target.transform.localScale = scale; // 크기 지정
            Renderer renderer = target.GetComponent<Renderer>(); // Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 기존 테스트 재질 적용
            }

            if (!keepCollider) // Collider가 필요 없는 표식인지 확인
            {
                Collider collider = target.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 여부 확인
                {
                    Object.DestroyImmediate(collider); // 불필요 Collider 삭제
                }
            }

            return target; // 생성한 Primitive 반환
        }

        private static Material LoadMaterial(string materialName) // 기존 테스트 재질 조회
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 전체 경로 생성
            return AssetDatabase.LoadAssetAtPath<Material>(path); // 재질 에셋 반환
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 컴포넌트 확보 헬퍼
        {
            T component = target.GetComponent<T>(); // 기존 컴포넌트 조회
            return component != null ? component : target.AddComponent<T>(); // 기존 또는 새 컴포넌트 반환
        }

        private static void EnsureMarker(Scene scene) // Day 6 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == Day6MarkerName); // 기존 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(Day6MarkerName); // 새 Day 6 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 마커 숨김
            }
        }
    }
}
