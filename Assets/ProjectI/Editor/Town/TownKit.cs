using System.Collections.Generic; // 재질 캐시
using ProjectI.Brightness; // 게임용 밝기 광원·실내 영역
using UnityEditor; // 에셋 저장
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools.Town // 34일차 마을 제작 도구
{
    public static class TownKit // 마을 모델링에 쓰는 기본 도형·재질·조명·글자 도구
    {
        public const string ArtFolder = "Assets/ProjectI/Art/Generated/Day34"; // 재질·메시 폴더
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(); // 재질 캐시
        private static Mesh prismMesh; // 삼각기둥 메시 (박공)

        public static void ResetCache() // 메뉴 실행마다 캐시 초기화
        {
            Materials.Clear(); // 재질
            prismMesh = null; // 메시
        }

        public static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.25f, Color? emission = null) // URP Lit 재질 (이름 기준 재사용)
        {
            if (Materials.TryGetValue(name, out Material cached) && cached != null) // 캐시
            {
                return cached; // 반환
            }

            EnsureFolder(); // 폴더
            string path = $"{ArtFolder}/{name}.mat"; // 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 셰이더

            if (shader == null) // 대체
            {
                shader = Shader.Find("Standard"); // 기본
            }

            if (material == null) // 생성
            {
                material = new Material(shader) { name = name }; // 재질
                AssetDatabase.CreateAsset(material, path); // 저장
            }

            material.shader = shader; // 셰이더
            material.SetColor("_BaseColor", color); // 색
            material.color = color; // 호환 색
            material.SetFloat("_Metallic", metallic); // 금속성
            material.SetFloat("_Smoothness", smoothness); // 매끄러움

            if (emission.HasValue) // 발광
            {
                material.EnableKeyword("_EMISSION"); // 발광 켬
                material.SetColor("_EmissionColor", emission.Value); // 발광 색
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // 실시간
            }
            else // 발광 없음
            {
                material.DisableKeyword("_EMISSION"); // 발광 끔
            }

            EditorUtility.SetDirty(material); // 저장 대상
            Materials[name] = material; // 캐시
            return material; // 반환
        }

        public static Transform Group(Transform parent, string name, Vector3 localPosition, float yaw = 0f) // 빈 묶음
        {
            GameObject group = new GameObject(name); // 오브젝트
            group.transform.SetParent(parent, false); // 부모
            group.transform.localPosition = localPosition; // 위치
            group.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); // 방향
            return group.transform; // 반환
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider = true, Vector3? euler = null) // 상자
        {
            return Primitive(PrimitiveType.Cube, parent, name, center, size, material, collider, euler); // 생성
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 center, float diameter, float height, Material material, bool collider = false, Vector3? euler = null) // 원기둥 (높이는 실제 m)
        {
            return Primitive(PrimitiveType.Cylinder, parent, name, center, new Vector3(diameter, height * 0.5f, diameter), material, collider, euler); // 생성
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider = false) // 구
        {
            return Primitive(PrimitiveType.Sphere, parent, name, center, size, material, collider, null); // 생성
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 scale, Material material, bool collider, Vector3? euler) // 기본 도형
        {
            GameObject part = GameObject.CreatePrimitive(type); // 도형
            part.name = name; // 이름
            part.transform.SetParent(parent, false); // 부모
            part.transform.localPosition = center; // 위치
            part.transform.localRotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.identity; // 회전
            part.transform.localScale = scale; // 크기

            if (!collider) // 충돌 불필요
            {
                Object.DestroyImmediate(part.GetComponent<Collider>()); // 제거
            }

            part.GetComponent<Renderer>().sharedMaterial = material; // 재질
            return part; // 반환
        }

        public static GameObject Prism(Transform parent, string name, Vector3 baseCenter, float thickness, float baseWidth, float height, Material material, bool collider = true) // 박공 삼각기둥 (밑면 중심 기준, x 두께 · z 밑변 · y 높이)
        {
            GameObject part = new GameObject(name); // 오브젝트
            part.transform.SetParent(parent, false); // 부모
            part.transform.localPosition = baseCenter; // 위치
            part.transform.localScale = new Vector3(thickness, height, baseWidth); // 크기
            Mesh mesh = PrismMesh(); // 메시
            part.AddComponent<MeshFilter>().sharedMesh = mesh; // 표시
            part.AddComponent<MeshRenderer>().sharedMaterial = material; // 재질

            if (collider) // 충돌
            {
                MeshCollider meshCollider = part.AddComponent<MeshCollider>(); // 메시 충돌
                meshCollider.sharedMesh = mesh; // 메시
                meshCollider.convex = true; // 볼록
            }

            return part; // 반환
        }

        public static TextMesh Label(Transform parent, string name, string text, Vector3 position, float yaw, float height, Color color) // 3D 글자 (height = 글자 높이 m)
        {
            GameObject label = new GameObject(name); // 오브젝트
            label.transform.SetParent(parent, false); // 부모
            label.transform.localPosition = position; // 위치
            label.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); // 방향 (yaw 180 = +z 쪽에서 읽힘)
            TextMesh mesh = label.AddComponent<TextMesh>(); // 글자
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 기본 글꼴 (한글은 OS 글꼴로 대체 표시)
            mesh.font = font; // 글꼴
            mesh.text = text; // 내용
            mesh.fontSize = 96; // 해상도
            mesh.characterSize = height / 9.6f; // 글자 높이
            mesh.anchor = TextAnchor.MiddleCenter; // 가운데
            mesh.alignment = TextAlignment.Center; // 가운데
            mesh.color = color; // 색
            ProjectI.EditorTools.TextMeshDepthFixTool.ApplyTo(mesh); // 깊이 검사 글꼴 재질 (벽 너머로 비치지 않음)
            return mesh; // 반환
        }

        public static Light PointLight(Transform parent, string name, Vector3 position, float range, float intensity, Color color, float gameBrightness) // 실제 조명 + 게임용 밝기 광원
        {
            GameObject lightObject = new GameObject(name); // 오브젝트
            lightObject.transform.SetParent(parent, false); // 부모
            lightObject.transform.localPosition = position; // 위치
            Light light = lightObject.AddComponent<Light>(); // 조명
            light.type = LightType.Point; // 점광원
            light.range = range; // 범위
            light.intensity = intensity; // 세기
            light.color = color; // 색
            light.shadows = LightShadows.None; // 그림자 없음 (성능)
            BrightnessSource source = lightObject.AddComponent<BrightnessSource>(); // 게임용 밝기
            source.Configure(gameBrightness, range, true, light); // 고정 광원
            return light; // 반환
        }

        public static void Smoke(Transform parent, Vector3 position) // 굴뚝 연기 (URP 파티클, 셰이더가 없으면 생략)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit"); // 파티클 셰이더

            if (shader == null) // 없음
            {
                return; // 생략
            }

            EnsureFolder(); // 폴더
            string path = $"{ArtFolder}/Vic_SmokeParticle.mat"; // 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존

            if (material == null) // 생성
            {
                material = new Material(shader) { name = "Vic_SmokeParticle" }; // 재질
                AssetDatabase.CreateAsset(material, path); // 저장
            }

            material.shader = shader; // 셰이더
            material.SetFloat("_Surface", 1f); // 반투명
            material.SetFloat("_Blend", 0f); // 알파 혼합
            material.SetOverrideTag("RenderType", "Transparent"); // 투명
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); // 혼합
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); // 혼합
            material.SetInt("_ZWrite", 0); // 깊이 기록 끔
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); // 투명 키워드
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 투명 순서
            material.SetColor("_BaseColor", new Color(0.32f, 0.31f, 0.3f, 0.55f)); // 색
            EditorUtility.SetDirty(material); // 저장 대상

            GameObject smokeObject = new GameObject("Smoke"); // 오브젝트
            smokeObject.transform.SetParent(parent, false); // 부모
            smokeObject.transform.localPosition = position; // 위치
            smokeObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 위로 분출
            ParticleSystem system = smokeObject.AddComponent<ParticleSystem>(); // 파티클
            ParticleSystem.MainModule main = system.main; // 기본
            main.loop = true; // 반복
            main.prewarm = true; // 미리 채움
            main.startLifetime = 14f; // 수명
            main.startSpeed = 1.6f; // 속도
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.4f); // 크기
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // 회전
            main.startColor = new Color(0.3f, 0.29f, 0.28f, 0.6f); // 색
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 월드
            main.maxParticles = 80; // 최대
            ParticleSystem.EmissionModule emission = system.emission; // 방출
            emission.rateOverTime = 4f; // 초당
            ParticleSystem.ShapeModule shape = system.shape; // 모양
            shape.shapeType = ParticleSystemShapeType.Cone; // 원뿔
            shape.angle = 8f; // 각
            shape.radius = 0.5f; // 반지름
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime; // 크기 변화
            size.enabled = true; // 켬
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f)); // 커짐
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime; // 색 변화
            color.enabled = true; // 켬
            Gradient gradient = new Gradient(); // 흐려짐
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.1f), new GradientAlphaKey(0f, 1f) }); // 투명도
            color.color = gradient; // 적용
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime; // 바람
            velocity.enabled = true; // 켬
            velocity.space = ParticleSystemSimulationSpace.World; // 월드
            velocity.x = new ParticleSystem.MinMaxCurve(0.6f, 1.0f); // 바람 방향
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f); // 기본
            velocity.z = new ParticleSystem.MinMaxCurve(0.1f, 0.3f); // 바람 방향
            ParticleSystemRenderer renderer = smokeObject.GetComponent<ParticleSystemRenderer>(); // 렌더러
            renderer.sharedMaterial = material; // 재질
            renderer.renderMode = ParticleSystemRenderMode.Billboard; // 빌보드
        }

        public static IndoorBrightnessArea IndoorArea(Transform parent, string areaName, Vector3 center, Vector3 size) // 실내 밝기 영역 (자연광 제외)
        {
            GameObject areaObject = new GameObject("IndoorArea"); // 오브젝트
            areaObject.transform.SetParent(parent, false); // 부모
            IndoorBrightnessArea area = areaObject.AddComponent<IndoorBrightnessArea>(); // 영역 (상자 충돌체 자동)
            area.Configure(areaName, size, center); // 구성 (트리거)
            return area; // 반환
        }

        public static void Wall(Transform frame, string name, float length, float bottom, float top, float thickness, IList<WallOpening> openings, Material material, bool collider = true) // 구멍 뚫린 벽 (frame: x = 벽 길이 방향, z = 바깥쪽)
        {
            List<WallOpening> sorted = new List<WallOpening>(openings ?? new List<WallOpening>()); // 정렬 사본
            sorted.Sort((a, b) => a.Center.CompareTo(b.Center)); // 왼쪽부터
            float cursor = -length * 0.5f; // 현재 위치
            int index = 0; // 조각 번호

            foreach (WallOpening opening in sorted) // 구멍 순회
            {
                float holeBottom = Mathf.Clamp(opening.Bottom, bottom, top); // 이 벽 높이 안의 구멍 아래
                float holeTop = Mathf.Clamp(opening.Top, bottom, top); // 이 벽 높이 안의 구멍 위

                if (holeTop - holeBottom < 0.01f) // 이 벽 높이와 겹치지 않는 구멍 (예: 징두리 위쪽 창)
                {
                    continue; // 구멍 없이 이어서 쌓음
                }

                float left = opening.Center - (opening.Width * 0.5f); // 구멍 왼쪽
                float right = opening.Center + (opening.Width * 0.5f); // 구멍 오른쪽
                Segment(frame, $"{name}_{index++}", cursor, left, bottom, top, thickness, material, collider); // 구멍 앞 벽

                if (holeBottom > bottom + 0.01f) // 구멍 아래 벽
                {
                    Segment(frame, $"{name}_{index++}", left, right, bottom, holeBottom, thickness, material, collider); // 생성
                }

                if (holeTop < top - 0.01f) // 구멍 위 벽
                {
                    Segment(frame, $"{name}_{index++}", left, right, holeTop, top, thickness, material, collider); // 생성
                }

                cursor = right; // 다음
            }

            Segment(frame, $"{name}_{index}", cursor, length * 0.5f, bottom, top, thickness, material, collider); // 마지막 벽
        }

        private static void Segment(Transform frame, string name, float from, float to, float bottom, float top, float thickness, Material material, bool collider) // 벽 조각
        {
            if (to - from < 0.01f || top - bottom < 0.01f) // 너무 작음
            {
                return; // 생략
            }

            Box(frame, name, new Vector3((from + to) * 0.5f, (bottom + top) * 0.5f, 0f), new Vector3(to - from, top - bottom, thickness), material, collider); // 생성
        }

        private static Mesh PrismMesh() // 밑변 z(-0.5~0.5) · 높이 y(0~1) · 두께 x(-0.5~0.5) 삼각기둥
        {
            if (prismMesh != null) // 캐시
            {
                return prismMesh; // 반환
            }

            EnsureFolder(); // 폴더
            string path = $"{ArtFolder}/GablePrism.asset"; // 경로
            prismMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); // 기존

            if (prismMesh != null) // 있음
            {
                return prismMesh; // 반환
            }

            Vector3 a0 = new Vector3(-0.5f, 0f, -0.5f), b0 = new Vector3(-0.5f, 0f, 0.5f), c0 = new Vector3(-0.5f, 1f, 0f); // 왼쪽 삼각형
            Vector3 a1 = new Vector3(0.5f, 0f, -0.5f), b1 = new Vector3(0.5f, 0f, 0.5f), c1 = new Vector3(0.5f, 1f, 0f); // 오른쪽 삼각형
            List<Vector3> vertices = new List<Vector3>(); // 정점
            List<int> triangles = new List<int>(); // 삼각형
            AddFace(vertices, triangles, a0, b0, c0); // 왼쪽 면 (-x) · 법선 = Cross(b-a, c-a)
            AddFace(vertices, triangles, a1, c1, b1); // 오른쪽 면 (+x)
            AddQuad(vertices, triangles, a0, a1, b1, b0); // 밑면 (-y)
            AddQuad(vertices, triangles, b0, b1, c1, c0); // 앞 경사면 (+z)
            AddQuad(vertices, triangles, c0, c1, a1, a0); // 뒤 경사면 (-z)
            prismMesh = new Mesh { name = "GablePrism" }; // 메시
            prismMesh.SetVertices(vertices); // 정점
            prismMesh.SetTriangles(triangles, 0); // 삼각형
            prismMesh.RecalculateNormals(); // 법선 (면마다 정점 분리)
            prismMesh.RecalculateBounds(); // 범위
            AssetDatabase.CreateAsset(prismMesh, path); // 저장
            return prismMesh; // 반환
        }

        private static void AddFace(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c) // 삼각형 면
        {
            int start = vertices.Count; // 시작
            vertices.Add(a); // 정점
            vertices.Add(b); // 정점
            vertices.Add(c); // 정점
            triangles.Add(start); // 삼각형
            triangles.Add(start + 1); // 삼각형
            triangles.Add(start + 2); // 삼각형
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d) // 사각형 면
        {
            AddFace(vertices, triangles, a, b, c); // 반쪽
            AddFace(vertices, triangles, a, c, d); // 반쪽
        }

        private static void EnsureFolder() // 폴더 생성
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) // 없음
            {
                AssetDatabase.CreateFolder("Assets/ProjectI/Art/Generated", "Day34"); // 생성
            }
        }
    }

    public struct WallOpening // 벽 구멍 (벽 길이 방향 중심·폭, 아래·위 높이)
    {
        public float Center; // 중심
        public float Width; // 폭
        public float Bottom; // 아래
        public float Top; // 위
        public bool IsDoor; // 문 여부

        public WallOpening(float center, float width, float bottom, float top, bool isDoor = false) // 생성자
        {
            Center = center; // 중심
            Width = width; // 폭
            Bottom = bottom; // 아래
            Top = top; // 위
            IsDoor = isDoor; // 문
        }
    }
}
