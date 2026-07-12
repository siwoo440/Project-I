using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectI
{
    /// <summary>
    /// 전체 게임 흐름 제어 및 씬 전환 허브. (기획서 PART 13.3.1)
    /// 씬을 넘나들어도 유지되는 싱글톤.
    /// TODO: 게임 상태(State) 관리, 일차 진행, 페이즈(빚) 연동 등.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[GameManager] 초기화 완료");
        }

        /// <summary>씬 이름으로 전환. (Build Profiles에 등록된 씬만 가능)</summary>
        public void LoadScene(string sceneName)
        {
            Debug.Log($"[GameManager] 씬 로드 → {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
}
