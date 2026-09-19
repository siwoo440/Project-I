using System; // 실행 인자
using System.Collections; // 진행 순서
using System.IO; // 결과 파일
using System.Text; // 결과 조립
using ProjectI.Core; // 씬 이동
using ProjectI.Loop; // 맵 로더
using ProjectI.Net.Voice; // 합성 음성
using ProjectI.Persistence; // 일차
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 현재 씬

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public sealed class CoopAutoTest : MonoBehaviour // 44일차: 빌드 두 개 자동 협동 시험 (-coopAutoHost / -coopAutoJoin 주소 · -coopAutoResult 파일)
    {
        public const string HostArg = "-coopAutoHost"; // 방장 역할
        public const string JoinArg = "-coopAutoJoin"; // 참가 역할 (다음 인자: 주소)
        public const string ResultArg = "-coopAutoResult"; // 결과 파일 경로
        public const ushort Port = 7787; // 시험 전용 포트 (평소 7777 과 겹치지 않게)
        private const float TotalTimeout = 200f; // 전체 제한 시간
        private readonly StringBuilder report = new StringBuilder(); // 결과
        private bool isHost; // 역할
        private string joinAddress = "127.0.0.1"; // 참가 주소
        private string resultPath; // 결과 파일
        private int passed; // 통과
        private int failed; // 실패
        private bool finished; // 끝남

        public static bool Requested => HasArg(HostArg) || HasArg(JoinArg); // 자동 시험 실행 중

        public static void AttachIfRequested(GameObject runner) // 실행 인자가 있으면 시험 추가
        {
            if (Requested && runner.GetComponent<CoopAutoTest>() == null) // 요청됨
            {
                runner.AddComponent<CoopAutoTest>(); // 추가
            }
        }

        private void Start() // 시험 시작
        {
            isHost = HasArg(HostArg); // 역할
            joinAddress = ArgValue(JoinArg) ?? joinAddress; // 주소
            resultPath = ArgValue(ResultArg) ?? Path.Combine(Application.persistentDataPath, $"CoopAutoTest_{(isHost ? "host" : "guest")}.txt"); // 결과 파일
            Application.runInBackground = true; // 창이 가려져도 진행
            Application.targetFrameRate = 30; // 부담 줄이기
            AudioListener.volume = 0f; // 소리 끔
            NetConsistency.Interval = 5f; // 점검 자주
            Line($"[Project I] 협동 자동 시험 / 역할 {(isHost ? "방장" : "참가")} / 버전 {NetworkSession.ProtocolTag} / {DateTime.Now:yyyy-MM-dd HH:mm:ss}"); // 머리말
            StartCoroutine(Watchdog()); // 제한 시간
            StartCoroutine(isHost ? HostRoutine() : GuestRoutine()); // 진행
        }

        private IEnumerator HostRoutine() // 방장: 방 열기 → 참가 확인 → 점검 일치 → 음성 중계 → 참가자 나감
        {
            yield return WaitMainMenu(); // 메뉴

            if (!NetworkSession.BeginHost(Port, RoomVisibility.Public, false, out string error)) // 직접 IP 방 (Steam 사용 안 함)
            {
                Check("방 열기", false, error); // 실패
                Finish(); // 끝
                yield break; // 종료
            }

            ProjectServices.TryGet(out SceneFlowManager flow); // 씬 관리자
            flow?.ContinueGame(); // 게임 월드 (저장이 없으면 1일차)
            yield return WaitUntil(() => NetworkSession.IsConnected && WorldReady(), 60f, "방장 게임 월드·방 열림"); // 준비
            yield return WaitUntil(() => NetworkSession.PlayerCount >= 2, 90f, "참가자 접속 (2/4)"); // 참가
            yield return WaitUntil(() => NetConsistency.Matches >= 1, 60f, "참가자 화면 점검 일치"); // 점검
            Check("점검 중 불일치 기록 없음", NetConsistency.Mismatches == 0, NetConsistency.LastResult); // 불일치
            yield return WaitUntil(() => NetPlayerAvatar.VoicePacketsRelayed >= 10, 40f, "참가자 음성 조각 중계 (10개 이상)"); // 음성
            Check("요청 거절 없음 (정상 진행 중)", NetGuard.TotalRejected == 0, $"거절 {NetGuard.TotalRejected}"); // 보안 오탐
            yield return WaitUntil(() => NetworkSession.PlayerCount <= 1, 60f, "참가자 나감 처리"); // 나감
            Line($"요약 {NetDebugPage.SummaryLine()}"); // 요약
            Finish(); // 끝
        }

        private IEnumerator GuestRoutine() // 참가자: 입장 → 사무소 도착·목록 → 몸체 → 점검 보고 → 합성 음성 → 나가기
        {
            yield return WaitMainMenu(); // 메뉴
            yield return new WaitForSecondsRealtime(4f); // 방장이 방을 열 시간
            bool started = false; // 시작

            for (int attempt = 0; attempt < 10 && !started; attempt++) // 방장 준비 전이면 다시 시도
            {
                started = NetworkSession.BeginJoin(joinAddress, Port, string.Empty, out string error); // 참가

                if (!started) // 실패
                {
                    Line($"  참가 시작 실패 {attempt + 1}: {error}"); // 기록
                    yield return new WaitForSecondsRealtime(3f); // 대기
                }
                else
                {
                    yield return WaitUntil(() => NetworkSession.IsConnected || !NetworkSession.IsOnline, 20f, null); // 연결 결과
                    started = NetworkSession.IsConnected; // 결과

                    if (!started) // 거절·시간 초과
                    {
                        NetworkSession.Leave(); // 정리
                        yield return new WaitForSecondsRealtime(3f); // 다시
                    }
                }
            }

            Check("방장에게 연결", started, NetworkSession.TakePendingMessage()); // 연결

            if (!started) // 실패
            {
                Finish(); // 끝
                yield break; // 종료
            }

            yield return WaitUntil(WorldReady, 60f, "사무소 도착 (게임 월드 준비)"); // 사무소
            yield return WaitUntil(() => NetWorldState.Instance != null && DailySnapshotService.Instance != null && DailySnapshotService.Instance.CurrentDay == NetWorldState.Instance.Day, 20f, "방장 일차와 같음"); // 일차
            yield return WaitUntil(() => NetItemSync.Instance != null && NetItemSync.Instance.LastSnapshotCount > 0, 30f, "방장 아이템 목록 받음"); // 목록
            yield return WaitUntil(() => NetPlayerAvatar.All.Count >= 2 && NetPlayerAvatar.Local != null, 20f, "원정대원 몸체 2개"); // 몸체
            NetPlayerAvatar host = NetPlayerAvatar.Find(Unity.Netcode.NetworkManager.ServerClientId); // 방장 몸체
            yield return WaitUntil(() => host != null && host.TryGetNetworkWorldPosition(out _), 20f, "방장 몸체가 같은 맵에 보임"); // 위치
            yield return new WaitForSecondsRealtime(6f); // 아이템 목록 적용·점검 보고 (5초 간격)
            NetPlayerAvatar.Local?.ReportConsistencyNow(); // 한 번 더 보고
            yield return new WaitForSecondsRealtime(3f); // 방장 비교
            yield return SendSyntheticVoice(); // 합성 음성
            yield return new WaitForSecondsRealtime(3f); // 전달
            Line($"요약 {NetDebugPage.SummaryLine()}"); // 요약
            NetworkSession.Leave(); // 나가기
            yield return new WaitForSecondsRealtime(2f); // 정리
            Check("나가기 후 혼자 하기 상태", !NetworkSession.IsOnline, null); // 정리
            Finish(); // 끝
        }

        private IEnumerator SendSyntheticVoice() // 마이크 없이 440Hz 음을 40ms 조각으로 2초 보냄
        {
            NetPlayerAvatar local = NetPlayerAvatar.Local; // 내 몸체
            float[] tone = new float[VoiceCodec.FrameSamples * 2]; // 40ms
            int sent = 0; // 보낸 수

            for (int packet = 0; packet < 50 && local != null; packet++) // 2초
            {
                for (int index = 0; index < tone.Length; index++) // 음
                {
                    tone[index] = 0.3f * Mathf.Sin(2f * Mathf.PI * 440f * (packet * tone.Length + index) / VoiceCodec.SampleRate); // 사인파
                }

                byte[] data = new byte[tone.Length]; // 압축
                VoiceCodec.EncodeMuLaw(tone, 0, tone.Length, data, 0); // μ-law
                sent += local.SendVoice(VoiceCodec.CodecMuLaw, data) ? 1 : 0; // 전송
                yield return new WaitForSecondsRealtime(0.04f); // 다음 조각
            }

            Check($"합성 음성 전송 ({sent}/50)", sent >= 45, null); // 결과
        }

        private IEnumerator WaitMainMenu() // 부트 → 메인 메뉴 도착
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneFlowManager.MainMenuSceneName && ProjectServices.TryGet(out SceneFlowManager _), 60f, "메인 메뉴 도착"); // 메뉴
            yield return new WaitForSecondsRealtime(1f); // 메뉴 구성
        }

        private IEnumerator WaitUntil(Func<bool> condition, float timeout, string name) // 조건 대기 (name 이 있으면 결과 기록)
        {
            float start = Time.realtimeSinceStartup; // 시작

            while (!SafeCheck(condition) && Time.realtimeSinceStartup - start < timeout) // 대기
            {
                yield return null; // 다음 프레임
            }

            if (name != null) // 기록
            {
                bool ok = SafeCheck(condition); // 결과
                Check(name, ok, ok ? $"{Time.realtimeSinceStartup - start:0.0}초" : $"{timeout:0}초 초과"); // 기록
            }
        }

        private static bool SafeCheck(Func<bool> condition) // 조건 확인 (예외는 실패)
        {
            try
            {
                return condition(); // 결과
            }
            catch (Exception) // 준비 전 참조
            {
                return false; // 아직
            }
        }

        private static bool WorldReady() // 게임 월드 준비 (사무소·이동 중 아님)
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            DailySnapshotService service = DailySnapshotService.Instance; // 저장
            return loader != null && !loader.IsTransitioning && service != null && service.IsInitialized && !service.IsRestoreInProgress; // 결과
        }

        private IEnumerator Watchdog() // 전체 제한 시간
        {
            yield return new WaitForSecondsRealtime(TotalTimeout); // 대기

            if (!finished) // 아직
            {
                Check("전체 제한 시간 안에 끝남", false, $"{TotalTimeout:0}초"); // 실패
                Finish(); // 끝
            }
        }

        private void Check(string name, bool ok, string detail) // 결과 한 줄
        {
            if (ok) // 통과
            {
                passed++; // 집계
            }
            else
            {
                failed++; // 집계
            }

            Line($"{(ok ? "PASS" : "FAIL")}  {name}{(string.IsNullOrEmpty(detail) ? string.Empty : $"  ({detail})")}"); // 기록
        }

        private void Line(string text) // 로그·결과
        {
            report.AppendLine(text); // 결과
            Debug.Log($"[CoopAutoTest] {text}"); // 로그
        }

        private void Finish() // 결과 저장 후 종료
        {
            if (finished) // 이미
            {
                return; // 생략
            }

            finished = true; // 기록
            StopAllCoroutines(); // 중단
            report.Insert(0, $"결과 {passed} 통과 · {failed} 실패\n"); // 요약

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? Application.persistentDataPath); // 폴더
                File.WriteAllText(resultPath, report.ToString(), Encoding.UTF8); // 저장
            }
            catch (Exception exception) // 저장 실패
            {
                Debug.LogError($"[CoopAutoTest] 결과 저장 실패 {resultPath}: {exception.Message}"); // 기록
            }

            Debug.Log($"[CoopAutoTest] 끝 / {passed} 통과 · {failed} 실패 / {resultPath}"); // 기록
            NetworkSession.Leave(); // 연결 정리 (저장하지 않음)
            Invoke(nameof(Quit), 2f); // 참가자에게 방 닫힘 이유가 전달될 시간
        }

        private void Quit() // 종료
        {
            Application.Quit(); // 게임 끝
        }

        private static bool HasArg(string name) // 실행 인자 있음
        {
            foreach (string arg in Environment.GetCommandLineArgs()) // 인자
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) // 일치
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        private static string ArgValue(string name) // 실행 인자 다음 값
        {
            string[] args = Environment.GetCommandLineArgs(); // 인자

            for (int index = 0; index < args.Length - 1; index++) // 인자
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) // 일치
                {
                    return args[index + 1]; // 다음 값
                }
            }

            return null; // 없음
        }
    }
}
