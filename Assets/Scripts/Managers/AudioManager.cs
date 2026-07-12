using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 3D 위치 기반 오디오 · 소음 · BGM/SFX. (기획서 PART 12.6)
    /// 사운드가 곧 생존 정보(몬스터·발소리 방향) + 소음 시스템 연동.
    /// TODO: 3D 오디오, 소음 이벤트, BGM 상태. (Phase 1~2)
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[AudioManager] 초기화 (스텁)");
        }
    }
}
