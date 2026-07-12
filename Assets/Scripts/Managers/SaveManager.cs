using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 세이브/로드. (기획서 PART 13.2.3)
    /// 일차(Day) 단위 저장. 공유(골드·빚·창고·마차강화) / 개인(능력치·장비).
    /// 로컬 JSON + Steam 클라우드, 멀티는 호스트 기준.
    /// TODO: 저장 구조 정의, 일차 재실행 지원. (Phase 2 일차 23)
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[SaveManager] 초기화 (스텁)");
        }
    }
}
