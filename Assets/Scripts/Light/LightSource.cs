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

        [SerializeField] Kind kind = Kind.Lantern;
        [SerializeField] float brightnessValue = 50f;  // 방 밝기 기여치 (횃불 30 / 랜턴 50)
        [SerializeField] Light visualLight;            // 실제 조명(없으면 자식에서 검색)
        [SerializeField] bool isOn = false;            // 휴대 광원 초기값 = 꺼짐 (F로 켬)

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
