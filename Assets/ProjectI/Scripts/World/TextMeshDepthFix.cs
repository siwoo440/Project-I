using System.Collections.Generic; // 글꼴별 재질 목록 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 로드 이벤트 참조

namespace ProjectI.World // 월드 기능 네임스페이스
{
    public static class TextMeshDepthFix // 3D 글자가 벽을 뚫고 보이지 않도록 깊이 검사 재질로 교체
    {
        public const string MaterialResourcePath = "Text/TextMeshDepth"; // Resources 안 재질 경로
        public const string ShaderName = "ProjectI/TextMeshDepth"; // 깊이 검사 글자 셰이더
        private static readonly Dictionary<Font, Material> materials = new Dictionary<Font, Material>(); // 글꼴별 재질
        private static Material template; // 기본 재질 (기본 글꼴은 이 재질을 그대로 사용)
        private static Font builtinFont; // 기본 글꼴

        public static Font BuiltinFont // 기본 글꼴 (한글은 OS 글꼴로 대체 표시)
        {
            get
            {
                if (builtinFont == null) // 처음
                {
                    builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 불러오기
                }

                return builtinFont; // 반환
            }
        }

        public static Material Template // 기본 재질
        {
            get
            {
                if (template == null) // 처음
                {
                    template = Resources.Load<Material>(MaterialResourcePath); // 빌드에 포함되는 재질
                }

                return template; // 반환
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 게임 시작 시
        private static void Install() // 씬 로드마다 적용
        {
            materials.Clear(); // 이전 실행 정리
            SceneManager.sceneLoaded -= OnSceneLoaded; // 중복 방지
            SceneManager.sceneLoaded += OnSceneLoaded; // 추가 로드 씬 포함
            Font.textureRebuilt -= OnFontTextureRebuilt; // 중복 방지
            Font.textureRebuilt += OnFontTextureRebuilt; // 글꼴 텍스처 갱신 추적

            for (int i = 0; i < SceneManager.sceneCount; i++) // 이미 열린 씬
            {
                ApplyToScene(SceneManager.GetSceneAt(i)); // 적용
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) // 씬 로드
        {
            ApplyToScene(scene); // 적용
        }

        public static int ApplyToScene(Scene scene) // 씬 안 모든 3D 글자에 적용
        {
            if (!scene.IsValid() || !scene.isLoaded) // 로드 안 됨
            {
                return 0; // 생략
            }

            int count = 0; // 적용 수

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트
            {
                foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true)) // 꺼진 것 포함
                {
                    if (Apply(text)) // 적용
                    {
                        count++; // 수
                    }
                }
            }

            return count; // 반환
        }

        public static bool Apply(TextMesh text) // 글자 하나에 적용 (바뀌면 true)
        {
            if (text == null) // 없음
            {
                return false; // 생략
            }

            MeshRenderer renderer = text.GetComponent<MeshRenderer>(); // 렌더러

            if (renderer == null) // 없음
            {
                return false; // 생략
            }

            if (text.font == null) // 글꼴 미지정 (기본 글꼴로 표시되던 글자)
            {
                text.font = BuiltinFont; // 기본 글꼴 지정
            }

            Material material = MaterialFor(text.font); // 재질

            if (material == null || renderer.sharedMaterial == material) // 없음 또는 이미 적용
            {
                return false; // 생략
            }

            renderer.sharedMaterial = material; // 교체
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 글자 그림자 없음
            return true; // 적용됨
        }

        public static Material MaterialFor(Font font) // 글꼴에 맞는 깊이 검사 재질
        {
            if (font == null) // 없음
            {
                return null; // 생략
            }

            if (materials.TryGetValue(font, out Material cached) && cached != null) // 캐시
            {
                RefreshTexture(font, cached); // 텍스처 확인
                return cached; // 반환
            }

            Material material; // 새 재질

            if (font == BuiltinFont && Template != null) // 기본 글꼴은 재질 에셋 그대로 (씬에 저장된 참조와 같음)
            {
                material = Template; // 에셋
            }
            else
            {
                Shader shader = Template != null ? Template.shader : Shader.Find(ShaderName); // 셰이더

                if (shader == null) // 셰이더 없음
                {
                    return null; // 기존 재질 유지
                }

                material = new Material(shader) { name = $"TextMeshDepth_{font.name}", hideFlags = HideFlags.DontSave }; // 글꼴 전용
            }

            RefreshTexture(font, material); // 글꼴 텍스처 연결
            materials[font] = material; // 캐시
            return material; // 반환
        }

        private static void RefreshTexture(Font font, Material material) // 글꼴 텍스처 연결
        {
            Texture texture = font.material != null ? font.material.mainTexture : null; // 글꼴 텍스처

            if (texture != null && material.mainTexture != texture) // 다름
            {
                material.mainTexture = texture; // 연결
            }
        }

        private static void OnFontTextureRebuilt(Font font) // 동적 글꼴 텍스처 재생성
        {
            if (materials.TryGetValue(font, out Material material) && material != null) // 사용 중인 글꼴
            {
                RefreshTexture(font, material); // 다시 연결
            }
        }
    }
}
