using System; // 날짜와 예외 처리 기능 사용
using System.IO; // JSON 저장 파일 읽기와 쓰기 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.SceneManagement; // 현재 Scene 확인 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class CampaignSaveManager : MonoBehaviour // 캠페인 저장 파일 생성과 복원 관리
    {
        public static CampaignSaveManager Instance { get; private set; } // 현재 저장 관리자 접근점

        const int CurrentSaveVersion = 1; // 현재 지원하는 저장 파일 버전

        [Header("저장 설정")] // Inspector 저장 설정 구분
        [SerializeField] string villageSceneName = "Village"; // 저장을 허용할 마을 Scene 이름
        [SerializeField] string saveFolderName = "ProjectI"; // 저장 파일을 보관할 하위 폴더 이름
        [SerializeField] string saveFileName = "campaign_save.json"; // 캠페인 저장 파일 이름

        string lastMessage = "저장 기록을 확인하지 않았습니다."; // 최근 저장 처리 결과 안내

        public string LastMessage => lastMessage; // 최근 저장 처리 결과 반환
        public bool HasSaveFile => File.Exists(SavePath); // 저장 파일 존재 여부 반환
        public string SavePath => Path.Combine(Application.persistentDataPath, saveFolderName, saveFileName); // 실제 저장 파일 경로 반환

        void Awake() // 저장 관리자 싱글톤 설정
        {
            if (Instance != null && Instance != this) // 기존 저장 관리자 존재 여부 확인
            {
                Destroy(gameObject); // Scene 전환으로 생성된 중복 관리자 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 저장 관리자로 등록
            transform.SetParent(null); // 영구 오브젝트 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 저장 관리자 유지
        }

        void OnDestroy() // 저장 관리자 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 저장 관리자인지 확인
            {
                Instance = null; // 전역 저장 관리자 참조 초기화
            }
        }

        public bool SaveGame() // 현재 마을 캠페인 상태를 JSON 파일로 저장
        {
            if (SceneManager.GetActiveScene().name != villageSceneName) // 현재 Scene이 마을인지 확인
            {
                SetMessage("저장은 마을에서만 할 수 있습니다.", true); // 저장 제한 원인 출력
                return false; // 저장 실패 반환
            }

            CampaignManager campaignManager = CampaignManager.Instance; // 현재 캠페인 관리자 가져오기
            VillageShopManager shopManager = VillageShopManager.Instance; // 현재 상점 관리자 가져오기

            if (campaignManager == null) // 캠페인 관리자 존재 여부 확인
            {
                SetMessage("CampaignManager를 찾을 수 없습니다.", true); // 누락된 관리자 오류 출력
                return false; // 저장 실패 반환
            }

            if (shopManager == null) // 상점 관리자 존재 여부 확인
            {
                SetMessage("VillageShopManager를 찾을 수 없습니다.", true); // 누락된 관리자 오류 출력
                return false; // 구매품 손실을 방지하기 위해 저장 중단
            }

            if (campaignManager.HasOpenSettlement) // 빚 납부 선택이 진행 중인지 확인
            {
                SetMessage("빚 납부를 확정한 후 저장할 수 있습니다.", false); // 저장 제한 안내 출력
                return false; // 불완전한 정산 상태 저장 방지
            }

            CampaignStateData state = campaignManager.State; // 현재 캠페인 상태 가져오기
            CampaignSaveData saveData = new CampaignSaveData(); // 새로운 저장 데이터 생성

            saveData.saveVersion = CurrentSaveVersion; // 현재 저장 파일 버전 기록
            saveData.currentDay = state.CurrentDay; // 현재 날짜 기록
            saveData.deadlineDay = state.DeadlineDay; // 마감 날짜 기록
            saveData.gold = state.Gold; // 현재 골드 기록
            saveData.remainingDebt = state.RemainingDebt; // 남은 빚 기록
            saveData.completedRuns = state.CompletedRuns; // 완료 탐험 횟수 기록
            saveData.campaignWon = state.CampaignWon; // 캠페인 성공 여부 기록
            saveData.campaignFailed = state.CampaignFailed; // 캠페인 실패 여부 기록
            saveData.pendingShopItemKeys = shopManager.GetPendingItemKeys(); // 구매 대기 아이템 기록
            saveData.remainingShopStocks = shopManager.GetRemainingStocks(); // 현재 상점 재고 기록
            saveData.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // 현재 저장 시간 기록

            try // 저장 파일 생성 시도
            {
                string folderPath = Path.GetDirectoryName(SavePath); // 저장 폴더 경로 계산
                Directory.CreateDirectory(folderPath); // 저장 폴더가 없으면 생성

                string json = JsonUtility.ToJson(saveData, true); // 저장 데이터를 보기 좋은 JSON 문자열로 변환
                string temporaryPath = SavePath + ".tmp"; // 저장 중 사용할 임시 파일 경로 계산

                File.WriteAllText(temporaryPath, json); // 임시 파일에 JSON 데이터 기록

                if (File.Exists(SavePath)) // 기존 저장 파일 존재 여부 확인
                {
                    File.Delete(SavePath); // 기존 저장 파일 제거
                }

                File.Move(temporaryPath, SavePath); // 완성된 임시 파일을 실제 저장 파일로 이동
                SetMessage($"저장 완료: {saveData.savedAt}", false); // 저장 성공 안내 출력
                return true; // 저장 성공 반환
            }
            catch (Exception exception) // 파일 저장 예외 처리
            {
                SetMessage($"저장 실패: {exception.Message}", true); // 저장 실패 원인 출력
                return false; // 저장 실패 반환
            }
        }

        public bool LoadGame() // JSON 파일에서 캠페인과 상점 상태 복원
        {
            if (SceneManager.GetActiveScene().name != villageSceneName) // 현재 Scene이 마을인지 확인
            {
                SetMessage("불러오기는 마을에서만 할 수 있습니다.", true); // 불러오기 제한 원인 출력
                return false; // 불러오기 실패 반환
            }

            if (!File.Exists(SavePath)) // 저장 파일 존재 여부 확인
            {
                SetMessage("불러올 저장 파일이 없습니다.", false); // 저장 파일 없음 안내 출력
                return false; // 불러오기 실패 반환
            }

            CampaignManager campaignManager = CampaignManager.Instance; // 현재 캠페인 관리자 가져오기
            VillageShopManager shopManager = VillageShopManager.Instance; // 현재 상점 관리자 가져오기

            if (campaignManager == null || shopManager == null) // 필수 관리자 존재 여부 확인
            {
                SetMessage("CampaignManager 또는 VillageShopManager가 없습니다.", true); // 관리자 누락 오류 출력
                return false; // 불러오기 실패 반환
            }

            try // 저장 파일 읽기 시도
            {
                string json = File.ReadAllText(SavePath); // 저장 파일의 JSON 문자열 읽기
                CampaignSaveData saveData = JsonUtility.FromJson<CampaignSaveData>(json); // JSON 문자열을 저장 데이터로 변환

                if (saveData == null) // 변환된 저장 데이터 존재 여부 확인
                {
                    SetMessage("저장 파일 내용을 읽을 수 없습니다.", true); // 손상된 저장 파일 안내 출력
                    return false; // 불러오기 실패 반환
                }

                if (saveData.saveVersion != CurrentSaveVersion) // 저장 버전 호환 여부 확인
                {
                    SetMessage($"지원하지 않는 저장 버전입니다: {saveData.saveVersion}", true); // 버전 오류 출력
                    return false; // 불러오기 실패 반환
                }

                campaignManager.ApplySavedState(saveData); // 캠페인 날짜와 경제 상태 복원
                shopManager.RestoreSavedShopState(saveData.pendingShopItemKeys, saveData.remainingShopStocks); // 구매품과 재고 상태 복원

                SetMessage($"불러오기 완료: {saveData.savedAt}", false); // 불러오기 성공 안내 출력
                return true; // 불러오기 성공 반환
            }
            catch (Exception exception) // 파일 읽기 또는 변환 예외 처리
            {
                SetMessage($"불러오기 실패: {exception.Message}", true); // 불러오기 실패 원인 출력
                return false; // 불러오기 실패 반환
            }
        }

        public bool DeleteSave() // 현재 캠페인 저장 파일 삭제
        {
            try // 저장 파일 삭제 시도
            {
                if (!File.Exists(SavePath)) // 저장 파일 존재 여부 확인
                {
                    SetMessage("삭제할 저장 파일이 없습니다.", false); // 파일 없음 안내 출력
                    return false; // 삭제 실패 반환
                }

                File.Delete(SavePath); // 실제 캠페인 저장 파일 삭제

                string temporaryPath = SavePath + ".tmp"; // 임시 저장 파일 경로 계산

                if (File.Exists(temporaryPath)) // 임시 파일 존재 여부 확인
                {
                    File.Delete(temporaryPath); // 남아 있는 임시 저장 파일 삭제
                }

                SetMessage("캠페인 저장 파일을 삭제했습니다.", false); // 삭제 완료 안내 출력
                return true; // 삭제 성공 반환
            }
            catch (Exception exception) // 저장 파일 삭제 예외 처리
            {
                SetMessage($"저장 파일 삭제 실패: {exception.Message}", true); // 삭제 실패 원인 출력
                return false; // 삭제 실패 반환
            }
        }

        public string GetSaveSummary() // 저장 장부에 표시할 저장 파일 요약 반환
        {
            if (!File.Exists(SavePath)) // 저장 파일 존재 여부 확인
            {
                return "저장 기록 없음"; // 저장 파일 없음 문구 반환
            }

            try // 저장 파일 요약 읽기 시도
            {
                string json = File.ReadAllText(SavePath); // 저장 JSON 문자열 읽기
                CampaignSaveData saveData = JsonUtility.FromJson<CampaignSaveData>(json); // 저장 데이터로 변환

                if (saveData == null) // 변환 결과 존재 여부 확인
                {
                    return "저장 기록을 읽을 수 없음"; // 손상된 데이터 문구 반환
                }

                return $"{saveData.currentDay}일차 / {saveData.gold}골드 / 빚 {saveData.remainingDebt}골드\n저장 시간: {saveData.savedAt}"; // 저장 상태 요약 반환
            }
            catch // 요약 읽기 예외 처리
            {
                return "저장 기록을 읽을 수 없음"; // 읽기 실패 문구 반환
            }
        }

        void SetMessage(string message, bool error) // 최근 결과 저장과 Console 출력 처리
        {
            lastMessage = message; // 저장 장부에 표시할 최근 결과 저장

            if (error) // 오류 메시지 여부 확인
            {
                Debug.LogError($"[CampaignSave] {message}"); // 오류 결과 Console 출력
            }
            else // 정상 또는 안내 메시지인 경우
            {
                Debug.Log($"[CampaignSave] {message}"); // 일반 결과 Console 출력
            }
        }
    }
}