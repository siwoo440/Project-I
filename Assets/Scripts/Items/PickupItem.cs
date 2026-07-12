using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 주울 수 있는 아이템. (기획서 PART 8)
    /// 상호작용(E) 시 인벤토리에 저장, 버리기(Q) 시 월드로 배출.
    /// 무게·슬롯 등은 ItemData(없으면 fallback 값)에서 가져온다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [Header("데이터 (없으면 아래 fallback 사용)")]
        [SerializeField] ItemData data;
        [SerializeField] string fallbackName = ""; // 비우면 오브젝트(GameObject) 이름 사용
        [SerializeField] float fallbackWeight = 1f;
        [SerializeField] int fallbackSlots = 1;

        Rigidbody rb;
        Collider col;

        /// <summary>표시 이름: ① ItemData.displayName → ② fallbackName → ③ 오브젝트 이름.
        /// (기본값 "아이템"은 미설정으로 간주하여 오브젝트 이름으로 대체)</summary>
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

        /// <summary>인벤토리에 저장(숨김).</summary>
        public void EnterInventory(Transform storageParent)
        {
            SetPhysics(false);
            transform.SetParent(storageParent);
            transform.localPosition = Vector3.zero;
            gameObject.SetActive(false);
        }

        /// <summary>선택되어 손 앵커에 표시.</summary>
        public void ShowInHand(Transform anchor)
        {
            gameObject.SetActive(true);
            SetPhysics(false);
            if (anchor != null)
            {
                transform.SetParent(anchor);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>선택 해제(숨김).</summary>
        public void HideInHand() => gameObject.SetActive(false);

        /// <summary>월드로 배출(버리기).</summary>
        public void ExitToWorld(Vector3 impulse)
        {
            gameObject.SetActive(true);
            transform.SetParent(null);
            SetPhysics(true);
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }

        void SetPhysics(bool on)
        {
            if (col != null) col.enabled = on;
            rb.detectCollisions = on;
            rb.isKinematic = !on;
        }
    }
}
