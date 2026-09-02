using System; // 직렬화 기능 사용
using System.Collections.Generic; // 아이템 목록 기능 사용
using ProjectI.Items; // ItemInstanceData 사용

namespace ProjectI.Persistence // 일차 저장·복구 네임스페이스
{
    [Serializable] // JsonUtility 직렬화 허용
    public sealed class EconomySnapshotData // 사무소 경제 전체 상태 스냅샷
    {
        public bool hasData; // 경제 상태를 실제로 읽었는지 표시
        public int sharedFunds; // 공동 자금
        public float saleMultiplier = 1f; // 판매 배율
        public int debtPhaseIndex; // 채무 현재 0기반 단계
        public int paidInCurrentPhase; // 현재 단계 누적 납부액
    }

    [Serializable] // JsonUtility 직렬화 허용
    public sealed class DailySnapshotData // 하루 전체 게임 상태 저장 데이터
    {
        public int schemaVersion = 1; // 저장 데이터 구조 버전
        public int currentDay = 1; // 이 데이터로 시작할 플레이 일차
        public int completedDay; // 불변 DailySnapshot이면 완료한 일차 번호
        public string activeDestination = "Office"; // 저장 당시 환경 맵 목적지
        public EconomySnapshotData economy = new EconomySnapshotData(); // 공동 자금과 채무 상태
        public int selectedQuickSlot; // 플레이어 선택 슬롯 인덱스
        public List<ItemInstanceData> items = new List<ItemInstanceData>(); // 모든 보존 대상 아이템
    }

    [Serializable] // JsonUtility 직렬화 허용
    public sealed class DailySnapshotEnvelope // SHA-256 검증 정보를 포함한 저장 파일 외곽 구조
    {
        public int schemaVersion = 1; // 외곽 저장 형식 버전
        public string checksum; // payloadJson SHA-256 값
        public string payloadJson; // 실제 DailySnapshotData JSON 문자열
    }
}
