using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 인벤토리에 담기고 손에 들 수 있는 것(아이템·보물)의 공통 규격.
    /// InventorySystem이 이 인터페이스로 아이템·보물을 함께 관리한다.
    /// </summary>
    public interface ICarryable
    {
        string DisplayName { get; }
        float Weight { get; }
        int Slots { get; }
        int BonusSlots { get; }
        bool TwoHanded { get; }   // 두손 운반(대형 보물): 슬롯 미사용, 양손 점유

        void EnterInventory(Transform storage);
        void ShowInHand(Transform anchor);
        void HideInHand();
        void ExitToWorld(Vector3 impulse);
    }
}
