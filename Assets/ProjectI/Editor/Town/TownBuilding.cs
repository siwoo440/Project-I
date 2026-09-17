using System.Collections.Generic; // 목록
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools.Town // 34일차 마을 제작 도구
{
    public enum TownFacade // 1층 정면 양식
    {
        Shopfront, // 칠한 목재 상점 정면 (진열창·간판 띠·차양)
        Office, // 석재 문틀·박공 머리의 사무소 정문
        Warehouse, // 아치 하역문·도르래 들보의 창고
    }

    public sealed class TownBuildingStyle // 빅토리아 시대 연립 건물 설정
    {
        public Material Brick; // 벽돌
        public Material Dressing; // 석재 장식 (모서리돌·층 띠·코니스·창 머리)
        public Material Paint; // 상점 정면·문틀 칠
        public Material DoorPaint; // 문 칠
        public Material AwningA; // 차양 줄무늬 1 (없으면 차양 없음)
        public Material AwningB; // 차양 줄무늬 2
        public Material Wallpaper; // 실내 벽지
        public Color SignColor = new Color(0.93f, 0.78f, 0.38f); // 금색 글자
        public TownFacade Facade = TownFacade.Shopfront; // 1층 양식
        public int Storeys = 2; // 층수
        public float RoofPitch = 40f; // 지붕 경사
        public int Dormers = 1; // 앞 지붕 도머창 수
        public bool Vacant; // 빈 점포 (임대 안내)
        public int LitSeed = 1; // 불 켜진 윗층 창 선택
    }

    public sealed class TownBuildingResult // 건물 생성 결과
    {
        public Transform Root; // 건물 루트 (앞면 = 로컬 +z)
        public Transform Interior; // 실내 기능 배치 루트 (바닥 윗면이 y=0)
        public float Width; // 폭 (x)
        public float Depth; // 깊이 (z)
        public float InnerBackZ; // 뒷벽 안쪽 면 z
        public float InnerSideX; // 옆벽 안쪽 면 x (양수)
        public float InnerFrontZ; // 앞벽 안쪽 면 z
        public float InnerHeight; // 실내 높이
    }

    public static class TownBuilding // 산업혁명기 영국풍 벽돌 연립 건물 (1층은 들어갈 수 있음)
    {
        public const float PlinthHeight = 0.3f; // 기단 높이
        public const float FloorTop = 0.35f; // 실내 바닥 윗면
        public const float WallThickness = 0.3f; // 벽 두께 (벽돌 0.24 + 실내 마감 0.06)
        public const float GroundTop = 4.5f; // 1층 윗면 (첫 층 띠)
        public const float UpperStorey = 3.4f; // 윗층 높이
        public const float DoorWidth = 1.7f; // 문 폭
        private const float DoorClear = 2.7f; // 문짝 높이
        private const float DoorTop = 3.45f; // 문 구멍 위 (채광창 포함)
        private const float DadoTop = 0.95f; // 징두리 높이
        private const float BrickOffset = 0.03f; // 벽돌층 중심 (프레임 기준)
        private const float Outer = WallThickness * 0.5f; // 벽 바깥 면 (프레임 기준)

        public static TownBuildingResult Build(Transform parent, string name, Vector3 groundPosition, float yaw, float width, float depth, TownBuildingStyle style, string sign) // 건물 한 채
        {
            Transform root = TownKit.Group(parent, name, groundPosition, yaw); // 루트
            int storeys = Mathf.Max(1, style.Storeys); // 층수
            float wallTop = GroundTop + ((storeys - 1) * UpperStorey); // 벽 윗면
            float halfWidth = width * 0.5f; // 반폭
            float halfDepth = depth * 0.5f; // 반깊이
            float sideLength = depth - (WallThickness * 2f); // 옆벽 길이
            float innerWidth = width - (WallThickness * 2f); // 실내 폭

            TownKit.Box(root, "Plinth", new Vector3(0f, (PlinthHeight - 0.25f) * 0.5f, 0f), new Vector3(width + 0.1f, PlinthHeight + 0.25f, depth + 0.1f), TownPalette.EngineeringBrick); // 기초 벽돌 (지면 아래까지)
            TownKit.Box(root, "Floor", new Vector3(0f, FloorTop - 0.05f, 0f), new Vector3(innerWidth + 0.02f, 0.1f, sideLength + 0.02f), TownPalette.FloorDark); // 실내 마루
            FloorBoards(root, innerWidth, sideLength); // 마루 줄눈

            Transform front = TownKit.Group(root, "Wall_Front", new Vector3(0f, 0f, halfDepth - Outer)); // 앞벽 (바깥 = +z)
            Transform back = TownKit.Group(root, "Wall_Back", new Vector3(0f, 0f, -(halfDepth - Outer)), 180f); // 뒷벽
            Transform left = TownKit.Group(root, "Wall_Left", new Vector3(-(halfWidth - Outer), 0f, 0f), -90f); // 왼쪽 벽 (로컬 +x = 앞쪽)
            Transform right = TownKit.Group(root, "Wall_Right", new Vector3(halfWidth - Outer, 0f, 0f), 90f); // 오른쪽 벽 (로컬 +x = 뒤쪽)

            List<WallOpening> frontOpenings = FrontOpenings(style, width); // 앞면 구멍
            List<WallOpening> backOpenings = new List<WallOpening> { new WallOpening(-width * 0.22f, 1.0f, 2.0f, 3.2f), new WallOpening(width * 0.22f, 1.0f, 2.0f, 3.2f) }; // 뒷면 높은 창
            List<WallOpening> sideOpenings = new List<WallOpening> { new WallOpening(0f, 1.2f, 1.4f, 3.1f) }; // 옆면 창

            BrickWall(front, width, innerWidth, wallTop, frontOpenings, style); // 앞벽
            BrickWall(back, width, innerWidth, wallTop, backOpenings, style); // 뒷벽
            BrickWall(left, sideLength, sideLength, wallTop, sideOpenings, style); // 왼쪽 벽
            BrickWall(right, sideLength, sideLength, wallTop, sideOpenings, style); // 오른쪽 벽

            switch (style.Facade) // 1층 정면
            {
                case TownFacade.Office: OfficeFront(front, width, frontOpenings, style, sign); break; // 사무소
                case TownFacade.Warehouse: WarehouseFront(front, width, wallTop, storeys, style, sign); break; // 창고
                default: Shopfront(front, width, frontOpenings, style, sign); break; // 상점
            }

            foreach (WallOpening opening in backOpenings) // 뒷면 1층 창
            {
                Sash(back, "Window_G", opening.Center, opening.Bottom, opening.Width, opening.Top - opening.Bottom, style, false, false, true); // 창
            }

            Sash(left, "Window_G", 0f, sideOpenings[0].Bottom, sideOpenings[0].Width, sideOpenings[0].Top - sideOpenings[0].Bottom, style, false, false, true); // 왼쪽 창
            Sash(right, "Window_G", 0f, sideOpenings[0].Bottom, sideOpenings[0].Width, sideOpenings[0].Top - sideOpenings[0].Bottom, style, false, false, true); // 오른쪽 창
            UpperWindows(front, back, left, right, width, sideLength, storeys, style); // 윗층 창
            Dressings(front, back, left, right, width, sideLength, wallTop, storeys, style); // 모서리돌·층 띠·코니스
            Rainwater(back, left, right, width, sideLength, wallTop); // 물받이·선홈통
            float apex = Roof(root, width, depth, wallTop, style); // 지붕
            Chimneys(root, width, apex); // 굴뚝

            Transform interior = TownKit.Group(root, "Interior", new Vector3(0f, FloorTop, 0f)); // 실내 루트
            float innerHeight = GroundTop - FloorTop; // 실내 높이
            TownKit.Box(interior, "Ceiling", new Vector3(0f, innerHeight - 0.05f, 0f), new Vector3(innerWidth, 0.1f, sideLength), TownPalette.PaintCream, false); // 천장
            CeilingCornice(interior, innerWidth, sideLength, innerHeight); // 천장 몰딩
            Transform area = TownKit.IndoorArea(interior, sign, new Vector3(0f, innerHeight * 0.5f, 0f), new Vector3(innerWidth, innerHeight, sideLength)).transform; // 실내 밝기 영역
            GasChandelier(area, new Vector3(0f, innerHeight - 0.1f, 0f), style.Vacant ? 0.9f : 1.8f); // 가스 샹들리에 (실내 영역의 자식)

            return new TownBuildingResult // 결과
            {
                Root = root, // 루트
                Interior = interior, // 실내
                Width = width, // 폭
                Depth = depth, // 깊이
                InnerBackZ = -halfDepth + WallThickness, // 뒷벽 안쪽
                InnerFrontZ = halfDepth - WallThickness, // 앞벽 안쪽
                InnerSideX = halfWidth - WallThickness, // 옆벽 안쪽
                InnerHeight = innerHeight, // 실내 높이
            };
        }

        private static List<WallOpening> FrontOpenings(TownBuildingStyle style, float width) // 1층 정면 구멍 (0번 = 문)
        {
            float half = width * 0.5f; // 반폭
            List<WallOpening> openings = new List<WallOpening>(); // 결과

            switch (style.Facade) // 양식별
            {
                case TownFacade.Warehouse: // 넓은 하역문
                    openings.Add(new WallOpening(0f, 3.0f, PlinthHeight, 3.6f, true)); // 하역문
                    openings.Add(new WallOpening(-half * 0.62f, 1.1f, 1.2f, 3.0f)); // 작은 창
                    openings.Add(new WallOpening(half * 0.62f, 1.1f, 1.2f, 3.0f)); // 작은 창
                    break;
                case TownFacade.Office: // 가운데 문 + 오르내리창
                    openings.Add(new WallOpening(0f, DoorWidth, PlinthHeight, DoorTop, true)); // 문
                    openings.Add(new WallOpening(-width * 0.28f, 1.3f, 1.1f, 3.3f)); // 창
                    openings.Add(new WallOpening(width * 0.28f, 1.3f, 1.1f, 3.3f)); // 창
                    break;
                default: // 상점: 문 양쪽 진열창
                    float inner = (DoorWidth * 0.5f) + 0.35f; // 진열창 안쪽 끝
                    float outer = half - 0.62f; // 진열창 바깥 끝
                    openings.Add(new WallOpening(0f, DoorWidth, PlinthHeight, DoorTop, true)); // 문
                    openings.Add(new WallOpening(-(inner + outer) * 0.5f, outer - inner, 0.95f, DoorTop)); // 왼쪽 진열창
                    openings.Add(new WallOpening((inner + outer) * 0.5f, outer - inner, 0.95f, DoorTop)); // 오른쪽 진열창
                    break;
            }

            return openings; // 반환
        }

        private static void BrickWall(Transform frame, float length, float innerLength, float wallTop, List<WallOpening> openings, TownBuildingStyle style) // 벽돌 + 실내 벽지·징두리
        {
            Transform brick = TownKit.Group(frame, "Brick", new Vector3(0f, 0f, BrickOffset)); // 벽돌층
            TownKit.Wall(brick, "Brick", length, PlinthHeight, wallTop, WallThickness - 0.06f, openings, style.Brick); // 벽돌 (충돌)
            BrickCourses(brick, length, wallTop, openings); // 벽돌 줄 그림자

            Transform lining = TownKit.Group(frame, "Lining", new Vector3(0f, 0f, -Outer + 0.035f)); // 벽지층
            TownKit.Wall(lining, "Paper", innerLength, DadoTop, GroundTop, 0.05f, openings, style.Wallpaper != null ? style.Wallpaper : TownPalette.Wallpaper, false); // 벽지
            Transform dado = TownKit.Group(frame, "Wainscot", new Vector3(0f, 0f, -Outer + 0.03f)); // 징두리층
            TownKit.Wall(dado, "Panel", innerLength, FloorTop, DadoTop, 0.06f, openings, TownPalette.Wainscot, false); // 징두리 판벽
            Transform rail = TownKit.Group(frame, "DadoRail", new Vector3(0f, 0f, -Outer + 0.02f)); // 징두리 띠
            TownKit.Wall(rail, "Rail", innerLength, DadoTop - 0.03f, DadoTop + 0.03f, 0.08f, openings, TownPalette.WoodDark, false); // 띠
        }

        private static void BrickCourses(Transform brick, float length, float wallTop, List<WallOpening> openings) // 1.2m 마다 어두운 벽돌 줄 (벽돌 질감 대신)
        {
            float z = ((WallThickness - 0.06f) * 0.5f) + 0.004f; // 벽 바깥 면
            Transform group = TownKit.Group(brick, "Courses", new Vector3(0f, 0f, z)); // 묶음

            for (float y = PlinthHeight + 0.9f; y < wallTop - 0.3f; y += 0.9f) // 줄
            {
                TownKit.Wall(group, "Course", length, y, y + 0.025f, 0.006f, openings, TownPalette.BrickCourse, false); // 구멍을 피한 얇은 띠
            }
        }

        private static void Shopfront(Transform front, float width, List<WallOpening> openings, TownBuildingStyle style, string sign) // 칠한 목재 상점 정면
        {
            Transform group = TownKit.Group(front, "Shopfront", Vector3.zero); // 묶음
            float half = width * 0.5f; // 반폭
            float pilasterX = half - 0.34f; // 기둥 위치

            for (int side = -1; side <= 1; side += 2) // 양쪽 기둥
            {
                TownKit.Box(group, "Pilaster", new Vector3(side * pilasterX, (PlinthHeight + 4.35f) * 0.5f, Outer + 0.06f), new Vector3(0.44f, 4.35f - PlinthHeight, 0.12f), style.Paint); // 기둥 (충돌)
                TownKit.Box(group, "PilasterBase", new Vector3(side * pilasterX, PlinthHeight + 0.2f, Outer + 0.08f), new Vector3(0.52f, 0.4f, 0.16f), style.Paint, false); // 기둥 받침
                TownKit.Box(group, "Console", new Vector3(side * pilasterX, 4.1f, Outer + 0.16f), new Vector3(0.5f, 0.6f, 0.32f), style.Paint, false); // 까치발
                TownKit.Box(group, "ConsoleScroll", new Vector3(side * pilasterX, 3.86f, Outer + 0.3f), new Vector3(0.36f, 0.14f, 0.1f), style.Dressing, false); // 까치발 장식
            }

            for (int index = 1; index < openings.Count; index++) // 진열창
            {
                DisplayWindow(group, openings[index], style); // 창
            }

            Doorway(group, openings[0], style, false); // 문

            float fasciaWidth = width - 1.24f; // 간판 띠 폭
            TownKit.Box(group, "Fascia", new Vector3(0f, 3.92f, Outer + 0.08f), new Vector3(fasciaWidth, 0.72f, 0.12f), style.Paint, false); // 간판 띠
            TownKit.Box(group, "FasciaInset", new Vector3(0f, 3.92f, Outer + 0.145f), new Vector3(fasciaWidth - 0.3f, 0.5f, 0.02f), TownPalette.PaintBlack, false); // 글자 판
            TownKit.Box(group, "FasciaCornice", new Vector3(0f, 4.33f, Outer + 0.14f), new Vector3(width - 0.9f, 0.1f, 0.28f), style.Paint, false); // 간판 위 몰딩
            TownKit.Box(group, "FasciaBead", new Vector3(0f, 3.55f, Outer + 0.14f), new Vector3(fasciaWidth, 0.05f, 0.16f), style.Dressing, false); // 간판 아래 띠
            TownKit.Label(group, "FasciaText", sign, new Vector3(0f, 3.92f, Outer + 0.16f), 180f, 0.38f, style.SignColor); // 금색 글자
            TownKit.Box(group, "Ornament_L", new Vector3(-fasciaWidth * 0.36f, 3.92f, Outer + 0.158f), new Vector3(0.5f, 0.03f, 0.005f), TownPalette.Gold, false); // 장식선
            TownKit.Box(group, "Ornament_R", new Vector3(fasciaWidth * 0.36f, 3.92f, Outer + 0.158f), new Vector3(0.5f, 0.03f, 0.005f), TownPalette.Gold, false); // 장식선

            if (style.AwningA != null) // 줄무늬 차양
            {
                Awning(group, width - 1.5f, style); // 차양
            }

            Transform bracket = TownKit.Group(group, "HangingSign", new Vector3(pilasterX, GroundTop + 0.85f, Outer)); // 돌출 간판 (1층 위)
            TownKit.Box(bracket, "Arm", new Vector3(0f, 0.36f, 0.55f), new Vector3(0.04f, 0.04f, 1.1f), TownPalette.CastIron, false); // 팔
            TownKit.Box(bracket, "Brace", new Vector3(0f, 0.1f, 0.3f), new Vector3(0.03f, 0.03f, 0.7f), TownPalette.CastIron, false, new Vector3(-38f, 0f, 0f)); // 받침
            TownKit.Cylinder(bracket, "Scroll", new Vector3(0f, 0.2f, 0.15f), 0.2f, 0.02f, TownPalette.CastIron, false, new Vector3(0f, 0f, 90f)); // 소용돌이
            TownKit.Box(bracket, "Board", new Vector3(0f, -0.05f, 0.72f), new Vector3(0.06f, 0.6f, 0.78f), style.Paint, false); // 판
            TownKit.Box(bracket, "BoardFrame", new Vector3(0f, -0.05f, 0.72f), new Vector3(0.05f, 0.68f, 0.86f), TownPalette.Gold, false); // 금테
            TownKit.Label(bracket, "Text_A", sign, new Vector3(0.04f, -0.05f, 0.72f), -90f, 0.17f, style.SignColor); // 글자
            TownKit.Label(bracket, "Text_B", sign, new Vector3(-0.04f, -0.05f, 0.72f), 90f, 0.17f, style.SignColor); // 글자

            WallGasLamp(group, new Vector3(-pilasterX, 2.95f, Outer + 0.12f), true); // 기둥 벽등
            WallGasLamp(group, new Vector3(pilasterX, 2.95f, Outer + 0.12f), false); // 기둥 벽등 (불빛만)
            DoorStep(group, DoorWidth + 0.5f); // 문 앞 디딤돌

            if (style.Vacant) // 빈 점포 안내
            {
                WallOpening window = openings[2]; // 오른쪽 진열창
                TownKit.Box(group, "ToLetBill", new Vector3(window.Center, 2.1f, -0.05f), new Vector3(0.7f, 0.5f, 0.01f), TownPalette.Paper, false); // 안내문
                TownKit.Label(group, "ToLetText", "임대 문의", new Vector3(window.Center, 2.1f, -0.04f), 180f, 0.12f, new Color(0.2f, 0.1f, 0.05f)); // 글자
                for (int sheet = 0; sheet < 3; sheet++) // 반대쪽 창에 붙인 신문지
                {
                    float x = openings[1].Center + ((sheet - 1) * 0.62f); // 위치
                    TownKit.Box(group, "Newspaper", new Vector3(x, 1.6f + (sheet % 2 * 0.25f), -0.05f), new Vector3(0.55f, 0.75f, 0.01f), TownPalette.Paper, false, new Vector3(0f, 0f, (sheet - 1) * 3f)); // 신문지
                }
            }
        }

        private static void DisplayWindow(Transform group, WallOpening opening, TownBuildingStyle style) // 큰 진열창 (창살·가로대·윗창)
        {
            Transform window = TownKit.Group(group, "DisplayWindow", new Vector3(opening.Center, 0f, 0f)); // 묶음
            float width = opening.Width; // 폭
            float bottom = opening.Bottom; // 아래
            float top = opening.Top; // 위
            float transom = top - 0.55f; // 가로대 높이
            const float bar = 0.09f; // 틀 두께
            TownKit.Box(window, "Glass", new Vector3(0f, (bottom + top) * 0.5f, -0.02f), new Vector3(width - 0.05f, top - bottom - 0.05f, 0.03f), TownPalette.ShopGlass); // 유리 (충돌)
            TownKit.Box(window, "Frame_Top", new Vector3(0f, top - (bar * 0.5f), 0f), new Vector3(width, bar, 0.16f), style.Paint, false); // 위틀
            TownKit.Box(window, "Frame_Bottom", new Vector3(0f, bottom + (bar * 0.5f), 0.02f), new Vector3(width, bar, 0.2f), style.Paint, false); // 아래틀
            TownKit.Box(window, "Frame_L", new Vector3(-(width - bar) * 0.5f, (bottom + top) * 0.5f, 0f), new Vector3(bar, top - bottom, 0.16f), style.Paint, false); // 왼틀
            TownKit.Box(window, "Frame_R", new Vector3((width - bar) * 0.5f, (bottom + top) * 0.5f, 0f), new Vector3(bar, top - bottom, 0.16f), style.Paint, false); // 오른틀
            TownKit.Box(window, "Transom", new Vector3(0f, transom, 0f), new Vector3(width, 0.07f, 0.12f), style.Paint, false); // 가로대
            int panes = Mathf.Max(2, Mathf.RoundToInt(width / 1.1f)); // 칸 수

            for (int index = 1; index < panes; index++) // 세로 창살
            {
                float x = (-width * 0.5f) + (index * width / panes); // 위치
                TownKit.Box(window, "Mullion", new Vector3(x, (bottom + transom) * 0.5f, 0f), new Vector3(0.05f, transom - bottom, 0.1f), style.Paint, false); // 아래 창살
            }

            int lights = panes * 2; // 윗창 칸

            for (int index = 1; index < lights; index++) // 윗창 창살
            {
                float x = (-width * 0.5f) + (index * width / lights); // 위치
                TownKit.Box(window, "TopLight", new Vector3(x, (transom + top) * 0.5f, 0f), new Vector3(0.035f, top - transom, 0.08f), style.Paint, false); // 창살
            }

            TownKit.Box(window, "Stallriser", new Vector3(0f, (PlinthHeight + bottom) * 0.5f, Outer + 0.03f), new Vector3(width, bottom - PlinthHeight, 0.06f), style.Paint, false); // 창 아래 판
            TownKit.Box(window, "StallPanel", new Vector3(0f, (PlinthHeight + bottom) * 0.5f, Outer + 0.065f), new Vector3(width - 0.3f, bottom - PlinthHeight - 0.2f, 0.02f), TownPalette.PaintBlack, false); // 판 장식
            TownKit.Box(window, "Sill", new Vector3(0f, bottom - 0.02f, Outer + 0.1f), new Vector3(width + 0.1f, 0.06f, 0.2f), style.Dressing, false); // 창턱
        }

        private static void Doorway(Transform group, WallOpening opening, TownBuildingStyle style, bool stoneSurround) // 문틀·채광창·판벽 문
        {
            Transform door = TownKit.Group(group, "Doorway", Vector3.zero); // 묶음
            float width = opening.Width; // 폭
            float leafTop = FloorTop + DoorClear; // 문짝 위
            Material surround = stoneSurround ? style.Dressing : style.Paint; // 문틀 재질
            TownKit.Box(door, "Jamb_L", new Vector3(-(width * 0.5f) - 0.07f, (PlinthHeight + opening.Top) * 0.5f, Outer + 0.03f), new Vector3(0.14f, opening.Top - PlinthHeight, 0.08f), surround, false); // 왼쪽 문설주
            TownKit.Box(door, "Jamb_R", new Vector3((width * 0.5f) + 0.07f, (PlinthHeight + opening.Top) * 0.5f, Outer + 0.03f), new Vector3(0.14f, opening.Top - PlinthHeight, 0.08f), surround, false); // 오른쪽 문설주
            TownKit.Box(door, "Transom", new Vector3(0f, leafTop + 0.05f, 0f), new Vector3(width, 0.1f, 0.14f), style.Paint); // 가로대 (충돌)
            TownKit.Box(door, "Fanlight", new Vector3(0f, (leafTop + 0.1f + opening.Top) * 0.5f, 0f), new Vector3(width, opening.Top - leafTop - 0.1f, 0.03f), TownPalette.WindowLit); // 채광창 (충돌)

            for (int spoke = -2; spoke <= 2; spoke++) // 부챗살
            {
                TownKit.Box(door, "Spoke", new Vector3(spoke * 0.12f, (leafTop + 0.1f + opening.Top) * 0.5f, 0.03f), new Vector3(0.025f, opening.Top - leafTop - 0.12f, 0.02f), style.Paint, false, new Vector3(0f, 0f, spoke * 18f)); // 살
            }

            TownKit.Box(door, "Threshold", new Vector3(0f, FloorTop - 0.02f, 0f), new Vector3(width, 0.04f, WallThickness + 0.02f), style.Dressing); // 문턱
            Transform hinge = TownKit.Group(door, "DoorHinge", new Vector3(-width * 0.5f, 0f, -Outer + 0.04f), 100f); // 안쪽으로 연 문
            DoorLeaf(hinge, width, style.DoorPaint); // 문짝
        }

        private static void DoorLeaf(Transform hinge, float width, Material paint) // 네 판 문짝 (충돌 없음)
        {
            float height = DoorClear - 0.04f; // 높이
            float y = FloorTop + (height * 0.5f); // 중심
            TownKit.Box(hinge, "Leaf", new Vector3(width * 0.5f, y, 0f), new Vector3(width - 0.04f, height, 0.06f), paint, false); // 문짝
            float[] rows = { FloorTop + 0.55f, FloorTop + 1.75f }; // 판 높이

            foreach (float row in rows) // 위·아래 판
            {
                for (int column = -1; column <= 1; column += 2) // 좌우 판
                {
                    float x = (width * 0.5f) + (column * width * 0.21f); // 위치
                    float panelHeight = row < FloorTop + 1f ? 0.75f : 1.15f; // 판 높이
                    TownKit.Box(hinge, "Panel", new Vector3(x, row + (panelHeight * 0.5f) - 0.3f, 0.035f), new Vector3(width * 0.32f, panelHeight, 0.02f), paint, false); // 바깥 판
                    TownKit.Box(hinge, "Panel_In", new Vector3(x, row + (panelHeight * 0.5f) - 0.3f, -0.035f), new Vector3(width * 0.32f, panelHeight, 0.02f), paint, false); // 안쪽 판
                }
            }

            TownKit.Sphere(hinge, "Knob", new Vector3(width - 0.14f, FloorTop + 1.05f, 0.07f), Vector3.one * 0.07f, TownPalette.Brass); // 손잡이
            TownKit.Sphere(hinge, "Knob_In", new Vector3(width - 0.14f, FloorTop + 1.05f, -0.07f), Vector3.one * 0.07f, TownPalette.Brass); // 안쪽 손잡이
            TownKit.Box(hinge, "Letterbox", new Vector3(width * 0.5f, FloorTop + 1.25f, 0.05f), new Vector3(0.3f, 0.06f, 0.015f), TownPalette.Brass, false); // 우편 구멍
            TownKit.Cylinder(hinge, "Knocker", new Vector3(width * 0.5f, FloorTop + 1.6f, 0.06f), 0.13f, 0.02f, TownPalette.Brass, false, new Vector3(90f, 0f, 0f)); // 문고리
        }

        private static void OfficeFront(Transform front, float width, List<WallOpening> openings, TownBuildingStyle style, string sign) // 사무소 정문 (석재 문틀·박공 머리·명판)
        {
            Transform group = TownKit.Group(front, "OfficeFront", Vector3.zero); // 묶음
            Transform rustication = TownKit.Group(group, "Rustication", new Vector3(0f, 0f, Outer + 0.012f)); // 1층 석재 줄눈

            for (float y = PlinthHeight + 0.45f; y < GroundTop - 0.2f; y += 0.45f) // 줄
            {
                TownKit.Wall(rustication, "Band", width, y, y + 0.05f, 0.02f, openings, style.Dressing, false); // 석재 띠
            }

            for (int index = 1; index < openings.Count; index++) // 1층 오르내리창
            {
                WallOpening window = openings[index]; // 창
                Sash(front, "Window_G", window.Center, window.Bottom, window.Width, window.Top - window.Bottom, style, index == 1, true, true); // 창
                TownKit.Box(group, "WindowGuard", new Vector3(window.Center, window.Bottom - 0.35f, Outer + 0.22f), new Vector3(window.Width + 0.2f, 0.05f, 0.03f), TownPalette.CastIron, false); // 창 아래 쇠 난간
            }

            Doorway(group, openings[0], style, true); // 문
            float pilasterX = (DoorWidth * 0.5f) + 0.32f; // 문 기둥

            for (int side = -1; side <= 1; side += 2) // 문 옆 석재 기둥
            {
                TownKit.Box(group, "DoorPilaster", new Vector3(side * pilasterX, (PlinthHeight + 3.6f) * 0.5f, Outer + 0.08f), new Vector3(0.32f, 3.6f - PlinthHeight, 0.16f), style.Dressing); // 기둥
                TownKit.Box(group, "DoorCapital", new Vector3(side * pilasterX, 3.62f, Outer + 0.11f), new Vector3(0.42f, 0.14f, 0.22f), style.Dressing, false); // 기둥머리
            }

            float entablatureWidth = (pilasterX * 2f) + 0.6f; // 머리 폭
            TownKit.Box(group, "Entablature", new Vector3(0f, 3.86f, Outer + 0.12f), new Vector3(entablatureWidth, 0.34f, 0.24f), style.Dressing, false); // 머리
            TownKit.Box(group, "Frieze", new Vector3(0f, 3.86f, Outer + 0.245f), new Vector3(entablatureWidth - 0.4f, 0.24f, 0.01f), TownPalette.PaintBlack, false); // 명패 띠
            TownKit.Label(group, "FriezeText", sign, new Vector3(0f, 3.86f, Outer + 0.255f), 180f, 0.19f, style.SignColor); // 이름
            GameObject pediment = TownKit.Prism(group, "Pediment", new Vector3(0f, 4.03f, Outer + 0.12f), 0.26f, entablatureWidth + 0.1f, 0.42f, style.Dressing, false); // 박공 머리
            pediment.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 삼각형이 정면을 향하도록

            TownKit.Box(group, "BrassPlate", new Vector3(pilasterX + 0.55f, 1.7f, Outer + 0.02f), new Vector3(0.5f, 0.34f, 0.02f), TownPalette.Brass, false); // 놋쇠 명판
            TownKit.Label(group, "BrassText", "원정 사무소", new Vector3(pilasterX + 0.55f, 1.7f, Outer + 0.035f), 180f, 0.07f, new Color(0.18f, 0.12f, 0.04f)); // 명판 글자
            WallGasLamp(group, new Vector3(-(pilasterX + 0.55f), 2.8f, Outer + 0.05f), true); // 벽등
            DoorStep(group, entablatureWidth); // 계단
            TownKit.Box(group, "BootScraper", new Vector3(pilasterX + 0.2f, 0.07f, Outer + 0.75f), new Vector3(0.3f, 0.14f, 0.05f), TownPalette.CastIron, false); // 신발 긁개
        }

        private static void WarehouseFront(Transform front, float width, float wallTop, int storeys, TownBuildingStyle style, string sign) // 창고 정면 (아치 하역문·위층 하역문·도르래)
        {
            Transform group = TownKit.Group(front, "WarehouseFront", Vector3.zero); // 묶음
            const float doorWidth = 3.0f; // 하역문 폭
            const float doorTop = 3.6f; // 하역문 위
            const float radius = 2.2f; // 아치 반지름
            float centerY = doorTop - Mathf.Sqrt((radius * radius) - (doorWidth * doorWidth * 0.25f)); // 아치 중심
            float spread = Mathf.Asin((doorWidth * 0.5f) / radius) * Mathf.Rad2Deg; // 아치 각

            for (int index = -3; index <= 3; index++) // 아치 쐐기돌
            {
                float angle = index * spread / 3f; // 각
                float rad = angle * Mathf.Deg2Rad; // 라디안
                Vector3 position = new Vector3(Mathf.Sin(rad) * (radius + 0.16f), centerY + (Mathf.Cos(rad) * (radius + 0.16f)), Outer + 0.03f); // 위치
                Vector3 size = index == 0 ? new Vector3(0.36f, 0.62f, 0.1f) : new Vector3(0.3f, 0.42f, 0.08f); // 이맛돌은 크게
                TownKit.Box(group, index == 0 ? "Keystone" : "Voussoir", position, size, style.Dressing, false, new Vector3(0f, 0f, -angle)); // 돌
            }

            for (int side = -1; side <= 1; side += 2) // 문 옆 석재 받침
            {
                TownKit.Box(group, "Impost", new Vector3(side * ((doorWidth * 0.5f) + 0.15f), doorTop - 0.1f, Outer + 0.04f), new Vector3(0.4f, 0.2f, 0.1f), style.Dressing, false); // 받침돌
                TownKit.Box(group, "Guard", new Vector3(side * ((doorWidth * 0.5f) + 0.12f), PlinthHeight + 0.45f, Outer + 0.1f), new Vector3(0.18f, 0.9f, 0.18f), TownPalette.CastIron, false); // 모서리 보호쇠
            }

            TownKit.Box(group, "Threshold", new Vector3(0f, FloorTop - 0.02f, 0f), new Vector3(doorWidth, 0.04f, WallThickness + 0.02f), style.Dressing); // 문턱

            for (int side = -1; side <= 1; side += 2) // 두 짝 문 (안쪽으로 열림)
            {
                Transform hinge = TownKit.Group(group, "DoorHinge", new Vector3(side * doorWidth * 0.5f, 0f, -Outer + 0.04f), side < 0 ? 95f : -95f); // 경첩
                float leaf = (doorWidth * 0.5f) - 0.03f; // 문짝 폭
                float x = side < 0 ? leaf * 0.5f : -leaf * 0.5f; // 문짝 중심
                TownKit.Box(hinge, "Leaf", new Vector3(x, FloorTop + 1.55f, 0f), new Vector3(leaf, 3.1f, 0.07f), style.DoorPaint, false); // 문짝
                TownKit.Box(hinge, "Brace", new Vector3(x, FloorTop + 1.55f, 0.05f), new Vector3(0.08f, 3.2f, 0.03f), TownPalette.WoodDark, false, new Vector3(0f, 0f, side * 25f)); // 대각 보강
                TownKit.Box(hinge, "Strap_Top", new Vector3(x, FloorTop + 2.6f, 0.05f), new Vector3(leaf, 0.08f, 0.02f), TownPalette.CastIron, false); // 쇠띠
                TownKit.Box(hinge, "Strap_Low", new Vector3(x, FloorTop + 0.5f, 0.05f), new Vector3(leaf, 0.08f, 0.02f), TownPalette.CastIron, false); // 쇠띠
            }

            List<WallOpening> windows = FrontOpenings(style, width); // 작은 창

            for (int index = 1; index < windows.Count; index++) // 1층 작은 창 (쇠창살)
            {
                WallOpening window = windows[index]; // 창
                Sash(front, "Window_G", window.Center, window.Bottom, window.Width, window.Top - window.Bottom, style, false, false, true); // 창
                Transform bars = TownKit.Group(group, "IronBars", new Vector3(window.Center, 0f, Outer + 0.1f)); // 창살

                for (int bar = -2; bar <= 2; bar++) // 세로 창살
                {
                    TownKit.Box(bars, "Bar", new Vector3(bar * 0.2f, (window.Bottom + window.Top) * 0.5f, 0f), new Vector3(0.03f, window.Top - window.Bottom, 0.03f), TownPalette.CastIron, false); // 창살
                }
            }

            for (int storey = 1; storey < storeys; storey++) // 위층 하역문
            {
                float bottom = GroundTop + ((storey - 1) * UpperStorey) + 0.35f; // 문 아래
                TownKit.Box(group, "LoftRecess", new Vector3(0f, bottom + 1.2f, Outer + 0.005f), new Vector3(1.8f, 2.4f, 0.01f), TownPalette.Soot, false); // 어두운 구멍
                TownKit.Box(group, "LoftDoor_L", new Vector3(-0.47f, bottom + 1.2f, Outer + 0.03f), new Vector3(0.86f, 2.3f, 0.05f), style.DoorPaint, false); // 문짝
                TownKit.Box(group, "LoftDoor_R", new Vector3(0.47f, bottom + 1.2f, Outer + 0.03f), new Vector3(0.86f, 2.3f, 0.05f), style.DoorPaint, false); // 문짝
                TownKit.Box(group, "LoftLintel", new Vector3(0f, bottom + 2.52f, Outer + 0.04f), new Vector3(2.2f, 0.24f, 0.08f), style.Dressing, false); // 인방
                TownKit.Box(group, "LoftSill", new Vector3(0f, bottom - 0.04f, Outer + 0.1f), new Vector3(2.1f, 0.1f, 0.2f), style.Dressing, false); // 문턱
                TownKit.Box(group, "LoftRail", new Vector3(0f, bottom + 0.95f, Outer + 0.12f), new Vector3(1.9f, 0.05f, 0.04f), TownPalette.CastIron, false); // 안전 난간
            }

            Transform hoist = TownKit.Group(group, "Hoist", new Vector3(0f, wallTop - 0.1f, Outer)); // 도르래 들보
            TownKit.Box(hoist, "Beam", new Vector3(0f, 0f, 0.75f), new Vector3(0.26f, 0.32f, 1.5f), TownPalette.WoodDark, false); // 들보
            TownKit.Box(hoist, "Hood", new Vector3(0f, 0.28f, 0.8f), new Vector3(0.7f, 0.06f, 1.7f), TownPalette.Slate, false, new Vector3(8f, 0f, 0f)); // 덮개
            TownKit.Cylinder(hoist, "Pulley", new Vector3(0f, -0.26f, 1.3f), 0.3f, 0.06f, TownPalette.CastIron, false, new Vector3(0f, 0f, 90f)); // 도르래
            float ropeLength = wallTop - GroundTop - 0.6f; // 밧줄 길이
            TownKit.Cylinder(hoist, "Rope", new Vector3(0f, -0.4f - (ropeLength * 0.5f), 1.42f), 0.03f, ropeLength, TownPalette.Rope); // 밧줄
            TownKit.Box(hoist, "Hook", new Vector3(0f, -0.45f - ropeLength, 1.42f), new Vector3(0.05f, 0.18f, 0.12f), TownPalette.CastIron, false); // 갈고리
            TownProps.Sack(hoist, new Vector3(0f, -1.0f - ropeLength, 1.42f)); // 매달린 자루

            string[] words = sign.Split(' '); // 하역문 양쪽에 나눠 씀
            string leftWord = words.Length > 1 ? words[0] : sign; // 왼쪽
            string rightWord = words.Length > 1 ? string.Join(" ", words, 1, words.Length - 1) : string.Empty; // 오른쪽
            float signY = GroundTop + 1.55f; // 첫 윗층 창 사이 높이

            for (int side = -1; side <= 1; side += 2) // 양쪽 벽 글씨
            {
                string word = side < 0 ? leftWord : rightWord; // 글자

                if (string.IsNullOrEmpty(word)) // 빈 쪽
                {
                    continue; // 생략
                }

                TownKit.Box(group, "PaintedBand", new Vector3(side * 2.5f, signY, Outer + 0.004f), new Vector3(2.2f, 0.8f, 0.005f), TownPalette.PaintBlack, false); // 글씨 바탕
                TownKit.Label(group, "PaintedSign", word, new Vector3(side * 2.5f, signY, Outer + 0.012f), 180f, 0.5f, new Color(0.86f, 0.82f, 0.7f)); // 벽 글씨
            }
            WallGasLamp(group, new Vector3((doorWidth * 0.5f) + 0.75f, 3.1f, Outer + 0.05f), true); // 벽등
            DoorStep(group, doorWidth + 0.4f); // 경사판 대신 낮은 디딤
        }

        private static void Awning(Transform group, float width, TownBuildingStyle style) // 줄무늬 천 차양
        {
            Transform awning = TownKit.Group(group, "Awning", new Vector3(0f, 3.5f, Outer + 0.14f)); // 묶음 (간판 띠 아래)
            const float projection = 1.5f; // 돌출
            const float slope = 20f; // 기울기
            int stripes = Mathf.Max(6, Mathf.RoundToInt(width / 0.42f)); // 줄 수
            float stripeWidth = width / stripes; // 줄 폭
            float drop = Mathf.Sin(slope * Mathf.Deg2Rad) * projection; // 앞쪽 처짐
            Vector3 center = new Vector3(0f, -drop * 0.5f, projection * 0.5f * Mathf.Cos(slope * Mathf.Deg2Rad)); // 천 중심

            for (int index = 0; index < stripes; index++) // 줄무늬
            {
                float x = (-width * 0.5f) + ((index + 0.5f) * stripeWidth); // 위치
                Material material = index % 2 == 0 ? style.AwningA : style.AwningB; // 색
                TownKit.Box(awning, "Stripe", new Vector3(x, center.y, center.z), new Vector3(stripeWidth, 0.03f, projection), material, false, new Vector3(slope, 0f, 0f)); // 천
                TownKit.Box(awning, "Valance", new Vector3(x, -drop - 0.12f, (projection * Mathf.Cos(slope * Mathf.Deg2Rad)) + 0.01f), new Vector3(stripeWidth, 0.24f, 0.02f), material, false); // 앞 드림
            }

            TownKit.Box(awning, "Roller", new Vector3(0f, 0.02f, 0.02f), new Vector3(width + 0.1f, 0.1f, 0.1f), TownPalette.CastIron, false); // 말이 통
            TownKit.Box(awning, "FrontBar", new Vector3(0f, -drop, projection * Mathf.Cos(slope * Mathf.Deg2Rad)), new Vector3(width, 0.04f, 0.04f), TownPalette.CastIron, false); // 앞대

            for (int side = -1; side <= 1; side += 2) // 양쪽 팔
            {
                TownKit.Box(awning, "Arm", new Vector3(side * width * 0.48f, -drop * 0.5f - 0.3f, projection * 0.45f), new Vector3(0.03f, 0.03f, 1.6f), TownPalette.CastIron, false, new Vector3(-8f, 0f, 0f)); // 팔
            }
        }

        private static void Sash(Transform frame, string name, float x, float bottom, float width, float height, TownBuildingStyle style, bool lit, bool keystone, bool opening) // 오르내리창 (창살 4칸 × 2) + 석재 인방·창턱
        {
            Transform group = TownKit.Group(frame, name, new Vector3(x, 0f, 0f)); // 묶음
            float top = bottom + height; // 위
            float middle = bottom + (height * 0.5f); // 가운데
            float z = opening ? -0.02f : Outer + 0.02f; // 창틀 위치 (구멍 안쪽으로 들어가 깊이감)
            Material glass = lit ? TownPalette.WindowLit : TownPalette.WindowDark; // 유리

            if (opening) // 실제 구멍: 충돌 유리
            {
                TownKit.Box(group, "Glass", new Vector3(0f, middle, z - 0.02f), new Vector3(width - 0.04f, height - 0.04f, 0.03f), glass); // 유리
                TownKit.Box(group, "Reveal_Top", new Vector3(0f, top - 0.02f, Outer * 0.5f), new Vector3(width, 0.04f, Outer), TownPalette.Dressing, false); // 창 옆면
            }
            else // 장식 창: 벽 위에 붙임
            {
                TownKit.Box(group, "Glass", new Vector3(0f, middle, Outer + 0.006f), new Vector3(width, height, 0.012f), glass, false); // 유리
            }

            const float bar = 0.07f; // 틀 두께
            Material paint = TownPalette.PaintCream; // 창틀 칠
            TownKit.Box(group, "Frame_Top", new Vector3(0f, top - (bar * 0.5f), z), new Vector3(width, bar, 0.06f), paint, false); // 위틀
            TownKit.Box(group, "Frame_Bottom", new Vector3(0f, bottom + (bar * 0.5f), z), new Vector3(width, bar, 0.06f), paint, false); // 아래틀
            TownKit.Box(group, "Frame_L", new Vector3(-(width - bar) * 0.5f, middle, z), new Vector3(bar, height, 0.06f), paint, false); // 왼틀
            TownKit.Box(group, "Frame_R", new Vector3((width - bar) * 0.5f, middle, z), new Vector3(bar, height, 0.06f), paint, false); // 오른틀
            TownKit.Box(group, "MeetingRail", new Vector3(0f, middle, z + 0.01f), new Vector3(width, 0.06f, 0.06f), paint, false); // 가운데 창틀
            TownKit.Box(group, "Glazing_V", new Vector3(0f, middle, z + 0.005f), new Vector3(0.03f, height, 0.04f), paint, false); // 세로 창살
            TownKit.Box(group, "Glazing_H1", new Vector3(0f, bottom + (height * 0.25f), z + 0.005f), new Vector3(width, 0.03f, 0.04f), paint, false); // 가로 창살
            TownKit.Box(group, "Glazing_H2", new Vector3(0f, bottom + (height * 0.75f), z + 0.005f), new Vector3(width, 0.03f, 0.04f), paint, false); // 가로 창살
            TownKit.Box(group, "Lintel", new Vector3(0f, top + 0.12f, Outer + 0.03f), new Vector3(width + 0.36f, 0.24f, 0.08f), style.Dressing, false); // 석재 인방

            if (keystone) // 이맛돌
            {
                TownKit.Box(group, "Keystone", new Vector3(0f, top + 0.15f, Outer + 0.06f), new Vector3(0.22f, 0.34f, 0.08f), style.Dressing, false); // 이맛돌
            }

            TownKit.Box(group, "Sill", new Vector3(0f, bottom - 0.05f, Outer + 0.08f), new Vector3(width + 0.24f, 0.09f, 0.2f), style.Dressing, false); // 창턱
        }

        private static void UpperWindows(Transform front, Transform back, Transform left, Transform right, float width, float sideLength, int storeys, TownBuildingStyle style) // 윗층 오르내리창 (장식)
        {
            int seed = style.LitSeed * 7919; // 불 켜진 창 선택

            for (int storey = 1; storey < storeys; storey++) // 층
            {
                float bottom = GroundTop + ((storey - 1) * UpperStorey) + 0.75f; // 창 아래
                float height = storey == storeys - 1 ? 1.75f : 1.95f; // 위층은 조금 낮게
                int count = Mathf.Max(2, Mathf.FloorToInt((width - 1.2f) / 2.4f)); // 창 수

                for (int index = 0; index < count; index++) // 앞면
                {
                    float x = (-width * 0.5f) + ((index + 0.5f) * width / count); // 위치

                    if (style.Facade == TownFacade.Warehouse && Mathf.Abs(x) < 1.6f) // 하역문 자리
                    {
                        continue; // 생략
                    }

                    seed = ((seed * 1103515245) + 12345) & 0x7fffffff; // 의사 난수
                    Sash(front, "Window_U", x, bottom, 1.1f, height, style, !style.Vacant && seed % 3 == 0, style.Facade == TownFacade.Office, false); // 창
                }

                Sash(back, "Window_U", -width * 0.22f, bottom, 1.0f, height, style, false, false, false); // 뒷면
                Sash(back, "Window_U", width * 0.22f, bottom, 1.0f, height, style, false, false, false); // 뒷면
                seed = ((seed * 1103515245) + 12345) & 0x7fffffff; // 의사 난수
                Sash(left, "Window_U", 0f, bottom, 1.1f, height, style, !style.Vacant && seed % 4 == 0, false, false); // 왼쪽
                Sash(right, "Window_U", 0f, bottom, 1.1f, height, style, false, false, false); // 오른쪽
            }
        }

        private static void Dressings(Transform front, Transform back, Transform left, Transform right, float width, float sideLength, float wallTop, int storeys, TownBuildingStyle style) // 모서리돌·층 띠·코니스·톱니 장식
        {
            float z = Outer + 0.02f; // 바깥 면
            Transform quoins = TownKit.Group(front, "Quoins", Vector3.zero); // 앞 모서리돌
            int course = 0; // 줄 번호

            for (float y = PlinthHeight + 0.17f; y < wallTop - 0.2f; y += 0.34f, course++) // 모서리돌 쌓기
            {
                float longSide = course % 2 == 0 ? 0.56f : 0.34f; // 앞면 길이
                float shortSide = course % 2 == 0 ? 0.3f : 0.52f; // 옆면 길이

                for (int side = -1; side <= 1; side += 2) // 양쪽 모서리
                {
                    TownKit.Box(quoins, "Quoin", new Vector3(side * ((width * 0.5f) - (longSide * 0.5f)), y, z), new Vector3(longSide, 0.3f, 0.05f), style.Dressing, false); // 앞면 돌
                }

                TownKit.Box(left, "Quoin", new Vector3((sideLength * 0.5f) + WallThickness - (shortSide * 0.5f), y, z), new Vector3(shortSide, 0.3f, 0.05f), style.Dressing, false); // 왼쪽 옆면 돌 (앞 모서리까지)
                TownKit.Box(right, "Quoin", new Vector3(-(sideLength * 0.5f) - WallThickness + (shortSide * 0.5f), y, z), new Vector3(shortSide, 0.3f, 0.05f), style.Dressing, false); // 오른쪽 옆면 돌
            }

            for (int storey = 1; storey < storeys; storey++) // 층 띠
            {
                float y = GroundTop + ((storey - 1) * UpperStorey); // 높이
                StringCourse(front, width + 0.1f, y, style); // 앞
                StringCourse(back, width + 0.1f, y, style); // 뒤
                StringCourse(left, sideLength + (WallThickness * 2f) + 0.1f, y, style); // 왼쪽 (모서리까지)
                StringCourse(right, sideLength + (WallThickness * 2f) + 0.1f, y, style); // 오른쪽
            }

            Transform cornice = TownKit.Group(front, "Cornice", Vector3.zero); // 앞 코니스
            TownKit.Box(cornice, "Frieze", new Vector3(0f, wallTop - 0.3f, Outer + 0.03f), new Vector3(width, 0.24f, 0.06f), style.Dressing, false); // 띠
            TownKit.Box(cornice, "Corona", new Vector3(0f, wallTop - 0.05f, Outer + 0.16f), new Vector3(width + 0.2f, 0.2f, 0.32f), style.Dressing, false); // 돌출부
            TownKit.Box(cornice, "Gutter", new Vector3(0f, wallTop - 0.3f, Outer + 0.36f), new Vector3(width, 0.12f, 0.14f), TownPalette.CastIron, false); // 물받이 (처마 끝 아래)

            for (float x = (-width * 0.5f) + 0.15f; x < (width * 0.5f) - 0.1f; x += 0.3f) // 톱니 장식
            {
                TownKit.Box(cornice, "Dentil", new Vector3(x, wallTop - 0.2f, Outer + 0.1f), new Vector3(0.12f, 0.12f, 0.12f), style.Dressing, false); // 톱니
            }

            TownKit.Box(back, "BackEaves", new Vector3(0f, wallTop - 0.08f, Outer + 0.08f), new Vector3(width + 0.1f, 0.16f, 0.16f), style.Dressing, false); // 뒤 처마 띠
        }

        public static void GhostSign(TownBuildingResult building, bool rightWall, string text, float y, float height) // 옆벽에 칠한 오래된 광고 글씨
        {
            Transform wall = building.Root.Find(rightWall ? "Wall_Right" : "Wall_Left"); // 옆벽 프레임

            if (wall == null) // 없음
            {
                return; // 생략
            }

            Transform sign = TownKit.Group(wall, "GhostSign", Vector3.zero); // 묶음
            TownKit.Box(sign, "Band", new Vector3(0f, y, Outer + 0.004f), new Vector3(Mathf.Min(building.Depth - 1.6f, text.Length * height * 1.1f + 0.8f), height * 1.7f, 0.005f), TownPalette.PaintGray, false); // 바랜 바탕
            TownKit.Label(sign, "Text", text, new Vector3(0f, y, Outer + 0.012f), 180f, height, new Color(0.78f, 0.74f, 0.64f)); // 바랜 글씨
        }

        private static void StringCourse(Transform frame, float length, float y, TownBuildingStyle style) // 층 띠
        {
            TownKit.Box(frame, "StringCourse", new Vector3(0f, y, Outer + 0.05f), new Vector3(length, 0.18f, 0.1f), style.Dressing, false); // 띠
        }

        private static void Rainwater(Transform back, Transform left, Transform right, float width, float sideLength, float wallTop) // 선홈통 (뒤쪽 모서리)
        {
            float height = wallTop - PlinthHeight; // 길이

            foreach (Transform frame in new[] { left, right }) // 옆벽
            {
                float x = frame == left ? -(sideLength * 0.5f) + 0.35f : (sideLength * 0.5f) - 0.35f; // 뒤쪽 끝
                TownKit.Cylinder(frame, "Downpipe", new Vector3(x, PlinthHeight + (height * 0.5f), Outer + 0.1f), 0.1f, height, TownPalette.CastIron); // 관
                TownKit.Box(frame, "Hopper", new Vector3(x, wallTop - 0.1f, Outer + 0.12f), new Vector3(0.26f, 0.24f, 0.2f), TownPalette.CastIron, false); // 깔때기
                TownKit.Box(frame, "Shoe", new Vector3(x, PlinthHeight + 0.08f, Outer + 0.2f), new Vector3(0.12f, 0.12f, 0.25f), TownPalette.CastIron, false); // 배수구

                for (float y = PlinthHeight + 1.5f; y < wallTop - 0.5f; y += 2f) // 고정 쇠
                {
                    TownKit.Box(frame, "PipeClip", new Vector3(x, y, Outer + 0.08f), new Vector3(0.16f, 0.05f, 0.14f), TownPalette.CastIron, false); // 고정 쇠
                }
            }

            TownKit.Box(back, "BackGutter", new Vector3(0f, wallTop - 0.3f, Outer + 0.36f), new Vector3(width, 0.12f, 0.14f), TownPalette.CastIron, false); // 뒤 물받이
        }

        private static float Roof(Transform root, float width, float depth, float wallTop, TownBuildingStyle style) // 슬레이트 박공 지붕 + 파라펫 박공 + 도머 (용마루 높이 반환)
        {
            Transform group = TownKit.Group(root, "Roof", Vector3.zero); // 묶음
            float pitch = style.RoofPitch; // 경사
            float tan = Mathf.Tan(pitch * Mathf.Deg2Rad); // 기울기
            float cos = Mathf.Cos(pitch * Mathf.Deg2Rad); // 코사인
            const float eave = 0.3f; // 처마
            const float thickness = 0.14f; // 판 두께
            float halfDepth = depth * 0.5f; // 반깊이
            float rise = halfDepth * tan; // 높이
            float apex = wallTop + rise; // 용마루
            float run = halfDepth + eave; // 수평 길이
            float slope = run / cos; // 경사 길이
            float slabWidth = width - 0.2f; // 파라펫 안쪽

            for (int side = -1; side <= 1; side += 2) // 앞·뒤 경사
            {
                float centerY = apex - (run * 0.5f * tan) + (thickness * 0.5f / cos); // 중심
                Vector3 euler = new Vector3(side * pitch, 0f, 0f); // 기울기
                TownKit.Box(group, side > 0 ? "Slab_Front" : "Slab_Back", new Vector3(0f, centerY, side * run * 0.5f), new Vector3(slabWidth, thickness, slope), TownPalette.Slate, true, euler); // 슬레이트 판
                int rows = Mathf.FloorToInt(slope / 0.3f); // 슬레이트 줄

                for (int row = 1; row < rows; row++) // 줄눈
                {
                    float t = (float)row / rows; // 위치
                    float y = apex - (run * t * tan) + (thickness / cos) + 0.008f; // 높이
                    TownKit.Box(group, "SlateRow", new Vector3(0f, y, side * run * t), new Vector3(slabWidth, 0.02f, 0.05f), TownPalette.SlateEdge, false, euler); // 줄
                }
            }

            TownKit.Cylinder(group, "RidgeRoll", new Vector3(0f, apex + (thickness / cos) + 0.02f, 0f), 0.2f, slabWidth, TownPalette.Lead, false, new Vector3(0f, 0f, 90f)); // 납 용마루

            for (int side = -1; side <= 1; side += 2) // 파라펫 박공
            {
                float x = side * ((width * 0.5f) - Outer); // 박공 위치
                const float parapet = 0.3f; // 지붕보다 올라온 높이
                TownKit.Box(group, side < 0 ? "GableBase_L" : "GableBase_R", new Vector3(x, wallTop + (parapet * 0.5f), 0f), new Vector3(WallThickness, parapet, depth), style.Brick); // 박공 아랫단
                TownKit.Prism(group, side < 0 ? "Gable_L" : "Gable_R", new Vector3(x, wallTop + parapet, 0f), WallThickness, depth, rise, style.Brick); // 박공 벽돌 (지붕면보다 0.3 위)
                float copingLength = (halfDepth + 0.05f) / cos; // 갓돌 길이

                for (int slopeSide = -1; slopeSide <= 1; slopeSide += 2) // 갓돌 두 줄
                {
                    Vector3 position = new Vector3(x, wallTop + parapet + (rise * 0.5f) + 0.06f, slopeSide * halfDepth * 0.5f); // 중심
                    TownKit.Box(group, "Coping", position, new Vector3(WallThickness + 0.12f, 0.12f, copingLength), style.Dressing, false, new Vector3(slopeSide * pitch, 0f, 0f)); // 갓돌
                }

                TownKit.Box(group, "Kneeler", new Vector3(x, wallTop + parapet, halfDepth - 0.18f), new Vector3(WallThickness + 0.14f, 0.36f, 0.44f), style.Dressing, false); // 박공 끝돌
                TownKit.Box(group, "Kneeler_B", new Vector3(x, wallTop + parapet, -halfDepth + 0.18f), new Vector3(WallThickness + 0.14f, 0.36f, 0.44f), style.Dressing, false); // 박공 끝돌
            }

            int dormers = Mathf.Clamp(style.Dormers, 0, 2); // 도머 수

            for (int index = 0; index < dormers; index++) // 도머창
            {
                float x = dormers == 1 ? 0f : (index == 0 ? -width * 0.24f : width * 0.24f); // 위치
                Dormer(group, x, halfDepth, wallTop, tan, style); // 생성
            }

            return apex; // 용마루 높이
        }

        private static void Dormer(Transform group, float x, float halfDepth, float wallTop, float tan, TownBuildingStyle style) // 지붕창
        {
            const float setBack = 1.0f; // 벽에서 들어간 거리
            const float width = 1.4f; // 폭
            const float depth = 1.9f; // 깊이 (지붕 안쪽으로)
            float frontZ = halfDepth - setBack; // 앞면 z
            float roofY = wallTop + (setBack * tan); // 앞면에서 지붕 높이
            float top = roofY + 1.45f; // 도머 윗면
            float bottom = wallTop + 0.2f; // 아래 (지붕 속)
            Transform dormer = TownKit.Group(group, "Dormer", new Vector3(x, 0f, frontZ)); // 묶음 (앞면 기준)
            TownKit.Box(dormer, "Cheeks", new Vector3(0f, (bottom + top) * 0.5f, -depth * 0.5f), new Vector3(width, top - bottom, depth), TownPalette.Slate); // 몸체
            TownKit.Box(dormer, "Face", new Vector3(0f, (roofY + top) * 0.5f, 0.02f), new Vector3(width + 0.06f, top - roofY, 0.04f), TownPalette.PaintCream, false); // 앞판
            TownKit.Box(dormer, "Glass", new Vector3(0f, roofY + 0.72f, 0.045f), new Vector3(0.8f, 0.95f, 0.01f), TownPalette.WindowDark, false); // 유리
            TownKit.Box(dormer, "Bar_V", new Vector3(0f, roofY + 0.72f, 0.055f), new Vector3(0.03f, 0.95f, 0.01f), TownPalette.PaintCream, false); // 창살
            TownKit.Box(dormer, "Bar_H", new Vector3(0f, roofY + 0.72f, 0.055f), new Vector3(0.8f, 0.03f, 0.01f), TownPalette.PaintCream, false); // 창살
            GameObject cap = TownKit.Prism(dormer, "Cap", new Vector3(0f, top, -depth * 0.5f + 0.1f), depth + 0.2f, width + 0.3f, 0.55f, TownPalette.Slate, false); // 작은 박공 지붕
            cap.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 용마루가 앞뒤 방향
            GameObject capFace = TownKit.Prism(dormer, "CapFace", new Vector3(0f, top, 0.03f), 0.05f, width + 0.08f, 0.5f, TownPalette.PaintCream, false); // 박공 앞판
            capFace.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 정면
        }

        private static void Chimneys(Transform root, float width, float apex) // 박공 위 굴뚝 단지 + 항아리
        {
            Transform group = TownKit.Group(root, "Chimneys", Vector3.zero); // 묶음

            for (int side = -1; side <= 1; side += 2) // 양쪽 박공
            {
                float x = side * ((width * 0.5f) - 0.4f); // 위치
                TownKit.Box(group, "Stack", new Vector3(x, apex + 0.6f, 0f), new Vector3(0.8f, 2.0f, 1.5f), TownPalette.SootBrick, true); // 굴뚝 몸체
                TownKit.Box(group, "StackBand", new Vector3(x, apex + 1.35f, 0f), new Vector3(0.9f, 0.12f, 1.6f), TownPalette.DressingSoot, false); // 띠
                TownKit.Box(group, "StackCap", new Vector3(x, apex + 1.62f, 0f), new Vector3(0.96f, 0.16f, 1.66f), TownPalette.DressingSoot, false); // 갓

                for (int pot = -1; pot <= 1; pot++) // 항아리 3개
                {
                    float height = 0.5f + (Mathf.Abs(pot) * 0.08f); // 높이
                    TownKit.Cylinder(group, "Pot", new Vector3(x, apex + 1.7f + (height * 0.5f), pot * 0.42f), 0.24f, height, TownPalette.ChimneyPot); // 항아리
                    TownKit.Cylinder(group, "PotRim", new Vector3(x, apex + 1.7f + height, pot * 0.42f), 0.3f, 0.05f, TownPalette.ChimneyPot); // 테
                    TownKit.Cylinder(group, "PotSoot", new Vector3(x, apex + 1.73f + height, pot * 0.42f), 0.18f, 0.01f, TownPalette.Soot); // 그을음
                }
            }
        }

        private static void WallGasLamp(Transform group, Vector3 position, bool withLight) // 벽 가스등
        {
            Transform lamp = TownKit.Group(group, "WallGasLamp", position); // 묶음
            TownKit.Box(lamp, "Plate", new Vector3(0f, 0f, 0.01f), new Vector3(0.14f, 0.22f, 0.02f), TownPalette.CastIron, false); // 벽판
            TownKit.Box(lamp, "Arm", new Vector3(0f, 0.02f, 0.18f), new Vector3(0.03f, 0.03f, 0.34f), TownPalette.CastIron, false); // 팔
            TownKit.Box(lamp, "ArmCurl", new Vector3(0f, -0.08f, 0.1f), new Vector3(0.02f, 0.02f, 0.2f), TownPalette.CastIron, false, new Vector3(-35f, 0f, 0f)); // 받침
            TownKit.Box(lamp, "Crown", new Vector3(0f, 0.22f, 0.36f), new Vector3(0.24f, 0.05f, 0.24f), TownPalette.CastIron, false); // 머리
            TownKit.Box(lamp, "Roof", new Vector3(0f, 0.27f, 0.36f), new Vector3(0.16f, 0.06f, 0.16f), TownPalette.CastIron, false, new Vector3(0f, 45f, 0f)); // 지붕
            TownKit.Box(lamp, "Glass", new Vector3(0f, 0.06f, 0.36f), new Vector3(0.18f, 0.28f, 0.18f), TownPalette.LampGlow, false); // 유리
            TownKit.Box(lamp, "Base", new Vector3(0f, -0.1f, 0.36f), new Vector3(0.12f, 0.05f, 0.12f), TownPalette.CastIron, false); // 받침

            if (withLight) // 실제 조명
            {
                TownKit.PointLight(lamp, "Light", new Vector3(0f, 0.05f, 0.5f), 7f, 1.3f, new Color(1f, 0.74f, 0.45f), 0.3f); // 조명
            }
        }

        private static void DoorStep(Transform group, float width) // 문 앞 디딤돌
        {
            TownKit.Box(group, "Step", new Vector3(0f, 0.09f, Outer + 0.35f), new Vector3(width, 0.18f, 0.5f), TownPalette.DressingSoot); // 디딤돌 (보도 위 0.18)
        }

        private static void FloorBoards(Transform root, float innerWidth, float innerDepth) // 마루 줄눈
        {
            Transform group = TownKit.Group(root, "FloorBoards", Vector3.zero); // 묶음
            int count = Mathf.FloorToInt(innerWidth / 0.3f); // 줄 수

            for (int index = 1; index < count; index++) // 줄
            {
                float x = (-innerWidth * 0.5f) + (index * innerWidth / count); // 위치
                TownKit.Box(group, "Seam", new Vector3(x, FloorTop + 0.002f, 0f), new Vector3(0.018f, 0.004f, innerDepth), TownPalette.FloorSeam, false); // 줄눈
            }
        }

        private static void CeilingCornice(Transform interior, float innerWidth, float innerDepth, float innerHeight) // 천장 몰딩
        {
            float y = innerHeight - 0.16f; // 높이
            TownKit.Box(interior, "Cornice_F", new Vector3(0f, y, (innerDepth * 0.5f) - 0.06f), new Vector3(innerWidth, 0.12f, 0.12f), TownPalette.PaintCream, false); // 앞
            TownKit.Box(interior, "Cornice_B", new Vector3(0f, y, -(innerDepth * 0.5f) + 0.06f), new Vector3(innerWidth, 0.12f, 0.12f), TownPalette.PaintCream, false); // 뒤
            TownKit.Box(interior, "Cornice_L", new Vector3(-(innerWidth * 0.5f) + 0.06f, y, 0f), new Vector3(0.12f, 0.12f, innerDepth), TownPalette.PaintCream, false); // 왼쪽
            TownKit.Box(interior, "Cornice_R", new Vector3((innerWidth * 0.5f) - 0.06f, y, 0f), new Vector3(0.12f, 0.12f, innerDepth), TownPalette.PaintCream, false); // 오른쪽
            TownKit.Cylinder(interior, "CeilingRose", new Vector3(0f, innerHeight - 0.11f, 0f), 0.7f, 0.03f, TownPalette.PaintCream); // 천장 장식
        }

        private static void GasChandelier(Transform area, Vector3 position, float intensity) // 가스 샹들리에 (3구)
        {
            Transform lamp = TownKit.Group(area, "GasChandelier", position); // 묶음
            TownKit.Cylinder(lamp, "Stem", new Vector3(0f, -0.4f, 0f), 0.04f, 0.8f, TownPalette.Brass); // 대
            TownKit.Sphere(lamp, "Hub", new Vector3(0f, -0.82f, 0f), Vector3.one * 0.12f, TownPalette.Brass); // 중심

            for (int arm = 0; arm < 3; arm++) // 팔
            {
                float angle = arm * 120f; // 각도
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward; // 방향
                TownKit.Box(lamp, "Arm", new Vector3(0f, -0.84f, 0f) + (direction * 0.2f), new Vector3(0.03f, 0.03f, 0.4f), TownPalette.Brass, false, new Vector3(0f, angle, 0f)); // 팔
                TownKit.Cylinder(lamp, "Cup", new Vector3(0f, -0.8f, 0f) + (direction * 0.4f), 0.1f, 0.05f, TownPalette.Brass); // 받침
                TownKit.Sphere(lamp, "Globe", new Vector3(0f, -0.7f, 0f) + (direction * 0.4f), new Vector3(0.16f, 0.2f, 0.16f), TownPalette.LampGlow); // 유리 갓
            }

            TownKit.PointLight(lamp, "Light", new Vector3(0f, -0.85f, 0f), 11f, intensity, new Color(1f, 0.8f, 0.55f), 0.55f); // 조명
        }
    }
}
