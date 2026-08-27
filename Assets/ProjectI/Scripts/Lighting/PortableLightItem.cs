using System.Collections.Generic; // 활성 휴대 조명 목록 기능 참조
using ProjectI.Brightness; // 게임용 밝기 광원 기능 참조
using ProjectI.Items; // 월드 아이템과 사용 인터페이스 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Lighting // 휴대 조명 기능 네임스페이스
{
    [RequireComponent(typeof(WorldItem))] // 빠른 슬롯과 월드 배치를 위한 WorldItem 필수 지정
    [RequireComponent(typeof(BrightnessSource))] // 게임 밝기 계산용 BrightnessSource 필수 지정
    public sealed class PortableLightItem : MonoBehaviour, IUsableItem // 횃불·랜턴의 점화·소화·연료·보관 상태 처리
    {
        private static readonly HashSet<PortableLightItem> ActiveLights = new HashSet<PortableLightItem>(); // 현재 씬의 활성 휴대 조명 목록
        [SerializeField] private float maxFuel = 60f; // 최대 연료량
        [SerializeField] private float currentFuel = 60f; // 현재 남은 연료량
        [SerializeField] private float fuelConsumptionPerSecond = 1f; // 실제 점화 중 초당 연료 소비량
        [SerializeField] private bool isIgnited; // 사용자가 마지막으로 선택한 점화 상태
        private WorldItem worldItem; // 현재 인벤토리 보관·손·월드 상태 확인용 아이템
        private BrightnessSource brightnessSource; // 대표 게임용 밝기 광원
        private BrightnessSource[] brightnessSources; // 랜턴 주 빔과 근거리 주변광을 함께 제어할 모든 게임용 광원

        public static IEnumerable<PortableLightItem> Lights => ActiveLights; // F1 휴대 조명 디버그 페이지용 활성 목록 공개
        public float MaxFuel => maxFuel; // 최대 연료량 공개
        public float CurrentFuel => currentFuel; // 현재 연료량 공개
        public float NormalizedFuel => maxFuel <= 0f ? 0f : Mathf.Clamp01(currentFuel / maxFuel); // UI와 디버그용 연료 비율 공개
        public bool IsIgnited => isIgnited; // 사용자가 유지 중인 점화 상태 공개
        public bool IsStored // 현재 빠른 슬롯 숨김 보관 여부 공개
        {
            get
            {
                ResolveReferences(); // Edit Mode Validator에서도 비직렬화 참조를 안전하게 다시 확보
                return worldItem != null && worldItem.IsStored; // 실제 보관 상태 반환
            }
        }

        public bool IsEmitting // 현재 실제 빛을 내고 있는지 공개
        {
            get
            {
                ResolveReferences(); // Edit Mode Validator에서도 모든 BrightnessSource 참조를 안전하게 다시 확보

                foreach (BrightnessSource source in brightnessSources) // 휴대 조명에 연결된 모든 게임용 광원 순회
                {
                    if (source != null && source.SourceEnabled) // 하나라도 실제 빛을 내고 있는지 확인
                    {
                        return true; // 현재 휴대 조명이 발광 중임을 반환
                    }
                }

                return false; // 모든 게임용 광원이 꺼진 상태 반환
            }
        }

        public PortableLightState State => ResolveState(); // 현재 점화·보관·연료 상태 공개

        public WorldItem WorldItem // 디버그와 Validator용 WorldItem 참조 공개
        {
            get
            {
                ResolveReferences(); // 씬 재오픈 직후에도 같은 오브젝트의 WorldItem을 다시 조회
                return worldItem; // 확보된 WorldItem 참조 반환
            }
        }

        public BrightnessSource BrightnessSource // 검증용 이동형 BrightnessSource 참조 공개
        {
            get
            {
                ResolveReferences(); // 씬 재오픈 직후에도 같은 오브젝트의 BrightnessSource를 다시 조회
                return brightnessSource; // 확보된 BrightnessSource 참조 반환
            }
        }

        private void Awake() // 휴대 조명 초기화
        {
            ResolveReferences(); // WorldItem과 BrightnessSource 참조 확보
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel); // 저장된 연료량을 유효 범위로 보정

            if (currentFuel <= 0f) // 시작 연료가 비어 있는지 확인
            {
                isIgnited = false; // 빈 조명이 켜진 상태로 시작하지 않도록 소화
            }

            RefreshEmissionState(); // 시작 보관·점화 상태에 맞춰 실제 Light와 BrightnessSource 동기화
        }

        private void OnEnable() // 휴대 조명 활성화 처리
        {
            ResolveReferences(); // 필요한 구성 요소 참조 확보
            ActiveLights.Add(this); // F1 디버그와 향후 관리용 활성 목록에 등록
            RefreshEmissionState(); // 활성화 직후 실제 광원 상태 갱신
        }

        private void OnDisable() // 휴대 조명 비활성화 처리
        {
            ActiveLights.Remove(this); // 활성 휴대 조명 목록에서 제거

            ResolveReferences(); // 비활성화 직전 모든 게임용 광원 참조 확보

            foreach (BrightnessSource source in brightnessSources) // 주 빔과 주변광을 포함한 모든 광원 순회
            {
                if (source != null) // 유효한 광원인지 확인
                {
                    source.SetSourceEnabled(false); // 비활성 GameObject가 어떤 빛도 남기지 않도록 모두 끄기
                }
            }
        }

        private void Update() // 점화 상태와 인벤토리 상태에 따른 연료 소비 처리
        {
            ResolveReferences(); // 런타임 참조 손실에 대비해 필요한 구성 요소 확보

            if (!CanConsumeFuelNow()) // 현재 실제로 빛과 연료 소비가 가능한 상태인지 확인
            {
                RefreshEmissionState(); // 보관 또는 소화 상태에 맞춰 광원을 끄거나 유지
                return; // 연료 감소 없이 이번 프레임 종료
            }

            currentFuel = Mathf.Max(0f, currentFuel - (fuelConsumptionPerSecond * Time.deltaTime)); // 실제 점화 중에만 현재 연료 감소

            if (currentFuel <= 0f) // 이번 프레임에 연료가 모두 소모되었는지 확인
            {
                currentFuel = 0f; // 부동소수점 오차 없이 정확히 빈 연료로 정리
                isIgnited = false; // 연료 소진 시 자동 소화
            }

            RefreshEmissionState(); // 연료 감소 또는 자동 소화 결과를 실제 광원 상태에 반영
        }

        public void Configure(float maximumFuel, float consumptionPerSecond, bool startIgnited) // 에디터 자동 설정용 연료와 시작 상태 지정
        {
            ResolveReferences(); // 필요한 구성 요소 참조 확보
            maxFuel = Mathf.Max(1f, maximumFuel); // 최대 연료 최소값 보정
            currentFuel = maxFuel; // 새 테스트 아이템은 연료를 가득 채운 상태로 시작
            fuelConsumptionPerSecond = Mathf.Max(0.01f, consumptionPerSecond); // 초당 연료 소비량 최소값 보정
            isIgnited = startIgnited; // 테스트용 시작 점화 상태 저장
            RefreshEmissionState(); // 새 설정을 실제 광원에 즉시 반영
        }

        public bool CanUse(PlayerInventory inventory) // 좌클릭으로 현재 휴대 조명을 사용할 수 있는지 확인
        {
            ResolveReferences(); // 사용 시점에 필수 구성 요소 참조를 다시 확보

            if (inventory == null || worldItem == null) // 유효 인벤토리와 아이템 참조 존재 여부 확인
            {
                return false; // 잘못된 호출에서는 사용 차단
            }

            return isIgnited || currentFuel > 0f; // 켜진 조명은 끌 수 있고 꺼진 조명은 연료가 있을 때만 켤 수 있음
        }

        public void Use(PlayerInventory inventory) // 좌클릭으로 점화 또는 소화 상태 전환
        {
            if (!CanUse(inventory)) // 현재 사용 가능 여부 확인
            {
                return; // 사용 불가 상태에서는 변경 없음
            }

            if (isIgnited) // 현재 사용자가 켜 둔 상태인지 확인
            {
                isIgnited = false; // 좌클릭으로 소화 상태 전환
                RefreshEmissionState(); // 실제 Light와 BrightnessSource 즉시 끄기
                return; // 소화 처리 종료
            }

            if (currentFuel <= 0f) // 점화 전에 남은 연료 존재 여부 확인
            {
                return; // 빈 조명 점화 차단
            }

            isIgnited = true; // 좌클릭으로 점화 상태 전환
            RefreshEmissionState(); // 손에 들고 있는 상태라면 실제 광원 즉시 켜기
        }

        public void SetFuel(float value) // 향후 연료 충전 시스템에서 사용할 현재 연료 설정
        {
            currentFuel = Mathf.Clamp(value, 0f, maxFuel); // 현재 연료를 유효 범위로 저장

            if (currentFuel <= 0f) // 충전 결과가 빈 연료인지 확인
            {
                isIgnited = false; // 연료가 없으면 강제로 소화 상태 유지
            }

            RefreshEmissionState(); // 연료 변경을 실제 광원 상태에 반영
        }

        private bool CanConsumeFuelNow() // 현재 실제로 연료가 줄어야 하는 상태 판정
        {
            if (!isIgnited || currentFuel <= 0f) // 점화 여부와 남은 연료 확인
            {
                return false; // 꺼졌거나 빈 조명은 연료를 소비하지 않음
            }

            if (worldItem == null || worldItem.IsStored) // 빠른 슬롯 숨김 보관 여부 확인
            {
                return false; // 선택되지 않아 InventoryStorage에 숨겨진 동안 연료 소비 일시 정지
            }

            return true; // 손에 들었거나 월드에 내려놓은 점화 조명만 연료 소비
        }

        private void RefreshEmissionState() // 현재 상태를 Unity Light와 게임용 BrightnessSource에 동기화
        {
            ResolveReferences(); // 주 빔과 주변광을 포함한 모든 BrightnessSource 참조 확보
            bool shouldEmit = isIgnited && currentFuel > 0f && worldItem != null && !worldItem.IsStored; // 손 또는 월드의 점화 조명만 실제 빛을 내도록 판정

            foreach (BrightnessSource source in brightnessSources) // 휴대 조명에 속한 모든 게임용 광원 순회
            {
                if (source != null) // 유효한 광원인지 확인
                {
                    source.SetSourceEnabled(shouldEmit); // 랜턴 주 빔과 주변광 또는 횃불 광원을 동시에 켜거나 끄기
                }
            }
        }

        private PortableLightState ResolveState() // 현재 상태를 디버그용 enum으로 변환
        {
            ResolveReferences(); // Edit Mode F1/Validator에서도 실제 WorldItem 상태를 읽을 수 있도록 참조 확보

            if (currentFuel <= 0f) // 남은 연료가 없는지 확인
            {
                return PortableLightState.Empty; // 빈 연료 상태 반환
            }

            if (isIgnited && worldItem != null && worldItem.IsStored) // 점화 상태를 기억한 채 슬롯에 숨겨졌는지 확인
            {
                return PortableLightState.StoredPaused; // 광원과 연료 소비가 일시 정지된 상태 반환
            }

            if (isIgnited) // 실제 또는 잠재 점화 상태인지 확인
            {
                return PortableLightState.Ignited; // 손 또는 월드에서 켜진 상태 반환
            }

            return PortableLightState.Extinguished; // 연료는 있지만 꺼진 상태 반환
        }

        private void ResolveReferences() // 같은 오브젝트의 필수 구성 요소 참조 확보
        {
            if (worldItem == null) // WorldItem 참조 누락 확인
            {
                worldItem = GetComponent<WorldItem>(); // 같은 오브젝트의 WorldItem 조회
            }

            if (brightnessSource == null) // 대표 BrightnessSource 참조 누락 확인
            {
                brightnessSource = GetComponent<BrightnessSource>(); // 같은 오브젝트의 주 게임용 광원 조회
            }

            if (brightnessSources == null || brightnessSources.Length == 0) // 휴대 조명 전체 광원 배열 누락 확인
            {
                brightnessSources = GetComponentsInChildren<BrightnessSource>(true); // 주 빔과 근거리 주변광을 포함해 모든 자식 BrightnessSource 조회
            }
        }

        private void OnValidate() // 인스펙터 연료 값 검증
        {
            maxFuel = Mathf.Max(1f, maxFuel); // 최대 연료 최소값 보정
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel); // 현재 연료 유효 범위 보정
            fuelConsumptionPerSecond = Mathf.Max(0.01f, fuelConsumptionPerSecond); // 초당 연료 소비량 최소값 보정
            ResolveReferences(); // 에디터 상태에서도 필수 구성 요소 참조 확보
            RefreshEmissionState(); // 인스펙터 변경 내용을 실제 광원 상태에 반영
        }
    }
}
