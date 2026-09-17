using System.Collections; // 벨 흔들림 연출 코루틴 사용
using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 플레이어 시선 상호작용용 Collider 필수 지정
    public sealed class OfficeSaleBell : MonoBehaviour, IInteractable // 판매대 옆 벨: 누르면 판매할 물건을 고르는 창을 엶 (32일차)
    {
        [SerializeField] private OfficeSaleCounter counter; // 연결된 판매대
        [SerializeField] private OfficeSalePanel panel; // 판매 선택 창
        [SerializeField] private Transform ringVisual; // 눌렀을 때 흔들릴 외형
        [SerializeField] private AudioSource ringAudio; // 벨 소리 (선택)

        public string Prompt => BuildPrompt(); // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번 누르기
        public float HoldDuration => 0f; // 길게 누르기 불필요
        public OfficeSaleCounter Counter => counter; // 연결 판매대 공개
        public OfficeSalePanel Panel => panel; // 선택 창 공개

        private void Awake() // 참조 초기화
        {
            ResolveReferences(); // 판매대·창 확보
        }

        public void Configure(OfficeSaleCounter targetCounter, OfficeSalePanel targetPanel, Transform targetRingVisual) // 에디터 자동 구성
        {
            counter = targetCounter; // 판매대
            panel = targetPanel; // 창
            ringVisual = targetRingVisual; // 외형
        }

        public bool CanInteract(PlayerInteractor interactor) // 판매대가 연결되어 있으면 항상 누를 수 있음
        {
            ResolveReferences(); // 참조 확보
            return interactor != null && counter != null && panel != null && !panel.IsOpen; // 창이 닫혀 있을 때
        }

        public void Interact(PlayerInteractor interactor) // 벨을 울리고 판매 선택 창을 엶
        {
            ResolveReferences(); // 참조 확보

            if (counter == null || panel == null) // 필수 참조
            {
                return; // 중단
            }

            Ring(); // 벨 연출

            if (counter.CollectPlacedItems().Count == 0) // 올린 물건 없음
            {
                Debug.Log("[Project I] 판매 벨 / 판매대에 올린 회수품이 없습니다", this); // 안내
                return; // 창을 열지 않음
            }

            panel.Open(counter, interactor); // 선택 창 열기
        }

        private void Ring() // 소리·흔들림
        {
            if (ringAudio != null) // 소리
            {
                ringAudio.Play(); // 재생
            }

            if (ringVisual != null && isActiveAndEnabled) // 흔들림
            {
                StopAllCoroutines(); // 이전 연출 중단
                StartCoroutine(RingRoutine()); // 연출
            }
        }

        private IEnumerator RingRoutine() // 벨이 잠깐 좌우로 흔들림
        {
            Quaternion baseRotation = ringVisual.localRotation; // 원래 회전
            float elapsed = 0f; // 경과

            while (elapsed < 0.4f) // 0.4초
            {
                elapsed += Time.deltaTime; // 경과
                float angle = Mathf.Sin(elapsed * 60f) * 12f * (1f - (elapsed / 0.4f)); // 줄어드는 흔들림
                ringVisual.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle); // 적용
                yield return null; // 다음 프레임
            }

            ringVisual.localRotation = baseRotation; // 원복
        }

        private string BuildPrompt() // 판매대 상태에 맞는 안내
        {
            ResolveReferences(); // 참조 확보

            if (counter == null) // 판매대 없음
            {
                return "판매 벨"; // 기본
            }

            int count = 0; // 올린 수
            int total = 0; // 합계

            foreach (var item in counter.CollectPlacedItems()) // 올린 물건 순회
            {
                count++; // 집계
                total += counter.PriceOf(item); // 합계
            }

            return count == 0 ? "판매 벨 — 판매대에 올린 회수품 없음" : $"벨 울리기 — 판매할 물건 고르기 ({count}개 / 최대 {total})"; // 안내
        }

        private void ResolveReferences() // 참조 자동 확보
        {
            if (counter == null) // 판매대 누락
            {
                counter = GetComponentInParent<OfficeSaleCounter>(); // 부모에서 조회

                if (counter == null && transform.parent != null) // 형제에서 조회
                {
                    counter = transform.parent.GetComponentInChildren<OfficeSaleCounter>(true); // 같은 부모 아래
                }
            }

            if (panel == null) // 창 누락
            {
                panel = GetComponent<OfficeSalePanel>(); // 같은 오브젝트

                if (panel == null) // 없으면 추가
                {
                    panel = gameObject.AddComponent<OfficeSalePanel>(); // 런타임 안전용
                }
            }
        }
    }
}
