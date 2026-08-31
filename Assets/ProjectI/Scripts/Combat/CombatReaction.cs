using System; // 상태 변경 이벤트와 배열 정렬 기능 참조
using UnityEngine; // 유니티 물리 이동과 수치 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public sealed class CombatReaction : MonoBehaviour, ICombatReactionReceiver // 몬스터·더미가 공유하는 경직 누적과 넉백 반응
    {
        [SerializeField] private float staggerThreshold = 30f; // 경직 발동에 필요한 누적 수치
        [SerializeField] private float staggerRecoveryPerSecond = 12f; // 피격이 없을 때 초당 경직 회복량
        [SerializeField] private float staggerDuration = 0.35f; // 경직 발동 유지 시간
        [SerializeField, Range(0f, 0.95f)] private float knockbackResistance = 0.10f; // 넉백 거리 감소 비율
        [SerializeField] private float knockbackMoveSpeed = 12f; // 넉백 이동 처리 속도
        [SerializeField] private float collisionRadius = 0.42f; // 넉백 벽 충돌 검사 반경
        [SerializeField] private float staggerVisualAngle = 8f; // 테스트 더미 경직 시 좌우 기울기 각도
        private float currentStagger; // 현재 누적 경직 수치
        private float staggerRemaining; // 현재 남은 경직 시간
        private Vector3 knockbackDirection; // 현재 넉백 이동 방향
        private float knockbackDistanceRemaining; // 현재 남은 넉백 이동 거리
        private float lastKnockbackDistance; // 마지막 요청 넉백 거리
        private GameObject lastSource; // 마지막 반응을 발생시킨 피해 원인
        private GameObject lastObstacle; // 마지막 넉백 이동을 막은 물리 장애물
        private Quaternion baseLocalRotation = Quaternion.identity; // 경직 시각 표현 복구용 기본 회전
        private bool rotationCaptured; // 기본 회전 저장 여부
        private bool visualWasStaggered; // 직전 프레임 경직 시각 적용 여부

        public event Action StateChanged; // 경직·넉백 상태 변경 이벤트

        public Transform ReactionTransform => transform; // 대표 반응 Transform 공개
        public bool IsStaggered => staggerRemaining > 0f; // 현재 경직 유지 여부 공개
        public float CurrentStagger => currentStagger; // 현재 누적 경직 수치 공개
        public float StaggerThreshold => staggerThreshold; // 현재 경직 한계 수치 공개
        public float KnockbackResistance => knockbackResistance; // 현재 넉백 저항 비율 공개
        public float StaggerRemaining => staggerRemaining; // 남은 경직 시간 공개
        public float KnockbackDistanceRemaining => knockbackDistanceRemaining; // 남은 넉백 거리 공개
        public float LastKnockbackDistance => lastKnockbackDistance; // 마지막 적용 요청 넉백 거리 공개
        public GameObject LastSource => lastSource; // 마지막 반응 원인 공개
        public GameObject LastObstacle => lastObstacle; // 마지막 넉백 차단 장애물 공개

        private void Awake() // 반응 대상 초기 기준 자세 저장
        {
            CaptureBaseRotation(); // 현재 로컬 회전을 경직 시각 복구 기준으로 저장
        }

        private void OnEnable() // 반응 기능 활성화 처리
        {
            CaptureBaseRotation(); // 활성화 시 최신 기본 자세 재확인
        }

        private void OnDisable() // 반응 기능 비활성화 처리
        {
            RestoreBaseRotation(); // 비활성화 시 경직 기울기 제거
        }

        private void Update() // 프레임별 경직 회복과 넉백 이동 처리
        {
            float deltaTime = Time.deltaTime; // 현재 프레임 시간 저장
            bool changed = false; // 이번 프레임 상태 변경 여부 저장

            if (staggerRemaining > 0f) // 현재 경직 유지 상태 확인
            {
                float previousRemaining = staggerRemaining; // 이전 경직 잔여 시간 저장
                staggerRemaining = Mathf.Max(0f, staggerRemaining - deltaTime); // 경직 잔여 시간 감소
                changed |= !Mathf.Approximately(previousRemaining, staggerRemaining); // 경직 시간 변화 기록
            }
            else if (currentStagger > 0f) // 경직 중이 아니면서 누적 수치가 남았는지 확인
            {
                float previousStagger = currentStagger; // 이전 경직 누적값 저장
                currentStagger = Mathf.Max(0f, currentStagger - (staggerRecoveryPerSecond * deltaTime)); // 시간에 따른 경직 누적 회복
                changed |= !Mathf.Approximately(previousStagger, currentStagger); // 경직 누적 변화 기록
            }

            if (knockbackDistanceRemaining > 0f) // 남은 넉백 이동 거리 존재 여부 확인
            {
                changed |= TickKnockback(deltaTime); // 벽 충돌을 고려한 넉백 이동 처리
            }

            UpdateStaggerVisual(); // 경직 상태를 테스트 더미 기울기로 시각 표현

            if (changed) // 외부 진단에 전달할 상태 변화 여부 확인
            {
                StateChanged?.Invoke(); // 경직·넉백 상태 변경 이벤트 발생
            }
        }

        public void Configure(float threshold, float recoveryPerSecond, float duration, float resistance, float moveSpeed, float radius) // Day15 자동 Setup용 반응 수치 구성
        {
            staggerThreshold = Mathf.Max(1f, threshold); // 경직 한계 최소값 보정
            staggerRecoveryPerSecond = Mathf.Max(0f, recoveryPerSecond); // 경직 회복량 음수 방지
            staggerDuration = Mathf.Max(0.05f, duration); // 경직 지속 시간 최소값 보정
            knockbackResistance = Mathf.Clamp(resistance, 0f, 0.95f); // 넉백 저항 범위 보정
            knockbackMoveSpeed = Mathf.Max(0.1f, moveSpeed); // 넉백 이동 속도 최소값 보정
            collisionRadius = Mathf.Clamp(radius, 0.05f, 1.5f); // 벽 검사 반경 범위 보정
        }

        public void ReceiveReaction(DamageInfo damageInfo) // 실제 피해 적용 후 경직·넉백 반응 수신
        {
            lastSource = damageInfo.Source; // 마지막 피해 원인 저장
            lastObstacle = null; // 새 반응에서 이전 벽 충돌 기록 초기화

            if (damageInfo.StaggerPower > 0f) // 경직 힘이 포함된 피해인지 확인
            {
                currentStagger = Mathf.Min(staggerThreshold, currentStagger + damageInfo.StaggerPower); // 경직 수치를 한계까지 누적

                if (currentStagger >= staggerThreshold) // 경직 발동 한계 도달 여부 확인
                {
                    currentStagger = 0f; // 경직 발동 후 누적 수치 초기화
                    baseLocalRotation = transform.localRotation; // 경직 시작 직전 현재 바라보는 회전을 시각 기준으로 저장
                    rotationCaptured = true; // 최신 경직 기준 회전 저장 완료 기록
                    visualWasStaggered = true; // 경직 시각 효과 활성 상태 기록
                    staggerRemaining = staggerDuration; // 설정된 시간 동안 경직 상태 활성화
                }
            }

            Vector3 horizontalForce = Vector3.ProjectOnPlane(damageInfo.Force, Vector3.up); // 수직 성분을 제외한 넉백 힘 계산
            float requestedDistance = horizontalForce.magnitude * (1f - knockbackResistance); // 저항을 반영한 실제 넉백 거리 계산
            lastKnockbackDistance = requestedDistance; // F1 진단용 마지막 넉백 거리 저장

            if (requestedDistance > 0.001f) // 실제 넉백 이동이 필요한지 확인
            {
                knockbackDirection = horizontalForce.normalized; // 넉백 이동 방향 저장
                knockbackDistanceRemaining = Mathf.Max(knockbackDistanceRemaining, requestedDistance); // 기존 이동보다 강한 넉백 거리 유지
            }

            StateChanged?.Invoke(); // 즉시 변경된 반응 상태 외부 전달
        }

        public void ResetReaction() // 테스트용 경직·넉백 상태 초기화
        {
            currentStagger = 0f; // 누적 경직 수치 초기화
            staggerRemaining = 0f; // 경직 유지 시간 초기화
            knockbackDirection = Vector3.zero; // 넉백 방향 초기화
            knockbackDistanceRemaining = 0f; // 남은 넉백 거리 초기화
            lastKnockbackDistance = 0f; // 마지막 넉백 거리 초기화
            lastSource = null; // 마지막 피해 원인 초기화
            lastObstacle = null; // 마지막 차단 장애물 초기화
            visualWasStaggered = false; // 경직 시각 적용 상태 초기화
            RestoreBaseRotation(); // 경직 시각 기울기 기본 자세로 복구
            StateChanged?.Invoke(); // 초기화 상태 외부 전달
        }

        private bool TickKnockback(float deltaTime) // 벽 충돌을 고려한 한 프레임 넉백 이동 처리
        {
            if (knockbackDirection.sqrMagnitude <= 0.0001f) // 유효 넉백 방향 존재 여부 확인
            {
                knockbackDistanceRemaining = 0f; // 잘못된 방향의 잔여 거리 제거
                return true; // 상태 변화 발생 반환
            }

            float requestedStep = Mathf.Min(knockbackDistanceRemaining, knockbackMoveSpeed * deltaTime); // 이번 프레임 이동할 넉백 거리 계산
            Vector3 origin = transform.position + (Vector3.up * 1.05f); // 바닥을 피한 벽 충돌 검사 시작점 계산
            RaycastHit[] hits = Physics.SphereCastAll(origin, collisionRadius, knockbackDirection, requestedStep, ~0, QueryTriggerInteraction.Ignore); // 넉백 진행 방향의 물리 장애물 전체 검사
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance)); // 가까운 장애물부터 정렬
            float allowedStep = requestedStep; // 기본 이동 허용 거리를 요청 거리로 설정

            foreach (RaycastHit hit in hits) // 넉백 방향 충돌 결과 순회
            {
                if (hit.collider == null || IsOwnedBySelf(hit.collider.transform)) // 자기 자신 Collider 여부 확인
                {
                    continue; // 자기 Collider 충돌 무시
                }

                allowedStep = Mathf.Max(0f, hit.distance - 0.03f); // 벽 바로 앞까지만 이동하도록 거리 제한
                lastObstacle = hit.collider.gameObject; // 마지막 넉백 차단 장애물 기록
                knockbackDistanceRemaining = 0f; // 벽 충돌 이후 남은 넉백 이동 종료
                break; // 가장 가까운 유효 장애물에서 검사 종료
            }

            transform.position += knockbackDirection * allowedStep; // 허용된 거리만큼 실제 대상 위치 이동

            if (knockbackDistanceRemaining > 0f) // 벽으로 즉시 종료되지 않은 이동인지 확인
            {
                knockbackDistanceRemaining = Mathf.Max(0f, knockbackDistanceRemaining - allowedStep); // 실제 이동한 거리만큼 잔여 넉백 감소
            }

            return allowedStep > 0f || knockbackDistanceRemaining <= 0f; // 실제 이동 또는 종료 상태 변화 여부 반환
        }


        private void CaptureBaseRotation() // 현재 기본 로컬 회전 저장
        {
            if (rotationCaptured && Application.isPlaying) // 런타임에서 이미 기준 자세가 저장됐는지 확인
            {
                return; // AI 등 외부 회전과 충돌하지 않도록 중복 저장 생략
            }

            baseLocalRotation = transform.localRotation; // 현재 로컬 회전을 기본 자세로 저장
            rotationCaptured = true; // 기준 자세 저장 완료 기록
        }

        private void UpdateStaggerVisual() // 경직 상태를 간단한 좌우 흔들림으로 표현
        {
            if (!rotationCaptured) // 기본 회전 누락 여부 확인
            {
                CaptureBaseRotation(); // 누락 기준 자세 저장
            }

            if (!IsStaggered) // 현재 경직 상태가 아닌지 확인
            {
                if (visualWasStaggered) // 직전까지 경직 기울기가 적용됐는지 확인
                {
                    RestoreBaseRotation(); // 경직 종료 순간에만 기본 자세 복구
                    visualWasStaggered = false; // 시각 경직 적용 종료 기록
                }

                return; // 평상시 외부 AI 회전을 건드리지 않고 처리 종료
            }

            visualWasStaggered = true; // 현재 경직 시각 효과 활성 상태 기록
            float normalized = staggerDuration <= 0f ? 0f : Mathf.Clamp01(staggerRemaining / staggerDuration); // 남은 경직 시간 비율 계산
            float pulse = Mathf.Sin((1f - normalized) * Mathf.PI * 4f); // 짧은 좌우 흔들림 파형 계산
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, pulse * staggerVisualAngle); // 경직 시작 자세 기준 좌우 기울기 적용
        }

        private void RestoreBaseRotation() // 경직 시각 표현 종료 후 기본 자세 복구
        {
            if (rotationCaptured) // 기본 회전 저장 여부 확인
            {
                transform.localRotation = baseLocalRotation; // 저장된 기본 로컬 회전 복구
            }
        }

        private bool IsOwnedBySelf(Transform target) // Collider가 현재 반응 대상 계층에 속하는지 확인
        {
            return target != null && (target == transform || target.IsChildOf(transform)); // 자기 자신과 모든 자식 Collider 여부 반환
        }

        private void OnValidate() // 인스펙터 반응 수치 검증
        {
            staggerThreshold = Mathf.Max(1f, staggerThreshold); // 경직 한계 최소값 보정
            staggerRecoveryPerSecond = Mathf.Max(0f, staggerRecoveryPerSecond); // 경직 회복량 음수 방지
            staggerDuration = Mathf.Max(0.05f, staggerDuration); // 경직 지속 시간 최소값 보정
            knockbackResistance = Mathf.Clamp(knockbackResistance, 0f, 0.95f); // 넉백 저항 범위 보정
            knockbackMoveSpeed = Mathf.Max(0.1f, knockbackMoveSpeed); // 넉백 이동 속도 최소값 보정
            collisionRadius = Mathf.Clamp(collisionRadius, 0.05f, 1.5f); // 벽 검사 반경 범위 보정
            staggerVisualAngle = Mathf.Clamp(staggerVisualAngle, 0f, 20f); // 경직 시각 기울기 각도 범위 보정
        }
    }
}
