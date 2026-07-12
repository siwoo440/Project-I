using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 몬스터 행동·탐지 (개체별 컴포넌트). (기획서 PART 7.3~7.4)
    /// 기본 FSM: 배회 → 탐지 → 추격 → 공격 → 복귀. 개체별 고유 AI로 오버라이드.
    /// 탐지: 시각(시야각/거리) + 청각(소음) + 밝기 연동.
    /// TODO: FSM, 탐지, 기믹별 확장. (Phase 1 일차 11, 14)
    /// </summary>
    public class MonsterAI : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[MonsterAI] 초기화 (스텁)");
        }
    }
}
