using System.Collections.Generic; // 캐시
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools.Town // 34일차 마을 제작 도구
{
    public static class TownPalette // 마을 재질 모음
    {
        private static readonly Dictionary<Material, Material> Shades = new Dictionary<Material, Material>(); // 지붕 음영 캐시

        public static Material Ground => TownKit.Mat("Town_Ground", new Color(0.36f, 0.32f, 0.25f), 0f, 0.1f); // 흙 지면
        public static Material Grass => TownKit.Mat("Town_Grass", new Color(0.30f, 0.42f, 0.20f), 0f, 0.1f); // 풀밭
        public static Material Road => TownKit.Mat("Town_RoadCobble", new Color(0.34f, 0.33f, 0.31f), 0f, 0.2f); // 도로 돌길
        public static Material RoadSeam => TownKit.Mat("Town_RoadSeam", new Color(0.22f, 0.21f, 0.20f), 0f, 0.1f); // 돌길 줄눈
        public static Material Curb => TownKit.Mat("Town_Curb", new Color(0.56f, 0.54f, 0.50f), 0f, 0.2f); // 경계석
        public static Material Sidewalk => TownKit.Mat("Town_Sidewalk", new Color(0.63f, 0.59f, 0.52f), 0f, 0.15f); // 보도
        public static Material Plinth => TownKit.Mat("Town_Plinth", new Color(0.47f, 0.45f, 0.42f), 0f, 0.2f); // 기단석
        public static Material Brick => TownKit.Mat("Town_Brick", new Color(0.55f, 0.30f, 0.22f), 0f, 0.15f); // 벽돌
        public static Material BrickDark => TownKit.Mat("Town_BrickDark", new Color(0.38f, 0.23f, 0.18f), 0f, 0.15f); // 어두운 벽돌
        public static Material StoneWall => TownKit.Mat("Town_StoneWall", new Color(0.52f, 0.50f, 0.46f), 0f, 0.15f); // 돌담
        public static Material Plaster => TownKit.Mat("Town_Plaster", new Color(0.87f, 0.81f, 0.69f), 0f, 0.1f); // 회벽
        public static Material PlasterBlue => TownKit.Mat("Town_PlasterBlue", new Color(0.63f, 0.72f, 0.77f), 0f, 0.1f); // 푸른 회벽
        public static Material PlasterGreen => TownKit.Mat("Town_PlasterGreen", new Color(0.66f, 0.74f, 0.60f), 0f, 0.1f); // 녹색 회벽
        public static Material PlasterRose => TownKit.Mat("Town_PlasterRose", new Color(0.82f, 0.66f, 0.62f), 0f, 0.1f); // 분홍 회벽
        public static Material Timber => TownKit.Mat("Town_Timber", new Color(0.29f, 0.19f, 0.11f), 0f, 0.25f); // 목재 골조
        public static Material TimberGray => TownKit.Mat("Town_TimberGray", new Color(0.30f, 0.28f, 0.26f), 0f, 0.25f); // 회색 목재
        public static Material FloorWood => TownKit.Mat("Town_FloorWood", new Color(0.52f, 0.38f, 0.24f), 0f, 0.3f); // 마루
        public static Material FloorSeam => TownKit.Mat("Town_FloorSeam", new Color(0.30f, 0.21f, 0.13f), 0f, 0.1f); // 마루 줄눈
        public static Material Ceiling => TownKit.Mat("Town_Ceiling", new Color(0.62f, 0.52f, 0.40f), 0f, 0.1f); // 천장
        public static Material DoorWood => TownKit.Mat("Town_DoorWood", new Color(0.40f, 0.25f, 0.14f), 0f, 0.3f); // 문
        public static Material DoorGreen => TownKit.Mat("Town_DoorGreen", new Color(0.20f, 0.36f, 0.30f), 0f, 0.3f); // 녹색 문
        public static Material DoorRed => TownKit.Mat("Town_DoorRed", new Color(0.52f, 0.18f, 0.16f), 0f, 0.3f); // 붉은 문
        public static Material WoodLight => TownKit.Mat("Town_WoodLight", new Color(0.66f, 0.52f, 0.34f), 0f, 0.25f); // 밝은 나무
        public static Material WoodDark => TownKit.Mat("Town_WoodDark", new Color(0.26f, 0.17f, 0.10f), 0f, 0.25f); // 어두운 나무
        public static Material RoofTile => TownKit.Mat("Town_RoofTile", new Color(0.46f, 0.20f, 0.15f), 0f, 0.2f); // 붉은 기와
        public static Material RoofSlate => TownKit.Mat("Town_RoofSlate", new Color(0.26f, 0.28f, 0.32f), 0f, 0.3f); // 슬레이트
        public static Material RoofMoss => TownKit.Mat("Town_RoofMoss", new Color(0.24f, 0.33f, 0.26f), 0f, 0.2f); // 이끼 낀 지붕
        public static Material Glass => TownKit.Mat("Town_Glass", new Color(0.42f, 0.55f, 0.62f), 0.1f, 0.9f); // 유리
        public static Material Iron => TownKit.Mat("Town_Iron", new Color(0.14f, 0.14f, 0.15f), 0.7f, 0.4f); // 쇠
        public static Material Brass => TownKit.Mat("Town_Brass", new Color(0.80f, 0.62f, 0.28f), 0.9f, 0.7f); // 놋쇠
        public static Material LampGlow => TownKit.Mat("Town_LampGlow", new Color(1f, 0.85f, 0.55f), 0f, 0.5f, new Color(2.6f, 1.9f, 1.0f)); // 등불
        public static Material SignBoard => TownKit.Mat("Town_SignBoard", new Color(0.24f, 0.15f, 0.09f), 0f, 0.3f); // 간판
        public static Material Leaf => TownKit.Mat("Town_Leaf", new Color(0.24f, 0.42f, 0.18f), 0f, 0.2f); // 잎
        public static Material LeafDark => TownKit.Mat("Town_LeafDark", new Color(0.17f, 0.31f, 0.14f), 0f, 0.2f); // 짙은 잎
        public static Material Bark => TownKit.Mat("Town_Bark", new Color(0.30f, 0.22f, 0.15f), 0f, 0.1f); // 나무껍질
        public static Material Water => TownKit.Mat("Town_Water", new Color(0.18f, 0.30f, 0.36f), 0f, 0.95f); // 물
        public static Material Puddle => TownKit.Mat("Town_Puddle", new Color(0.14f, 0.15f, 0.16f), 0f, 0.95f); // 웅덩이
        public static Material Hay => TownKit.Mat("Town_Hay", new Color(0.78f, 0.66f, 0.36f), 0f, 0.1f); // 건초
        public static Material Sack => TownKit.Mat("Town_Sack", new Color(0.70f, 0.62f, 0.46f), 0f, 0.1f); // 자루
        public static Material Paper => TownKit.Mat("Town_Paper", new Color(0.92f, 0.89f, 0.80f), 0f, 0.1f); // 종이
        public static Material ClothRed => TownKit.Mat("Town_ClothRed", new Color(0.66f, 0.22f, 0.20f), 0f, 0.1f); // 붉은 천
        public static Material ClothBlue => TownKit.Mat("Town_ClothBlue", new Color(0.24f, 0.36f, 0.58f), 0f, 0.1f); // 푸른 천
        public static Material ClothCream => TownKit.Mat("Town_ClothCream", new Color(0.88f, 0.84f, 0.72f), 0f, 0.1f); // 흰 천
        public static Material ClothGreen => TownKit.Mat("Town_ClothGreen", new Color(0.30f, 0.46f, 0.30f), 0f, 0.1f); // 녹색 천
        public static Material Gold => TownKit.Mat("Town_Gold", new Color(0.92f, 0.74f, 0.30f), 1f, 0.8f); // 금
        public static Material Rope => TownKit.Mat("Town_Rope", new Color(0.62f, 0.52f, 0.36f), 0f, 0.1f); // 밧줄
        public static Material Target => TownKit.Mat("Town_TargetRed", new Color(0.78f, 0.20f, 0.16f), 0f, 0.2f); // 과녁 붉은색

        public static Material VictorianBrick => TownKit.Mat("Vic_BrickRed", new Color(0.45f, 0.23f, 0.17f), 0f, 0.12f); // 붉은 벽돌 (그을음)
        public static Material SootBrick => TownKit.Mat("Vic_BrickSoot", new Color(0.26f, 0.18f, 0.15f), 0f, 0.1f); // 검게 그을린 벽돌
        public static Material StockBrick => TownKit.Mat("Vic_BrickStock", new Color(0.58f, 0.48f, 0.33f), 0f, 0.1f); // 누런 런던 벽돌
        public static Material BrickCourse => TownKit.Mat("Vic_BrickCourse", new Color(0.18f, 0.12f, 0.10f), 0f, 0.05f); // 벽돌 줄눈 그림자
        public static Material EngineeringBrick => TownKit.Mat("Vic_EngineeringBrick", new Color(0.20f, 0.20f, 0.23f), 0f, 0.3f); // 푸른빛 기초 벽돌
        public static Material Dressing => TownKit.Mat("Vic_StoneDressing", new Color(0.74f, 0.71f, 0.63f), 0f, 0.15f); // 석재 장식 (포틀랜드석)
        public static Material DressingSoot => TownKit.Mat("Vic_StoneSoot", new Color(0.52f, 0.50f, 0.45f), 0f, 0.12f); // 그을린 석재
        public static Material Slate => TownKit.Mat("Vic_Slate", new Color(0.20f, 0.22f, 0.26f), 0.05f, 0.35f); // 슬레이트
        public static Material SlateEdge => TownKit.Mat("Vic_SlateEdge", new Color(0.13f, 0.14f, 0.17f), 0.05f, 0.3f); // 슬레이트 줄
        public static Material Lead => TownKit.Mat("Vic_Lead", new Color(0.30f, 0.32f, 0.33f), 0.4f, 0.35f); // 납 (용마루·물받이)
        public static Material ChimneyPot => TownKit.Mat("Vic_ChimneyPot", new Color(0.62f, 0.33f, 0.20f), 0f, 0.2f); // 굴뚝 항아리 (테라코타)
        public static Material Soot => TownKit.Mat("Vic_Soot", new Color(0.06f, 0.06f, 0.06f), 0f, 0.05f); // 그을음
        public static Material PaintGreen => TownKit.Mat("Vic_PaintGreen", new Color(0.10f, 0.24f, 0.17f), 0f, 0.55f); // 병 녹색 칠
        public static Material PaintOxblood => TownKit.Mat("Vic_PaintOxblood", new Color(0.34f, 0.09f, 0.09f), 0f, 0.55f); // 적갈색 칠
        public static Material PaintNavy => TownKit.Mat("Vic_PaintNavy", new Color(0.11f, 0.15f, 0.27f), 0f, 0.55f); // 남색 칠
        public static Material PaintBlack => TownKit.Mat("Vic_PaintBlack", new Color(0.06f, 0.06f, 0.07f), 0f, 0.6f); // 검은 칠
        public static Material PaintGray => TownKit.Mat("Vic_PaintGray", new Color(0.30f, 0.31f, 0.31f), 0f, 0.4f); // 바랜 회색 칠
        public static Material PaintCream => TownKit.Mat("Vic_PaintCream", new Color(0.86f, 0.82f, 0.71f), 0f, 0.45f); // 크림 칠 (창틀)
        public static Material WindowDark => TownKit.Mat("Vic_WindowGlass", new Color(0.09f, 0.11f, 0.13f), 0.2f, 0.92f); // 어두운 창유리
        public static Material WindowLit => TownKit.Mat("Vic_WindowLit", new Color(0.55f, 0.42f, 0.24f), 0f, 0.6f, new Color(0.9f, 0.62f, 0.3f)); // 불 켜진 창
        public static Material ShopGlass => TownKit.Mat("Vic_ShopGlass", new Color(0.20f, 0.24f, 0.26f), 0.2f, 0.95f); // 진열창 유리
        public static Material AwningCream => TownKit.Mat("Vic_AwningCream", new Color(0.82f, 0.77f, 0.64f), 0f, 0.1f); // 차양 흰 줄
        public static Material AwningRed => TownKit.Mat("Vic_AwningRed", new Color(0.52f, 0.14f, 0.13f), 0f, 0.1f); // 차양 붉은 줄
        public static Material AwningGreen => TownKit.Mat("Vic_AwningGreen", new Color(0.16f, 0.32f, 0.22f), 0f, 0.1f); // 차양 녹색 줄
        public static Material AwningBlue => TownKit.Mat("Vic_AwningBlue", new Color(0.16f, 0.22f, 0.38f), 0f, 0.1f); // 차양 남색 줄
        public static Material Wallpaper => TownKit.Mat("Vic_Wallpaper", new Color(0.46f, 0.33f, 0.26f), 0f, 0.1f); // 실내 벽지
        public static Material WallpaperGreen => TownKit.Mat("Vic_WallpaperGreen", new Color(0.30f, 0.36f, 0.28f), 0f, 0.1f); // 녹색 벽지
        public static Material Wainscot => TownKit.Mat("Vic_Wainscot", new Color(0.24f, 0.15f, 0.09f), 0f, 0.45f); // 징두리 판벽
        public static Material FloorDark => TownKit.Mat("Vic_FloorDark", new Color(0.33f, 0.23f, 0.15f), 0f, 0.35f); // 어두운 마루
        public static Material CastIron => TownKit.Mat("Vic_CastIron", new Color(0.08f, 0.09f, 0.10f), 0.6f, 0.45f); // 주철
        public static Material PostRed => TownKit.Mat("Vic_PostRed", new Color(0.62f, 0.08f, 0.07f), 0.1f, 0.6f); // 우체통 빨강
        public static Material Setts => TownKit.Mat("Vic_Setts", new Color(0.25f, 0.24f, 0.23f), 0f, 0.35f); // 도로 돌 (비에 젖은)
        public static Material Flagstone => TownKit.Mat("Vic_Flagstone", new Color(0.45f, 0.44f, 0.41f), 0f, 0.2f); // 보도 판석
        public static Material Smoke => TownKit.Mat("Vic_Smoke", new Color(0.35f, 0.34f, 0.33f), 0f, 0f); // 굴뚝 연기 덩어리

        public static Material RoofShade(Material roof) // 지붕 음영 (줄눈·용마루)
        {
            if (roof == null) // 확인
            {
                return RoofSlate; // 기본
            }

            if (Shades.TryGetValue(roof, out Material shade) && shade != null) // 캐시
            {
                return shade; // 반환
            }

            Color color = roof.color * 0.72f; // 어둡게
            color.a = 1f; // 불투명
            shade = TownKit.Mat($"{roof.name}_Shade", color, 0f, 0.2f); // 생성
            Shades[roof] = shade; // 캐시
            return shade; // 반환
        }

        public static void ResetCache() // 캐시 초기화
        {
            Shades.Clear(); // 초기화
        }
    }
}
