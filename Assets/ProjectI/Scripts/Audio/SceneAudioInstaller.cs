using ProjectI.Player; // 발소리 대상
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 로드

namespace ProjectI.Audio // 소리 네임스페이스
{
    public static class SceneAudioInstaller // 씬이 열릴 때 음악·환경음·발소리를 자동으로 붙임 (씬 파일 수정 없음)
    {
        private const string AudioRootName = "__SceneAudio"; // 씬별 소리 묶음
        private const string MainMenuScene = "MainMenu"; // 메인 메뉴
        private const string OfficeScene = "01_Office"; // 거점 마을
        private const string SmokestackName = "Smokestack"; // 공장 굴뚝 오브젝트 이름 (마을 제작 도구)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 게임 시작 시
        private static void Install() // 등록
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 중복 방지
            SceneManager.sceneLoaded += OnSceneLoaded; // 이후 씬

            for (int index = 0; index < SceneManager.sceneCount; index++) // 이미 열린 씬
            {
                Apply(SceneManager.GetSceneAt(index)); // 적용
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) // 씬 로드
        {
            Apply(scene); // 적용
        }

        public static void Apply(Scene scene) // 씬 하나에 적용
        {
            if (!scene.IsValid() || !scene.isLoaded) // 준비 안 됨
            {
                return; // 생략
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // 발소리 (플레이어)
            {
                foreach (PlayerMovement movement in root.GetComponentsInChildren<PlayerMovement>(true)) // 플레이어
                {
                    if (movement.GetComponent<FootstepAudio>() == null) // 없음
                    {
                        movement.gameObject.AddComponent<FootstepAudio>(); // 추가
                    }
                }
            }

            if (scene.name == MainMenuScene) // 메인 메뉴
            {
                GameObject audioRoot = CreateRoot(scene); // 묶음
                AddLoop(audioRoot.transform, "Music", SoundId.MenuMusic, SoundCategory.Music, 0.55f, false, Vector3.zero); // 음악
                AddLoop(audioRoot.transform, "Wind", SoundId.WindLoop, SoundCategory.Ambience, 0.3f, false, Vector3.zero); // 바람
                AddSmokestackHum(scene, audioRoot.transform); // 굴뚝
            }
            else if (scene.name == OfficeScene) // 마을
            {
                GameObject audioRoot = CreateRoot(scene); // 묶음
                AddLoop(audioRoot.transform, "Wind", SoundId.WindLoop, SoundCategory.Ambience, 0.35f, false, Vector3.zero); // 바람
                AddSmokestackHum(scene, audioRoot.transform); // 굴뚝
            }
            else if (scene.name.Contains("Dungeon")) // 던전 계열
            {
                GameObject audioRoot = CreateRoot(scene); // 묶음
                AddLoop(audioRoot.transform, "Drone", SoundId.DungeonDrone, SoundCategory.Ambience, 0.45f, false, Vector3.zero); // 지하 울림
            }
        }

        private static GameObject CreateRoot(Scene scene) // 씬별 묶음 (다시 로드되면 새로)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 이미 있음
            {
                if (root.name == AudioRootName) // 같은 이름
                {
                    Object.Destroy(root); // 제거
                }
            }

            GameObject created = new GameObject(AudioRootName); // 생성
            SceneManager.MoveGameObjectToScene(created, scene); // 씬 소속 (씬과 함께 사라짐)
            return created; // 반환
        }

        private static void AddSmokestackHum(Scene scene, Transform audioRoot) // 공장 굴뚝마다 기계음
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 루트
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 전체
                {
                    if (child.name == SmokestackName) // 굴뚝
                    {
                        AddLoop(audioRoot, "FactoryHum", SoundId.FactoryHum, SoundCategory.Ambience, 0.5f, true, child.position + (Vector3.up * 2f)); // 기계음
                    }
                }
            }
        }

        private static void AddLoop(Transform parent, string name, SoundId sound, SoundCategory category, float volume, bool spatial, Vector3 position) // 반복 소리 하나
        {
            GameObject loop = new GameObject(name); // 오브젝트
            loop.transform.SetParent(parent, false); // 부모
            loop.transform.position = position; // 위치
            loop.AddComponent<AmbientLoop>().Configure(sound, category, volume, spatial, 70f); // 설정
        }
    }
}
