using System.Collections.Generic; // 현재 디버그 페이지 목록 기능 참조
using ProjectI.Player; // 재바인딩 가능한 플레이어 입력 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // Canvas Text와 Button 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class DebugPageManager : MonoBehaviour // F1 디버그 창 표시와 좌우 페이지 이동을 관리
    {
        [SerializeField] private PlayerInputReader inputReader; // F1·좌우 화살표 Input Action을 제공하는 입력 래퍼
        [SerializeField] private GameObject windowRoot; // 실제 디버그 창 전체 루트
        [SerializeField] private Text titleText; // 현재 페이지 이름 Text
        [SerializeField] private Text pageCounterText; // 현재 페이지 번호 Text
        [SerializeField] private Text contentText; // 현재 페이지 내용 Text
        [SerializeField] private Button previousButton; // 이전 페이지 마우스 버튼
        [SerializeField] private Button nextButton; // 다음 페이지 마우스 버튼
        [SerializeField] private Text footerText; // F1·좌우 화살표 조작 안내 Text
        private readonly List<DebugPageProvider> pages = new List<DebugPageProvider>(); // 현재 등록된 디버그 페이지 정렬 목록
        private int currentPageIndex; // 현재 표시 중인 페이지 인덱스
        private bool isOpen; // 현재 F1 디버그 창 표시 여부

        public bool IsOpen => isOpen; // 다른 개발 도구에서 확인할 디버그 창 열림 상태 공개
        public int PageCount => pages.Count; // 현재 등록된 디버그 페이지 수 공개
        public int CurrentPageIndex => currentPageIndex; // 현재 페이지 인덱스 공개

        private void Awake() // 디버그 페이지 관리자 초기화
        {
            if (inputReader == null) // 입력 래퍼 참조 누락 확인
            {
                inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 현재 씬 플레이어 입력 래퍼 자동 조회
            }

            previousButton?.onClick.AddListener(ShowPreviousPage); // 왼쪽 UI 화살표에 이전 페이지 이동 연결
            nextButton?.onClick.AddListener(ShowNextPage); // 오른쪽 UI 화살표에 다음 페이지 이동 연결
            RefreshPages(); // 현재 활성화된 모든 DebugPageProvider 수집
            SetOpen(false); // 게임 시작 시 디버그 창은 숨김 상태로 시작
        }

        private void OnEnable() // 관리자 활성화 처리
        {
            DebugPageRegistry.PagesChanged += HandlePagesChanged; // 이후 추가되는 디버그 페이지를 자동 반영하도록 변경 이벤트 구독
        }

        private void OnDisable() // 관리자 비활성화 처리
        {
            DebugPageRegistry.PagesChanged -= HandlePagesChanged; // 페이지 변경 이벤트 구독 해제
        }

        private void Update() // 재바인딩 가능한 디버그 입력 처리
        {
            if (inputReader != null && inputReader.DebugTogglePressed) // F1 기본값의 DebugToggle 액션 입력 확인
            {
                ToggleWindow(); // 디버그 창 표시 상태 반전
            }

            if (!isOpen) // 디버그 창이 닫혀 있는지 확인
            {
                return; // 페이지 이동 입력은 닫힌 상태에서 무시
            }

            if (inputReader != null && inputReader.DebugPreviousPagePressed) // 왼쪽 화살표 기본값의 이전 페이지 입력 확인
            {
                ShowPreviousPage(); // 이전 디버그 페이지 표시
            }

            if (inputReader != null && inputReader.DebugNextPagePressed) // 오른쪽 화살표 기본값의 다음 페이지 입력 확인
            {
                ShowNextPage(); // 다음 디버그 페이지 표시
            }
        }

        private void LateUpdate() // 각 시스템 상태 갱신 뒤 디버그 내용 표시
        {
            if (!isOpen) // 디버그 창이 닫혀 있는지 확인
            {
                return; // 숨겨진 창의 문자열 갱신 비용 생략
            }

            RefreshCurrentContent(); // 현재 페이지에서 최신 디버그 문자열을 다시 생성
        }

        public void Configure(PlayerInputReader reader, GameObject targetWindow, Text targetTitle, Text targetCounter, Text targetContent, Button targetPreviousButton, Button targetNextButton, Text targetFooter) // 에디터 자동 구성용 UI 참조 지정
        {
            inputReader = reader; // 재바인딩 가능한 입력 래퍼 저장
            windowRoot = targetWindow; // 디버그 창 루트 저장
            titleText = targetTitle; // 제목 Text 저장
            pageCounterText = targetCounter; // 페이지 번호 Text 저장
            contentText = targetContent; // 내용 Text 저장
            previousButton = targetPreviousButton; // 이전 페이지 Button 저장
            nextButton = targetNextButton; // 다음 페이지 Button 저장
            footerText = targetFooter; // 조작 안내 Text 저장

            if (footerText != null) // 조작 안내 Text 존재 여부 확인
            {
                footerText.text = "F1 : Debug ON/OFF    ← / → : Page"; // 공통 디버그 창 조작법 표시
            }

            if (windowRoot != null) // 창 루트 존재 여부 확인
            {
                windowRoot.SetActive(false); // 에디터 저장 상태에서도 처음에는 숨겨진 창으로 유지
            }
        }

        public void ToggleWindow() // F1 입력으로 디버그 창 열기·닫기
        {
            SetOpen(!isOpen); // 현재 열림 상태 반전 적용
        }

        public void ShowPreviousPage() // 현재 목록의 이전 페이지 표시
        {
            if (pages.Count == 0) // 표시 가능한 페이지 존재 여부 확인
            {
                return; // 페이지 이동 중단
            }

            currentPageIndex = (currentPageIndex - 1 + pages.Count) % pages.Count; // 목록 처음에서 뒤로 가면 마지막 페이지로 순환
            RefreshCurrentContent(); // 새 현재 페이지를 즉시 화면에 반영
        }

        public void ShowNextPage() // 현재 목록의 다음 페이지 표시
        {
            if (pages.Count == 0) // 표시 가능한 페이지 존재 여부 확인
            {
                return; // 페이지 이동 중단
            }

            currentPageIndex = (currentPageIndex + 1) % pages.Count; // 목록 마지막에서 앞으로 가면 첫 페이지로 순환
            RefreshCurrentContent(); // 새 현재 페이지를 즉시 화면에 반영
        }

        public void RefreshPages() // 중앙 Registry의 현재 디버그 페이지 목록 다시 수집
        {
            DebugPageProvider previousPage = GetCurrentPage(); // 목록 갱신 전 현재 페이지 참조 저장
            pages.Clear(); // 이전 페이지 목록 초기화
            pages.AddRange(DebugPageRegistry.CreateSortedSnapshot()); // 활성 페이지들을 정렬된 순서로 다시 등록

            if (pages.Count == 0) // 등록된 페이지가 하나도 없는지 확인
            {
                currentPageIndex = 0; // 안전한 기본 인덱스로 초기화
                RefreshCurrentContent(); // 페이지 없음 상태 UI 반영
                return; // 추가 인덱스 계산 중단
            }

            int previousIndex = previousPage == null ? -1 : pages.IndexOf(previousPage); // 기존 현재 페이지가 새 목록에도 존재하는지 확인
            currentPageIndex = previousIndex >= 0 ? previousIndex : Mathf.Clamp(currentPageIndex, 0, pages.Count - 1); // 가능하면 같은 페이지를 유지하고 아니면 유효 인덱스로 보정
            RefreshCurrentContent(); // 갱신된 목록과 현재 페이지를 UI에 반영
        }

        private void SetOpen(bool open) // 디버그 창 표시 상태 적용
        {
            isOpen = open; // 현재 표시 상태 저장

            if (isOpen) // 새 상태가 열림인지 확인
            {
                RefreshPages(); // 창을 열 때 최신 디버그 페이지 목록 다시 수집
            }

            if (windowRoot != null) // 디버그 창 루트 존재 여부 확인
            {
                windowRoot.SetActive(isOpen); // F1 상태에 맞춰 실제 Canvas 패널 표시 전환
            }
        }

        private void RefreshCurrentContent() // 현재 디버그 페이지 제목·번호·내용 갱신
        {
            DebugPageProvider currentPage = GetCurrentPage(); // 현재 인덱스의 페이지 조회

            if (currentPage == null) // 현재 표시할 페이지가 없는지 확인
            {
                SetText(titleText, "Debug"); // 기본 제목 표시
                SetText(pageCounterText, "0 / 0"); // 페이지 없음 번호 표시
                SetText(contentText, "등록된 DebugPageProvider가 없습니다."); // 페이지 없음 안내 표시
                return; // 일반 페이지 출력 중단
            }

            SetText(titleText, currentPage.PageName); // 현재 페이지 이름 표시
            SetText(pageCounterText, $"{currentPageIndex + 1} / {pages.Count}"); // 1 기반 현재 페이지 번호 표시
            SetText(contentText, currentPage.BuildDebugText()); // 현재 페이지 최신 디버그 내용 표시
        }

        private DebugPageProvider GetCurrentPage() // 현재 인덱스의 디버그 페이지 안전 조회
        {
            if (pages.Count == 0 || currentPageIndex < 0 || currentPageIndex >= pages.Count) // 목록과 인덱스 유효성 확인
            {
                return null; // 현재 페이지 없음 반환
            }

            return pages[currentPageIndex]; // 현재 페이지 반환
        }

        private void HandlePagesChanged() // 디버그 페이지 등록·해제 이벤트 처리
        {
            RefreshPages(); // 새 목록을 즉시 다시 수집
        }

        private static void SetText(Text target, string value) // Text null 검사 포함 공통 문자열 적용
        {
            if (target != null) // 대상 Text 존재 여부 확인
            {
                target.text = value; // 지정 문자열 표시
            }
        }
    }
}
