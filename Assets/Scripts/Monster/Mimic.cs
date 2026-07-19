using System.Collections; // 위장 해제 지연과 기습 처리
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(MonsterAI))] // 위장 해제 후 사용할 MonsterAI 필수 지정
    [RequireComponent(typeof(CharacterController))] // 미믹 이동과 충돌을 위한 CharacterController 필수 지정
    public class Mimic : MonoBehaviour, IInteractable // 보물상자로 위장한 뒤 플레이어를 기습하는 몬스터
    {
        [Header("위장 외형")] // Inspector 위장 외형 설정 구분
        [SerializeField] GameObject disguiseVisual; // 위장 상태에서 표시할 보물상자 외형
        [SerializeField] GameObject monsterVisual; // 위장 해제 후 표시할 몬스터 외형
        [SerializeField] string disguiseName = "낡은 보물상자"; // 상호작용 안내에 표시할 가짜 보물 이름

        [Header("위장 해제")] // Inspector 위장 해제 설정 구분
        [SerializeField] bool revealOnProximity = true; // 플레이어가 가까이 접근하면 위장을 해제할지 결정
        [SerializeField] float proximityRevealDistance = 1.25f; // 접근으로 위장을 해제하는 거리
        [SerializeField] float revealDelay = 0.25f; // 위장 외형 변경 후 공격을 시작할 때까지의 지연시간

        [Header("기습 공격")] // Inspector 기습 공격 설정 구분
        [SerializeField] float ambushDamage = 25f; // 위장 해제 시 플레이어에게 줄 기습 피해
        [SerializeField] float ambushRange = 2.2f; // 기습 피해가 적용되는 최대 거리

        [Header("디버그")] // Inspector 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 미믹 상태 변경 로그를 출력할지 결정

        MonsterAI monsterAI; // 위장 해제 후 작동시킬 기존 몬스터 AI
        PlayerController player; // 접근 거리와 기습 대상을 확인할 플레이어
        bool isRevealed; // 미믹이 이미 위장을 해제했는지 저장

        public bool IsRevealed => isRevealed; // 외부에서 미믹 위장 해제 상태 확인

        void Awake() // 미믹 AI와 위장 외형 초기화
        {
            monsterAI = GetComponent<MonsterAI>(); // 같은 오브젝트의 MonsterAI 가져오기
            monsterAI.Damaged += HandleDamaged; // 몬스터 피격 이벤트에 위장 해제 처리 연결
            monsterAI.enabled = false; // 위장 중에는 기존 몬스터 AI 정지

            SetRevealVisual(false); // 게임 시작 시 보물상자 위장 외형 표시
        }

        void Start() // 현재 Scene의 플레이어 검색
        {
            player = FindFirstObjectByType<PlayerController>(); // 접근 감지와 기습에 사용할 플레이어 저장
        }

        void Update() // 위장 중 플레이어 접근 거리 확인
        {
            if (isRevealed || !revealOnProximity || player == null) // 접근 감지를 실행할 수 있는 상태인지 확인
            {
                return; // 접근 감지 중단
            }

            Vector3 difference = player.transform.position - transform.position; // 미믹에서 플레이어까지의 방향 계산
            difference.y = 0f; // 높이 차이를 거리 계산에서 제외

            if (difference.magnitude <= proximityRevealDistance) // 플레이어가 접근 감지 거리 안에 들어왔는지 확인
            {
                Reveal(player); // 접근한 플레이어를 대상으로 위장 해제
            }
        }

        void OnDestroy() // 미믹 제거 시 피격 이벤트 연결 해제
        {
            if (monsterAI != null) // MonsterAI가 존재하는지 확인
            {
                monsterAI.Damaged -= HandleDamaged; // 피격 이벤트에서 위장 해제 처리 제거
            }
        }

        public string GetPrompt() // 미믹의 현재 상태에 맞는 상호작용 안내 반환
        {
            if (isRevealed) // 이미 위장을 해제했는지 확인
            {
                return "미믹!"; // 몬스터 상태 안내 반환
            }

            return $"[E] 보물: {disguiseName}"; // 일반 보물처럼 보이는 가짜 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 위장한 미믹과 상호작용
        {
            if (isRevealed) // 이미 위장을 해제했는지 확인
            {
                return; // 중복 위장 해제 방지
            }

            PlayerController interactingPlayer = null; // 상호작용한 플레이어 저장 변수 초기화

            if (interactor != null) // 상호작용 컴포넌트가 존재하는지 확인
            {
                interactingPlayer = interactor.GetComponent<PlayerController>(); // 상호작용한 오브젝트에서 플레이어 검색
            }

            Reveal(interactingPlayer != null ? interactingPlayer : player); // 보물을 열려고 한 플레이어를 대상으로 위장 해제
        }

        void HandleDamaged() // 위장 상태에서 공격받았을 때 처리
        {
            if (player == null) // 캐시된 플레이어가 없는지 확인
            {
                player = FindFirstObjectByType<PlayerController>(); // 현재 Scene에서 플레이어 다시 검색
            }

            Reveal(player); // 공격받은 즉시 위장 해제
        }

        void Reveal(PlayerController target) // 미믹 위장을 해제하고 기습 시작
        {
            if (isRevealed) // 이미 위장을 해제했는지 확인
            {
                return; // 중복 위장 해제 방지
            }

            isRevealed = true; // 미믹을 위장 해제 상태로 변경
            SetRevealVisual(true); // 보물 외형을 숨기고 몬스터 외형 표시
            StartCoroutine(BeginAmbush(target)); // 지연 후 기습과 AI 활성화 시작

            if (showDebug) // 디버그 로그 출력 여부 확인
            {
                Debug.Log("[Mimic] 보물상자 위장을 해제했습니다."); // 위장 해제 결과 출력
            }
        }

        IEnumerator BeginAmbush(PlayerController target) // 위장 해제 지연 후 기습 피해와 추격 적용
        {
            float safeRevealDelay = Mathf.Max(0f, revealDelay); // 위장 해제 지연시간이 음수가 되지 않도록 제한

            if (safeRevealDelay > 0f) // 실제 지연시간이 있는지 확인
            {
                yield return new WaitForSeconds(safeRevealDelay); // 설정된 시간만큼 기습 발동 대기
            }

            monsterAI.enabled = true; // 기존 MonsterAI 활성화
            monsterAI.Alert(); // MonsterAI를 즉시 플레이어 추격 상태로 변경

            if (target == null || target.IsDead) // 기습 대상이 없거나 사망했는지 확인
            {
                yield break; // 기습 피해 처리 중단
            }

            Vector3 difference = target.transform.position - transform.position; // 미믹에서 대상까지의 방향 계산
            difference.y = 0f; // 높이 차이를 거리 계산에서 제외

            if (difference.magnitude <= ambushRange) // 대상이 기습 범위 안에 있는지 확인
            {
                target.TakeDamage(Mathf.Max(0f, ambushDamage)); // 플레이어에게 기습 피해 적용

                if (showDebug) // 디버그 로그 출력 여부 확인
                {
                    Debug.Log($"[Mimic] 기습 공격 — {ambushDamage:F0} 피해"); // 기습 피해 결과 출력
                }
            }
        }

        void SetRevealVisual(bool revealed) // 위장 상태에 맞게 두 외형 활성 상태 변경
        {
            if (disguiseVisual != null) // 보물상자 외형이 연결되어 있는지 확인
            {
                disguiseVisual.SetActive(!revealed); // 위장 중에만 보물상자 외형 표시
            }

            if (monsterVisual != null) // 몬스터 외형이 연결되어 있는지 확인
            {
                monsterVisual.SetActive(revealed); // 위장 해제 후에만 몬스터 외형 표시
            }
        }
    }
}