using System.Collections.Generic; // 선택 목록 사용
using ProjectI.Interaction; // 플레이어 상호작용 참조
using ProjectI.Audio; // 효과음
using ProjectI.Items; // WorldItem·인벤토리 참조
using ProjectI.UI; // 정식 창 모양
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public sealed class OfficeSalePanel : MonoBehaviour // 판매대 위 물건 중 팔 것을 고르는 창 (32일차 기능 · 36일차 정식 창 모양)
    {
        private const float RowHeight = 52f; // 줄 높이

        private readonly List<WorldItem> items = new List<WorldItem>(); // 판매대 위 물건
        private readonly List<bool> selected = new List<bool>(); // 선택 여부
        private OfficeSaleCounter counter; // 대상 판매대
        private PlayerInteractor interactor; // 연 플레이어
        private Canvas canvas; // 창 캔버스
        private RectTransform listContent; // 목록 내용
        private ScrollRect listScroll; // 목록 스크롤
        private Text summaryLabel; // 선택 합계
        private Button sellButton; // 판매 버튼
        private RetroHover sellHover; // 판매 버튼 글자
        private readonly List<Text> rowLabels = new List<Text>(); // 줄 글자
        private int builtCount = -1; // 목록을 만든 물건 수
        private int openedFrame = -1; // 연 프레임 (같은 프레임 Esc 판정 방지)

        public bool IsOpen { get; private set; } // 열림 여부
        public IReadOnlyList<WorldItem> Items => items; // 목록 공개 (검증용)
        public int LastSaleTotal { get; private set; } // 마지막 판매 합계 (검증용)

        public void Open(OfficeSaleCounter targetCounter, PlayerInteractor targetInteractor) // 창 열기
        {
            if (IsOpen || targetCounter == null) // 중복·대상 확인
            {
                return; // 중단
            }

            counter = targetCounter; // 판매대
            interactor = targetInteractor; // 플레이어
            items.Clear(); // 목록 초기화
            selected.Clear(); // 선택 초기화

            foreach (WorldItem item in counter.CollectPlacedItems()) // 판매대 위 물건
            {
                items.Add(item); // 등록
                selected.Add(true); // 기본은 전부 선택
            }

            if (items.Count == 0) // 팔 물건 없음
            {
                return; // 열지 않음
            }

            if (PlayerControlLock.IsLocked) // 다른 창이 열려 있음
            {
                return; // 열지 않음
            }

            IsOpen = true; // 열림
            openedFrame = Time.frameCount; // 연 프레임
            BlockPlayer(true); // 플레이어 조작 잠시 정지
            ShowView(); // 창 표시
        }

        public void SetSelected(int index, bool value) // 선택 변경 (검증·외부용)
        {
            if (index >= 0 && index < selected.Count) // 범위
            {
                selected[index] = value; // 적용
            }
        }

        public int ConfirmSale() // 고른 물건 판매 후 창 닫기
        {
            List<WorldItem> toSell = new List<WorldItem>(); // 판매 대상

            for (int index = 0; index < items.Count; index++) // 목록 순회
            {
                if (selected[index] && items[index] != null) // 선택됨
                {
                    toSell.Add(items[index]); // 등록
                }
            }

            LastSaleTotal = counter == null ? 0 : counter.Sell(toSell); // 판매
            Close(); // 닫기
            return LastSaleTotal; // 합계
        }

        public void Close() // 판매 없이 닫기
        {
            if (!IsOpen) // 이미 닫힘
            {
                return; // 중단
            }

            IsOpen = false; // 닫힘
            BlockPlayer(false); // 조작 복구
            items.Clear(); // 정리
            selected.Clear(); // 정리

            if (canvas != null) // 창
            {
                canvas.gameObject.SetActive(false); // 숨김
            }
        }

        private void Update() // 창이 열린 동안 상태 확인
        {
            if (!IsOpen) // 닫힘
            {
                return; // 중단
            }

            for (int index = items.Count - 1; index >= 0; index--) // 사라지거나 다시 집어 간 물건 제거
            {
                WorldItem item = items[index]; // 물건

                if (item == null || !item.gameObject.activeInHierarchy || item.IsHeld || item.IsStored) // 판매대에서 없어짐
                {
                    items.RemoveAt(index); // 목록 제거
                    selected.RemoveAt(index); // 선택 제거 (같은 위치)
                }
            }

            if (items.Count == 0 || counter == null) // 팔 물건이 없어짐
            {
                Close(); // 닫기
                return; // 중단
            }

            if (Time.frameCount > openedFrame + 1 && Cursor.lockState == CursorLockMode.Locked) // Esc로 커서를 다시 잠그면 취소로 처리
            {
                Close(); // 닫기
                return; // 중단
            }

            RefreshView(); // 창 내용
        }

        private void OnDisable() // 비활성화 시 조작 복구
        {
            Close(); // 닫기
        }

        private void ShowView() // 창 열기 (처음이면 구성)
        {
            BuildView(); // 구성
            RetroUi.EnsureEventSystem(); // UI 입력
            canvas.gameObject.SetActive(true); // 표시
            builtCount = -1; // 목록 다시 만들기
            RefreshView(); // 내용
            listScroll.verticalNormalizedPosition = 1f; // 맨 위
        }

        private void RefreshView() // 목록·합계 갱신
        {
            if (canvas == null || !IsOpen || counter == null) // 닫힘
            {
                return; // 생략
            }

            if (builtCount != items.Count) // 물건 수가 바뀜 (집어 감 등)
            {
                RebuildRows(); // 다시 만들기
            }

            int total = 0; // 합계
            int count = 0; // 선택 수

            for (int index = 0; index < items.Count; index++) // 물건
            {
                WorldItem item = items[index]; // 물건
                int price = item == null ? 0 : counter.PriceOf(item); // 금액
                rowLabels[index].text = $"[{(selected[index] ? "X" : " ")}]  {(item == null ? "-" : item.DisplayName)}"; // 선택 표시

                if (selected[index] && item != null) // 선택됨
                {
                    total += price; // 합계
                    count++; // 수
                }
            }

            int funds = counter.Economy == null ? 0 : counter.Economy.SharedFunds; // 자금
            summaryLabel.text = $"선택 {count}개  ·  합계 {total:N0}  ·  공동 자금 {funds:N0} → {funds + total:N0}"; // 합계
            sellButton.interactable = count > 0; // 선택이 있어야 판매
            sellHover.SetText($"판매 ({total:N0})"); // 버튼 글자
        }

        private void RebuildRows() // 목록 줄 만들기
        {
            for (int index = listContent.childCount - 1; index >= 0; index--) // 이전 줄
            {
                Transform child = listContent.GetChild(index); // 줄
                child.SetParent(null, false); // 분리
                Destroy(child.gameObject); // 제거
            }

            rowLabels.Clear(); // 정리

            for (int index = 0; index < items.Count; index++) // 물건
            {
                int captured = index; // 캡처
                RectTransform row = RetroUi.Rect(listContent, $"Row_{index}"); // 줄
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight; // 높이
                Image fill = row.gameObject.AddComponent<Image>(); // 바탕
                fill.color = Color.white; // 버튼 색이 곱함
                Button button = row.gameObject.AddComponent<Button>(); // 줄 전체가 선택 버튼
                ColorBlock colors = button.colors; // 색
                colors.normalColor = RetroUi.EntryFill; // 기본
                colors.highlightedColor = RetroUi.EntryHover; // 올림
                colors.selectedColor = RetroUi.EntryFill; // 선택
                colors.pressedColor = RetroUi.OrangeDim; // 누름
                button.colors = colors; // 적용
                button.targetGraphic = fill; // 대상
                button.onClick.AddListener(() => ToggleRow(captured)); // 전환
                RetroUi.Frame(row, RetroUi.OrangeDim, 2f); // 테두리
                Text label = RetroUi.Label(row, "Name", string.Empty, 22, RetroUi.OrangeBright, TextAnchor.MiddleLeft); // 이름
                RetroUi.Stretch(label.rectTransform, 16f, 180f, 0f, 0f); // 왼쪽
                WorldItem item = items[index]; // 물건
                Text price = RetroUi.Label(row, "Price", item == null ? string.Empty : $"{counter.PriceOf(item):N0}", 22, RetroUi.Orange, TextAnchor.MiddleRight); // 금액
                RetroUi.Stretch(price.rectTransform, 0f, 20f, 0f, 0f); // 오른쪽
                rowLabels.Add(label); // 등록
            }

            builtCount = items.Count; // 기록
        }

        private void ToggleRow(int index) // 줄 선택 전환
        {
            if (index >= 0 && index < selected.Count) // 범위
            {
                selected[index] = !selected[index]; // 전환
                SoundPlayer.Play(SoundId.UiClick, 0.5f); // 소리
            }
        }

        private void BuildView() // 창 구성 (처음 한 번)
        {
            if (canvas != null) // 이미 있음
            {
                return; // 생략
            }

            canvas = RetroUi.CreateCanvas("SalePanelCanvas", 300, transform); // 캔버스
            RectTransform root = (RectTransform)canvas.transform; // 루트
            RetroUi.Solid(root, "Shade", RetroUi.Shade, true); // 뒤 화면
            RectTransform window = RetroUi.Rect(root, "Window"); // 창
            RetroUi.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 720f)); // 가운데
            RetroUi.Solid(window, "Fill", RetroUi.Backdrop, true); // 바탕
            RetroUi.Frame(window, RetroUi.Orange, 3f); // 테두리

            Text title = RetroUi.Label(window, "Title", "판매할 물건을 고르세요", 34, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, -46f), new Vector2(600f, 50f)); // 위
            Button all = RetroUi.BoxButton(window, "SelectAll", "전체 선택", 22, () => SetAll(true)); // 전체 선택
            RetroUi.Place((RectTransform)all.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, -110f), new Vector2(180f, 46f)); // 위치
            Button none = RetroUi.BoxButton(window, "SelectNone", "전체 해제", 22, () => SetAll(false)); // 전체 해제
            RetroUi.Place((RectTransform)none.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(232f, -110f), new Vector2(180f, 46f)); // 위치

            RectTransform listFrame = RetroUi.Rect(window, "ListFrame"); // 목록 틀
            RetroUi.Stretch(listFrame, 36f, 36f, 150f, 170f); // 여백
            RetroUi.Frame(listFrame, RetroUi.Red, 2f); // 테두리
            listScroll = RetroUi.ScrollList(listFrame, "List", 8f, out listContent); // 목록
            RetroUi.Stretch((RectTransform)listScroll.transform, 14f, 10f, 12f, 12f); // 안쪽

            summaryLabel = RetroUi.Label(window, "Summary", string.Empty, 22, RetroUi.OrangeBright, TextAnchor.MiddleLeft); // 합계
            RetroUi.Place(summaryLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(36f, 130f), new Vector2(780f, 34f)); // 아래
            sellButton = RetroUi.BoxButton(window, "Sell", "판매", 26, () => ConfirmSale()); // 판매
            RetroUi.Place((RectTransform)sellButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-250f, 60f), new Vector2(240f, 58f)); // 오른쪽 아래
            sellHover = sellButton.GetComponent<RetroHover>(); // 글자
            Button cancel = RetroUi.BoxButton(window, "Cancel", "취소", 26, Close); // 취소
            RetroUi.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-36f, 60f), new Vector2(190f, 58f)); // 오른쪽 아래
            canvas.gameObject.SetActive(false); // 처음엔 숨김
        }

        private void SetAll(bool value) // 전체 선택·해제
        {
            for (int index = 0; index < selected.Count; index++) // 순회
            {
                selected[index] = value; // 적용
            }
        }

        private void BlockPlayer(bool block) // 창이 열린 동안 플레이어 조작 정지 (공용 잠금)
        {
            if (block) // 잠그기
            {
                PlayerControlLock.Acquire(this, interactor); // 잠금
                return; // 종료
            }

            PlayerControlLock.Release(this); // 해제
        }
    }
}
