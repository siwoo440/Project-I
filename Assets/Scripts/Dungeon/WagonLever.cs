using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 마차 '떠나기' 레버. E → 던전 종료(익스트랙션). (기획서 PART 3.2)
    /// </summary>
    public class WagonLever : MonoBehaviour, IInteractable
    {
        [Tooltip("레버 작동 시 탈출을 요청할 부모 Wagon 컴포넌트")] [SerializeField] Wagon wagon;

        void Awake() { if (wagon == null) wagon = GetComponentInParent<Wagon>(); }

        public string GetPrompt() => "[E] 떠나기 (던전 종료)";

        public void Interact(PlayerInteractor interactor)
        {
            if (wagon != null)
                wagon.Leave(interactor != null ? interactor.Inventory : null);
        }
    }
}
