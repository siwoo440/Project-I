using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class ThreatFeedback : MonoBehaviour // 몬스터와 기믹의 공통 소리 및 파티클 피드백
    {
        [Header("효과음")] // Inspector 효과음 설정 구분
        [SerializeField] AudioClip warningClip; // 위험 행동 시작 경고음
        [SerializeField] AudioClip appearClip; // 고스트 출현 효과음
        [SerializeField] AudioClip attackClip; // 공격 실행 효과음
        [SerializeField] AudioClip hitClip; // 피격 효과음

        [Header("파티클")] // Inspector 파티클 설정 구분
        [SerializeField] ParticleSystem warningEffectPrefab; // 위험 경고 파티클 프리팹
        [SerializeField] ParticleSystem appearEffectPrefab; // 출현 파티클 프리팹
        [SerializeField] ParticleSystem attackEffectPrefab; // 공격 파티클 프리팹
        [SerializeField] ParticleSystem hitEffectPrefab; // 피격 파티클 프리팹

        [Header("3D 사운드")] // Inspector 3D 사운드 설정 구분
        [SerializeField][Range(0f, 1f)] float volume = 0.7f; // 전체 효과음 음량
        [SerializeField] float maxDistance = 20f; // 효과음 최대 도달 거리
        [SerializeField] bool useFallbackTones = true; // 효과음 누락 시 임시 전자음 사용 여부

        [Header("임시 전자음")] // Inspector 임시 전자음 설정 구분
        [SerializeField] float warningTone = 180f; // 경고용 낮은 전자음 주파수
        [SerializeField] float appearTone = 340f; // 출현용 전자음 주파수
        [SerializeField] float attackTone = 100f; // 공격용 낮은 전자음 주파수
        [SerializeField] float hitTone = 520f; // 피격용 높은 전자음 주파수

        MonsterAI monsterAI; // 자동 피격 이벤트 연결용 MonsterAI

        void Awake() // MonsterAI 참조 초기화
        {
            monsterAI = GetComponent<MonsterAI>(); // 같은 오브젝트의 MonsterAI 검색
        }

        void OnEnable() // 몬스터 피격 이벤트 구독
        {
            if (monsterAI != null) // MonsterAI 존재 여부 확인
            {
                monsterAI.Damaged += PlayHit; // 몬스터 피격 시 피드백 연결
            }
        }

        void OnDisable() // 몬스터 피격 이벤트 구독 해제
        {
            if (monsterAI != null) // MonsterAI 존재 여부 확인
            {
                monsterAI.Damaged -= PlayHit; // 몬스터 피격 피드백 연결 해제
            }
        }

        public void PlayWarning() // 현재 오브젝트 위치에서 위험 경고 재생
        {
            PlayWarningAt(transform.position); // 현재 위치를 경고 발생 위치로 사용
        }

        public void PlayWarningAt(Vector3 position) // 지정된 위치에서 위험 경고 재생
        {
            PlayFeedback( // 경고 소리와 파티클 재생
                warningClip, // 경고 효과음
                warningTone, // 경고 임시 전자음
                0.45f, // 경고 전자음 길이
                warningEffectPrefab, // 경고 파티클
                position); // 경고 발생 위치
        }

        public void PlayAppear() // 현재 위치에서 출현 피드백 재생
        {
            PlayFeedback( // 출현 소리와 파티클 재생
                appearClip, // 출현 효과음
                appearTone, // 출현 임시 전자음
                0.3f, // 출현 전자음 길이
                appearEffectPrefab, // 출현 파티클
                transform.position); // 현재 오브젝트 위치
        }

        public void PlayAttack() // 현재 위치에서 공격 피드백 재생
        {
            PlayFeedback( // 공격 소리와 파티클 재생
                attackClip, // 공격 효과음
                attackTone, // 공격 임시 전자음
                0.2f, // 공격 전자음 길이
                attackEffectPrefab, // 공격 파티클
                transform.position + Vector3.up); // 오브젝트 상체 위치
        }

        public void PlayHit() // 현재 위치에서 피격 피드백 재생
        {
            PlayFeedback( // 피격 소리와 파티클 재생
                hitClip, // 피격 효과음
                hitTone, // 피격 임시 전자음
                0.12f, // 피격 전자음 길이
                hitEffectPrefab, // 피격 파티클
                transform.position + Vector3.up); // 오브젝트 상체 위치
        }

        void PlayFeedback( // 지정된 소리와 파티클 피드백 재생
            AudioClip clip, // 우선 사용할 실제 효과음
            float fallbackFrequency, // 효과음 누락 시 사용할 주파수
            float fallbackDuration, // 임시 전자음 재생시간
            ParticleSystem effectPrefab, // 생성할 파티클 프리팹
            Vector3 position) // 피드백 발생 위치
        {
            AudioManager audioManager = AudioManager.Instance; // 전역 AudioManager 가져오기

            if (audioManager == null) // 전역 AudioManager 존재 여부 확인
            {
                audioManager = FindFirstObjectByType<AudioManager>(); // Scene에서 AudioManager 다시 검색
            }

            if (audioManager != null) // AudioManager 검색 성공 여부 확인
            {
                if (clip != null) // 실제 효과음 연결 여부 확인
                {
                    audioManager.Play3D(clip, position, volume, 1f, maxDistance); // 실제 3D 효과음 재생
                }
                else if (useFallbackTones) // 임시 전자음 사용 여부 확인
                {
                    audioManager.PlayTone3D(fallbackFrequency, fallbackDuration, position, volume * 0.5f, maxDistance); // 임시 3D 전자음 재생
                }
            }

            SpawnEffect(effectPrefab, position); // 지정된 위치에 파티클 생성
        }

        void SpawnEffect(ParticleSystem effectPrefab, Vector3 position) // 임시 파티클 생성과 자동 제거
        {
            if (effectPrefab == null) // 파티클 프리팹 연결 여부 확인
            {
                return; // 파티클 생성 중단
            }

            ParticleSystem createdEffect = Instantiate( // 파티클 프리팹 생성
                effectPrefab, // 생성할 파티클 프리팹
                position, // 파티클 발생 위치
                Quaternion.identity); // 월드 기본 회전 적용

            createdEffect.Play(); // 파티클 재생 시작

            ParticleSystem.MainModule mainModule = createdEffect.main; // 파티클 기본 설정 가져오기
            float removeDelay = mainModule.duration + mainModule.startLifetime.constantMax + 0.2f; // 전체 파티클 종료시간 계산
            Destroy(createdEffect.gameObject, Mathf.Max(0.2f, removeDelay)); // 파티클 종료 후 오브젝트 제거
        }
    }
}