using System.Collections.Generic; // 압력판 위 행위자 집합 관리 참조
using UnityEngine; // Trigger·Transform 보간 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class PressurePlate : MonoBehaviour // Player·Monster가 밟으면 연결된 여러 함정을 동시에 작동시키는 압력판
    {
        [SerializeField] private Transform plateVisual; // 실제 눌려 보이는 상판 Transform
        [SerializeField] private Vector3 releasedLocalPosition; // 아무도 밟지 않은 기본 상판 위치
        [SerializeField] private Vector3 pressedLocalPosition; // 행위자가 밟은 눌림 위치
        [SerializeField] private TrapControllerBase[] linkedTraps; // 압력판이 작동시킬 함정 목록
        [SerializeField] private float moveSpeed = 10f; // 상판 눌림·복귀 보간 속도
        private readonly HashSet<int> occupants = new HashSet<int>(); // 현재 압력판 위 행위자 ID 집합
        private bool pressed; // 현재 압력판 눌림 상태

        public bool IsPressed => pressed; // F1 진단용 현재 눌림 여부 공개
        public int OccupantCount => occupants.Count; // 현재 압력판 위 행위자 수 공개
        public TrapControllerBase[] LinkedTraps => linkedTraps; // Validator·F1용 연결 함정 공개

        private void Update() // 압력판 상판 눌림·복귀 시각 보간
        {
            if (plateVisual == null) // 상판 시각 Transform 존재 여부 확인
            {
                return; // 시각 보간 생략
            }

            Vector3 target = pressed ? pressedLocalPosition : releasedLocalPosition; // 현재 눌림 상태에 맞는 목표 위치 선택
            plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, target, moveSpeed * Time.deltaTime); // 상판을 목표 위치로 부드럽게 이동
        }

        public void Configure(Transform targetPlateVisual, Vector3 releasedPosition, Vector3 pressedPosition, TrapControllerBase[] traps) // Editor Setup용 압력판 구성
        {
            plateVisual = targetPlateVisual; // 눌림 상판 Transform 저장
            releasedLocalPosition = releasedPosition; // 기본 위치 저장
            pressedLocalPosition = pressedPosition; // 눌림 위치 저장
            linkedTraps = traps; // 연결 함정 목록 저장

            if (plateVisual != null) // 상판 존재 여부 확인
            {
                plateVisual.localPosition = releasedLocalPosition; // Edit Mode 기본 높이 정렬
            }
        }

        private void OnTriggerEnter(Collider other) // Player·Monster 압력판 진입 처리
        {
            if (!TrapActorUtility.TryGetActor(other, out GameObject actor)) // 함정 작동 가능한 행위자인지 확인
            {
                return; // 비행위자 Collider 제외
            }

            bool wasEmpty = occupants.Count == 0; // 이번 진입 전 아무도 밟지 않았는지 저장
            occupants.Add(actor.GetInstanceID()); // 현재 행위자를 압력판 점유 목록에 추가

            if (wasEmpty && occupants.Count > 0) // 압력판이 처음 눌리는 순간인지 확인
            {
                pressed = true; // 눌림 상태 활성화
                TriggerLinkedTraps(actor); // 연결된 모든 함정에 작동 요청
            }
        }

        private void OnTriggerExit(Collider other) // Player·Monster 압력판 이탈 처리
        {
            if (!TrapActorUtility.TryGetActor(other, out GameObject actor)) // 유효 행위자 여부 확인
            {
                return; // 비행위자 Collider 제외
            }

            occupants.Remove(actor.GetInstanceID()); // 현재 행위자를 점유 목록에서 제거

            if (occupants.Count == 0) // 압력판 위에 아무도 남지 않았는지 확인
            {
                pressed = false; // 상판 복귀 상태 설정
            }
        }

        private void TriggerLinkedTraps(GameObject actor) // 연결 함정 전체 작동 요청
        {
            if (linkedTraps == null) // 연결 함정 배열 누락 여부 확인
            {
                return; // 작동 대상 없음
            }

            for (int index = 0; index < linkedTraps.Length; index++) // 연결 함정 전체 순회
            {
                TrapControllerBase trap = linkedTraps[index]; // 현재 연결 함정 조회
                trap?.TriggerTrap(actor); // 현재 함정 상태가 허용하면 작동 요청
            }
        }
    }
}
