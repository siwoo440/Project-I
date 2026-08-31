using ProjectI.Items; // 기존 빠른 슬롯·월드 아이템 사용 체계 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    [RequireComponent(typeof(WorldItem))] // 기존 F 획득·빠른 슬롯 연동용 WorldItem 필수 지정
    [RequireComponent(typeof(MeleeWeaponTrace))] // 근접 공격 궤적 기능 필수 지정
    public sealed class MeleeWeaponItem : MonoBehaviour, IUsableItem // 좌클릭 한 번으로 단일 근접 공격을 요청하는 무기
    {
        [SerializeField] private AttackDefinition attackDefinition; // 현재 무기의 단일 공격 데이터
        [SerializeField] private MeleeWeaponTrace weaponTrace; // 실제 무기날 이동 궤적 판정
        [SerializeField] private Transform visualPivot; // 단일 휘두르기 시각 피벗
        private WorldItem worldItem; // 기존 인벤토리·운반 상태 확인용 월드 아이템
        private Quaternion baseVisualRotation = Quaternion.identity; // 공격 전 무기 시각 기준 회전
        private bool baseRotationCaptured; // 런타임 기준 회전 저장 여부

        public AttackDefinition AttackDefinition => attackDefinition; // CombatController와 Validator용 공격 데이터 공개
        public MeleeWeaponTrace WeaponTrace => weaponTrace; // CombatController와 Validator용 궤적 공개
        public WorldItem WorldItem => worldItem; // 기존 아이템 참조 공개
        public bool IsHeld => worldItem != null && worldItem.IsHeld; // 현재 실제 손에 든 상태 공개
        public Transform VisualPivot => visualPivot; // Validator용 시각 피벗 공개

        private void Awake() // 근접 무기 초기화
        {
            ResolveReferences(); // WorldItem과 궤적 참조 확보
            CaptureBaseVisualRotation(true); // 런타임 시작 기준 회전 저장
        }

        private void OnEnable() // 근접 무기 활성화 처리
        {
            ResolveReferences(); // 활성화 직후 참조 재확인
            CaptureBaseVisualRotation(!Application.isPlaying); // Edit Mode에서는 최신 기본 자세 다시 저장
        }

        public void Configure(AttackDefinition definition, MeleeWeaponTrace trace, Transform pivot) // Day14~15 자동 Setup용 단일 공격 구성
        {
            attackDefinition = definition; // 공격 데이터 연결
            weaponTrace = trace; // 무기 궤적 연결
            visualPivot = pivot; // 휘두르기 시각 피벗 연결
            ResolveReferences(); // 기존 아이템 참조 확보
            CaptureBaseVisualRotation(true); // 새 시각 피벗 기준 회전 저장
        }

        public bool CanUse(PlayerInventory inventory) // 빠른 슬롯 좌클릭 사용 가능 여부 확인
        {
            ResolveReferences(); // 사용 시점 필수 참조 확보

            if (inventory == null || worldItem == null || inventory.SelectedItem != worldItem || !worldItem.IsHeld) // 현재 선택·운반된 실제 무기인지 확인
            {
                return false; // 선택되지 않았거나 들지 않은 무기 사용 차단
            }

            CombatController controller = inventory.GetComponent<CombatController>(); // 플레이어 공통 전투 제어기 조회
            return controller != null; // 쿨타임·스태미나 실패 사유는 CombatController에서 일관되게 처리
        }

        public void Use(PlayerInventory inventory) // 좌클릭으로 현재 무기의 단일 공격 시작 요청
        {
            CombatController controller = inventory == null ? null : inventory.GetComponent<CombatController>(); // 플레이어 공통 전투 제어기 조회

            if (controller != null) // 유효 전투 제어기 존재 여부 확인
            {
                controller.TryStartAttack(this); // 콤보 없이 한 번의 공격 시작 요청
            }
        }

        public void BeginAttack(int attackId, Transform instigatorRoot, Transform attackOrigin) // CombatController 단일 공격 시작 처리
        {
            CaptureBaseVisualRotation(false); // 공격 전 시각 기준 회전 보장
            weaponTrace?.EndTrace(); // 이전 비정상 궤적 상태 정리
        }

        public void BeginActiveTrace(int attackId, Transform instigatorRoot, Transform attackOrigin) // 공격 Active 단계 진입 처리
        {
            weaponTrace?.BeginTrace(attackId, instigatorRoot, attackOrigin); // 새 공격 식별값으로 궤적 활성화
        }

        public void TickActiveTrace(CombatController controller) // 공격 Active 단계 매 프레임 궤적 처리
        {
            if (attackDefinition != null) // 공격 데이터 연결 여부 확인
            {
                weaponTrace?.TickTrace(controller, attackDefinition); // 설정 반경과 피해량으로 근접 궤적 검사
            }
        }

        public void EndActiveTrace() // 공격 Active 단계 종료 처리
        {
            weaponTrace?.EndTrace(); // 근접 궤적 검사 비활성화
        }

        public void UpdateAttackPose(AttackPhase phase, float normalizedProgress) // 무기별 데이터에 따른 단일 휘두르기 시각 처리
        {
            if (visualPivot == null || attackDefinition == null) // 시각 피벗과 공격 데이터 존재 여부 확인
            {
                return; // 시각 애니메이션 생략
            }

            float t = Mathf.Clamp01(normalizedProgress); // 현재 공격 단계 진행률 0~1 보정
            Quaternion windupRotation = baseVisualRotation * Quaternion.Euler(attackDefinition.WindupEuler); // 무기별 준비 자세 목표 회전 계산
            Quaternion strikeRotation = baseVisualRotation * Quaternion.Euler(attackDefinition.StrikeEuler); // 무기별 타격 자세 목표 회전 계산

            if (phase == AttackPhase.Windup) // 준비 단계 여부 확인
            {
                visualPivot.localRotation = Quaternion.Slerp(baseVisualRotation, windupRotation, t); // 기본 자세에서 준비 자세로 회전
            }
            else if (phase == AttackPhase.Active) // 실제 타격 단계 여부 확인
            {
                visualPivot.localRotation = Quaternion.Slerp(windupRotation, strikeRotation, t); // 준비 자세에서 타격 자세로 한 번 휘두르기
            }
            else if (phase == AttackPhase.Recovery) // 공격 회복 단계 여부 확인
            {
                visualPivot.localRotation = Quaternion.Slerp(strikeRotation, baseVisualRotation, t); // 타격 자세에서 기본 자세로 복귀
            }
        }

        public void EndAttack() // 전체 공격 종료 또는 취소 처리
        {
            weaponTrace?.EndTrace(); // 남은 궤적 검사 비활성화

            if (visualPivot != null) // 시각 피벗 존재 여부 확인
            {
                visualPivot.localRotation = baseVisualRotation; // 공격 종료 후 기본 무기 자세 복구
            }
        }

        private void ResolveReferences() // 같은 무기 오브젝트의 필수 참조 확보
        {
            if (worldItem == null) // WorldItem 참조 누락 확인
            {
                worldItem = GetComponent<WorldItem>(); // 기존 아이템 기능 조회
            }

            if (weaponTrace == null) // 궤적 참조 누락 확인
            {
                weaponTrace = GetComponent<MeleeWeaponTrace>(); // 같은 오브젝트의 궤적 기능 조회
            }
        }

        private void CaptureBaseVisualRotation(bool force) // 시각 피벗의 기본 회전 저장
        {
            if (visualPivot == null) // 시각 피벗 누락 여부 확인
            {
                return; // 기준 회전 저장 중단
            }

            if (force || !baseRotationCaptured) // 강제 갱신 또는 첫 저장 여부 확인
            {
                baseVisualRotation = visualPivot.localRotation; // 현재 시각 피벗 로컬 회전을 기준 자세로 저장
                baseRotationCaptured = true; // 기준 자세 저장 완료 기록
            }
        }
    }
}
