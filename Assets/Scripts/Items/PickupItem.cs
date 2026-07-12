using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 주울 수 있는 아이템. (기획서 PART 8)
    /// 인벤토리에 넣으면 '오브젝트는 살아있되 모델만 숨김' → 켜진 광원(랜턴)은 소지 중 계속 빛남.
    /// 무게·슬롯·이름은 ItemData(없으면 fallback)에서 가져온다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PickupItem : MonoBehaviour, IInteractable, ICarryable
    {
        [Header("데이터 (없으면 아래 fallback 사용)")]
        [SerializeField] ItemData data;
        [SerializeField] string fallbackName = ""; // 비우면 오브젝트(GameObject) 이름 사용
        [SerializeField] float fallbackWeight = 1f;
        [SerializeField] int fallbackSlots = 1;

        Rigidbody rb;
        Collider col;
        Transform storageParent;

        /// <summary>표시 이름: ① ItemData.displayName → ② fallbackName → ③ 오브젝트 이름.</summary>
        public string DisplayName
        {
            get
            {
                if (data != null && !string.IsNullOrWhiteSpace(data.displayName) && data.displayName != "아이템")
                    return data.displayName;
                if (!string.IsNullOrWhiteSpace(fallbackName) && fallbackName != "아이템")
                    return fallbackName;
                return gameObject.name;
            }
        }

        public float Weight => data != null ? data.weightKg : fallbackWeight;
        public int Slots => Mathf.Max(1, data != null ? data.inventorySlots : fallbackSlots);
        public int BonusSlots => data != null ? data.bonusSlots : 0;
        public bool TwoHanded => false; // 일반 아이템은 두손 아님

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public string GetPrompt() => $"[E] 줍기: {DisplayName}";

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor != null && interactor.Inventory != null)
                interactor.Inventory.TryAdd(this);
        }

        // ---- ICarryable: 오브젝트는 활성 유지, 모델(Renderer)만 On/Off ----
        public void EnterInventory(Transform storage)
        {
            storageParent = storage;
            SetPhysics(false);
            SetVisible(false);
            transform.SetParent(storage);
            transform.localPosition = Vector3.zero;
        }

        public void ShowInHand(Transform anchor)
        {
            SetVisible(true);
            SetPhysics(false);
            if (anchor != null)
            {
                transform.SetParent(anchor);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        public void HideInHand()
        {
            SetVisible(false);
            if (storageParent != null)
            {
                transform.SetParent(storageParent);
                transform.localPosition = Vector3.zero;
            }
        }

        public void ExitToWorld(Vector3 impulse)
        {
            SetVisible(true);
            transform.SetParent(null);
            SetPhysics(true);
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        }

        void SetPhysics(bool on)
        {
            if (col != null) col.enabled = on;
            rb.detectCollisions = on;
            rb.isKinematic = !on;
        }
    }
}
