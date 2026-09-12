using System.Collections; // 여닫기 연출 Coroutine 사용
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Wagon // 마차 기능 네임스페이스
{
    [DisallowMultipleComponent] // 마차당 하나
    public sealed class WagonTravelCover : MonoBehaviour // 이동 중 바깥을 가리는 마차 천막 (도착하면 걷힘)
    {
        [SerializeField] private Transform[] panels = new Transform[0]; // 말려 올라가는 천막 패널 (상단·측면 기준점)
        [SerializeField] private float animationDuration = 0.8f; // 천막을 치고 걷는 시간
        [SerializeField] private float rolledScale = 0.04f; // 완전히 걷혔을 때 남는 두께 비율
        private Coroutine routine; // 진행 중 연출
        private float closedAmount; // 0 = 완전히 걷힘, 1 = 완전히 닫힘

        public bool IsClosed => closedAmount > 0.999f; // 완전히 닫혔는지 공개
        public bool IsOpen => closedAmount < 0.001f; // 완전히 걷혔는지 공개
        public bool IsAnimating => routine != null; // 연출 중 여부 공개
        public float ClosedAmount => closedAmount; // 진행도 공개
        public int PanelCount => panels == null ? 0 : panels.Length; // 패널 수 공개

        public void Configure(Transform[] targetPanels) // 에디터 구성
        {
            panels = targetPanels ?? new Transform[0]; // 패널 목록 저장
        }

        private void Awake() // 시작 시 걷힌 상태로 초기화
        {
            SetClosedImmediate(false); // 사무소에서는 열린 상태
        }

        public void SetClosedImmediate(bool closed) // 연출 없이 즉시 적용 (복구·초기화용)
        {
            StopRoutine(); // 진행 중 연출 중단
            closedAmount = closed ? 1f : 0f; // 상태 기록
            ApplyAmount(closedAmount); // 즉시 반영
        }

        public void Close() // 천막을 쳐서 바깥을 가림
        {
            StartAnimation(1f); // 닫기 연출
        }

        public void Open() // 천막을 걷어 바깥이 보이게 함
        {
            StartAnimation(0f); // 열기 연출
        }

        public IEnumerator CloseRoutine() // 닫힘이 끝날 때까지 기다리는 연출 (이동 절차용)
        {
            Close(); // 닫기 시작

            while (routine != null) // 연출 대기
            {
                yield return null; // 다음 프레임
            }
        }

        public IEnumerator OpenRoutine() // 열림이 끝날 때까지 기다리는 연출 (도착용)
        {
            Open(); // 열기 시작

            while (routine != null) // 연출 대기
            {
                yield return null; // 다음 프레임
            }
        }

        private void StartAnimation(float target) // 목표 상태로 연출 시작
        {
            if (!isActiveAndEnabled) // 비활성 상태 확인
            {
                closedAmount = target; // 상태만 기록
                ApplyAmount(closedAmount); // 즉시 반영
                return; // 종료
            }

            StopRoutine(); // 기존 연출 중단
            routine = StartCoroutine(AnimateTo(target)); // 새 연출 시작
        }

        private void StopRoutine() // 진행 중 연출 정리
        {
            if (routine != null) // 진행 확인
            {
                StopCoroutine(routine); // 중단
                routine = null; // 참조 해제
            }
        }

        private IEnumerator AnimateTo(float target) // 천막 여닫기 연출
        {
            float start = closedAmount; // 시작 상태
            float duration = Mathf.Max(0.05f, animationDuration) * Mathf.Abs(target - start); // 남은 거리만큼만 시간 사용
            float elapsed = 0f; // 경과

            while (elapsed < duration) // 연출
            {
                elapsed += Time.deltaTime; // 시간 누적
                closedAmount = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration))); // 부드러운 진행
                ApplyAmount(closedAmount); // 반영
                yield return null; // 다음 프레임
            }

            closedAmount = target; // 최종 상태
            ApplyAmount(closedAmount); // 최종 반영
            routine = null; // 연출 종료
        }

        private void ApplyAmount(float amount) // 진행도를 패널 크기에 반영 (기준점에서 말려 올라감)
        {
            if (panels == null) // 구성 확인
            {
                return; // 종료
            }

            float scale = Mathf.Lerp(Mathf.Max(0.001f, rolledScale), 1f, Mathf.Clamp01(amount)); // 남는 두께

            foreach (Transform panel in panels) // 패널 순회
            {
                if (panel == null) // 누락 확인
                {
                    continue; // 다음
                }

                panel.localScale = new Vector3(1f, scale, 1f); // 기준점(로컬 +Y 위쪽)에서 말려 올라감
                panel.gameObject.SetActive(amount > 0.002f); // 완전히 걷히면 숨김 (충돌체까지 제거)
            }
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            animationDuration = Mathf.Clamp(animationDuration, 0.05f, 5f); // 연출 시간 범위
            rolledScale = Mathf.Clamp(rolledScale, 0.001f, 0.5f); // 걷힌 두께 범위
        }
    }
}
