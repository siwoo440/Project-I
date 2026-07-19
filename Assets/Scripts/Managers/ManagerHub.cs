using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class ManagerHub : MonoBehaviour // Scene의 주요 매니저 참조를 한곳에서 관리
    {
        [Header("던전 시스템")] // 던전 관련 참조 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 절차적 던전 생성기
        [SerializeField] DungeonTimeSystem dungeonTimeSystem; // 던전 제한시간 시스템
        [SerializeField] LightSystem lightSystem; // 현재 방 밝기 관리 시스템

        [Header("스폰 시스템")] // 자동 생성 매니저 참조 구분
        [SerializeField] MonsterSpawnManager monsterSpawnManager; // 일반 몬스터 스폰 매니저
        [SerializeField] TreasureSpawnManager treasureSpawnManager; // 보물과 미믹 스폰 매니저
        [SerializeField] TrapSpawnManager trapSpawnManager; // 함정 스폰 매니저
        [SerializeField] StalkerSpawnManager stalkerSpawnManager; // 스토커 스폰 매니저
        [SerializeField] GimmickMonsterSpawnManager gimmickSpawnManager; // 고스트와 웃는 석상 스폰 매니저

        [Header("영구 시스템")] // Scene 전환 후 유지되는 매니저 참조 구분
        [SerializeField] AudioManager audioManager; // 효과음과 AudioSource 풀 관리
        [SerializeField] ParticleEffectPool particleEffectPool; // 파티클 풀 관리
        [SerializeField] RunResultManager runResultManager; // Scene 간 던전 결과 보관 매니저

        [Header("검증 시스템")] // 개발용 검사 시스템 참조 구분
        [SerializeField] VerticalSliceValidator verticalSliceValidator; // 수직 슬라이스 구성 검사
        [SerializeField] RuntimePerformanceMonitor performanceMonitor; // 성능과 반복 생성 검사
       

        public DungeonGenerator DungeonGenerator => dungeonGenerator; // 던전 생성기 공개 접근점
        public DungeonTimeSystem DungeonTimeSystem => dungeonTimeSystem; // 제한시간 시스템 공개 접근점
        public LightSystem LightSystem => lightSystem; // 밝기 시스템 공개 접근점
        public MonsterSpawnManager MonsterSpawnManager => monsterSpawnManager; // 몬스터 스폰 매니저 공개 접근점
        public TreasureSpawnManager TreasureSpawnManager => treasureSpawnManager; // 보물 스폰 매니저 공개 접근점
        public TrapSpawnManager TrapSpawnManager => trapSpawnManager; // 함정 스폰 매니저 공개 접근점
        public StalkerSpawnManager StalkerSpawnManager => stalkerSpawnManager; // 스토커 스폰 매니저 공개 접근점
        public GimmickMonsterSpawnManager GimmickSpawnManager => gimmickSpawnManager; // 기믹 스폰 매니저 공개 접근점
        public AudioManager AudioManager => audioManager; // 효과음 매니저 공개 접근점
        public ParticleEffectPool ParticleEffectPool => particleEffectPool; // 파티클 풀 공개 접근점
        public VerticalSliceValidator VerticalSliceValidator => verticalSliceValidator; // 수직 슬라이스 검사기 공개 접근점
        public RuntimePerformanceMonitor PerformanceMonitor => performanceMonitor; // 성능 검사기 공개 접근점
        public RunResultManager RunResultManager => runResultManager; // 던전 결과 매니저 공개 접근점
        void Reset() // 컴포넌트를 처음 추가할 때 참조 자동 검색
        {
            ResolveReferences(); // 현재 Scene의 매니저 참조 연결
        }

        void Awake() // 실행 시 누락된 매니저 참조 보정
        {
            ResolveReferences(); // 누락된 매니저 참조 자동 검색
            ValidateReferences(); // 최종 매니저 구성 검사
        }

        [ContextMenu("매니저 참조 자동 연결")]
        public void ResolveReferences() // 현재 Scene에서 누락된 매니저 참조 자동 검색
        {
            dungeonGenerator        ??= FindFirstObjectByType<DungeonGenerator>(); // 던전 생성기 검색
            dungeonTimeSystem       ??= FindFirstObjectByType<DungeonTimeSystem>(); // 제한시간 시스템 검색
            lightSystem             ??= FindFirstObjectByType<LightSystem>(); // 밝기 시스템 검색
            monsterSpawnManager     ??= FindFirstObjectByType<MonsterSpawnManager>(); // 몬스터 스폰 매니저 검색
            treasureSpawnManager    ??= FindFirstObjectByType<TreasureSpawnManager>(); // 보물 스폰 매니저 검색
            trapSpawnManager        ??= FindFirstObjectByType<TrapSpawnManager>(); // 함정 스폰 매니저 검색
            stalkerSpawnManager     ??= FindFirstObjectByType<StalkerSpawnManager>(); // 스토커 스폰 매니저 검색
            gimmickSpawnManager     ??= FindFirstObjectByType<GimmickMonsterSpawnManager>(); // 기믹 스폰 매니저 검색
            audioManager            ??= FindFirstObjectByType<AudioManager>(); // 효과음 매니저 검색
            particleEffectPool      ??= FindFirstObjectByType<ParticleEffectPool>(); // 파티클 풀 검색
            verticalSliceValidator  ??= FindFirstObjectByType<VerticalSliceValidator>(); // 수직 슬라이스 검사기 검색
            performanceMonitor      ??= FindFirstObjectByType<RuntimePerformanceMonitor>(); // 성능 검사기 검색
            runResultManager        ??= FindFirstObjectByType<RunResultManager>(); // 던전 결과 매니저 검색
        }

        [ContextMenu("매니저 참조 검사")]
        public void ValidateReferences() // 모든 주요 매니저의 연결 여부 검사
        {
            int missingCount = 0; // 누락된 매니저 개수 초기화
            missingCount += CheckReference(dungeonGenerator      , "DungeonGenerator"); // 던전 생성기 검사
            missingCount += CheckReference(dungeonTimeSystem     , "DungeonTimeSystem"); // 제한시간 시스템 검사
            missingCount += CheckReference(lightSystem           , "LightSystem"); // 밝기 시스템 검사
            missingCount += CheckReference(monsterSpawnManager   , "MonsterSpawnManager"); // 몬스터 스폰 매니저 검사
            missingCount += CheckReference(treasureSpawnManager  , "TreasureSpawnManager"); // 보물 스폰 매니저 검사
            missingCount += CheckReference(trapSpawnManager      , "TrapSpawnManager"); // 함정 스폰 매니저 검사
            missingCount += CheckReference(stalkerSpawnManager   , "StalkerSpawnManager"); // 스토커 스폰 매니저 검사
            missingCount += CheckReference(gimmickSpawnManager   , "GimmickMonsterSpawnManager"); // 기믹 스폰 매니저 검사
            missingCount += CheckReference(audioManager          , "AudioManager"); // 효과음 매니저 검사
            missingCount += CheckReference(particleEffectPool    , "ParticleEffectPool"); // 파티클 풀 검사
            missingCount += CheckReference(verticalSliceValidator, "VerticalSliceValidator"); // 수직 슬라이스 검사기 검사
            missingCount += CheckReference(performanceMonitor    , "RuntimePerformanceMonitor"); // 성능 검사기 검사
            missingCount += CheckReference(runResultManager      , "RunResultManager"); // 던전 결과 매니저 검사

            if (missingCount == 0) // 누락된 매니저가 없는지 확인
            {
                Debug.Log("[ManagerHub] 모든 주요 매니저 참조가 연결되었습니다."); // 정상 검사 결과 출력
            }
            else // 누락된 매니저가 있는 경우
            {
                Debug.LogError($"[ManagerHub] 누락된 매니저가 {missingCount}개 있습니다."); // 누락된 매니저 수 출력
            }
        }

        int CheckReference(Object target, string managerName) // 전달된 매니저 참조 존재 여부 확인
        {
            if (target != null) // 매니저 참조가 연결되었는지 확인
            {
                return 0; // 누락 없음 반환
            }

            Debug.LogError($"[ManagerHub] {managerName} 참조가 없습니다."); // 개별 누락 매니저 출력
            return 1; // 누락 한 개 반환
        }
    }
}
