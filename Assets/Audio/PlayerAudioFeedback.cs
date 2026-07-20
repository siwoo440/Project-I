using UnityEngine; // Unity 기본 기능과 AudioClip 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(PlayerController))] // PlayerController가 반드시 존재하도록 지정
    [RequireComponent(typeof(CharacterController))] // 발소리 이동속도 확인용 CharacterController 지정
    public class PlayerAudioFeedback : MonoBehaviour // 플레이어 이동과 전투 및 피격 오디오 재생
    {
        [Header("발소리")] // Inspector 발소리 설정 구분
        [SerializeField] AudioClip[] footstepClips; // 무작위로 재생할 발소리 목록
        [SerializeField][Range(0f, 1f)] float footstepVolume = 0.65f; // 발소리 개별 음량
        [SerializeField] float walkStepInterval = 0.52f; // 걷는 동안 발소리 재생 간격
        [SerializeField] float runStepInterval = 0.32f; // 달리는 동안 발소리 재생 간격
        [SerializeField] float movementThreshold = 0.2f; // 발소리가 시작되는 최소 수평속도
        [SerializeField] float runSpeedThreshold = 5.5f; // 달리기로 판단할 수평속도

        [Header("플레이어 상태")] // Inspector 플레이어 상태 효과음 구분
        [SerializeField] AudioClip hurtClip; // 일반 피해를 받을 때 재생할 효과음
        [SerializeField] AudioClip deathClip; // 최종 사망 시 재생할 효과음
        [SerializeField][Range(0f, 1f)] float statusVolume = 0.8f; // 피격과 사망 효과음 음량

        [Header("무기 공격")] // Inspector 무기별 공격 효과음 구분
        [SerializeField] AudioClip unarmedSwingClip; // 맨손 공격 효과음
        [SerializeField] AudioClip swordSwingClip; // 검 공격 효과음
        [SerializeField] AudioClip axeSwingClip; // 도끼 공격 효과음
        [SerializeField] AudioClip bowShotClip; // 활 발사 효과음
        [SerializeField] AudioClip attackHitClip; // 몬스터 명중 확인 효과음
        [SerializeField][Range(0f, 1f)] float attackVolume = 0.8f; // 공격 효과음 음량

        [Header("상호작용")] // Inspector 상호작용 효과음 구분
        [SerializeField] AudioClip interactionClip; // 문과 레버 등 E 상호작용 공통 효과음
        [SerializeField][Range(0f, 1f)] float interactionVolume = 0.7f; // 상호작용 효과음 음량

        PlayerController playerController; // 피해와 사망 이벤트를 제공하는 플레이어
        CharacterController characterController; // 현재 이동속도와 접지 상태를 제공하는 컨트롤러
        PlayerCombat playerCombat; // 공격과 명중 이벤트를 제공하는 전투 컴포넌트
        PlayerInteractor playerInteractor; // E 상호작용 이벤트를 제공하는 컴포넌트

        float nextFootstepTime; // 다음 발소리를 재생할 수 있는 시각
        static bool missingAudioManagerLogged; // 오디오 관리자 누락 경고 중복 방지

        void Awake() // 플레이어 오디오에 필요한 컴포넌트 참조 초기화
        {
            playerController = GetComponent<PlayerController>(); // 같은 오브젝트의 PlayerController 가져오기
            characterController = GetComponent<CharacterController>(); // 같은 오브젝트의 CharacterController 가져오기
            playerCombat = GetComponent<PlayerCombat>(); // 같은 오브젝트의 PlayerCombat 가져오기
            playerInteractor = GetComponent<PlayerInteractor>(); // 같은 오브젝트의 PlayerInteractor 가져오기
        }

        void OnEnable() // 플레이어의 피해와 전투 및 상호작용 이벤트 구독
        {
            if (playerController != null) // PlayerController 존재 여부 확인
            {
                playerController.Damaged += HandleDamaged; // 플레이어 피격 효과음 이벤트 연결
                playerController.Died += HandleDied; // 플레이어 최종 사망 효과음 이벤트 연결
            }

            if (playerCombat != null) // PlayerCombat 존재 여부 확인
            {
                playerCombat.AttackPerformed += HandleAttackPerformed; // 공격 실행 효과음 이벤트 연결
                playerCombat.AttackHit += HandleAttackHit; // 공격 명중 효과음 이벤트 연결
            }

            if (playerInteractor != null) // PlayerInteractor 존재 여부 확인
            {
                playerInteractor.Interacted += HandleInteracted; // E 상호작용 효과음 이벤트 연결
            }
        }

        void OnDisable() // 플레이어 오디오 이벤트 구독 해제
        {
            if (playerController != null) // PlayerController 존재 여부 확인
            {
                playerController.Damaged -= HandleDamaged; // 플레이어 피격 효과음 이벤트 해제
                playerController.Died -= HandleDied; // 플레이어 사망 효과음 이벤트 해제
            }

            if (playerCombat != null) // PlayerCombat 존재 여부 확인
            {
                playerCombat.AttackPerformed -= HandleAttackPerformed; // 공격 실행 효과음 이벤트 해제
                playerCombat.AttackHit -= HandleAttackHit; // 공격 명중 효과음 이벤트 해제
            }

            if (playerInteractor != null) // PlayerInteractor 존재 여부 확인
            {
                playerInteractor.Interacted -= HandleInteracted; // 상호작용 효과음 이벤트 해제
            }
        }

        void Update() // 플레이어 이동 상태에 따른 발소리 처리
        {
            HandleFootsteps(); // 현재 이동속도와 접지 상태로 발소리 재생
        }

        void HandleFootsteps() // CharacterController 이동속도를 이용한 발소리 재생
        {
            if (Time.timeScale <= 0f || characterController == null) // 게임 정지와 컨트롤러 존재 여부 확인
            {
                return; // 발소리 처리 중단
            }

            if (!characterController.isGrounded) // 플레이어 접지 여부 확인
            {
                return; // 공중에서는 발소리 재생 중단
            }

            Vector3 horizontalVelocity = characterController.velocity; // 현재 플레이어 이동속도 가져오기
            horizontalVelocity.y = 0f; // 수직 이동속도를 발소리 계산에서 제외
            float speed = horizontalVelocity.magnitude; // 현재 수평 이동속도 계산

            if (speed < movementThreshold) // 실제 이동 중인지 확인
            {
                nextFootstepTime = Time.time; // 다음 이동 시 발소리를 즉시 재생할 수 있도록 시각 갱신
                return; // 정지 상태에서 발소리 재생 중단
            }

            if (Time.time < nextFootstepTime) // 다음 발소리 재생 시각 도달 여부 확인
            {
                return; // 발소리 재생 대기
            }

            bool running = speed >= runSpeedThreshold; // 현재 속도가 달리기 기준 이상인지 확인
            float interval = running ? runStepInterval : walkStepInterval; // 걷기와 달리기에 맞는 발소리 간격 결정

            PlayRandomLocalSfx(footstepClips, footstepVolume); // 등록된 발소리 중 하나를 무작위 재생
            nextFootstepTime = Time.time + Mathf.Max(0.05f, interval); // 다음 발소리 재생 시각 설정
        }

        void HandleDamaged(float amount, bool fatal) // 일반 피해를 받을 때 피격 효과음 재생
        {
            if (fatal) // 즉사 피해인지 확인
            {
                return; // 사망 효과음과 중복되지 않도록 피격 효과음 생략
            }

            PlayLocalSfx(hurtClip, statusVolume); // 플레이어 피격 효과음 재생
        }

        void HandleDied() // 부활하지 못한 최종 사망 효과음 재생
        {
            PlayLocalSfx(deathClip, statusVolume); // 플레이어 최종 사망 효과음 재생
        }

        void HandleAttackPerformed(WeaponType? weaponType) // 무기 종류에 맞는 공격 효과음 재생
        {
            if (!weaponType.HasValue) // 무기 데이터가 없는 맨손 공격인지 확인
            {
                PlayLocalSfx(unarmedSwingClip, attackVolume); // 맨손 공격 효과음 재생
                return; // 무기 종류 검사 중단
            }

            switch (weaponType.Value) // 현재 무기 종류 확인
            {
                case WeaponType.Sword: // 검 공격인지 확인
                    PlayLocalSfx(swordSwingClip, attackVolume); // 검 공격 효과음 재생
                    break; // 검 처리 종료

                case WeaponType.Axe: // 도끼 공격인지 확인
                    PlayLocalSfx(axeSwingClip, attackVolume); // 도끼 공격 효과음 재생
                    break; // 도끼 처리 종료

                case WeaponType.Bow: // 활 공격인지 확인
                    PlayLocalSfx(bowShotClip, attackVolume); // 활 발사 효과음 재생
                    break; // 활 처리 종료
            }
        }

        void HandleAttackHit() // 공격이 몬스터에게 명중했을 때 효과음 재생
        {
            PlayLocalSfx(attackHitClip, attackVolume); // 몬스터 명중 확인 효과음 재생
        }

        void HandleInteracted() // 플레이어가 E 상호작용을 실행했을 때 효과음 재생
        {
            PlayLocalSfx(interactionClip, interactionVolume); // 공통 상호작용 효과음 재생
        }

        void PlayRandomLocalSfx(AudioClip[] clips, float volume) // AudioClip 배열에서 무작위 효과음 재생
        {
            if (clips == null || clips.Length == 0) // 발소리 AudioClip 목록 존재 여부 확인
            {
                return; // 무작위 효과음 재생 중단
            }

            int randomIndex = Random.Range(0, clips.Length); // 재생할 AudioClip 번호 무작위 선택
            PlayLocalSfx(clips[randomIndex], volume); // 선택한 효과음 재생
        }

        void PlayLocalSfx(AudioClip clip, float volume) // GameAudioManager의 2D SFXSource로 효과음 재생
        {
            if (clip == null) // AudioClip 연결 여부 확인
            {
                return; // 비어 있는 효과음 재생 중단
            }

            GameAudioManager audioManager = GameAudioManager.Instance; // 전역 GameAudioManager 가져오기

            if (audioManager == null) // GameAudioManager 존재 여부 확인
            {
                if (!missingAudioManagerLogged) // 누락 오류가 아직 출력되지 않았는지 확인
                {
                    Debug.LogWarning("[PlayerAudio] GameAudioManager가 없습니다."); // 오디오 관리자 누락 경고 출력
                    missingAudioManagerLogged = true; // 경고 출력 완료 상태 저장
                }

                return; // 효과음 재생 중단
            }

            audioManager.PlaySfx(clip, volume); // AudioMixer의 SFX 그룹으로 효과음 재생
        }
    }
}