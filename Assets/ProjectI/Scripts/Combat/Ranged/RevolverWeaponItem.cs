using ProjectI.Monsters; // 몬스터 청각용 총성 소음 이벤트 참조
using UnityEngine; // Raycast·Transform·탄퍼짐 기능 참조

namespace ProjectI.Combat.Ranged // 원거리 전투 기능 네임스페이스
{
    public sealed class RevolverWeaponItem : RangedWeaponItemBase // 6발 실린더와 연사 탄퍼짐·재장전을 사용하는 리볼버
    {
        [SerializeField] private Transform muzzle; // 총구 발사 기준 위치
        [SerializeField] private Transform cylinderRoot; // 발사·재장전 중 회전하는 실린더 루트
        [SerializeField] private GameObject[] chamberRoundVisuals = new GameObject[6]; // 실린더 안 6발 탄약 시각 요소
        [SerializeField] private GameObject muzzleFlash; // 짧은 총구 화염 시각 요소
        [SerializeField] private int cylinderCapacity = 6; // 리볼버 실린더 최대 장탄 수
        [SerializeField] private int loadedRounds = 6; // 현재 실린더 장탄 수
        [SerializeField] private int reserveRounds = 24; // 시작 예비 탄약 수
        [SerializeField] private float baseDamage = 28f; // 리볼버 한 발 기본 피해량
        [SerializeField] private float staggerPower = 8f; // 리볼버 한 발 경직 힘
        [SerializeField] private float knockbackForce = 0.55f; // 리볼버 한 발 넉백 힘
        [SerializeField] private float maxRange = 75f; // 즉시 탄도 최대 사거리
        [SerializeField] private float fireInterval = 0.16f; // 최소 발사 간격
        [SerializeField] private float reloadTime = 1.65f; // 6발 실린더 재장전 모션 시간
        [SerializeField] private float hipBaseSpread = 0.85f; // 비조준 기본 탄퍼짐 각도
        [SerializeField] private float aimedBaseSpread = 0.18f; // 우클릭 조준 기본 탄퍼짐 각도
        [SerializeField] private float spreadPerRapidShot = 0.75f; // 빠른 연사 한 발당 누적 탄퍼짐
        [SerializeField] private float maxAdditionalSpread = 4.5f; // 연사 누적 최대 추가 탄퍼짐
        [SerializeField] private float spreadRecoveryPerSecond = 3.2f; // 발사를 멈췄을 때 초당 탄퍼짐 회복량
        [SerializeField] private float rapidFireWindow = 0.48f; // 이전 발사와 빠른 연사로 판단할 시간
        [SerializeField] private LayerMask hitMask = ~0; // 리볼버 즉시 탄도 충돌 레이어
        private float additionalSpread; // 현재 연사로 누적된 추가 탄퍼짐
        private float nextFireTime; // 다음 발사 가능 시간
        private float lastShotTime = -999f; // 마지막 발사 시간
        private float muzzleFlashRemaining; // 총구 화염 남은 표시 시간
        private int shotSequence; // Damage Pipeline 공격 식별 번호
        private Quaternion cylinderBaseRotation = Quaternion.identity; // 실린더 기본 로컬 회전
        private Vector3 cylinderBasePosition = Vector3.zero; // 실린더 기본 로컬 위치

        public int CylinderCapacity => cylinderCapacity; // 최대 장탄 수 공개
        public int LoadedRounds => loadedRounds; // 현재 장탄 수 공개
        public int ReserveRounds => reserveRounds; // 현재 예비 탄약 공개
        public float CurrentSpread => (IsAiming ? aimedBaseSpread + (additionalSpread * 0.55f) : hipBaseSpread + additionalSpread); // 현재 실제 발사 탄퍼짐 공개
        public float BaseDamage => baseDamage; // F1·Validator용 피해량 공개
        public float ReloadTime => reloadTime; // F1·Validator용 재장전 시간 공개
        public Transform Muzzle => muzzle; // Validator용 총구 참조 공개
        public Transform CylinderRoot => cylinderRoot; // Validator용 실린더 참조 공개

        protected override void Awake() // 리볼버 초기화
        {
            base.Awake(); // 공통 원거리 무기 초기화

            if (cylinderRoot != null) // 실린더 루트 존재 여부 확인
            {
                cylinderBaseRotation = cylinderRoot.localRotation; // 기본 실린더 회전 저장
                cylinderBasePosition = cylinderRoot.localPosition; // 기본 실린더 위치 저장
            }

            RefreshRoundVisuals(); // 시작 6발 탄약 시각 동기화

            if (muzzleFlash != null) // 총구 화염 오브젝트 존재 여부 확인
            {
                muzzleFlash.SetActive(false); // 시작 시 총구 화염 숨김
            }
        }

        public void ConfigureRevolver(Transform targetMuzzle, Transform targetCylinderRoot, GameObject[] roundVisuals, GameObject targetMuzzleFlash, int capacity, int startingLoaded, int startingReserve, float damage, float stagger, float knockback, float range, float interval, float targetReloadTime, float hipSpread, float aimedSpread, float rapidSpread, float maxSpread, float recovery, float rapidWindow) // Day16 자동 Setup용 리볼버 설정
        {
            muzzle = targetMuzzle; // 총구 Transform 저장
            cylinderRoot = targetCylinderRoot; // 실린더 루트 저장
            chamberRoundVisuals = roundVisuals ?? new GameObject[0]; // 6발 탄약 시각 배열 저장
            muzzleFlash = targetMuzzleFlash; // 총구 화염 시각 저장
            cylinderCapacity = Mathf.Max(1, capacity); // 실린더 최대 장탄 수 최소값 보정
            loadedRounds = Mathf.Clamp(startingLoaded, 0, cylinderCapacity); // 현재 장탄 수 범위 보정
            reserveRounds = Mathf.Max(0, startingReserve); // 예비 탄약 음수 방지
            baseDamage = Mathf.Max(0f, damage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, stagger); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockback); // 넉백 힘 음수 방지
            maxRange = Mathf.Max(1f, range); // 사거리 최소값 보정
            fireInterval = Mathf.Max(0.05f, interval); // 발사 간격 최소값 보정
            reloadTime = Mathf.Max(0.2f, targetReloadTime); // 장전 시간 최소값 보정
            hipBaseSpread = Mathf.Max(0f, hipSpread); // 비조준 탄퍼짐 음수 방지
            aimedBaseSpread = Mathf.Max(0f, aimedSpread); // 조준 탄퍼짐 음수 방지
            spreadPerRapidShot = Mathf.Max(0f, rapidSpread); // 연사 추가 탄퍼짐 음수 방지
            maxAdditionalSpread = Mathf.Max(0f, maxSpread); // 최대 추가 탄퍼짐 음수 방지
            spreadRecoveryPerSecond = Mathf.Max(0f, recovery); // 탄퍼짐 회복 속도 음수 방지
            rapidFireWindow = Mathf.Max(0.05f, rapidWindow); // 빠른 연사 판정 시간 최소값 보정

            if (cylinderRoot != null) // 실린더 루트 존재 여부 확인
            {
                cylinderBaseRotation = cylinderRoot.localRotation; // Setup 생성 기본 회전 저장
                cylinderBasePosition = cylinderRoot.localPosition; // Setup 생성 기본 위치 저장
            }

            RefreshRoundVisuals(); // 설정된 탄약 수를 실린더 시각에 반영

            if (muzzleFlash != null) // 총구 화염 존재 여부 확인
            {
                muzzleFlash.SetActive(false); // Setup 직후 총구 화염 숨김
            }
        }

        protected override bool CanFire() // 현재 리볼버 발사 가능 여부 반환
        {
            return loadedRounds > 0 && Time.time >= nextFireTime && AimCamera != null; // 탄약·발사 간격·카메라 조건을 만족할 때만 발사 허용
        }

        protected override void Fire() // 리볼버 즉시 탄도 한 발 발사
        {
            float timeSinceLastShot = Time.time - lastShotTime; // 이전 발사와 현재 발사 간격 계산
            float shotSpread = CurrentSpread; // 현재 발사에 사용할 탄퍼짐 저장
            Vector3 direction = ApplySpread(AimCamera.transform.forward, shotSpread); // 조준 여부와 연사 누적을 반영한 발사 방향 계산
            Vector3 origin = AimCamera.transform.position; // 카메라 중심을 즉시 탄도 시작점으로 사용
            loadedRounds--; // 실린더 장탄 수 한 발 감소
            nextFireTime = Time.time + fireInterval; // 다음 발사 가능 시각 계산
            lastShotTime = Time.time; // 마지막 발사 시각 저장
            shotSequence++; // 공격 식별 번호 증가

            if (timeSinceLastShot <= rapidFireWindow) // 빠른 연속 발사 여부 확인
            {
                additionalSpread = Mathf.Min(maxAdditionalSpread, additionalSpread + spreadPerRapidShot); // 연사할수록 추가 탄퍼짐 누적
            }
            else // 충분히 쉬었다가 발사한 경우 처리
            {
                additionalSpread = Mathf.Min(maxAdditionalSpread, additionalSpread + (spreadPerRapidShot * 0.25f)); // 첫 발은 작은 반동만 누적
            }

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore)) // 즉시 탄도 첫 충돌 검사
            {
                IDamageable target = DamagePipeline.FindDamageable(hit.collider); // 충돌 Collider에서 공통 피해 대상 검색

                if (target != null) // 피해 가능한 대상 명중 여부 확인
                {
                    DamageInfo damageInfo = new DamageInfo(gameObject, InstigatorObject, CombatFaction.Player, CombatDamageType.Piercing, baseDamage, hit.point, hit.normal, staggerPower, direction * knockbackForce, shotSequence); // 리볼버 관통 피해 요청 생성
                    DamagePipeline.TryApply(damageInfo, target, out _); // 기존 Damage Pipeline에 실제 피해·경직 전달
                }
            }

            MonsterNoiseSystem.Emit(gameObject, muzzle == null ? transform.position : muzzle.position, 30f, 1f, MonsterNoiseKind.Weapon, "Revolver Gunshot"); // 17일차 몬스터가 먼 거리에서도 조사하는 큰 총성 소음 발생
            TriggerMuzzleFlash(); // 짧은 총구 화염 표시
            RotateCylinderAfterShot(); // 한 발 발사 후 실린더 다음 약실로 회전
            RefreshRoundVisuals(); // 남은 6발 탄약 시각 동기화
        }

        protected override bool TryStartReload() // R 입력 리볼버 재장전 시작
        {
            if (loadedRounds >= cylinderCapacity || reserveRounds <= 0) // 실린더가 가득 찼거나 예비 탄약이 없는지 확인
            {
                return false; // 재장전 시작 차단
            }

            return BeginReload(reloadTime); // 실린더 오픈·탄피 배출·재삽입 모션 시작
        }

        protected override void CompleteReload() // 리볼버 재장전 완료 탄약 처리
        {
            int missing = cylinderCapacity - loadedRounds; // 현재 비어 있는 약실 수 계산
            int transfer = Mathf.Min(missing, reserveRounds); // 예비 탄약에서 실제 넣을 수량 계산
            loadedRounds += transfer; // 실린더에 새 탄약 삽입
            reserveRounds -= transfer; // 예비 탄약 실제 소비

            if (cylinderRoot != null) // 실린더 루트 존재 여부 확인
            {
                cylinderRoot.localPosition = cylinderBasePosition; // 실린더 기본 위치 복구
                cylinderRoot.localRotation = cylinderBaseRotation; // 실린더 기본 회전 복구
            }

            RefreshRoundVisuals(); // 6발 장전 결과 시각 갱신
        }

        protected override Vector3 EvaluateReloadPosition(float progress) // 리볼버를 옆으로 돌려 실린더를 보이는 장전 이동 계산
        {
            float arc = Mathf.Sin(progress * Mathf.PI); // 장전 중간 최대 이동값 계산
            return new Vector3(-0.10f * arc, -0.08f * arc, 0.05f * arc); // 총을 화면 중앙 아래·왼쪽으로 이동하는 장전 자세 반환
        }

        protected override Vector3 EvaluateReloadEuler(float progress) // 리볼버 장전 전체 총기 회전 계산
        {
            float arc = Mathf.Sin(progress * Mathf.PI); // 장전 중간 최대 기울기 계산
            return new Vector3(12f * arc, -18f * arc, 52f * arc); // 실린더 측면이 보이도록 총기를 옆으로 회전
        }

        protected override void TickWeaponSpecific() // 탄퍼짐 회복·총구 화염·실린더 장전 모션 갱신
        {
            if (Time.time - lastShotTime > fireInterval) // 실제 발사 직후가 아닌지 확인
            {
                additionalSpread = Mathf.Max(0f, additionalSpread - (spreadRecoveryPerSecond * Time.deltaTime)); // 발사를 쉬면 연사 탄퍼짐 점진 회복
            }

            if (muzzleFlashRemaining > 0f) // 총구 화염 표시 시간이 남았는지 확인
            {
                muzzleFlashRemaining = Mathf.Max(0f, muzzleFlashRemaining - Time.deltaTime); // 총구 화염 남은 시간 감소

                if (muzzleFlashRemaining <= 0f && muzzleFlash != null) // 화염 표시 시간 종료 여부 확인
                {
                    muzzleFlash.SetActive(false); // 총구 화염 숨김
                }
            }

            UpdateCylinderReloadPose(); // 실린더 열림·회전 장전 표현 갱신
        }

        private void UpdateCylinderReloadPose() // 재장전 동안 실린더 옆으로 열고 회전
        {
            if (cylinderRoot == null) // 실린더 루트 누락 확인
            {
                return; // 장전 시각 처리 중단
            }

            if (!IsReloading) // 재장전 상태가 아닌지 확인
            {
                return; // 평상시 발사 회전을 유지
            }

            float progress = ReloadProgress; // 현재 장전 진행률 조회
            float open = Mathf.Sin(progress * Mathf.PI); // 실린더 오픈 정도 계산
            cylinderRoot.localPosition = cylinderBasePosition + new Vector3(-0.18f * open, 0f, 0f); // 실린더를 왼쪽으로 꺼내는 장전 모션 적용
            cylinderRoot.localRotation = cylinderBaseRotation * Quaternion.Euler(0f, 0f, progress * 360f); // 탄피 배출·삽입 느낌의 실린더 한 바퀴 회전 적용
        }

        private void TriggerMuzzleFlash() // 짧은 총구 화염 활성화
        {
            if (muzzleFlash == null) // 화염 시각 누락 확인
            {
                return; // 화염 처리 생략
            }

            muzzleFlash.SetActive(true); // 총구 화염 즉시 표시
            muzzleFlashRemaining = 0.045f; // 약 45ms 표시 시간 설정
        }

        private void RotateCylinderAfterShot() // 발사 후 다음 약실 위치로 실린더 회전
        {
            if (cylinderRoot == null) // 실린더 루트 누락 확인
            {
                return; // 회전 처리 생략
            }

            cylinderBaseRotation *= Quaternion.Euler(0f, 0f, 360f / cylinderCapacity); // 약실 수에 맞춰 기본 회전 한 칸 진행
            cylinderRoot.localRotation = cylinderBaseRotation; // 새 실린더 기본 회전 즉시 적용
        }

        private void RefreshRoundVisuals() // 실린더 탄약 6발 표시 갱신
        {
            if (chamberRoundVisuals == null) // 탄약 시각 배열 누락 확인
            {
                return; // 표시 처리 생략
            }

            for (int index = 0; index < chamberRoundVisuals.Length; index++) // 약실 탄약 시각 전체 순회
            {
                if (chamberRoundVisuals[index] != null) // 현재 탄약 시각 오브젝트 유효성 확인
                {
                    chamberRoundVisuals[index].SetActive(index < loadedRounds); // 현재 장탄 수만큼 황동 탄약 시각 표시
                }
            }
        }

        private void OnValidate() // 리볼버 설정값 안전 범위 보정
        {
            cylinderCapacity = Mathf.Max(1, cylinderCapacity); // 실린더 최소 1발 보장
            loadedRounds = Mathf.Clamp(loadedRounds, 0, cylinderCapacity); // 장탄 수 실린더 용량 범위 보정
            reserveRounds = Mathf.Max(0, reserveRounds); // 예비 탄약 음수 방지
            baseDamage = Mathf.Max(0f, baseDamage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, staggerPower); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockbackForce); // 넉백 힘 음수 방지
            maxRange = Mathf.Max(1f, maxRange); // 사거리 최소값 보정
            fireInterval = Mathf.Max(0.05f, fireInterval); // 발사 간격 최소값 보정
            reloadTime = Mathf.Max(0.2f, reloadTime); // 장전 시간 최소값 보정
            hipBaseSpread = Mathf.Max(0f, hipBaseSpread); // 비조준 탄퍼짐 음수 방지
            aimedBaseSpread = Mathf.Max(0f, aimedBaseSpread); // 조준 탄퍼짐 음수 방지
            spreadPerRapidShot = Mathf.Max(0f, spreadPerRapidShot); // 연사 탄퍼짐 증가량 음수 방지
            maxAdditionalSpread = Mathf.Max(0f, maxAdditionalSpread); // 최대 추가 탄퍼짐 음수 방지
            spreadRecoveryPerSecond = Mathf.Max(0f, spreadRecoveryPerSecond); // 탄퍼짐 회복량 음수 방지
            rapidFireWindow = Mathf.Max(0.05f, rapidFireWindow); // 빠른 연사 시간 최소값 보정
        }
    }
}
