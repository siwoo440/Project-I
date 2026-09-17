using System.Collections.Generic; // 선택 목록 사용
using ProjectI.Interaction; // 플레이어 상호작용 참조
using ProjectI.Items; // WorldItem·인벤토리 참조
using ProjectI.Player; // 플레이어 이동·시점 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public sealed class OfficeSalePanel : MonoBehaviour // 판매대 위 물건 중 팔 것을 고르는 창 (32일차, 정식 HUD 전까지 즉시 모드 GUI)
    {
        private const float PanelWidth = 440f; // 창 너비
        private const float RowHeight = 30f; // 줄 높이

        private readonly List<WorldItem> items = new List<WorldItem>(); // 판매대 위 물건
        private readonly List<bool> selected = new List<bool>(); // 선택 여부
        private OfficeSaleCounter counter; // 대상 판매대
        private PlayerInteractor interactor; // 연 플레이어
        private PlayerInventory inventory; // 잠시 끌 인벤토리 입력
        private PlayerMovement movement; // 잠시 멈출 이동
        private float savedSpeed = 1f; // 원래 이동 배율
        private bool savedSprint = true; // 원래 달리기 허용
        private Vector2 scroll; // 목록 스크롤
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

            IsOpen = true; // 열림
            openedFrame = Time.frameCount; // 연 프레임
            scroll = Vector2.zero; // 스크롤 초기화
            BlockPlayer(true); // 플레이어 조작 잠시 정지
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
            }
        }

        private void OnDisable() // 비활성화 시 조작 복구
        {
            Close(); // 닫기
        }

        private void OnGUI() // 선택 창 그리기
        {
            if (!IsOpen || counter == null) // 닫힘
            {
                return; // 중단
            }

            float listHeight = Mathf.Min(items.Count, 8) * RowHeight; // 목록 높이
            float height = 150f + listHeight; // 창 높이
            Rect area = new Rect((Screen.width - PanelWidth) * 0.5f, (Screen.height - height) * 0.5f, PanelWidth, height); // 화면 중앙
            GUI.Box(area, string.Empty); // 배경
            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 10f, area.width - 28f, area.height - 20f)); // 안쪽
            GUILayout.Label("판매할 물건을 고르세요"); // 제목

            GUILayout.BeginHorizontal(); // 전체 선택 줄
            if (GUILayout.Button("전체 선택", GUILayout.Width(100f))) // 전체 선택
            {
                SetAll(true); // 적용
            }

            if (GUILayout.Button("전체 해제", GUILayout.Width(100f))) // 전체 해제
            {
                SetAll(false); // 적용
            }
            GUILayout.EndHorizontal(); // 줄 끝

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(listHeight + 6f)); // 목록
            int total = 0; // 선택 합계
            int count = 0; // 선택 수

            for (int index = 0; index < items.Count; index++) // 물건 순회
            {
                WorldItem item = items[index]; // 물건

                if (item == null) // 사라짐
                {
                    continue; // 다음
                }

                int price = counter.PriceOf(item); // 금액
                selected[index] = GUILayout.Toggle(selected[index], $"  {item.DisplayName}   {price}", GUILayout.Height(RowHeight - 4f)); // 선택 토글

                if (selected[index]) // 선택됨
                {
                    total += price; // 합계
                    count++; // 수
                }
            }

            GUILayout.EndScrollView(); // 목록 끝
            GUILayout.Label($"선택 {count}개 / 합계 {total}   (공동 자금 {(counter.Economy == null ? 0 : counter.Economy.SharedFunds)})"); // 합계

            GUILayout.BeginHorizontal(); // 버튼 줄
            GUI.enabled = count > 0; // 선택이 있어야 판매
            if (GUILayout.Button($"판매 ({total})", GUILayout.Height(32f))) // 판매
            {
                ConfirmSale(); // 판매
            }

            GUI.enabled = true; // 복구
            if (GUILayout.Button("취소", GUILayout.Height(32f))) // 취소
            {
                Close(); // 닫기
            }
            GUILayout.EndHorizontal(); // 줄 끝
            GUILayout.EndArea(); // 안쪽 끝
        }

        private void SetAll(bool value) // 전체 선택·해제
        {
            for (int index = 0; index < selected.Count; index++) // 순회
            {
                selected[index] = value; // 적용
            }
        }

        private void BlockPlayer(bool block) // 창이 열린 동안 플레이어 조작 정지
        {
            if (block) // 정지
            {
                inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 인벤토리
                movement = interactor == null ? null : interactor.GetComponent<PlayerMovement>(); // 이동

                if (movement != null) // 이동 정지
                {
                    savedSpeed = movement.ExternalSpeedMultiplier; // 원래 배율
                    savedSprint = movement.ExternalSprintAllowed; // 원래 달리기
                    movement.SetExternalMovementModifier(0f, false); // 정지 (중력은 유지)
                }
            }
            else if (movement != null) // 이동 복구
            {
                movement.SetExternalMovementModifier(savedSpeed, savedSprint); // 원래 값
            }

            if (inventory != null) // 슬롯 전환·버리기 입력
            {
                inventory.enabled = !block; // 켜고 끔
            }

            if (interactor != null) // F 입력
            {
                interactor.enabled = !block; // 켜고 끔
            }

            Cursor.lockState = block ? CursorLockMode.None : CursorLockMode.Locked; // 창이 열린 동안 커서 표시 (시점 회전도 멈춤)
            Cursor.visible = block; // 표시
        }
    }
}
