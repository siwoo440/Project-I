using System.Collections.Generic;
using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 구역(방) 단위 밝기. 최종 밝기 = 기본 밝기(0) + 방 안 화톳불 기여 합. (기획서 PART 5.1)
    /// 트리거 볼륨(Box Collider, isTrigger)로 만들며, 플레이어가 들어오면 LightSystem에 현재 방을 알린다.
    /// </summary>
    public class LightRoom : MonoBehaviour
    {
        [SerializeField] float baseBrightness = 0f;   // 구역 기본 밝기(기본 0 = 칠흑)
        readonly List<Brazier> braziers = new List<Brazier>();
        Collider zone;

        void Awake()
        {
            zone = GetComponent<Collider>();
            if (zone == null)
            {
                Debug.LogWarning($"[LightRoom] {name}: Collider(Box)가 필요합니다.");
                return;
            }
            zone.isTrigger = true;

            // 방 범위 안에 있는 화톳불 수집
            foreach (var b in FindObjectsByType<Brazier>(FindObjectsSortMode.None))
                if (zone.bounds.Contains(b.transform.position)) braziers.Add(b);
        }

        /// <summary>기본 밝기 + 방 안 화톳불 현재 기여 합.</summary>
        public float FixedBrightness
        {
            get
            {
                float s = baseBrightness;
                foreach (var b in braziers) s += b.Contribution;
                return s;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var ls = other.GetComponentInParent<LightSystem>();
            if (ls != null) ls.SetCurrentRoom(this);
        }

        void OnTriggerExit(Collider other)
        {
            var ls = other.GetComponentInParent<LightSystem>();
            if (ls != null) ls.ClearRoom(this);
        }
    }
}
