using System.Collections.Generic; // 확보 아이템 집합과 정리 버퍼 기능 참조
using ProjectI.Items; // 기존 WorldItem 기능 참조
using UnityEngine; // 유니티 물리 Trigger와 Transform 기능 참조

namespace ProjectI.Wagon // 마차 시스템 네임스페이스
{
    [RequireComponent(typeof(BoxCollider))] // 적재 판정용 Trigger BoxCollider 필수 지정
    public sealed class WagonCargoArea : MonoBehaviour // 마차 뒤 대형 창고 내부 회수품 확보 판정
    {
        [SerializeField] private BoxCollider cargoTrigger; // 실제 적재 판정 범위
        private readonly HashSet<WorldItem> securedItems = new HashSet<WorldItem>(); // 현재 확보된 월드 아이템 집합
        private readonly List<WorldItem> cleanupBuffer = new List<WorldItem>(); // FixedUpdate 안전 정리용 임시 목록

        public int SecuredCount => securedItems.Count; // 현재 확보된 회수품 개수 공개

        private void Awake() // 적재 구역 초기화
        {
            ResolveTrigger(); // BoxCollider 참조 확보
        }

        private void FixedUpdate() // 들기·보관·범위 이탈 상태를 물리 프레임마다 정리
        {
            cleanupBuffer.Clear(); // 이전 정리 후보 초기화

            foreach (WorldItem item in securedItems) // 현재 확보 아이템 순회
            {
                if (item == null || item.IsHeld || item.IsStored || !ContainsItemCenter(item)) // 확보 조건이 깨졌는지 확인
                {
                    cleanupBuffer.Add(item); // 확보 해제 대상에 등록
                }
            }

            foreach (WorldItem item in cleanupBuffer) // 정리 대상 순회
            {
                Release(item); // 확보 상태 해제
            }
        }

        public void Configure(BoxCollider trigger) // 에디터 프리팹 생성용 적재 Trigger 지정
        {
            cargoTrigger = trigger; // 적재 Trigger 참조 저장

            if (cargoTrigger != null) // 유효 Trigger 확인
            {
                cargoTrigger.isTrigger = true; // 물리 충돌 대신 진입·이탈 판정만 사용
            }
        }

        public bool IsSecured(WorldItem item) // 특정 아이템의 현재 확보 여부 반환
        {
            return item != null && securedItems.Contains(item); // 확보 집합 포함 여부 반환
        }

        public void Release(WorldItem item) // 외부 또는 다른 마차에서 특정 아이템 확보 해제
        {
            if (item == null) // 이미 파괴된 아이템 여부 확인
            {
                securedItems.Remove(item); // null 엔트리 제거 시도
                return; // 추가 상태 처리 중단
            }

            securedItems.Remove(item); // 현재 적재 구역 집합에서 제거
            WagonCargoItemState state = item.GetComponent<WagonCargoItemState>(); // 아이템 확보 상태 컴포넌트 조회

            if (state != null && state.SecuredArea == this) // 현재 마차가 부여한 상태인지 확인
            {
                state.SetSecured(null, false); // 아이템의 확보 상태 해제
            }
        }

        private void OnTriggerEnter(Collider other) // 아이템이 창고 Trigger에 들어온 순간 처리
        {
            TrySecure(other); // 유효한 WorldItem이면 확보 처리
        }

        private void OnTriggerStay(Collider other) // Collider 활성화가 Trigger 내부에서 시작된 경우 보정
        {
            TrySecure(other); // G 내려놓기 직후에도 확보 판정 재시도
        }

        private void OnTriggerExit(Collider other) // 아이템이 창고 밖으로 나간 순간 처리
        {
            WorldItem item = other == null ? null : other.GetComponentInParent<WorldItem>(); // 이탈 Collider에서 WorldItem 조회

            if (item != null) // 유효 월드 아이템 확인
            {
                Release(item); // 창고 밖으로 나가면 미확보 상태로 복귀
            }
        }

        private void OnDisable() // 마차 비활성화 시 확보 상태 정리
        {
            cleanupBuffer.Clear(); // 정리 버퍼 초기화
            cleanupBuffer.AddRange(securedItems); // 현재 확보 항목 전체 복사

            foreach (WorldItem item in cleanupBuffer) // 확보 아이템 순회
            {
                Release(item); // 모든 확보 상태 해제
            }
        }

        private void TrySecure(Collider source) // Trigger Collider에서 WorldItem 확보 시도
        {
            if (source == null) // Collider 유효성 확인
            {
                return; // 확보 처리 중단
            }

            WorldItem item = source.GetComponentInParent<WorldItem>(); // Collider 부모 방향에서 기존 WorldItem 조회

            if (item == null || item.IsHeld || item.IsStored || !ContainsItemCenter(item)) // 월드 상태와 실제 창고 내부 여부 확인
            {
                return; // 확보 조건 미충족
            }

            WagonCargoItemState state = item.GetComponent<WagonCargoItemState>(); // 기존 확보 상태 컴포넌트 조회

            if (state == null) // Day21 이전 아이템이라 상태 컴포넌트가 없는지 확인
            {
                state = item.gameObject.AddComponent<WagonCargoItemState>(); // 기존 WorldItem에 최소 상태 컴포넌트 추가
            }

            if (state.SecuredArea != null && state.SecuredArea != this) // 다른 마차에서 확보 중인지 확인
            {
                state.SecuredArea.Release(item); // 이전 마차 확보 상태 먼저 해제
            }

            securedItems.Add(item); // 현재 마차 확보 집합에 등록
            state.SetSecured(this, true); // 아이템을 확보 상태로 표시
        }

        private bool ContainsItemCenter(WorldItem item) // 아이템 중심점이 회전된 BoxCollider 내부인지 검사
        {
            ResolveTrigger(); // 적재 Trigger 참조 확보

            if (cargoTrigger == null || item == null) // 필요한 참조 유효성 확인
            {
                return false; // 내부 판정 실패
            }

            Vector3 localPoint = cargoTrigger.transform.InverseTransformPoint(item.transform.position) - cargoTrigger.center; // 아이템 중심을 Trigger 로컬 좌표로 변환
            Vector3 halfSize = cargoTrigger.size * 0.5f; // 로컬 Trigger 반크기 계산
            return Mathf.Abs(localPoint.x) <= halfSize.x && Mathf.Abs(localPoint.y) <= halfSize.y && Mathf.Abs(localPoint.z) <= halfSize.z; // 세 축 모두 범위 안이면 확보 가능
        }

        private void ResolveTrigger() // 적재 Trigger 참조 자동 확보
        {
            if (cargoTrigger == null) // 인스펙터 참조 누락 확인
            {
                cargoTrigger = GetComponent<BoxCollider>(); // 같은 GameObject BoxCollider 자동 조회
            }

            if (cargoTrigger != null) // 유효 Collider 확인
            {
                cargoTrigger.isTrigger = true; // 항상 Trigger 상태 유지
            }
        }
    }
}
