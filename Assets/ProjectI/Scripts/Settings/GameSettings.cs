using System; // 이벤트
using System.Collections.Generic; // 해상도 목록
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Settings // 게임 설정 네임스페이스
{
    public static class GameSettings // 감도·음량·화면·음성 설정 (PlayerPrefs 저장)
    {
        private const string Prefix = "ProjectI.Settings."; // 저장 키 접두사
        public const float DefaultLookSensitivity = 1f; // 기본 감도 배율
        public const float MinLookSensitivity = 0.2f; // 최소 감도 배율
        public const float MaxLookSensitivity = 3f; // 최대 감도 배율
        private static bool loaded; // 불러옴 여부
        private static float lookSensitivity = DefaultLookSensitivity; // 감도 배율
        private static float masterVolume = 1f; // 전체 음량
        private static float musicVolume = 0.7f; // 음악
        private static float sfxVolume = 1f; // 효과음
        private static float ambienceVolume = 0.8f; // 환경음
        private static bool fullscreen = true; // 전체 화면
        private static bool vSync = true; // 수직 동기화
        private static int resolutionWidth; // 해상도 폭 (0 = 현재 화면)
        private static int resolutionHeight; // 해상도 높이
        private static bool voiceEnabled; // 43일차: 음성 채팅 (기본 꺼짐)
        private static bool pushToTalk = true; // 누르고 말하기 (V)
        private static float voiceVolume = 1f; // 음성 재생 음량
        private static float micGain = 1f; // 마이크 입력 음량
        private static float voiceThreshold = 0.02f; // 항상 켜기 감지 기준
        private static string micDevice = string.Empty; // 마이크 장치 (빈칸 = 기본)
        public const float MaxVoiceVolume = 2f; // 음성 음량 최대
        public const float MaxMicGain = 3f; // 마이크 입력 최대
        public const float MaxVoiceThreshold = 0.2f; // 감지 기준 최대

        public static event Action Changed; // 값 변경

        public static float LookSensitivity { get { EnsureLoaded(); return lookSensitivity; } set { EnsureLoaded(); lookSensitivity = Mathf.Clamp(value, MinLookSensitivity, MaxLookSensitivity); Changed?.Invoke(); } } // 마우스 감도 배율
        public static float MasterVolume { get { EnsureLoaded(); return masterVolume; } set { EnsureLoaded(); masterVolume = Mathf.Clamp01(value); ApplyAudio(); Changed?.Invoke(); } } // 전체 음량
        public static float MusicVolume { get { EnsureLoaded(); return musicVolume; } set { EnsureLoaded(); musicVolume = Mathf.Clamp01(value); Changed?.Invoke(); } } // 음악 음량
        public static float SfxVolume { get { EnsureLoaded(); return sfxVolume; } set { EnsureLoaded(); sfxVolume = Mathf.Clamp01(value); Changed?.Invoke(); } } // 효과음 음량
        public static float AmbienceVolume { get { EnsureLoaded(); return ambienceVolume; } set { EnsureLoaded(); ambienceVolume = Mathf.Clamp01(value); Changed?.Invoke(); } } // 환경음 음량
        public static bool VoiceEnabled { get { EnsureLoaded(); return voiceEnabled; } set { EnsureLoaded(); voiceEnabled = value; Changed?.Invoke(); } } // 43일차: 음성 채팅
        public static bool PushToTalk { get { EnsureLoaded(); return pushToTalk; } set { EnsureLoaded(); pushToTalk = value; Changed?.Invoke(); } } // 누르고 말하기
        public static float VoiceVolume { get { EnsureLoaded(); return voiceVolume; } set { EnsureLoaded(); voiceVolume = Mathf.Clamp(value, 0f, MaxVoiceVolume); Changed?.Invoke(); } } // 음성 재생 음량
        public static float MicGain { get { EnsureLoaded(); return micGain; } set { EnsureLoaded(); micGain = Mathf.Clamp(value, 0f, MaxMicGain); Changed?.Invoke(); } } // 마이크 입력 음량
        public static float VoiceThreshold { get { EnsureLoaded(); return voiceThreshold; } set { EnsureLoaded(); voiceThreshold = Mathf.Clamp(value, 0f, MaxVoiceThreshold); Changed?.Invoke(); } } // 항상 켜기 감지 기준
        public static string MicDevice { get { EnsureLoaded(); return micDevice; } set { EnsureLoaded(); micDevice = value ?? string.Empty; Changed?.Invoke(); } } // 마이크 장치
        public static bool Fullscreen { get { EnsureLoaded(); return fullscreen; } set { EnsureLoaded(); fullscreen = value; ApplyDisplay(); Changed?.Invoke(); } } // 전체 화면
        public static bool VSync { get { EnsureLoaded(); return vSync; } set { EnsureLoaded(); vSync = value; ApplyDisplay(); Changed?.Invoke(); } } // 수직 동기화
        public static Vector2Int Resolution // 해상도
        {
            get
            {
                EnsureLoaded(); // 불러오기
                return resolutionWidth > 0 && resolutionHeight > 0 ? new Vector2Int(resolutionWidth, resolutionHeight) : new Vector2Int(Screen.width, Screen.height); // 저장값 또는 현재
            }
            set
            {
                EnsureLoaded(); // 불러오기
                resolutionWidth = Mathf.Max(640, value.x); // 폭
                resolutionHeight = Mathf.Max(360, value.y); // 높이
                ApplyDisplay(); // 적용
                Changed?.Invoke(); // 알림
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] // 게임 시작 시
        private static void Initialize() // 불러와 적용
        {
            loaded = false; // 다시 읽기
            Changed = null; // 이전 실행 구독 정리
            EnsureLoaded(); // 불러오기
            ApplyAll(); // 적용
        }

        public static void Save() // 저장
        {
            EnsureLoaded(); // 불러오기
            PlayerPrefs.SetFloat(Prefix + "LookSensitivity", lookSensitivity); // 감도
            PlayerPrefs.SetFloat(Prefix + "MasterVolume", masterVolume); // 음량
            PlayerPrefs.SetFloat(Prefix + "MusicVolume", musicVolume); // 음악
            PlayerPrefs.SetFloat(Prefix + "SfxVolume", sfxVolume); // 효과음
            PlayerPrefs.SetFloat(Prefix + "AmbienceVolume", ambienceVolume); // 환경음
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0); // 전체 화면
            PlayerPrefs.SetInt(Prefix + "VSync", vSync ? 1 : 0); // 수직 동기화
            PlayerPrefs.SetInt(Prefix + "ResolutionWidth", resolutionWidth); // 폭
            PlayerPrefs.SetInt(Prefix + "ResolutionHeight", resolutionHeight); // 높이
            PlayerPrefs.SetInt(Prefix + "VoiceEnabled", voiceEnabled ? 1 : 0); // 음성
            PlayerPrefs.SetInt(Prefix + "PushToTalk", pushToTalk ? 1 : 0); // 말하기 방식
            PlayerPrefs.SetFloat(Prefix + "VoiceVolume", voiceVolume); // 음성 음량
            PlayerPrefs.SetFloat(Prefix + "MicGain", micGain); // 마이크 음량
            PlayerPrefs.SetFloat(Prefix + "VoiceThreshold", voiceThreshold); // 감지 기준
            PlayerPrefs.SetString(Prefix + "MicDevice", micDevice); // 장치
            PlayerPrefs.Save(); // 디스크 기록
        }

        public static void ResetToDefaults() // 기본값
        {
            EnsureLoaded(); // 불러오기
            lookSensitivity = DefaultLookSensitivity; // 감도
            masterVolume = 1f; // 음량
            musicVolume = 0.7f; // 음악
            sfxVolume = 1f; // 효과음
            ambienceVolume = 0.8f; // 환경음
            fullscreen = true; // 전체 화면
            vSync = true; // 수직 동기화
            resolutionWidth = 0; // 현재 화면
            resolutionHeight = 0; // 현재 화면
            pushToTalk = true; // 누르고 말하기 (음성 켜기 여부는 유지)
            voiceVolume = 1f; // 음성 음량
            micGain = 1f; // 마이크 음량
            voiceThreshold = 0.02f; // 감지 기준
            micDevice = string.Empty; // 기본 장치
            ApplyAll(); // 적용
            Changed?.Invoke(); // 알림
        }

        public static IReadOnlyList<Vector2Int> AvailableResolutions() // 고를 수 있는 해상도 (작은 것부터)
        {
            List<Vector2Int> result = new List<Vector2Int>(); // 결과

            foreach (UnityEngine.Resolution resolution in Screen.resolutions) // 모니터 지원 해상도
            {
                Vector2Int size = new Vector2Int(resolution.width, resolution.height); // 크기

                if (size.x >= 1024 && !result.Contains(size)) // 너무 작은 것·중복 제외
                {
                    result.Add(size); // 추가
                }
            }

            if (result.Count == 0) // 에디터 등 목록이 없을 때
            {
                result.AddRange(new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) }); // 기본 목록
            }

            result.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y)); // 작은 순
            return result; // 반환
        }

        private static void EnsureLoaded() // 한 번만 읽기
        {
            if (loaded) // 이미 읽음
            {
                return; // 생략
            }

            loaded = true; // 표시
            lookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "LookSensitivity", DefaultLookSensitivity), MinLookSensitivity, MaxLookSensitivity); // 감도
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f)); // 음량
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "MusicVolume", 0.7f)); // 음악
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "SfxVolume", 1f)); // 효과음
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "AmbienceVolume", 0.8f)); // 환경음
            fullscreen = PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) == 1; // 전체 화면
            vSync = PlayerPrefs.GetInt(Prefix + "VSync", 1) == 1; // 수직 동기화
            resolutionWidth = PlayerPrefs.GetInt(Prefix + "ResolutionWidth", 0); // 폭
            resolutionHeight = PlayerPrefs.GetInt(Prefix + "ResolutionHeight", 0); // 높이
            voiceEnabled = PlayerPrefs.GetInt(Prefix + "VoiceEnabled", 0) == 1; // 음성 (기본 꺼짐)
            pushToTalk = PlayerPrefs.GetInt(Prefix + "PushToTalk", 1) == 1; // 말하기 방식
            voiceVolume = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "VoiceVolume", 1f), 0f, MaxVoiceVolume); // 음성 음량
            micGain = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "MicGain", 1f), 0f, MaxMicGain); // 마이크 음량
            voiceThreshold = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "VoiceThreshold", 0.02f), 0f, MaxVoiceThreshold); // 감지 기준
            micDevice = PlayerPrefs.GetString(Prefix + "MicDevice", string.Empty); // 장치
        }

        private static void ApplyAll() // 모두 적용
        {
            ApplyAudio(); // 음량
            ApplyDisplay(); // 화면
        }

        private static void ApplyAudio() // 음량 적용
        {
            AudioListener.volume = masterVolume; // 전체 음량
        }

        private static void ApplyDisplay() // 화면 적용 (에디터 게임 창은 바꾸지 않음)
        {
            QualitySettings.vSyncCount = vSync ? 1 : 0; // 수직 동기화

            if (Application.isEditor) // 에디터
            {
                return; // 해상도·전체 화면 생략
            }

            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg.StartsWith("-coopAuto", StringComparison.OrdinalIgnoreCase))) // 44일차: 자동 협동 시험은 작은 창
            {
                Screen.SetResolution(960, 540, FullScreenMode.Windowed); // 창 모드
                return; // 저장된 해상도 무시
            }

            Vector2Int size = Resolution; // 해상도
            Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed); // 적용
        }
    }
}
