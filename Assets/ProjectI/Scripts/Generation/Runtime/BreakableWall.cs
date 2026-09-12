using System.Collections; // 파괴 연출 Coroutine 사용
using ProjectI.Combat; // 공통 체력·Damage Pipeline 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 통로를 막는 충돌체
    [RequireComponent(typeof(CombatHealth))] // 공통 피해 대상
    public sealed class BreakableWall : MonoBehaviour // 부수면 비밀방으로 가는 통로가 열리는 금 간 벽
    {
        [SerializeField] private float breakDuration = 0.45f; // 무너지는 연출 시간
        private CombatHealth health; // 공통 체력
        private bool isBroken; // 파괴 여부

        public bool IsBroken => isBroken; // 파괴 여부 공개
        public float CurrentHealth => health == null ? 0f : health.CurrentHealth; // 남은 내구도 공개
        public string Prompt => "금 간 벽 — 부수면 길이 열린다"; // 안내 문구 (조사용)

        private void Awake() // 참조 확보
        {
            health = GetComponent<CombatHealth>(); // 공통 체력
        }

        private void OnEnable() // 사망(파괴) 이벤트 구독
        {
            health = health != null ? health : GetComponent<CombatHealth>(); // 참조 보정
            health.Died += HandleBroken; // 구독
        }

        private void OnDisable() // 구독 해제
        {
            if (health != null) // 참조 확인
            {
                health.Died -= HandleBroken; // 해제
            }
        }

        public void Configure(float maxHealth) // 생성기 구성
        {
            health = health != null ? health : GetComponent<CombatHealth>(); // 참조 보정
            health.Configure("금 간 벽", CombatFaction.Neutral, Mathf.Max(1f, maxHealth)); // 플레이어가 공격할 수 있는 중립 파괴 대상
        }

        public void Break() // 즉시 파괴 (검증·디버그용)
        {
            HandleBroken(); // 파괴 처리
        }

        private void HandleBroken() // 벽이 부서져 통로가 열림
        {
            if (isBroken) // 중복 확인
            {
                return; // 종료
            }

            isBroken = true; // 상태 기록
            Collider blocker = GetComponent<Collider>(); // 통로 차단 충돌체

            if (blocker != null) // 확인
            {
                blocker.enabled = false; // 즉시 통과 가능
            }

            if (isActiveAndEnabled) // 연출 가능 확인
            {
                StartCoroutine(BreakRoutine()); // 무너지는 연출
                return; // 종료
            }

            gameObject.SetActive(false); // 즉시 제거
        }

        private IEnumerator BreakRoutine() // 벽이 바닥으로 무너지는 연출
        {
            Vector3 start = transform.localPosition; // 시작 위치
            Vector3 end = start + (Vector3.down * (transform.localScale.y + 0.2f)); // 바닥 아래
            float elapsed = 0f; // 경과

            while (elapsed < breakDuration) // 연출
            {
                elapsed += Time.deltaTime; // 시간 누적
                transform.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / breakDuration)); // 내려감
                yield return null; // 다음 프레임
            }

            gameObject.SetActive(false); // 완전히 치움
        }
    }
}
