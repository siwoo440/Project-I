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

        [Header("Scene")] // Inspector Scene 설정 구분
        [Tooltip("선택 후 이동할 Dungeon Scene 이름")] [SerializeField] string sceneName = "Dungeon"; // 선택 후 이동할 Dungeon Scene 이름

        [Header("난이도")] // Inspector 던전 난이도 설정 구분
        [Tooltip("화면에 표시할 위험 단계")] [SerializeField][Range(1, 5)] int dangerLevel = 1; // 화면에 표시할 위험 단계
        [Tooltip("생성할 던전 방 개수")] [SerializeField][Min(8)] int roomCount = 8; // 생성할 던전 방 개수
        [Tooltip("일반 몬스터 생성 수 배율")] [SerializeField][Min(0f)] float monsterSpawnMultiplier = 1f; // 일반 몬스터 생성 수 배율
        [Tooltip("함정 생성 수 배율")] [SerializeField][Min(0f)] float trapSpawnMultiplier = 1f; // 함정 생성 수 배율
        [Tooltip("보물 가치 배율")] [SerializeField][Min(0f)] float rewardValueMultiplier = 1f; // 보물 가치 배율
        [Tooltip("경로마다 다른 던전 배치를 만들 시드 보정값")] [SerializeField] int seedOffset; // 경로마다 다른 던전 배치를 만들 시드 보정값

        public string RouteId => routeId; // 경로 고유 ID 반환
        public string DisplayName => displayName; // 던전 표시 이름 반환
        public string Description => description; // 던전 설명 반환
        public string SceneName => sceneName; // 이동할 Scene 이름 반환
        public int DangerLevel => dangerLevel; // 위험 단계 반환
        public int RoomCount => roomCount; // 생성할 방 개수 반환
        public float MonsterSpawnMultiplier => monsterSpawnMultiplier; // 몬스터 생성 배율 반환
        public float TrapSpawnMultiplier => trapSpawnMultiplier; // 함정 생성 배율 반환
        public float RewardValueMultiplier => rewardValueMultiplier; // 보물 가치 배율 반환
        public int SeedOffset => seedOffset; // 던전 시드 보정값 반환
    }
}