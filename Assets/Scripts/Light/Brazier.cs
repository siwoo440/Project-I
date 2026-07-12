using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 화톳불(횃불대) — 구역 고정 광원. 시간이 지날수록 밝기 감소, E로 재점화. (기획서 PART 5.4.2, 5.5)
    /// </summary>
    public class Brazier : MonoBehaviour, IInteractable
    {
        [SerializeField] float maxBrightness = 40f;   // 최대 기여치
        [SerializeField] float decayPerSec = 1.5f;    // 초당 감소량
        [SerializeField] Light visualLight;           // 실제 조명(없으면 자식 검색)
        [SerializeField] float maxIntensity = 2.5f;   // 최대 조명 세기

        float current;

        public float Contribution => current;         // LightRoom이 합산

        void Awake()
        {
            if (visualLight == null) visualLight = GetComponentInChildren<Light>(true);
            current = maxBrightness;
            Apply();
        }

        void Update()
        {
            if (current > 0f)
            {
                current = Mathf.Max(0f, current - decayPerSec * Time.deltaTime);
                Apply();
            }
        }

        public string GetPrompt() => $"[E] 재점화 (밝기 {Mathf.RoundToInt(current)})";
        public void Interact(PlayerInteractor interactor) { current = maxBrightness; Apply(); }

        void Apply()
        {
            if (visualLight == null) return;
            float t = maxBrightness > 0f ? current / maxBrightness : 0f;
            visualLight.intensity = maxIntensity * t;
            visualLight.enabled = current > 0.01f;
        }
    }
}
