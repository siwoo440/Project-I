using UnityEngine; // Unity 기본 기능과 ScriptableObject 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [CreateAssetMenu(fileName = "DangerGrade", menuName = "Project I/Danger Grade")] // 위험 등급 데이터 생성 메뉴
    public class DangerGradeData : ScriptableObject // 위험 등급별 공통 설정 데이터
    {
        [Header("등급")] // Inspector 등급 설정 구분
        [Tooltip("E부터 S까지의 위험 등급")][SerializeField] DangerGrade grade = DangerGrade.E; // 현재 위험 등급

        [Header("기본 내부 크기")] // Inspector 방 수 설정 구분
        [Tooltip("등급별 기본 최소 방 수")][SerializeField][Min(1)] int minimumRoomCount = 20; // 기본 최소 방 수
        [Tooltip("등급별 기본 최대 방 수")][SerializeField][Min(1)] int maximumRoomCount = 24; // 기본 최대 방 수

        [Header("몬스터")] // Inspector 몬스터 설정 구분
        [Tooltip("몬스터 생성 확률 배율")][SerializeField][Min(0f)] float monsterSpawnChanceMultiplier = 0.85f; // 몬스터 생성 확률 배율
        [Tooltip("등급별 최대 몬스터 수 보정")][SerializeField] int maximumMonsterAdjustment = -2; // 최대 몬스터 수 보정

        [Header("함정")] // Inspector 함정 설정 구분
        [Tooltip("등급별 기본 최소 함정 수")][SerializeField][Min(0)] int minimumTrapCount = 4; // 기본 최소 함정 수
        [Tooltip("등급별 기본 최대 함정 수")][SerializeField][Min(0)] int maximumTrapCount = 6; // 기본 최대 함정 수

        [Header("경제")] // Inspector 경제 설정 구분
        [Tooltip("회수품 기본 수익 배율")][SerializeField][Min(0f)] float rewardValueMultiplier = 1f; // 회수품 수익 배율
        [Tooltip("해당 등급의 기본 마차 이동 비용")][SerializeField][Min(0)] int defaultTravelCost; // 기본 이동 비용

        public DangerGrade Grade => grade; // 위험 등급 반환
        public int Stage => (int)grade; // 위험 단계 반환
        public string GradeName => grade.ToString(); // 위험 등급 문자 반환
        public int MinimumRoomCount => minimumRoomCount; // 기본 최소 방 수 반환
        public int MaximumRoomCount => maximumRoomCount; // 기본 최대 방 수 반환
        public float MonsterSpawnChanceMultiplier => monsterSpawnChanceMultiplier; // 몬스터 생성 배율 반환
        public int MaximumMonsterAdjustment => maximumMonsterAdjustment; // 최대 몬스터 보정 반환
        public int MinimumTrapCount => minimumTrapCount; // 기본 최소 함정 수 반환
        public int MaximumTrapCount => maximumTrapCount; // 기본 최대 함정 수 반환
        public float RewardValueMultiplier => rewardValueMultiplier; // 수익 배율 반환
        public int DefaultTravelCost => defaultTravelCost; // 기본 이동 비용 반환

        void OnValidate() // Inspector 입력값 자동 보정
        {
            minimumRoomCount = Mathf.Max(1, minimumRoomCount); // 최소 방 수 보정
            maximumRoomCount = Mathf.Max(minimumRoomCount, maximumRoomCount); // 최대 방 수 보정
            minimumTrapCount = Mathf.Max(0, minimumTrapCount); // 최소 함정 수 보정
            maximumTrapCount = Mathf.Max(minimumTrapCount, maximumTrapCount); // 최대 함정 수 보정
            monsterSpawnChanceMultiplier = Mathf.Max(0f, monsterSpawnChanceMultiplier); // 몬스터 배율 보정
            rewardValueMultiplier = Mathf.Max(0f, rewardValueMultiplier); // 수익 배율 보정
            defaultTravelCost = Mathf.Max(0, defaultTravelCost); // 이동 비용 보정
        }
    }
}