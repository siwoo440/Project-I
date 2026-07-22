using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 마차 적재 지점. E → 지금 손에 든(선택 슬롯/두손) 것을 마차에 싣는다. (기획서 PART 3.4.3)
    /// </summary>
    public class WagonCargo : MonoBehaviour, IInteractable
    {
        [Tooltip("확보한 회수품을 전달할 부모 Wagon 컴포넌트")] [SerializeField] Wagon wagon;

        void Awake() { if (wagon == null) wagon = GetComponentInParent<Wagon>(); }
        public string GetPrompt() => "[E] 마차에 싣기";
        public void Interact(PlayerInteractor interactor)
        {
            if (interactor == null || interactor.Inventory == null || wagon == null) return;
            var item = interactor.Inventory.TakeSelected();
            if (item != null) wagon.Deposit(item);
            else Debug.Log("[Wagon] 실을 것이 없습니다(손에 든 것 없음).");
        }
    }
}
