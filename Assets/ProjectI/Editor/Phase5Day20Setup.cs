using System.IO; // 대상 씬과 에셋 폴더 확인 기능 참조
using System.Linq; // 씬 루트 이름 검색 기능 참조
using ProjectI.Items; // 기존 WorldItem·PlayerInventory·CarryType 참조
using ProjectI.Player; // 기존 플레이어 입력 기능 참조
using UnityEditor; // 유니티 에디터 메뉴와 에셋 정리 기능 참조
using UnityEditor.SceneManagement; // 씬 열기와 저장 기능 참조
using UnityEngine; // 유니티 오브젝트·재질·물리 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day20 기존 아이템 방식 자동 구성
    public static class Phase5Day20Setup // 별도 회수품 시스템 없이 기존 빠른 슬롯 아이템 구조로 시험품 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string RootName = "===Day20 Existing Item Test==="; // 새 Day20 기존 아이템 시험장 루트 이름
        private const string ReadyMarkerName = "===Day20 Existing Item Ready v2==="; // 새 Day20 자동 적용 완료 마커 이름
        private const string LegacyRootName = "===Day20 Item Recoverable System==="; // 이전 Day20 별도 회수품 시험장 루트 이름
        private const string LegacyReadyMarkerName = "===Day20 Item Recoverable Ready==="; // 이전 Day20 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day20"; // Day20 테스트 재질 생성 폴더
        private static readonly Vector3 TestAreaCenter = new Vector3(20f, 0f, 20f); // 다른 시험장과 분리한 Day20 테스트 중심
        private static readonly string[] LegacySourcePaths = // 이전 Day20 전용 런타임 코드 삭제 대상 목록
        {
            "Assets/ProjectI/Scripts/Items/ItemCategory.cs", // 이전 공통 아이템 분류 코드 경로
            "Assets/ProjectI/Scripts/Items/ItemData.cs", // 이전 공통 ItemData 코드 경로
            "Assets/ProjectI/Scripts/Items/RecoverableData.cs", // 이전 회수품 데이터 코드 경로
            "Assets/ProjectI/Scripts/Items/RecoverableInstance.cs", // 이전 회수품 개체 코드 경로
            "Assets/ProjectI/Scripts/Items/RecoverableSpawnPoint.cs", // 이전 회수품 생성 코드 경로
            "Assets/ProjectI/Scripts/Items/PlayerRecoverableCarrier.cs", // 이전 빠른 슬롯 외 직접 운반 코드 경로
            "Assets/ProjectI/Scripts/Diagnostics/RecoverableDebugPage.cs" // 이전 회수품 전용 F1 페이지 코드 경로
        }; // 이전 Day20 전용 소스 삭제 대상 배열 정의 완료

        static Phase5Day20Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 뒤 자동 적용 예약
        }

        [MenuItem("Tools/Project I/Day 20/Apply Existing Item Carry Fix")] // 수동 Day20 기존 아이템 방식 재구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day20 재구성 실행
        {
            ApplyDay20(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 구성 제외 상태에서는 중단
            }

            ApplyDay20(false, false); // 새 완료 마커가 없으면 자동 적용
        }

        private static void ApplyDay20(bool showDialog, bool force) // 기존 빠른 슬롯 구조를 사용한 Day20 전체 재구성
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 대상 씬 누락 시 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            GameObject existingMarker = FindRoot(scene, ReadyMarkerName); // 새 Day20 완료 마커 조회

            if (!force && existingMarker != null) // 이미 새 방식이 적용됐는지 확인
            {
                EditorApplication.delayCall += DeleteLegacySourceFiles; // 남아 있는 이전 Day20 전용 코드 정리를 다음 에디터 틱으로 예약
                return; // 시험장 중복 생성 방지
            }

            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 기존 싱글 플레이어 입력 래퍼 조회
            GameObject player = inputReader == null ? null : inputReader.gameObject; // 기존 플레이어 루트 조회

            if (player == null) // 플레이어 존재 여부 확인
            {
                Debug.LogError("[Project I] Day20 수정 전에 기존 Player가 필요합니다."); // 선행 플레이어 누락 오류 출력
                return; // Day20 재구성 중단
            }

            PlayerInventory inventory = player.GetComponent<PlayerInventory>(); // 기존 빠른 슬롯 인벤토리 조회
            PlayerCarryController carryController = player.GetComponent<PlayerCarryController>(); // 기존 화면 운반 제어기 조회
            Camera playerCamera = player.GetComponentInChildren<Camera>(true); // 기존 1인칭 카메라 조회

            if (inventory == null || carryController == null || playerCamera == null) // 기존 Day5~6 핵심 구조 존재 여부 확인
            {
                Debug.LogError("[Project I] Day20 수정 전에 PlayerInventory·PlayerCarryController·Player Camera가 필요합니다."); // 선행 아이템 구조 누락 오류 출력
                return; // Day20 재구성 중단
            }

            RemoveRoot(scene, LegacyRootName); // 이전 별도 회수품 시험장 제거
            RemoveRoot(scene, LegacyReadyMarkerName); // 이전 별도 회수품 완료 마커 제거
            RemoveRoot(scene, RootName); // 기존 새 시험장 제거
            RemoveRoot(scene, ReadyMarkerName); // 기존 새 완료 마커 제거
            RemoveLegacyCarrierComponents(player); // 이전 PlayerRecoverableCarrier 컴포넌트 제거
            RemoveChildIfExists(playerCamera.transform, "RecoverableOneHandCarryPoint"); // 이전 왼손 회수품 CarryPoint 제거
            RemoveChildIfExists(playerCamera.transform, "RecoverableTwoHandCarryPoint"); // 이전 회수품 양손 CarryPoint 제거
            RemoveMissingScriptsRecursive(player.transform); // 전용 코드 삭제 뒤 남을 수 있는 Missing Script 제거
            Transform oneHandPoint = GetOrCreateCarryPoint(playerCamera.transform, "OneHandCarryPoint", new Vector3(0.42f, -0.38f, 0.90f)); // 기존 한손 지점을 화면 오른쪽으로 복구
            Transform twoHandPoint = GetOrCreateCarryPoint(playerCamera.transform, "TwoHandCarryPoint", new Vector3(0f, -0.48f, 1.05f)); // 기존 양손 지점을 화면 중앙으로 복구
            carryController.Configure(playerCamera.transform, oneHandPoint, twoHandPoint, inputReader); // 기존 PlayerCarryController에 기존 CarryPoint 다시 연결
            DeleteAssetIfExists("Assets/ProjectI/Resources/Items/Recoverables"); // 이전 Day20 전용 ScriptableObject 폴더 제거
            DeleteAssetIfExists(MaterialFolder); // 이전 Day20 생성 재질 폴더 제거
            EnsureAssetFolder(MaterialFolder); // 새 단순 시험 재질 폴더 생성
            Material floorMaterial = GetOrCreateMaterial("ItemTest_Floor", new Color(0.07f, 0.08f, 0.09f), 0.12f, 0.42f); // 시험장 바닥 재질 생성
            Material silverMaterial = GetOrCreateMaterial("ItemTest_Silver", new Color(0.55f, 0.58f, 0.62f), 0.72f, 0.62f); // 은 동전 재질 생성
            Material brassMaterial = GetOrCreateMaterial("ItemTest_Brass", new Color(0.46f, 0.28f, 0.08f), 0.60f, 0.52f); // 금속 장식 재질 생성
            Material goldMaterial = GetOrCreateMaterial("ItemTest_Gold", new Color(0.72f, 0.45f, 0.05f), 0.74f, 0.66f); // 왕관 재질 생성
            Material stoneMaterial = GetOrCreateMaterial("ItemTest_Stone", new Color(0.36f, 0.38f, 0.40f), 0.08f, 0.48f); // 조각상 재질 생성
            GameObject root = new GameObject(RootName); // Day20 기존 아이템 시험장 루트 생성
            CreateBoxVisual(root.transform, "ItemTest_Floor", TestAreaCenter + new Vector3(0f, 0.035f, 0f), new Vector3(12f, 0.07f, 5.5f), floorMaterial); // 시험 구역 바닥 표식 생성
            CreateTreasureItem(root.transform, "Day20_SilverCoin", "은 동전", TestAreaCenter + new Vector3(-3.6f, 0.45f, 0f), CarryType.OneHand, 0.18f, 1f, silverMaterial, TreasureVisual.Coin); // 기존 빠른 슬롯을 사용하는 한손 은 동전 생성
            CreateTreasureItem(root.transform, "Day20_MetalOrnament", "장인의 금속 장식", TestAreaCenter + new Vector3(-1.2f, 0.45f, 0f), CarryType.OneHand, 0.24f, 3f, brassMaterial, TreasureVisual.Ornament); // 기존 빠른 슬롯을 사용하는 한손 금속 장식 생성
            CreateTreasureItem(root.transform, "Day20_Crown", "왕관", TestAreaCenter + new Vector3(1.2f, 0.55f, 0f), CarryType.TwoHand, 0.42f, 4f, goldMaterial, TreasureVisual.Crown); // 기존 빠른 슬롯을 사용하는 양손 왕관 생성
            CreateTreasureItem(root.transform, "Day20_GodsStatue", "신들의 조각상", TestAreaCenter + new Vector3(3.6f, 0.80f, 0f), CarryType.TwoHand, 0.45f, 6f, stoneMaterial, TreasureVisual.Statue); // 기존 빠른 슬롯을 사용하는 양손 조각상 생성
            GameObject marker = new GameObject(ReadyMarkerName); // 새 Day20 완료 마커 생성
            EditorUtility.SetDirty(carryController); // 기존 운반 제어기 변경 저장 대상으로 표시
            EditorUtility.SetDirty(marker); // 완료 마커 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 플레이어와 시험장 변경 저장
            AssetDatabase.SaveAssets(); // 생성 재질 저장
            bool validationPassed = Phase5Day20Validator.Validate(false); // 기존 아이템 방식 정적 검증 실행
            EditorApplication.delayCall += DeleteLegacySourceFiles; // 검증 완료 뒤 이전 Day20 전용 런타임 코드 삭제 예약

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day20이 기존 빠른 슬롯 아이템 방식으로 복구되었습니다." : "Day20 검증 실패 - Console을 확인하세요."; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 출력
            }
        }

        private static void CreateTreasureItem(Transform parent, string objectName, string displayName, Vector3 position, CarryType carryType, float carryRadius, float mass, Material material, TreasureVisual visualType) // 기존 WorldItem 기반 테스트 보물 생성
        {
            GameObject root = new GameObject(objectName); // 보물 기능 루트 생성
            root.transform.SetParent(parent); // Day20 시험장 아래 배치
            root.transform.position = position; // 보물 월드 위치 지정
            Rigidbody body = root.AddComponent<Rigidbody>(); // 기존 WorldItem 필수 Rigidbody 추가
            body.mass = Mathf.Max(0.1f, mass); // 테스트 보물 무게 적용
            body.interpolation = RigidbodyInterpolation.Interpolate; // 물리 이동 보간 활성화
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 낙하 중 관통 완화
            BoxCollider collider = root.AddComponent<BoxCollider>(); // 공통 루트 충돌체 추가
            ConfigureCollider(collider, visualType); // 보물 형상에 맞는 충돌 영역 지정
            WorldItem worldItem = root.AddComponent<WorldItem>(); // 기존 빠른 슬롯 월드 아이템 기능 추가
            worldItem.Configure(displayName, carryRadius, carryType); // 기존 이름·반지름·한손/양손 규칙만 사용
            worldItem.ConfigureCarryPose(carryType, Vector3.zero, Vector3.zero); // 기존 OneHand/TwoHand CarryPoint를 그대로 사용
            CreateTreasureVisual(root.transform, visualType, material); // 프리미티브 기반 시험 외형 생성
            EditorUtility.SetDirty(worldItem); // WorldItem 설정 저장 대상으로 표시
        }

        private static void ConfigureCollider(BoxCollider collider, TreasureVisual visualType) // 테스트 보물별 충돌 크기 지정
        {
            if (visualType == TreasureVisual.Coin) // 은 동전 충돌 크기 지정
            {
                collider.center = new Vector3(0f, 0.10f, 0f); // 은 동전 충돌 중심 지정
                collider.size = new Vector3(0.38f, 0.18f, 0.38f); // 은 동전 충돌 범위 지정
                return; // 은 동전 설정 종료
            }

            if (visualType == TreasureVisual.Ornament) // 금속 장식 충돌 크기 지정
            {
                collider.center = new Vector3(0f, 0.24f, 0f); // 금속 장식 충돌 중심 지정
                collider.size = new Vector3(0.52f, 0.48f, 0.34f); // 금속 장식 충돌 범위 지정
                return; // 금속 장식 설정 종료
            }

            if (visualType == TreasureVisual.Crown) // 왕관 충돌 크기 지정
            {
                collider.center = new Vector3(0f, 0.31f, 0f); // 왕관 충돌 중심 지정
                collider.size = new Vector3(0.90f, 0.62f, 0.74f); // 왕관 충돌 범위 지정
                return; // 왕관 설정 종료
            }

            collider.center = new Vector3(0f, 0.75f, 0f); // 조각상 충돌 중심 지정
            collider.size = new Vector3(0.74f, 1.50f, 0.62f); // 조각상 충돌 범위 지정
        }

        private static void CreateTreasureVisual(Transform parent, TreasureVisual visualType, Material material) // 테스트 보물 프리미티브 외형 생성
        {
            if (visualType == TreasureVisual.Coin) // 은 동전 외형 생성
            {
                CreatePrimitivePart(parent, PrimitiveType.Cylinder, "SilverCoin", new Vector3(0f, 0.10f, 0f), new Vector3(0.34f, 0.05f, 0.34f), new Vector3(90f, 0f, 0f), material); // 납작한 은 동전 생성
                return; // 은 동전 외형 생성 종료
            }

            if (visualType == TreasureVisual.Ornament) // 금속 장식 외형 생성
            {
                CreatePrimitivePart(parent, PrimitiveType.Cube, "MetalOrnamentBase", new Vector3(0f, 0.16f, 0f), new Vector3(0.46f, 0.22f, 0.30f), Vector3.zero, material); // 금속 장식 받침 생성
                CreatePrimitivePart(parent, PrimitiveType.Sphere, "MetalOrnamentCore", new Vector3(0f, 0.38f, 0f), new Vector3(0.28f, 0.28f, 0.20f), Vector3.zero, material); // 금속 장식 핵심 생성
                return; // 금속 장식 외형 생성 종료
            }

            if (visualType == TreasureVisual.Crown) // 왕관 외형 생성
            {
                CreatePrimitivePart(parent, PrimitiveType.Cylinder, "CrownBase", new Vector3(0f, 0.20f, 0f), new Vector3(0.70f, 0.14f, 0.70f), Vector3.zero, material); // 왕관 하단 링 생성

                for (int index = 0; index < 5; index++) // 왕관 장식 다섯 개 순회
                {
                    float angle = index * 72f * Mathf.Deg2Rad; // 원형 장식 배치 각도 계산
                    Vector3 localPosition = new Vector3(Mathf.Cos(angle) * 0.28f, 0.48f, Mathf.Sin(angle) * 0.28f); // 왕관 둘레 장식 위치 계산
                    CreatePrimitivePart(parent, PrimitiveType.Cube, "CrownPoint_" + (index + 1), localPosition, new Vector3(0.12f, 0.46f, 0.12f), new Vector3(0f, -index * 72f, 0f), material); // 왕관 뾰족 장식 생성
                }

                return; // 왕관 외형 생성 종료
            }

            CreatePrimitivePart(parent, PrimitiveType.Cube, "StatuePedestal", new Vector3(0f, 0.16f, 0f), new Vector3(0.66f, 0.32f, 0.56f), Vector3.zero, material); // 조각상 받침 생성
            CreatePrimitivePart(parent, PrimitiveType.Capsule, "StatueBody", new Vector3(0f, 0.76f, 0f), new Vector3(0.48f, 0.78f, 0.42f), Vector3.zero, material); // 조각상 몸체 생성
            CreatePrimitivePart(parent, PrimitiveType.Sphere, "StatueHead", new Vector3(0f, 1.30f, 0f), new Vector3(0.42f, 0.42f, 0.42f), Vector3.zero, material); // 조각상 머리 생성
        }

        private static void CreateBoxVisual(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material) // 충돌 없는 시험장 바닥 표식 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 Cube 생성
            box.name = objectName; // 오브젝트 이름 지정
            box.transform.SetParent(parent); // 지정 부모 아래 연결
            box.transform.position = position; // 월드 위치 지정
            box.transform.localScale = scale; // 표시 크기 지정
            Renderer renderer = box.GetComponent<Renderer>(); // Cube Renderer 조회
            renderer.sharedMaterial = material; // 시험 재질 적용
            Collider collider = box.GetComponent<Collider>(); // 자동 생성 Collider 조회

            if (collider != null) // Collider 존재 여부 확인
            {
                Object.DestroyImmediate(collider); // 바닥 표식이 이동을 막지 않도록 Collider 제거
            }
        }

        private static void CreatePrimitivePart(Transform parent, PrimitiveType primitiveType, string objectName, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material) // 보물 외형 프리미티브 부품 생성
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType); // 요청 프리미티브 생성
            part.name = objectName; // 부품 이름 지정
            part.transform.SetParent(parent, false); // 보물 루트 아래 연결
            part.transform.localPosition = localPosition; // 로컬 위치 지정
            part.transform.localScale = localScale; // 로컬 크기 지정
            part.transform.localRotation = Quaternion.Euler(localEuler); // 로컬 회전 지정
            Renderer renderer = part.GetComponent<Renderer>(); // 부품 Renderer 조회
            renderer.sharedMaterial = material; // 시험 재질 적용
            Collider collider = part.GetComponent<Collider>(); // 자동 생성 부품 Collider 조회

            if (collider != null) // 부품 Collider 존재 여부 확인
            {
                Object.DestroyImmediate(collider); // 루트 BoxCollider만 사용하도록 부품 Collider 제거
            }
        }

        private static Transform GetOrCreateCarryPoint(Transform cameraTransform, string pointName, Vector3 localPosition) // 기존 카메라 CarryPoint 확보
        {
            Transform point = cameraTransform.Find(pointName); // 기존 CarryPoint 조회

            if (point == null) // CarryPoint 누락 여부 확인
            {
                GameObject pointObject = new GameObject(pointName); // 누락 CarryPoint 생성
                point = pointObject.transform; // 새 Transform 참조 저장
                point.SetParent(cameraTransform, false); // 카메라 자식으로 연결
            }

            point.localPosition = localPosition; // 기존 Day5 화면 위치로 보정
            point.localRotation = Quaternion.identity; // 카메라 기준 기본 회전 적용
            return point; // 확보한 CarryPoint 반환
        }

        private static void RemoveLegacyCarrierComponents(GameObject player) // 이전 Day20 직접 운반 컴포넌트 제거
        {
            MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>(); // 플레이어 MonoBehaviour 목록 조회

            foreach (MonoBehaviour behaviour in behaviours) // 플레이어 기능 컴포넌트 순회
            {
                if (behaviour == null) // Missing Script 여부 확인
                {
                    continue; // Missing Script는 별도 정리 단계에서 처리
                }

                if (behaviour.GetType().Name != "PlayerRecoverableCarrier") // 이전 전용 운반 타입 이름 확인
                {
                    continue; // 다른 플레이어 기능은 유지
                }

                Object.DestroyImmediate(behaviour); // 이전 전용 운반 컴포넌트 제거
            }
        }

        private static void RemoveMissingScriptsRecursive(Transform root) // 지정 루트와 자식 Missing Script 재귀 제거
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root.gameObject); // 현재 오브젝트 Missing Script 제거

            for (int index = 0; index < root.childCount; index++) // 모든 자식 Transform 순회
            {
                RemoveMissingScriptsRecursive(root.GetChild(index)); // 자식 Missing Script 재귀 제거
            }
        }

        private static void RemoveChildIfExists(Transform parent, string childName) // 이전 전용 CarryPoint 제거
        {
            Transform child = parent.Find(childName); // 대상 자식 조회

            if (child != null) // 대상 자식 존재 여부 확인
            {
                Object.DestroyImmediate(child.gameObject); // 이전 전용 CarryPoint 제거
            }
        }

        private static GameObject FindRoot(Scene scene, string rootName) // 씬 루트 이름으로 오브젝트 조회
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 일치하는 첫 루트 반환
        }

        private static void RemoveRoot(Scene scene, string rootName) // 지정 이름의 씬 루트 제거
        {
            GameObject root = FindRoot(scene, rootName); // 대상 루트 조회

            if (root != null) // 대상 루트 존재 여부 확인
            {
                Object.DestroyImmediate(root); // 기존 루트 즉시 제거
            }
        }

        private static void DeleteLegacySourceFiles() // 이전 Day20 전용 소스 파일 실제 삭제
        {
            foreach (string sourcePath in LegacySourcePaths) // 삭제 대상 소스 경로 순회
            {
                DeleteAssetIfExists(sourcePath); // 소스와 해당 meta를 AssetDatabase로 함께 제거
            }
        }

        private static void DeleteAssetIfExists(string assetPath) // 지정 에셋 또는 폴더 안전 삭제
        {
            if (!AssetDatabase.IsValidFolder(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) == null) // 에셋 존재 여부 확인
            {
                return; // 없는 에셋은 삭제 생략
            }

            AssetDatabase.DeleteAsset(assetPath); // 대상 에셋과 meta를 함께 삭제
        }

        private static void EnsureAssetFolder(string folderPath) // 중첩 에셋 폴더 존재 보장
        {
            string[] parts = folderPath.Split('/'); // 폴더 경로 단계 분리
            string current = parts[0]; // Assets 루트에서 시작

            for (int index = 1; index < parts.Length; index++) // 하위 폴더 단계 순회
            {
                string next = current + "/" + parts[index]; // 다음 단계 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 다음 폴더 누락 여부 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 누락 폴더 생성
                }

                current = next; // 다음 단계 기준 경로 갱신
            }
        }

        private static Material GetOrCreateMaterial(string fileName, Color color, float metallic, float smoothness) // 테스트 재질 생성 또는 갱신
        {
            string path = MaterialFolder + "/" + fileName + ".mat"; // 재질 에셋 경로 구성
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

            if (material == null) // 기존 재질 누락 여부 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit Shader 조회

                if (shader == null) // URP Shader 조회 실패 여부 확인
                {
                    shader = Shader.Find("Standard"); // 기본 Standard Shader 대체 조회
                }

                material = new Material(shader); // 새 재질 생성
                AssetDatabase.CreateAsset(material, path); // 지정 경로에 재질 에셋 저장
            }

            material.color = color; // 기본 색상 적용

            if (material.HasProperty("_Metallic")) // Metallic 속성 존재 여부 확인
            {
                material.SetFloat("_Metallic", metallic); // 금속성 적용
            }

            if (material.HasProperty("_Smoothness")) // Smoothness 속성 존재 여부 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 매끄러움 적용
            }

            EditorUtility.SetDirty(material); // 재질 저장 대상으로 표시
            return material; // 구성된 재질 반환
        }

        private enum TreasureVisual // 테스트 보물 외형 분류
        {
            Coin, // 은 동전 외형
            Ornament, // 금속 장식 외형
            Crown, // 왕관 외형
            Statue // 조각상 외형
        }
    }
}
