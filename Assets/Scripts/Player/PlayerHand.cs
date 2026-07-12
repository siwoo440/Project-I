using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 활성 손 슬롯 1개. (기획서 PART 5.4.3, 8.1.2)
    /// 플레이어는 한 번에 하나의 아이템만 손에 든다 → "빛(광원) vs 화력(무기)" 트레이드오프의 토대.
    /// TODO: 4일차 인벤토리(5칸) 연동 시, 선택된 슬롯의 아이템을 손에 표시하도록 확장.
    /// </summary>
    public class PlayerHand : MonoBehaviour
    {
        [SerializeField] Transform handAnchor;   // 카메라 앞 '손' 위치 (없으면 자동 생성)
        [SerializeField] float dropForce = 2.5f;

        public PickupItem Held { get; private set; }
        public bool IsEmpty => Held == null;

        void Awake()
        {
            if (handAnchor == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    var anchor = new GameObject("HandAnchor").transform;
                    anchor.SetParent(cam.transform);
                    anchor.localPosition = new Vector3(0.4f, -0.3f, 0.7f); // 우하단 앞쪽
                    anchor.localRotation = Quaternion.identity;
                    handAnchor = anchor;
                }
            }
        }

        /// <summary>손이 비어 있으면 아이템을 든다.</summary>
        public bool TryHold(PickupItem item)
        {
            if (!IsEmpty || item == null) return false;
            Held = item;
            item.OnPickedUp(handAnchor);
            return true;
        }

        /// <summary>든 아이템을 앞으로 던져 내려놓는다.</summary>
        public void DropHeld()
        {
            if (IsEmpty) return;
            var item = Held;
            Held = null;
            Vector3 dir = handAnchor != null ? handAnchor.forward : transform.forward;
            item.OnDropped(dir * dropForce);
        }
    }
}
