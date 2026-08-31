using UnityEngine; // MonoBehaviour·GameObject 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public abstract class TrapControllerBase : MonoBehaviour // 가시·도끼가 공유하는 상태·진단 기반 클래스
    {
        [SerializeField] private string displayName = "Trap"; // F1 진단용 함정 표시 이름
        [SerializeField] protected TrapDamageSource damageSource; // 현재 함정 피해 판정 볼륨
        [SerializeField] protected TrapState state = TrapState.Ready; // 현재 함정 동작 상태
        private GameObject lastTriggerSource; // 마지막으로 함정을 작동시킨 행위자
        private int activationSequence; // 동일 함정 공격 식별용 누적 작동 번호

        public string DisplayName => displayName; // F1 진단용 표시 이름 공개
        public TrapState State => state; // 현재 상태 공개
        public TrapDamageSource DamageSource => damageSource; // Validator·진단용 피해 소스 공개
        public GameObject LastTriggerSource => lastTriggerSource; // 마지막 작동 주체 공개
        public int ActivationSequence => activationSequence; // 현재 누적 작동 횟수 공개
        public virtual bool CanTrigger => state == TrapState.Ready || state == TrapState.Waiting; // 외부 Trigger 수용 가능 상태 공개

        public abstract bool TriggerTrap(GameObject triggerSource = null); // 함정 종류별 실제 작동 요청

        protected void ConfigureBase(string targetName, TrapDamageSource targetDamageSource) // Editor Setup용 공통 참조 구성
        {
            displayName = string.IsNullOrWhiteSpace(targetName) ? "Trap" : targetName; // 표시 이름 저장
            damageSource = targetDamageSource; // 피해 소스 연결
        }

        protected int BeginActivation(GameObject triggerSource) // 새 작동 주기 시작과 고유 공격 번호 생성
        {
            lastTriggerSource = triggerSource; // 마지막 작동 주체 저장
            activationSequence++; // 작동 번호 증가
            return (GetInstanceID() * 397) ^ activationSequence; // 현재 함정 인스턴스와 순서를 조합한 공격 ID 반환
        }
    }
}
