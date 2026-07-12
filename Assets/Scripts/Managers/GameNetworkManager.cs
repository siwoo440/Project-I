using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 멀티플레이 네트워킹(Steam P2P) · 호스트 권한 · 동기화. (기획서 PART 10 / 13.3.1)
    /// ※ 유니티 Netcode의 NetworkManager와 이름 충돌을 피하려고 GameNetworkManager로 명명.
    /// TODO: Steamworks 로비/세션, 호스트 권한, 상태 동기화. (Phase 2 일차 17~)
    /// </summary>
    public class GameNetworkManager : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[GameNetworkManager] 초기화 (스텁)");
        }
    }
}
