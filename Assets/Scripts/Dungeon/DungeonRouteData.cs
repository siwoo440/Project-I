using UnityEngine; // ScriptableObject와 Inspector 설정 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [CreateAssetMenu(fileName = "DungeonRoute", menuName = "Project I/Dungeon Route")] // Project 창에서 던전 경로 데이터 생성 메뉴 추가
    public class DungeonRouteData : ScriptableObject // 선택 가능한 던전 경로 원본 데이터
    {
        [Header("표시 정보")] // Inspector 표시 정보 구분
        [Tooltip("저장과 비교에 사용할 고유 경로 ID")] [SerializeField] string routeId = "route"; // 저장과 비교에 사용할 고유 경로 ID
        [Tooltip("플레이어에게 표시할 던전 이름")] [SerializeField] string displayName = "던전"; // 플레이어에게 표시할 던전 이름
        [Tooltip("던전 선택 화면에 표시할 설명")] [SerializeField][TextArea(2, 4)] string description = "던전 설명"; // 던전 선택 화면에 표시할 설명
        [Tooltip("탐사 지역의 대표 기믹")][SerializeField][TextArea(1, 2)] string coreMechanic = "핵심 기믹"; // 탐사 지역 핵심 기믹

        [Header("Scene")] // Inspector Scene 설정 구분
        [Tooltip("선택 후 이동할 Dungeon Scene 이름")][SerializeField] string sceneName = string.Empty; // 이동할 Dungeon Scene 이름
        [Tooltip("현재 실제 출발이 가능한 지역인지 결정")][SerializeField] bool isAvailable; // 지역 출발 가능 상태

        [Header("위험 등급")] // Inspector 위험 등급 설정 구분
        [Tooltip("지역에 적용할 공통 위험 등급 데이터")][SerializeField] DangerGradeData dangerGradeData; // 공통 위험 등급 데이터

        [Header("지역별 내부 크기")] // Inspector 지역 방 수 설정 구분
        [Tooltip("현재 지역의 최소 방 수")][SerializeField][Min(1)] int minimumRoomCount = 20; // 지역 최소 방 수
        [Tooltip("현재 지역의 최대 방 수")][SerializeField][Min(1)] int maximumRoomCount = 24; // 지역 최대 방 수

        [Header("원정 비용")] // Inspector 원정 비용 설정 구분
        [Tooltip("현재 지역까지의 마차 이동 비용")][SerializeField][Min(0)] int travelCost; // 마차 이동 비용

        [Header("기존 스폰 호환")] // 기존 함정 시스템 호환 설정 구분
        [Tooltip("현재 방별 함정 생성 시스템에 적용할 임시 배율")][SerializeField][Min(0f)] float trapSpawnMultiplier = 1f; // 임시 함정 생성 배율

        [Header("시드")] // Inspector 시드 설정 구분
        [Tooltip("지역마다 다른 던전 배치를 만들 시드 보정값")][SerializeField] int seedOffset; // 지역별 시드 보정값


        public string RouteId => routeId; // 지역 고유 ID 반환
        public string DisplayName => displayName; // 지역 표시 이름 반환
        public string Description => description; // 지역 설명 반환
        public string CoreMechanic => coreMechanic; // 지역 핵심 기믹 반환
        public string SceneName => sceneName; // 이동할 Scene 이름 반환
        public bool IsAvailable => isAvailable; // 출발 가능 상태 반환
        public DangerGradeData GradeData => dangerGradeData; // 공통 위험 등급 데이터 반환
        public int DangerLevel => dangerGradeData != null ? dangerGradeData.Stage : 0; // 위험 단계 반환
        public string DangerGradeLabel => dangerGradeData != null ? dangerGradeData.GradeName : "-"; // 위험 등급 문자 반환
        public int MinimumRoomCount => minimumRoomCount; // 지역 최소 방 수 반환
        public int MaximumRoomCount => maximumRoomCount; // 지역 최대 방 수 반환
        public int RoomCount => maximumRoomCount; // 기존 코드 호환용 최대 방 수 반환
        public int TravelCost => travelCost; // 마차 이동 비용 반환
        public float MonsterSpawnMultiplier => dangerGradeData != null ? dangerGradeData.MonsterSpawnChanceMultiplier : 1f; // 몬스터 생성 배율 반환
        public float TrapSpawnMultiplier => trapSpawnMultiplier; // 임시 함정 생성 배율 반환
        public float RewardValueMultiplier => dangerGradeData != null ? dangerGradeData.RewardValueMultiplier : 1f; // 회수품 수익 배율 반환
        public int MinimumTrapCount => dangerGradeData != null ? dangerGradeData.MinimumTrapCount : 0; // 등급별 최소 함정 수 반환
        public int MaximumTrapCount => dangerGradeData != null ? dangerGradeData.MaximumTrapCount : 0; // 등급별 최대 함정 수 반환
        public int SeedOffset => seedOffset; // 던전 시드 보정값 반환

        public int GetRandomRoomCount() // 지역별 방 수 범위에서 생성 개수 결정
        {
            int safeMinimum = Mathf.Max(1, minimumRoomCount); // 안전한 최소 방 수 계산
            int safeMaximum = Mathf.Max(safeMinimum, maximumRoomCount); // 안전한 최대 방 수 계산
            return Random.Range(safeMinimum, safeMaximum + 1); // 양 끝을 포함한 무작위 방 수 반환
        }

        void OnValidate() // Inspector 지역 데이터 자동 보정
        {
            minimumRoomCount = Mathf.Max(1, minimumRoomCount); // 최소 방 수 보정
            maximumRoomCount = Mathf.Max(minimumRoomCount, maximumRoomCount); // 최대 방 수 보정
            travelCost = Mathf.Max(0, travelCost); // 이동 비용 보정
            trapSpawnMultiplier = Mathf.Max(0f, trapSpawnMultiplier); // 임시 함정 배율 보정
        }

    }
}