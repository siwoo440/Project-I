using ProjectI.Items; // 기존 빠른 슬롯 기능 참조
using UnityEngine; // 투사체 생성과 Transform 기능 참조

namespace ProjectI.Combat.Ranged // 원거리 전투 기능 네임스페이스
{
    public sealed class CrossbowWeaponItem : RangedWeaponItemBase // 단발 볼트와 확대 조준·재장전을 사용하는 석궁
    {
        [SerializeField] private Transform muzzle; // 볼트 생성 발사 위치
        [SerializeField] private CrossbowBoltProjectile boltTemplate; // 런타임 복제용 비활성 볼트 템플릿
        [SerializeField] private GameObject loadedBoltVisual; // 석궁 레일 위 장전 볼트 시각 요소
        [SerializeField] private Transform stringRoot; // 장전 시 당겨지는 시위 시각 루트
        [SerializeField] private float projectileSpeed = 38f; // 석궁 볼트 초기 발사 속도
        [SerializeField] private float baseDamage = 55f; // 석궁 기본 관통 피해량
        [SerializeField] private float staggerPower = 28f; // 석궁 피격 경직 힘
        [SerializeField] private float knockbackForce = 1.0f; // 석궁 피격 넉백 힘
        [SerializeField] private float reloadTime = 1.45f; // 석궁 단발 재장전 시간
        [SerializeField] private int reserveBolts = 12; // 시작 예비 볼트 수량
        [SerializeField] private bool loaded = true; // 현재 레일에 볼트 장전 여부
        private Vector3 stringBasePosition; // 시위 기본 로컬 위치
        private int shotSequence; // Damage Pipeline 공격 식별 번호
        private float lastShotTime = -999f; // F1 진단용 마지막 발사 시간

        public bool Loaded => loaded; // 장전 볼트 존재 여부 공개
        public int ReserveBolts => reserveBolts; // 예비 볼트 수량 공개
        public float ProjectileSpeed => projectileSpeed; // F1·Validator용 볼트 속도 공개
        public float BaseDamage => baseDamage; // F1·Validator용 기본 피해 공개
        public float ReloadTime => reloadTime; // F1·Validator용 재장전 시간 공개
        public float LastShotTime => lastShotTime; // 마지막 발사 시각 공개
        public Transform Muzzle => muzzle; // Validator용 발사 위치 공개
        public CrossbowBoltProjectile BoltTemplate => boltTemplate; // Validator용 볼트 템플릿 공개

        protected override void Awake() // 석궁 초기화
        {
            base.Awake(); // 공통 원거리 무기 초기화

            if (stringRoot != null) // 시위 시각 루트 존재 여부 확인
            {
                stringBasePosition = stringRoot.localPosition; // 장전 모션용 원래 시위 위치 저장
            }

            RefreshLoadedVisual(); // 시작 탄약 상태를 볼트 시각에 반영
        }

        public void ConfigureCrossbow(Transform targetMuzzle, CrossbowBoltProjectile targetBoltTemplate, GameObject targetLoadedBoltVisual, Transform targetStringRoot, float speed, float damage, float stagger, float knockback, float targetReloadTime, int startingReserveBolts, bool startsLoaded) // Day16 자동 Setup용 석궁 설정
        {
            muzzle = targetMuzzle; // 볼트 발사 위치 저장
            boltTemplate = targetBoltTemplate; // 볼트 템플릿 저장
            loadedBoltVisual = targetLoadedBoltVisual; // 레일 장전 볼트 시각 저장
            stringRoot = targetStringRoot; // 시위 시각 루트 저장
            projectileSpeed = Mathf.Max(5f, speed); // 볼트 초기 속도 최소값 보정
            baseDamage = Mathf.Max(0f, damage); // 기본 피해량 음수 방지
            staggerPower = Mathf.Max(0f, stagger); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockback); // 넉백 힘 음수 방지
            reloadTime = Mathf.Max(0.2f, targetReloadTime); // 재장전 시간 최소값 보정
            reserveBolts = Mathf.Max(0, startingReserveBolts); // 예비 볼트 음수 방지
            loaded = startsLoaded; // 시작 장전 상태 저장

            if (stringRoot != null) // 시위 시각 루트 존재 여부 확인
            {
                stringBasePosition = stringRoot.localPosition; // 현재 시위 기본 위치 저장
            }

            RefreshLoadedVisual(); // 설정 직후 볼트 시각 갱신
        }

        public void AddReserveBolts(int amount) // 박힌 볼트 F 회수 시 예비 탄약 증가
        {
            if (amount <= 0) // 유효 회수 수량 확인
            {
                return; // 증가 처리 중단
            }

            reserveBolts += amount; // 회수된 볼트를 예비 수량에 추가
        }

        protected override bool CanFire() // 현재 석궁 발사 가능 여부 반환
        {
            return loaded && muzzle != null && boltTemplate != null && AimCamera != null; // 장전·발사 위치·볼트·카메라가 모두 있을 때만 발사 허용
        }

        protected override void Fire() // 석궁 볼트 한 발 발사
        {
            Vector3 direction = AimCamera.transform.forward.normalized; // 카메라 정중앙 시선 방향을 볼트 초기 방향으로 사용
            CrossbowBoltProjectile bolt = Object.Instantiate(boltTemplate, muzzle.position, Quaternion.LookRotation(direction)); // 비활성 템플릿에서 새 볼트 투사체 복제
            bolt.gameObject.name = "Day16_FiredCrossbowBolt"; // 런타임 발사 볼트 이름 지정
            bolt.gameObject.SetActive(true); // 복제된 볼트 활성화
            shotSequence++; // 석궁 공격 식별 번호 증가
            bolt.Launch(gameObject, InstigatorObject, direction, projectileSpeed, baseDamage, staggerPower, knockbackForce, shotSequence); // 중력 적용 포물선 비행과 Damage Pipeline 정보 전달
            loaded = false; // 발사 후 레일 장전 상태 비움
            lastShotTime = Time.time; // 마지막 발사 시각 기록
            RefreshLoadedVisual(); // 레일의 장전 볼트 시각 숨김
        }

        protected override bool TryStartReload() // R 입력 석궁 재장전 시작
        {
            if (loaded || reserveBolts <= 0) // 이미 장전됐거나 예비 볼트가 없는지 확인
            {
                return false; // 재장전 시작 차단
            }

            return BeginReload(reloadTime); // 석궁 장전 모션 타이머 시작
        }

        protected override void CompleteReload() // 석궁 장전 완료 탄약 처리
        {
            if (!loaded && reserveBolts > 0) // 실제 장전 가능한 상태 재확인
            {
                reserveBolts--; // 예비 볼트 한 발 소비
                loaded = true; // 레일에 새 볼트 장전
            }

            if (stringRoot != null) // 시위 시각 루트 존재 여부 확인
            {
                stringRoot.localPosition = stringBasePosition; // 장전 완료 후 시위 기본 위치 복구
            }

            RefreshLoadedVisual(); // 장전된 볼트 시각 표시
        }

        protected override Vector3 EvaluateReloadPosition(float progress) // 석궁 아래로 내렸다가 복귀하는 장전 이동 계산
        {
            float arc = Mathf.Sin(progress * Mathf.PI); // 0→1→0 형태의 장전 중간 강조값 계산
            return new Vector3(0f, -0.18f * arc, 0.08f * arc); // 석궁을 아래·약간 뒤로 이동하는 장전 자세 반환
        }

        protected override Vector3 EvaluateReloadEuler(float progress) // 석궁 장전 중 기울기 계산
        {
            float arc = Mathf.Sin(progress * Mathf.PI); // 장전 중간 최대 기울기 계산
            return new Vector3(28f * arc, 0f, 8f * arc); // 레일을 아래로 보이게 기울이는 단순 장전 회전 반환
        }

        protected override void TickWeaponSpecific() // 석궁 시위 당김 시각 갱신
        {
            if (stringRoot == null) // 시위 시각 루트 누락 확인
            {
                return; // 시위 처리 생략
            }

            if (!IsReloading) // 장전 중이 아닌지 확인
            {
                stringRoot.localPosition = Vector3.Lerp(stringRoot.localPosition, stringBasePosition, 12f * Time.deltaTime); // 시위 기본 위치로 부드럽게 복귀
                return; // 추가 당김 처리 생략
            }

            float pull = Mathf.Sin(ReloadProgress * Mathf.PI); // 장전 중 시위가 뒤로 당겨졌다 돌아오는 값 계산
            stringRoot.localPosition = stringBasePosition + new Vector3(0f, 0f, -0.22f * pull); // 시위를 레일 뒤쪽으로 당기는 단순 장전 표현 적용
        }

        private void RefreshLoadedVisual() // 레일 위 볼트 표시 상태 동기화
        {
            if (loadedBoltVisual != null) // 장전 볼트 시각 존재 여부 확인
            {
                loadedBoltVisual.SetActive(loaded); // 실제 장전 여부와 시각 요소 활성 상태 일치
            }
        }

        private void OnValidate() // 석궁 설정값 안전 범위 보정
        {
            projectileSpeed = Mathf.Max(5f, projectileSpeed); // 볼트 발사 속도 최소값 보정
            baseDamage = Mathf.Max(0f, baseDamage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, staggerPower); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockbackForce); // 넉백 힘 음수 방지
            reloadTime = Mathf.Max(0.2f, reloadTime); // 장전 시간 최소값 보정
            reserveBolts = Mathf.Max(0, reserveBolts); // 예비 볼트 음수 방지
        }
    }
}
