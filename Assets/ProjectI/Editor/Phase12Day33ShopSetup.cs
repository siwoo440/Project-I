using System.Collections.Generic; // 목록 사용
using System.Linq; // 정렬 사용
using ProjectI.Economy; // 상점 참조
using ProjectI.Items; // 아이템 정의 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase12Day33ShopSetup // 사무소에 위·아래 2칸 마트형 진열대와 수령대를 만듭니다 (33일차)
    {
        private const string OfficeScenePath = "Assets/ProjectI/Scenes/01_Office.unity"; // 사무소 씬
        private const string CatalogFolder = "Assets/ProjectI/Data/Shop"; // 상품 목록 폴더
        private const string CatalogPath = CatalogFolder + "/OfficeShopCatalog.asset"; // 상품 목록
        private const string DefinitionFolder = "Assets/ProjectI/Resources/Day24Recovery/Definitions"; // 아이템 정의
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day33"; // 재질
        private const string ShopRootName = "Day33_Shop"; // 상점 루트 이름

        private const float ShelfWidth = 3.2f; // 진열대 폭
        private const float ShelfDepth = 0.5f; // 진열대 깊이
        private const float ShelfHeight = 1.75f; // 진열대 높이
        private const float Board = 0.04f; // 판 두께
        private const float LowerTop = 0.16f; // 아래 칸 바닥 높이
        private const float UpperTop = 0.88f; // 위 칸 바닥 높이
        private const float DisplayHeight = 0.5f; // 견본이 들어갈 높이
        private const float TrayWidth = 0.9f; // 수령대 폭
        private const float TrayDepth = 0.6f; // 수령대 깊이
        private const float TrayHeight = 0.85f; // 수령대 높이
        private const float FloorTopY = 0.2f; // 사무소 바닥 윗면 (OfficeInterior 기준)
        private const float SouthWallInnerZ = -5.875f; // 남쪽 벽 안쪽 면 (OfficeInterior 기준)
        private const float ShelfCenterX = -2.0f; // 진열대 중심 x
        private const float TrayCenterX = 0.25f; // 수령대 중심 x

        private static readonly (string id, int price, int max, ShopShelfTier tier)[] Products = // 상품 표 (칸 안에서는 왼쪽부터)
        {
            ("light.flashlight", 120, 5, ShopShelfTier.Upper), // 손전등
            ("key.basic", 200, 5, ShopShelfTier.Upper), // 열쇠
            ("consumable.healing", 150, 5, ShopShelfTier.Upper), // 회복 아이템
            ("tool.small_tool", 60, 5, ShopShelfTier.Upper), // 작은 도구
            ("tool.pickaxe", 180, 3, ShopShelfTier.Lower), // 곡괭이
            ("weapon.sword", 250, 3, ShopShelfTier.Lower), // 검
            ("weapon.crossbow", 400, 3, ShopShelfTier.Lower), // 석궁
            ("weapon.revolver_6shot", 650, 3, ShopShelfTier.Lower), // 6연발 리볼버
        };

        [MenuItem("Project I/Day 33/Build Office Shop")] // 메뉴
        public static void Setup() // 구성
        {
            ShopCatalog catalog = BuildCatalog(); // 상품 목록

            if (catalog == null || catalog.Entries.Count == 0) // 확인
            {
                Debug.LogError("[Project I] 33일차 상점 구성 실패 / 상품 목록을 만들지 못했습니다"); // 오류
                return; // 종료
            }

            var scene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single); // 사무소 씬
            catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath); // 씬 전환 뒤 다시 불러오기 (참조 무효화 방지)
            OfficeSaleCounter counter = Object.FindFirstObjectByType<OfficeSaleCounter>(FindObjectsInactive.Include); // 판매대 (사무소 실내 기준 찾기)

            if (counter == null || counter.transform.parent == null) // 확인
            {
                Debug.LogError("[Project I] 33일차 상점 구성 실패 / 01_Office 에서 사무소 실내를 찾지 못했습니다"); // 오류
                return; // 종료
            }

            Transform interior = counter.transform.parent; // OfficeInterior
            Transform old = interior.Find(ShopRootName); // 이전 상점

            if (old != null) // 다시 만들기
            {
                Object.DestroyImmediate(old.gameObject); // 제거
            }

            Material frame = CreateOrUpdateMaterial("Shop_ShelfFrame", new Color(0.30f, 0.20f, 0.13f), 0f, 0.3f); // 틀
            Material board = CreateOrUpdateMaterial("Shop_ShelfBoard", new Color(0.62f, 0.47f, 0.30f), 0f, 0.35f); // 선반 판
            Material rail = CreateOrUpdateMaterial("Shop_PriceRail", new Color(0.92f, 0.90f, 0.82f), 0f, 0.2f); // 가격표 띠
            Material tag = CreateOrUpdateMaterial("Shop_PriceTag", new Color(0.95f, 0.78f, 0.25f), 0f, 0.3f); // 가격표
            Material trayTop = CreateOrUpdateMaterial("Shop_TrayTop", new Color(0.24f, 0.36f, 0.30f), 0f, 0.3f); // 수령대 윗면

            GameObject root = new GameObject(ShopRootName); // 상점 루트
            Undo.RegisterCreatedObjectUndo(root, "Build Office Shop"); // 되돌리기
            root.transform.SetParent(interior, false); // 사무소 실내
            root.transform.localPosition = Vector3.zero; // 기준
            root.transform.localRotation = Quaternion.identity; // 기준

            Transform shelfRoot = BuildShelf(root.transform, frame, board, rail); // 진열대 틀
            OfficeShopPanel panel = shelfRoot.gameObject.AddComponent<OfficeShopPanel>(); // 구매 창
            OfficeShopShelf shelf = shelfRoot.gameObject.AddComponent<OfficeShopShelf>(); // 진열대
            OfficeShopPickupTray tray = BuildTray(root.transform, frame, trayTop); // 수령대
            shelf.Configure(catalog, tray, panel); // 연결

            int slotCount = 0; // 진열 수
            slotCount += BuildTier(shelf, catalog, ShopShelfTier.Upper, UpperTop, tag); // 위 칸
            slotCount += BuildTier(shelf, catalog, ShopShelfTier.Lower, LowerTop, tag); // 아래 칸

            EditorUtility.SetDirty(shelf); // 저장 대상
            EditorSceneManager.MarkSceneDirty(scene); // 변경
            Physics.SyncTransforms(); // 겹침 검사 준비
            ReportOverlaps(root.transform, shelfRoot, new Vector3(ShelfWidth, ShelfHeight, ShelfDepth)); // 진열대 겹침
            ReportOverlaps(root.transform, tray.transform, new Vector3(TrayWidth, TrayHeight, TrayDepth)); // 수령대 겹침
            EditorSceneManager.SaveScene(scene); // 저장
            AssetDatabase.SaveAssets(); // 에셋 저장
            Debug.Log($"[Project I] 33일차 상점 구성 완료 / 진열 {slotCount}종 (위 {catalog.EntriesOn(ShopShelfTier.Upper).Count} · 아래 {catalog.EntriesOn(ShopShelfTier.Lower).Count}) / 진열대 {shelfRoot.position} / 수령대 {tray.transform.position}"); // 완료
        }

        public static void SetupFromCommandLine() // 배치모드 실행용
        {
            Setup(); // 구성
        }

        private static ShopCatalog BuildCatalog() // 상품 목록 에셋 생성·갱신
        {
            EnsureFolder("Assets/ProjectI", "Data"); // 폴더
            EnsureFolder("Assets/ProjectI/Data", "Shop"); // 폴더
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath); // 기존

            if (catalog == null) // 새로 생성
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>(); // 생성
                AssetDatabase.CreateAsset(catalog, CatalogPath); // 저장
            }

            List<ShopEntry> entries = new List<ShopEntry>(); // 상품

            foreach (var product in Products) // 표 순회
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{DefinitionFolder}/{product.id}.asset"); // 정의

                if (definition == null || definition.RecoveryPrefab == null) // 없음
                {
                    Debug.LogWarning($"[Project I] 상점 상품 제외 / {product.id} 정의 또는 프리팹 없음"); // 경고
                    continue; // 다음
                }

                entries.Add(new ShopEntry { item = definition, price = product.price, maxPerPurchase = product.max, tier = product.tier }); // 등록
            }

            catalog.Configure(entries); // 적용
            EditorUtility.SetDirty(catalog); // 저장 대상
            AssetDatabase.SaveAssets(); // 저장
            return catalog; // 반환
        }

        private static Transform BuildShelf(Transform parent, Material frame, Material board, Material rail) // 위·아래 2칸 진열대 틀
        {
            GameObject shelf = new GameObject("Shelf"); // 진열대
            shelf.transform.SetParent(parent, false); // 상점 아래
            shelf.transform.localPosition = new Vector3(ShelfCenterX, FloorTopY, SouthWallInnerZ + (ShelfDepth * 0.5f) + 0.03f); // 남쪽 벽에 붙임 (앞면이 +z, 사무소 안쪽)
            shelf.transform.localRotation = Quaternion.identity; // 앞면 +z

            float innerWidth = ShelfWidth - (Board * 2f); // 안쪽 폭
            CreatePart(shelf.transform, "Plinth", new Vector3(0f, (LowerTop - Board) * 0.5f, 0f), new Vector3(ShelfWidth, LowerTop - Board, ShelfDepth - 0.02f), frame); // 받침
            CreatePart(shelf.transform, "Board_Lower", new Vector3(0f, LowerTop - (Board * 0.5f), 0f), new Vector3(innerWidth, Board, ShelfDepth), board); // 아래 칸 판
            CreatePart(shelf.transform, "Board_Upper", new Vector3(0f, UpperTop - (Board * 0.5f), 0f), new Vector3(innerWidth, Board, ShelfDepth), board); // 위 칸 판
            CreatePart(shelf.transform, "Board_Top", new Vector3(0f, ShelfHeight - (Board * 0.5f), 0f), new Vector3(ShelfWidth, Board, ShelfDepth), frame); // 지붕 판
            CreatePart(shelf.transform, "Side_L", new Vector3(-(ShelfWidth - Board) * 0.5f, ShelfHeight * 0.5f, 0f), new Vector3(Board, ShelfHeight, ShelfDepth), frame); // 왼쪽 옆판
            CreatePart(shelf.transform, "Side_R", new Vector3((ShelfWidth - Board) * 0.5f, ShelfHeight * 0.5f, 0f), new Vector3(Board, ShelfHeight, ShelfDepth), frame); // 오른쪽 옆판
            CreatePart(shelf.transform, "Back", new Vector3(0f, ShelfHeight * 0.5f, -(ShelfDepth * 0.5f) + 0.01f), new Vector3(ShelfWidth, ShelfHeight, 0.02f), frame); // 뒤판
            CreatePart(shelf.transform, "Rail_Lower", new Vector3(0f, LowerTop - 0.035f, (ShelfDepth * 0.5f) + 0.006f), new Vector3(innerWidth, 0.06f, 0.012f), rail, false); // 아래 칸 가격표 띠
            CreatePart(shelf.transform, "Rail_Upper", new Vector3(0f, UpperTop - 0.035f, (ShelfDepth * 0.5f) + 0.006f), new Vector3(innerWidth, 0.06f, 0.012f), rail, false); // 위 칸 가격표 띠
            return shelf.transform; // 반환
        }

        private static int BuildTier(OfficeShopShelf shelf, ShopCatalog catalog, ShopShelfTier tier, float baseY, Material tagMaterial) // 한 칸의 견본 진열
        {
            List<ShopEntry> entries = catalog.EntriesOn(tier); // 칸 상품
            float innerWidth = ShelfWidth - (Board * 2f); // 안쪽 폭
            float slotWidth = entries.Count == 0 ? innerWidth : innerWidth / entries.Count; // 칸 폭

            for (int index = 0; index < entries.Count; index++) // 상품 순회
            {
                ShopEntry entry = entries[index]; // 상품
                int entryIndex = catalog.Entries.ToList().IndexOf(entry); // 목록 번호
                float x = (-innerWidth * 0.5f) + (slotWidth * (index + 0.5f)); // 칸 중심
                GameObject slot = new GameObject($"{tier}_{index + 1}_{entry.item.ItemId}"); // 진열 칸
                slot.transform.SetParent(shelf.transform, false); // 진열대 아래
                slot.transform.localPosition = new Vector3(x, baseY, 0f); // 칸 바닥
                BoxCollider hit = slot.AddComponent<BoxCollider>(); // 시선 판정 (칸 전체)
                hit.center = new Vector3(0f, DisplayHeight * 0.5f + 0.02f, 0.02f); // 칸 안
                hit.size = new Vector3(slotWidth - 0.06f, DisplayHeight + 0.04f, ShelfDepth - 0.08f); // 칸 크기
                OfficeShopShelfItem item = slot.AddComponent<OfficeShopShelfItem>(); // 상품 조사
                item.Configure(shelf, entryIndex); // 연결
                MakeDisplayCopy(entry.item.RecoveryPrefab, slot.transform, new Vector3(slotWidth - 0.16f, DisplayHeight - 0.06f, ShelfDepth - 0.16f)); // 견본
                BuildPriceTag(slot.transform, entry.price, tagMaterial); // 가격표
                EditorUtility.SetDirty(item); // 저장 대상
            }

            return entries.Count; // 진열 수
        }

        private static void MakeDisplayCopy(GameObject prefab, Transform slot, Vector3 fit) // 실제 아이템 모양의 견본 (기능·물리 제거)
        {
            GameObject copy = Object.Instantiate(prefab); // 프리팹 연결 없는 복사본
            copy.name = "Display"; // 이름
            StripToVisual(copy); // 기능 제거
            copy.transform.SetParent(slot, false); // 칸 아래
            copy.transform.localPosition = Vector3.zero; // 기준
            copy.transform.localRotation = Quaternion.identity; // 기준
            Bounds bounds = LocalBounds(copy, slot); // 칸 기준 크기

            if (bounds.size.y > bounds.size.x && bounds.size.y > bounds.size.z) // 세워진 긴 물건 → 눕힘
            {
                copy.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * copy.transform.localRotation; // 눕히기
                bounds = LocalBounds(copy, slot); // 다시 계산
            }

            if (bounds.size.z > bounds.size.x) // 앞뒤로 긴 물건 → 폭 방향으로
            {
                copy.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * copy.transform.localRotation; // 돌리기
                bounds = LocalBounds(copy, slot); // 다시 계산
            }

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)); // 가장 긴 변
            float shrink = Mathf.Min(fit.x / Mathf.Max(0.001f, bounds.size.x), Mathf.Min(fit.y / Mathf.Max(0.001f, bounds.size.y), fit.z / Mathf.Max(0.001f, bounds.size.z))); // 칸에 맞는 배율
            float grow = largest < 0.2f ? 0.2f / Mathf.Max(0.001f, largest) : 1f; // 너무 작은 물건은 보이게 키움
            float scale = Mathf.Min(shrink, grow); // 최종 배율
            copy.transform.localScale *= scale; // 적용
            bounds = LocalBounds(copy, slot); // 다시 계산
            Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z); // 바닥 중심
            copy.transform.localPosition -= bottomCenter - new Vector3(0f, 0.005f, 0f); // 칸 바닥 중앙에 세움
        }

        private static void StripToVisual(GameObject copy) // 스크립트·물리·조명·소리 제거
        {
            List<MonoBehaviour> behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true).ToList(); // 스크립트
            behaviours.Sort((a, b) => Rank(a).CompareTo(Rank(b))); // 의존 순서 (다른 스크립트 → 식별자 → WorldItem)

            foreach (MonoBehaviour behaviour in behaviours) // 제거
            {
                if (behaviour != null) // 확인
                {
                    Object.DestroyImmediate(behaviour); // 제거
                }
            }

            RemoveAll<Joint>(copy); // 관절
            RemoveAll<Rigidbody>(copy); // 물리
            RemoveAll<Collider>(copy); // 충돌
            RemoveAll<Light>(copy); // 조명
            RemoveAll<AudioSource>(copy); // 소리
            RemoveAll<ParticleSystem>(copy); // 파티클
            RemoveAll<ParticleSystemRenderer>(copy); // 파티클 표시
            RemoveAll<Animator>(copy); // 애니메이터
        }

        private static int Rank(MonoBehaviour behaviour) // 제거 순서
        {
            if (behaviour is WorldItem) // 가장 나중
            {
                return 2; // 마지막
            }

            return behaviour is WorldItemIdentity ? 1 : 0; // 식별자 → 나머지
        }

        private static void RemoveAll<T>(GameObject root) where T : Component // 컴포넌트 일괄 제거
        {
            foreach (T component in root.GetComponentsInChildren<T>(true)) // 순회
            {
                if (component != null) // 확인
                {
                    Object.DestroyImmediate(component); // 제거
                }
            }
        }

        private static Bounds LocalBounds(GameObject copy, Transform space) // 렌더러 범위를 기준 공간으로 (8꼭짓점)
        {
            bool hasBounds = false; // 초기화
            Bounds result = new Bounds(Vector3.zero, Vector3.zero); // 결과

            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true)) // 렌더러 순회
            {
                Bounds local = renderer.localBounds; // 렌더러 자기 공간
                Vector3 min = local.min; // 최소
                Vector3 max = local.max; // 최대

                for (int corner = 0; corner < 8; corner++) // 꼭짓점
                {
                    Vector3 point = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z); // 꼭짓점
                    Vector3 inSpace = space.InverseTransformPoint(renderer.transform.TransformPoint(point)); // 기준 공간

                    if (!hasBounds) // 첫 점
                    {
                        result = new Bounds(inSpace, Vector3.zero); // 시작
                        hasBounds = true; // 표시
                    }
                    else // 확장
                    {
                        result.Encapsulate(inSpace); // 포함
                    }
                }
            }

            return result; // 반환
        }

        private static void BuildPriceTag(Transform slot, int price, Material tagMaterial) // 가격표 (숫자만: 3D 글꼴의 한글 누락 방지)
        {
            GameObject plate = CreatePart(slot, "PriceTag", new Vector3(0f, -0.035f, (ShelfDepth * 0.5f) + 0.014f), new Vector3(0.16f, 0.05f, 0.006f), tagMaterial, false); // 가격표 판
            GameObject label = new GameObject("PriceText"); // 글자
            label.transform.SetParent(slot, false); // 칸 아래 (판 배율 영향 없음)
            label.transform.localPosition = new Vector3(0f, -0.035f, (ShelfDepth * 0.5f) + 0.019f); // 판 앞
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 사무소 안쪽(+z)에서 읽히도록
            TextMesh text = label.AddComponent<TextMesh>(); // 3D 글자
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 기본 글꼴
            text.font = font; // 글꼴
            text.text = price.ToString(); // 가격
            text.fontSize = 64; // 해상도
            text.characterSize = 0.006f; // 크기
            text.anchor = TextAnchor.MiddleCenter; // 가운데
            text.alignment = TextAlignment.Center; // 가운데
            text.color = new Color(0.12f, 0.08f, 0.05f); // 글자색
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material; // 글꼴 재질
            plate.name = "PriceTag"; // 이름
        }

        private static OfficeShopPickupTray BuildTray(Transform parent, Material frame, Material top) // 산 물건이 놓이는 수령대
        {
            GameObject trayObject = new GameObject("PickupTray"); // 수령대
            trayObject.transform.SetParent(parent, false); // 상점 아래
            trayObject.transform.localPosition = new Vector3(TrayCenterX, FloorTopY, SouthWallInnerZ + (TrayDepth * 0.5f) + 0.03f); // 진열대 오른쪽, 벽에 붙임
            trayObject.transform.localRotation = Quaternion.identity; // 진열대와 같은 방향
            OfficeShopPickupTray tray = trayObject.AddComponent<OfficeShopPickupTray>(); // 수령대 (상자 콜라이더 자동 추가)
            BoxCollider surface = trayObject.GetComponent<BoxCollider>(); // 윗면
            surface.center = new Vector3(0f, TrayHeight - 0.025f, 0f); // 윗면 높이
            surface.size = new Vector3(TrayWidth, 0.05f, TrayDepth); // 윗면 크기
            CreatePart(trayObject.transform, "Top", surface.center, surface.size, top, false); // 윗면 외형 (판정은 루트)
            CreatePart(trayObject.transform, "Lip_Back", new Vector3(0f, TrayHeight + 0.03f, -(TrayDepth * 0.5f) + 0.015f), new Vector3(TrayWidth, 0.06f, 0.03f), frame); // 뒤 턱
            float legX = (TrayWidth * 0.5f) - 0.05f; // 다리 x
            float legZ = (TrayDepth * 0.5f) - 0.05f; // 다리 z
            float legHeight = TrayHeight - 0.05f; // 다리 높이

            foreach (Vector2 corner in new[] { new Vector2(-legX, -legZ), new Vector2(legX, -legZ), new Vector2(-legX, legZ), new Vector2(legX, legZ) }) // 네 다리
            {
                CreatePart(trayObject.transform, "Leg", new Vector3(corner.x, legHeight * 0.5f, corner.y), new Vector3(0.06f, legHeight, 0.06f), frame); // 다리
            }

            return tray; // 반환
        }

        private static GameObject CreatePart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider = true) // 상자 부품
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); // 상자
            part.name = name; // 이름
            part.transform.SetParent(parent, false); // 부모
            part.transform.localPosition = localPosition; // 위치
            part.transform.localScale = localScale; // 크기

            if (!keepCollider) // 판정 불필요
            {
                Object.DestroyImmediate(part.GetComponent<Collider>()); // 제거
            }

            part.GetComponent<Renderer>().sharedMaterial = material; // 재질
            return part; // 반환
        }

        private static void ReportOverlaps(Transform shopRoot, Transform target, Vector3 size) // 사무소 기존 물체와 겹침 경고
        {
            Vector3 center = target.position + (Vector3.up * ((size.y * 0.5f) + 0.05f)); // 바닥 위 중심
            Vector3 half = new Vector3(size.x * 0.5f - 0.02f, (size.y * 0.5f) - 0.05f, size.z * 0.5f - 0.02f); // 살짝 줄인 범위

            foreach (Collider hit in Physics.OverlapBox(center, half, target.rotation, ~0, QueryTriggerInteraction.Ignore)) // 겹친 물체
            {
                if (!hit.transform.IsChildOf(shopRoot)) // 상점 자신 제외
                {
                    Debug.LogWarning($"[Project I] 상점 배치 겹침 / {target.name} ↔ {hit.name} ({hit.transform.position})"); // 경고
                }
            }
        }

        private static void EnsureFolder(string parent, string name) // 폴더 생성
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) // 없음
            {
                AssetDatabase.CreateFolder(parent, name); // 생성
            }
        }

        private static Material CreateOrUpdateMaterial(string materialName, Color baseColor, float metallic, float smoothness) // URP Lit 재질
        {
            EnsureFolder("Assets/ProjectI/Art/Generated", "Day33"); // 폴더
            string assetPath = $"{MaterialFolder}/{materialName}.mat"; // 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 셰이더

            if (shader == null) // 대체
            {
                shader = Shader.Find("Standard"); // 기본
            }

            if (material == null) // 새로 생성
            {
                material = new Material(shader) { name = materialName }; // 재질
                AssetDatabase.CreateAsset(material, assetPath); // 저장
            }

            material.shader = shader; // 셰이더
            material.SetColor("_BaseColor", baseColor); // 색
            material.color = baseColor; // 호환 색
            material.SetFloat("_Metallic", metallic); // 금속성
            material.SetFloat("_Smoothness", smoothness); // 매끄러움
            EditorUtility.SetDirty(material); // 저장 대상
            return material; // 반환
        }
    }
}
