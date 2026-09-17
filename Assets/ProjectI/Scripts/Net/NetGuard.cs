using System.Collections.Generic; // 기록
using System.Text; // 요약·이름 정리
using ProjectI.Combat; // 근접 무기
using ProjectI.Combat.Ranged; // 원거리 무기
using ProjectI.Items; // 아이템
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public enum NetChannel // 요청 종류 (종류마다 초당 한도)
    {
        Item, // 줍기·내려놓기·손·보관·소모
        Damage, // 공격 피해
        Economy, // 판매·구매·상환
        Device, // 문·장치·함정
        Travel, // 마차 출발
        Snapshot, // 전체 목록 요청
        Voice, // 43일차: 음성 조각
    }

    public static class NetGuard // 42일차: 방장이 참가자 요청을 검사 (값·거리·횟수) — 부정 요청이 쌓이면 자동으로 내보냄
    {
        public const int MaxIdLength = 64; // 아이템 ID 길이
        public const int MaxPathLength = 512; // 계층 경로 길이
        public const int MaxSaleItems = 32; // 한 번에 판매할 수 있는 수
        public const float MaxCoordinate = 20000f; // 월드 좌표 한계
        public const float InteractReach = 8f; // 줍기·보관·문·장치 거리 (몸체 위치 지연 포함)
        public const float CounterReach = 10f; // 판매대·진열대·장부 거리
        public const float DropReach = 12f; // 내려놓기·던지기 거리 (사망 시 흩뿌림 포함)
        public const float TrapReach = 20f; // 함정 발판과 본체가 떨어져 있을 수 있음
        public const float ElevatorReach = 60f; // 승강기 호출 버튼은 다른 층에 있음
        public const float MeleeReach = 9f; // 근접 공격 거리
        public const float RangedReach = 160f; // 원거리 공격 거리
        public const float HitPointSlack = 8f; // 맞은 지점과 대상 중심 차이 허용
        public const float MaxThrowSpeed = 30f; // 던지기 속도
        public const float MaxStagger = 200f; // 경직 힘
        public const float MaxForce = 40f; // 넉백 힘
        public const float WeaponMemorySeconds = 5f; // 무기를 바꾼 뒤에도 날아가는 화살을 인정하는 시간
        public const float StrikeLimit = 40f; // 경고 누적 한도 (넘으면 내보냄)
        public const float StrikeDecayPerSecond = 1f; // 경고가 초당 줄어드는 양
        public const float SummaryInterval = 10f; // 거절 기록 요약 간격
        public const string KickReasonAbuse = "부정 요청이 너무 많아 방장이 연결을 끊었습니다"; // 자동 내보내기 이유

        private static readonly Dictionary<ulong, ClientRecord> records = new Dictionary<ulong, ClientRecord>(); // 대원별 기록
        private static float nextSummary; // 다음 요약 시각

        public static int TotalRejected { get; private set; } // 검증용: 거절된 요청 수
        public static int TotalKicked { get; private set; } // 검증용: 자동으로 내보낸 수

        private sealed class Bucket // 초당 한도 (토큰)
        {
            public float Tokens; // 남은 횟수
            public float Last; // 마지막 충전 시각
        }

        private sealed class ClientRecord // 대원 한 명의 기록
        {
            public readonly Dictionary<NetChannel, Bucket> Buckets = new Dictionary<NetChannel, Bucket>(); // 종류별 한도
            public readonly Dictionary<string, int> Rejected = new Dictionary<string, int>(); // 거절 이유별 수 (요약용)
            public float Strikes; // 경고
            public float StrikeTime; // 경고 갱신 시각
            public bool Kicked; // 내보냄
            public readonly Dictionary<string, float> Attacks = new Dictionary<string, float>(); // 처리한 공격 (무기·공격 번호·대상)
            public int LastAttackId = int.MinValue; // 마지막 공격 번호
            public float LastAttackTime = -999f; // 마지막 새 공격 시각
        }

        public static void Reset() // 방을 열거나 닫을 때 초기화
        {
            records.Clear(); // 기록
            nextSummary = 0f; // 요약
            TotalRejected = 0; // 집계
            TotalKicked = 0; // 집계
        }

        public static void Forget(ulong clientId) // 대원이 나감
        {
            records.Remove(clientId); // 기록 삭제
        }

        // ───────────────────────── 횟수·경고 ─────────────────────────

        public static bool Allow(ulong sender, NetChannel channel, float overStrike = 1f) // 초당 한도 안이면 true (방장 자신은 항상 허용 · 넘으면 overStrike 만큼 경고)
        {
            if (IsHostSelf(sender)) // 방장
            {
                return true; // 허용
            }

            ClientRecord record = Record(sender); // 기록

            if (record.Kicked) // 이미 내보내는 중
            {
                return false; // 무시
            }

            Limits(channel, out float rate, out float burst); // 한도
            float now = Time.unscaledTime; // 현재

            if (!record.Buckets.TryGetValue(channel, out Bucket bucket)) // 처음
            {
                bucket = new Bucket { Tokens = burst, Last = now }; // 가득
                record.Buckets[channel] = bucket; // 등록
            }

            bucket.Tokens = Mathf.Min(burst, bucket.Tokens + (now - bucket.Last) * rate); // 충전
            bucket.Last = now; // 기록

            if (bucket.Tokens < 1f) // 초과
            {
                Reject(sender, channel.ToString(), "횟수 초과", overStrike); // 경고
                return false; // 거부
            }

            bucket.Tokens -= 1f; // 사용
            return true; // 허용
        }

        public static void Reject(ulong sender, string kind, string reason, float strikes) // 거절 기록 (경고가 쌓이면 내보냄)
        {
            if (IsHostSelf(sender)) // 방장
            {
                return; // 기록 안 함
            }

            TotalRejected++; // 집계
            ClientRecord record = Record(sender); // 기록
            string key = $"{kind}:{reason}"; // 이유
            record.Rejected[key] = record.Rejected.TryGetValue(key, out int count) ? count + 1 : 1; // 누적

            if (strikes <= 0f || record.Kicked) // 경고 없음
            {
                return; // 종료
            }

            float now = Time.unscaledTime; // 현재
            record.Strikes = Mathf.Max(0f, record.Strikes - (now - record.StrikeTime) * StrikeDecayPerSecond) + strikes; // 줄어든 뒤 추가
            record.StrikeTime = now; // 기록

            if (record.Strikes < StrikeLimit) // 한도 안
            {
                return; // 종료
            }

            record.Kicked = true; // 한 번만
            TotalKicked++; // 집계
            Debug.LogWarning($"[Project I] 협동 보안 / 대원 {sender + 1} 자동 내보내기 / 마지막 거절 {key}"); // 기록
            NetworkSession.Kick(sender, false, KickReasonAbuse); // 내보내기
        }

        public static void Tick() // 방장: 거절 요약을 주기적으로 기록 (로그 폭주 방지)
        {
            if (Time.unscaledTime < nextSummary || records.Count == 0) // 대기
            {
                return; // 생략
            }

            nextSummary = Time.unscaledTime + SummaryInterval; // 다음
            StringBuilder builder = null; // 요약

            foreach (KeyValuePair<ulong, ClientRecord> pair in records) // 대원
            {
                if (pair.Value.Rejected.Count == 0) // 없음
                {
                    continue; // 다음
                }

                builder ??= new StringBuilder("[Project I] 협동 보안 요약"); // 시작
                builder.Append($" / 대원 {pair.Key + 1}:"); // 대원

                foreach (KeyValuePair<string, int> entry in pair.Value.Rejected) // 이유
                {
                    builder.Append($" {entry.Key}×{entry.Value}"); // 이유·수
                }

                pair.Value.Rejected.Clear(); // 비우기
            }

            if (builder != null) // 기록 있음
            {
                Debug.LogWarning(builder.ToString()); // 기록
            }
        }

        private static void Limits(NetChannel channel, out float rate, out float burst) // 종류별 초당 횟수·순간 최대
        {
            switch (channel)
            {
                case NetChannel.Item: rate = 12f; burst = 30f; return; // 여러 개 줍기·던지기
                case NetChannel.Damage: rate = 15f; burst = 30f; return; // 여러 대상 베기·연사
                case NetChannel.Economy: rate = 2f; burst = 5f; return; // 판매·구매
                case NetChannel.Device: rate = 5f; burst = 10f; return; // 문·스위치
                case NetChannel.Travel: rate = 1f; burst = 3f; return; // 종
                case NetChannel.Voice: rate = 40f; burst = 80f; return; // 음성 (보내는 쪽은 초당 20~25번)
                default: rate = 0.5f; burst = 3f; return; // 전체 목록 (맵 이동 때만)
            }
        }

        // ───────────────────────── 값 검사 ─────────────────────────

        public static bool Finite(float value) // NaN·무한대 아님
        {
            return !float.IsNaN(value) && !float.IsInfinity(value); // 결과
        }

        public static bool Finite(Vector3 value) // 모든 성분 유한
        {
            return Finite(value.x) && Finite(value.y) && Finite(value.z); // 결과
        }

        public static bool Finite(Quaternion value) // 모든 성분 유한
        {
            return Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w); // 결과
        }

        public static bool InWorld(Vector3 value) // 월드 범위 안
        {
            return Finite(value) && Mathf.Abs(value.x) < MaxCoordinate && Mathf.Abs(value.y) < MaxCoordinate && Mathf.Abs(value.z) < MaxCoordinate; // 결과
        }

        public static bool ValidId(string value) // 아이템 ID 형식
        {
            return !string.IsNullOrEmpty(value) && value.Length <= MaxIdLength; // 결과
        }

        public static bool ValidPath(string value, bool allowEmpty = false) // 계층 경로 형식
        {
            return value == null ? allowEmpty : (value.Length == 0 ? allowEmpty : value.Length <= MaxPathLength); // 결과
        }

        // ───────────────────────── 위치 ─────────────────────────

        public static bool TryGetPosition(ulong sender, out Vector3 position) // 대원 몸체의 현재 위치 (같은 맵일 때만)
        {
            position = Vector3.zero; // 기본
            NetPlayerAvatar avatar = NetPlayerAvatar.Find(sender); // 몸체
            return avatar != null && avatar.TryGetNetworkWorldPosition(out position); // 결과
        }

        public static bool Near(ulong sender, Vector3 point, float reach) // 대원이 그 지점 가까이 있음 (방장 자신은 항상 true)
        {
            if (IsHostSelf(sender)) // 방장
            {
                return true; // 허용
            }

            return TryGetPosition(sender, out Vector3 position) && (position - point).sqrMagnitude <= reach * reach; // 거리
        }

        // ───────────────────────── 무기·공격 ─────────────────────────

        public static bool TryWeaponProfile(WorldItem item, out float maxDamage, out float reach, out float minInterval) // 무기 최대 피해·사거리·최소 공격 간격
        {
            maxDamage = 0f; // 기본
            reach = 0f; // 기본
            minInterval = 0f; // 기본

            if (item == null) // 빈손
            {
                return false; // 무기 아님
            }

            MeleeWeaponItem melee = item.GetComponentInChildren<MeleeWeaponItem>(true); // 근접

            if (melee != null && melee.AttackDefinition != null) // 근접 무기
            {
                maxDamage = melee.AttackDefinition.BaseDamage; // 피해
                reach = MeleeReach; // 거리
                minInterval = melee.AttackDefinition.CooldownDuration * 0.6f; // 간격 (지연 여유)
                return true; // 결과
            }

            CrossbowWeaponItem crossbow = item.GetComponentInChildren<CrossbowWeaponItem>(true); // 석궁

            if (crossbow != null) // 석궁
            {
                maxDamage = crossbow.BaseDamage; // 피해
                reach = RangedReach; // 거리
                minInterval = 0.1f; // 간격
                return true; // 결과
            }

            RevolverWeaponItem revolver = item.GetComponentInChildren<RevolverWeaponItem>(true); // 리볼버

            if (revolver != null) // 리볼버
            {
                maxDamage = revolver.BaseDamage; // 피해
                reach = RangedReach; // 거리
                minInterval = 0.05f; // 간격
                return true; // 결과
            }

            return false; // 무기 아님
        }

        public static bool AcceptAttack(ulong sender, string weaponId, int attackId, int targetId, float minInterval) // 같은 공격의 같은 대상은 한 번만, 새 공격은 최소 간격 이후만
        {
            if (IsHostSelf(sender)) // 방장
            {
                return true; // 허용
            }

            ClientRecord record = Record(sender); // 기록
            float now = Time.unscaledTime; // 현재
            string key = $"{weaponId}|{attackId}|{targetId}"; // 공격·대상

            if (record.Attacks.ContainsKey(key)) // 이미 처리
            {
                return false; // 중복 (경고 없음)
            }

            if (attackId != record.LastAttackId) // 새 공격
            {
                if (now - record.LastAttackTime < minInterval) // 너무 빠름
                {
                    Reject(sender, "피해", "공격 간격", 1f); // 경고
                    return false; // 거부
                }

                record.LastAttackId = attackId; // 기록
                record.LastAttackTime = now; // 기록
            }

            if (record.Attacks.Count > 256) // 오래된 기록 정리
            {
                List<string> old = new List<string>(); // 삭제 대상

                foreach (KeyValuePair<string, float> pair in record.Attacks) // 기록
                {
                    if (now - pair.Value > 10f) // 10초 지남
                    {
                        old.Add(pair.Key); // 추가
                    }
                }

                foreach (string oldKey in old) // 삭제
                {
                    record.Attacks.Remove(oldKey); // 삭제
                }
            }

            record.Attacks[key] = now; // 기록
            return true; // 허용
        }

        // ───────────────────────── 이름 ─────────────────────────

        public static string CleanName(string text, int maxLength = 24) // 서식 태그·제어 문자 제거 · 길이 제한
        {
            if (string.IsNullOrEmpty(text)) // 없음
            {
                return string.Empty; // 빈 이름
            }

            StringBuilder builder = new StringBuilder(Mathf.Min(text.Length, maxLength)); // 결과

            for (int index = 0; index < text.Length; index++) // 글자
            {
                char character = text[index]; // 글자

                if (builder.Length >= maxLength) // 길이
                {
                    break; // 여기까지
                }

                if (character == '<') // 서식 태그 (<color=...> 등) 통째로 제거
                {
                    int close = text.IndexOf('>', index + 1); // 닫는 괄호
                    index = close > index ? close : index; // 태그 끝으로 건너뜀 (닫는 괄호가 없으면 '<' 만 제거)
                    continue; // 다음
                }

                if (char.IsControl(character) || character == '<' || character == '>' || char.IsSurrogate(character) || character == (char)0x200B || character == (char)0x202E) // 제어·태그·서로게이트·보이지 않는 글자·방향 뒤집기
                {
                    continue; // 제외
                }

                builder.Append(character); // 추가
            }

            return builder.ToString().Trim(); // 앞뒤 공백 제거
        }

        private static ClientRecord Record(ulong sender) // 기록 찾기·만들기
        {
            if (!records.TryGetValue(sender, out ClientRecord record)) // 없음
            {
                record = new ClientRecord { StrikeTime = Time.unscaledTime }; // 생성
                records[sender] = record; // 등록
            }

            return record; // 반환
        }

        private static bool IsHostSelf(ulong sender) // 방장 자신이 보낸 요청
        {
            return sender == Unity.Netcode.NetworkManager.ServerClientId; // 방장 번호
        }
    }
}
