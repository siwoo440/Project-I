using System.Text; // 보고
using ProjectI.Net; // 협동 보안
using UnityEditor; // 에디터 기능
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase17Day42SecurityCheck // 42일차: 요청 검사 모듈 자가 시험 (네트워크 없이 잘못된 요청을 흉내 냄)
    {
        private const ulong FakeGuest = 9001; // 시험용 참가자 번호 (실제 대원과 겹치지 않음)

        [MenuItem("Project I/Day 42/Run Security Self-Test")] // 메뉴
        public static void Run() // 시험 실행
        {
            StringBuilder report = new StringBuilder("[Project I] 42일차 보안 자가 시험\n"); // 보고
            int passed = 0; // 통과
            int failed = 0; // 실패

            void Check(string name, bool ok) // 한 항목
            {
                report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}"); // 기록

                if (ok) // 통과
                {
                    passed++; // 집계
                }
                else
                {
                    failed++; // 집계
                }
            }

            NetGuard.Reset(); // 초기화

            // 값 검사
            Check("NaN 거부", !NetGuard.Finite(float.NaN)); // NaN
            Check("무한대 거부", !NetGuard.Finite(new Vector3(0f, float.PositiveInfinity, 0f))); // 무한대
            Check("먼 좌표 거부", !NetGuard.InWorld(new Vector3(0f, 0f, 1e6f))); // 월드 밖
            Check("정상 좌표 허용", NetGuard.InWorld(new Vector3(10f, 2f, -30f))); // 정상
            Check("긴 ID 거부", !NetGuard.ValidId(new string('x', NetGuard.MaxIdLength + 1))); // 길이
            Check("빈 ID 거부", !NetGuard.ValidId(string.Empty)); // 빈 값
            Check("정상 ID 허용", NetGuard.ValidId("g1a2b3c4d-12")); // 정상
            Check("긴 경로 거부", !NetGuard.ValidPath(new string('/', NetGuard.MaxPathLength + 1))); // 경로

            // 이름 정리
            Check("서식 태그 제거", NetGuard.CleanName("<color=red>보스</color>") == "보스"); // 태그
            Check("줄바꿈 제거", NetGuard.CleanName("원정\n대원") == "원정대원"); // 제어 문자
            Check("방향 뒤집기 제거", NetGuard.CleanName("abc" + (char)0x202E + "def") == "abcdef"); // 보이지 않는 문자
            Check("길이 제한 24자", NetGuard.CleanName(new string('가', 40)).Length == 24); // 길이
            Check("닫히지 않은 괄호", NetGuard.CleanName("a<b") == "ab"); // 괄호만 제거

            // 횟수 제한 (에디터 한 번의 호출 안에서는 시간이 흐르지 않음 → 순간 최대치만 허용)
            int allowed = 0; // 허용 수

            for (int index = 0; index < 100; index++) // 1초 안에 100번
            {
                allowed += NetGuard.Allow(FakeGuest, NetChannel.Economy) ? 1 : 0; // 판매·구매 요청
            }

            Check($"판매·구매 폭주 제한 (허용 {allowed}/100, 기대 5)", allowed == 5); // 순간 최대 5
            Check($"폭주가 계속되면 자동 내보내기 (내보냄 {NetGuard.TotalKicked})", NetGuard.TotalKicked == 1); // 경고 40 초과
            Check("내보낸 뒤 요청 무시", !NetGuard.Allow(FakeGuest, NetChannel.Item)); // 무시
            Check("방장 요청은 항상 허용", NetGuard.Allow(0, NetChannel.Economy)); // 방장

            // 공격 중복·간격
            NetGuard.Reset(); // 초기화
            Check("첫 공격 허용", NetGuard.AcceptAttack(FakeGuest, "sword", 1, 100, 0.4f)); // 첫 공격
            Check("같은 공격 두 번째 대상 허용", NetGuard.AcceptAttack(FakeGuest, "sword", 1, 101, 0.4f)); // 휩쓸기
            Check("같은 공격 같은 대상 중복 거부", !NetGuard.AcceptAttack(FakeGuest, "sword", 1, 100, 0.4f)); // 중복
            Check("간격 안의 새 공격 거부", !NetGuard.AcceptAttack(FakeGuest, "sword", 2, 100, 0.4f)); // 너무 빠름
            Check("무기 아닌 손 아이템", !NetGuard.TryWeaponProfile(null, out _, out _, out _)); // 빈손

            // 방 코드 (41일차)
            Check("코드 정리", RoomCode.TryParseCode("k7q-2mx", out string code) && code == "K7Q2MX"); // 정리
            Check("주소는 코드 아님", !RoomCode.TryParseCode("127.0.0.1", out _)); // 주소

            NetGuard.Reset(); // 정리
            report.Insert(0, $"결과 {passed} 통과 · {failed} 실패\n"); // 요약

            if (failed == 0) // 모두 통과
            {
                Debug.Log(report.ToString()); // 기록
            }
            else
            {
                Debug.LogError(report.ToString()); // 오류
            }
        }
    }
}
