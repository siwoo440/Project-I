using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 인벤토리 · 무게. (기획서 PART 4.4, 8)
    /// 기본 5칸 + 가방 보너스 칸(동시 적용). 아이템은 slots만큼 칸 차지.
    /// 휠로 선택 → 선택 아이템을 손(PlayerHand.Anchor)에 표시.
    /// 무게 구간별 페널티(이동속도·스태미너 회복) 제공 → PlayerController가 참조.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        [SerializeField] int baseSlots = 5;
        [SerializeField] float weightLimit = 30f;

        readonly List<PickupItem> items = new List<PickupItem>();
        int selected = -1;
        PlayerHand hand;
        Transform storage;

        public int BonusSlots { get { int b = 0; foreach (var i in items) b += i.BonusSlots; return b; } }
        public int TotalSlots => baseSlots + BonusSlots;
        public int UsedSlots { get { int s = 0; foreach (var i in items) s += i.Slots; return s; } }
        public float CurrentWeight { get { float w = 0; foreach (var i in items) w += i.Weight; return w; } }
        public float WeightRatio => weightLimit > 0f ? CurrentWeight / weightLimit : 0f;

        void Awake()
        {
            hand = GetComponent<PlayerHand>();
            var s = new GameObject("Inventory_Storage").transform;
            s.SetParent(transform);
            storage = s;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || items.Count == 0) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f) Select((selected + 1) % items.Count);
            else if (scroll < -0.01f) Select((selected - 1 + items.Count) % items.Count);
        }

        /// <summary>슬롯이 남으면 아이템을 담는다. (무게는 하드 제한이 아니라 페널티로 작동)</summary>
        public bool TryAdd(PickupItem item)
        {
            if (item == null) return false;
            if (UsedSlots + item.Slots > TotalSlots)
            {
                Debug.Log($"[Inventory] 슬롯 부족 ({UsedSlots}/{TotalSlots}, 필요 {item.Slots})");
                return false;
            }
            item.EnterInventory(storage);
            items.Add(item);
            Select(items.Count - 1);
            return true;
        }

        /// <summary>선택된 아이템을 앞으로 던져 버린다.</summary>
        public void DropSelected()
        {
            if (selected < 0 || selected >= items.Count) return;
            var item = items[selected];
            items.RemoveAt(selected);
            Vector3 dir = (hand != null && hand.Anchor != null) ? hand.Anchor.forward : transform.forward;
            item.ExitToWorld(dir * 2.5f);

            if (items.Count == 0) selected = -1;
            else Select(Mathf.Clamp(selected, 0, items.Count - 1));
        }

        void Select(int index)
        {
            if (selected >= 0 && selected < items.Count) items[selected].HideInHand();
            selected = index;
            if (selected >= 0 && selected < items.Count && hand != null)
                items[selected].ShowInHand(hand.Anchor);
        }

        // ---- 무게 페널티 (기획서 PART 4.4.1) ----
        public float SpeedMultiplier
        {
            get
            {
                float r = WeightRatio;
                if (r <= 0.5f) return 1f;
                if (r <= 0.8f) return 0.85f;
                if (r <= 1.0f) return 0.70f;
                return 0.40f; // 과적
            }
        }

        public float StaminaRegenMultiplier
        {
            get
            {
                float r = WeightRatio;
                if (r <= 0.5f) return 1f;
                if (r <= 0.8f) return 0.75f;
                if (r <= 1.0f) return 0.50f;
                return 0.10f; // 과적
            }
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 100, 640, 20),
                $"인벤토리: {UsedSlots}/{TotalSlots}칸   무게: {CurrentWeight:F1}/{weightLimit:F0}kg ({WeightRatio * 100f:F0}%)");
            string sel = (selected >= 0 && selected < items.Count) ? items[selected].DisplayName : "없음";
            GUI.Label(new Rect(10, 120, 640, 20), $"손(선택): {sel}    [휠] 전환  /  [Q] 버리기");
        }
    }
}
