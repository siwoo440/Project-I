using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public enum PoweredIronDoorState // 전동 철제문의 현재 이동 상태
    {
        Closed, // 완전 닫힘 상태
        Opening, // 열리는 중 상태
        Open, // 완전 열림 상태
        Closing // 닫히는 중 상태
    }

    public sealed class PoweredIronDoor : MonoBehaviour, IPowerStateReceiver // 전력을 사용해 움직이는 철제문 제어
    {
        [SerializeField] private string displayName = "철제문"; // 배전반과 로컬 버튼에 표시할 문 이름
        [SerializeField] private PowerConsumer powerConsumer; // 방 전력을 받는 공통 전력 소비자
        [SerializeField] private Transform movingPanel; // 실제로 이동하는 철제문 패널
        [SerializeField] private Vector3 closedLocalPosition; // 완전 닫힘 로컬 위치
        [SerializeField] private Vector3 openLocalPosition; // 완전 열림 로컬 위치
        [SerializeField] private float moveSpeed = 2.4f; // 철제문 초당 이동 속도
        [SerializeField] private PoweredIronDoorState state = PoweredIronDoorState.Closed; // 현재 문 상태
        [SerializeField] private GameObject[] localOpenVisuals; // 문 옆 패널의 열림 표시 요소
        [SerializeField] private GameObject[] localClosedVisuals; // 문 옆 패널의 닫힘 표시 요소
        [SerializeField] private GameObject[] localMovingVisuals; // 문 옆 패널의 이동 중 표시 요소
        [SerializeField] private GameObject[] localNoPowerVisuals; // 문 옆 패널의 정전 표시 요소

        public string DisplayName => displayName; // 철제문 표시 이름 공개
        public bool HasPower => powerConsumer != null && powerConsumer.HasPower; // 현재 문에 실제 전력이 공급되는지 공개
        public PoweredIronDoorState State => state; // 현재 문 상태 공개
        public bool IsOpen => state == PoweredIronDoorState.Open; // 완전 열림 여부 공개
        public bool IsClosed => state == PoweredIronDoorState.Closed; // 완전 닫힘 여부 공개
        public bool IsMoving => state == PoweredIronDoorState.Opening || state == PoweredIronDoorState.Closing; // 현재 이동 중 여부 공개
        public PowerConsumer PowerConsumer => powerConsumer; // Validator용 전력 소비자 참조 공개

        private void Awake() // 전동 철제문 초기화
        {
            ClampSettings(); // 이동 속도 안전 범위 보정
            SnapToStoredState(); // 저장된 문 상태에 맞는 위치 적용
            UpdateStatusVisuals(); // 초기 상태등 갱신
        }

        private void OnEnable() // 전동 철제문 활성화 처리
        {
            UpdateStatusVisuals(); // 활성화 직후 현재 상태등 동기화
        }

        private void Update() // 프레임별 철제문 이동 처리
        {
            if (!HasPower || movingPanel == null) // 전력 또는 이동 패널 존재 여부 확인
            {
                UpdateStatusVisuals(); // 정전 상태등 유지
                return; // 전력이 없으면 현재 위치에서 이동 중지
            }

            if (state == PoweredIronDoorState.Opening) // 열리는 중인지 확인
            {
                MovePanelTowards(openLocalPosition, PoweredIronDoorState.Open); // 완전 열림 위치로 이동
            }
            else if (state == PoweredIronDoorState.Closing) // 닫히는 중인지 확인
            {
                MovePanelTowards(closedLocalPosition, PoweredIronDoorState.Closed); // 완전 닫힘 위치로 이동
            }

            UpdateStatusVisuals(); // 현재 문 상태를 로컬 표시등에 반영
        }

        public void Configure(string targetDisplayName, PowerConsumer consumer, Transform panel, Vector3 closedPosition, Vector3 openPosition, float targetMoveSpeed, PoweredIronDoorState startState, GameObject[] openVisuals, GameObject[] closedVisuals, GameObject[] movingVisuals, GameObject[] noPowerVisuals) // 자동 Setup용 전동 철제문 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 문 표시 이름 저장
            powerConsumer = consumer; // 공통 전력 소비자 연결
            movingPanel = panel; // 이동 패널 연결
            closedLocalPosition = closedPosition; // 닫힘 위치 저장
            openLocalPosition = openPosition; // 열림 위치 저장
            moveSpeed = Mathf.Max(0.1f, targetMoveSpeed); // 이동 속도 최소값 보정
            state = startState; // 시작 문 상태 저장
            localOpenVisuals = openVisuals; // 로컬 열림 표시 연결
            localClosedVisuals = closedVisuals; // 로컬 닫힘 표시 연결
            localMovingVisuals = movingVisuals; // 로컬 이동 표시 연결
            localNoPowerVisuals = noPowerVisuals; // 로컬 정전 표시 연결
            SnapToStoredState(); // 시작 상태에 맞는 문 위치 적용
            UpdateStatusVisuals(); // 시작 상태등 적용
        }

        public bool RequestOpen() // 배전반 또는 로컬 버튼에서 문 열기 요청
        {
            if (!HasPower) // 문 전력 공급 여부 확인
            {
                UpdateStatusVisuals(); // 정전 상태등 갱신
                return false; // 전력이 없으면 개방 실패 반환
            }

            if (state == PoweredIronDoorState.Open || state == PoweredIronDoorState.Opening) // 이미 열렸거나 열리는 중인지 확인
            {
                return true; // 중복 요청을 성공 상태로 처리
            }

            state = PoweredIronDoorState.Opening; // 문을 열리는 중 상태로 변경
            UpdateStatusVisuals(); // 이동 상태등 즉시 반영
            return true; // 개방 요청 성공 반환
        }

        public bool RequestClose() // 배전반 또는 로컬 버튼에서 문 닫기 요청
        {
            if (!HasPower) // 문 전력 공급 여부 확인
            {
                UpdateStatusVisuals(); // 정전 상태등 갱신
                return false; // 전력이 없으면 폐쇄 실패 반환
            }

            if (state == PoweredIronDoorState.Closed || state == PoweredIronDoorState.Closing) // 이미 닫혔거나 닫히는 중인지 확인
            {
                return true; // 중복 요청을 성공 상태로 처리
            }

            state = PoweredIronDoorState.Closing; // 문을 닫히는 중 상태로 변경
            UpdateStatusVisuals(); // 이동 상태등 즉시 반영
            return true; // 폐쇄 요청 성공 반환
        }

        public void OnPowerStateChanged(bool hasPower) // 공통 전력 소비자의 상태 변경 수신
        {
            UpdateStatusVisuals(); // 통전 또는 정전 상태를 로컬 표시등에 즉시 반영
        }

        private void MovePanelTowards(Vector3 targetPosition, PoweredIronDoorState completedState) // 이동 패널을 목표 로컬 위치로 이동
        {
            movingPanel.localPosition = Vector3.MoveTowards(movingPanel.localPosition, targetPosition, moveSpeed * Time.deltaTime); // 현재 위치에서 목표 위치로 일정 속도 이동

            if ((movingPanel.localPosition - targetPosition).sqrMagnitude > 0.0001f) // 아직 목표 위치에 도달하지 않았는지 확인
            {
                return; // 다음 프레임까지 이동 계속
            }

            movingPanel.localPosition = targetPosition; // 완전한 목표 위치로 스냅
            state = completedState; // 열림 또는 닫힘 완료 상태 저장
        }

        private void SnapToStoredState() // 저장된 상태 기준 초기 위치 적용
        {
            if (movingPanel == null) // 이동 패널 존재 여부 확인
            {
                return; // 패널이 없으면 위치 적용 생략
            }

            if (state == PoweredIronDoorState.Open) // 완전 열림 시작 상태인지 확인
            {
                movingPanel.localPosition = openLocalPosition; // 열린 위치로 즉시 이동
            }
            else if (state == PoweredIronDoorState.Closed) // 완전 닫힘 시작 상태인지 확인
            {
                movingPanel.localPosition = closedLocalPosition; // 닫힌 위치로 즉시 이동
            }
        }

        private void UpdateStatusVisuals() // 문 옆 상태등을 현재 상태에 맞춰 갱신
        {
            bool powered = HasPower; // 현재 문 통전 여부 계산
            SetVisualArrayState(localOpenVisuals, powered && state == PoweredIronDoorState.Open); // 전력 있음 + 열림 상태 표시
            SetVisualArrayState(localClosedVisuals, powered && state == PoweredIronDoorState.Closed); // 전력 있음 + 닫힘 상태 표시
            SetVisualArrayState(localMovingVisuals, powered && IsMoving); // 전력 있음 + 이동 상태 표시
            SetVisualArrayState(localNoPowerVisuals, !powered); // 정전 상태 표시
        }

        private static void SetVisualArrayState(GameObject[] visuals, bool activeState) // 상태 시각 요소 배열 활성화 처리
        {
            if (visuals == null) // 시각 요소 배열 존재 여부 확인
            {
                return; // 배열이 없으면 처리 생략
            }

            foreach (GameObject visual in visuals) // 시각 요소 전체 순회
            {
                if (visual == null) // 유효 시각 요소 여부 확인
                {
                    continue; // 누락 요소 건너뜀
                }

                visual.SetActive(activeState); // 현재 문 상태에 맞춰 표시 전환
            }
        }

        private void ClampSettings() // 전동 철제문 수치 안전 범위 보정
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed); // 이동 속도 최소값 보정
        }

        private void OnValidate() // 인스펙터 변경 시 상태 보정
        {
            ClampSettings(); // 에디터 수치 안전 범위 보정
            UpdateStatusVisuals(); // 에디터 상태를 표시등에 반영
        }
    }
}
