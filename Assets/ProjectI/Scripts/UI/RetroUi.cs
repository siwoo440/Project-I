using System; // 콜백
using ProjectI.Audio; // 효과음
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.EventSystems; // UI 입력 이벤트
using UnityEngine.InputSystem.UI; // 새 Input System UI 입력 모듈
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public static class RetroUi // 주황색 단말기 느낌의 uGUI 생성 도구 (메인 메뉴·서버·설정·일시정지 공용)
    {
        public static readonly Color Backdrop = new Color(0.03f, 0.02f, 0.015f, 0.9f); // 창 바탕
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.55f); // 뒤 화면 어둡게
        public static readonly Color Orange = new Color(0.96f, 0.5f, 0.24f); // 기본 글자
        public static readonly Color OrangeBright = new Color(1f, 0.72f, 0.42f); // 강조 글자
        public static readonly Color OrangeDim = new Color(0.62f, 0.32f, 0.17f); // 테두리·보조 글자
        public static readonly Color EntryFill = new Color(0.44f, 0.2f, 0.1f, 0.96f); // 목록 칸
        public static readonly Color EntryHover = new Color(0.58f, 0.28f, 0.14f, 1f); // 목록 칸 강조
        public static readonly Color Red = new Color(0.8f, 0.17f, 0.15f); // 빨강 (스크롤바·체크)
        public static readonly Color Green = new Color(0.22f, 0.78f, 0.36f); // 초록 (검색창)
        public static readonly Color Disabled = new Color(0.4f, 0.32f, 0.28f); // 비활성 글자
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f); // 투명
        private static Font font; // 공용 글꼴

        public static Font Font // 한글 가능한 OS 글꼴 (고정폭 D2Coding 우선)
        {
            get
            {
                if (font == null) // 처음
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "D2Coding", "Malgun Gothic", "맑은 고딕", "Consolas", "Arial" }, 32); // OS 글꼴

                    if (font == null) // OS 글꼴 없음
                    {
                        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 기본 글꼴
                    }
                }

                return font; // 반환
            }
        }

        public static void EnsureEventSystem() // UI 입력 처리기 보장
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) // 이미 있음
            {
                return; // 생략
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); // 현재 씬에 생성 (씬마다 새로 확인)
        }

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null) // 화면 전체 캔버스
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); // 생성
            canvasObject.transform.SetParent(parent, false); // 부모
            Canvas canvas = canvasObject.GetComponent<Canvas>(); // 캔버스
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 화면 위
            canvas.sortingOrder = sortingOrder; // 순서
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); // 배율
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 해상도 비례
            scaler.referenceResolution = new Vector2(1920f, 1080f); // 기준 해상도
            scaler.matchWidthOrHeight = 0.5f; // 가로·세로 절반씩
            return canvas; // 반환
        }

        public static RectTransform Rect(Transform parent, string name) // 부모 전체를 채우는 빈 영역
        {
            GameObject rectObject = new GameObject(name, typeof(RectTransform)); // 생성
            RectTransform rect = (RectTransform)rectObject.transform; // 영역
            rect.SetParent(parent, false); // 부모
            Stretch(rect); // 전체
            return rect; // 반환
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f) // 부모 채우기 (안쪽 여백)
        {
            rect.anchorMin = Vector2.zero; // 왼쪽 아래
            rect.anchorMax = Vector2.one; // 오른쪽 위
            rect.pivot = new Vector2(0.5f, 0.5f); // 가운데
            rect.offsetMin = new Vector2(left, bottom); // 여백
            rect.offsetMax = new Vector2(-right, -top); // 여백
            return rect; // 반환
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size) // 한 점 기준 배치
        {
            rect.anchorMin = anchor; // 기준점
            rect.anchorMax = anchor; // 기준점
            rect.pivot = pivot; // 중심
            rect.anchoredPosition = position; // 위치
            rect.sizeDelta = size; // 크기
            return rect; // 반환
        }

        public static Image Solid(Transform parent, string name, Color color, bool raycast = false) // 단색 이미지 (부모 채움)
        {
            RectTransform rect = Rect(parent, name); // 영역
            Image image = rect.gameObject.AddComponent<Image>(); // 이미지
            image.color = color; // 색
            image.raycastTarget = raycast; // 클릭 판정
            return image; // 반환
        }

        public static void Frame(RectTransform target, Color color, float width) // 사각 테두리 (선 4개)
        {
            RectTransform frame = Rect(target, "Frame"); // 묶음
            Edge(frame, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -width), Vector2.zero, color); // 위
            Edge(frame, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, width), color); // 아래
            Edge(frame, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 0f), color); // 왼쪽
            Edge(frame, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-width, 0f), Vector2.zero, color); // 오른쪽
        }

        public static void CornerBrackets(RectTransform target, Color color, float length, float width, float inset) // 화면 네 모서리 꺾쇠
        {
            RectTransform group = Rect(target, "CornerBrackets"); // 묶음

            for (int corner = 0; corner < 4; corner++) // 네 모서리
            {
                float x = corner % 2; // 0 왼쪽 · 1 오른쪽
                float y = corner / 2; // 0 아래 · 1 위
                Vector2 anchor = new Vector2(x, y); // 모서리
                float sx = x < 0.5f ? 1f : -1f; // 안쪽 방향
                float sy = y < 0.5f ? 1f : -1f; // 안쪽 방향
                Vector2 origin = new Vector2(sx * inset, sy * inset); // 꺾쇠 기준
                Bar(group, "H", anchor, origin + new Vector2(sx * length * 0.5f, sy * width * 0.5f), new Vector2(length, width), color); // 가로
                Bar(group, "V", anchor, origin + new Vector2(sx * width * 0.5f, sy * length * 0.5f), new Vector2(width, length), color); // 세로
            }
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor alignment) // 글자 (부모 채움)
        {
            RectTransform rect = Rect(parent, name); // 영역
            Text label = rect.gameObject.AddComponent<Text>(); // 글자
            label.font = Font; // 글꼴
            label.text = text; // 내용
            label.fontSize = size; // 크기
            label.color = color; // 색
            label.alignment = alignment; // 정렬
            label.horizontalOverflow = HorizontalWrapMode.Overflow; // 가로 넘침 허용
            label.verticalOverflow = VerticalWrapMode.Overflow; // 세로 넘침 허용
            label.raycastTarget = false; // 클릭 통과
            return label; // 반환
        }

        public static Button TextButton(Transform parent, string name, string text, int size, Action onClick, TextAnchor alignment = TextAnchor.MiddleLeft) // 글자만 있는 메뉴 버튼 (마우스를 올리면 "> " 표시)
        {
            RectTransform rect = Rect(parent, name); // 영역
            Image hit = rect.gameObject.AddComponent<Image>(); // 클릭 판정
            hit.color = Clear; // 투명
            Button button = rect.gameObject.AddComponent<Button>(); // 버튼
            button.transition = Selectable.Transition.None; // 색 전환은 RetroHover 담당
            Text label = Label(rect, "Label", text, size, Orange, alignment); // 글자
            RetroHover hover = rect.gameObject.AddComponent<RetroHover>(); // 강조
            hover.Configure(label, null, text, true); // 설정
            button.onClick.AddListener(() => { SoundPlayer.Play(SoundId.UiClick, 0.6f); onClick?.Invoke(); }); // 클릭
            return button; // 반환
        }

        public static Button BoxButton(Transform parent, string name, string text, int size, Action onClick) // 테두리 있는 버튼
        {
            RectTransform rect = Rect(parent, name); // 영역
            Image fill = rect.gameObject.AddComponent<Image>(); // 바탕
            fill.color = Clear; // 투명
            Button button = rect.gameObject.AddComponent<Button>(); // 버튼
            button.transition = Selectable.Transition.None; // 색 전환은 RetroHover 담당
            Frame(rect, OrangeDim, 2f); // 테두리
            Text label = Label(rect, "Label", text, size, Orange, TextAnchor.MiddleCenter); // 글자
            RetroHover hover = rect.gameObject.AddComponent<RetroHover>(); // 강조
            hover.Configure(label, fill, text, false); // 설정
            button.onClick.AddListener(() => { SoundPlayer.Play(SoundId.UiClick, 0.6f); onClick?.Invoke(); }); // 클릭
            return button; // 반환
        }

        public static InputField InputBox(Transform parent, string name, string placeholder, int size, Color border) // 입력창
        {
            RectTransform rect = Rect(parent, name); // 영역
            Image fill = rect.gameObject.AddComponent<Image>(); // 바탕
            fill.color = new Color(0f, 0f, 0f, 0.35f); // 어두운 바탕
            Frame(rect, border, 2f); // 테두리
            Text text = Label(rect, "Text", string.Empty, size, OrangeBright, TextAnchor.MiddleLeft); // 입력 글자
            Stretch(text.rectTransform, 12f, 12f, 0f, 0f); // 여백
            text.horizontalOverflow = HorizontalWrapMode.Wrap; // 입력창 안에서 줄임
            text.supportRichText = false; // 태그 해석 안 함
            Text hint = Label(rect, "Placeholder", placeholder, size, Red, TextAnchor.MiddleLeft); // 안내
            Stretch(hint.rectTransform, 12f, 12f, 0f, 0f); // 여백
            hint.fontStyle = FontStyle.Italic; // 기울임
            InputField input = rect.gameObject.AddComponent<InputField>(); // 입력
            input.textComponent = text; // 글자
            input.placeholder = hint; // 안내
            input.targetGraphic = fill; // 강조 대상
            input.caretColor = OrangeBright; // 커서
            input.selectionColor = new Color(0.96f, 0.5f, 0.24f, 0.35f); // 선택
            input.characterLimit = 32; // 최대 길이
            return input; // 반환
        }

        public static Slider SliderBar(Transform parent, string name, float min, float max, float value) // 가로 슬라이더
        {
            RectTransform rect = Rect(parent, name); // 영역
            Image track = Solid(rect, "Track", new Color(0f, 0f, 0f, 0.45f)); // 바탕
            Stretch(track.rectTransform, 0f, 0f, 12f, 12f); // 가는 막대
            Frame(track.rectTransform, OrangeDim, 2f); // 테두리
            RectTransform fillArea = Rect(rect, "FillArea"); // 채움 영역
            Stretch(fillArea, 2f, 2f, 14f, 14f); // 막대 안쪽
            Image fill = Solid(fillArea, "Fill", Orange); // 채움
            RectTransform handleArea = Rect(rect, "HandleArea"); // 손잡이 영역
            Stretch(handleArea, 8f, 8f, 0f, 0f); // 여백
            Image handle = Solid(handleArea, "Handle", OrangeBright, true); // 손잡이
            handle.rectTransform.sizeDelta = new Vector2(14f, 0f); // 폭
            Slider slider = rect.gameObject.AddComponent<Slider>(); // 슬라이더
            slider.fillRect = fill.rectTransform; // 채움
            slider.handleRect = handle.rectTransform; // 손잡이
            slider.targetGraphic = handle; // 강조 대상
            slider.direction = Slider.Direction.LeftToRight; // 방향
            slider.minValue = min; // 최소
            slider.maxValue = max; // 최대
            slider.SetValueWithoutNotify(value); // 값
            return slider; // 반환
        }

        public static ScrollRect ScrollList(Transform parent, string name, float spacing, out RectTransform content) // 세로 목록 (오른쪽 빨간 스크롤바)
        {
            RectTransform rect = Rect(parent, name); // 영역
            ScrollRect scroll = rect.gameObject.AddComponent<ScrollRect>(); // 스크롤
            RectTransform viewport = Rect(rect, "Viewport"); // 보이는 영역
            Stretch(viewport, 0f, 30f, 0f, 0f); // 스크롤바 자리
            viewport.gameObject.AddComponent<RectMask2D>(); // 잘라내기
            Image viewportHit = viewport.gameObject.AddComponent<Image>(); // 휠·드래그 판정
            viewportHit.color = Clear; // 투명
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); // 목록 내용
            content.SetParent(viewport, false); // 부모
            content.anchorMin = new Vector2(0f, 1f); // 위쪽 기준
            content.anchorMax = new Vector2(1f, 1f); // 위쪽 기준
            content.pivot = new Vector2(0.5f, 1f); // 위쪽 기준
            content.offsetMin = Vector2.zero; // 여백
            content.offsetMax = Vector2.zero; // 여백
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); // 세로 배치
            layout.spacing = spacing; // 간격
            layout.padding = new RectOffset(0, 0, 0, 0); // 여백
            layout.childControlWidth = true; // 폭 맞춤
            layout.childControlHeight = true; // 높이 맞춤
            layout.childForceExpandWidth = true; // 폭 채움
            layout.childForceExpandHeight = false; // 높이 유지
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>(); // 내용 높이
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; // 선호 높이

            RectTransform barRect = Rect(rect, "Scrollbar"); // 스크롤바
            barRect.anchorMin = new Vector2(1f, 0f); // 오른쪽
            barRect.anchorMax = new Vector2(1f, 1f); // 오른쪽
            barRect.pivot = new Vector2(1f, 0.5f); // 오른쪽
            barRect.offsetMin = new Vector2(-14f, 0f); // 폭
            barRect.offsetMax = Vector2.zero; // 여백
            Image barBack = barRect.gameObject.AddComponent<Image>(); // 홈
            barBack.color = new Color(0.35f, 0.07f, 0.06f, 0.9f); // 어두운 빨강
            RectTransform slidingArea = Rect(barRect, "SlidingArea"); // 손잡이 영역
            Image handle = Solid(slidingArea, "Handle", Red, true); // 손잡이
            Scrollbar bar = barRect.gameObject.AddComponent<Scrollbar>(); // 스크롤바
            bar.handleRect = handle.rectTransform; // 손잡이
            bar.targetGraphic = handle; // 강조 대상
            bar.direction = Scrollbar.Direction.BottomToTop; // 방향

            scroll.viewport = viewport; // 보이는 영역
            scroll.content = content; // 내용
            scroll.horizontal = false; // 가로 없음
            scroll.vertical = true; // 세로
            scroll.movementType = ScrollRect.MovementType.Clamped; // 끝에서 멈춤
            scroll.scrollSensitivity = 30f; // 휠 속도
            scroll.verticalScrollbar = bar; // 스크롤바
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; // 항상 표시
            return scroll; // 반환
        }

        private static void Edge(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color) // 테두리 선
        {
            Image line = Solid(parent, name, color); // 선
            line.rectTransform.anchorMin = anchorMin; // 기준
            line.rectTransform.anchorMax = anchorMax; // 기준
            line.rectTransform.offsetMin = offsetMin; // 두께
            line.rectTransform.offsetMax = offsetMax; // 두께
        }

        private static void Bar(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color) // 꺾쇠 막대
        {
            Image bar = Solid(parent, name, color); // 막대
            Place(bar.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, size); // 배치
        }
    }

    public sealed class RetroHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler // 마우스를 올리면 강조
    {
        private Text label; // 글자
        private Image fill; // 바탕 (없으면 글자만)
        private string text; // 원래 글자
        private bool arrow; // "> " 표시 여부
        private bool hovered; // 강조 중
        private Selectable selectable; // 버튼

        public void Configure(Text targetLabel, Image targetFill, string baseText, bool showArrow) // 설정
        {
            label = targetLabel; // 글자
            fill = targetFill; // 바탕
            text = baseText; // 글자
            arrow = showArrow; // 표시
            selectable = GetComponent<Selectable>(); // 버튼
            Refresh(); // 반영
        }

        public void SetText(string baseText) // 글자 바꾸기
        {
            text = baseText; // 저장
            Refresh(); // 반영
        }

        public void OnPointerEnter(PointerEventData eventData) // 올림
        {
            hovered = true; // 강조
            Refresh(); // 반영

            if (selectable == null || selectable.IsInteractable()) // 누를 수 있음
            {
                SoundPlayer.Play(SoundId.UiHover, 0.35f); // 짧은 소리
            }
        }

        public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(); } // 내림
        public void OnSelect(BaseEventData eventData) { hovered = true; Refresh(); } // 선택
        public void OnDeselect(BaseEventData eventData) { hovered = false; Refresh(); } // 해제

        private void OnDisable() // 숨김
        {
            hovered = false; // 초기화
            Refresh(); // 반영
        }

        private void Update() // 버튼 활성 상태 변화 반영
        {
            if (selectable != null && label != null && label.color != CurrentColor()) // 색이 다름
            {
                Refresh(); // 반영
            }
        }

        private Color CurrentColor() // 현재 글자색
        {
            bool interactable = selectable == null || selectable.IsInteractable(); // 활성
            return !interactable ? RetroUi.Disabled : hovered ? (fill != null ? RetroUi.Backdrop : RetroUi.OrangeBright) : RetroUi.Orange; // 색
        }

        private void Refresh() // 반영
        {
            if (label == null) // 설정 전
            {
                return; // 생략
            }

            bool interactable = selectable == null || selectable.IsInteractable(); // 활성
            bool active = hovered && interactable; // 강조
            label.color = CurrentColor(); // 색
            label.text = arrow ? (active ? "> " + text : "  " + text) : text; // 화살표

            if (fill != null) // 바탕
            {
                fill.color = active ? RetroUi.Orange : RetroUi.Clear; // 강조 시 채움
            }
        }
    }
}
