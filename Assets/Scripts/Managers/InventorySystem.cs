using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 인벤토리 · 무게. (기획서 PART 4.4, 8) 아이템·보물을 ICarryable로 공용 관리.
    /// 기본 5칸 + 가방 보너스 칸. 두손 운반 보물은 슬롯 대신 '양손'으로 운반(슬롯 미사용, 손 점유).
    /// 하단 중앙 핫바 UI로 칸 표시.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        [SerializeField] int baseSlots = 5;
        [SerializeField] float weightLimit = 30f;

        readonly List<ICarryable> items = new List<ICarryable>();
        ICarryable twoHand;   // 두손 운반 중인 보물
        int selected = -1;
        PlayerHand hand;
        Transform storage;

        public int BonusSlots { get { int b = 0; foreach (var i in items) b += i.BonusSlots; return b; } }
        public int TotalSlots => baseSlots + BonusSlots;
        public int UsedSlots { get { int s = 0; foreach (var i in items) s += i.Slots; return s; } }
        public float CurrentWeight
        {
            get { float w = 0; foreach (var i in items) w += i.Weight; if (twoHand != null) w += twoHand.Weight; return w; }
        }
        public float WeightRatio => weightLimit > 0f ? CurrentWeight / weightLimit : 0f;
        public bool CarryingTwoHand => twoHand != null;

        void Awake()
        {
            hand = GetComponent<PlayerHand>();
            var s = new GameObject("Inventory_Storage").transform;
            s.SetParent(transform);
            storage = s;
        }

        void Update()
        {
            if (twoHand != null) return; // 두손 운반 중엔 슬롯 전환 없음
            var mouse = Mouse.current;
            if (mouse == null || items.Count == 0) return;
            float sc = mouse.scroll.ReadValue().y;
            if (sc > 0.01f) Select((selected + 1) % items.Count);
            else if (sc < -0.01f) Select((selected - 1 + items.Count) % items.Count);
        }

        public bool TryAdd(ICarryable c)
        {
            if (c == null) return false;

            // 두손 물건을 들고 있으면 내려놓기 전까지 아무것도 줍지 못함
            if (twoHand != null)
            {
                Debug.Log("[Inventory] 두손 운반 중 — 먼저 [Q]로 내려놓으세요");
                return false;
            }

            if (c.TwoHanded)
            {
                if (selected >= 0 && selected < items.Count) items[selected].HideInHand();
                twoHand = c;
                c.EnterInventory(storage);
                if (hand != null) c.ShowInHand(hand.Anchor);
                return true;
            }

            if (UsedSlots + c.Slots > TotalSlots)
            {
                Debug.Log($"[Inventory] 슬롯 부족 ({UsedSlots}/{TotalSlots}, 필요 {c.Slots})");
                return false;
            }
            c.EnterInventory(storage);
            items.Add(c);
            if (twoHand == null) Select(items.Count - 1);
            return true;
        }

        public void DropSelected()
        {
            if (twoHand != null)
            {
                var t = twoHand; twoHand = null; Toss(t);
                if (items.Count > 0) Select(Mathf.Clamp(selected, 0, items.Count - 1));
                return;
            }
            if (selected < 0 || selected >= items.Count) return;
            var item = items[selected]; items.RemoveAt(selected); Toss(item);
            if (items.Count == 0) selected = -1;
            else Select(Mathf.Clamp(selected, 0, items.Count - 1));
        }

        void Toss(ICarryable c)
        {
            Vector3 dir = (hand != null && hand.Anchor != null) ? hand.Anchor.forward : transform.forward;
            c.ExitToWorld(dir * 2.5f);
        }

        void Select(int index)
        {
            if (twoHand != null) return;
            if (selected >= 0 && selected < items.Count) items[selected].HideInHand();
            selected = index;
            if (selected >= 0 && selected < items.Count && hand != null) items[selected].ShowInHand(hand.Anchor);
        }

        // 무게 페널티 (기획서 PART 4.4.1)
        public float SpeedMultiplier { get { float r = WeightRatio; if (r <= 0.5f) return 1f; if (r <= 0.8f) return 0.85f; if (r <= 1f) return 0.70f; return 0.40f; } }
        public float StaminaRegenMultiplier { get { float r = WeightRatio; if (r <= 0.5f) return 1f; if (r <= 0.8f) return 0.75f; if (r <= 1f) return 0.50f; return 0.10f; } }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 100, 640, 20),
                $"인벤토리: {UsedSlots}/{TotalSlots}칸   무게: {CurrentWeight:F1}/{weightLimit:F0}kg ({WeightRatio * 100f:F0}%)");
            if (twoHand != null)
                GUI.Label(new Rect(10, 120, 640, 20), $"두손 운반 중: {twoHand.DisplayName}   [Q] 내려놓기");

            DrawHotbar();
        }

        void DrawHotbar()
        {
            int total = TotalSlots;
            float bw = 48f, gap = 4f, h = 48f;
            float totalW = total * bw + (total - 1) * gap;
            float x0 = (Screen.width - totalW) / 2f;
            float y = Screen.height - h - 12f;

            int slot = 0;
            for (int i = 0; i < items.Count && slot < total; i++)
                for (int s = 0; s < items[i].Slots && slot < total; s++, slot++)
                    DrawSlot(x0 + slot * (bw + gap), y, bw, h, i == selected, s == 0 ? Abbrev(items[i].DisplayName) : "·");
            for (; slot < total; slot++)
                DrawSlot(x0 + slot * (bw + gap), y, bw, h, false, "");
        }

        void DrawSlot(float x, float y, float w, float h, bool selectedSlot, string label)
        {
            var prev = GUI.color;
            GUI.color = selectedSlot ? new Color(1f, 0.9f, 0.4f, 0.95f) : new Color(1f, 1f, 1f, 0.55f);
            GUI.Box(new Rect(x, y, w, h), label);
            GUI.color = prev;
        }

        static string Abbrev(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length <= 4 ? s : s.Substring(0, 4));
    }
}
