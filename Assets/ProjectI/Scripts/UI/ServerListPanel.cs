using System; // 이벤트
using System.Collections; // 코루틴
using System.Collections.Generic; // 목록
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Enter 입장
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class ServerListPanel : MonoBehaviour // 서버 목록 창 (검색 · 도전 원정 · 정렬 · 새로고침 · 참가)
    {
        private const float FadeDuration = 0.25f; // 나타나는 시간
        private const float LoadingDelay = 0.45f; // 새로고침 연출 시간
        private IServerListProvider provider; // 목록 공급자
        private CanvasGroup group; // 투명도
        private InputField searchField; // 검색
        private RetroHover challengeHover; // 도전 원정 표시
        private RetroHover sortHover; // 정렬 표시
        private Button refreshButton; // 새로고침
        private RectTransform content; // 목록 내용
        private ScrollRect scroll; // 스크롤
        private Text statusLabel; // 상태 줄
        private readonly List<ServerListing> listings = new List<ServerListing>(); // 받은 목록
        private bool includeChallenge = true; // 도전 원정 포함
        private ServerSortMode sortMode = ServerSortMode.Worldwide; // 정렬
        private Coroutine fadeRoutine; // 나타나기
        private Coroutine refreshRoutine; // 새로고침
        private InputField addressField; // 참가 코드 또는 주소 (41일차)
        private RetroHover visibilityHover; // 공개 범위 표시
        private bool publicRoom = true; // 공개 방 (끄면 코드 전용)
        private InputField portField; // 포트
        private Button hostButton; // 방 만들기
        private Button joinButton; // 참가
        private bool connecting; // 연결 진행 중

        public event Action Closed; // 닫힘 (메뉴로 돌아가기)
        public event Action<ushort, bool> HostRequested; // 방 만들기 (포트, 공개 방 여부)
        public event Action<string, ushort> JoinRequested; // 입장 (코드 또는 주소, 포트 칸)
        public bool IsOpen => gameObject.activeSelf; // 열림 여부
        public int ShownCount => content == null ? 0 : content.childCount; // 표시된 서버 수 (검증용)
        public string StatusText => statusLabel == null ? string.Empty : statusLabel.text; // 상태 줄 (검증용)
        public bool PublicRoom => publicRoom; // 공개 방 선택 (검증용)

        public static ServerListPanel Create(Transform canvas, IServerListProvider listProvider) // 생성 (처음엔 닫힘)
        {
            RectTransform root = RetroUi.Rect(canvas, "ServerListPanel"); // 전체 화면
            ServerListPanel panel = root.gameObject.AddComponent<ServerListPanel>(); // 창
            panel.provider = listProvider ?? new SampleServerListProvider(); // 공급자
            panel.Build(root); // 구성
            root.gameObject.SetActive(false); // 닫힘
            return panel; // 반환
        }

        public void Open() // 열기
        {
            gameObject.SetActive(true); // 표시
            transform.SetAsLastSibling(); // 맨 앞

            if (fadeRoutine != null) // 이전 연출
            {
                StopCoroutine(fadeRoutine); // 정지
            }

            fadeRoutine = StartCoroutine(FadeIn()); // 나타나기
            Refresh(); // 목록 요청
        }

        public void Close() // 닫기
        {
            if (!IsOpen) // 이미 닫힘
            {
                return; // 생략
            }

            gameObject.SetActive(false); // 숨김 (코루틴도 멈춤)
            refreshRoutine = null; // 정리
            fadeRoutine = null; // 정리
            Closed?.Invoke(); // 알림
        }

        public void Refresh() // 새로고침
        {
            if (!isActiveAndEnabled) // 닫힘
            {
                return; // 생략
            }

            if (refreshRoutine != null) // 진행 중
            {
                StopCoroutine(refreshRoutine); // 다시 시작
            }

            refreshRoutine = StartCoroutine(RefreshRoutine()); // 요청
        }

        public void ShowStatus(string text) // 상태 줄 문구 (연결 진행 등)
        {
            if (statusLabel != null && !string.IsNullOrEmpty(text)) // 있음
            {
                statusLabel.text = text; // 표시
            }
        }

        public void SetConnecting(bool value) // 연결 중에는 방 만들기·참가를 막음
        {
            connecting = value; // 기록

            if (hostButton != null) // 버튼
            {
                hostButton.interactable = !value; // 방 만들기
                joinButton.interactable = !value; // 참가
            }
        }

        private ushort ReadPort() // 포트 입력 (잘못되면 기본값)
        {
            return ushort.TryParse(portField.text, out ushort port) && port > 1024 ? port : (ushort)7777; // 포트
        }

        private void RequestHost() // 방 만들기
        {
            if (!connecting) // 대기 중 아님
            {
                HostRequested?.Invoke(ReadPort(), publicRoom); // 알림
            }
        }

        public void SetJoinInput(string text) // 코드·주소 입력 지정 (검증용)
        {
            addressField.text = text ?? string.Empty; // 입력
        }

        private void ToggleVisibility() // 공개 ↔ 코드 전용
        {
            publicRoom = !publicRoom; // 전환
            RefreshToggleLabels(); // 표시
        }

        private void RequestJoin() // 코드 또는 주소로 입장
        {
            if (!connecting) // 대기 중 아님
            {
                JoinRequested?.Invoke(addressField.text, ReadPort()); // 알림
            }
        }

        public void SetSearch(string text) // 검색어 지정 (검증용)
        {
            searchField.SetTextWithoutNotify(text ?? string.Empty); // 입력
            Rebuild(); // 반영
        }

        private IEnumerator FadeIn() // 나타나기
        {
            for (float time = 0f; time < FadeDuration; time += Time.unscaledDeltaTime) // 진행
            {
                group.alpha = time / FadeDuration; // 투명도
                yield return null; // 다음 프레임
            }

            group.alpha = 1f; // 완료
            fadeRoutine = null; // 정리
        }

        private IEnumerator RefreshRoutine() // 목록 요청
        {
            refreshButton.interactable = false; // 중복 방지
            statusLabel.text = "서버 목록을 불러오는 중..."; // 안내
            ClearEntries(); // 비우기
            yield return new WaitForSecondsRealtime(LoadingDelay); // 연출
            bool received = false; // 응답 여부
            provider.Request(result => // 요청
            {
                listings.Clear(); // 비우기

                if (result != null) // 응답
                {
                    listings.AddRange(result); // 저장
                }

                received = true; // 완료
            });

            while (!received) // 응답 대기 (온라인 공급자는 비동기)
            {
                yield return null; // 다음 프레임
            }

            refreshButton.interactable = true; // 다시 허용
            refreshRoutine = null; // 정리
            Rebuild(); // 표시
        }

        private void Rebuild() // 필터·정렬 적용 후 다시 그리기
        {
            ClearEntries(); // 비우기
            List<ServerListing> shown = ServerListFilter.Apply(listings, searchField.text, includeChallenge, sortMode); // 적용

            foreach (ServerListing listing in shown) // 표시
            {
                CreateEntry(listing); // 한 줄
            }

            scroll.verticalNormalizedPosition = 1f; // 맨 위
            string source = provider.IsOnline ? string.Empty : " · 예시 목록 (온라인 연결 전)"; // 출처
            statusLabel.text = shown.Count == 0 ? $"조건에 맞는 서버가 없습니다{source}" : $"서버 {shown.Count}개{source}"; // 상태
        }

        private void ClearEntries() // 목록 비우기
        {
            for (int index = content.childCount - 1; index >= 0; index--) // 뒤에서부터
            {
                Transform child = content.GetChild(index); // 줄
                child.SetParent(null, false); // 즉시 개수에서 빠짐
                Destroy(child.gameObject); // 제거
            }
        }

        private void Join(ServerListing listing) // 참가
        {
            statusLabel.text = $"'{listing.Name}' 에 연결하는 중..."; // 안내
            provider.Join(listing, (success, message) => // 요청
            {
                if (statusLabel != null) // 창이 남아 있음
                {
                    statusLabel.text = message; // 결과
                }
            });
        }

        private void Build(RectTransform root) // 화면 구성
        {
            group = root.gameObject.AddComponent<CanvasGroup>(); // 투명도
            Image shade = RetroUi.Solid(root, "Backdrop", RetroUi.Backdrop, true); // 뒤 화면 가림 (클릭 차단)
            shade.color = new Color(RetroUi.Backdrop.r, RetroUi.Backdrop.g, RetroUi.Backdrop.b, 0.8f); // 뒤 건물이 살짝 보임

            Text title = RetroUi.Label(root, "Title", "서버", 46, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(120f, -78f), new Vector2(200f, 60f)); // 왼쪽 위

            searchField = RetroUi.InputBox(root, "Search", "서버 이름 검색...", 22, RetroUi.Green); // 검색
            RetroUi.Place((RectTransform)searchField.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(330f, -78f), new Vector2(340f, 50f)); // 위치
            searchField.onValueChanged.AddListener(_ => Rebuild()); // 입력마다 반영

            Button challenge = RetroUi.BoxButton(root, "ChallengeToggle", string.Empty, 22, ToggleChallenge); // 도전 원정
            RetroUi.Place((RectTransform)challenge.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(700f, -78f), new Vector2(320f, 50f)); // 위치
            challengeHover = challenge.GetComponent<RetroHover>(); // 표시

            Button sort = RetroUi.BoxButton(root, "Sort", string.Empty, 22, CycleSort); // 정렬
            RetroUi.Place((RectTransform)sort.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1050f, -78f), new Vector2(300f, 50f)); // 위치
            sortHover = sort.GetComponent<RetroHover>(); // 표시

            refreshButton = RetroUi.TextButton(root, "Refresh", "[ 새로고침 ]", 24, Refresh, TextAnchor.MiddleCenter); // 새로고침
            RetroUi.Place((RectTransform)refreshButton.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-120f, -78f), new Vector2(240f, 50f)); // 오른쪽 위

            RectTransform listFrame = RetroUi.Rect(root, "ListFrame"); // 목록 테두리
            RetroUi.Stretch(listFrame, 110f, 110f, 130f, 230f); // 여백 (아래에 방 만들기·참가 줄)
            RetroUi.Solid(listFrame, "Fill", new Color(0f, 0f, 0f, 0.35f)); // 바탕
            RetroUi.Frame(listFrame, RetroUi.Red, 3f); // 빨간 테두리
            scroll = RetroUi.ScrollList(listFrame, "List", 10f, out content); // 목록
            RetroUi.Stretch((RectTransform)scroll.transform, 26f, 18f, 22f, 22f); // 안쪽 여백

            statusLabel = RetroUi.Label(root, "Status", string.Empty, 20, RetroUi.OrangeDim, TextAnchor.MiddleRight); // 상태 줄
            RetroUi.Place(statusLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-120f, 90f), new Vector2(1200f, 30f)); // 아래 오른쪽

            hostButton = RetroUi.BoxButton(root, "Host", "[ 방 만들기 ]", 24, RequestHost); // 방 만들기 (현재 저장으로 방장 시작)
            RetroUi.Place((RectTransform)hostButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(110f, 170f), new Vector2(230f, 54f)); // 왼쪽
            Button visibility = RetroUi.BoxButton(root, "Visibility", string.Empty, 22, ToggleVisibility); // 41일차: 공개 범위
            RetroUi.Place((RectTransform)visibility.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(355f, 170f), new Vector2(230f, 54f)); // 방 만들기 옆
            visibilityHover = visibility.GetComponent<RetroHover>(); // 표시
            Text addressTitle = RetroUi.Label(root, "AddressTitle", "코드·주소", 22, RetroUi.Orange, TextAnchor.MiddleRight); // 입력 제목
            RetroUi.Place(addressTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(595f, 170f), new Vector2(120f, 54f)); // 위치
            addressField = RetroUi.InputBox(root, "Address", "K7Q-2MX 또는 127.0.0.1", 22, RetroUi.Green); // 코드 또는 주소 (빈칸이면 같은 컴퓨터)
            RetroUi.Place((RectTransform)addressField.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(725f, 170f), new Vector2(320f, 54f)); // 위치
            addressField.characterLimit = 64; // 길이
            addressField.onEndEdit.AddListener(_ => { if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) RequestJoin(); }); // Enter 로 입장
            Text portTitle = RetroUi.Label(root, "PortTitle", ":", 26, RetroUi.Orange, TextAnchor.MiddleCenter); // 구분
            RetroUi.Place(portTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(1050f, 170f), new Vector2(20f, 54f)); // 위치
            portField = RetroUi.InputBox(root, "Port", "7777", 22, RetroUi.Green); // 포트 (주소일 때만 사용)
            RetroUi.Place((RectTransform)portField.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(1075f, 170f), new Vector2(110f, 54f)); // 위치
            portField.text = "7777"; // 기본 포트
            portField.contentType = InputField.ContentType.IntegerNumber; // 숫자만
            portField.characterLimit = 5; // 길이
            joinButton = RetroUi.BoxButton(root, "Join", "[ 입장 ]", 24, RequestJoin); // 코드·주소 입장
            RetroUi.Place((RectTransform)joinButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(1205f, 170f), new Vector2(180f, 54f)); // 위치

            Button back = RetroUi.TextButton(root, "Back", "메뉴로 돌아가기", 26, Close); // 돌아가기
            RetroUi.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(120f, 90f), new Vector2(360f, 50f)); // 왼쪽 아래

            RefreshToggleLabels(); // 표시
        }

        private void CreateEntry(ServerListing listing) // 서버 한 줄
        {
            RectTransform row = RetroUi.Rect(content, $"Server_{listing.Id}"); // 줄
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>(); // 높이
            layout.preferredHeight = 66f; // 높이
            Image fill = row.gameObject.AddComponent<Image>(); // 바탕
            fill.color = Color.white; // 색은 버튼 색 전환이 곱함
            Button button = row.gameObject.AddComponent<Button>(); // 줄 전체가 참가 버튼
            ColorBlock colors = button.colors; // 색
            colors.normalColor = RetroUi.EntryFill; // 기본
            colors.highlightedColor = RetroUi.EntryHover; // 올림
            colors.selectedColor = RetroUi.EntryHover; // 선택
            colors.pressedColor = RetroUi.OrangeDim; // 누름
            colors.disabledColor = new Color(0.22f, 0.14f, 0.1f, 0.9f); // 가득 참
            colors.colorMultiplier = 1f; // 배율
            colors.fadeDuration = 0.06f; // 전환
            button.colors = colors; // 적용
            button.targetGraphic = fill; // 대상
            button.interactable = !listing.IsFull; // 가득 차면 끔
            ServerListing captured = listing; // 캡처
            button.onClick.AddListener(() => Join(captured)); // 참가
            RetroUi.Frame(row, RetroUi.OrangeDim, 2f); // 테두리

            Color textColor = listing.IsFull ? RetroUi.Disabled : RetroUi.Orange; // 글자색
            string tag = listing.ChallengeMode ? "  [도전]" : string.Empty; // 도전 원정 표시
            Text name = RetroUi.Label(row, "Name", listing.Name + tag, 22, textColor, TextAnchor.UpperLeft); // 이름
            RetroUi.Stretch(name.rectTransform, 18f, 360f, 8f, 30f); // 왼쪽 위
            Text count = RetroUi.Label(row, "Players", $"{listing.Players} / {listing.MaxPlayers}", 20, textColor, TextAnchor.LowerLeft); // 인원
            RetroUi.Stretch(count.rectTransform, 18f, 360f, 30f, 8f); // 왼쪽 아래
            Text ping = RetroUi.Label(row, "Ping", $"{listing.Region}  {listing.PingMs}ms", 20, ServerListFilter.PingColor(listing.PingMs), TextAnchor.MiddleRight); // 지연
            RetroUi.Stretch(ping.rectTransform, 0f, 200f, 0f, 0f); // 가운데 오른쪽
            Text join = RetroUi.Label(row, "Join", listing.IsFull ? "가득 참" : "참가", 24, textColor, TextAnchor.MiddleRight); // 참가
            RetroUi.Stretch(join.rectTransform, 0f, 40f, 0f, 0f); // 오른쪽
        }

        private void ToggleChallenge() // 도전 원정 포함 전환
        {
            includeChallenge = !includeChallenge; // 전환
            RefreshToggleLabels(); // 표시
            Rebuild(); // 반영
        }

        private void CycleSort() // 정렬 순환
        {
            sortMode = (ServerSortMode)(((int)sortMode + 1) % Enum.GetValues(typeof(ServerSortMode)).Length); // 다음
            RefreshToggleLabels(); // 표시
            Rebuild(); // 반영
        }

        private void RefreshToggleLabels() // 버튼 글자
        {
            challengeHover.SetText($"도전 원정 포함 [{(includeChallenge ? "X" : " ")}]"); // 체크
            sortHover.SetText($"정렬: {ServerListFilter.Describe(sortMode)} ▾"); // 정렬
            visibilityHover.SetText(publicRoom ? "공개 방 [X]" : "코드 전용 [ ]"); // 공개 범위
        }
    }
}
