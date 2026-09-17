using System.Collections; // 이동 연출 Coroutine 사용
using System.Collections.Generic; // 목록 사용
using ProjectI.Interaction; // 상호작용 규약 참조
using ProjectI.Player; // 플레이어 이동 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonElevator : MonoBehaviour, ProjectI.Net.INetworkDevice // 전력 승강기 — 여러 층을 오가며, 전기가 없으면 움직이지 않음
    {
        [SerializeField] private Transform car; // 움직이는 판
        [SerializeField] private float[] stopLocalY = System.Array.Empty<float>(); // 층별 정지 높이 (모듈 로컬)
        [SerializeField] private float speed = 2.2f; // 이동 속도 (m/s)
        [SerializeField] private Vector3 rideCenter = new Vector3(0f, 1.1f, 0f); // 탑승 판정 범위 중심 (판 기준)
        [SerializeField] private Vector3 rideSize = new Vector3(2.7f, 2.2f, 2.7f); // 탑승 판정 범위 크기
        private DungeonPowerZone zone; // 전력 구역
        private int currentStop; // 현재 층 번호
        private bool isMoving; // 이동 중

        public Transform Car => car; // 판 공개
        public int StopCount => stopLocalY.Length; // 정차 층 수 공개
        public int CurrentStop => currentStop; // 현재 층 공개
        public bool IsMoving => isMoving; // 이동 여부 공개
        public bool HasPower => zone == null || zone.HasPower; // 전력 여부 공개 (구역 미연결이면 항상 가능)
        public DungeonPowerZone Zone => zone; // 구역 공개

        public void Configure(Transform carTransform, float[] stops, Vector3 volumeCenter, Vector3 volumeSize) // 구성 (프리팹 제작기에서 호출)
        {
            car = carTransform; // 판
            stopLocalY = stops; // 정지 높이
            rideCenter = volumeCenter; // 탑승 판정 중심
            rideSize = volumeSize; // 탑승 판정 크기
        }

        public void Bind(DungeonPowerZone target) // 전력 구역 연결 (생성기에서 호출)
        {
            zone = target; // 구역
        }

        public float StopLocalY(int index) // 층별 정지 높이
        {
            return index >= 0 && index < stopLocalY.Length ? stopLocalY[index] : 0f; // 반환
        }

        public string FloorLabel(int index) // 버튼에 쓸 층 이름 (아래에서 위로)
        {
            return $"{index + 1}";
        }

        public bool CanMoveTo(int index) // 그 층으로 갈 수 있는지
        {
            return !isMoving && HasPower && index >= 0 && index < stopLocalY.Length && index != currentStop; // 판정
        }

        public void GoToStop(int index) // 지정 층으로 이동
        {
            if (!CanMoveTo(index)) // 불가
            {
                return; // 종료
            }

            requestedStop = index; // 협동: 요청 층 기록
            StartCoroutine(MoveRoutine(index)); // 이동
        }

        private int requestedStop = -1; // 마지막으로 요청된 층 (-1 = 처음 층)

        public int NetworkState => requestedStop < 0 ? currentStop : requestedStop; // 협동 상태 (가는·있는 층)

        public void ApplyNetworkState(int state) // 협동: 같은 층으로 이동
        {
            if (CanMoveTo(state)) // 이동 가능
            {
                GoToStop(state); // 이동
                return; // 종료
            }

            if (!isMoving && state != currentStop) // 전기가 없는 등 이동 불가 → 바로 맞춤
            {
                requestedStop = state; // 기록
                SetStopImmediate(state); // 즉시
            }
        }

        public void SetStopImmediate(int index) // 즉시 층 지정 (검증·초기화용)
        {
            if (index < 0 || index >= stopLocalY.Length || car == null) // 확인
            {
                return; // 종료
            }

            currentStop = index; // 층
            Vector3 local = car.localPosition; // 위치
            local.y = stopLocalY[index]; // 높이
            car.localPosition = local; // 적용
            Physics.SyncTransforms(); // 물리 반영
        }

        private IEnumerator MoveRoutine(int target) // 판을 움직이며 위에 탄 플레이어를 함께 옮김
        {
            isMoving = true; // 이동 중
            float from = car.localPosition.y; // 시작 높이
            float to = stopLocalY[target]; // 목표 높이
            float distance = Mathf.Abs(to - from); // 거리
            float duration = Mathf.Max(0.2f, distance / Mathf.Max(0.2f, speed)); // 소요 시간
            float elapsed = 0f; // 경과
            List<Transform> riders = CollectRiders(); // 판 위 플레이어
            List<PlayerMovement> movements = new List<PlayerMovement>(); // 이동 컴포넌트
            List<CharacterController> controllers = new List<CharacterController>(); // 충돌체

            foreach (Transform rider in riders) // 탑승자 준비 (이동 중에는 충돌체를 꺼서 판에 끼지 않게 함)
            {
                PlayerMovement movement = rider.GetComponentInChildren<PlayerMovement>(); // 이동
                CharacterController controller = rider.GetComponentInChildren<CharacterController>(); // 충돌체
                movements.Add(movement); // 등록
                controllers.Add(controller); // 등록

                if (movement != null) // 확인
                {
                    movement.enabled = false; // 정지 (시점은 자유)
                }

                if (controller != null) // 확인
                {
                    controller.enabled = false; // 해제
                }
            }

            while (elapsed < duration) // 이동
            {
                elapsed += Time.deltaTime; // 시간 누적
                float previous = car.localPosition.y; // 직전 높이
                Vector3 local = car.localPosition; // 위치
                local.y = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)); // 보간
                car.localPosition = local; // 적용
                float delta = car.localPosition.y - previous; // 이동량

                foreach (Transform rider in riders) // 탑승자도 같은 만큼
                {
                    if (rider != null) // 확인
                    {
                        rider.position += Vector3.up * delta; // 함께 이동
                    }
                }

                yield return null; // 다음 프레임
            }

            Vector3 finalLocal = car.localPosition; // 마무리
            finalLocal.y = to; // 목표
            car.localPosition = finalLocal; // 적용

            for (int index = 0; index < riders.Count; index++) // 탑승자 복구
            {
                if (controllers[index] != null) // 충돌체
                {
                    controllers[index].enabled = true; // 복구
                }

                if (movements[index] != null) // 이동 컴포넌트
                {
                    movements[index].enabled = true; // 복구
                    movements[index].NotifyTeleported(); // 추락 판정 초기화
                }
            }

            Physics.SyncTransforms(); // 물리 반영
            currentStop = target; // 층 기록
            isMoving = false; // 종료
        }

        private List<Transform> CollectRiders() // 판 위에 서 있는 플레이어 찾기
        {
            List<Transform> riders = new List<Transform>(); // 결과

            if (car == null) // 판 없음
            {
                return riders; // 빈 목록
            }

            Bounds bounds = new Bounds(car.TransformPoint(rideCenter), rideSize); // 월드 범위 (충돌체 없이 계산 — 시선 상호작용을 가리지 않게)

            foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) // 플레이어 순회
            {
                Transform root = movement.transform.root; // 루트

                if (bounds.Contains(movement.transform.position) || bounds.Contains(root.position)) // 판 위
                {
                    riders.Add(root); // 등록
                }
            }

            return riders; // 반환
        }
    }
}
