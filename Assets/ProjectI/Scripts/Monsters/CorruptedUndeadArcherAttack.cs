using UnityEngine; // 활·팔 Transform 애니메이션과 투사체 생성 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class CorruptedUndeadArcherAttack : MonoBehaviour // 부패한 망자 궁수의 조준·시위 당김·포물선 화살 발사 기능
    {
        [SerializeField] private MonsterData data; // 공격 시간·피해·화살 속도 데이터
        [SerializeField] private MonsterSensor sensor; // 발사 직전 시야 유지 확인용 감각 참조
        [SerializeField] private Transform muzzle; // 화살 생성 위치
        [SerializeField] private MonsterArrowProjectile arrowTemplate; // 런타임 복제용 비활성 적 화살 템플릿
        [SerializeField] private Transform bowRoot; // 활 전체 조준 회전 시각 루트
        [SerializeField] private Transform stringRoot; // 시위를 뒤로 당기는 시각 루트
        [SerializeField] private Transform leftArm; // 활을 지지하는 왼팔 시각 루트
        [SerializeField] private Transform rightArm; // 시위를 당기는 오른팔 시각 루트
        [SerializeField] private GameObject nockedArrowVisual; // 활에 얹혀 있는 장전 화살 시각 요소
        private Transform target; // 현재 조준 중 플레이어 대상
        private float aimStartedTime; // 현재 조준 시작 시각
        private float nextAttackTime; // 다음 화살 발사 가능 시각
        private float reloadVisualReadyTime; // 발사 후 새 장전 화살 표시 시각
        private int attackSequence; // Damage Pipeline 공격 식별 번호
        private bool aiming; // 현재 시위를 당기며 조준 중인지 여부
        private Vector3 stringBasePosition; // 시위 기본 로컬 위치
        private Quaternion bowBaseRotation = Quaternion.identity; // 활 기본 로컬 회전
        private Quaternion leftArmBaseRotation = Quaternion.identity; // 왼팔 기본 로컬 회전
        private Quaternion rightArmBaseRotation = Quaternion.identity; // 오른팔 기본 로컬 회전

        public bool IsBusy => aiming; // 현재 조준·발사 준비 상태 공개
        public bool CanStartAttack => !aiming && Time.time >= nextAttackTime; // 현재 새 공격 시작 가능 여부 공개
        public float AimProgress => !aiming || data == null ? 0f : Mathf.Clamp01((Time.time - aimStartedTime) / data.AimTime); // 현재 시위 당김 진행률 공개
        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time); // 남은 공격 쿨타임 공개
        public Transform CurrentTarget => target; // 현재 조준 대상 공개
        public float ProjectileSpeed => data == null ? 0f : data.ProjectileSpeed; // 진단용 화살 속도 공개
        public float Damage => data == null ? 0f : data.AttackDamage; // 진단용 화살 피해량 공개

        private void Awake() // 망자 궁수 공격 시각 기본 자세 저장
        {
            CaptureBasePose(); // 활·팔·시위 기본 자세 저장
        }

        private void Update() // 프레임별 조준·발사와 재장전 시각 처리
        {
            UpdateReloadVisual(); // 발사 후 새 화살 준비 시각 처리

            if (!aiming || data == null) // 실제 조준 중인지 확인
            {
                return; // 조준 처리 생략
            }

            if (target == null || sensor == null || !sensor.CanSeeTarget(target)) // 발사 전 대상 소실·벽 차단 여부 확인
            {
                CancelAttack(); // 시야가 끊기면 화살 발사 취소
                return; // 조준 처리 종료
            }

            float progress = AimProgress; // 현재 조준 진행률 조회
            ApplyAimPose(progress); // 시위·팔·활에 조준 자세 적용

            if (progress >= 1f) // 설정된 조준 시간이 완료됐는지 확인
            {
                FireArrow(); // 포물선 화살 실제 발사
            }
        }

        public void Configure(MonsterData targetData, MonsterSensor targetSensor, Transform targetMuzzle, MonsterArrowProjectile targetArrowTemplate, Transform targetBowRoot, Transform targetStringRoot, Transform targetLeftArm, Transform targetRightArm, GameObject targetNockedArrowVisual) // Day17 자동 Setup용 궁수 공격 참조 구성
        {
            data = targetData; // 공격 데이터 저장
            sensor = targetSensor; // 시야 감각 저장
            muzzle = targetMuzzle; // 화살 생성 위치 저장
            arrowTemplate = targetArrowTemplate; // 화살 템플릿 저장
            bowRoot = targetBowRoot; // 활 시각 루트 저장
            stringRoot = targetStringRoot; // 시위 시각 루트 저장
            leftArm = targetLeftArm; // 왼팔 시각 루트 저장
            rightArm = targetRightArm; // 오른팔 시각 루트 저장
            nockedArrowVisual = targetNockedArrowVisual; // 장전 화살 시각 저장
            CaptureBasePose(); // 설정된 생성 자세를 기본값으로 저장
            RefreshNockedArrow(true); // 시작 장전 화살 표시
        }

        public bool TryStartAttack(Transform attackTarget) // Brain에서 원거리 공격 조준 시작 요청
        {
            if (!CanStartAttack || attackTarget == null || data == null || sensor == null) // 쿨타임·대상·필수 참조 확인
            {
                return false; // 공격 시작 실패 반환
            }

            if (!sensor.CanSeeTarget(attackTarget)) // 공격 시작 순간 직접 시야 확보 여부 확인
            {
                return false; // 벽 뒤 대상 공격 차단
            }

            target = attackTarget; // 현재 조준 대상 저장
            aiming = true; // 조준 상태 활성화
            aimStartedTime = Time.time; // 조준 시작 시각 저장
            RefreshNockedArrow(true); // 활 위 장전 화살 표시
            return true; // 공격 시작 성공 반환
        }

        public void CancelAttack() // 경직·시야 상실·사망 시 현재 조준 취소
        {
            aiming = false; // 조준 상태 종료
            target = null; // 현재 조준 대상 제거
            RestoreBasePose(); // 활·팔·시위 기본 자세 복구
        }

        private void FireArrow() // 조준 완료 후 실제 화살 투사체 발사
        {
            if (muzzle == null || arrowTemplate == null || target == null || data == null) // 발사 필수 참조 존재 여부 확인
            {
                CancelAttack(); // 누락 참조 상태에서 공격 정리
                return; // 발사 처리 종료
            }

            Vector3 targetPoint = ResolveTargetPoint(target); // 플레이어 가슴 부근 탄도 목표점 계산
            bool exactArc = MonsterBallistics.TryCalculateLaunchVelocity(muzzle.position, targetPoint, data.ProjectileSpeed, Mathf.Abs(Physics.gravity.y), out Vector3 velocity); // 현재 속도로 낮은 포물선 발사 속도 계산

            if (!exactArc && velocity.sqrMagnitude < 0.01f) // 정확한 탄도도 대체 속도도 만들지 못했는지 확인
            {
                velocity = (targetPoint - muzzle.position).normalized * data.ProjectileSpeed; // 최후 대체 직선 발사 속도 생성
            }

            MonsterArrowProjectile arrow = Object.Instantiate(arrowTemplate, muzzle.position, Quaternion.LookRotation(velocity.normalized, Vector3.up)); // 비활성 템플릿에서 적 화살 복제
            arrow.gameObject.name = "Day17_UndeadArrow"; // 런타임 화살 이름 지정
            arrow.gameObject.SetActive(true); // 복제 화살 활성화
            attackSequence++; // 공격 식별 번호 증가
            arrow.Launch(gameObject, velocity, data.AttackDamage, data.StaggerPower, data.KnockbackForce, attackSequence); // Enemy Damage Pipeline 정보를 포함해 화살 발사
            MonsterNoiseSystem.Emit(gameObject, muzzle.position, 8f, 0.22f, MonsterNoiseKind.Weapon, "Undead Bow Shot"); // 다른 몬스터가 들을 수 있는 작은 활 발사 소음 발생
            aiming = false; // 발사 후 조준 상태 종료
            target = null; // 발사 대상 참조 정리
            nextAttackTime = Time.time + data.AttackCooldown; // 다음 화살 공격 가능 시각 설정
            reloadVisualReadyTime = Time.time + (data.AttackCooldown * 0.48f); // 쿨타임 중간에 새 화살을 꺼내는 시각 시점 계산
            RefreshNockedArrow(false); // 발사 직후 활 위 화살 숨김
            RestoreBasePose(); // 활·팔·시위 기본 자세 복구
        }

        private void ApplyAimPose(float progress) // 조준 진행률에 따라 활과 팔·시위를 당기는 시각 표현
        {
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)); // 자연스러운 시위 당김 보간값 계산

            if (bowRoot != null) // 활 시각 루트 존재 여부 확인
            {
                bowRoot.localRotation = bowBaseRotation * Quaternion.Euler(-5f * pull, 0f, 2f * pull); // 조준 중 활을 약간 세우는 회전 적용
            }

            if (leftArm != null) // 활 지지 팔 존재 여부 확인
            {
                leftArm.localRotation = leftArmBaseRotation * Quaternion.Euler(-12f * pull, 0f, -8f * pull); // 왼팔을 앞으로 고정하는 조준 자세 적용
            }

            if (rightArm != null) // 시위 당김 팔 존재 여부 확인
            {
                rightArm.localRotation = rightArmBaseRotation * Quaternion.Euler(-32f * pull, -14f * pull, 18f * pull); // 오른팔을 뒤로 당기는 조준 자세 적용
            }

            if (stringRoot != null) // 시위 루트 존재 여부 확인
            {
                stringRoot.localPosition = stringBasePosition + new Vector3(0f, 0f, -0.26f * pull); // 시위를 몬스터 몸쪽으로 실제 이동
            }
        }

        private void UpdateReloadVisual() // 공격 쿨타임 중 새 화살 장전 시각 갱신
        {
            if (aiming || nockedArrowVisual == null) // 조준 중이거나 장전 화살 시각 누락 여부 확인
            {
                return; // 재장전 시각 처리 생략
            }

            if (!nockedArrowVisual.activeSelf && Time.time >= reloadVisualReadyTime) // 새 화살 표시 시점 도달 여부 확인
            {
                RefreshNockedArrow(true); // 활 레일 위 새 화살 표시
            }
        }

        private void CaptureBasePose() // 현재 생성된 활·팔·시위 기본 자세 저장
        {
            if (stringRoot != null) // 시위 루트 존재 여부 확인
            {
                stringBasePosition = stringRoot.localPosition; // 시위 기본 위치 저장
            }

            if (bowRoot != null) // 활 루트 존재 여부 확인
            {
                bowBaseRotation = bowRoot.localRotation; // 활 기본 회전 저장
            }

            if (leftArm != null) // 왼팔 존재 여부 확인
            {
                leftArmBaseRotation = leftArm.localRotation; // 왼팔 기본 회전 저장
            }

            if (rightArm != null) // 오른팔 존재 여부 확인
            {
                rightArmBaseRotation = rightArm.localRotation; // 오른팔 기본 회전 저장
            }
        }

        private void RestoreBasePose() // 조준 종료 후 활·팔·시위 기본 자세 복구
        {
            if (stringRoot != null) // 시위 루트 존재 여부 확인
            {
                stringRoot.localPosition = stringBasePosition; // 시위 기본 위치 복구
            }

            if (bowRoot != null) // 활 루트 존재 여부 확인
            {
                bowRoot.localRotation = bowBaseRotation; // 활 기본 회전 복구
            }

            if (leftArm != null) // 왼팔 존재 여부 확인
            {
                leftArm.localRotation = leftArmBaseRotation; // 왼팔 기본 회전 복구
            }

            if (rightArm != null) // 오른팔 존재 여부 확인
            {
                rightArm.localRotation = rightArmBaseRotation; // 오른팔 기본 회전 복구
            }
        }

        private void RefreshNockedArrow(bool visible) // 활 위 장전 화살 표시 상태 변경
        {
            if (nockedArrowVisual != null) // 장전 화살 시각 존재 여부 확인
            {
                nockedArrowVisual.SetActive(visible); // 요청된 장전 화살 표시 상태 적용
            }
        }

        private static Vector3 ResolveTargetPoint(Transform targetTransform) // 화살 탄도에 사용할 플레이어 상체 목표 위치 계산
        {
            CharacterController controller = targetTransform == null ? null : targetTransform.GetComponent<CharacterController>(); // 대상 CharacterController 조회

            if (targetTransform == null) // 대상 Transform 누락 확인
            {
                return Vector3.zero; // 기본 위치 반환
            }

            if (controller != null) // 플레이어 캐릭터 충돌체 존재 여부 확인
            {
                return targetTransform.TransformPoint(controller.center) + (Vector3.up * (controller.height * 0.10f)); // 가슴 부근 목표 위치 반환
            }

            return targetTransform.position + (Vector3.up * 1.1f); // 일반 대상 기본 상체 위치 반환
        }
    }
}
