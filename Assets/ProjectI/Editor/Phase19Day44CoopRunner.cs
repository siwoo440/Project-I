using System; // 시간
using System.Diagnostics; // 빌드 실행
using System.IO; // 결과 파일
using System.Text; // 보고
using UnityEditor; // 에디터 기능
using Debug = UnityEngine.Debug; // 로그

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase19Day44CoopRunner // 44일차: 빌드 두 개를 방장·참가자로 띄워 자동 협동 시험 → 결과 모아 보기
    {
        private const string ExecutablePath = "Builds/Windows/ProjectI.exe"; // 빌드 실행 파일
        private const string ResultFolder = "Builds/CoopTest"; // 결과 폴더 (git 제외)
        private const double GuestDelaySeconds = 3d; // 방장 먼저 켜고 참가자는 잠시 뒤
        private const double TimeoutSeconds = 260d; // 전체 제한 시간
        private static Process hostProcess; // 방장
        private static Process guestProcess; // 참가자
        private static double startTime; // 시작 시각
        private static bool guestStarted; // 참가자 실행됨

        [MenuItem("Project I/Day 44/Run Coop Auto Test (2 builds)")] // 메뉴
        public static void Run() // 자동 시험 시작
        {
            string exe = Path.Combine(ProjectRoot(), ExecutablePath); // 실행 파일

            if (!File.Exists(exe)) // 빌드 없음
            {
                EditorUtility.DisplayDialog("협동 자동 시험", "Builds/Windows/ProjectI.exe 가 없습니다.\nProject I > Build > Windows Alpha 로 먼저 빌드하세요.", "확인"); // 안내
                return; // 종료
            }

            if (hostProcess != null && !hostProcess.HasExited) // 진행 중
            {
                Debug.LogWarning("[Project I] 협동 자동 시험이 이미 진행 중입니다."); // 안내
                return; // 종료
            }

            string folder = Path.Combine(ProjectRoot(), ResultFolder); // 결과 폴더
            Directory.CreateDirectory(folder); // 생성

            foreach (string old in new[] { "host.txt", "guest.txt", "host.log", "guest.log" }) // 이전 결과
            {
                string path = Path.Combine(folder, old); // 경로

                if (File.Exists(path)) // 있음
                {
                    File.Delete(path); // 삭제 (이번 결과와 섞이지 않게)
                }
            }

            hostProcess = Launch(exe, $"-coopAutoHost {CommonArgs(folder, "host")}"); // 방장
            guestProcess = null; // 참가자는 잠시 뒤
            guestStarted = false; // 기록
            startTime = EditorApplication.timeSinceStartup; // 시작
            EditorApplication.update -= Poll; // 중복 방지
            EditorApplication.update += Poll; // 진행 확인
            Debug.Log($"[Project I] 협동 자동 시험 시작 — 창 두 개가 뜹니다 (최대 {TimeoutSeconds:0}초). 결과: {folder}"); // 안내
        }

        [MenuItem("Project I/Day 44/Show Last Coop Auto Test Result")] // 메뉴
        public static void ShowLast() // 마지막 결과 보기
        {
            Report(false); // 보고
        }

        private static string CommonArgs(string folder, string role) // 공통 실행 인자
        {
            return $"-coopAutoResult \"{Path.Combine(folder, role + ".txt")}\" -logFile \"{Path.Combine(folder, role + ".log")}\" -screen-fullscreen 0 -screen-width 960 -screen-height 540"; // 결과·로그 파일, 작은 창
        }

        private static Process Launch(string exe, string arguments) // 빌드 실행
        {
            ProcessStartInfo info = new ProcessStartInfo(exe, arguments) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? ProjectRoot() }; // 설정 (steam_appid.txt 가 있는 폴더)
            return Process.Start(info); // 시작
        }

        private static void Poll() // 에디터 매 프레임: 참가자 실행·끝 확인
        {
            double elapsed = EditorApplication.timeSinceStartup - startTime; // 경과

            if (!guestStarted && elapsed >= GuestDelaySeconds) // 참가자 실행
            {
                guestStarted = true; // 기록
                guestProcess = Launch(Path.Combine(ProjectRoot(), ExecutablePath), $"-coopAutoJoin 127.0.0.1 {CommonArgs(Path.Combine(ProjectRoot(), ResultFolder), "guest")}"); // 참가자
            }

            bool hostDone = hostProcess == null || hostProcess.HasExited; // 방장 끝
            bool guestDone = guestStarted && (guestProcess == null || guestProcess.HasExited); // 참가자 끝

            if (hostDone && guestDone) // 둘 다 끝
            {
                EditorApplication.update -= Poll; // 중지
                Report(true); // 결과
                return; // 종료
            }

            if (elapsed < TimeoutSeconds) // 아직
            {
                return; // 대기
            }

            EditorApplication.update -= Poll; // 중지
            Kill(hostProcess); // 강제 종료
            Kill(guestProcess); // 강제 종료
            Debug.LogError($"[Project I] 협동 자동 시험 제한 시간 {TimeoutSeconds:0}초 초과 — 강제 종료했습니다."); // 안내
            Report(true); // 남은 결과
        }

        private static void Kill(Process process) // 강제 종료
        {
            try
            {
                if (process != null && !process.HasExited) // 실행 중
                {
                    process.Kill(); // 종료
                }
            }
            catch (Exception exception) // 이미 종료 등
            {
                Debug.LogWarning($"[Project I] 시험 창 종료 실패: {exception.Message}"); // 기록
            }
        }

        private static void Report(bool afterRun) // 두 결과 파일 모아 보고
        {
            string folder = Path.Combine(ProjectRoot(), ResultFolder); // 결과 폴더
            StringBuilder builder = new StringBuilder("[Project I] 44일차 협동 자동 시험 결과\n"); // 보고
            bool ok = true; // 전체 통과

            foreach (string role in new[] { "host", "guest" }) // 역할
            {
                string path = Path.Combine(folder, role + ".txt"); // 결과 파일
                builder.AppendLine(role == "host" ? "── 방장 ──" : "── 참가자 ──"); // 제목

                if (!File.Exists(path)) // 결과 없음 (강제 종료·실행 실패)
                {
                    builder.AppendLine($"결과 파일 없음 — 로그를 확인하세요: {Path.Combine(folder, role + ".log")}"); // 안내
                    ok = false; // 실패
                    continue; // 다음
                }

                string text = File.ReadAllText(path, Encoding.UTF8); // 결과
                builder.AppendLine(text.TrimEnd()); // 추가
                ok &= !text.Contains("FAIL"); // 실패 항목
            }

            if (ok) // 모두 통과
            {
                Debug.Log(builder.ToString()); // 기록
            }
            else
            {
                Debug.LogError(builder.ToString()); // 오류
            }

            if (afterRun) // 방금 끝난 시험
            {
                EditorUtility.DisplayDialog("협동 자동 시험", ok ? "모든 항목 통과 (Console 에 상세 결과)" : "실패 항목이 있습니다 (Console 에 상세 결과)", "확인"); // 알림
            }
        }

        private static string ProjectRoot() // 프로젝트 루트
        {
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")); // 경로
        }
    }
}
