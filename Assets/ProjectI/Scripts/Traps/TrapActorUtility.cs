using ProjectI.Combat; // 공통 피해 대상 판정 참조
using ProjectI.Monsters; // 무체력 웃는 석상 몬스터 판정 참조
using UnityEngine; // Collider·GameObject 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public static class TrapActorUtility // 압력판·숨은 Trigger가 플레이어와 몬스터를 구분하는 공통 도우미
    {
        public static bool TryGetActor(Collider collider, out GameObject actor) // Collider가 함정을 작동시킬 수 있는 행위자인지 판정
        {
            actor = null; // 기본 결과를 대상 없음으로 초기화

            if (collider == null) // Collider 유효성 확인
            {
                return false; // 대상 없음 반환
            }

            IDamageable damageable = DamagePipeline.FindDamageable(collider); // 플레이어·일반 몬스터 공통 피해 대상 검색

            if (damageable != null && damageable.DamageTransform != null) // 피해 가능한 플레이어·몬스터 여부 확인
            {
                actor = damageable.DamageTransform.root.gameObject; // 동일 행위자의 여러 Collider를 하나의 루트로 통합
                return true; // 함정 작동 대상 반환
            }

            SmilingStatueBehavior statue = collider.GetComponentInParent<SmilingStatueBehavior>(); // 체력 없는 웃는 석상 여부 검색

            if (statue != null) // 웃는 석상 Collider 여부 확인
            {
                actor = statue.transform.root.gameObject; // 불사 석상도 물리 압력판은 누를 수 있도록 루트 반환
                return true; // 함정 작동 대상 반환
            }

            return false; // 플레이어·몬스터가 아닌 Collider 제외
        }
    }
}
