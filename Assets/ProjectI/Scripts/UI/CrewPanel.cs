using System; // 콜백
using System.Collections.Generic; // 목록
using ProjectI.Net; // 협동 세션
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class CrewPanel : MonoBehaviour // 42일차: 원정대원 관리 창 (목록·지연 · 방장: 내보내기·차단·방 잠그기)
    {
        private const float RefreshInterval = 1f; // 지연 갱신 간격
        private const float ConfirmSeconds = 3f; // 한 번 더 눌러 확인하는 시간
        private RectTransform content; // 목록 내용
        private Text titleLabel; // 제목
        private Text statusLabel; // 안내 줄
        private Button lockButton; // 방 잠그기
        private RetroHover lockHover; // 잠금 글자
        private Action onClosed; // 닫힘 콜백
        private float nextRefresh; // 다음 갱신
        private string armedAction = string.Empty; // 확인 대기 중인 동작 (번호|종류)
        private float armedUntil; // 확인 대기 끝
        private readonly Dictionary<ulong, Text> pingLabels = new Dictionary<ulong, Text>(); // 대원별 지연 글자 (제자리 갱신)
        private string shownSignature = string.Empty; // 표시 중인 목록 (번호·이름·잠금)

        public bool IsOpen => gameObject.activeSelf; // 열림 여부
        public int ShownCount => content == null ? 0 : content.childCount; // 검증용

        public static CrewPanel Create(Transform canvas) // 생성 (처음엔 닫힘)
        {
            RectTransform root = RetroUi.Rect(canvas, "CrewPanel"); // 전체 화면
            CrewPanel panel = root.gameObject.AddComponent<CrewPanel>(); // 창
            panel.Build(root); // 구성
            root.gameObject.SetActive(false); // 닫힘
            return panel; // 반환
        }

        public void Open(Action closed) // 열기
        {
            onClosed = closed; // 콜백
            gameObject.SetActive(true); // 표시
            transform.SetAsLastSibling(); // 맨 앞
            armedAction = string.Empty; // 확인 초기화
            statusLabel.text = NetworkSession.IsHost ? "방장: 버튼을 한 번 더 누르면 실행됩니다" : "방장만 원정대원을 관리할 수 있습니다"; // 안내
            NetworkSession.CrewChanged += Rebuild; // 목록 변화
            Rebuild(); // 목록
        }

        public void Close() // 닫기
        {
            if (!IsOpen) // 이미 닫힘
            {
                return; // 생략
            }

            NetworkSession.CrewChanged -= Rebuild; // 해제
            gameObject.SetActive(false); // 숨김
            Action callback = onClosed; // 콜백
            onClosed = null; // 정리
            callback?.Invoke(); // 알림
        }

        private void OnDestroy() // 파괴
        {
            NetworkSession.CrewChanged -= Rebuild; // 해제
        }

        private void Update() // 지연 갱신
        {
            if (Time.unscaledTime < nextRefresh) // 대기
            {
                return; // 생략
            }

            nextRefresh = Time.unscaledTime + RefreshInterval; // 다음

            if (!NetworkSession.IsOnline) // 연결 끊김
            {
                Close(); // 닫기
                return; // 종료
            }

            if (armedAction.Length > 0 && Time.unscaledTime > armedUntil) // 확인 시간 지남
            {
                armedAction = string.Empty; // 취소
                Rebuild(); // 버튼 글자 복구
                return; // 종료
            }

            List<NetworkSession.CrewEntry> crew = NetworkSession.GetCrew(); // 목록

            if (Signature(crew) != shownSignature) // 대원이 바뀜
            {
                Rebuild(); // 다시 그리기
                return; // 종료
            }

            foreach (NetworkSession.CrewEntry entry in crew) // 지연만 제자리 갱신 (버튼을 다시 만들지 않아 클릭이 끊기지 않음)
            {
                if (pingLabels.TryGetValue(entry.ClientId, out Text label) && label != null) // 글자
                {
                    label.text = PingText(entry.PingMs); // 지연
                    label.color = ServerListFilter.PingColor(entry.PingMs); // 색
                }
            }
        }

        private static string Signature(List<NetworkSession.CrewEntry> crew) // 목록 비교용 문자열
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(NetworkSession.RoomLocked ? "L" : "U"); // 잠금

            foreach (NetworkSession.CrewEntry entry in crew) // 대원
            {
                builder.Append('|').Append(entry.ClientId).Append(':').Append(entry.Name); // 번호·이름
            }

            return builder.ToString(); // 반환
        }

        private static string PingText(int pingMs) // 지연 글자
        {
            return pingMs > 0 ? $"{pingMs}ms" : "—"; // 모르면 대시
        }

        private void Build(RectTransform root) // 화면 구성
        {
            RetroUi.Solid(root, "Shade", RetroUi.Shade, true); // 뒤 화면 어둡게 (클릭 차단)
            RectTransform window = RetroUi.Rect(root, "Window"); // 창
            RetroUi.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 700f)); // 가운데
            RetroUi.Solid(window, "Fill", RetroUi.Backdrop, true); // 바탕
            RetroUi.Frame(window, RetroUi.Orange, 3f); // 테두리

            titleLabel = RetroUi.Label(window, "Title", "원정대원", 40, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, -50f), new Vector2(500f, 60f)); // 왼쪽 위

            lockButton = RetroUi.BoxButton(window, "Lock", string.Empty, 22, ToggleLock); // 방 잠그기
            RetroUi.Place((RectTransform)lockButton.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, -50f), new Vector2(300f, 50f)); // 오른쪽 위
            lockHover = lockButton.GetComponent<RetroHover>(); // 글자

            RectTransform listFrame = RetroUi.Rect(window, "ListFrame"); // 목록 테두리
            RetroUi.Stretch(listFrame, 40f, 40f, 100f, 150f); // 여백
            RetroUi.Frame(listFrame, RetroUi.OrangeDim, 2f); // 테두리
            ScrollRect scroll = RetroUi.ScrollList(listFrame, "List", 8f, out content); // 목록
            RetroUi.Stretch((RectTransform)scroll.transform, 16f, 10f, 12f, 12f); // 안쪽 여백

            statusLabel = RetroUi.Label(window, "Status", string.Empty, 20, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 안내
            RetroUi.Place(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(40f, 110f), new Vector2(900f, 30f)); // 아래

            Button back = RetroUi.TextButton(window, "Back", "돌아가기", 30, Close); // 돌아가기
            RetroUi.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(40f, 55f), new Vector2(300f, 50f)); // 왼쪽 아래
        }

        private void Rebuild() // 목록 다시 그리기
        {
            if (content == null || !IsOpen) // 닫힘
            {
                return; // 생략
            }

            for (int index = content.childCount - 1; index >= 0; index--) // 비우기
            {
                Transform child = content.GetChild(index); // 줄
                child.SetParent(null, false); // 즉시 개수에서 빠짐
                Destroy(child.gameObject); // 제거
            }

            List<NetworkSession.CrewEntry> crew = NetworkSession.GetCrew(); // 목록
            bool host = NetworkSession.IsHost; // 방장
            pingLabels.Clear(); // 글자 참조
            shownSignature = Signature(crew); // 표시 목록
            titleLabel.text = $"원정대원  {crew.Count}/{NetworkSession.MaxPlayers}"; // 제목
            lockButton.gameObject.SetActive(host); // 방장만
            lockHover.SetText(NetworkSession.RoomLocked ? "방 잠김 [X]" : "방 잠그기 [ ]"); // 잠금

            foreach (NetworkSession.CrewEntry entry in crew) // 대원
            {
                CreateRow(entry, host); // 한 줄
            }
        }

        private void CreateRow(NetworkSession.CrewEntry entry, bool host) // 대원 한 줄
        {
            RectTransform row = RetroUi.Rect(content, $"Crew_{entry.ClientId}"); // 줄
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>(); // 높이
            layout.preferredHeight = 62f; // 높이
            RetroUi.Solid(row, "Fill", RetroUi.EntryFill); // 바탕
            RetroUi.Frame(row, RetroUi.OrangeDim, 2f); // 테두리

            string role = entry.IsHost ? "  [방장]" : string.Empty; // 역할
            string self = entry.IsSelf ? "  (나)" : string.Empty; // 나
            Text name = RetroUi.Label(row, "Name", $"{entry.Name}{role}{self}", 24, RetroUi.Orange, TextAnchor.MiddleLeft); // 이름
            RetroUi.Stretch(name.rectTransform, 18f, 420f, 0f, 0f); // 왼쪽
            Text pingLabel = RetroUi.Label(row, "Ping", PingText(entry.PingMs), 20, ServerListFilter.PingColor(entry.PingMs), TextAnchor.MiddleRight); // 지연
            RetroUi.Stretch(pingLabel.rectTransform, 0f, 330f, 0f, 0f); // 가운데 오른쪽
            pingLabels[entry.ClientId] = pingLabel; // 제자리 갱신용

            if (!host || entry.IsHost) // 방장만 · 방장 자신 제외
            {
                return; // 버튼 없음
            }

            ulong clientId = entry.ClientId; // 캡처
            string kickKey = $"{clientId}|kick"; // 내보내기 확인 키
            string banKey = $"{clientId}|ban"; // 차단 확인 키
            Button kick = RetroUi.BoxButton(row, "Kick", armedAction == kickKey ? "[ 확인? ]" : "[ 내보내기 ]", 20, () => Arm(kickKey, () => NetworkSession.Kick(clientId, false))); // 내보내기
            RetroUi.Place((RectTransform)kick.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(150f, 44f)); // 오른쪽
            Button ban = RetroUi.BoxButton(row, "Ban", armedAction == banKey ? "[ 확인? ]" : "[ 차단 ]", 20, () => Arm(banKey, () => NetworkSession.Kick(clientId, true))); // 차단
            RetroUi.Place((RectTransform)ban.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(150f, 44f)); // 맨 오른쪽
        }

        private void Arm(string key, Action action) // 두 번 눌러 실행 (실수 방지)
        {
            if (armedAction == key && Time.unscaledTime <= armedUntil) // 확인
            {
                armedAction = string.Empty; // 초기화
                action?.Invoke(); // 실행
                statusLabel.text = "처리했습니다"; // 안내
            }
            else
            {
                armedAction = key; // 대기
                armedUntil = Time.unscaledTime + ConfirmSeconds; // 시간
                statusLabel.text = "한 번 더 누르면 실행됩니다 (3초)"; // 안내
            }

            Rebuild(); // 버튼 글자
        }

        private void ToggleLock() // 방 잠그기 전환
        {
            NetworkSession.SetRoomLocked(!NetworkSession.RoomLocked); // 전환
            statusLabel.text = NetworkSession.RoomLocked ? "새 입장을 막았습니다 (이미 들어왔던 대원은 다시 연결 가능)" : "새 입장을 허용합니다"; // 안내
            Rebuild(); // 갱신
        }
    }
}
