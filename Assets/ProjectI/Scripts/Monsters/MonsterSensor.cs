using System; // Raycast 결과 정렬 기능 참조
using ProjectI.Combat; // 플레이어 피해 대상과 Damage Pipeline 참조
using UnityEngine; // 물리 감지와 Transform 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class MonsterSensor : MonoBehaviour // 시각·청각·특수 감지를 통합하는 공통 몬스터 감각 컴포넌트
    {
        [SerializeField] private MonsterData data; // 시야·청각 거리 데이터
        [SerializeField] private Transform eyePoint; // 시야 Raycast 시작 위치
        [SerializeField] private GameObject ownerRoot; // 자기 Collider와 소음을 제외할 몬스터 루트
        private PlayerDamageReceiver playerReceiver; // 현재 싱글플레이어 공통 피해 대상 참조
        private Transform visibleTarget; // 현재 직접 시야에 보이는 대상
        private Vector3 lastVisiblePosition; // 마지막 직접 시야 위치
        private Vector3 lastHeardPosition; // 마지막으로 들은 소리 위치
        private float lastHeardTime = -999f; // 마지막 소리 감지 시각
        private string lastHeardLabel = "None"; // 마지막 소리 종류 진단 이름
        private float nextVisionCheckTime; // 다음 시각 Raycast 검사 시각

        public Transform VisibleTarget => visibleTarget; // 현재 직접 보이는 대상 공개
        public bool HasVisibleTarget => visibleTarget != null; // 직접 시야 대상 존재 여부 공개
        public Vector3 LastVisiblePosition => lastVisiblePosition; // 마지막 직접 확인 위치 공개
        public Vector3 LastHeardPosition => lastHeardPosition; // 마지막 소리 위치 공개
        public string LastHeardLabel => lastHeardLabel; // 마지막 소리 이름 공개
        public float LastHeardAge => Time.time - lastHeardTime; // 마지막 소리 이후 경과 시간 공개
        public float LastHeardTime => lastHeardTime; // Brain이 새 소음만 한 번 처리할 수 있도록 마지막 감지 시각 공개
        public bool HasRecentNoise => data != null && Time.time - lastHeardTime <= data.InvestigateDuration; // 아직 조사할 가치가 있는 소리 존재 여부 공개
        public float DistanceToPlayer => playerReceiver == null ? float.PositiveInfinity : Vector3.Distance(transform.position, playerReceiver.transform.position); // 플레이어까지 현재 거리 공개

        private void OnEnable() // 감각 기능 활성화 처리
        {
            MonsterNoiseSystem.NoiseEmitted += HandleNoise; // 공통 소음 이벤트 구독
            ResolvePlayer(); // 현재 플레이어 피해 대상 검색
        }

        private void OnDisable() // 감각 기능 비활성화 처리
        {
            MonsterNoiseSystem.NoiseEmitted -= HandleNoise; // 공통 소음 이벤트 구독 해제
        }

        private void Update() // 설정된 간격으로 시각 감지 갱신
        {
            if (data == null || Time.time < nextVisionCheckTime) // 데이터 누락 또는 시각 검사 대기 여부 확인
            {
                return; // 이번 프레임 시각 검사 생략
            }

            nextVisionCheckTime = Time.time + data.VisionInterval; // 다음 시각 검사 시각 예약
            RefreshVision(); // 현재 플레이어 직접 시야 판정 갱신
        }

        public void Configure(MonsterData targetData, Transform targetEyePoint, GameObject targetOwnerRoot) // Day17 자동 Setup용 감각 참조 구성
        {
            data = targetData; // 몬스터 감지 데이터 저장
            eyePoint = targetEyePoint; // 눈 위치 저장
            ownerRoot = targetOwnerRoot; // 자기 루트 저장
            ResolvePlayer(); // 설정 직후 플레이어 참조 확보
        }

        public bool CanSeeTarget(Transform target) // 지정 대상이 현재 시야 거리·각도·벽 차단을 통과하는지 확인
        {
            if (target == null || data == null) // 대상 또는 데이터 누락 확인
            {
                return false; // 시각 감지 실패 반환
            }

            Vector3 origin = eyePoint == null ? transform.position + (Vector3.up * 1.6f) : eyePoint.position; // 눈 또는 몸체 기준 시야 시작 위치 계산
            Vector3 targetPoint = ResolveTargetPoint(target); // 플레이어 상체 중심 피격 위치 계산
            Vector3 toTarget = targetPoint - origin; // 눈에서 목표까지 방향 계산
            float distance = toTarget.magnitude; // 목표까지 실제 거리 계산

            if (distance <= 0.001f || distance > data.VisionRange) // 너무 가깝거나 시야 최대 거리 밖인지 확인
            {
                return false; // 시각 감지 실패 반환
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized; // 몬스터 수평 전방 방향 계산
            Vector3 flatDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized; // 목표까지 수평 방향 계산
            float angle = Vector3.Angle(flatForward, flatDirection); // 정면과 목표 사이 시야각 계산

            if (angle > data.VisionAngle * 0.5f) // 시야 원뿔 밖인지 확인
            {
                return false; // 각도 초과 감지 실패 반환
            }

            RaycastHit[] hits = Physics.RaycastAll(origin, toTarget.normalized, distance + 0.12f, ~0, QueryTriggerInteraction.Ignore); // 눈과 목표 사이 모든 물리 충돌 조회
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance)); // 가까운 충돌부터 정렬

            foreach (RaycastHit hit in hits) // 시야선의 충돌 결과 순회
            {
                if (hit.collider == null || IsOwnedBySelf(hit.collider.transform)) // 자기 자신 Collider 여부 확인
                {
                    continue; // 자기 몸체 충돌 무시
                }

                PlayerDamageReceiver player = hit.collider.GetComponentInParent<PlayerDamageReceiver>(); // 충돌 대상이 플레이어 계층인지 확인
                return player != null && player.IsAlive; // 첫 유효 충돌이 살아있는 플레이어일 때만 시야 확보 반환
            }

            return false; // 첫 유효 충돌 없이 목표를 찾지 못한 상태 반환
        }

        public bool TrySpecialSense(MonsterBrain brain, out Transform target, out Vector3 sensedPosition) // 향후 웃는 석상·미믹용 특수 감각 확장점 실행
        {
            target = null; // 특수 감지 대상 기본값 초기화
            sensedPosition = Vector3.zero; // 특수 감지 위치 기본값 초기화
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>(); // 같은 몬스터 루트의 감각 확장 컴포넌트 조회

            foreach (MonoBehaviour behaviour in behaviours) // 감각 확장 컴포넌트 순회
            {
                if (behaviour is IMonsterSpecialSense specialSense && specialSense.TrySense(brain, out target, out sensedPosition)) // 특수 감각 구현체가 실제 대상을 감지했는지 확인
                {
                    return true; // 첫 성공 특수 감지 결과 반환
                }
            }

            return false; // 특수 감지 결과 없음 반환
        }

        private void RefreshVision() // 현재 플레이어 시각 감지 갱신
        {
            ResolvePlayer(); // 플레이어 참조 유효성 재확인

            if (playerReceiver == null || !playerReceiver.IsAlive) // 유효한 살아있는 플레이어 여부 확인
            {
                visibleTarget = null; // 현재 시야 대상 제거
                return; // 시각 갱신 종료
            }

            Transform target = playerReceiver.transform; // 플레이어 Transform 조회

            if (CanSeeTarget(target)) // 거리·시야각·벽 차단을 모두 통과했는지 확인
            {
                visibleTarget = target; // 직접 시야 대상 저장
                lastVisiblePosition = target.position; // 마지막 직접 확인 위치 갱신
            }
            else // 현재 플레이어가 직접 보이지 않는 경우 처리
            {
                visibleTarget = null; // 직접 시야 대상 제거
            }
        }

        private void HandleNoise(MonsterNoiseEvent noise) // 공통 소음 이벤트 청각 판정
        {
            if (data == null || noise.Source == null || IsOwnedBySelf(noise.Source.transform)) // 데이터 누락 또는 자기 소음 여부 확인
            {
                return; // 청각 처리 생략
            }

            float effectiveRange = Mathf.Min(data.HearingRange, noise.Radius); // 몬스터 청각과 실제 소음 중 더 짧은 전달 거리 계산
            float distance = Vector3.Distance(transform.position, noise.Position); // 소음 발생 지점까지 거리 계산

            if (distance > effectiveRange) // 소음 전달 거리 밖인지 확인
            {
                return; // 소음 감지 실패 처리
            }

            lastHeardPosition = noise.Position; // 마지막 소음 월드 위치 저장
            lastHeardTime = Time.time; // 마지막 소음 감지 시각 저장
            lastHeardLabel = noise.Label; // F1 진단용 소음 종류 저장
        }

        private void ResolvePlayer() // 현재 씬 플레이어 공통 피해 대상 확보
        {
            if (playerReceiver == null || !playerReceiver.gameObject.activeInHierarchy) // 플레이어 참조 누락·비활성 여부 확인
            {
                playerReceiver = UnityEngine.Object.FindFirstObjectByType<PlayerDamageReceiver>(); // System.Object와 충돌하지 않도록 UnityEngine.Object를 명시해 활성 플레이어 피해 수신기 검색
            }
        }

        private static Vector3 ResolveTargetPoint(Transform target) // 시야 Raycast와 활 조준에 사용할 상체 중심 위치 계산
        {
            CharacterController controller = target.GetComponent<CharacterController>(); // 플레이어 CharacterController 조회

            if (controller != null) // 캐릭터 충돌체 존재 여부 확인
            {
                return target.TransformPoint(controller.center) + (Vector3.up * (controller.height * 0.12f)); // 가슴 부근 목표 위치 반환
            }

            return target.position + (Vector3.up * 1.2f); // 일반 대상 기본 상체 높이 반환
        }

        private bool IsOwnedBySelf(Transform candidate) // 물리 충돌이 현재 몬스터 계층인지 확인
        {
            if (candidate == null) // 유효 Transform 여부 확인
            {
                return false; // 자기 계층 아님 반환
            }

            Transform ownerTransform = ownerRoot == null ? transform : ownerRoot.transform; // 실제 자기 루트 선택
            return candidate == ownerTransform || candidate.IsChildOf(ownerTransform); // 자기 루트 또는 자식 Collider 여부 반환
        }
    }
}
