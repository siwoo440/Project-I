using System.IO; // 입력 에셋과 씬 파일 상태 확인 기능 참조
using System.Linq; // 목록 검색 기능 참조
using System.Text.RegularExpressions; // Input Action JSON 부분 수정 기능 참조
using ProjectI.Interaction; // 상호작용 기능 참조
using ProjectI.Items; // 아이템 기능 참조
using ProjectI.Player; // 플레이어 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Input Action Asset 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 자동 업그레이드 등록
    public static class Phase2Day5Setup // 5일차 상호작용과 월드 아이템 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 프로젝트 Input Action 파일 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string Day5MarkerName = "===Day5 Ready==="; // 5일차 적용 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 기존 테스트 재질 폴더 경로

        static Phase2Day5Setup() // 자동 설정 생성자
        {
            EditorApplication.delayCall += TryAutoUpgrade; // 스크립트 컴파일 후 자동 업그레이드 예약
        }

        private static void TryAutoUpgrade() // 자동 업그레이드 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 업그레이드 중단
            }

            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬이 없으면 업그레이드 중단
            }

            EnsureInteractionInput(); // Interact 입력을 F와 기본 Button 방식으로 보정
            string sceneText = File.ReadAllText(ExplorationOfficeScenePath); // 현재 씬 YAML 읽기

            if (sceneText.Contains(Day5MarkerName)) // 5일차 업그레이드 적용 여부 확인
            {
                return; // 이미 적용된 씬 유지
            }

            ApplyDay5Upgrade(false); // 첫 실행 자동 업그레이드
        }

        [MenuItem("Tools/Project I/Day 5/Apply Day 5 Upgrade")] // 수동 업그레이드 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 업그레이드 실행
        {
            ApplyDay5Upgrade(true); // 결과 대화상자를 포함한 업그레이드 실행
        }

        private static void ApplyDay5Upgrade(bool showDialog) // 5일차 전체 업그레이드
        {
            EnsureInteractionInput(); // 입력 에셋 우선 보정
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 테스트 맵 루트 조회

            if (player == null || mapRoot == null) // 3~4일차 기본 구조 누락 확인
            {
                Debug.LogError("[Project I] Day 5 적용 전에 Player와 공용 테스트 맵 구성이 필요합니다."); // 선행 구성 누락 오류 출력

                if (showDialog) // 수동 실행 여부 확인
                {
                    EditorUtility.DisplayDialog("Project I", "Day 4까지의 Player/Test Map 구성을 먼저 확인하세요.", "확인"); // 선행 구성 안내 출력
                }

                return; // 업그레이드 중단
            }

            UpgradePlayer(player); // 플레이어 상호작용·운반 기능 추가
            UpgradeTestMap(mapRoot); // 상호작용 전용 시험 구역 추가
            EnsureDay5Marker(scene); // 5일차 완료 마커 추가
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool success = Phase2Day5Validator.Validate(false); // 5일차 전체 구성 검증

            if (showDialog) // 수동 실행 결과 대화상자 여부 확인
            {
                string message = success ? "Day 5 상호작용과 월드 아이템 구성이 완료되었습니다." : "Day 5 구성 후 검증 실패 - Console을 확인하세요."; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }
        }

        private static void EnsureInteractionInput() // Interact를 E→F로 변경하고 고정 Hold Interaction 제거
        {
            if (!File.Exists(InputActionsPath)) // Input Action 원본 파일 존재 여부 확인
            {
                Debug.LogError("[Project I] InputSystem_Actions.inputactions를 찾을 수 없습니다."); // 입력 에셋 누락 오류 출력
                return; // 입력 수정 중단
            }

            string json = File.ReadAllText(InputActionsPath); // Input Action JSON 원문 읽기
            string updated = json; // 수정본 초기화
            updated = updated.Replace("\"interactions\": \\\"\\\"", "\"interactions\": \"\""); // 이전 5일차 자동 수정으로 깨진 JSON 값을 우선 복구
            string actionObjectPattern = "\\{(?=[^{}]*\\\"name\\\"\\s*:\\s*\\\"Interact\\\")(?=[^{}]*\\\"type\\\"\\s*:\\s*\\\"Button\\\")[^{}]*\\}"; // Interact Action 객체만 찾는 패턴
            updated = Regex.Replace(updated, actionObjectPattern, match => Regex.Replace(match.Value, "(\\\"interactions\\\"\\s*:\\s*)\\\"[^\\\"]*\\\"", "$1\"\""), RegexOptions.Singleline); // Interact Action의 고정 Hold Interaction을 올바른 JSON 빈 문자열로 변경
            string bindingObjectPattern = "\\{(?=[^{}]*\\\"path\\\"\\s*:\\s*\\\"<Keyboard>/e\\\")(?=[^{}]*\\\"action\\\"\\s*:\\s*\\\"Interact\\\")[^{}]*\\}"; // E 키 Interact 바인딩 객체만 찾는 패턴
            updated = Regex.Replace(updated, bindingObjectPattern, match => match.Value.Replace("<Keyboard>/e", "<Keyboard>/f"), RegexOptions.Singleline); // Interact 키를 F로 변경

            if (updated == json) // 실제 문자열 변경 여부 확인
            {
                AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate); // 현재 파일을 다시 임포트하여 상태 갱신
                return; // 추가 파일 쓰기 불필요
            }

            File.WriteAllText(InputActionsPath, updated); // 수정된 Input Action JSON 저장
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate); // 변경된 입력 에셋 강제 재임포트
            AssetDatabase.Refresh(); // 에셋 데이터 갱신
        }

        public static void RefreshPlayerCarrySetup(GameObject player) // 5일차 운반 포즈를 현재 플레이어에 다시 적용
        {
            UpgradePlayer(player); // 플레이어 한손·양손 운반 지점과 컴포넌트 갱신
        }

        public static void RebuildInteractionTestZone(GameObject mapRoot) // 겹침 없는 5일차 시험 구역 재생성
        {
            UpgradeTestMap(mapRoot); // 현재 08_InteractionTest를 안전한 위치로 다시 생성
        }

        private static void UpgradePlayer(GameObject player) // 플레이어 5일차 기능 추가
        {
            PlayerInputReader inputReader = GetOrAddComponent<PlayerInputReader>(player); // 기존 입력 래퍼 확보
            PlayerCarryController carryController = GetOrAddComponent<PlayerCarryController>(player); // 월드 아이템 운반 기능 확보
            PlayerInteractor interactor = GetOrAddComponent<PlayerInteractor>(player); // 상호작용 감지 기능 확보
            InteractionPromptHud promptHud = GetOrAddComponent<InteractionPromptHud>(player); // 상호작용 안내 UI 확보
            Camera playerCamera = player.GetComponentInChildren<Camera>(true); // 플레이어 카메라 조회

            if (playerCamera == null) // 플레이어 카메라 누락 확인
            {
                Debug.LogError("[Project I] Day 5 Player Camera를 찾을 수 없습니다.", player); // 카메라 누락 오류 출력
                return; // 플레이어 업그레이드 중단
            }

            Transform legacyCarryPoint = playerCamera.transform.Find("CarryPoint"); // 기존 조준점 앞 운반 지점 조회

            if (legacyCarryPoint != null && legacyCarryPoint.childCount == 0) // 기존 지점에 연결된 자식이 없는지 확인
            {
                Object.DestroyImmediate(legacyCarryPoint.gameObject); // 더 이상 사용하지 않는 기존 CarryPoint 삭제
            }

            Transform oneHandCarryPoint = GetOrCreateCarryPoint(playerCamera.transform, "OneHandCarryPoint", new Vector3(0.42f, -0.38f, 0.90f)); // 화면 오른쪽 아래 한손 포즈 생성
            Transform twoHandCarryPoint = GetOrCreateCarryPoint(playerCamera.transform, "TwoHandCarryPoint", new Vector3(0f, -0.48f, 1.05f)); // 화면 중앙 아래 양손 포즈 생성
            carryController.Configure(playerCamera.transform, oneHandCarryPoint, twoHandCarryPoint, inputReader); // 한손·양손 운반 포즈 참조 연결
            interactor.Configure(playerCamera, inputReader, carryController); // 상호작용 기능 참조 연결
            promptHud.Configure(interactor); // 안내 HUD와 상호작용 기능 연결
            EditorUtility.SetDirty(carryController); // 운반 기능 변경 저장 대상으로 표시
            EditorUtility.SetDirty(interactor); // 상호작용 기능 변경 저장 대상으로 표시
            EditorUtility.SetDirty(promptHud); // 안내 UI 변경 저장 대상으로 표시
        }

        private static Transform GetOrCreateCarryPoint(Transform cameraTransform, string pointName, Vector3 localPosition) // 카메라 아래 운반 포즈 지점 확보
        {
            Transform point = cameraTransform.Find(pointName); // 기존 운반 지점 조회

            if (point == null) // 운반 지점 미생성 확인
            {
                GameObject pointObject = new GameObject(pointName); // 빈 운반 포즈 오브젝트 생성
                point = pointObject.transform; // 새 트랜스폼 참조
                point.SetParent(cameraTransform, false); // 카메라 자식으로 연결
            }

            point.localPosition = localPosition; // 화면 기준 운반 위치 설정
            point.localRotation = Quaternion.identity; // 카메라 방향과 동일한 기본 회전 설정
            return point; // 확보한 운반 지점 반환
        }

        private static void UpgradeTestMap(GameObject mapRoot) // 공용 테스트 맵에 5일차 시험 구역 추가
        {
            Transform existingZone = mapRoot.transform.Find("08_InteractionTest"); // 기존 5일차 시험 구역 조회

            if (existingZone != null) // 기존 시험 구역 존재 확인
            {
                Object.DestroyImmediate(existingZone.gameObject); // 중복 생성을 막기 위해 기존 구역 삭제
            }

            Material floorMaterial = LoadMaterial("Test_Blue"); // 시험 구역 바닥 재질 조회
            Material wallMaterial = LoadMaterial("Test_Wall"); // 고정 오브젝트 재질 조회
            Material yellowMaterial = LoadMaterial("Test_Yellow"); // Press/Hold 시험 재질 조회
            Material orangeMaterial = LoadMaterial("Test_Orange"); // Toggle/아이템 시험 재질 조회
            Material metalMaterial = LoadMaterial("Test_Metal"); // 아이템/표적 재질 조회
            GameObject zone = new GameObject("08_InteractionTest"); // 5일차 시험 구역 루트 생성
            zone.transform.SetParent(mapRoot.transform); // 공용 테스트 맵 아래 배치
            CreateBox(zone.transform, "InteractionFloor", new Vector3(-10f, 0.04f, -24f), new Vector3(18f, 0.05f, 8f), floorMaterial, false); // 다른 테스트 구역과 겹치지 않는 좌하단 바닥 표식 생성
            CreateInteractionStation(zone.transform, "PressTest", new Vector3(-16f, 1f, -23.5f), yellowMaterial, InteractionType.Press, "버튼 누르기", 0.1f); // Press 시험 물체 생성
            CreateInteractionStation(zone.transform, "HoldTest", new Vector3(-12.5f, 1f, -23.5f), yellowMaterial, InteractionType.Hold, "밸브 돌리기", 1.5f); // Hold 시험 물체 생성
            CreateInteractionStation(zone.transform, "ToggleTest", new Vector3(-9f, 1f, -23.5f), orangeMaterial, InteractionType.Toggle, "전원 전환", 0.1f); // Toggle 시험 물체 생성
            CreateWorldItem(zone.transform, "TestItem_Sword", new Vector3(-5.5f, 0.20f, -22.5f), PrimitiveType.Cube, new Vector3(0.12f, 0.08f, 1.10f), metalMaterial, "시험용 검", CarryType.OneHand); // 작고 길쭉한 한손 검 크기의 시험 아이템 생성
            CreateWorldItem(zone.transform, "TestItem_Pickaxe", new Vector3(-3.5f, 0.35f, -24.5f), PrimitiveType.Cylinder, new Vector3(0.10f, 0.55f, 0.10f), orangeMaterial, "시험용 곡괭이", CarryType.TwoHand); // 작고 길쭉한 양손 곡괭이 크기의 시험 아이템 생성
            CreateBox(zone.transform, "ThrowTarget", new Vector3(-4.5f, 1.5f, -27.5f), new Vector3(5f, 3f, 0.3f), wallMaterial, true); // 아이템 투척 표적 벽 생성
            CreateBox(zone.transform, "DropBoundary_Left", new Vector3(-7f, 0.6f, -25.5f), new Vector3(0.25f, 1.2f, 4f), wallMaterial, true); // 내려놓기 충돌 확인용 왼쪽 벽 생성
            CreateBox(zone.transform, "DropBoundary_Right", new Vector3(-2f, 0.6f, -25.5f), new Vector3(0.25f, 1.2f, 4f), wallMaterial, true); // 내려놓기 충돌 확인용 오른쪽 벽 생성
        }

        private static void CreateInteractionStation(Transform parent, string name, Vector3 position, Material material, InteractionType type, string prompt, float holdDuration) // 시험 상호작용 물체 생성
        {
            GameObject station = CreateBox(parent, name, position, new Vector3(1.4f, 2f, 1.4f), material, true); // 시험용 상호작용 본체 생성
            TestInteractable interactable = station.AddComponent<TestInteractable>(); // 공통 시험 상호작용 컴포넌트 추가
            interactable.Configure(prompt, type, holdDuration, station.transform); // 상호작용 방식과 안내 문구 설정
        }

        private static void CreateWorldItem(Transform parent, string name, Vector3 position, PrimitiveType primitiveType, Vector3 scale, Material material, string displayName, CarryType carryType) // 시험 월드 아이템 생성
        {
            GameObject item = GameObject.CreatePrimitive(primitiveType); // 기본 Primitive 아이템 생성
            item.name = name; // 아이템 오브젝트 이름 설정
            item.transform.SetParent(parent); // 시험 구역 아래 배치
            item.transform.position = position; // 초기 월드 위치 설정
            item.transform.localScale = scale; // 시험 아이템 크기 설정
            Renderer renderer = item.GetComponent<Renderer>(); // 아이템 렌더러 조회

            if (renderer != null && material != null) // 렌더러와 재질 존재 확인
            {
                renderer.sharedMaterial = material; // 기존 테스트 재질 적용
            }

            Rigidbody body = item.AddComponent<Rigidbody>(); // 월드 물리 Rigidbody 추가
            body.mass = 2f; // 기본 아이템 질량 지정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 물리 프레임 사이 움직임 보간
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 빠른 투척 시 충돌 안정성 향상
            WorldItem worldItem = item.AddComponent<WorldItem>(); // 월드 아이템 기능 추가
            float radius = Mathf.Clamp(Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) * 0.75f, 0.08f, 0.22f); // 작은 도구 두께 기준 공간 검사 반지름 계산
            worldItem.Configure(displayName, radius, carryType); // 표시 이름·운반 반지름·한손/양손 방식을 설정
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 공통 Box 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // Cube Primitive 생성
            box.name = name; // 오브젝트 이름 지정
            box.transform.SetParent(parent); // 부모 지정
            box.transform.position = position; // 월드 위치 지정
            box.transform.localScale = scale; // 크기 지정
            Renderer renderer = box.GetComponent<Renderer>(); // 렌더러 조회

            if (renderer != null && material != null) // 렌더러와 재질 존재 확인
            {
                renderer.sharedMaterial = material; // 테스트 재질 적용
            }

            if (!keepCollider) // 충돌이 필요 없는 바닥 표식 확인
            {
                Collider collider = box.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 확인
                {
                    Object.DestroyImmediate(collider); // 장식용 Collider 제거
                }
            }

            return box; // 생성된 오브젝트 반환
        }

        private static Material LoadMaterial(string materialName) // 기존 테스트 재질 조회
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{materialName}.mat"); // 지정 이름의 Material 반환
        }

        private static void EnsureDay5Marker(Scene scene) // 5일차 자동 적용 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == Day5MarkerName); // 기존 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(Day5MarkerName); // 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서는 숨김
            }
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 컴포넌트 확보 공통 도우미
        {
            T component = target.GetComponent<T>(); // 기존 컴포넌트 조회

            if (component == null) // 컴포넌트 미존재 확인
            {
                component = target.AddComponent<T>(); // 필요한 컴포넌트 새로 추가
            }

            return component; // 확보한 컴포넌트 반환
        }
    }
}
