using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // Canvas 씬 소속 확인 기능 참조
using UnityEngine.UI; // Canvas UI Image와 Text 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    public sealed class QuickSlotHud : MonoBehaviour // Canvas 기반 빠른 슬롯 6칸 HUD 갱신
    {
        public const string CanvasName = "PlayerHUDCanvas"; // 빠른 슬롯 Canvas 이름 (Day6 구성 규칙)
        private const string PanelName = "QuickSlotPanel"; // 빠른 슬롯 패널 이름
        [SerializeField] private PlayerInventory inventory; // 표시할 플레이어 인벤토리
        [SerializeField] private Image[] slotBackgrounds = new Image[PlayerInventory.Capacity]; // 슬롯 배경 Image 목록
        [SerializeField] private Text[] slotNumbers = new Text[PlayerInventory.Capacity]; // 왼쪽 위 슬롯 번호 Text 목록
        [SerializeField] private Text[] itemNames = new Text[PlayerInventory.Capacity]; // 슬롯 중앙 아이템 이름 Text 목록
        [SerializeField] private Text[] lockLabels = new Text[PlayerInventory.Capacity]; // 양손 잠금 표시 Text 목록
        [SerializeField] private Color normalColor = new Color(0.07f, 0.07f, 0.07f, 0.86f); // 일반 슬롯 배경색
        [SerializeField] private Color selectedColor = new Color(0.42f, 0.42f, 0.42f, 0.96f); // 선택 슬롯 배경색
        [SerializeField] private Color lockedColor = new Color(0.28f, 0.12f, 0.12f, 0.96f); // 양손 잠금 슬롯 배경색

        private void Awake() // HUD 초기화
        {
            if (inventory == null) // 인벤토리 참조 누락 확인
            {
                inventory = GetComponent<PlayerInventory>(); // 같은 플레이어의 인벤토리 자동 조회
            }

            Refresh(); // 시작 화면 상태 즉시 갱신
        }

        private void LateUpdate() // 인벤토리 처리 뒤 Canvas 상태 갱신
        {
            Refresh(); // 현재 슬롯 데이터와 선택 상태 반영
        }

        public void Configure(PlayerInventory targetInventory) // 기존 Day 6 Setup 호환용 인벤토리 지정
        {
            inventory = targetInventory; // HUD 대상 인벤토리 저장
        }

        public void Configure(PlayerInventory targetInventory, Image[] backgrounds, Text[] numbers, Text[] names, Text[] locks) // Canvas UI 참조 전체 지정
        {
            inventory = targetInventory; // HUD 대상 인벤토리 저장
            slotBackgrounds = backgrounds; // 슬롯 배경 Image 목록 저장
            slotNumbers = numbers; // 왼쪽 위 숫자 Text 목록 저장
            itemNames = names; // 아이템 이름 Text 목록 저장
            lockLabels = locks; // LOCK Text 목록 저장
            Refresh(); // 연결 직후 Canvas 내용 갱신
        }

        private void Refresh() // 빠른 슬롯 6칸 Canvas 내용 갱신
        {
            if (inventory == null) // 인벤토리 참조 누락 확인
            {
                return; // HUD 갱신 중단
            }

            EnsureSlotCanvas(); // 슬롯 UI가 없거나 환경 씬에 묶여 있으면 플레이어 씬에 확보

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 1번부터 6번 슬롯 순회
            {
                WorldItem item = inventory.GetItem(index); // 현재 슬롯 아이템 조회
                bool selected = index == inventory.SelectedIndex; // 현재 선택 슬롯 여부 확인
                bool locked = selected && inventory.IsSelectionLocked; // 선택 슬롯 양손 잠금 여부 확인

                if (slotNumbers != null && index < slotNumbers.Length && slotNumbers[index] != null) // 슬롯 번호 Text 참조 유효성 확인
                {
                    slotNumbers[index].text = (index + 1).ToString(); // 슬롯 왼쪽 위에 1~6 숫자 표시
                }

                if (itemNames != null && index < itemNames.Length && itemNames[index] != null) // 아이템 이름 Text 참조 유효성 확인
                {
                    itemNames[index].text = item == null ? string.Empty : item.DisplayName; // 빈 슬롯은 이름을 숨기고 아이템이 있으면 이름 표시
                }

                if (lockLabels != null && index < lockLabels.Length && lockLabels[index] != null) // LOCK Text 참조 유효성 확인
                {
                    lockLabels[index].text = locked ? "LOCK" : string.Empty; // 양손 운반 중 선택 슬롯에만 LOCK 표시
                }

                if (slotBackgrounds != null && index < slotBackgrounds.Length && slotBackgrounds[index] != null) // 슬롯 배경 Image 참조 유효성 확인
                {
                    slotBackgrounds[index].color = locked ? lockedColor : selected ? selectedColor : normalColor; // 잠금·선택·일반 상태에 맞는 배경색 적용
                }
            }
        }

        private void EnsureSlotCanvas() // 슬롯 UI가 항상 플레이어와 같은 Persistent 씬에 있도록 보장
        {
            if (HasBoundSlots() && itemNames[0].gameObject.scene == gameObject.scene) // 이미 같은 씬의 UI와 연결됐는지 확인
            {
                return; // 추가 처리 불필요
            }

            GameObject canvasObject = FindCanvasInScene(gameObject.scene); // 플레이어 씬의 HUD Canvas 우선 조회

            if (canvasObject == null) // 플레이어 씬에 HUD Canvas가 없는 경우
            {
                canvasObject = BuildCanvas(); // 환경 씬에 의존하지 않도록 플레이어 씬에 직접 생성
                Debug.LogWarning($"[Project I] {CanvasName}이 Persistent 씬에 없어 런타임에 생성했습니다. Tools > Project I > Day 26 > Move Player HUD 메뉴로 씬에 고정하세요.", this); // 구성 누락 안내
            }

            BindFromCanvas(canvasObject.transform); // 슬롯 UI 참조 연결
        }

        private bool HasBoundSlots() // 슬롯 UI 참조가 모두 살아 있는지 확인
        {
            if (itemNames == null || itemNames.Length < PlayerInventory.Capacity || slotBackgrounds == null || slotBackgrounds.Length < PlayerInventory.Capacity) // 배열 길이 확인
            {
                return false; // 연결 필요
            }

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 슬롯 순회
            {
                if (itemNames[index] == null || slotBackgrounds[index] == null) // 파괴·누락 참조 확인
                {
                    return false; // 연결 필요
                }
            }

            return true; // 모두 연결됨
        }

        private static GameObject FindCanvasInScene(Scene scene) // 지정 씬 루트에서 HUD Canvas 조회
        {
            if (!scene.IsValid() || !scene.isLoaded) // 씬 유효성 확인
            {
                return null; // 조회 불가
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (root.name == CanvasName && root.transform.Find(PanelName) != null) // HUD Canvas 구조 확인
                {
                    return root; // HUD Canvas 반환
                }
            }

            return null; // 없음
        }

        private void BindFromCanvas(Transform canvasRoot) // Canvas 하위 슬롯 오브젝트에서 참조 연결
        {
            Transform panel = canvasRoot.Find(PanelName); // 슬롯 패널 조회
            slotBackgrounds = new Image[PlayerInventory.Capacity]; // 배경 배열 생성
            slotNumbers = new Text[PlayerInventory.Capacity]; // 번호 배열 생성
            itemNames = new Text[PlayerInventory.Capacity]; // 이름 배열 생성
            lockLabels = new Text[PlayerInventory.Capacity]; // 잠금 배열 생성

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 슬롯 순회
            {
                Transform slot = panel == null ? null : panel.Find($"Slot_{index + 1}"); // 슬롯 조회

                if (slot == null) // 슬롯 누락 확인
                {
                    continue; // 다음 슬롯
                }

                slotBackgrounds[index] = slot.GetComponent<Image>(); // 배경 연결
                slotNumbers[index] = slot.Find("Number")?.GetComponent<Text>(); // 번호 연결
                itemNames[index] = slot.Find("ItemName")?.GetComponent<Text>(); // 이름 연결
                lockLabels[index] = slot.Find("Lock")?.GetComponent<Text>(); // 잠금 연결
            }
        }

        private GameObject BuildCanvas() // Day6과 같은 배치의 HUD Canvas를 플레이어 씬에 생성
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); // Canvas 생성
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene); // 플레이어와 같은 Persistent 씬 소속
            Canvas canvas = canvasObject.GetComponent<Canvas>(); // Canvas 조회
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 화면 위 표시
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); // Scaler 조회
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 해상도 비례 크기
            scaler.referenceResolution = new Vector2(1920f, 1080f); // 기준 해상도
            scaler.matchWidthOrHeight = 0.5f; // 가로·세로 혼합 보정
            RectTransform panel = new GameObject(PanelName, typeof(RectTransform)).GetComponent<RectTransform>(); // 패널 생성
            panel.SetParent(canvasObject.transform, false); // Canvas 아래 배치
            panel.anchorMin = new Vector2(0.5f, 0f); // 하단 중앙 앵커
            panel.anchorMax = new Vector2(0.5f, 0f); // 하단 중앙 앵커
            panel.pivot = new Vector2(0.5f, 0f); // 하단 중앙 피벗
            panel.anchoredPosition = new Vector2(0f, 28f); // 하단에서 약간 위
            panel.sizeDelta = new Vector2(568f, 72f); // 6칸 전체 크기
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 기본 런타임 폰트
            float slotWidth = 88f; // 슬롯 너비
            float gap = 8f; // 슬롯 간격
            float startX = (-(slotWidth * PlayerInventory.Capacity + gap * (PlayerInventory.Capacity - 1)) * 0.5f) + slotWidth * 0.5f; // 첫 슬롯 중심

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 6칸 생성
            {
                RectTransform slot = new GameObject($"Slot_{index + 1}", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); // 슬롯 생성
                slot.SetParent(panel, false); // 패널 아래 배치
                slot.sizeDelta = new Vector2(slotWidth, 72f); // 슬롯 크기
                slot.anchoredPosition = new Vector2(startX + index * (slotWidth + gap), 0f); // 슬롯 위치
                slot.GetComponent<Image>().raycastTarget = false; // 입력 차단 방지
                CreateText(slot, "Number", font, 16, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -5f), new Vector2(24f, 20f)); // 번호
                CreateText(slot, "ItemName", font, 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -22f)); // 이름
                CreateText(slot, "Lock", font, 12, TextAnchor.LowerCenter, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-12f, 18f)); // 잠금
            }

            return canvasObject; // 생성 Canvas 반환
        }

        private static void CreateText(RectTransform parent, string objectName, Font font, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta) // 슬롯 내부 Text 생성
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>(); // Text 오브젝트 생성
            rect.SetParent(parent, false); // 슬롯 아래 배치
            rect.anchorMin = anchorMin; // 최소 앵커
            rect.anchorMax = anchorMax; // 최대 앵커
            rect.pivot = pivot; // 피벗
            rect.anchoredPosition = anchoredPosition; // 위치
            rect.sizeDelta = sizeDelta; // 크기
            Text text = rect.GetComponent<Text>(); // Text 조회
            text.font = font; // 폰트
            text.fontSize = fontSize; // 글자 크기
            text.alignment = alignment; // 정렬
            text.color = Color.white; // 흰색
            text.raycastTarget = false; // 입력 차단 방지
        }
    }
}
