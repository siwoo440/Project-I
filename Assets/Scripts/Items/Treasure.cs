using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 보물. (기획서 PART 8.3) 가치는 생성 시점에 고정: 기본랜덤 × 밝기배수(생성 위치) × 리스크배수.
    /// 이후 어디로 옮겨도 값 불변(악용 방지). 두손 운반 보물은 슬롯 대신 양손으로 운반.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Treasure : MonoBehaviour, IInteractable, ICarryable
    {
        [Header("데이터 (없으면 아래 fallback)")]
        [SerializeField] TreasureData data;
        [SerializeField] int fbMinValue = 100;
        [SerializeField] int fbMaxValue = 300;
        [SerializeField] float fbWeight = 2f;
        [SerializeField] int fbSlots = 1;
        [SerializeField] bool fbTwoHanded = false;

        int lockedValue = -1;
        Rigidbody rb;
        Collider col;
        Transform storageParent;

        public string DisplayName => data != null ? data.displayName : name;
        public float Weight => data != null ? data.weightKg : fbWeight;
        public int Slots => Mathf.Max(1, data != null ? data.inventorySlots : fbSlots);
        public int BonusSlots => 0;
        public bool TwoHanded => data != null ? data.twoHanded : fbTwoHanded;
        public int Value => lockedValue;

        void Awake() { rb = GetComponent<Rigidbody>(); col = GetComponent<Collider>(); }
        void Start() { if (lockedValue < 0) GenerateValue(FindBrightnessMul(), 1f); }

        /// <summary>가치 고정 산정. (스폰 시스템(13일차)이 정확한 밝기·리스크 배수로 호출 예정)</summary>
        public void GenerateValue(float brightnessMul, float riskMul)
        {
            int min = data != null ? data.minValue : fbMinValue;
            int max = data != null ? data.maxValue : fbMaxValue;
            int baseVal = Random.Range(min, max + 1);
            lockedValue = Mathf.RoundToInt(baseVal * brightnessMul * riskMul);
        }

        float FindBrightnessMul()
        {
            foreach (var room in FindObjectsByType<LightRoom>(FindObjectsSortMode.None))
                if (room.Contains(transform.position)) return BrightnessMul(room.FixedBrightness);
            return 1f;
        }

        // 밝기 배수 (기획서 PART 6.4 / 8.3)
        static float BrightnessMul(float b)
        {
            if (b >= 81f) return 0.5f;
            if (b >= 61f) return 0.8f;
            if (b >= 41f) return 1.0f;
            if (b >= 21f) return 1.5f;
            return 2.0f;
        }

        public string GetPrompt()
            => $"[E] 보물: {DisplayName} ({Value}골드{(TwoHanded ? ", 두손" : "")})";

        public void Interact(PlayerInteractor interactor)
        {
            if (lockedValue < 0) GenerateValue(FindBrightnessMul(), 1f);
            if (interactor != null && interactor.Inventory != null)
                interactor.Inventory.TryAdd(this);
        }

        public void EnterInventory(Transform storage)
        {
            storageParent = storage;
            SetPhysics(false); SetVisible(false);
            transform.SetParent(storage); transform.localPosition = Vector3.zero;
        }
        public void ShowInHand(Transform anchor)
        {
            SetVisible(true); SetPhysics(false);
            if (anchor != null) { transform.SetParent(anchor); transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; }
        }
        public void HideInHand()
        {
            SetVisible(false);
            if (storageParent != null) { transform.SetParent(storageParent); transform.localPosition = Vector3.zero; }
        }
        public void ExitToWorld(Vector3 impulse)
        {
            SetVisible(true); transform.SetParent(null); SetPhysics(true); rb.AddForce(impulse, ForceMode.VelocityChange);
        }
        void SetVisible(bool v) { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v; }
        void SetPhysics(bool on) { if (col != null) col.enabled = on; rb.detectCollisions = on; rb.isKinematic = !on; }
    }
}
