using UnityEngine; // Unity 기본 기능과 AudioClip 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class SceneAudioController : MonoBehaviour // Scene 시작 시 배경음과 환경음 및 테스트 효과음 재생
    {
        [Header("Scene 오디오")] // Inspector Scene 오디오 설정 구분
        [SerializeField] AudioClip bgmClip; // 현재 Scene에서 재생할 배경음
        [SerializeField] AudioClip ambientClip; // 현재 Scene에서 재생할 전체 환경음
        [SerializeField] AudioClip startupSfxClip; // Scene 시작 시 한 번 재생할 테스트 효과음

        [Header("Scene 종료 설정")] // Inspector Scene 종료 설정 구분
        [SerializeField] bool stopBgmOnSceneExit = true; // 현재 Scene 종료 시 배경음 정지 여부
        [SerializeField] bool stopAmbientOnSceneExit = true; // 현재 Scene 종료 시 환경음 정지 여부

        void Start() // Scene 시작 시 연결된 AudioClip 재생
        {
            GameAudioManager audioManager = GameAudioManager.Instance; // 현재 전역 오디오 관리자 가져오기

            if (audioManager == null) // 오디오 관리자 존재 여부 확인
            {
                Debug.LogError("[SceneAudio] GameAudioManager가 없습니다."); // 오디오 관리자 누락 오류 출력
                return; // Scene 오디오 재생 중단
            }

            if (bgmClip != null) // BGM AudioClip 연결 여부 확인
            {
                audioManager.PlayBgm(bgmClip, true); // BGM 그룹으로 배경음 반복 재생
            }

            if (ambientClip != null) // Ambient AudioClip 연결 여부 확인
            {
                audioManager.PlayAmbient(ambientClip, true); // Ambient 그룹으로 환경음 반복 재생
            }

            if (startupSfxClip != null) // 테스트 SFX AudioClip 연결 여부 확인
            {
                audioManager.PlaySfx(startupSfxClip); // SFX 그룹으로 효과음 한 번 재생
            }
        }

        void OnDestroy() // 현재 Scene 종료 시 설정된 오디오 정지
        {
            GameAudioManager audioManager = GameAudioManager.Instance; // 현재 전역 오디오 관리자 가져오기

            if (audioManager == null) // 오디오 관리자 존재 여부 확인
            {
                return; // 오디오 정지 처리 중단
            }

            if (stopBgmOnSceneExit && bgmClip != null) // 현재 Scene의 BGM 정지 여부 확인
            {
                audioManager.StopBgm(); // 현재 재생 중인 배경음 정지
            }

            if (stopAmbientOnSceneExit && ambientClip != null) // 현재 Scene의 환경음 정지 여부 확인
            {
                audioManager.StopAmbient(); // 현재 재생 중인 환경음 정지
            }
        }
    }
}