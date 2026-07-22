using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 휴대 광원(횃불/랜턴). 밝기 기여치 + 실제 유니티 조명 On/Off. (기획서 PART 5.4)
    /// F키로 토글(LightSystem이 손에 든 것을 찾아 토글).
    /// </summary>
    public class LightSource : MonoBehaviour
    {
        public enum Kind { Torch, Lantern }

        [Tooltip("광원의 종류와 밝기 계산 방식을 구분하는 값")] [SerializeField] Kind kind = Kind.Lantern;
        [Tooltip("방 밝기 기여치 (횃불 30 / 랜턴 50)")] [SerializeField] float brightnessValue = 50f;  // 방 밝기 기여치 (횃불 30 / 랜턴 50)
        [Tooltip("실제 조명(없으면 자식에서 검색)")] [SerializeField] Light visualLight;            // 실제 조명(없으면 자식에서 검색)
        [Tooltip("휴대 광원 초기값 = 꺼짐 (T로 켬)")] [SerializeField] bool isOn = false;            // 휴대 광원 초기값 = 꺼짐 (T로 켬)

        public Kind LightType => kind;
        public bool IsOn => isOn;
        public float Contribution => isOn ? brightnessValue : 0f;

        void Awake()
        {
            if (visualLight == null) visualLight = GetComponentInChildren<Light>(true);
            Apply();
        }

        public void SetOn(bool on) { isOn = on; Apply(); }
        public void Toggle() => SetOn(!isOn);

        void Apply()
        {
            if (visualLight != null) visualLight.enabled = isOn;
        }
    }
}