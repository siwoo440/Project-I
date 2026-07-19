using UnityEngine; // SerializeField를 사용하기 위한 Unity 기본 기능

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public enum RunEndReason // 던전 탐험 종료 원인
    {
        None, // 아직 던전 결과가 확정되지 않음
        ManualExtraction, // 플레이어가 마차 레버로 직접 탈출
        DeadlineExtraction, // 제한시간 종료 시 마차 근처에서 자동 탈출
        DeadlineAbandoned, // 제한시간 종료 시 던전에 유기
        PlayerDeath // 부활할 수 없는 최종 사망
    }

    [System.Serializable] // Unity가 결과 필드를 직렬화할 수 있도록 지정
    public class RunResultData // 한 번의 던전 탐험 결과 데이터
    {
        [SerializeField] bool hasResult; // 결과 확정 여부
        [SerializeField] bool escaped; // 플레이어 탈출 성공 여부
        [SerializeField] RunEndReason endReason; // 던전 종료 원인
        [SerializeField] int securedItemCount; // 마차에 확보한 전체 아이템 수
        [SerializeField] int securedTreasureCount; // 마차에 확보한 보물 수
        [SerializeField] int securedValue; // 확보한 보물 총 가치
        [SerializeField] float elapsedSeconds; // 던전 진행시간
        [SerializeField] int dungeonSeed; // 던전 생성에 사용한 시드

        public bool HasResult => hasResult; // 결과 확정 여부 반환
        public bool Escaped => escaped; // 탈출 성공 여부 반환
        public RunEndReason EndReason => endReason; // 종료 원인 반환
        public int SecuredItemCount => securedItemCount; // 확보한 전체 아이템 수 반환
        public int SecuredTreasureCount => securedTreasureCount; // 확보한 보물 수 반환
        public int SecuredValue => securedValue; // 확보 가치 반환
        public float ElapsedSeconds => elapsedSeconds; // 던전 진행시간 반환
        public int DungeonSeed => dungeonSeed; // 던전 생성 시드 반환

        public void SetResult( // 확정된 던전 탐험 결과 저장
            bool didEscape, // 탈출 성공 여부
            RunEndReason reason, // 던전 종료 원인
            int itemCount, // 확보한 전체 아이템 수
            int treasureCount, // 확보한 보물 수
            int value, // 확보한 보물 가치
            float duration, // 던전 진행시간
            int seed) // 던전 생성 시드
        {
            hasResult = true; // 결과 확정 상태 활성화
            escaped = didEscape; // 탈출 성공 여부 저장
            endReason = reason; // 종료 원인 저장
            securedItemCount = Mathf.Max(0, itemCount); // 전체 아이템 수를 음수가 되지 않도록 저장
            securedTreasureCount = Mathf.Max(0, treasureCount); // 보물 수를 음수가 되지 않도록 저장
            securedValue = Mathf.Max(0, value); // 확보 가치를 음수가 되지 않도록 저장
            elapsedSeconds = Mathf.Max(0f, duration); // 진행시간을 음수가 되지 않도록 저장
            dungeonSeed = seed; // 던전 생성 시드 저장
        }

        public void Clear() // 새로운 던전 시작을 위해 이전 결과 초기화
        {
            hasResult = false; // 결과 확정 상태 해제
            escaped = false; // 탈출 성공 상태 초기화
            endReason = RunEndReason.None; // 종료 원인 초기화
            securedItemCount = 0; // 전체 확보 아이템 수 초기화
            securedTreasureCount = 0; // 확보 보물 수 초기화
            securedValue = 0; // 확보 가치 초기화
            elapsedSeconds = 0f; // 던전 진행시간 초기화
            dungeonSeed = 0; // 던전 생성 시드 초기화
        }

        public string GetEndReasonText() // 종료 원인을 한글 문구로 반환
        {
            switch (endReason) // 현재 종료 원인 확인
            {
                case RunEndReason.ManualExtraction: // 수동 탈출 결과
                    return "마차 레버를 이용한 탈출"; // 수동 탈출 문구 반환

                case RunEndReason.DeadlineExtraction: // 제한시간 자동 탈출 결과
                    return "제한시간 종료 직전 자동 탈출"; // 자동 탈출 문구 반환

                case RunEndReason.DeadlineAbandoned: // 제한시간 유기 결과
                    return "제한시간 초과로 던전에 유기"; // 유기 실패 문구 반환

                case RunEndReason.PlayerDeath: // 최종 사망 결과
                    return "부활 불가로 최종 사망"; // 최종 사망 문구 반환

                default: // 결과가 아직 없는 경우
                    return "결과 없음"; // 기본 문구 반환
            }
        }
    }
}