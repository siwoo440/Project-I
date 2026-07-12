using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 주울 수 있는 아이템. (기획서 PART 8) 상호작용(E) 시 손에 들리고, 버리기(Q) 시 앞으로 던져진다.
    /// 들고 있는 동안에는 물리/충돌을 끄고 손 앵커에 부착.
    /// TODO: 무게·인벤토리 칸·소음 등 데이터(아이템.csv) 연동은 4일차(InventorySystem).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] string itemName = "아이템";

        Rigidbody rb;
        Collider col;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public string GetPrompt() => $"[E] 줍기: {itemName}";

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor != null && interactor.Hand != null)
                interactor.Hand.TryHold(this);
        }

        /// <summary>손에 들릴 때: 물리 끄고 앵커에 부착.</summary>
        public void OnPickedUp(Transform anchor)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            if (col != null) col.enabled = false;
            if (anchor != null)
            {
                transform.SetParent(anchor);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>내려놓을 때: 부모 해제, 물리 복구 후 힘 적용.</summary>
        public void OnDropped(Vector3 impulse)
        {
            transform.SetParent(null);
            if (col != null) col.enabled = true;
            rb.detectCollisions = true;
            rb.isKinematic = false;
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }
    }
}
