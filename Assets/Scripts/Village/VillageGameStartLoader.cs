using System.Collections; // 관리자 생성 대기 코루틴 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageGameStartLoader : MonoBehaviour // 메인 메뉴 시작 요청을 Village에서 처리
    {
        [Header("초기화 설정")] // Inspector 초기화 설정 구분
        [SerializeField][Min(1)] int managerWaitFrames = 120; // 필수 관리자를 기다릴 최대 프레임 수

        IEnumerator Start() // Village 관리자 생성을 기다린 뒤 시작 요청 처리
        {
            GameStartMode startMode = GameStartRequest.Consume(); // 메인 메뉴에서 전달한 시작 방식 가져오기

            if (startMode == GameStartMode.None) // MainMenu를 거치지 않은 직접 실행인지 확인
            {
                Debug.Log("[VillageStart] 직접 Village Scene을 실행하여 캠페인 시작 처리를 건너뜁니다."); // 개발 테스트 상태 출력
                yield break; // 새 게임과 이어하기 처리 중단
            }

            for (int frame = 0; frame < managerWaitFrames; frame++) // 지정된 프레임 동안 필수 관리자 검색
            {
                bool hasCampaignManager = CampaignManager.Instance != null; // 캠페인 관리자 존재 여부 확인
                bool hasShopManager = VillageShopManager.Instance != null; // 상점 관리자 존재 여부 확인
                bool hasSaveManager = CampaignSaveManager.Instance != null; // 저장 관리자 존재 여부 확인

                if (hasCampaignManager && hasShopManager && hasSaveManager) // 모든 필수 관리자 준비 여부 확인
                {
                    break; // 관리자 대기 종료
                }

                yield return null; // 다음 프레임까지 대기
            }

            CampaignManager campaignManager = CampaignManager.Instance; // 준비된 캠페인 관리자 가져오기
            VillageShopManager shopManager = VillageShopManager.Instance; // 준비된 상점 관리자 가져오기
            CampaignSaveManager saveManager = CampaignSaveManager.Instance; // 준비된 저장 관리자 가져오기

            if (campaignManager == null || shopManager == null || saveManager == null) // 필수 관리자 누락 여부 확인
            {
                Debug.LogError("[VillageStart] CampaignManager, VillageShopManager 또는 CampaignSaveManager가 없습니다."); // 시작 처리 실패 원인 출력
                yield break; // 게임 시작 처리 중단
            }

            if (startMode == GameStartMode.NewGame) // 새 게임 시작 요청인지 확인
            {
                StartNewCampaign(campaignManager, shopManager, saveManager); // 새 캠페인 초기화 실행
                yield break; // 이어하기 검사 중단
            }

            if (startMode == GameStartMode.Continue) // 이어하기 요청인지 확인
            {
                ContinueCampaign(saveManager); // 기존 캠페인 저장 기록 복원
            }
        }

        void StartNewCampaign(CampaignManager campaignManager, VillageShopManager shopManager, CampaignSaveManager saveManager) // 기존 기록을 지우고 새 캠페인 시작
        {
            if (saveManager.HasSaveFile) // 기존 저장 파일 존재 여부 확인
            {
                saveManager.DeleteSave(); // 기존 캠페인 저장 파일 삭제
            }

            campaignManager.ResetCampaign(); // 날짜와 골드 및 빚을 시작값으로 초기화
            shopManager.ResetForNewCampaign(); // 구매 대기 목록과 상점 재고 초기화

            if (saveManager.SaveGame()) // 초기 캠페인 상태 저장 성공 여부 확인
            {
                Debug.Log("[VillageStart] 새 캠페인을 시작하고 최초 저장을 완료했습니다."); // 새 게임 시작 성공 출력
            }
            else // 최초 저장에 실패한 경우
            {
                Debug.LogError("[VillageStart] 새 캠페인은 시작했지만 최초 저장에 실패했습니다."); // 초기 저장 실패 출력
            }
        }

        void ContinueCampaign(CampaignSaveManager saveManager) // 기존 캠페인 저장 기록 불러오기
        {
            if (saveManager.LoadGame()) // 캠페인 불러오기 성공 여부 확인
            {
                Debug.Log("[VillageStart] 저장된 캠페인을 이어서 시작했습니다."); // 이어하기 성공 출력
            }
            else // 저장 기록 불러오기에 실패한 경우
            {
                Debug.LogError("[VillageStart] 저장된 캠페인을 불러오지 못했습니다."); // 이어하기 실패 출력
            }
        }
    }
}