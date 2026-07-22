using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 손 위치(앵커) 제공. InventorySystem이 선택된 아이템을 이 앵커에 표시한다.
    /// (활성 손 슬롯 1개 규칙, 기획서 PART 5.4.3 / 8.1.2)
    /// </summary>
    public class PlayerHand : MonoBehaviour
    {
        [Tooltip("없으면 카메라 밑에 자동 생성")] [SerializeField] Transform handAnchor;   // 없으면 카메라 밑에 자동 생성
        public Transform Anchor => handAnchor;

        void Awake()
        {
            if (handAnchor == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    var a = new GameObject("HandAnchor").transform;
                    a.SetParent(cam.transform);
                    a.localPosition = new Vector3(0.4f, -0.3f, 0.7f); // 우하단 앞
                    a.localRotation = Quaternion.identity;
                    handAnchor = a;
                }
            }
        }
    }
}
