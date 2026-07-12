using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 카메라 전방으로 레이캐스트해 상호작용 대상 감지 + E(상호작용) / Q(선택 아이템 버리기). (기획서 PART 4.7)
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float interactDistance = 3f;
        [SerializeField] Transform rayOrigin;   // 카메라 (없으면 자동)

        public InventorySystem Inventory { get; private set; }
        IInteractable current;

        void Awake()
        {
            Inventory = GetComponent<InventorySystem>();
            if (rayOrigin == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) rayOrigin = cam.transform;
            }
        }

        void Update()
        {
            DetectTarget();

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.eKey.wasPressedThisFrame && current != null) current.Interact(this);
            if (kb.qKey.wasPressedThisFrame && Inventory != null) Inventory.DropSelected();
        }

        void DetectTarget()
        {
            current = null;
            if (rayOrigin == null) return;
            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, interactDistance))
                current = hit.collider.GetComponentInParent<IInteractable>();
        }

        void OnGUI()
        {
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 20, 20), "+");
            if (current != null)
                GUI.Label(new Rect(Screen.width / 2f - 120, Screen.height / 2f + 12, 240, 20), current.GetPrompt());
        }
    }
}
