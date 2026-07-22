using System; // 외부에 NavMesh 생성 완료 이벤트를 전달하기 위해 사용
using Unity.AI.Navigation; // NavMeshSurface 기능을 사용하기 위해 추가
using UnityEngine; // Unity 기본 컴포넌트와 로그 기능 사용
using UnityEngine.AI; // NavMesh 위치 검사 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [DefaultExecutionOrder(-200)] // 다른 스폰 매니저보다 먼저 던전 생성 이벤트를 구독
    [DisallowMultipleComponent] // 같은 오브젝트에 중복 추가되는 것을 방지
    [RequireComponent(typeof(NavMeshSurface))] // 런타임 NavMesh 생성을 위한 Surface 필수 지정
    public class RuntimeNavMeshBuilder : MonoBehaviour // 절차적 던전용 런타임 NavMesh 생성 담당
    {
        [Header("필수 참조")] // Inspector 필수 참조 구분
        [Tooltip("던전 생성 완료 이벤트를 제공하는 생성기")] [SerializeField] DungeonGenerator dungeonGenerator; // 던전 생성 완료 이벤트를 제공하는 생성기
        [Tooltip("실제 NavMesh를 생성할 Surface")] [SerializeField] NavMeshSurface navMeshSurface; // 실제 NavMesh를 생성할 Surface

        [Header("검증 설정")] // NavMesh 생성 결과 확인 설정 구분
        [Tooltip("시작 방 주변 NavMesh 검색 거리")] [SerializeField] float startRoomSampleDistance = 4f; // 시작 방 주변 NavMesh 검색 거리
        [Tooltip("NavMesh 생성 결과 로그 출력 여부")] [SerializeField] bool showDebug = true; // NavMesh 생성 결과 로그 출력 여부

        public bool IsReady { get; private set; } // 현재 NavMesh 사용 가능 여부
        public event Action NavigationBuilt; // 외부 시스템에 NavMesh 생성 완료 전달

        void Awake() // 던전 생성기와 NavMeshSurface 참조 초기화
        {
            if (dungeonGenerator == null) { dungeonGenerator = GetComponent<DungeonGenerator>(); }
            // 같은 오브젝트에서 DungeonGenerator 검색 // DungeonGenerator가 Inspector에 연결되지 않았는지 확인

            if (dungeonGenerator == null) { dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); }
            // 같은 오브젝트에서 찾지 못했는지 확인// 현재 Scene 전체에서 DungeonGenerator 검색

            if (navMeshSurface == null) { navMeshSurface = GetComponent<NavMeshSurface>(); }
            // NavMeshSurface가 Inspector에 연결되지 않았는지 확인// 같은 오브젝트에서 NavMeshSurface 검색
        }

        void OnEnable() // 던전 생성 이벤트 구독
        {
            if (dungeonGenerator == null) { return; } // 던전 생성기 참조가 없는지 확인// 이벤트 구독 중단

            dungeonGenerator.GenerationStarted += HandleGenerationStarted; // 재생성 시작 시 기존 NavMesh 제거 연결
            dungeonGenerator.GenerationCompleted += HandleGenerationCompleted; // 방 생성 완료 시 NavMesh 생성 연결
        }

        void Start() // 이벤트를 놓친 경우를 대비해 생성 상태 확인
        {
            if (dungeonGenerator != null && dungeonGenerator.GenerationCount > 0 && !IsReady) // 던전은 생성됐지만 NavMesh가 준비되지 않았는지 확인
            {
                RebuildNavigation(); // 현재 생성된 방을 기반으로 NavMesh 생성
            }
        }

        void OnDisable() // 던전 생성 이벤트 구독 해제
        {
            if (dungeonGenerator == null) // 던전 생성기 참조가 없는지 확인
            {
                return; // 이벤트 구독 해제 중단
            }

            dungeonGenerator.GenerationStarted -= HandleGenerationStarted; // 재생성 시작 이벤트 연결 해제
            dungeonGenerator.GenerationCompleted -= HandleGenerationCompleted; // 생성 완료 이벤트 연결 해제
        }

        void HandleGenerationStarted() // 새로운 던전 생성 시작 처리
        {
            IsReady = false; // 기존 NavMesh를 사용할 수 없는 상태로 변경

            if (navMeshSurface != null) // NavMeshSurface가 존재하는지 확인
            {
                navMeshSurface.RemoveData(); // 이전 던전에서 생성한 NavMesh 데이터 제거
            }
        }

        void HandleGenerationCompleted() // 던전 방 배치 완료 처리
        {
            RebuildNavigation(); // 새로 생성된 방을 기반으로 NavMesh 다시 생성
        }

        public void RebuildNavigation() // 현재 DungeonGenerator 자식 구조를 기반으로 NavMesh 생성
        {
            if (navMeshSurface == null) // NavMeshSurface 참조가 없는지 확인
            {
                Debug.LogError("[RuntimeNavMeshBuilder] NavMeshSurface가 연결되지 않았습니다."); // 필수 참조 오류 출력
                IsReady = false; // NavMesh 사용 불가 상태 저장
                return; // NavMesh 생성 중단
            }

            navMeshSurface.BuildNavMesh(); // 현재 활성화된 방과 Collider를 기반으로 NavMesh 생성

            bool foundNavigation = false; // 실제 NavMesh 생성 여부 저장

            if (dungeonGenerator != null && dungeonGenerator.StartRoom != null) // 시작 방이 정상적으로 생성되었는지 확인
            {
                Vector3 sampleOrigin = dungeonGenerator.StartRoom.transform.position + Vector3.up * 0.2f; // 시작 방 바닥 주변 검색 위치 계산
                float safeDistance = Mathf.Max(0.1f, startRoomSampleDistance); // 검색 거리가 너무 작거나 음수가 되지 않도록 제한

                foundNavigation = NavMesh.SamplePosition( // 시작 방 주변에서 NavMesh 위치 검색
                    sampleOrigin, // 검색할 시작 위치
                    out NavMeshHit navigationHit, // 검색된 NavMesh 정보 저장
                    safeDistance, // 최대 검색 거리
                    NavMesh.AllAreas); // 모든 NavMesh 영역 검색
            }

            IsReady = foundNavigation; // 실제 검색 결과를 NavMesh 준비 상태로 저장

            if (!IsReady) // 시작 방 주변에서 NavMesh를 찾지 못했는지 확인
            {
                Debug.LogError("[RuntimeNavMeshBuilder] NavMesh를 생성했지만 시작 방에서 이동 가능 영역을 찾지 못했습니다."); // Surface 또는 바닥 Collider 설정 오류 출력
                return; // 완료 이벤트 전달 중단
            }

            NavigationBuilt?.Invoke(); // 외부 시스템에 NavMesh 생성 완료 전달

            if (showDebug) // 디버그 로그 표시 여부 확인
            {
                Debug.Log("[RuntimeNavMeshBuilder] 절차적 던전 NavMesh 생성 완료"); // 정상 생성 결과 출력
            }
        }
    }
}