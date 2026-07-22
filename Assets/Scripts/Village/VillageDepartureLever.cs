using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageDepartureLever : MonoBehaviour, IInteractable // 마차 출발을 요청하는 상호작용 레버
    {
        [Header("마차 연결")] // Inspector 마차 참조 구분
        [Tooltip("출발시킬 마차 이동 컴포넌트")] [SerializeField] VillageWagonTravelController wagonTravel; // 출발시킬 마차 이동 컴포넌트

        [Header("레버 연출")] // Inspector 임시 레버 연출 설정 구분
        [Tooltip("작동 후 레버 로컬 회전값")] [SerializeField] Vector3 pulledLocalEulerAngles = new Vector3(0f, 0f, 45f); // 작동 후 레버 로컬 회전값

        bool pulled; // 레버 사용 완료 여부

        void Reset() // 컴포넌트 추가 시 부모 마차 자동 검색
        {
            wagonTravel = GetComponentInParent<VillageWagonTravelController>(); // 부모에서 마차 이동 컴포넌트 검색
        }

        void Awake() // 실행 시 마차 참조 누락 보정
        {
            if (wagonTravel == null) // Inspector 참조 누락 여부 확인
            {
                wagonTravel = GetComponentInParent<VillageWagonTravelController>(); // 부모에서 마차 이동 컴포넌트 재검색
            }
        }

        public string GetPrompt() // 플레이어 화면에 표시할 상호작용 문구 반환
        {
            if (wagonTravel == null) // 마차 이동 컴포넌트 연결 여부 확인
            {
                return "마차 이동 컴포넌트 연결 필요"; // 참조 누락 안내 반환
            }

            return wagonTravel.GetDeparturePrompt(); // 현재 마차 상태에 맞는 문구 반환
        }

        public void Interact(PlayerInteractor interactor) // E 입력 시 마차 출발 요청
        {
            if (pulled) // 이미 작동한 레버인지 확인
            {
                return; // 중복 작동 방지
            }

            if (wagonTravel == null) // 마차 이동 컴포넌트 연결 여부 확인
            {
                Debug.LogError("[VillageDepartureLever] VillageWagonTravelController가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 상호작용 중단
            }

            if (wagonTravel.TryStartTravel(interactor)) // 실제 마차 출발 성공 여부 확인
            {
                pulled = true; // 레버 사용 완료 상태 저장
                transform.localRotation = Quaternion.Euler(pulledLocalEulerAngles); // 임시 레버 당김 회전 적용
                Debug.Log("[VillageDepartureLever] 출발 레버가 작동했습니다."); // 레버 작동 결과 출력
            }
        }
    }
}