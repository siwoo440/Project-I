using ProjectI.Brightness; // 광원·플레이어 밝기 센서 기능 참조
using UnityEngine; // 카메라와 IMGUI 화면 표시 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class LightDebugLabelManager : MonoBehaviour // F1이 열렸을 때 각 월드 광원 옆에 현재 기여 밝기를 숫자로 표시
    {
        [SerializeField] private DebugPageManager debugPageManager; // F1 디버그 창 현재 열림 상태 제공자
        [SerializeField] private PlayerBrightnessSensor sensor; // 현재 플레이어 위치 제공자
        [SerializeField] private Camera targetCamera; // 광원 월드 좌표를 화면 좌표로 변환할 카메라
        private GUIStyle labelStyle; // 광원 숫자 라벨 공통 IMGUI 스타일

        private void Awake() // 월드 광원 라벨 관리자 초기화
        {
            ResolveReferences(); // F1 관리자·센서·카메라 참조 자동 확보
        }

        private void OnGUI() // F1 디버그 상태에서 모든 광원의 현재 기여 밝기 화면 표시
        {
            ResolveReferences(); // 씬 상태 변경에 대비해 필요한 참조 확보

            if (debugPageManager == null || !debugPageManager.IsOpen || sensor == null || targetCamera == null) // F1이 닫혔거나 필요한 참조가 없는지 확인
            {
                return; // 평상시에는 모든 월드 광원 라벨 계산과 표시 생략
            }

            EnsureStyle(); // IMGUI 라벨 스타일 최초 1회 생성
            Vector3 samplePosition = sensor.transform.position; // 현재 플레이어 밝기 계산 위치 조회
            IndoorBrightnessArea targetArea = IndoorBrightnessArea.FindContaining(samplePosition); // 현재 플레이어의 Outdoor 또는 Indoor 방 조회

            foreach (BrightnessSource source in BrightnessSource.Sources) // 현재 등록된 모든 게임용 광원 순회
            {
                if (source == null) // 파괴되었거나 누락된 광원 확인
                {
                    continue; // 다음 광원 검사
                }

                Vector3 worldPosition = BrightnessDebugUtility.GetEmissionPosition(source); // 실제 Light의 월드 위치 조회
                Vector3 screenPosition = targetCamera.WorldToScreenPoint(worldPosition); // 광원 위치를 현재 화면 좌표로 변환

                if (screenPosition.z <= 0f) // 카메라 뒤쪽에 있는 광원인지 확인
                {
                    continue; // 화면 뒤 광원 라벨은 표시하지 않음
                }

                LightContributionDebugInfo info = BrightnessDebugUtility.Evaluate(source, samplePosition, targetArea); // 현재 플레이어 위치의 실제 광원 기여 밝기 계산
                float guiX = screenPosition.x - 70f; // 라벨이 광원 중심 위에 오도록 X 좌표 보정
                float guiY = Screen.height - screenPosition.y - 24f; // Unity 화면 Y를 IMGUI 상단 기준 Y로 변환
                Rect labelRect = new Rect(guiX, guiY, 140f, 44f); // 이름과 숫자를 표시할 화면 사각형 생성
                GUI.Label(labelRect, $"{info.DisplayName}\n{info.Contribution:0.000}", labelStyle); // 광원 옆에 이름과 현재 실제 기여 밝기 표시
            }
        }

        public void Configure(DebugPageManager manager, PlayerBrightnessSensor targetSensor, Camera camera) // 에디터 자동 구성용 참조 지정
        {
            debugPageManager = manager; // F1 페이지 관리자 저장
            sensor = targetSensor; // 플레이어 밝기 센서 저장
            targetCamera = camera; // 1인칭 카메라 저장
        }

        private void ResolveReferences() // 필요한 런타임 참조 자동 확보
        {
            if (debugPageManager == null) // F1 관리자 참조 누락 확인
            {
                debugPageManager = Object.FindFirstObjectByType<DebugPageManager>(); // 현재 씬 공통 F1 관리자 조회
            }

            if (sensor == null) // 플레이어 밝기 센서 참조 누락 확인
            {
                sensor = Object.FindFirstObjectByType<PlayerBrightnessSensor>(); // 현재 씬 플레이어 센서 조회
            }

            if (targetCamera == null) // 플레이어 카메라 참조 누락 확인
            {
                targetCamera = Camera.main; // MainCamera 태그 카메라 우선 조회
            }

            if (targetCamera == null) // MainCamera로 찾지 못했는지 확인
            {
                targetCamera = Object.FindFirstObjectByType<Camera>(); // 현재 씬 첫 활성 카메라를 대체 참조로 조회
            }
        }

        private void EnsureStyle() // 광원 숫자 라벨 IMGUI 스타일 생성
        {
            if (labelStyle != null) // 이미 스타일이 준비됐는지 확인
            {
                return; // 중복 스타일 생성 방지
            }

            labelStyle = new GUIStyle(GUI.skin.box); // 기본 Box 기반 읽기 쉬운 라벨 스타일 생성
            labelStyle.alignment = TextAnchor.MiddleCenter; // 이름과 숫자를 중앙 정렬
            labelStyle.fontSize = 13; // 게임 화면에서 읽기 쉬운 글자 크기 적용
            labelStyle.normal.textColor = Color.white; // 어두운 환경에서도 읽히도록 흰 글자 적용
        }
    }
}
