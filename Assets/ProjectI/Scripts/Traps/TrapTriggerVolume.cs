using System.Collections.Generic; // 동일 행위자 중복 Trigger 방지 집합 참조
using UnityEngine; // Trigger Collider 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class TrapTriggerVolume : MonoBehaviour // 플레이어·몬스터가 통로에 들어오면 연결 함정을 작동시키는 숨은 Trigger
    {
        [SerializeField] private TrapControllerBase targetTrap; // 현재 Trigger가 작동시킬 함정
        [SerializeField] private bool requireExitBeforeRetrigger = true; // 같은 행위자의 연속 작동에 퇴장 요구 여부
        private readonly HashSet<int> insideActors = new HashSet<int>(); // 현재 Trigger 내부 행위자 ID 집합

        public TrapControllerBase TargetTrap => targetTrap; // Validator용 연결 함정 공개

        public void Configure(TrapControllerBase trap) // Editor Setup용 연결 함정 설정
        {
            targetTrap = trap; // Trigger 대상 함정 저장
        }

        private void OnTriggerEnter(Collider other) // 플레이어·몬스터 통로 진입 처리
        {
            if (!TrapActorUtility.TryGetActor(other, out GameObject actor)) // 함정 작동 가능한 행위자인지 확인
            {
                return; // 장식·함정 부품 Collider 제외
            }

            int id = actor.GetInstanceID(); // 동일 행위자 여러 Collider 통합 ID 계산

            if (requireExitBeforeRetrigger && !insideActors.Add(id)) // 이미 Trigger 내부에 있는 행위자인지 확인
            {
                return; // 퇴장 전 반복 Trigger 방지
            }

            if (!requireExitBeforeRetrigger) // 퇴장 요구가 없는 Trigger 여부 확인
            {
                insideActors.Add(id); // 현재 내부 행위자 기록
            }

            targetTrap?.TriggerTrap(actor); // 연결 함정 작동 요청
        }

        private void OnTriggerExit(Collider other) // 행위자 Trigger 퇴장 처리
        {
            if (!TrapActorUtility.TryGetActor(other, out GameObject actor)) // 플레이어·몬스터 여부 확인
            {
                return; // 비행위자 Collider 무시
            }

            insideActors.Remove(actor.GetInstanceID()); // 재진입 시 다시 작동할 수 있도록 내부 기록 제거
        }
    }
}
