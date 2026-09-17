using ProjectI.Economy; // 판매대·벨 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase11Day32SaleBellSetup // 사무소 판매대 옆에 판매 벨을 배치합니다 (32일차)
    {
        private const string OfficeScenePath = "Assets/ProjectI/Scenes/01_Office.unity"; // 사무소 씬
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day32"; // 재질 폴더
        private const string BellName = "Day32_SaleBell"; // 벨 오브젝트 이름
        private const float StandSize = 0.42f; // 받침대 가로·세로
        private const float StandGap = 0.12f; // 판매대와 받침대 사이 간격

        [MenuItem("Project I/Day 32/Add Sale Bell To Office")] // 메뉴
        public static void Setup() // 배치
        {
            var scene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single); // 사무소 씬 열기
            OfficeSaleCounter counter = Object.FindFirstObjectByType<OfficeSaleCounter>(FindObjectsInactive.Include); // 판매대

            if (counter == null) // 판매대 없음
            {
                Debug.LogError("[Project I] 32일차 판매 벨 배치 실패 / 01_Office 에 판매대가 없습니다"); // 오류
                return; // 종료
            }

            Transform parent = counter.transform.parent; // 판매대와 같은 부모 (판매대는 크기 배율이 있어 자식으로 두지 않음)
            Transform old = parent == null ? null : parent.Find(BellName); // 이전 벨

            if (old != null) // 다시 만들기
            {
                Object.DestroyImmediate(old.gameObject); // 제거
            }

            Material wood = CreateOrUpdateMaterial("SaleBell_Stand", new Color(0.36f, 0.24f, 0.15f), 0f, 0.25f); // 받침대
            Material brass = CreateOrUpdateMaterial("SaleBell_Brass", new Color(0.86f, 0.66f, 0.28f), 0.9f, 0.75f); // 놋쇠
            Material dark = CreateOrUpdateMaterial("SaleBell_Base", new Color(0.12f, 0.1f, 0.09f), 0.3f, 0.4f); // 받침

            Vector3 counterScale = counter.transform.localScale; // 판매대 크기 (부모 기준)
            float counterHeight = counterScale.y; // 판매대 높이
            float floorY = counter.transform.localPosition.y - (counterHeight * 0.5f); // 바닥 높이 (부모 기준)
            Physics.SyncTransforms(); // 겹침 검사 전 충돌체 위치 동기화
            Vector3 side = PickFreeSide(counter, counterScale); // 비어 있는 옆면 방향 (부모 기준)
            float offset = (Mathf.Abs(Vector3.Dot(side, counter.transform.localRotation * Vector3.right)) > 0.5f ? counterScale.x : counterScale.z) * 0.5f + StandGap + (StandSize * 0.5f); // 판매대 중심에서 받침대 중심까지
            Vector3 basePosition = counter.transform.localPosition + (side * offset); // 받침대 위치
            basePosition.y = floorY; // 바닥

            GameObject root = new GameObject(BellName); // 벨 루트
            Undo.RegisterCreatedObjectUndo(root, "Add Sale Bell"); // 되돌리기
            root.transform.SetParent(parent, false); // 판매대 옆
            root.transform.localPosition = basePosition; // 위치
            root.transform.localRotation = counter.transform.localRotation; // 판매대 방향

            float standHeight = counterHeight - 0.02f; // 판매대보다 살짝 낮은 받침대
            CreatePart(root.transform, "Stand", PrimitiveType.Cube, new Vector3(0f, standHeight * 0.5f, 0f), new Vector3(StandSize, standHeight, StandSize), wood, true); // 받침대
            CreatePart(root.transform, "BellBase", PrimitiveType.Cylinder, new Vector3(0f, standHeight + 0.015f, 0f), new Vector3(0.2f, 0.015f, 0.2f), dark, false); // 벨 받침 (원기둥 높이는 배율의 2배)
            GameObject dome = CreatePart(root.transform, "BellDome", PrimitiveType.Sphere, new Vector3(0f, standHeight + 0.03f, 0f), new Vector3(0.16f, 0.13f, 0.16f), brass, false); // 벨 반구
            CreatePart(dome.transform, "Knob", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.22f, 0.3f, 0.22f), brass, false); // 누르는 꼭지 (반구 기준)

            BoxCollider bellCollider = root.AddComponent<BoxCollider>(); // 조사 판정 (벨 주변)
            bellCollider.center = new Vector3(0f, standHeight + 0.1f, 0f); // 벨 높이
            bellCollider.size = new Vector3(0.3f, 0.22f, 0.3f); // 벨 크기보다 넉넉하게

            OfficeSalePanel panel = root.AddComponent<OfficeSalePanel>(); // 선택 창
            OfficeSaleBell bell = root.AddComponent<OfficeSaleBell>(); // 벨
            bell.Configure(counter, panel, dome.transform); // 연결

            EditorUtility.SetDirty(bell); // 저장 대상
            EditorSceneManager.MarkSceneDirty(scene); // 변경 표시
            EditorSceneManager.SaveScene(scene); // 저장
            AssetDatabase.SaveAssets(); // 재질 저장
            Debug.Log($"[Project I] 32일차 판매 벨 배치 완료 / 판매대 {counter.name} 옆 {side} / 위치 {root.transform.position}"); // 완료
        }

        public static void SetupFromCommandLine() // 배치모드 실행용
        {
            Setup(); // 배치
        }

        private static Vector3 PickFreeSide(OfficeSaleCounter counter, Vector3 counterScale) // 벨을 둘 옆면 (막히지 않은 쪽)
        {
            Transform t = counter.transform; // 판매대
            Vector3[] localSides = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back }; // 오른쪽 우선

            foreach (Vector3 localSide in localSides) // 옆면 후보
            {
                Vector3 sideInParent = t.localRotation * localSide; // 부모 기준 방향
                float half = (localSide.x != 0f ? counterScale.x : counterScale.z) * 0.5f; // 판매대 반폭
                Vector3 worldDirection = t.parent == null ? sideInParent : t.parent.TransformDirection(sideInParent); // 월드 방향
                Vector3 center = t.position + (worldDirection.normalized * (half + StandGap + (StandSize * 0.5f))); // 받침대 중심 (판매대 높이)
                Vector3 checkCenter = center + (Vector3.up * 0.25f); // 바닥 두께를 피하도록 위로 올린 검사 중심
                Collider[] hits = Physics.OverlapBox(checkCenter, new Vector3(StandSize * 0.5f, Mathf.Max(0.1f, (counterScale.y * 0.5f) - 0.3f), StandSize * 0.5f), t.rotation, ~0, QueryTriggerInteraction.Ignore); // 겹침 검사 (바닥 제외)
                bool blocked = false; // 막힘 여부

                foreach (Collider hit in hits) // 겹친 물체
                {
                    if (hit.transform != t && !hit.transform.IsChildOf(t)) // 판매대 자신 제외
                    {
                        blocked = true; // 막힘
                        Debug.Log($"[Project I] 판매 벨 후보 {localSide} 막힘 / {hit.name}"); // 진단
                        break; // 중단
                    }
                }

                if (!blocked) // 비어 있음
                {
                    return sideInParent; // 선택
                }
            }

            return t.localRotation * Vector3.right; // 모두 막혔으면 오른쪽
        }

        private static GameObject CreatePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider) // 기본 도형 부품
        {
            GameObject part = GameObject.CreatePrimitive(type); // 도형
            part.name = name; // 이름
            part.transform.SetParent(parent, false); // 부모
            part.transform.localPosition = localPosition; // 위치
            part.transform.localScale = localScale; // 크기

            if (!keepCollider) // 벨 부품은 루트 판정만 사용
            {
                Object.DestroyImmediate(part.GetComponent<Collider>()); // 제거
            }

            part.GetComponent<Renderer>().sharedMaterial = material; // 재질
            return part; // 반환
        }

        private static Material CreateOrUpdateMaterial(string materialName, Color baseColor, float metallic, float smoothness) // URP Lit 재질
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) // 폴더
            {
                AssetDatabase.CreateFolder("Assets/ProjectI/Art/Generated", "Day32"); // 생성
            }

            string assetPath = $"{MaterialFolder}/{materialName}.mat"; // 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); // 셰이더

            if (material == null) // 새로 생성
            {
                material = new Material(shader) { name = materialName }; // 재질
                AssetDatabase.CreateAsset(material, assetPath); // 저장
            }

            material.shader = shader; // 셰이더 갱신
            material.SetColor("_BaseColor", baseColor); // 색
            material.color = baseColor; // 호환 색
            material.SetFloat("_Metallic", metallic); // 금속성
            material.SetFloat("_Smoothness", smoothness); // 매끄러움
            EditorUtility.SetDirty(material); // 저장 대상
            return material; // 반환
        }
    }
}
