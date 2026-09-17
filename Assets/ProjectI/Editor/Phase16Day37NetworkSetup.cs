using System.IO; // 폴더
using System.Reflection; // 넷코드 고유 번호 생성
using System.Text; // 보고
using ProjectI.EditorTools.Town; // 3D 글자 (깊이 재질)
using ProjectI.Net; // 협동 컴포넌트
using Unity.Netcode; // 넷코드
using Unity.Netcode.Transports.UTP; // 직접 IP 연결
using UnityEditor; // 에디터 기능
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase16Day37NetworkSetup // 협동 네트워크 프리팹 구성 (37일차)
    {
        private const string Folder = "Assets/ProjectI/Resources/Net"; // Resources 폴더 (실행 중 불러옴)
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day37"; // 몸체 재질
        private const string AvatarPath = Folder + "/NetPlayerAvatar.prefab"; // 원정대원 몸체
        private const string WorldStatePath = Folder + "/NetWorldState.prefab"; // 월드 상태
        private const string PrefabListPath = Folder + "/ProjectINetworkPrefabs.asset"; // 네트워크 프리팹 목록
        private const string ManagerPath = Folder + "/ProjectINetwork.prefab"; // 네트워크 관리자

        [MenuItem("Project I/Day 38/Rebuild Network Prefabs (Item Sync)")] // 38일차: 아이템 동기화 포함 다시 만들기
        public static void RebuildForDay38() // 같은 구성 (월드 상태에 아이템 동기화 포함)
        {
            Build(); // 구성
        }

        [MenuItem("Project I/Day 37/Build Network Prefabs")] // 메뉴
        public static void Build() // 구성
        {
            EnsureFolder(Folder); // 폴더
            EnsureFolder(MaterialFolder); // 폴더
            GameObject avatar = BuildAvatar(); // 몸체
            GameObject worldState = BuildWorldState(); // 월드 상태
            NetworkPrefabsList list = BuildPrefabList(avatar, worldState); // 목록
            GameObject manager = BuildManager(avatar, list); // 관리자
            AssetDatabase.SaveAssets(); // 저장

            StringBuilder report = new StringBuilder("[Project I] 37일차 협동 네트워크 프리팹 구성 완료"); // 보고
            report.AppendLine(); // 줄
            report.AppendLine($"원정대원 몸체 {AvatarPath} · 고유 번호 {HashOf(avatar)}"); // 몸체
            report.AppendLine($"월드 상태 {WorldStatePath} · 고유 번호 {HashOf(worldState)} · 아이템 동기화 {(worldState.GetComponent<NetItemSync>() != null ? "포함" : "없음")}"); // 월드
            report.AppendLine($"프리팹 목록 {PrefabListPath} · {list.PrefabList.Count}개"); // 목록
            report.Append($"관리자 {ManagerPath} · 씬 관리 {manager.GetComponent<NetworkManager>().NetworkConfig.EnableSceneManagement} · 접속 확인 {manager.GetComponent<NetworkManager>().NetworkConfig.ConnectionApproval}"); // 관리자

            if (HashOf(avatar) == 0 || HashOf(worldState) == 0) // 고유 번호 없음
            {
                Debug.LogWarning("[Project I] 네트워크 프리팹 고유 번호가 0입니다 — 메뉴를 한 번 더 실행하세요."); // 경고
            }

            Debug.Log(report.ToString()); // 보고
        }

        private static GameObject BuildAvatar() // 다른 대원에게 보이는 몸체 (코트 · 머리 · 중절모 · 이름표)
        {
            Material coat = MakeMaterial("Net_Coat", new Color(0.32f, 0.24f, 0.18f)); // 코트
            Material skin = MakeMaterial("Net_Skin", new Color(0.86f, 0.7f, 0.58f)); // 얼굴
            Material hat = MakeMaterial("Net_Hat", new Color(0.12f, 0.11f, 0.1f)); // 모자
            Material scarf = MakeMaterial("Net_Scarf", new Color(0.8f, 0.36f, 0.14f)); // 주황 목도리 (눈에 띄게)

            GameObject root = new GameObject("NetPlayerAvatar"); // 루트
            root.AddComponent<NetworkObject>(); // 네트워크 오브젝트
            NetPlayerAvatar avatar = root.AddComponent<NetPlayerAvatar>(); // 몸체
            Transform visual = new GameObject("Visual").transform; // 몸체 묶음
            visual.SetParent(root.transform, false); // 부모
            Transform body = Part(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(0.62f, 0.8f, 0.5f), coat); // 몸통
            Part(PrimitiveType.Cylinder, visual, "Scarf", new Vector3(0f, 1.4f, 0f), new Vector3(0.44f, 0.06f, 0.44f), scarf); // 목도리
            Part(PrimitiveType.Sphere, visual, "Head", new Vector3(0f, 1.62f, 0f), new Vector3(0.32f, 0.36f, 0.32f), skin); // 머리
            Part(PrimitiveType.Cylinder, visual, "HatBrim", new Vector3(0f, 1.8f, 0f), new Vector3(0.46f, 0.015f, 0.46f), hat); // 모자 챙
            Part(PrimitiveType.Cylinder, visual, "HatCrown", new Vector3(0f, 1.9f, 0f), new Vector3(0.28f, 0.1f, 0.28f), hat); // 모자 윗부분
            Part(PrimitiveType.Cube, visual, "Pack", new Vector3(0f, 1.0f, -0.3f), new Vector3(0.42f, 0.5f, 0.2f), hat); // 등짐 (앞뒤 구분)
            TextMesh tag = TownKit.Label(root.transform, "NameTag", "원정대원", new Vector3(0f, 2.25f, 0f), 180f, 0.16f, new Color(1f, 0.72f, 0.42f)); // 이름표
            avatar.Configure(visual, body, tag); // 연결
            return SavePrefab(root, AvatarPath); // 저장
        }

        private static GameObject BuildWorldState() // 방장 기준 월드 상태
        {
            GameObject root = new GameObject("NetWorldState"); // 루트
            root.AddComponent<NetworkObject>(); // 네트워크 오브젝트
            root.AddComponent<NetWorldState>(); // 월드 상태
            root.AddComponent<NetItemSync>(); // 38일차 아이템·경제 동기화
            return SavePrefab(root, WorldStatePath); // 저장
        }

        private static NetworkPrefabsList BuildPrefabList(GameObject avatar, GameObject worldState) // 네트워크 프리팹 목록 (다시 만듦)
        {
            AssetDatabase.DeleteAsset(PrefabListPath); // 이전 목록
            NetworkPrefabsList list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); // 목록
            list.Add(new NetworkPrefab { Prefab = avatar }); // 몸체
            list.Add(new NetworkPrefab { Prefab = worldState }); // 월드 상태
            AssetDatabase.CreateAsset(list, PrefabListPath); // 저장
            return list; // 반환
        }

        private static GameObject BuildManager(GameObject avatar, NetworkPrefabsList list) // 네트워크 관리자 (직접 IP · 씬 관리 끔 · 접속 확인)
        {
            GameObject root = new GameObject("ProjectINetwork"); // 루트
            NetworkManager manager = root.AddComponent<NetworkManager>(); // 관리자
            UnityTransport transport = root.AddComponent<UnityTransport>(); // 연결
            root.AddComponent<NetworkSession>(); // 세션
            manager.NetworkConfig ??= new NetworkConfig(); // 설정
            NetworkConfig config = manager.NetworkConfig; // 설정
            config.NetworkTransport = transport; // 연결 방식
            config.PlayerPrefab = avatar; // 접속하면 몸체 생성
            config.Prefabs.NetworkPrefabsLists.Clear(); // 목록 초기화
            config.Prefabs.NetworkPrefabsLists.Add(list); // 목록
            config.EnableSceneManagement = false; // 씬 교체는 맵 로더가 직접
            config.ConnectionApproval = true; // 버전·인원 확인
            config.TickRate = 30; // 초당 30번
            config.ForceSamePrefabs = true; // 같은 프리팹 목록 필요
            config.ClientConnectionBufferTimeout = 15; // 접속 대기
            manager.RunInBackground = true; // 다른 창을 봐도 연결 유지
            transport.ConnectTimeoutMS = 1000; // 연결 시도 간격
            transport.MaxConnectAttempts = 10; // 10초 뒤 실패
            transport.SetConnectionData("127.0.0.1", NetworkSession.DefaultPort, "0.0.0.0"); // 기본 주소
            return SavePrefab(root, ManagerPath); // 저장
        }

        private static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material) // 몸체 부품 (충돌 없음)
        {
            GameObject part = GameObject.CreatePrimitive(type); // 도형
            part.name = name; // 이름
            part.transform.SetParent(parent, false); // 부모
            part.transform.localPosition = position; // 위치
            part.transform.localScale = scale; // 크기
            Object.DestroyImmediate(part.GetComponent<Collider>()); // 충돌 제거 (다른 대원·물건과 부딪히지 않음)
            Renderer renderer = part.GetComponent<Renderer>(); // 렌더러
            renderer.sharedMaterial = material; // 재질
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // 그림자
            return part.transform; // 반환
        }

        private static Material MakeMaterial(string name, Color color) // URP Lit 재질
        {
            string path = $"{MaterialFolder}/{name}.mat"; // 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존

            if (material == null) // 새로
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); // 셰이더
                material = new Material(shader); // 재질
                AssetDatabase.CreateAsset(material, path); // 저장
            }

            material.SetColor("_BaseColor", color); // 색
            material.color = color; // 색 (호환)
            material.SetFloat("_Smoothness", 0.2f); // 매끈함
            EditorUtility.SetDirty(material); // 변경
            return material; // 반환
        }

        private static GameObject SavePrefab(GameObject root, string path) // 프리팹 저장 후 고유 번호 생성
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path); // 저장
            Object.DestroyImmediate(root); // 임시 오브젝트 제거
            NetworkObject networkObject = asset.GetComponent<NetworkObject>(); // 네트워크 오브젝트

            if (networkObject != null) // 있음
            {
                MethodInfo validate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); // 고유 번호 생성 함수
                validate?.Invoke(networkObject, null); // 생성
                EditorUtility.SetDirty(asset); // 변경
                PrefabUtility.SavePrefabAsset(asset); // 다시 저장
            }

            return asset; // 반환
        }

        private static uint HashOf(GameObject prefab) // 넷코드 고유 번호
        {
            NetworkObject networkObject = prefab == null ? null : prefab.GetComponent<NetworkObject>(); // 네트워크 오브젝트

            if (networkObject == null) // 없음
            {
                return 0; // 없음
            }

            SerializedProperty property = new SerializedObject(networkObject).FindProperty("GlobalObjectIdHash"); // 저장된 값
            return property == null ? 0 : property.uintValue; // 반환
        }

        private static void EnsureFolder(string path) // 폴더 생성
        {
            if (AssetDatabase.IsValidFolder(path)) // 있음
            {
                return; // 생략
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/'); // 부모
            EnsureFolder(parent); // 부모 먼저
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); // 생성
        }
    }
}
