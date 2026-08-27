using System.IO; // Input Action JSON과 씬 파일 입출력 기능 참조
using System.Linq; // Input Binding과 루트 오브젝트 검색 기능 참조
using ProjectI.Items; // 빠른 슬롯 HUD와 인벤토리 기능 참조
using ProjectI.Player; // 입력 래퍼와 재바인딩 서비스 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Input Action Asset 안전 수정 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조
using UnityEngine.UI; // Canvas UI 구성 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 후 입력·Canvas 수정 자동 적용
    public static class Phase2Day6InputUiFix // Q 버리기·재바인딩·Canvas 빠른 슬롯 UI·숫자키 직접 선택 통합 수정
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 프로젝트 Input Action Asset 경로
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string CanvasName = "PlayerHUDCanvas"; // 빠른 슬롯 Canvas 이름
        private const string FixMarkerName = "===Day6 Rebindable Input Canvas UI Ready==="; // 이번 수정 적용 완료 마커

        static Phase2Day6InputUiFix() // 자동 수정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 에디터 준비 후 자동 적용 예약
        }

        [MenuItem("Tools/Project I/Day 6/Apply Rebindable Input + Canvas UI")] // 수동 재적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 수정 실행
        {
            EnsureInputActions(); // Input Action 구성 갱신
            ApplySceneFix(true, true); // Canvas와 플레이어 구성 강제 재적용
        }

        private static void TryAutoApply() // 자동 수정 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 수정 중단
            }

            bool inputChanged = EnsureInputActions(); // 재바인딩 가능한 액션과 기본 키 구성

            if (inputChanged) // Input Action Asset이 실제로 변경되었는지 확인
            {
                EditorApplication.delayCall += () => ApplySceneFix(false, false); // Asset 재임포트 뒤 씬 수정 예약
                return; // 현재 프레임 씬 수정 중단
            }

            ApplySceneFix(false, false); // 입력 구성이 이미 최신이면 즉시 씬 구성 적용
        }

        public static bool EnsureInputActions() // Input Action JSON을 Unity API로 읽고 안전하게 필요한 액션 추가
        {
            if (!File.Exists(InputActionsPath)) // Input Action 파일 존재 여부 확인
            {
                Debug.LogError($"[Project I] Input Action 파일을 찾을 수 없습니다: {InputActionsPath}"); // 파일 누락 오류 출력
                return false; // 수정 없음 반환
            }

            string originalJson = File.ReadAllText(InputActionsPath); // 현재 Input Action JSON 원문 읽기
            InputActionAsset workingAsset = InputActionAsset.FromJson(originalJson); // Unity Input System으로 JSON 안전 파싱
            InputActionMap playerMap = workingAsset.FindActionMap(GameplayInputActions.Map, true); // Player 액션 맵 조회
            EnsureButtonAction(playerMap, GameplayInputActions.Interact, "<Keyboard>/f"); // 기본 F 상호작용 바인딩 확보
            EnsureButtonAction(playerMap, GameplayInputActions.Crouch, "<Keyboard>/leftCtrl"); // 기본 왼쪽 Ctrl 웅크리기 바인딩 확보
            EnsureButtonAction(playerMap, GameplayInputActions.Jump, "<Keyboard>/space"); // 기본 Space 점프 바인딩 확보
            EnsureButtonAction(playerMap, GameplayInputActions.Sprint, "<Keyboard>/leftShift"); // 기본 왼쪽 Shift 달리기 바인딩 확보
            EnsureButtonAction(playerMap, GameplayInputActions.Use, "<Mouse>/leftButton"); // 기본 좌클릭 아이템 사용 바인딩 추가
            EnsureButtonAction(playerMap, GameplayInputActions.Drop, "<Keyboard>/q"); // 기본 Q 아이템 버리기 바인딩 추가
            EnsureButtonAction(playerMap, GameplayInputActions.Pause, "<Keyboard>/escape"); // 기본 ESC 커서·일시정지 바인딩 추가
            EnsureValueAction(playerMap, GameplayInputActions.SlotScroll, "<Mouse>/scroll/y"); // 기본 마우스 휠 슬롯 전환 바인딩 추가

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 빠른 슬롯 1~6 액션 순회
            {
                string bindingPath = $"<Keyboard>/{index + 1}"; // 현재 슬롯 기본 숫자키 1~6 경로 생성
                EnsureButtonAction(playerMap, GameplayInputActions.QuickSlot(index), bindingPath); // 슬롯 직접 선택 액션과 기본 키 확보
            }

            string updatedJson = workingAsset.ToJson(); // Unity Input System이 생성한 유효 JSON 직렬화
            Object.DestroyImmediate(workingAsset); // 임시 InputActionAsset 메모리 정리

            if (NormalizeJson(originalJson) == NormalizeJson(updatedJson)) // 실질적인 입력 구성 변경 여부 확인
            {
                return false; // 변경 없음 반환
            }

            File.WriteAllText(InputActionsPath, updatedJson); // Unity가 생성한 유효 JSON으로 Input Action 파일 저장
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate); // 변경된 Input Action Asset 강제 재임포트
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            Debug.Log("[Project I] Day 6 Input Actions 갱신 - Drop 기본키 Q / 모든 게임플레이 입력 재바인딩 준비 완료"); // 입력 구성 완료 로그 출력
            return true; // 입력 파일 변경 반환
        }

        private static void ApplySceneFix(bool showDialog, bool force) // 플레이어 재바인딩 서비스와 Canvas UI 구성
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬 수정 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == FixMarkerName); // 기존 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 적용된 씬인지 확인
            {
                return; // 중복 자동 적용 방지
            }

            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회

            if (player == null) // 플레이어 누락 확인
            {
                Debug.LogError("[Project I] Day 6 Input/UI Fix에 필요한 Player를 찾을 수 없습니다."); // 플레이어 누락 오류 출력
                return; // 씬 수정 중단
            }

            PlayerInputReader inputReader = player.GetComponent<PlayerInputReader>(); // 기존 입력 래퍼 조회
            PlayerInventory inventory = player.GetComponent<PlayerInventory>(); // 기존 6칸 인벤토리 조회
            QuickSlotHud hud = player.GetComponent<QuickSlotHud>(); // 기존 빠른 슬롯 HUD 컴포넌트 조회

            if (inputReader == null || inventory == null || hud == null) // Day 6 핵심 컴포넌트 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 6 기본 구성을 먼저 적용해야 합니다. Tools > Project I > Day 6 > Apply Day 6 Upgrade를 실행하세요."); // 선행 구성 안내 출력
                return; // 씬 수정 중단
            }

            InputActionAsset actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // 갱신된 Input Action Asset 로드
            inputReader.Configure(actionAsset); // 플레이어 입력 래퍼에 최신 액션 에셋 재연결
            InputRebindService rebindService = GetOrAddComponent<InputRebindService>(player); // 향후 설정 화면용 재바인딩 서비스 확보
            rebindService.Configure(inputReader); // 재바인딩 서비스와 입력 래퍼 연결
            BuildCanvasHud(scene, inventory, hud); // Canvas 기반 빠른 슬롯 6칸 UI 구성
            EnsureMarker(scene); // 수정 완료 마커 확보
            EditorUtility.SetDirty(inputReader); // 입력 래퍼 변경 저장 대상으로 표시
            EditorUtility.SetDirty(rebindService); // 재바인딩 서비스 변경 저장 대상으로 표시
            EditorUtility.SetDirty(hud); // HUD 참조 변경 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 변경된 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장

            if (showDialog) // 수동 실행 결과 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "Q 버리기 / 재바인딩 입력 구조 / Canvas 빠른 슬롯 UI를 적용했습니다.", "확인"); // 완료 안내 표시
            }

            Debug.Log("[Project I] Day 6 Rebindable Input + Canvas UI 적용 완료"); // 적용 완료 로그 출력
        }

        private static void BuildCanvasHud(Scene scene, PlayerInventory inventory, QuickSlotHud hud) // 화면 하단 Canvas 빠른 슬롯 UI 생성
        {
            GameObject existingCanvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == CanvasName); // 기존 빠른 슬롯 Canvas 조회

            if (existingCanvas != null) // 기존 Canvas 존재 여부 확인
            {
                Object.DestroyImmediate(existingCanvas); // 설정 변경을 정확히 반영하기 위해 기존 Canvas 재생성
            }

            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); // Canvas 기본 오브젝트 생성
            Canvas canvas = canvasObject.GetComponent<Canvas>(); // Canvas 컴포넌트 조회
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 항상 화면 위에 표시되는 Overlay 방식 설정
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); // Canvas Scaler 조회
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 화면 해상도에 따른 UI 크기 보정 사용
            scaler.referenceResolution = new Vector2(1920f, 1080f); // 기준 해상도 설정
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; // 가로·세로 비율 혼합 보정 설정
            scaler.matchWidthOrHeight = 0.5f; // 가로와 세로를 동일 비율로 보정
            GameObject panelObject = new GameObject("QuickSlotPanel", typeof(RectTransform)); // 빠른 슬롯 패널 루트 생성
            RectTransform panelRect = panelObject.GetComponent<RectTransform>(); // 패널 RectTransform 조회
            panelRect.SetParent(canvasObject.transform, false); // Canvas 아래에 패널 연결
            panelRect.anchorMin = new Vector2(0.5f, 0f); // 화면 하단 중앙 기준 앵커 설정
            panelRect.anchorMax = new Vector2(0.5f, 0f); // 화면 하단 중앙 기준 앵커 설정
            panelRect.pivot = new Vector2(0.5f, 0f); // 패널 피벗을 하단 중앙으로 설정
            panelRect.anchoredPosition = new Vector2(0f, 28f); // 화면 하단에서 약간 위로 배치
            panelRect.sizeDelta = new Vector2(568f, 72f); // 6칸 슬롯 전체 크기 설정
            Image[] backgrounds = new Image[PlayerInventory.Capacity]; // 슬롯 배경 참조 배열 생성
            Text[] numbers = new Text[PlayerInventory.Capacity]; // 슬롯 번호 참조 배열 생성
            Text[] names = new Text[PlayerInventory.Capacity]; // 아이템 이름 참조 배열 생성
            Text[] locks = new Text[PlayerInventory.Capacity]; // 잠금 표시 참조 배열 생성
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 기본 런타임 폰트 조회
            float slotWidth = 88f; // 슬롯 한 칸 너비
            float gap = 8f; // 슬롯 사이 간격
            float totalWidth = (slotWidth * PlayerInventory.Capacity) + (gap * (PlayerInventory.Capacity - 1)); // 전체 슬롯 너비 계산
            float startX = (-totalWidth * 0.5f) + (slotWidth * 0.5f); // 첫 슬롯 중심 X 계산

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 1번부터 6번 슬롯 생성
            {
                GameObject slotObject = new GameObject($"Slot_{index + 1}", typeof(RectTransform), typeof(Image)); // 슬롯 배경 오브젝트 생성
                RectTransform slotRect = slotObject.GetComponent<RectTransform>(); // 슬롯 RectTransform 조회
                slotRect.SetParent(panelRect, false); // 패널 아래에 슬롯 연결
                slotRect.anchorMin = new Vector2(0.5f, 0.5f); // 패널 중앙 기준 앵커 설정
                slotRect.anchorMax = new Vector2(0.5f, 0.5f); // 패널 중앙 기준 앵커 설정
                slotRect.pivot = new Vector2(0.5f, 0.5f); // 슬롯 중심 피벗 설정
                slotRect.sizeDelta = new Vector2(slotWidth, 72f); // 슬롯 크기 설정
                slotRect.anchoredPosition = new Vector2(startX + (index * (slotWidth + gap)), 0f); // 현재 슬롯 가로 위치 계산
                Image background = slotObject.GetComponent<Image>(); // 슬롯 배경 Image 조회
                background.raycastTarget = false; // 게임 입력을 막지 않도록 UI Raycast 비활성화
                backgrounds[index] = background; // HUD 갱신용 배경 참조 저장
                numbers[index] = CreateText(slotRect, "Number", (index + 1).ToString(), font, 16, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -5f), new Vector2(24f, 20f)); // 슬롯 숫자를 왼쪽 위에 배치
                names[index] = CreateText(slotRect, "ItemName", string.Empty, font, 14, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -22f)); // 슬롯 중앙 아이템 이름 표시
                locks[index] = CreateText(slotRect, "Lock", string.Empty, font, 12, TextAnchor.LowerCenter, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-12f, 18f)); // 슬롯 하단 LOCK 표시
            }

            hud.Configure(inventory, backgrounds, numbers, names, locks); // 플레이어 HUD 컴포넌트에 Canvas UI 참조 전체 연결
        }

        private static Text CreateText(RectTransform parent, string objectName, string value, Font font, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta) // 슬롯 내부 Text 생성
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text)); // Text UI 오브젝트 생성
            RectTransform rect = textObject.GetComponent<RectTransform>(); // Text RectTransform 조회
            rect.SetParent(parent, false); // 슬롯 아래에 Text 연결
            rect.anchorMin = anchorMin; // 요청된 최소 앵커 적용
            rect.anchorMax = anchorMax; // 요청된 최대 앵커 적용
            rect.pivot = pivot; // 요청된 피벗 적용
            rect.anchoredPosition = anchoredPosition; // 요청된 위치 적용
            rect.sizeDelta = sizeDelta; // 요청된 크기 또는 Stretch 오프셋 적용
            Text text = textObject.GetComponent<Text>(); // Text 컴포넌트 조회
            text.text = value; // 초기 표시 문자열 설정
            text.font = font; // 기본 폰트 지정
            text.fontSize = fontSize; // 폰트 크기 지정
            text.alignment = alignment; // 텍스트 정렬 지정
            text.color = Color.white; // HUD 가독성을 위한 흰색 텍스트 적용
            text.raycastTarget = false; // 게임 입력을 막지 않도록 Text Raycast 비활성화
            return text; // 생성한 Text 반환
        }

        private static void EnsureButtonAction(InputActionMap map, string actionName, string defaultBinding) // 버튼 액션과 기본 바인딩 확보
        {
            InputAction action = map.FindAction(actionName, false); // 기존 액션 조회

            if (action == null) // 액션 미생성 확인
            {
                action = map.AddAction(actionName, InputActionType.Button); // 새 버튼 액션 생성
            }

            EnsureBinding(action, defaultBinding); // 기본 입력 바인딩 확보
        }

        private static void EnsureValueAction(InputActionMap map, string actionName, string defaultBinding) // 값 액션과 기본 바인딩 확보
        {
            InputAction action = map.FindAction(actionName, false); // 기존 액션 조회

            if (action == null) // 액션 미생성 확인
            {
                action = map.AddAction(actionName, InputActionType.Value); // 새 값 액션 생성
            }

            EnsureBinding(action, defaultBinding); // 기본 입력 바인딩 확보
        }

        private static void EnsureBinding(InputAction action, string bindingPath) // 지정 Input Action의 기본 바인딩 중복 없이 추가
        {
            bool exists = action.bindings.Any(binding => binding.path == bindingPath); // 동일 기본 경로 존재 여부 확인

            if (exists) // 이미 기본 바인딩이 존재하는지 확인
            {
                return; // 중복 추가 방지
            }

            action.AddBinding(bindingPath, groups: "Keyboard&Mouse"); // 키보드·마우스 기본 바인딩 추가
        }

        private static string NormalizeJson(string json) // JSON 포맷 차이를 제외한 간단한 비교 문자열 생성
        {
            return string.Concat(json.Where(character => !char.IsWhiteSpace(character))); // 모든 공백 문자를 제거하여 실질 구성 비교
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 컴포넌트 확보 헬퍼
        {
            T component = target.GetComponent<T>(); // 기존 컴포넌트 조회
            return component != null ? component : target.AddComponent<T>(); // 기존 또는 새 컴포넌트 반환
        }

        private static void EnsureMarker(Scene scene) // 수정 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == FixMarkerName); // 기존 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(FixMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 마커 숨김
            }
        }
    }
}
