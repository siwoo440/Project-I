using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // 새 입력 시스템 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    /// <summary>
    /// 플레이어의 맨손 공격과 무기별 전투를 처리.
    /// 검과 도끼는 근접 SphereCast, 활은 화살 소모형 Raycast, 방패는 우클릭 피해 감소 방식.
    /// 최종 피해량은 치명타 적용 후 몬스터 방어력을 차감하며 최소 피해량은 1.
    /// </summary>
    public class PlayerCombat : MonoBehaviour // 플레이어 전투 컴포넌트
    {
        [Header("맨손 기본 공격")] // 맨손 공격 설정 구분
        [Tooltip("맨손 기본 공격력")] [SerializeField] float unarmedDamage = 12f; // 맨손 기본 공격력
        [Tooltip("맨손 공격 대기시간")] [SerializeField] float unarmedCooldown = 0.6f; // 맨손 공격 대기시간
        [Tooltip("맨손 공격 사거리")] [SerializeField] float unarmedRange = 2f; // 맨손 공격 사거리

        [Header("공통")] // 공통 전투 설정 구분
        [Tooltip("근접 공격 판정 반경")] [SerializeField] float hitRadius = 0.4f; // 근접 공격 판정 반경
        [Tooltip("활 기본 대체 사거리")] [SerializeField] float bowFallbackRange = 20f; // 활 기본 대체 사거리

        Transform cam; // 공격 방향용 카메라 Transform
        InventorySystem inventory; // 화살 소모용 인벤토리
        float lastAttack = -999f; // 마지막 공격 시각

        public bool IsBlocking { get; private set; } // 현재 방패 방어 여부
        public float CurrentBlockReduction { get; private set; } // 현재 피해 감소율
        public event System.Action<WeaponType?> AttackPerformed; // 맨손 또는 무기 공격 실행 사실 전달
        public event System.Action AttackHit; // 공격이 몬스터에게 명중한 사실 전달

        void Awake() // 전투에 필요한 참조 초기화
        {
            Camera cameraComponent = GetComponentInChildren<Camera>(); // 자식 카메라 검색
            cam = cameraComponent != null ? cameraComponent.transform : transform; // 공격 기준 Transform 결정
            inventory = GetComponent<InventorySystem>(); // 인벤토리 컴포넌트 가져오기
        }

        void Update() // 매 프레임 공격과 방어 입력 처리
        {
            Mouse mouse = Mouse.current; // 현재 마우스 입력 가져오기

            if (mouse == null || Cursor.lockState != CursorLockMode.Locked) // 마우스 또는 커서 상태 확인
            {
                IsBlocking = false; // 방패 방어 상태 해제
                CurrentBlockReduction = 0f; // 피해 감소율 초기화
                return; // 전투 입력 처리 중단
            }

            WeaponData weapon = GetHeldWeapon(); // 현재 손에 든 무기 확인
            bool block = weapon != null && weapon.type == WeaponType.Shield && mouse.rightButton.isPressed; // 방패 우클릭 확인

            IsBlocking = block; // 방패 방어 상태 저장
            CurrentBlockReduction = block ? weapon.blockReduction : 0f; // 방패 피해 감소율 적용

            float cooldown = weapon != null ? weapon.attackCooldown : unarmedCooldown; // 현재 공격 대기시간 결정

            if (mouse.leftButton.wasPressedThisFrame && Time.time - lastAttack >= cooldown) // 좌클릭과 공격 대기시간 확인
            {
                Attack(weapon); // 현재 무기로 공격 실행
            }
        }

        void OnDisable() // 컴포넌트 비활성화 시 전투 상태 초기화
        {
            IsBlocking = false; // 방패 방어 상태 해제
            CurrentBlockReduction = 0f; // 피해 감소율 초기화
        }

        WeaponData GetHeldWeapon() // 현재 손에 든 무기 데이터 반환
        {
            PickupItem heldItem = cam != null ? cam.GetComponentInChildren<PickupItem>(false) : null; // 카메라 자식의 활성 아이템 검색
            return heldItem != null ? heldItem.Weapon : null; // 무기 데이터 또는 null 반환
        }

        void Attack(WeaponData weapon) // 무기 종류에 따른 공격 실행
        {
            lastAttack = Time.time; // 마지막 공격 시각 갱신

            if (weapon != null && weapon.type == WeaponType.Bow) // 활 사용 여부 확인
            {
                FireBow(weapon); // 활 원거리 공격 실행
                return; // 근접 공격 처리 방지
            }

            if (weapon != null && weapon.type == WeaponType.Shield) // 방패 사용 여부 확인
            {
                Debug.Log("[전투] 방패로는 공격 불가"); // 방패 공격 불가 안내
                return; // 공격 처리 중단
            }

            WeaponType? attackType = weapon != null ? weapon.type : (WeaponType?)null; // 현재 무기 종류 또는 맨손 상태 결정
            AttackPerformed?.Invoke(attackType); // 근접 공격 실행 사실을 오디오 피드백에 전달


            float damage = weapon != null ? weapon.attackDamage : unarmedDamage; // 현재 공격력 결정
            float range = weapon != null ? weapon.attackRange : unarmedRange; // 현재 공격 사거리 결정
            float criticalChance = weapon != null ? weapon.critChance : 0.1f; // 현재 치명타 확률 결정
            float criticalMultiplier = weapon != null ? weapon.critMultiplier : 1.5f; // 현재 치명타 배율 결정
            string attackName = weapon != null ? weapon.displayName : "맨손"; // 공격 표시 이름 결정
            Vector3 origin = cam.position + cam.forward * 0.6f; // 근접 판정 시작 위치 계산
            float castDistance = Mathf.Max(0.1f, range - 0.6f); // 실제 근접 판정 거리 계산

            if (Physics.SphereCast(origin, hitRadius, cam.forward, out RaycastHit hit, castDistance, ~0, QueryTriggerInteraction.Ignore)) // 전방 근접 충돌 검사
            {
                MonsterAI monster = hit.collider.GetComponentInParent<MonsterAI>(); // 충돌 대상의 몬스터 검색

                if (monster != null) // 몬스터 명중 여부 확인
                {
                    DealDamage(monster, damage, criticalChance, criticalMultiplier, attackName); // 몬스터 피해 적용
                    return; // 빗나감 로그 방지
                }
            }

            Debug.Log($"[전투] {attackName} 빗나감"); // 근접 공격 실패 출력
        }

        void FireBow(WeaponData weapon) // 활 원거리 공격 실행
        {
            if (inventory == null || !inventory.ConsumeItemByName("화살")) // 인벤토리와 화살 소지 여부 확인
            {
                Debug.Log("[전투] 화살이 없습니다"); // 화살 부족 안내
                return; // 활 공격 처리 중단
            }
            AttackPerformed?.Invoke(WeaponType.Bow); // 실제 화살을 소모한 활 발사 사실 전달

            float range = weapon.attackRange > 5f ? weapon.attackRange : bowFallbackRange; // 활 공격 사거리 결정
            Vector3 origin = cam.position + cam.forward * 0.6f; // 원거리 판정 시작 위치 계산

            if (Physics.Raycast(origin, cam.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore)) // 전방 원거리 충돌 검사
            {
                MonsterAI monster = hit.collider.GetComponentInParent<MonsterAI>(); // 충돌 대상의 몬스터 검색

                if (monster != null) // 몬스터 명중 여부 확인
                {
                    DealDamage(monster, weapon.attackDamage, weapon.critChance, weapon.critMultiplier, weapon.displayName); // 활 피해 적용
                    return; // 빗나감 로그 방지
                }
            }

            Debug.Log($"[전투] {weapon.displayName} 화살 빗나감"); // 원거리 공격 실패 출력
        }

        void DealDamage(MonsterAI monster, float damage, float criticalChance, float criticalMultiplier, string attackName) // 최종 피해 계산과 적용
        {
            bool isCritical = Random.value < criticalChance; // 치명타 발생 여부 결정
            float rawDamage = damage * (isCritical ? criticalMultiplier : 1f); // 치명타 포함 공격력 계산
            float finalDamage = Mathf.Max(1f, rawDamage - monster.Defense); // 방어력 차감과 최소 피해 적용

            monster.TakeDamage(finalDamage); // 몬스터 체력 감소
            AttackPerformed?.Invoke(WeaponType.Bow); // 실제 화살을 소모한 활 발사 사실 전달
            Debug.Log($"[전투] {attackName} {finalDamage:F0} 피해{(isCritical ? " (치명타!)" : "")}"); // 공격 결과 출력
        }
    }
}