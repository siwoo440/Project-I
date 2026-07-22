using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DungeonEntrance : MonoBehaviour, IHoldInteractable // 외부 Cube 입구의 F 길게 누르기 진입 처리
    {
        public enum EntranceType // 외부 입구 종류
        {
            Main = 0, // 고정 시작 방으로 연결되는 메인 입구
            Sub = 1 // 랜덤 안전 벽면으로 연결되는 서브 입구
        }

        [Header("입구 설정")] // 입구 Inspector 설정 구분
        [Tooltip("현재 Cube 입구 종류")] [SerializeField] EntranceType entranceType = EntranceType.Main; // 현재 Cube 입구 종류
        [Tooltip("내부 맵 생성과 도착 지점 제공자")] [SerializeField] DungeonGenerator dungeonGenerator; // 내부 맵 생성과 도착 지점 제공자
        [Tooltip("F키 필요 유지시간")] [SerializeField] float holdDuration = 1.5f; // F키 필요 유지시간

        public float HoldDuration => Mathf.Max(0.1f, holdDuration); // 안전한 필요 유지시간 반환

        void Awake() // 필수 참조 자동 보완
        {
            if (dungeonGenerator == null) // 생성기 Inspector 연결 여부 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // Scene의 생성기 자동 검색
            }
        }

        public string GetHoldPrompt(float progress) // 현재 입구 안내 문구 반환
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f); // 표시용 진행률 계산
            string entranceName = entranceType == EntranceType.Main ? "메인 입구" : "서브 입구"; // 입구 이름 선택
            return $"[F 길게] {entranceName} 열기  {percent}%"; // HUD 표시 문구 반환
        }

        public void CompleteHold(PlayerInteractor interactor) // F 길게 누르기 완료 후 내부 이동
        {
            if (interactor == null) // 상호작용 플레이어 존재 여부 확인
            {
                Debug.LogError("[DungeonEntrance] 상호작용한 플레이어가 없습니다."); // 플레이어 누락 오류
                return; // 진입 중단
            }

            if (dungeonGenerator == null) // 생성기 존재 여부 확인
            {
                Debug.LogError("[DungeonEntrance] DungeonGenerator가 연결되지 않았습니다."); // 생성기 누락 오류
                return; // 진입 중단
            }

            if (!dungeonGenerator.EnsureGenerated()) // 내부 던전 생성 완료 여부 확인
            {
                Debug.LogError("[DungeonEntrance] 내부 던전 생성에 실패했습니다."); // 생성 실패 오류
                return; // 진입 중단
            }

            Transform destination = entranceType == EntranceType.Main // 입구 종류 확인
                ? dungeonGenerator.GetMainEntranceSpawnPoint() // 메인 입구 도착 지점 선택
                : dungeonGenerator.SubEntranceSpawnPoint; // 서브 입구 도착 지점 선택

            if (destination == null) // 도착 지점 존재 여부 확인
            {
                Debug.LogError($"[DungeonEntrance] {entranceType} 입구 도착 지점이 없습니다."); // 도착 지점 누락 오류
                return; // 진입 중단
            }

            MovePlayer(interactor.transform, destination); // 플레이어 안전 순간이동
        }

        void MovePlayer(Transform playerTransform, Transform destination) // CharacterController를 고려한 플레이어 이동
        {
            CharacterController characterController = playerTransform.GetComponent<CharacterController>(); // 플레이어 CharacterController 검색
            Rigidbody playerRigidbody = playerTransform.GetComponent<Rigidbody>(); // 플레이어 Rigidbody 검색

            if (characterController != null) // CharacterController 존재 여부 확인
            {
                characterController.enabled = false; // 순간이동 중 충돌 비활성화
            }

            if (playerRigidbody != null && !playerRigidbody.isKinematic) // 동적 Rigidbody 존재 여부 확인
            {
                playerRigidbody.linearVelocity = Vector3.zero; // 이전 선형 속도 초기화
                playerRigidbody.angularVelocity = Vector3.zero; // 이전 회전 속도 초기화
            }

            playerTransform.SetPositionAndRotation(destination.position, destination.rotation); // 도착 위치와 회전 적용

            if (characterController != null) // CharacterController 재활성화 필요 여부 확인
            {
                characterController.enabled = true; // 플레이어 충돌 기능 복구
            }
        }
    }
}