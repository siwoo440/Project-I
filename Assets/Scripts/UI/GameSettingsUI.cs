using UnityEngine; // Unity 기본 기능과 OnGUI 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class GameSettingsUI : MonoBehaviour // 메인 메뉴 게임 설정 화면 관리
    {
        [Header("설정 연결")] // Inspector 설정 관리자 연결 구분
        [SerializeField] GameSettingsManager settingsManager; // 설정값을 저장하고 적용할 관리자

        bool windowOpen; // 현재 설정 화면 표시 여부
        float draftSensitivity = 1f; // 화면에서 편집 중인 마우스 감도

        float draftVolume = 0.8f; // 화면에서 편집 중인 전체 음량
        float draftBgmVolume = 0.7f; // 화면에서 편집 중인 배경음 음량
        float draftSfxVolume = 0.8f; // 화면에서 편집 중인 효과음 음량
        float draftAmbientVolume = 0.7f; // 화면에서 편집 중인 환경음 음량

        bool draftFullScreen = true; // 화면에서 편집 중인 전체 화면 여부
        bool draftVSync = true; // 화면에서 편집 중인 수직 동기화 여부
        int selectedResolutionIndex; // 화면에서 선택한 해상도 번호

        public bool IsOpen => windowOpen; // 현재 설정 화면 표시 여부 반환

        void Awake() // 설정 관리자 초기 참조 검색
        {
            ResolveSettingsManager(); // 설정 관리자 연결 시도
        }

        void ResolveSettingsManager() // 싱글톤과 현재 Scene에서 설정 관리자 검색
        {
            if (GameSettingsManager.Instance != null) // 전역 설정 관리자 존재 여부 확인
            {
                settingsManager = GameSettingsManager.Instance; // 전역 설정 관리자 연결
                return; // 추가 검색 중단
            }

            if (settingsManager == null) // Inspector 참조 존재 여부 확인
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>(); // 현재 Scene에서 설정 관리자 검색
            }
        }

        public void Open() // 저장된 설정값을 복사하고 설정 화면 열기
        {
            ResolveSettingsManager(); // 설정 관리자 참조 다시 확인

            if (settingsManager == null) // 설정 관리자 존재 여부 확인
            {
                Debug.LogError("[SettingsUI] GameSettingsManager가 없습니다."); // 관리자 누락 오류 출력
                return; // 설정 화면 열기 중단
            }

            draftSensitivity = settingsManager.MouseSensitivityMultiplier; // 현재 마우스 감도를 편집값으로 복사

            draftVolume = settingsManager.MasterVolume; // 현재 전체 음량을 편집값으로 복사
            draftBgmVolume = settingsManager.BgmVolume; // 현재 배경음 음량을 편집값으로 복사
            draftSfxVolume = settingsManager.SfxVolume; // 현재 효과음 음량을 편집값으로 복사
            draftAmbientVolume = settingsManager.AmbientVolume; // 현재 환경음 음량을 편집값으로 복사

            draftFullScreen = settingsManager.FullScreen; // 현재 전체 화면 여부를 편집값으로 복사
            draftVSync = settingsManager.VSync; // 현재 수직 동기화 여부를 편집값으로 복사
            selectedResolutionIndex = settingsManager.FindResolutionIndex(settingsManager.ResolutionWidth, settingsManager.ResolutionHeight); // 현재 해상도 번호 검색
            windowOpen = true; // 설정 화면 표시 활성화
        }

        public void Close() // 저장하지 않고 설정 화면 닫기
        {
            windowOpen = false; // 설정 화면 표시 비활성화
        }

        void SelectPreviousResolution() // 이전 해상도 선택
        {
            if (settingsManager == null || settingsManager.ResolutionCount <= 0) // 설정 관리자와 해상도 목록 확인
            {
                return; // 해상도 변경 중단
            }

            selectedResolutionIndex--; // 선택한 해상도 번호 감소

            if (selectedResolutionIndex < 0) // 첫 번째 해상도보다 앞으로 이동했는지 확인
            {
                selectedResolutionIndex = settingsManager.ResolutionCount - 1; // 마지막 해상도로 순환
            }
        }

        void SelectNextResolution() // 다음 해상도 선택
        {
            if (settingsManager == null || settingsManager.ResolutionCount <= 0) // 설정 관리자와 해상도 목록 확인
            {
                return; // 해상도 변경 중단
            }

            selectedResolutionIndex++; // 선택한 해상도 번호 증가

            if (selectedResolutionIndex >= settingsManager.ResolutionCount) // 마지막 해상도를 넘어갔는지 확인
            {
                selectedResolutionIndex = 0; // 첫 번째 해상도로 순환
            }
        }

        void SetDraftDefaults() // 설정 화면의 편집값을 기본값으로 변경
        {
            draftSensitivity = 1f; // 기본 마우스 감도 적용

            draftVolume = 0.8f; // 기본 전체 음량 적용
            draftBgmVolume = 0.7f; // 기본 배경음 음량 적용
            draftSfxVolume = 0.8f; // 기본 효과음 음량 적용
            draftAmbientVolume = 0.7f; // 기본 환경음 음량 적용

            draftFullScreen = true; // 기본 전체 화면 적용
            draftVSync = true; // 기본 수직 동기화 적용
            selectedResolutionIndex = settingsManager.FindResolutionIndex(1920, 1080); // Full HD와 가장 가까운 해상도 선택
        }

        void ApplyAndClose() // 편집 중인 설정 저장과 적용 후 화면 닫기
        {
            if (settingsManager == null) // 설정 관리자 존재 여부 확인
            {
                return; // 설정 저장 중단
            }

            Vector2Int resolution = settingsManager.GetResolution(selectedResolutionIndex); // 선택한 해상도 가져오기
            settingsManager.SaveAndApply(draftSensitivity, draftVolume, draftBgmVolume, draftSfxVolume, draftAmbientVolume, draftFullScreen, draftVSync, resolution.x, resolution.y); 
            // 편집한 게임과 오디오 설정 저장 및 적용
            windowOpen = false; // 설정 화면 닫기
        }

        void OnGUI() // OnGUI 기반 게임과 오디오 설정 화면 표시
        {
            if (!windowOpen) // 설정 화면 표시 여부 확인
            {
                return; // GUI 표시 중단
            }

            ResolveSettingsManager(); // 설정 관리자 참조 유지

            if (settingsManager == null) // 설정 관리자 존재 여부 확인
            {
                return; // 설정 화면 표시 중단
            }

            Vector2Int selectedResolution = settingsManager.GetResolution(selectedResolutionIndex); // 현재 선택한 해상도 가져오기

            float width = 700f; // 설정 창 너비
            float height = 650f; // 설정 창 높이
            float x = (Screen.width - width) * 0.5f; // 화면 가로 중앙 위치 계산
            float y = (Screen.height - height) * 0.5f; // 화면 세로 중앙 위치 계산

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 설정 제목용 GUI 스타일 생성
            titleStyle.alignment = TextAnchor.MiddleCenter; // 설정 제목 가운데 정렬
            titleStyle.fontSize = 32; // 설정 제목 글자 크기 설정
            titleStyle.fontStyle = FontStyle.Bold; // 설정 제목 굵게 표시

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label); // 설정 항목용 GUI 스타일 생성
            labelStyle.fontSize = 16; // 설정 항목 글자 크기 설정
            labelStyle.alignment = TextAnchor.MiddleLeft; // 설정 항목 왼쪽 정렬

            GUIStyle valueStyle = new GUIStyle(GUI.skin.label); // 설정값 표시용 GUI 스타일 생성
            valueStyle.fontSize = 16; // 설정값 글자 크기 설정
            valueStyle.alignment = TextAnchor.MiddleRight; // 설정값 오른쪽 정렬

            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty); // 메뉴 뒤쪽 입력 차단용 전체 배경 표시
            GUI.Box(new Rect(x, y, width, height), string.Empty); // 게임 설정 창 배경 표시
            GUI.Label(new Rect(x + 20f, y + 20f, width - 40f, 55f), "게임 설정", titleStyle); // 게임 설정 제목 표시

            GUI.Label(new Rect(x + 55f, y + 90f, 230f, 30f), "마우스 감도", labelStyle); // 마우스 감도 항목 표시
            GUI.Label(new Rect(x + 515f, y + 90f, 120f, 30f), $"{draftSensitivity:F2}배", valueStyle); // 현재 마우스 감도 표시
            draftSensitivity = GUI.HorizontalSlider(new Rect(x + 55f, y + 125f, 580f, 25f), draftSensitivity, 0.25f, 3f); // 마우스 감도 조절 슬라이더 표시

            GUI.Label(new Rect(x + 55f, y + 175f, 150f, 30f), "전체 음량", labelStyle); // Master 음량 항목 표시
            GUI.Label(new Rect(x + 235f, y + 175f, 80f, 30f), $"{draftVolume * 100f:F0}%", valueStyle); // 현재 Master 음량 표시
            draftVolume = GUI.HorizontalSlider(new Rect(x + 55f, y + 210f, 260f, 25f), draftVolume, 0f, 1f); // Master 음량 슬라이더 표시

            GUI.Label(new Rect(x + 385f, y + 175f, 150f, 30f), "배경음", labelStyle); // BGM 음량 항목 표시
            GUI.Label(new Rect(x + 555f, y + 175f, 80f, 30f), $"{draftBgmVolume * 100f:F0}%", valueStyle); // 현재 BGM 음량 표시
            draftBgmVolume = GUI.HorizontalSlider(new Rect(x + 385f, y + 210f, 250f, 25f), draftBgmVolume, 0f, 1f); // BGM 음량 슬라이더 표시

            GUI.Label(new Rect(x + 55f, y + 260f, 150f, 30f), "효과음", labelStyle); // SFX 음량 항목 표시
            GUI.Label(new Rect(x + 235f, y + 260f, 80f, 30f), $"{draftSfxVolume * 100f:F0}%", valueStyle); // 현재 SFX 음량 표시
            draftSfxVolume = GUI.HorizontalSlider(new Rect(x + 55f, y + 295f, 260f, 25f), draftSfxVolume, 0f, 1f); // SFX 음량 슬라이더 표시

            GUI.Label(new Rect(x + 385f, y + 260f, 150f, 30f), "환경음", labelStyle); // Ambient 음량 항목 표시
            GUI.Label(new Rect(x + 555f, y + 260f, 80f, 30f), $"{draftAmbientVolume * 100f:F0}%", valueStyle); // 현재 Ambient 음량 표시
            draftAmbientVolume = GUI.HorizontalSlider(new Rect(x + 385f, y + 295f, 250f, 25f), draftAmbientVolume, 0f, 1f); // Ambient 음량 슬라이더 표시

            GUI.Label(new Rect(x + 55f, y + 350f, 150f, 35f), "해상도", labelStyle); // 해상도 항목 표시

            if (GUI.Button(new Rect(x + 245f, y + 350f, 50f, 35f), "<")) // 이전 해상도 버튼 입력 확인
            {
                SelectPreviousResolution(); // 이전 해상도 선택
            }

            GUI.Label(new Rect(x + 315f, y + 350f, 180f, 35f), $"{selectedResolution.x} x {selectedResolution.y}", valueStyle); // 선택한 해상도 표시

            if (GUI.Button(new Rect(x + 515f, y + 350f, 50f, 35f), ">")) // 다음 해상도 버튼 입력 확인
            {
                SelectNextResolution(); // 다음 해상도 선택
            }

            draftFullScreen = GUI.Toggle(new Rect(x + 70f, y + 415f, 250f, 35f), draftFullScreen, " 전체 화면"); // 전체 화면 설정 토글 표시
            draftVSync = GUI.Toggle(new Rect(x + 390f, y + 415f, 250f, 35f), draftVSync, " 수직 동기화"); // 수직 동기화 설정 토글 표시

            GUI.Label(new Rect(x + 55f, y + 470f, width - 110f, 35f), "Master는 모든 소리를 조절하며 나머지 슬라이더는 종류별 음량을 조절합니다.", labelStyle); // AudioMixer 음량 안내 표시

            if (GUI.Button(new Rect(x + 55f, y + 555f, 180f, 50f), "적용 및 저장")) // 설정 적용 버튼 입력 확인
            {
                ApplyAndClose(); // 편집한 설정 저장과 적용
            }

            if (GUI.Button(new Rect(x + 260f, y + 555f, 180f, 50f), "기본값")) // 기본 설정 버튼 입력 확인
            {
                SetDraftDefaults(); // 편집값을 기본 설정으로 변경
            }

            if (GUI.Button(new Rect(x + 465f, y + 555f, 180f, 50f), "취소")) // 설정 취소 버튼 입력 확인
            {
                Close(); // 저장하지 않고 설정 화면 닫기
            }
        }
    }
}