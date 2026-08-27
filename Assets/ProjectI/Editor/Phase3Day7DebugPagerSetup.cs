using System.IO; // Input Action JSON과 씬 파일 입출력 기능 참조
using System.Linq; // Input Binding과 씬 루트 검색 기능 참조
using ProjectI.Brightness; // 밝기 디버그 페이지와 센서 기능 참조
using ProjectI.Diagnostics; // 공통 디버그 페이지 관리자 기능 참조
using ProjectI.Player; // 플레이어 입력과 상태 디버그 페이지 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.EventSystems; // UI Button 마우스 입력용 EventSystem 참조
using UnityEngine.InputSystem; // Input Action Asset 안전 수정 기능 참조
using UnityEngine.InputSystem.UI; // 새 Input System 기반 UI 입력 모듈 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조
using UnityEngine.UI; // 공통 디버그 Canvas UI 구성 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 F1 공통 디버그 페이지 자동 구성
    public static class Phase3Day7DebugPagerSetup // 기존 두 디버그 창을 하나의 F1 페이지형 창으로 통합
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 프로젝트 Input Action Asset 경로
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string OldBrightnessCanvasName = "BrightnessDebugCanvas"; // 제거할 기존 밝기 전용 Canvas 이름
        private const string DebugCanvasName = "DebugPageCanvas"; // 새 공통 F1 디버그 Canvas 이름
        private const string ReadyMarkerName = "===Day7 Debug Pager Ready==="; // 자동 적용 완료 마커 이름

        static Phase3Day7DebugPagerSetup() // 자동 구성 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 에디터 준비 뒤 입력과 씬 수정 예약
        }

        [MenuItem("Tools/Project I/Day 7/Apply F1 Debug Pager")] // 수동 통합 디버그 적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 구성 실행
        {
            bool inputChanged = EnsureDebugInputActions(); // F1·좌우 화살표 Input Action 구성

            if (inputChanged) // Input Action Asset이 재임포트되는지 확인
            {
                EditorApplication.delayCall += () => ApplySceneFix(true, true); // 재임포트 이후 씬 수정 예약
                return; // 현재 프레임 씬 수정 중단
            }

            ApplySceneFix(true, true); // 입력 구성이 최신이면 씬 즉시 강제 재구성
        }

        private static void TryAutoApply() // 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 작업 제외 상태 확인
            {
                return; // 자동 구성 중단
            }

            bool inputChanged = EnsureDebugInputActions(); // 디버그 Input Action 기본 키 구성

            if (inputChanged) // Input Action Asset이 변경되었는지 확인
            {
                EditorApplication.delayCall += () => ApplySceneFix(false, false); // 에셋 재임포트 후 씬 구성 예약
                return; // 현재 프레임 씬 작업 중단
            }

            ApplySceneFix(false, false); // 입력이 이미 최신이면 씬 자동 구성 실행
        }

        public static bool EnsureDebugInputActions() // F1·좌우 화살표를 재바인딩 가능한 Input Action으로 추가
        {
            if (!File.Exists(InputActionsPath)) // 프로젝트 Input Action 파일 존재 여부 확인
            {
                Debug.LogError($"[Project I] Input Action 파일을 찾을 수 없습니다: {InputActionsPath}"); // 파일 누락 오류 출력
                return false; // 입력 수정 없음 반환
            }

            string originalJson = File.ReadAllText(InputActionsPath); // 현재 Input Action JSON 읽기
            InputActionAsset workingAsset = InputActionAsset.FromJson(originalJson); // Unity Input System API로 JSON 안전 파싱
            InputActionMap playerMap = workingAsset.FindActionMap(GameplayInputActions.Map, true); // 기존 Player 액션 맵 조회
            EnsureButtonAction(playerMap, GameplayInputActions.DebugToggle, "<Keyboard>/f1"); // F1 기본값의 공통 디버그 창 토글 액션 확보
            EnsureButtonAction(playerMap, GameplayInputActions.DebugPreviousPage, "<Keyboard>/leftArrow"); // 왼쪽 화살표 기본값의 이전 페이지 액션 확보
            EnsureButtonAction(playerMap, GameplayInputActions.DebugNextPage, "<Keyboard>/rightArrow"); // 오른쪽 화살표 기본값의 다음 페이지 액션 확보
            string updatedJson = workingAsset.ToJson(); // Unity Input System이 생성한 유효 JSON 직렬화
            Object.DestroyImmediate(workingAsset); // 임시 InputActionAsset 메모리 정리

            if (NormalizeJson(originalJson) == NormalizeJson(updatedJson)) // 실질적인 입력 구성 변경 여부 확인
            {
                return false; // 변경 없음 반환
            }

            File.WriteAllText(InputActionsPath, updatedJson); // 새 디버그 액션이 포함된 Input Action 파일 저장
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate); // 변경된 Input Action Asset 강제 재임포트
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            Debug.Log("[Project I] F1 / LeftArrow / RightArrow 디버그 Input Actions 구성 완료"); // 입력 구성 완료 로그 출력
            return true; // 입력 파일 변경 반환
        }

        private static void ApplySceneFix(bool showDialog, bool force) // 기존 두 디버그 창을 하나의 공통 페이지형 Canvas로 통합
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬 수정 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 적용된 씬인지 확인
            {
                return; // 중복 자동 적용 방지
            }

            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회

            if (player == null) // 플레이어 존재 여부 확인
            {
                Debug.LogError("[Project I] F1 Debug Pager 구성에 필요한 Player를 찾을 수 없습니다."); // 플레이어 누락 오류 출력
                return; // 씬 수정 중단
            }

            PlayerInputReader inputReader = player.GetComponent<PlayerInputReader>(); // 재바인딩 가능한 입력 래퍼 조회
            PlayerDebugHud playerDebugPage = player.GetComponent<PlayerDebugHud>(); // 기존 플레이어 디버그 페이지 공급자 조회
            PlayerBrightnessSensor brightnessSensor = player.GetComponent<PlayerBrightnessSensor>(); // 7일차 밝기 센서 조회

            if (inputReader == null || playerDebugPage == null || brightnessSensor == null) // 선행 Day 4·6·7 구성 존재 여부 확인
            {
                Debug.LogError("[Project I] F1 Debug Pager 적용 전에 Day 4 PlayerDebugHud, Day 6 PlayerInputReader, Day 7 PlayerBrightnessSensor가 필요합니다."); // 선행 구성 누락 오류 출력
                return; // 씬 수정 중단
            }

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // 최신 Input Action Asset 로드
            inputReader.Configure(inputAsset); // 플레이어 입력 래퍼에 새 디버그 액션까지 다시 연결
            RemoveOldBrightnessCanvas(scene); // 기존 별도 밝기 디버그 Canvas 제거
            BrightnessDebugHud brightnessPage = GetOrAddComponent<BrightnessDebugHud>(player); // 플레이어 루트에 밝기 디버그 페이지 공급자 확보
            brightnessPage.Configure(brightnessSensor); // 현재 플레이어 밝기 센서 연결
            BuildDebugCanvas(scene, inputReader); // F1로 열고 좌우 페이지를 이동하는 단일 Canvas 생성
            EnsureEventSystem(scene); // 화면의 좌우 화살표 Button도 클릭 가능하도록 EventSystem 확보
            EnsureMarker(scene); // 자동 적용 완료 마커 확보
            EditorUtility.SetDirty(inputReader); // 입력 래퍼 변경 저장 대상으로 표시
            EditorUtility.SetDirty(playerDebugPage); // 플레이어 디버그 공급자 변경 저장 대상으로 표시
            EditorUtility.SetDirty(brightnessPage); // 밝기 디버그 공급자 변경 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 변경된 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 프로젝트 에셋 변경 저장
            bool validationPassed = Phase3Day7DebugPagerValidator.Validate(false); // 통합 디버그 구조 자동 검증

            if (showDialog) // 수동 적용 결과 안내 여부 확인
            {
                string message = validationPassed ? "F1 공통 디버그 창과 좌우 페이지 전환 구성이 완료되었습니다." : "F1 Debug Pager 검증 실패 - Console을 확인하세요."; // 결과 안내 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 적용 결과 대화상자 표시
            }

            Debug.Log("[Project I] Day 7 F1 Debug Pager 적용 완료"); // 통합 디버그 적용 완료 로그 출력
        }

        private static void RemoveOldBrightnessCanvas(Scene scene) // 기존 별도 밝기 디버그 Canvas 제거
        {
            GameObject oldCanvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == OldBrightnessCanvasName); // 기존 밝기 Canvas 조회

            if (oldCanvas != null) // 기존 별도 Canvas 존재 여부 확인
            {
                Object.DestroyImmediate(oldCanvas); // 중복 화면을 제거하기 위해 기존 밝기 Canvas 삭제
            }
        }

        private static void BuildDebugCanvas(Scene scene, PlayerInputReader inputReader) // 단일 F1 디버그 Canvas와 페이지 UI 생성
        {
            GameObject existingCanvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == DebugCanvasName); // 기존 공통 디버그 Canvas 조회

            if (existingCanvas != null) // 기존 Canvas 존재 여부 확인
            {
                Object.DestroyImmediate(existingCanvas); // 정확한 레이아웃 재적용을 위해 기존 Canvas 제거
            }

            GameObject canvasObject = new GameObject(DebugCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); // 공통 디버그 Canvas 생성
            Canvas canvas = canvasObject.GetComponent<Canvas>(); // Canvas 컴포넌트 조회
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 화면 위 Overlay 방식 지정
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); // Canvas Scaler 조회
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 해상도에 따른 UI 크기 보정 사용
            scaler.referenceResolution = new Vector2(1920f, 1080f); // 기준 해상도 설정
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; // 화면 비율 혼합 보정 방식 지정
            scaler.matchWidthOrHeight = 0.5f; // 가로·세로 균형 보정 설정
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 기본 런타임 폰트 조회

            GameObject window = new GameObject("DebugWindow", typeof(RectTransform), typeof(Image)); // 실제 F1 디버그 창 패널 생성
            RectTransform windowRect = window.GetComponent<RectTransform>(); // 디버그 창 RectTransform 조회
            windowRect.SetParent(canvasObject.transform, false); // Canvas 아래에 디버그 창 연결
            windowRect.anchorMin = new Vector2(0f, 1f); // 화면 왼쪽 위 기준 앵커 설정
            windowRect.anchorMax = new Vector2(0f, 1f); // 화면 왼쪽 위 기준 앵커 설정
            windowRect.pivot = new Vector2(0f, 1f); // 왼쪽 위 피벗 지정
            windowRect.anchoredPosition = new Vector2(24f, -24f); // 화면 모서리에서 약간 안쪽으로 배치
            windowRect.sizeDelta = new Vector2(500f, 360f); // 두 기존 디버그 창 내용을 담을 공통 크기 설정
            Image background = window.GetComponent<Image>(); // 디버그 창 배경 Image 조회
            background.color = new Color(0.04f, 0.04f, 0.04f, 0.90f); // 게임 화면을 적당히 비치는 어두운 배경 적용

            Text title = CreateText(windowRect, "PageTitle", string.Empty, font, 22, TextAnchor.MiddleCenter, new Vector2(72f, -12f), new Vector2(310f, 42f)); // 현재 페이지 제목 Text 생성
            Text counter = CreateText(windowRect, "PageCounter", "0 / 0", font, 16, TextAnchor.MiddleCenter, new Vector2(382f, -12f), new Vector2(62f, 42f)); // 현재 페이지 번호 Text 생성
            Button previous = CreateArrowButton(windowRect, "PreviousPageButton", "<", font, new Vector2(12f, -12f)); // 왼쪽 이전 페이지 Button 생성
            Button next = CreateArrowButton(windowRect, "NextPageButton", ">", font, new Vector2(446f, -12f)); // 오른쪽 다음 페이지 Button 생성
            Text content = CreateText(windowRect, "PageContent", string.Empty, font, 18, TextAnchor.UpperLeft, new Vector2(24f, -70f), new Vector2(452f, 236f)); // 현재 디버그 페이지 본문 Text 생성
            Text footer = CreateText(windowRect, "Footer", "F1 : Debug ON/OFF    ← / → : Page", font, 14, TextAnchor.MiddleCenter, new Vector2(24f, -316f), new Vector2(452f, 28f)); // 공통 조작 안내 Text 생성

            DebugPageManager manager = canvasObject.AddComponent<DebugPageManager>(); // F1 토글과 페이지 순환 관리자 추가
            manager.Configure(inputReader, window, title, counter, content, previous, next, footer); // 관리자와 입력·UI 참조 전체 연결
            EditorUtility.SetDirty(manager); // 관리자 구성 변경 저장 대상으로 표시
        }

        private static Text CreateText(RectTransform parent, string objectName, string initialText, Font font, int fontSize, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size) // 공통 Text UI 생성
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text)); // Text UI 오브젝트 생성
            RectTransform rect = textObject.GetComponent<RectTransform>(); // Text RectTransform 조회
            rect.SetParent(parent, false); // 지정 UI 부모 아래에 Text 연결
            rect.anchorMin = new Vector2(0f, 1f); // 창 왼쪽 위 기준 앵커 설정
            rect.anchorMax = new Vector2(0f, 1f); // 창 왼쪽 위 기준 앵커 설정
            rect.pivot = new Vector2(0f, 1f); // 왼쪽 위 피벗 지정
            rect.anchoredPosition = anchoredPosition; // 요청 위치 적용
            rect.sizeDelta = size; // 요청 크기 적용
            Text text = textObject.GetComponent<Text>(); // Text 컴포넌트 조회
            text.text = initialText; // 초기 문자열 지정
            text.font = font; // Unity 기본 런타임 폰트 지정
            text.fontSize = fontSize; // 요청 폰트 크기 적용
            text.alignment = alignment; // 요청 텍스트 정렬 적용
            text.color = Color.white; // 디버그 가독성을 위한 흰색 적용
            text.raycastTarget = false; // Text 자체가 버튼 클릭을 막지 않도록 Raycast 비활성화
            return text; // 생성한 Text 반환
        }

        private static Button CreateArrowButton(RectTransform parent, string objectName, string label, Font font, Vector2 anchoredPosition) // 이전·다음 페이지 화살표 Button 생성
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button)); // Button 기본 오브젝트 생성
            RectTransform rect = buttonObject.GetComponent<RectTransform>(); // Button RectTransform 조회
            rect.SetParent(parent, false); // 디버그 창 아래에 Button 연결
            rect.anchorMin = new Vector2(0f, 1f); // 창 왼쪽 위 기준 앵커 설정
            rect.anchorMax = new Vector2(0f, 1f); // 창 왼쪽 위 기준 앵커 설정
            rect.pivot = new Vector2(0f, 1f); // 왼쪽 위 피벗 지정
            rect.anchoredPosition = anchoredPosition; // 요청된 헤더 위치 적용
            rect.sizeDelta = new Vector2(44f, 42f); // 클릭하기 쉬운 화살표 Button 크기 지정
            Image image = buttonObject.GetComponent<Image>(); // Button 배경 Image 조회
            image.color = new Color(0.18f, 0.18f, 0.18f, 0.96f); // 헤더와 구분되는 Button 배경 적용
            Button button = buttonObject.GetComponent<Button>(); // Button 컴포넌트 조회
            Text labelText = CreateText(rect, "Label", label, font, 24, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(44f, 42f)); // Button 내부 화살표 문자열 생성
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f); // Button 전체 영역 기준 최소 앵커 설정
            labelText.rectTransform.anchorMax = new Vector2(1f, 1f); // Button 전체 영역 기준 최대 앵커 설정
            labelText.rectTransform.pivot = new Vector2(0.5f, 0.5f); // Button 중심 피벗 지정
            labelText.rectTransform.anchoredPosition = Vector2.zero; // Button 중앙에 화살표 배치
            labelText.rectTransform.sizeDelta = Vector2.zero; // Button 크기 전체에 Text Stretch 적용
            return button; // 생성한 Button 반환
        }

        private static void EnsureEventSystem(Scene scene) // 마우스로 좌우 화살표 Button도 사용할 수 있게 UI EventSystem 확보
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(); // 현재 활성 EventSystem 조회

            if (eventSystem == null) // EventSystem이 아직 없는지 확인
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); // 새 Input System 기반 EventSystem 생성
                eventSystem = eventSystemObject.GetComponent<EventSystem>(); // 생성한 EventSystem 참조 획득
                InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>(); // 새 UI 입력 모듈 조회
                inputModule.AssignDefaultActions(); // 마우스·키보드 기본 UI 액션 자동 연결
                return; // 새 EventSystem 구성이 끝났으므로 종료
            }

            BaseInputModule existingModule = eventSystem.GetComponent<BaseInputModule>(); // 기존 입력 모듈 존재 여부 확인

            if (existingModule == null) // EventSystem만 있고 입력 모듈이 없는지 확인
            {
                InputSystemUIInputModule inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>(); // 새 Input System UI 입력 모듈 추가
                inputModule.AssignDefaultActions(); // 마우스·키보드 기본 UI 액션 자동 연결
            }
        }

        private static void EnsureButtonAction(InputActionMap map, string actionName, string bindingPath) // 재바인딩 가능한 버튼 Input Action과 기본 키 확보
        {
            InputAction action = map.FindAction(actionName, false); // 기존 액션 조회

            if (action == null) // 액션 미생성 확인
            {
                action = map.AddAction(actionName, InputActionType.Button); // 새 버튼 액션 생성
            }

            bool bindingExists = action.bindings.Any(binding => binding.path == bindingPath); // 동일 기본 바인딩 존재 여부 확인

            if (!bindingExists) // 지정 기본 키가 아직 없는지 확인
            {
                action.AddBinding(bindingPath, groups: "Keyboard&Mouse"); // 키보드 기본 바인딩 추가
            }
        }

        private static string NormalizeJson(string json) // JSON 포맷 차이를 제외한 비교 문자열 생성
        {
            return string.Concat(json.Where(character => !char.IsWhiteSpace(character))); // 모든 공백 문자를 제거해 실질 구성 비교
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 기존 또는 새 컴포넌트 확보
        {
            T component = target.GetComponent<T>(); // 기존 컴포넌트 조회
            return component != null ? component : target.AddComponent<T>(); // 없으면 새로 추가한 뒤 반환
        }

        private static void EnsureMarker(Scene scene) // F1 Debug Pager 자동 적용 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 완료 마커 숨김
            }
        }
    }
}
