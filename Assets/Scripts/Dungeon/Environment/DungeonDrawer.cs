using System.Collections; // 서랍 이동 코루틴 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DungeonDrawer : MonoBehaviour, IInteractable // 열고 닫으며 내부 아이템을 공개하는 서랍
    {
        [Header("서랍 설정")] // Inspector 서랍 설정 구분
        [Tooltip("실제로 이동할 서랍 몸체")] [SerializeField] Transform drawerBody; // 실제로 이동할 서랍 몸체
        [Tooltip("닫힌 위치에서 열린 위치까지의 이동량")] [SerializeField] Vector3 openLocalOffset = new Vector3(0f, 0f, -0.8f); // 닫힌 위치에서 열린 위치까지의 이동량
        [Tooltip("서랍 이동에 걸리는 시간")] [SerializeField] float moveDuration = 0.4f; // 서랍 이동에 걸리는 시간

        [Header("내용물")] // Inspector 서랍 내용물 설정 구분
        [Tooltip("처음에는 숨겼다가 서랍을 열면 표시할 아이템")] [SerializeField] GameObject[] hiddenContents; // 처음에는 숨겼다가 서랍을 열면 표시할 아이템

        Vector3 closedLocalPosition; // 서랍 몸체의 닫힌 로컬 위치
        bool isOpen; // 현재 서랍이 열려 있는지 저장
        bool isMoving; // 현재 서랍이 이동 중인지 저장
        bool contentsRevealed; // 내용물을 한 번이라도 공개했는지 저장

        void Awake() // 서랍 위치와 내용물 초기화
        {
            if (drawerBody == null) // 서랍 몸체가 연결되지 않았는지 확인
            {
                drawerBody = transform; // 현재 오브젝트 자체를 서랍 몸체로 사용
            }

            closedLocalPosition = drawerBody.localPosition; // 시작 위치를 닫힌 위치로 저장

            if (hiddenContents == null) // 내용물 배열이 생성되지 않았는지 확인
            {
                return; // 내용물 초기화 중단
            }

            foreach (GameObject content in hiddenContents) // 등록된 모든 내용물 순회
            {
                if (content != null) // 내용물 오브젝트가 존재하는지 확인
                {
                    content.SetActive(false); // 게임 시작 시 내용물 숨김
                }
            }
        }

        public string GetPrompt() // 현재 서랍 상태에 맞는 안내 반환
        {
            if (isMoving) // 서랍이 이동 중인지 확인
            {
                return "서랍 작동 중"; // 이동 중 안내 반환
            }

            return isOpen ? "[E] 서랍 닫기" : "[E] 서랍 열기"; // 현재 상태에 맞는 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 서랍과 상호작용
        {
            if (isMoving) // 서랍이 이동 중인지 확인
            {
                return; // 중복 이동 방지
            }

            StartCoroutine(MoveDrawer(!isOpen)); // 현재 상태 반대 방향으로 서랍 이동
        }

        IEnumerator MoveDrawer(bool open) // 지정한 상태로 서랍 몸체 이동
        {
            isMoving = true; // 서랍을 이동 중 상태로 변경

            Vector3 startPosition = drawerBody.localPosition; // 현재 서랍 몸체 위치 저장
            Vector3 targetPosition = open ? closedLocalPosition + openLocalOffset : closedLocalPosition; // 열기 또는 닫기 목표 위치 계산
            float safeDuration = Mathf.Max(0.01f, moveDuration); // 이동시간이 0이 되지 않도록 제한
            float elapsed = 0f; // 현재까지 지난 이동시간 초기화

            while (elapsed < safeDuration) // 이동시간이 끝날 때까지 반복
            {
                elapsed += Time.deltaTime; // 프레임 시간을 이동시간에 누적
                float progress = Mathf.Clamp01(elapsed / safeDuration); // 이동 진행률 계산
                drawerBody.localPosition = Vector3.Lerp(startPosition, targetPosition, progress); // 진행률에 따라 서랍 위치 보간
                yield return null; // 다음 프레임까지 대기
            }

            drawerBody.localPosition = targetPosition; // 서랍을 정확한 최종 위치에 배치
            isOpen = open; // 최종 서랍 상태 저장
            isMoving = false; // 서랍 이동 상태 종료

            if (isOpen && !contentsRevealed) // 처음으로 서랍을 열었는지 확인
            {
                RevealContents(); // 숨겨진 내용물 공개
            }
        }

        void RevealContents() // 등록된 서랍 내용물 활성화
        {
            contentsRevealed = true; // 내용물 공개 완료 상태 저장

            if (hiddenContents == null) // 내용물 배열이 없는지 확인
            {
                return; // 내용물 공개 중단
            }

            foreach (GameObject content in hiddenContents) // 등록된 모든 내용물 순회
            {
                if (content != null) // 내용물이 존재하는지 확인
                {
                    content.SetActive(true); // 숨겨진 아이템 활성화
                }
            }

            Debug.Log("[Drawer] 숨겨진 내용물을 공개했습니다."); // 내용물 공개 결과 출력
        }
    }
}