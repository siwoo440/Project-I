using ProjectI.World; // 글자 깊이 재질 기능 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시
    public static class TextMeshDepthFixTool // 에디터에서도 3D 글자가 벽을 뚫고 보이지 않도록
    {
        private const string ScenesFolder = "Assets/ProjectI/Scenes"; // 대상 씬 폴더

        static TextMeshDepthFixTool() // 에디터 로드
        {
            EditorApplication.delayCall += () => LinkTemplateTexture(); // 에셋 로드 후 연결
            Font.textureRebuilt += font => // 글꼴 텍스처 재생성
            {
                if (font == TextMeshDepthFix.BuiltinFont) // 기본 글꼴
                {
                    LinkTemplateTexture(); // 다시 연결
                }
            };
        }

        public static Material LinkTemplateTexture() // 재질 에셋에 기본 글꼴 텍스처 연결 (씬 뷰 표시용)
        {
            Material material = TextMeshDepthFix.Template; // 재질 에셋

            if (material == null) // 아직 없음
            {
                return null; // 생략
            }

            Font font = TextMeshDepthFix.BuiltinFont; // 기본 글꼴
            Texture texture = font != null && font.material != null ? font.material.mainTexture : null; // 글꼴 텍스처

            if (texture != null && material.mainTexture != texture) // 다름
            {
                material.mainTexture = texture; // 연결
            }

            return material; // 반환
        }

        public static void ApplyTo(TextMesh text) // 빌더에서 새 글자 만들 때 사용
        {
            LinkTemplateTexture(); // 텍스처 연결
            TextMeshDepthFix.Apply(text); // 재질 교체
        }

        [MenuItem("Project I/Tools/Fix Text Depth In All Scenes")] // 메뉴
        public static void FixAllScenes() // 모든 씬의 3D 글자 재질 교체 후 저장
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장 안 한 변경 확인
            {
                return; // 취소
            }

            LinkTemplateTexture(); // 텍스처 연결
            int total = 0; // 전체 수

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder })) // 씬 목록
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); // 경로
                Scene scene = SceneManager.GetSceneByPath(path); // 이미 열린 씬
                bool openedHere = !scene.IsValid() || !scene.isLoaded; // 여기서 연 씬인지

                if (openedHere) // 닫힌 씬
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); // 추가로 열기
                }

                int count = TextMeshDepthFix.ApplyToScene(scene); // 적용
                total += count; // 합계

                if (count > 0) // 바뀜
                {
                    EditorSceneManager.MarkSceneDirty(scene); // 변경 표시
                    EditorSceneManager.SaveScene(scene); // 저장
                }

                Debug.Log($"[Project I] 글자 깊이 재질 적용: {path} — {count}개"); // 보고

                if (openedHere) // 여기서 연 씬 닫기
                {
                    EditorSceneManager.CloseScene(scene, true); // 닫기
                }
            }

            AssetDatabase.SaveAssets(); // 재질 저장
            Debug.Log($"[Project I] 글자 깊이 재질 적용 완료 — 총 {total}개"); // 보고
        }
    }
}
