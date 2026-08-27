using System.IO; // 씬 파일 확인 기능 참조
using System.Linq; // 목록 검색 기능 참조
using ProjectI.Player; // 플레이어 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Input Action Asset 참조
using UnityEngine.Rendering; // 환경광 모드 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 자동 설정 등록
    public static class Phase2Day3Setup // 3일차 플레이어와 테스트 맵 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 입력 액션 에셋 경로
        private const string TestMaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 테스트 재질 폴더 경로
        private const string MapRootName = "===Day3 Test Map==="; // 테스트 맵 루트 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름

        static Phase2Day3Setup() // 자동 설정 생성자
        {
            EditorApplication.delayCall += TryAutoSetup; // 스크립트 컴파일 후 자동 설정 예약
        }

        private static void TryAutoSetup() // 자동 설정 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 설정 중단
            }

            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 대상 씬이 없으면 설정 중단
            }

            string sceneText = File.ReadAllText(ExplorationOfficeScenePath); // 현재 씬 YAML 읽기

            if (sceneText.Contains(MapRootName)) // 이미 3일차 맵 구성 여부 확인
            {
                return; // 기존 구성 유지
            }

            RebuildDay3Scene(false); // 첫 실행 자동 구성
        }

        [MenuItem("Tools/Project I/Day 3/Rebuild Exploration Test Map")] // 수동 재생성 메뉴 등록
        public static void RebuildFromMenu() // 메뉴 기반 테스트 맵 재생성
        {
            RebuildDay3Scene(true); // 대화상자 포함 재생성 실행
        }

        private static void RebuildDay3Scene(bool showDialog) // 3일차 씬 전체 재구성
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // 입력 액션 에셋 조회

            if (inputActions == null) // 입력 에셋 누락 확인
            {
                EditorUtility.DisplayDialog("Project I", "InputSystem_Actions.inputactions를 찾을 수 없습니다.", "확인"); // 입력 에셋 누락 알림
                return; // 씬 생성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            RemoveOldDay3Objects(scene); // 기존 테스트 맵과 플레이어 제거
            Material floorMaterial = GetOrCreateMaterial("Test_Floor", new Color(0.12f, 0.13f, 0.15f)); // 기본 바닥 재질 생성
            Material wallMaterial = GetOrCreateMaterial("Test_Wall", new Color(0.22f, 0.24f, 0.27f)); // 벽 재질 생성
            Material blueMaterial = GetOrCreateMaterial("Test_Blue", new Color(0.12f, 0.34f, 0.62f)); // 파란 구역 재질 생성
            Material yellowMaterial = GetOrCreateMaterial("Test_Yellow", new Color(0.78f, 0.58f, 0.08f)); // 노란 구역 재질 생성
            Material orangeMaterial = GetOrCreateMaterial("Test_Orange", new Color(0.82f, 0.28f, 0.08f)); // 주황 구역 재질 생성
            Material redMaterial = GetOrCreateMaterial("Test_Red", new Color(0.58f, 0.08f, 0.08f)); // 위험 구역 재질 생성
            Material metalMaterial = GetOrCreateMaterial("Test_Metal", new Color(0.34f, 0.37f, 0.4f)); // 금속 재질 생성
            GameObject mapRoot = new GameObject(MapRootName); // 테스트 맵 루트 생성
            CreateBaseArena(mapRoot.transform, floorMaterial, wallMaterial, metalMaterial); // 기본 경기장 생성
            CreateSprintLane(mapRoot.transform, blueMaterial, metalMaterial); // 달리기 시험 구역 생성
            CreateSlalomZone(mapRoot.transform, yellowMaterial, metalMaterial); // 슬라럼 시험 구역 생성
            CreateNarrowCorridor(mapRoot.transform, orangeMaterial, wallMaterial); // 좁은 통로 시험 구역 생성
            CreateStairRampZone(mapRoot.transform, blueMaterial, wallMaterial, metalMaterial); // 계단과 경사 시험 구역 생성
            CreateFutureCrouchGate(mapRoot.transform, yellowMaterial, wallMaterial); // 향후 웅크리기 시험 구역 생성
            CreateFutureFallZone(mapRoot.transform, redMaterial, metalMaterial); // 향후 낙하 시험 구역 생성
            CreateDecoration(mapRoot.transform, metalMaterial, yellowMaterial); // 산업형 장식 요소 생성
            CreatePlayer(inputActions); // 3일차 플레이어 생성
            ConfigureEnvironment(); // 씬 조명과 환경 설정
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 생성 재질 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool success = Phase2Day3Validator.Validate(false); // 3일차 구성 최종 검증

            if (showDialog) // 수동 실행 대화상자 여부 확인
            {
                string message = success ? "Day 3 테스트 맵과 플레이어 구성이 완료되었습니다." : "Day 3 구성 후 검증 실패 - Console을 확인하세요."; // 결과 메시지 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }
        }

        private static void RemoveOldDay3Objects(Scene scene) // 기존 자동 생성 오브젝트 정리
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects()) // 씬 루트 오브젝트 순회
            {
                bool removeMap = rootObject.name == MapRootName; // 테스트 맵 루트 여부 확인
                bool removePlayer = rootObject.name == PlayerRootName; // 플레이어 루트 여부 확인
                bool removeLegacyCamera = rootObject.CompareTag("MainCamera"); // 기존 독립 메인 카메라 여부 확인

                if (removeMap || removePlayer || removeLegacyCamera) // 자동 생성 교체 대상 확인
                {
                    Object.DestroyImmediate(rootObject); // 기존 오브젝트 제거
                }
            }
        }

        private static void CreateBaseArena(Transform parent, Material floorMaterial, Material wallMaterial, Material metalMaterial) // 기본 테스트 공간 생성
        {
            CreateBox(parent, "00_MainFloor", new Vector3(0f, -0.25f, 0f), new Vector3(80f, 0.5f, 60f), floorMaterial, true); // 넓은 기본 바닥 생성
            CreateBox(parent, "Boundary_North", new Vector3(0f, 2f, 30f), new Vector3(80f, 4f, 0.5f), wallMaterial, true); // 북쪽 경계벽 생성
            CreateBox(parent, "Boundary_South", new Vector3(0f, 2f, -30f), new Vector3(80f, 4f, 0.5f), wallMaterial, true); // 남쪽 경계벽 생성
            CreateBox(parent, "Boundary_East", new Vector3(40f, 2f, 0f), new Vector3(0.5f, 4f, 60f), wallMaterial, true); // 동쪽 경계벽 생성
            CreateBox(parent, "Boundary_West", new Vector3(-40f, 2f, 0f), new Vector3(0.5f, 4f, 60f), wallMaterial, true); // 서쪽 경계벽 생성
            CreateBox(parent, "SpawnPad", new Vector3(0f, 0.03f, -23f), new Vector3(12f, 0.06f, 8f), metalMaterial, false); // 시작 지점 바닥 표식 생성
        }

        private static void CreateSprintLane(Transform parent, Material zoneMaterial, Material metalMaterial) // 달리기 직선 구역 생성
        {
            GameObject zone = new GameObject("01_SprintLane"); // 달리기 구역 루트 생성
            zone.transform.SetParent(parent); // 달리기 구역 부모 지정
            CreateBox(zone.transform, "LaneFloor", new Vector3(-27f, 0.03f, 2f), new Vector3(8f, 0.06f, 44f), zoneMaterial, false); // 직선 달리기 바닥 표식 생성

            for (int index = 0; index <= 8; index++) // 거리 표식 반복
            {
                float zPosition = -18f + (index * 5f); // 거리 표식 Z 위치 계산
                CreateBox(zone.transform, $"DistanceMarker_{index * 5:00}m_L", new Vector3(-31.3f, 0.7f, zPosition), new Vector3(0.2f, 1.4f, 0.2f), metalMaterial, true); // 왼쪽 거리 표식 생성
                CreateBox(zone.transform, $"DistanceMarker_{index * 5:00}m_R", new Vector3(-22.7f, 0.7f, zPosition), new Vector3(0.2f, 1.4f, 0.2f), metalMaterial, true); // 오른쪽 거리 표식 생성
            }
        }

        private static void CreateSlalomZone(Transform parent, Material zoneMaterial, Material obstacleMaterial) // 방향 전환 시험 구역 생성
        {
            GameObject zone = new GameObject("02_Slalom"); // 슬라럼 구역 루트 생성
            zone.transform.SetParent(parent); // 슬라럼 구역 부모 지정
            CreateBox(zone.transform, "SlalomFloor", new Vector3(-10f, 0.03f, 5f), new Vector3(12f, 0.06f, 28f), zoneMaterial, false); // 슬라럼 바닥 표식 생성

            for (int index = 0; index < 7; index++) // 장애물 반복 생성
            {
                float xOffset = index % 2 == 0 ? -2.2f : 2.2f; // 좌우 교차 위치 계산
                float zPosition = -6f + (index * 3.5f); // 장애물 Z 위치 계산
                CreateBox(zone.transform, $"SlalomObstacle_{index + 1}", new Vector3(-10f + xOffset, 0.9f, zPosition), new Vector3(1.2f, 1.8f, 1.2f), obstacleMaterial, true); // 슬라럼 장애물 생성
            }
        }

        private static void CreateNarrowCorridor(Transform parent, Material zoneMaterial, Material wallMaterial) // 충돌과 좁은 통로 시험 구역 생성
        {
            GameObject zone = new GameObject("03_NarrowCorridor"); // 좁은 통로 구역 루트 생성
            zone.transform.SetParent(parent); // 좁은 통로 구역 부모 지정
            CreateBox(zone.transform, "CorridorFloor", new Vector3(6f, 0.03f, -10f), new Vector3(14f, 0.06f, 8f), zoneMaterial, false); // 통로 바닥 표식 생성
            CreateBox(zone.transform, "CorridorWall_Left", new Vector3(6f, 1.5f, -12.2f), new Vector3(14f, 3f, 0.5f), wallMaterial, true); // 통로 왼쪽 벽 생성
            CreateBox(zone.transform, "CorridorWall_Right", new Vector3(6f, 1.5f, -7.8f), new Vector3(14f, 3f, 0.5f), wallMaterial, true); // 통로 오른쪽 벽 생성
            CreateBox(zone.transform, "CollisionWall", new Vector3(13f, 1.5f, -10f), new Vector3(0.5f, 3f, 8f), wallMaterial, true); // 정면 충돌 시험 벽 생성
        }

        private static void CreateStairRampZone(Transform parent, Material zoneMaterial, Material wallMaterial, Material metalMaterial) // 계단과 경사 시험 구역 생성
        {
            GameObject zone = new GameObject("04_StairRamp"); // 계단 경사 구역 루트 생성
            zone.transform.SetParent(parent); // 계단 경사 구역 부모 지정
            CreateBox(zone.transform, "ZoneFloor", new Vector3(20f, 0.03f, 10f), new Vector3(24f, 0.06f, 24f), zoneMaterial, false); // 구역 바닥 표식 생성

            for (int index = 0; index < 8; index++) // 계단 단수 반복
            {
                float stepHeight = (index + 1) * 0.2f; // 현재 계단 높이 계산
                float zPosition = 4f + (index * 0.55f); // 현재 계단 위치 계산
                CreateBox(zone.transform, $"Stair_{index + 1:00}", new Vector3(15f, stepHeight * 0.5f, zPosition), new Vector3(4f, stepHeight, 0.6f), wallMaterial, true); // 계단 블록 생성
            }

            CreateBox(zone.transform, "StairTopPlatform", new Vector3(15f, 0.8f, 10.5f), new Vector3(6f, 1.6f, 6f), metalMaterial, true); // 계단 상단 플랫폼 생성
            GameObject ramp = CreateBox(zone.transform, "Ramp", new Vector3(25f, 0.9f, 7f), new Vector3(5f, 0.4f, 10f), wallMaterial, true); // 경사로 본체 생성
            ramp.transform.rotation = Quaternion.Euler(-10f, 0f, 0f); // 경사로 기울기 적용
            CreateBox(zone.transform, "RampTopPlatform", new Vector3(25f, 1.55f, 13.2f), new Vector3(7f, 0.5f, 6f), metalMaterial, true); // 경사로 상단 플랫폼 생성
        }

        private static void CreateFutureCrouchGate(Transform parent, Material zoneMaterial, Material wallMaterial) // 향후 웅크리기 시험 구역 생성
        {
            GameObject zone = new GameObject("05_CrouchGate_Future"); // 웅크리기 구역 루트 생성
            zone.transform.SetParent(parent); // 웅크리기 구역 부모 지정
            CreateBox(zone.transform, "CrouchFloor", new Vector3(22f, 0.03f, -15f), new Vector3(16f, 0.06f, 8f), zoneMaterial, false); // 웅크리기 구역 바닥 표식 생성
            CreateBox(zone.transform, "Gate_Left", new Vector3(22f, 1.3f, -18.2f), new Vector3(16f, 2.6f, 0.5f), wallMaterial, true); // 웅크리기 통로 왼쪽 벽 생성
            CreateBox(zone.transform, "Gate_Right", new Vector3(22f, 1.3f, -11.8f), new Vector3(16f, 2.6f, 0.5f), wallMaterial, true); // 웅크리기 통로 오른쪽 벽 생성
            CreateBox(zone.transform, "LowBeam_Future", new Vector3(22f, 1.62f, -15f), new Vector3(5f, 0.34f, 6f), wallMaterial, true); // 현재는 통과 불가능한 낮은 천장 생성
        }

        private static void CreateFutureFallZone(Transform parent, Material zoneMaterial, Material metalMaterial) // 향후 낙하와 점프 시험 구역 생성
        {
            GameObject zone = new GameObject("06_FallTest_Future"); // 낙하 시험 구역 루트 생성
            zone.transform.SetParent(parent); // 낙하 시험 구역 부모 지정
            CreateBox(zone.transform, "WarningFloor", new Vector3(33f, 0.03f, 23f), new Vector3(10f, 0.06f, 10f), zoneMaterial, false); // 위험 구역 바닥 표식 생성
            CreateBox(zone.transform, "LowPlatform", new Vector3(31f, 0.3f, 23f), new Vector3(3f, 0.6f, 3f), metalMaterial, true); // 낮은 플랫폼 생성
            CreateBox(zone.transform, "MediumPlatform", new Vector3(35f, 0.75f, 23f), new Vector3(3f, 1.5f, 3f), metalMaterial, true); // 중간 플랫폼 생성
            CreateBox(zone.transform, "GuardRail_North", new Vector3(33f, 0.6f, 28f), new Vector3(10f, 1.2f, 0.2f), metalMaterial, true); // 북쪽 안전 난간 생성
            CreateBox(zone.transform, "GuardRail_East", new Vector3(38f, 0.6f, 23f), new Vector3(0.2f, 1.2f, 10f), metalMaterial, true); // 동쪽 안전 난간 생성
        }

        private static void CreateDecoration(Transform parent, Material metalMaterial, Material accentMaterial) // 테스트 맵 산업형 장식 생성
        {
            GameObject decoration = new GameObject("90_IndustrialDecoration"); // 장식 루트 생성
            decoration.transform.SetParent(parent); // 장식 루트 부모 지정

            for (int index = 0; index < 6; index++) // 중앙 기둥 반복 생성
            {
                float xPosition = -12f + (index * 5f); // 기둥 X 위치 계산
                CreateCylinder(decoration.transform, $"SupportColumn_{index + 1}", new Vector3(xPosition, 2f, 25f), new Vector3(0.5f, 2f, 0.5f), metalMaterial); // 산업형 지지 기둥 생성
            }

            CreateBox(decoration.transform, "Crate_A", new Vector3(5f, 0.5f, 18f), new Vector3(1f, 1f, 1f), metalMaterial, true); // 장식 상자 A 생성
            CreateBox(decoration.transform, "Crate_B", new Vector3(6.2f, 0.75f, 18f), new Vector3(1f, 1.5f, 1f), metalMaterial, true); // 장식 상자 B 생성
            CreateBox(decoration.transform, "Crate_C", new Vector3(7.4f, 0.4f, 18f), new Vector3(1f, 0.8f, 1f), metalMaterial, true); // 장식 상자 C 생성
            CreateZoneLight(decoration.transform, "GuideLight_Spawn", new Vector3(0f, 3.2f, -20f), new Color(1f, 0.72f, 0.18f)); // 시작 구역 안내 조명 생성
            CreateZoneLight(decoration.transform, "GuideLight_Center", new Vector3(0f, 4f, 6f), new Color(0.35f, 0.55f, 1f)); // 중앙 안내 조명 생성
            CreateZoneLight(decoration.transform, "GuideLight_Stairs", new Vector3(20f, 4f, 10f), new Color(1f, 0.55f, 0.2f)); // 계단 구역 안내 조명 생성
        }

        private static void CreatePlayer(InputActionAsset inputActions) // 3일차 플레이어 생성
        {
            GameObject player = new GameObject(PlayerRootName); // 플레이어 루트 생성
            player.transform.position = new Vector3(0f, 0.05f, -23f); // 시작 위치 설정
            CharacterController characterController = player.AddComponent<CharacterController>(); // 캐릭터 컨트롤러 추가
            characterController.height = 1.8f; // 플레이어 캡슐 높이 설정
            characterController.radius = 0.35f; // 플레이어 캡슐 반지름 설정
            characterController.center = new Vector3(0f, 0.9f, 0f); // 캡슐 중심 위치 설정
            characterController.stepOffset = 0.3f; // 기본 계단 오르기 높이 설정
            characterController.slopeLimit = 50f; // 기본 경사 오르기 각도 설정
            characterController.skinWidth = 0.035f; // 충돌 안정화를 위한 스킨 폭 설정
            characterController.minMoveDistance = 0f; // 미세 이동 제한 해제
            PlayerInputReader inputReader = player.AddComponent<PlayerInputReader>(); // 입력 래퍼 추가
            inputReader.Configure(inputActions); // 프로젝트 입력 액션 연결
            player.AddComponent<PlayerStamina>(); // 스태미나 컴포넌트 추가
            player.AddComponent<PlayerMovement>(); // 이동 컴포넌트 추가
            GameObject view = new GameObject("View"); // 플레이어 시점 오브젝트 생성
            view.transform.SetParent(player.transform); // 시점 오브젝트를 플레이어에 연결
            view.transform.localPosition = new Vector3(0f, 1.62f, 0f); // 눈높이 위치 설정
            Camera camera = view.AddComponent<Camera>(); // 1인칭 카메라 추가
            camera.fieldOfView = 70f; // 테스트용 시야각 설정
            camera.nearClipPlane = 0.05f; // 근거리 클리핑 거리 설정
            camera.farClipPlane = 500f; // 원거리 클리핑 거리 설정
            view.tag = "MainCamera"; // 메인 카메라 태그 지정
            view.AddComponent<AudioListener>(); // 플레이어 오디오 리스너 추가
            PlayerLook playerLook = player.AddComponent<PlayerLook>(); // 시점 제어 컴포넌트 추가
            playerLook.Configure(view.transform); // 카메라 시점 트랜스폼 연결
            player.AddComponent<PlayerDebugHud>(); // 3일차 디버그 HUD 추가
        }

        private static void ConfigureEnvironment() // 테스트 씬 환경 설정
        {
            RenderSettings.fog = true; // 거리감을 위한 포그 활성화
            RenderSettings.fogColor = new Color(0.07f, 0.075f, 0.085f); // 어두운 포그 색상 설정
            RenderSettings.fogDensity = 0.006f; // 포그 밀도 설정
            RenderSettings.ambientMode = AmbientMode.Trilight; // 삼색 환경광 모드 설정
            RenderSettings.ambientIntensity = 0.75f; // 환경광 강도 설정
            Light directionalLight = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(light => light.type == LightType.Directional); // 기존 방향광 조회

            if (directionalLight != null) // 방향광 존재 여부 확인
            {
                directionalLight.intensity = 1.1f; // 방향광 밝기 설정
                directionalLight.shadows = LightShadows.Soft; // 부드러운 그림자 활성화
                directionalLight.transform.rotation = Quaternion.Euler(50f, -35f, 0f); // 방향광 각도 설정
            }
        }

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 박스 오브젝트 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 큐브 생성
            box.name = objectName; // 오브젝트 이름 지정
            box.transform.SetParent(parent); // 부모 오브젝트 지정
            box.transform.position = position; // 월드 위치 지정
            box.transform.localScale = scale; // 크기 지정
            Renderer renderer = box.GetComponent<Renderer>(); // 렌더러 참조 획득
            renderer.sharedMaterial = material; // 공용 재질 적용

            if (!keepCollider) // 콜라이더 제거 여부 확인
            {
                Object.DestroyImmediate(box.GetComponent<Collider>()); // 시각 표식용 콜라이더 제거
            }

            GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic); // 테스트 지형 정적 플래그 지정
            return box; // 생성된 박스 반환
        }

        private static GameObject CreateCylinder(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material) // 원기둥 오브젝트 생성
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 기본 원기둥 생성
            cylinder.name = objectName; // 오브젝트 이름 지정
            cylinder.transform.SetParent(parent); // 부모 오브젝트 지정
            cylinder.transform.position = position; // 월드 위치 지정
            cylinder.transform.localScale = scale; // 크기 지정
            cylinder.GetComponent<Renderer>().sharedMaterial = material; // 공용 재질 적용
            GameObjectUtility.SetStaticEditorFlags(cylinder, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic); // 테스트 장식 정적 플래그 지정
            return cylinder; // 생성된 원기둥 반환
        }

        private static void CreateZoneLight(Transform parent, string objectName, Vector3 position, Color lightColor) // 안내 조명 생성
        {
            GameObject lightObject = new GameObject(objectName); // 조명 오브젝트 생성
            lightObject.transform.SetParent(parent); // 조명 부모 지정
            lightObject.transform.position = position; // 조명 위치 지정
            Light pointLight = lightObject.AddComponent<Light>(); // 포인트 라이트 추가
            pointLight.type = LightType.Point; // 포인트 라이트 타입 지정
            pointLight.color = lightColor; // 안내 조명 색상 지정
            pointLight.intensity = 5f; // 안내 조명 밝기 지정
            pointLight.range = 12f; // 안내 조명 범위 지정
            pointLight.shadows = LightShadows.None; // 테스트 성능을 위해 실시간 그림자 비활성화
        }

        private static Material GetOrCreateMaterial(string materialName, Color color) // 테스트용 재질 조회 또는 생성
        {
            EnsureFolder(TestMaterialFolder); // 테스트 재질 폴더 확인
            string materialPath = $"{TestMaterialFolder}/{materialName}.mat"; // 재질 저장 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath); // 기존 재질 조회

            if (material == null) // 기존 재질 없음 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit 셰이더 조회

                if (shader == null) // URP 셰이더 조회 실패 확인
                {
                    shader = Shader.Find("Standard"); // 기본 Standard 셰이더 대체 조회
                }

                material = new Material(shader); // 테스트 재질 생성
                material.name = materialName; // 재질 이름 지정
                AssetDatabase.CreateAsset(material, materialPath); // 프로젝트 에셋으로 저장
            }

            if (material.HasProperty("_BaseColor")) // URP 기본 색상 프로퍼티 확인
            {
                material.SetColor("_BaseColor", color); // URP 기본 색상 적용
            }
            else if (material.HasProperty("_Color")) // 기본 셰이더 색상 프로퍼티 확인
            {
                material.SetColor("_Color", color); // 기본 셰이더 색상 적용
            }
            EditorUtility.SetDirty(material); // 재질 변경 상태 표시
            return material; // 테스트 재질 반환
        }

        private static void EnsureFolder(string folderPath) // 재귀 폴더 생성 확인
        {
            if (AssetDatabase.IsValidFolder(folderPath)) // 대상 폴더 존재 여부 확인
            {
                return; // 기존 폴더 유지
            }

            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/'); // 부모 폴더 경로 계산
            string folderName = Path.GetFileName(folderPath); // 생성할 폴더 이름 계산

            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath)) // 부모 폴더 누락 확인
            {
                EnsureFolder(parentPath); // 부모 폴더 먼저 생성
            }

            AssetDatabase.CreateFolder(parentPath, folderName); // 대상 폴더 생성
        }
    }
}
