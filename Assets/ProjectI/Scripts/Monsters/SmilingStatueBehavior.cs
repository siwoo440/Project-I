using ProjectI.Combat; // 플레이어 피해 수신기 참조
using UnityEngine; // 카메라·Transform·Raycast 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class SmilingStatueBehavior : MonoBehaviour // 화면 안에 보이면 완전히 정지하고 체력·피격 없이 시야 밖에서만 추적·공격하는 불사 웃는 석상 AI
    {
        [SerializeField] private MonsterData data; // 석상 이동·감지·공격 수치 데이터
        [SerializeField] private MonsterMotor motor; // 관찰되지 않을 때 실제 이동 계층
        [SerializeField] private MonsterMeleeAttack meleeAttack; // 근접 석상 타격 기능
        [SerializeField] private Transform facePoint; // 플레이어 카메라 Raycast가 바라볼 석상 얼굴 위치
        [SerializeField] private Transform smileRoot; // 관찰 상태를 보여줄 미소 시각 루트
        private Transform playerTarget; // 현재 플레이어 Transform
        private Camera playerCamera; // 관찰 판정용 플레이어 카메라
        private bool observed; // 현재 플레이어가 석상을 보고 있는지 여부
        private float nextPlayerLookupTime; // 플레이어 재검색 최소 간격
        private Vector3 smileBaseScale = Vector3.one; // 기본 미소 크기 저장

        public bool IsObserved => observed; // F1 진단용 현재 관찰 여부 공개
        public Transform CurrentTarget => playerTarget; // 진단용 현재 플레이어 대상 공개
        public float DistanceToTarget => playerTarget == null ? float.PositiveInfinity : Vector3.Distance(transform.position, playerTarget.position); // 현재 플레이어 거리 공개
        public MonsterData Data => data; // 진단·Validator용 데이터 공개
        public bool IsInvulnerable => GetComponent<CombatHealth>() == null; // 체력 컴포넌트가 없어 영구 불사·비피격 상태인지 공개
        public MonsterMeleeAttack MeleeAttack => meleeAttack; // 진단용 공격 기능 공개

        private void Awake() // 석상 규칙형 AI 초기화
        {
            ResolveReferences(); // 같은 루트 필수 컴포넌트 자동 확보

            if (smileRoot != null) // 미소 시각 루트 존재 여부 확인
            {
                smileBaseScale = smileRoot.localScale; // 관찰 상태 시각 보정용 기본 크기 저장
            }
        }

        private void OnEnable() // 석상 활성화 시 플레이어 검색 예약
        {
            ResolveReferences(); // 활성화 직후 참조 재확인
            nextPlayerLookupTime = 0f; // 첫 프레임 즉시 플레이어 검색 허용
        }

        private void OnDisable() // 석상 비활성화 시 이동·공격 정리
        {
            motor?.Stop(); // 남아 있는 이동 요청 중지
            meleeAttack?.CancelAttack(); // 진행 중 근접 공격 취소
        }

        private void Update() // 플레이어 관찰 여부에 따른 정지·추적·공격 규칙 처리
        {
            if (data == null || motor == null || meleeAttack == null) // 체력 없이 동작하는 석상 필수 기능 참조 존재 여부 확인
            {
                return; // 규칙형 AI 처리 중단
            }

            ResolvePlayerIfNeeded(); // 플레이어·카메라 참조 확보

            if (playerTarget == null || playerCamera == null) // 플레이어 또는 카메라 조회 실패 여부 확인
            {
                motor.Stop(); // 대상이 없으면 이동 중지
                return; // 다음 프레임 재검색 대기
            }

            float distance = Vector3.ProjectOnPlane(playerTarget.position - transform.position, Vector3.up).magnitude; // 석상과 플레이어 수평 거리 계산

            if (distance > data.VisionRange) // 규칙형 활성 거리 밖인지 확인
            {
                observed = false; // 먼 거리 관찰 상태 해제
                motor.Stop(); // 활성 거리 밖 이동 정지
                meleeAttack.CancelAttack(); // 활성 거리 밖 공격 취소
                RefreshSmileVisual(); // 비활성 미소 상태 갱신
                return; // 행동 처리 종료
            }

            observed = IsObservedByPlayer(); // 카메라 시야각·벽 차단으로 실제 관찰 여부 계산
            RefreshSmileVisual(); // 현재 관찰 여부를 미소 크기에 약하게 반영

            if (observed) // 웃는 석상의 일부가 플레이어 화면 안에 실제로 보이는지 확인
            {
                motor.Stop(); // 화면 안에 보이는 동안 위치 이동 완전 정지
                meleeAttack.CancelAttack(); // 가까운 거리라도 진행 중 공격과 피해 판정을 즉시 취소
                return; // 화면 밖으로 완전히 사라질 때까지 이동·회전·공격 모두 차단
            }

            if (distance > data.AttackRange) // 관찰되지 않고 근접 공격 거리 밖인지 확인
            {
                motor.MoveTo(playerTarget.position, data.ChaseSpeed); // 플레이어 쪽으로 빠르게 추적
                return; // 추적 처리 완료
            }

            motor.Stop(); // 공격 거리 진입 후 위치 고정
            motor.FaceTarget(playerTarget.position); // 공격 전 플레이어 방향 정렬

            if (meleeAttack.CanStartAttack) // 현재 공격 쿨타임 종료 여부 확인
            {
                meleeAttack.TryStartAttack(playerTarget); // 관찰되지 않는 순간 근접 공격 시작
            }
        }

        public void Configure(MonsterData targetData, MonsterMotor targetMotor, MonsterMeleeAttack targetAttack, Transform targetFacePoint, Transform targetSmileRoot) // Day17 자동 Setup용 불사 웃는 석상 구성
        {
            data = targetData; // 석상 데이터 저장
            motor = targetMotor; // 이동 계층 저장
            meleeAttack = targetAttack; // 근접 공격 저장
            facePoint = targetFacePoint; // 얼굴 관찰 목표점 저장
            smileRoot = targetSmileRoot; // 미소 시각 루트 저장

            if (smileRoot != null) // 미소 시각 루트 존재 여부 확인
            {
                smileBaseScale = smileRoot.localScale; // Setup 생성 기본 미소 크기 저장
            }
        }

        private void ResolveReferences() // 같은 루트의 석상 필수 컴포넌트 자동 확보
        {
            motor ??= GetComponent<MonsterMotor>(); // 공통 이동 기능 자동 조회
            meleeAttack ??= GetComponent<MonsterMeleeAttack>(); // 공통 근접 공격 자동 조회
        }

        private void ResolvePlayerIfNeeded() // 플레이어와 카메라 참조를 필요할 때만 재검색
        {
            if (playerTarget != null && playerCamera != null) // 기존 유효 참조 존재 여부 확인
            {
                return; // 재검색 생략
            }

            if (Time.time < nextPlayerLookupTime) // 검색 최소 간격 도달 여부 확인
            {
                return; // 너무 잦은 전역 검색 방지
            }

            nextPlayerLookupTime = Time.time + 0.75f; // 다음 플레이어 검색 가능 시각 설정
            PlayerDamageReceiver receiver = UnityEngine.Object.FindFirstObjectByType<PlayerDamageReceiver>(); // 현재 활성 플레이어 피해 수신기 검색
            playerTarget = receiver == null ? null : receiver.transform; // 플레이어 루트 Transform 저장
            playerCamera = receiver == null ? null : receiver.GetComponentInChildren<Camera>(true); // 플레이어 자식 카메라 조회

            if (playerCamera == null && Camera.main != null) // 플레이어 자식 카메라 검색 실패 여부 확인
            {
                playerCamera = Camera.main; // MainCamera를 관찰 판정 대체 카메라로 사용
            }
        }

        private bool IsObservedByPlayer() // 석상의 일부가 실제 카메라 화면 안에 있고 벽에 가려지지 않았는지 판정
        {
            Vector3 face = facePoint == null ? transform.position + (Vector3.up * 2.05f) : facePoint.position; // 얼굴 기준 화면 판정 지점 계산
            Vector3 torso = transform.position + (Vector3.up * 1.30f); // 몸통 기준 화면 판정 지점 계산
            Vector3 basePoint = transform.position + (Vector3.up * 0.35f); // 하단 기준 화면 판정 지점 계산
            return IsScreenPointVisible(face) || IsScreenPointVisible(torso) || IsScreenPointVisible(basePoint); // 얼굴·몸통·하단 중 하나라도 화면에 보이면 즉시 관찰 상태 반환
        }

        private bool IsScreenPointVisible(Vector3 worldPoint) // 단일 석상 지점의 화면 내부·벽 차단 여부 판정
        {
            Vector3 viewport = playerCamera.WorldToViewportPoint(worldPoint); // 월드 지점을 현재 플레이어 화면 좌표로 변환

            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) // 화면 뒤쪽 또는 Viewport 바깥 여부 확인
            {
                return false; // 화면 내에 들어오지 않은 지점은 관찰 대상에서 제외
            }

            Vector3 delta = worldPoint - playerCamera.transform.position; // 카메라에서 검사 지점까지 방향 계산
            float distance = delta.magnitude; // 실제 가시선 거리 계산

            if (distance < 0.01f) // 카메라와 검사 지점이 거의 같은지 확인
            {
                return true; // 극근거리 화면 내부 상태는 관찰 중으로 처리
            }

            RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, delta.normalized, distance + 0.20f, ~0, QueryTriggerInteraction.Ignore); // 카메라에서 화면 내부 지점까지 전체 가시선 충돌 검사
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance)); // 가까운 충돌부터 벽 차단 여부를 판단하도록 정렬

            for (int index = 0; index < hits.Length; index++) // 가시선 충돌 전체 순회
            {
                Collider collider = hits[index].collider; // 현재 충돌 Collider 조회

                if (collider == null) // 유효 Collider 여부 확인
                {
                    continue; // 누락 충돌 건너뜀
                }

                if (playerTarget != null && (collider.transform == playerTarget || collider.transform.IsChildOf(playerTarget))) // 플레이어 자기 Collider 여부 확인
                {
                    continue; // 카메라 주변 플레이어 몸 충돌 무시
                }

                if (collider.transform == transform || collider.transform.IsChildOf(transform)) // 첫 유효 충돌이 웃는 석상 자신인지 확인
                {
                    return true; // 화면 안에서 직접 보이는 석상으로 판정
                }

                if (hits[index].distance < distance - 0.05f) // 검사 지점보다 앞에서 다른 오브젝트가 막았는지 확인
                {
                    return false; // 벽·장애물 뒤 석상은 화면에 보이지 않는 것으로 처리
                }
            }

            return true; // 가시선에 장애물이 없고 화면 내부라면 석상이 보이는 것으로 처리
        }

        private void RefreshSmileVisual() // 관찰되지 않을 때 미소가 조금 커져 상태를 알아보기 쉽게 표현
        {
            if (smileRoot == null) // 미소 시각 루트 누락 여부 확인
            {
                return; // 시각 갱신 생략
            }

            float multiplier = observed ? 0.92f : 1.12f; // 관찰 중에는 억제되고 시야 밖에서는 커지는 미소 크기 계산
            smileRoot.localScale = Vector3.Lerp(smileRoot.localScale, smileBaseScale * multiplier, 12f * Time.deltaTime); // 상태 변화에 따라 미소 크기 부드럽게 보간
        }
    }
}
