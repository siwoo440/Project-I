using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools.Town // 34일차 마을 제작 도구
{
    public static class TownProps // 거리·골목·실내 소품 (y=0 이 바닥)
    {
        public static void StreetLamp(Transform parent, string name, Vector3 position, float yaw) // 빅토리아식 가스 가로등 (기둥 꼭대기 등 + 사다리 걸이)
        {
            Transform lamp = TownKit.Group(parent, name, position, yaw); // 묶음
            Material iron = TownPalette.CastIron; // 주철
            TownKit.Cylinder(lamp, "Base", new Vector3(0f, 0.3f, 0f), 0.42f, 0.6f, iron, true); // 받침
            TownKit.Cylinder(lamp, "BaseRing", new Vector3(0f, 0.62f, 0f), 0.34f, 0.06f, iron); // 받침 테
            TownKit.Cylinder(lamp, "Column", new Vector3(0f, 1.85f, 0f), 0.16f, 2.5f, iron, true); // 기둥

            for (int flute = 0; flute < 4; flute++) // 세로 홈 장식
            {
                Vector3 offset = Quaternion.Euler(0f, flute * 90f, 0f) * new Vector3(0.08f, 0f, 0f); // 방향
                TownKit.Box(lamp, "Flute", new Vector3(0f, 1.6f, 0f) + offset, new Vector3(0.03f, 1.8f, 0.03f), iron, false, new Vector3(0f, flute * 90f, 0f)); // 홈
            }

            TownKit.Cylinder(lamp, "Collar", new Vector3(0f, 3.12f, 0f), 0.26f, 0.08f, iron); // 목 테
            TownKit.Box(lamp, "LadderBar", new Vector3(0f, 2.95f, 0f), new Vector3(0.8f, 0.05f, 0.05f), iron, false); // 사다리 걸이
            TownKit.Sphere(lamp, "BarEnd_L", new Vector3(-0.4f, 2.95f, 0f), Vector3.one * 0.07f, iron); // 끝 장식
            TownKit.Sphere(lamp, "BarEnd_R", new Vector3(0.4f, 2.95f, 0f), Vector3.one * 0.07f, iron); // 끝 장식
            TownKit.Box(lamp, "LanternBase", new Vector3(0f, 3.22f, 0f), new Vector3(0.26f, 0.06f, 0.26f), iron, false); // 등 바닥
            TownKit.Box(lamp, "LanternGlass", new Vector3(0f, 3.5f, 0f), new Vector3(0.36f, 0.5f, 0.36f), TownPalette.LampGlow, false); // 등 유리

            for (int corner = 0; corner < 4; corner++) // 등 모서리 살
            {
                Vector3 offset = Quaternion.Euler(0f, 45f + (corner * 90f), 0f) * new Vector3(0.26f, 0f, 0f); // 위치
                TownKit.Box(lamp, "LanternPost", new Vector3(0f, 3.5f, 0f) + offset, new Vector3(0.03f, 0.54f, 0.03f), iron, false); // 살
            }

            TownKit.Box(lamp, "LanternRoof", new Vector3(0f, 3.8f, 0f), new Vector3(0.5f, 0.07f, 0.5f), iron, false); // 지붕
            TownKit.Box(lamp, "LanternCap", new Vector3(0f, 3.88f, 0f), new Vector3(0.3f, 0.1f, 0.3f), iron, false, new Vector3(0f, 45f, 0f)); // 지붕 윗단
            TownKit.Cylinder(lamp, "Vent", new Vector3(0f, 3.98f, 0f), 0.12f, 0.12f, iron); // 환기통
            TownKit.Sphere(lamp, "Finial", new Vector3(0f, 4.08f, 0f), Vector3.one * 0.08f, iron); // 꼭지
            TownKit.PointLight(lamp, "Light", new Vector3(0f, 3.5f, 0f), 11f, 1.5f, new Color(1f, 0.76f, 0.48f), 0.45f); // 조명
        }

        public static void IronRailing(Transform parent, string name, Vector3 from, Vector3 to, float height) // 창살 주철 난간
        {
            Vector3 delta = to - from; // 방향
            float length = delta.magnitude; // 길이
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg; // 방향각
            Transform railing = TownKit.Group(parent, name, (from + to) * 0.5f, yaw); // 묶음 (길이 = 로컬 z)
            Material iron = TownPalette.CastIron; // 주철
            TownKit.Box(railing, "Plinth", new Vector3(0f, 0.15f, 0f), new Vector3(0.36f, 0.3f, length), TownPalette.DressingSoot); // 받침돌 (충돌)
            TownKit.Box(railing, "RailTop", new Vector3(0f, height - 0.05f, 0f), new Vector3(0.05f, 0.05f, length), iron, false); // 윗대
            TownKit.Box(railing, "RailLow", new Vector3(0f, 0.42f, 0f), new Vector3(0.05f, 0.05f, length), iron, false); // 아랫대
            TownKit.Box(railing, "Blocker", new Vector3(0f, (height + 0.3f) * 0.5f, 0f), new Vector3(0.06f, height - 0.3f, length), iron, true).GetComponent<Renderer>().enabled = false; // 보이지 않는 충돌 판

            for (float z = (-length * 0.5f) + 0.07f; z < length * 0.5f; z += 0.14f) // 창살
            {
                TownKit.Box(railing, "Bar", new Vector3(0f, (height + 0.3f) * 0.5f, z), new Vector3(0.025f, height - 0.3f, 0.025f), iron, false); // 창살
                TownKit.Box(railing, "Spear", new Vector3(0f, height + 0.04f, z), new Vector3(0.05f, 0.05f, 0.05f), iron, false, new Vector3(45f, 0f, 45f)); // 창끝
            }

            int posts = Mathf.Max(1, Mathf.RoundToInt(length / 2.4f)); // 기둥 수

            for (int index = 0; index <= posts; index++) // 기둥
            {
                float z = (-length * 0.5f) + (index * length / posts); // 위치
                TownKit.Box(railing, "Post", new Vector3(0f, (height + 0.3f) * 0.5f, z), new Vector3(0.08f, height - 0.2f, 0.08f), iron, false); // 기둥
                TownKit.Sphere(railing, "PostKnob", new Vector3(0f, height + 0.12f, z), Vector3.one * 0.1f, iron); // 기둥 머리
            }
        }

        public static void PillarBox(Transform parent, Vector3 position, float yaw) // 빨간 원통 우체통
        {
            Transform box = TownKit.Group(parent, "PillarBox", position, yaw); // 묶음 (투입구 = +z)
            TownKit.Cylinder(box, "Plinth", new Vector3(0f, 0.08f, 0f), 0.58f, 0.16f, TownPalette.PostRed, true); // 받침
            TownKit.Cylinder(box, "Body", new Vector3(0f, 0.72f, 0f), 0.5f, 1.12f, TownPalette.PostRed, true); // 몸통
            TownKit.Cylinder(box, "Cap", new Vector3(0f, 1.34f, 0f), 0.6f, 0.12f, TownPalette.PostRed); // 갓
            TownKit.Sphere(box, "Dome", new Vector3(0f, 1.42f, 0f), new Vector3(0.5f, 0.24f, 0.5f), TownPalette.PostRed); // 돔
            TownKit.Box(box, "Slot", new Vector3(0f, 1.1f, 0.25f), new Vector3(0.24f, 0.04f, 0.02f), TownPalette.PaintBlack, false); // 투입구
            TownKit.Box(box, "Plate", new Vector3(0f, 0.78f, 0.25f), new Vector3(0.18f, 0.12f, 0.01f), TownPalette.PaintCream, false); // 수거 시간 판
            TownKit.Box(box, "Door", new Vector3(0f, 0.5f, 0.25f), new Vector3(0.2f, 0.3f, 0.01f), TownPalette.PostRed, false); // 문
        }

        public static void Bollard(Transform parent, Vector3 position) // 주철 말뚝
        {
            TownKit.Cylinder(parent, "Bollard", position + new Vector3(0f, 0.45f, 0f), 0.2f, 0.9f, TownPalette.CastIron, true); // 몸통
            TownKit.Cylinder(parent, "BollardRing", position + new Vector3(0f, 0.72f, 0f), 0.24f, 0.05f, TownPalette.CastIron); // 테
            TownKit.Sphere(parent, "BollardCap", position + new Vector3(0f, 0.92f, 0f), Vector3.one * 0.18f, TownPalette.CastIron); // 머리
        }

        public static void Smokestack(Transform parent, Vector3 position, float height) // 공장 굴뚝 (가늘어지는 벽돌 + 석재 띠 + 연기)
        {
            Transform stack = TownKit.Group(parent, "Smokestack", position); // 묶음
            TownKit.Box(stack, "Base", new Vector3(0f, 1.5f, 0f), new Vector3(3.4f, 3f, 3.4f), TownPalette.SootBrick); // 받침
            TownKit.Box(stack, "BaseCap", new Vector3(0f, 3.05f, 0f), new Vector3(3.6f, 0.2f, 3.6f), TownPalette.DressingSoot, false); // 받침 갓
            const int segments = 5; // 단 수
            float segmentHeight = (height - 3f) / segments; // 단 높이

            for (int index = 0; index < segments; index++) // 단
            {
                float size = Mathf.Lerp(2.8f, 1.8f, (float)index / (segments - 1)); // 폭
                float y = 3f + ((index + 0.5f) * segmentHeight); // 중심
                TownKit.Box(stack, "Shaft", new Vector3(0f, y, 0f), new Vector3(size, segmentHeight + 0.02f, size), index % 2 == 0 ? TownPalette.SootBrick : TownPalette.VictorianBrick, index == 0); // 몸통
                TownKit.Box(stack, "Band", new Vector3(0f, 3f + ((index + 1) * segmentHeight) - 0.1f, 0f), new Vector3(size + 0.12f, 0.2f, size + 0.12f), TownPalette.DressingSoot, false); // 띠
            }

            TownKit.Box(stack, "Crown", new Vector3(0f, height + 0.2f, 0f), new Vector3(2.3f, 0.4f, 2.3f), TownPalette.DressingSoot, false); // 머리
            TownKit.Box(stack, "Mouth", new Vector3(0f, height + 0.41f, 0f), new Vector3(1.4f, 0.02f, 1.4f), TownPalette.Soot, false); // 그을린 입구
            TownKit.Smoke(stack, new Vector3(0f, height + 0.6f, 0f)); // 연기
        }

        public static void FactoryHall(Transform parent, Vector3 position, float yaw, float width, float depth, float height) // 톱니 지붕 공장 건물 (외관만)
        {
            Transform hall = TownKit.Group(parent, "FactoryHall", position, yaw); // 묶음
            TownKit.Box(hall, "Walls", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), TownPalette.SootBrick); // 벽체
            int bays = Mathf.Max(3, Mathf.RoundToInt(depth / 3.2f)); // 칸 수

            for (int index = 0; index < bays; index++) // 긴 벽의 아치 창
            {
                float z = (-depth * 0.5f) + ((index + 0.5f) * depth / bays); // 위치

                for (int side = -1; side <= 1; side += 2) // 양쪽 벽
                {
                    float x = side * ((width * 0.5f) + 0.02f); // 벽 면
                    TownKit.Box(hall, "Window", new Vector3(x, height * 0.55f, z), new Vector3(0.04f, height * 0.5f, 1.6f), index % 3 == 1 ? TownPalette.WindowLit : TownPalette.WindowDark, false); // 창
                    TownKit.Box(hall, "Pier", new Vector3(side * ((width * 0.5f) + 0.12f), height * 0.5f, z + (depth / bays * 0.5f)), new Vector3(0.24f, height, 0.5f), TownPalette.VictorianBrick, false); // 벽 기둥
                    TownKit.Box(hall, "Arch", new Vector3(x + (side * 0.02f), (height * 0.8f) + 0.1f, z), new Vector3(0.05f, 0.3f, 1.8f), TownPalette.DressingSoot, false); // 창 머리
                }

                GameObject tooth = TownKit.Prism(hall, "SawTooth", new Vector3(0f, height, z), width, depth / bays, 2.2f, TownPalette.Slate, false); // 톱니 지붕
                tooth.transform.localScale = new Vector3(width, 2.2f, depth / bays); // 크기
                TownKit.Box(hall, "RoofLight", new Vector3(0f, height + 1.1f, z + (depth / bays * 0.25f)), new Vector3(width - 0.2f, 1.8f, 0.05f), TownPalette.WindowDark, false, new Vector3(-50f, 0f, 0f)); // 북향 채광창
            }

            TownKit.Box(hall, "Cornice", new Vector3(0f, height - 0.1f, 0f), new Vector3(width + 0.3f, 0.25f, depth + 0.3f), TownPalette.DressingSoot, false); // 처마
            TownKit.Label(hall, "Name", "제철소", new Vector3(0f, height * 0.72f, (depth * 0.5f) + 0.03f), 180f, 1.0f, new Color(0.84f, 0.8f, 0.68f)); // 벽 글씨
        }

        public static void Bench(Transform parent, Vector3 position, float yaw) // 긴 의자
        {
            Transform bench = TownKit.Group(parent, "Bench", position, yaw); // 묶음
            TownKit.Box(bench, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.07f, 0.45f), TownPalette.WoodLight); // 좌판
            TownKit.Box(bench, "Back", new Vector3(0f, 0.8f, -0.2f), new Vector3(1.8f, 0.3f, 0.05f), TownPalette.WoodLight, false, new Vector3(-10f, 0f, 0f)); // 등받이
            TownKit.Box(bench, "Leg_L", new Vector3(-0.75f, 0.25f, 0f), new Vector3(0.08f, 0.45f, 0.42f), TownPalette.Iron, false); // 다리
            TownKit.Box(bench, "Leg_R", new Vector3(0.75f, 0.25f, 0f), new Vector3(0.08f, 0.45f, 0.42f), TownPalette.Iron, false); // 다리
        }

        public static void Barrel(Transform parent, Vector3 position, float height = 0.9f) // 나무통
        {
            Transform barrel = TownKit.Group(parent, "Barrel", position); // 묶음
            TownKit.Cylinder(barrel, "Body", new Vector3(0f, height * 0.5f, 0f), 0.62f, height, TownPalette.WoodLight, true); // 몸통
            TownKit.Cylinder(barrel, "Belly", new Vector3(0f, height * 0.5f, 0f), 0.68f, height * 0.5f, TownPalette.WoodLight); // 배
            TownKit.Cylinder(barrel, "Hoop_1", new Vector3(0f, height * 0.15f, 0f), 0.65f, 0.05f, TownPalette.Iron); // 테
            TownKit.Cylinder(barrel, "Hoop_2", new Vector3(0f, height * 0.85f, 0f), 0.65f, 0.05f, TownPalette.Iron); // 테
            TownKit.Cylinder(barrel, "Lid", new Vector3(0f, height + 0.005f, 0f), 0.56f, 0.02f, TownPalette.WoodDark); // 뚜껑
        }

        public static void Crate(Transform parent, Vector3 position, float size, float yaw) // 나무 상자
        {
            Transform crate = TownKit.Group(parent, "Crate", position, yaw); // 묶음
            TownKit.Box(crate, "Body", new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, TownPalette.WoodLight); // 몸통
            float edge = size * 0.08f; // 테두리 두께

            foreach (float y in new[] { edge * 0.5f, size - (edge * 0.5f) }) // 위·아래 테
            {
                TownKit.Box(crate, "Band", new Vector3(0f, y, 0f), new Vector3(size + 0.02f, edge, size + 0.02f), TownPalette.WoodDark, false); // 테
            }

            TownKit.Box(crate, "Cross_A", new Vector3(0f, size * 0.5f, (size * 0.5f) + 0.01f), new Vector3(size * 1.2f, edge, 0.02f), TownPalette.WoodDark, false, new Vector3(0f, 0f, 45f)); // 대각 살
            TownKit.Box(crate, "Cross_B", new Vector3(0f, size * 0.5f, -(size * 0.5f) - 0.01f), new Vector3(size * 1.2f, edge, 0.02f), TownPalette.WoodDark, false, new Vector3(0f, 0f, -45f)); // 대각 살
        }

        public static void CrateStack(Transform parent, Vector3 position, float yaw) // 상자 더미
        {
            Transform stack = TownKit.Group(parent, "CrateStack", position, yaw); // 묶음
            Crate(stack, new Vector3(-0.35f, 0f, 0f), 0.7f, 5f); // 아래
            Crate(stack, new Vector3(0.4f, 0f, 0.05f), 0.6f, -8f); // 아래
            Crate(stack, new Vector3(-0.3f, 0.7f, 0.02f), 0.55f, 20f); // 위
            Sack(stack, new Vector3(0.45f, 0.6f, 0f)); // 자루
        }

        public static void Sack(Transform parent, Vector3 position) // 자루
        {
            TownKit.Sphere(parent, "Sack", position + new Vector3(0f, 0.2f, 0f), new Vector3(0.45f, 0.42f, 0.36f), TownPalette.Sack); // 몸통
            TownKit.Cylinder(parent, "SackTie", position + new Vector3(0f, 0.44f, 0f), 0.1f, 0.1f, TownPalette.Rope); // 묶은 끝
        }

        public static void Well(Transform parent, Vector3 position) // 우물
        {
            Transform well = TownKit.Group(parent, "Well", position); // 묶음
            TownKit.Cylinder(well, "Ring", new Vector3(0f, 0.45f, 0f), 1.8f, 0.9f, TownPalette.StoneWall, true); // 돌 둘레
            TownKit.Cylinder(well, "Rim", new Vector3(0f, 0.93f, 0f), 1.95f, 0.08f, TownPalette.Plinth); // 테두리
            TownKit.Cylinder(well, "Water", new Vector3(0f, 0.7f, 0f), 1.5f, 0.02f, TownPalette.Water); // 물
            TownKit.Box(well, "Post_L", new Vector3(-0.85f, 1.6f, 0f), new Vector3(0.14f, 1.4f, 0.14f), TownPalette.Timber, true); // 기둥
            TownKit.Box(well, "Post_R", new Vector3(0.85f, 1.6f, 0f), new Vector3(0.14f, 1.4f, 0.14f), TownPalette.Timber, true); // 기둥
            TownKit.Cylinder(well, "Axle", new Vector3(0f, 2.05f, 0f), 0.12f, 1.7f, TownPalette.Timber, false, new Vector3(0f, 0f, 90f)); // 축
            TownKit.Cylinder(well, "RopeLine", new Vector3(0f, 1.55f, 0f), 0.03f, 1.0f, TownPalette.Rope); // 밧줄
            TownKit.Cylinder(well, "Bucket", new Vector3(0f, 1.0f, 0f), 0.3f, 0.3f, TownPalette.WoodLight); // 두레박
            TownKit.Box(well, "Roof_A", new Vector3(0f, 2.5f, 0.45f), new Vector3(2.2f, 0.06f, 1.1f), TownPalette.RoofTile, false, new Vector3(35f, 0f, 0f)); // 지붕
            TownKit.Box(well, "Roof_B", new Vector3(0f, 2.5f, -0.45f), new Vector3(2.2f, 0.06f, 1.1f), TownPalette.RoofTile, false, new Vector3(-35f, 0f, 0f)); // 지붕
        }

        public static void Tree(Transform parent, Vector3 position, float scale) // 나무
        {
            Transform tree = TownKit.Group(parent, "Tree", position, position.x * 37f); // 묶음 (위치로 회전 변화)
            TownKit.Cylinder(tree, "Trunk", new Vector3(0f, 1.4f * scale, 0f), 0.35f * scale, 2.8f * scale, TownPalette.Bark, true); // 줄기
            TownKit.Sphere(tree, "Crown_A", new Vector3(0f, 3.4f * scale, 0f), new Vector3(2.6f, 2.2f, 2.6f) * scale, TownPalette.Leaf); // 잎
            TownKit.Sphere(tree, "Crown_B", new Vector3(0.7f * scale, 2.9f * scale, 0.4f * scale), new Vector3(1.8f, 1.6f, 1.8f) * scale, TownPalette.LeafDark); // 잎
            TownKit.Sphere(tree, "Crown_C", new Vector3(-0.6f * scale, 3.1f * scale, -0.5f * scale), new Vector3(1.7f, 1.5f, 1.7f) * scale, TownPalette.Leaf); // 잎
            TownKit.Sphere(tree, "Crown_D", new Vector3(0.1f * scale, 4.3f * scale, 0.1f * scale), new Vector3(1.5f, 1.3f, 1.5f) * scale, TownPalette.LeafDark); // 잎
        }

        public static void Bush(Transform parent, Vector3 position, float scale) // 덤불
        {
            TownKit.Sphere(parent, "Bush", position + new Vector3(0f, 0.35f * scale, 0f), new Vector3(1.2f, 0.8f, 1.0f) * scale, TownPalette.LeafDark); // 덤불
            TownKit.Sphere(parent, "Bush_B", position + new Vector3(0.45f * scale, 0.3f * scale, 0.2f * scale), new Vector3(0.8f, 0.6f, 0.7f) * scale, TownPalette.Leaf); // 덤불
        }

        public static void StoneWallRun(Transform parent, string name, Vector3 from, Vector3 to, float height) // 돌담 (두 점 사이)
        {
            BrickWallRun(parent, name, from, to, height, TownPalette.StoneWall); // 돌담
        }

        public static void BrickWallRun(Transform parent, string name, Vector3 from, Vector3 to, float height, Material body) // 담장 (벽돌 몸통 + 석재 기둥·갓돌)
        {
            Vector3 delta = to - from; // 방향
            float length = delta.magnitude; // 길이

            if (length < 0.05f) // 너무 짧음
            {
                return; // 생략
            }

            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg; // 방향각
            Transform wall = TownKit.Group(parent, name, (from + to) * 0.5f, yaw); // 묶음 (길이 = 로컬 z)
            TownKit.Box(wall, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.5f, height, length), body); // 몸통
            TownKit.Box(wall, "Cap", new Vector3(0f, height + 0.06f, 0f), new Vector3(0.62f, 0.12f, length + 0.1f), TownPalette.DressingSoot, false); // 갓돌

            for (float y = 0.9f; y < height - 0.2f; y += 0.9f) // 벽돌 줄 그림자
            {
                TownKit.Box(wall, "Course", new Vector3(0f, y, 0f), new Vector3(0.512f, 0.025f, length), TownPalette.BrickCourse, false); // 줄
            }
            int posts = Mathf.FloorToInt(length / 5f); // 기둥 수

            for (int index = 0; index <= posts; index++) // 기둥
            {
                float z = posts == 0 ? 0f : (-length * 0.5f) + (index * length / posts); // 위치
                TownKit.Box(wall, "Pier", new Vector3(0f, (height + 0.25f) * 0.5f, z), new Vector3(0.7f, height + 0.25f, 0.7f), TownPalette.SootBrick, false); // 벽돌 기둥
                TownKit.Box(wall, "PierCap", new Vector3(0f, height + 0.32f, z), new Vector3(0.82f, 0.14f, 0.82f), TownPalette.DressingSoot, false); // 기둥 갓
            }
        }

        public static void WoodFence(Transform parent, string name, Vector3 from, Vector3 to) // 나무 울타리
        {
            Vector3 delta = to - from; // 방향
            float length = delta.magnitude; // 길이
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg; // 방향각
            Transform fence = TownKit.Group(parent, name, (from + to) * 0.5f, yaw); // 묶음
            TownKit.Box(fence, "Rail_Top", new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 0.1f, length), TownPalette.WoodLight); // 가로대 (충돌)
            TownKit.Box(fence, "Rail_Low", new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 0.1f, length), TownPalette.WoodLight); // 가로대
            int posts = Mathf.Max(1, Mathf.FloorToInt(length / 2f)); // 기둥 수

            for (int index = 0; index <= posts; index++) // 기둥
            {
                float z = (-length * 0.5f) + (index * length / posts); // 위치
                TownKit.Box(fence, "Post", new Vector3(0f, 0.6f, z), new Vector3(0.14f, 1.2f, 0.14f), TownPalette.Timber, false); // 기둥
            }
        }

        public static void NoticeBoard(Transform parent, Vector3 position, float yaw, string title) // 게시판
        {
            Transform board = TownKit.Group(parent, "NoticeBoard", position, yaw); // 묶음 (앞면 +z)
            TownKit.Box(board, "Post_L", new Vector3(-0.9f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), TownPalette.Timber, true); // 기둥
            TownKit.Box(board, "Post_R", new Vector3(0.9f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), TownPalette.Timber, true); // 기둥
            TownKit.Box(board, "Board", new Vector3(0f, 1.45f, 0f), new Vector3(1.7f, 1.0f, 0.06f), TownPalette.WoodLight, false); // 판
            TownKit.Box(board, "Roof", new Vector3(0f, 2.25f, 0.05f), new Vector3(2.1f, 0.06f, 0.5f), TownPalette.RoofTile, false, new Vector3(20f, 0f, 0f)); // 지붕
            TownKit.Label(board, "Title", title, new Vector3(0f, 2.05f, 0.08f), 180f, 0.14f, new Color(0.2f, 0.12f, 0.06f)); // 제목
            Material[] papers = { TownPalette.Paper, TownPalette.ClothCream, TownPalette.Paper }; // 종이

            for (int index = 0; index < 5; index++) // 붙은 종이
            {
                float x = -0.6f + (index * 0.3f); // 위치
                float y = 1.35f + ((index % 2) * 0.22f); // 높이
                TownKit.Box(board, "Paper", new Vector3(x, y, 0.04f), new Vector3(0.24f, 0.32f, 0.005f), papers[index % 3], false, new Vector3(0f, 0f, (index - 2) * 4f)); // 종이
                TownKit.Sphere(board, "Pin", new Vector3(x, y + 0.13f, 0.05f), Vector3.one * 0.025f, TownPalette.ClothRed); // 압정
            }
        }

        public static void HangingLantern(Transform parent, Vector3 position, float dropLength) // 매달린 등 (position = 매단 점)
        {
            Transform lantern = TownKit.Group(parent, "HangingLantern", position); // 묶음
            TownKit.Cylinder(lantern, "Chain", new Vector3(0f, -dropLength * 0.5f, 0f), 0.02f, dropLength, TownPalette.Iron); // 사슬
            TownKit.Box(lantern, "Cap", new Vector3(0f, -dropLength - 0.04f, 0f), new Vector3(0.22f, 0.06f, 0.22f), TownPalette.Iron, false); // 머리
            TownKit.Box(lantern, "Glow", new Vector3(0f, -dropLength - 0.2f, 0f), new Vector3(0.16f, 0.26f, 0.16f), TownPalette.LampGlow, false); // 불빛
            TownKit.PointLight(lantern, "Light", new Vector3(0f, -dropLength - 0.3f, 0f), 6f, 1.2f, new Color(1f, 0.72f, 0.42f), 0.3f); // 조명
        }

        public static void LaundryLine(Transform parent, Vector3 from, Vector3 to) // 빨랫줄 (두 벽 사이)
        {
            Vector3 delta = to - from; // 방향
            float length = delta.magnitude; // 길이
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg; // 방향각
            Transform line = TownKit.Group(parent, "LaundryLine", (from + to) * 0.5f, yaw); // 묶음 (줄 = 로컬 z)
            TownKit.Cylinder(line, "Rope", Vector3.zero, 0.02f, length, TownPalette.Rope, false, new Vector3(90f, 0f, 0f)); // 줄
            Material[] cloths = { TownPalette.ClothCream, TownPalette.ClothBlue, TownPalette.ClothRed, TownPalette.ClothGreen }; // 천

            for (int index = 0; index < 4; index++) // 빨래
            {
                float z = (-length * 0.35f) + (index * length * 0.23f); // 위치
                float height = 0.5f + ((index % 2) * 0.25f); // 길이
                TownKit.Box(line, "Cloth", new Vector3(0f, -height * 0.5f, z), new Vector3(0.02f, height, 0.5f), cloths[index], false, new Vector3(0f, 0f, 4f)); // 천
            }
        }

        public static void Puddle(Transform parent, Vector3 position, float size) // 웅덩이
        {
            TownKit.Cylinder(parent, "Puddle", position + new Vector3(0f, 0.012f, 0f), size, 0.01f, TownPalette.Puddle); // 얇은 원판
        }

        public static void WallPipe(Transform parent, Vector3 position, float height, float outward) // 벽 배수관 (outward = 벽에서 나오는 z 방향 부호)
        {
            TownKit.Cylinder(parent, "Pipe", position + new Vector3(0f, height * 0.5f, 0f), 0.12f, height, TownPalette.Iron); // 관
            TownKit.Box(parent, "PipeFoot", position + new Vector3(0f, 0.08f, 0.12f * outward), new Vector3(0.14f, 0.14f, 0.3f), TownPalette.Iron, false); // 배수구
        }

        public static void HitchingPost(Transform parent, Vector3 position, float yaw) // 말 묶는 기둥
        {
            Transform post = TownKit.Group(parent, "HitchingPost", position, yaw); // 묶음
            TownKit.Box(post, "Post_A", new Vector3(-0.9f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.14f), TownPalette.Timber, true); // 기둥
            TownKit.Box(post, "Post_B", new Vector3(0.9f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.14f), TownPalette.Timber, true); // 기둥
            TownKit.Box(post, "Rail", new Vector3(0f, 1.0f, 0f), new Vector3(2.0f, 0.1f, 0.1f), TownPalette.Timber, false); // 가로대
            TownKit.Cylinder(post, "Ring", new Vector3(0.3f, 0.9f, 0.07f), 0.12f, 0.02f, TownPalette.Iron, false, new Vector3(90f, 0f, 0f)); // 고리
        }

        public static void WaterTrough(Transform parent, Vector3 position, float yaw) // 물통
        {
            Transform trough = TownKit.Group(parent, "WaterTrough", position, yaw); // 묶음
            TownKit.Box(trough, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.6f, 0.6f), TownPalette.WoodDark); // 몸통
            TownKit.Box(trough, "Water", new Vector3(0f, 0.56f, 0f), new Vector3(1.45f, 0.02f, 0.45f), TownPalette.Water, false); // 물
        }

        public static void Signpost(Transform parent, Vector3 position, float yaw, string text) // 방향 표지판 (화살표 방향 = 로컬 +x)
        {
            Transform sign = TownKit.Group(parent, "Signpost", position, yaw); // 묶음
            TownKit.Box(sign, "Post", new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 2.4f, 0.14f), TownPalette.Timber, true); // 기둥
            TownKit.Box(sign, "Plank", new Vector3(0.55f, 2.0f, 0f), new Vector3(1.1f, 0.3f, 0.06f), TownPalette.WoodLight, false); // 판
            TownKit.Box(sign, "Tip", new Vector3(1.14f, 2.0f, 0f), new Vector3(0.22f, 0.22f, 0.06f), TownPalette.WoodLight, false, new Vector3(0f, 0f, 45f)); // 화살촉
            TownKit.Label(sign, "Text_A", text, new Vector3(0.55f, 2.0f, 0.04f), 180f, 0.16f, new Color(0.2f, 0.12f, 0.06f)); // 앞 글자
            TownKit.Label(sign, "Text_B", text, new Vector3(0.55f, 2.0f, -0.04f), 0f, 0.16f, new Color(0.2f, 0.12f, 0.06f)); // 뒤 글자
        }

        public static void Arch(Transform parent, string name, Vector3 position, float yaw, float width, float height, string text, Material pillar) // 출입 아치 (통로 = 로컬 z 방향, 주철 들보)
        {
            Transform arch = TownKit.Group(parent, name, position, yaw); // 묶음
            TownKit.Box(arch, "Pillar_L", new Vector3(-(width * 0.5f) - 0.3f, height * 0.5f, 0f), new Vector3(0.6f, height, 0.7f), pillar); // 기둥
            TownKit.Box(arch, "Pillar_R", new Vector3((width * 0.5f) + 0.3f, height * 0.5f, 0f), new Vector3(0.6f, height, 0.7f), pillar); // 기둥
            TownKit.Box(arch, "PillarCap_L", new Vector3(-(width * 0.5f) - 0.3f, height + 0.08f, 0f), new Vector3(0.78f, 0.16f, 0.86f), TownPalette.DressingSoot, false); // 기둥 머리
            TownKit.Box(arch, "PillarCap_R", new Vector3((width * 0.5f) + 0.3f, height + 0.08f, 0f), new Vector3(0.78f, 0.16f, 0.86f), TownPalette.DressingSoot, false); // 기둥 머리
            TownKit.Sphere(arch, "Finial_L", new Vector3(-(width * 0.5f) - 0.3f, height + 0.9f, 0f), Vector3.one * 0.36f, TownPalette.DressingSoot); // 공 장식
            TownKit.Sphere(arch, "Finial_R", new Vector3((width * 0.5f) + 0.3f, height + 0.9f, 0f), Vector3.one * 0.36f, TownPalette.DressingSoot); // 공 장식
            TownKit.Box(arch, "FinialNeck_L", new Vector3(-(width * 0.5f) - 0.3f, height + 0.45f, 0f), new Vector3(0.3f, 0.6f, 0.3f), TownPalette.DressingSoot, false); // 받침
            TownKit.Box(arch, "FinialNeck_R", new Vector3((width * 0.5f) + 0.3f, height + 0.45f, 0f), new Vector3(0.3f, 0.6f, 0.3f), TownPalette.DressingSoot, false); // 받침
            TownKit.Box(arch, "Beam", new Vector3(0f, height + 0.35f, 0f), new Vector3(width + 0.2f, 0.36f, 0.3f), TownPalette.CastIron, false); // 주철 들보
            TownKit.Box(arch, "Lattice", new Vector3(0f, height - 0.05f, 0f), new Vector3(width, 0.06f, 0.1f), TownPalette.CastIron, false); // 아래 띠

            for (float x = (-width * 0.5f) + 0.4f; x < width * 0.5f; x += 0.8f) // 격자 장식
            {
                TownKit.Box(arch, "LatticeBar", new Vector3(x, height + 0.15f, 0f), new Vector3(0.03f, 0.42f, 0.04f), TownPalette.CastIron, false, new Vector3(0f, 0f, 35f)); // 사선
            }
            TownKit.Box(arch, "SignBoard", new Vector3(0f, height + 0.35f, 0.27f), new Vector3(Mathf.Min(width, 3.2f), 0.34f, 0.04f), TownPalette.SignBoard, false); // 간판
            TownKit.Box(arch, "SignBoard_Back", new Vector3(0f, height + 0.35f, -0.27f), new Vector3(Mathf.Min(width, 3.2f), 0.34f, 0.04f), TownPalette.SignBoard, false); // 간판
            TownKit.Label(arch, "Text_Front", text, new Vector3(0f, height + 0.35f, 0.3f), 180f, 0.24f, new Color(0.95f, 0.88f, 0.62f)); // +z 쪽 글자
            TownKit.Label(arch, "Text_Back", text, new Vector3(0f, height + 0.35f, -0.3f), 0f, 0.24f, new Color(0.95f, 0.88f, 0.62f)); // -z 쪽 글자
            HangingLantern(arch, new Vector3(-(width * 0.5f) + 0.3f, height + 0.15f, 0.3f), 0.35f); // 등
            HangingLantern(arch, new Vector3((width * 0.5f) - 0.3f, height + 0.15f, 0.3f), 0.35f); // 등
        }

        public static void Rug(Transform parent, Vector3 position, Vector2 size, Material material) // 바닥 깔개
        {
            TownKit.Box(parent, "Rug", position + new Vector3(0f, 0.006f, 0f), new Vector3(size.x, 0.012f, size.y), material, false); // 깔개
            TownKit.Box(parent, "RugBorder", position + new Vector3(0f, 0.004f, 0f), new Vector3(size.x + 0.12f, 0.008f, size.y + 0.12f), TownPalette.ClothCream, false); // 테두리
        }

        public static void Table(Transform parent, Vector3 position, Vector3 size, float yaw) // 탁자 (size = 윗판 폭·높이·깊이)
        {
            Transform table = TownKit.Group(parent, "Table", position, yaw); // 묶음
            TownKit.Box(table, "Top", new Vector3(0f, size.y - 0.03f, 0f), new Vector3(size.x, 0.06f, size.z), TownPalette.WoodLight); // 윗판
            float x = (size.x * 0.5f) - 0.08f; // 다리 x
            float z = (size.z * 0.5f) - 0.08f; // 다리 z

            foreach (Vector2 corner in new[] { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z) }) // 다리
            {
                TownKit.Box(table, "Leg", new Vector3(corner.x, (size.y - 0.06f) * 0.5f, corner.y), new Vector3(0.08f, size.y - 0.06f, 0.08f), TownPalette.WoodDark, false); // 다리
            }
        }

        public static void Chair(Transform parent, Vector3 position, float yaw) // 의자 (앉는 방향 = +z)
        {
            Transform chair = TownKit.Group(parent, "Chair", position, yaw); // 묶음
            TownKit.Box(chair, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.46f, 0.05f, 0.46f), TownPalette.WoodLight); // 좌판
            TownKit.Box(chair, "Back", new Vector3(0f, 0.78f, -0.21f), new Vector3(0.46f, 0.6f, 0.05f), TownPalette.WoodLight, false); // 등받이
            foreach (Vector2 corner in new[] { new Vector2(-0.19f, -0.19f), new Vector2(0.19f, -0.19f), new Vector2(-0.19f, 0.19f), new Vector2(0.19f, 0.19f) }) // 다리
            {
                TownKit.Box(chair, "Leg", new Vector3(corner.x, 0.22f, corner.y), new Vector3(0.05f, 0.44f, 0.05f), TownPalette.WoodDark, false); // 다리
            }
        }

        public static void Bookshelf(Transform parent, Vector3 position, float yaw, float width) // 책장 (앞면 +z)
        {
            Transform shelf = TownKit.Group(parent, "Bookshelf", position, yaw); // 묶음
            TownKit.Box(shelf, "Body", new Vector3(0f, 1.0f, -0.17f), new Vector3(width, 2.0f, 0.06f), TownPalette.WoodDark); // 뒤판 (충돌)
            TownKit.Box(shelf, "Side_L", new Vector3(-(width * 0.5f) + 0.03f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.4f), TownPalette.WoodDark); // 옆판
            TownKit.Box(shelf, "Side_R", new Vector3((width * 0.5f) - 0.03f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.4f), TownPalette.WoodDark); // 옆판
            Material[] covers = { TownPalette.ClothRed, TownPalette.ClothBlue, TownPalette.ClothGreen, TownPalette.ClothCream, TownPalette.Sack }; // 책 표지
            int seed = 7; // 고정 변화

            for (int level = 0; level < 4; level++) // 칸
            {
                float y = 0.1f + (level * 0.5f); // 칸 높이
                TownKit.Box(shelf, "Board", new Vector3(0f, y, 0f), new Vector3(width - 0.1f, 0.04f, 0.38f), TownPalette.WoodLight, false); // 선반
                float x = -(width * 0.5f) + 0.12f; // 책 시작

                while (x < (width * 0.5f) - 0.2f) // 책
                {
                    seed = ((seed * 31) + 11) % 97; // 의사 난수
                    float bookWidth = 0.05f + ((seed % 5) * 0.012f); // 두께
                    float bookHeight = 0.28f + ((seed % 7) * 0.02f); // 높이

                    if (seed % 11 != 0) // 가끔 빈 자리
                    {
                        TownKit.Box(shelf, "Book", new Vector3(x + (bookWidth * 0.5f), y + 0.02f + (bookHeight * 0.5f), 0.02f), new Vector3(bookWidth, bookHeight, 0.26f), covers[seed % covers.Length], false); // 책
                    }

                    x += bookWidth + 0.01f; // 다음
                }
            }

            TownKit.Box(shelf, "Top", new Vector3(0f, 2.02f, 0f), new Vector3(width, 0.05f, 0.42f), TownPalette.WoodDark, false); // 윗판
        }

        public static void WallShelf(Transform parent, Vector3 position, float yaw, float width) // 벽 선반 + 단지 (앞면 +z)
        {
            Transform shelf = TownKit.Group(parent, "WallShelf", position, yaw); // 묶음
            Material[] jars = { TownPalette.ClothBlue, TownPalette.Brass, TownPalette.ClothCream, TownPalette.ClothGreen }; // 단지 색

            for (int level = 0; level < 2; level++) // 두 단
            {
                float y = level * 0.55f; // 높이
                TownKit.Box(shelf, "Board", new Vector3(0f, y, 0f), new Vector3(width, 0.04f, 0.3f), TownPalette.WoodLight, false); // 판
                TownKit.Box(shelf, "Bracket_L", new Vector3(-(width * 0.5f) + 0.1f, y - 0.1f, -0.05f), new Vector3(0.04f, 0.18f, 0.2f), TownPalette.Iron, false); // 받침
                TownKit.Box(shelf, "Bracket_R", new Vector3((width * 0.5f) - 0.1f, y - 0.1f, -0.05f), new Vector3(0.04f, 0.18f, 0.2f), TownPalette.Iron, false); // 받침
                int count = Mathf.FloorToInt(width / 0.3f); // 단지 수

                for (int index = 0; index < count; index++) // 단지
                {
                    float x = -(width * 0.5f) + 0.2f + (index * 0.3f); // 위치
                    float height = 0.14f + (((index + level) % 3) * 0.05f); // 높이
                    TownKit.Cylinder(shelf, "Jar", new Vector3(x, y + 0.02f + (height * 0.5f), 0f), 0.14f, height, jars[(index + level) % jars.Length]); // 단지
                }
            }
        }

        public static void Cabinet(Transform parent, Vector3 position, float yaw) // 서류함 (앞면 +z)
        {
            Transform cabinet = TownKit.Group(parent, "Cabinet", position, yaw); // 묶음
            TownKit.Box(cabinet, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(0.8f, 1.3f, 0.5f), TownPalette.WoodDark); // 몸통

            for (int drawer = 0; drawer < 4; drawer++) // 서랍
            {
                float y = 0.2f + (drawer * 0.3f); // 높이
                TownKit.Box(cabinet, "Drawer", new Vector3(0f, y, 0.255f), new Vector3(0.7f, 0.26f, 0.02f), TownPalette.WoodLight, false); // 서랍 앞판
                TownKit.Box(cabinet, "Handle", new Vector3(0f, y, 0.275f), new Vector3(0.16f, 0.03f, 0.02f), TownPalette.Brass, false); // 손잡이
            }
        }

        public static void Candle(Transform parent, Vector3 position) // 촛대
        {
            TownKit.Cylinder(parent, "CandleBase", position + new Vector3(0f, 0.01f, 0f), 0.12f, 0.02f, TownPalette.Brass); // 받침
            TownKit.Cylinder(parent, "Candle", position + new Vector3(0f, 0.08f, 0f), 0.04f, 0.14f, TownPalette.Paper); // 초
            TownKit.Sphere(parent, "Flame", position + new Vector3(0f, 0.17f, 0f), new Vector3(0.025f, 0.05f, 0.025f), TownPalette.LampGlow); // 불꽃
        }

        public static void WallMap(Transform parent, Vector3 position, float yaw) // 벽 지도 (앞면 +z)
        {
            Transform map = TownKit.Group(parent, "WallMap", position, yaw); // 묶음
            TownKit.Box(map, "Frame", Vector3.zero, new Vector3(1.6f, 1.1f, 0.04f), TownPalette.WoodDark, false); // 액자
            TownKit.Box(map, "Paper", new Vector3(0f, 0f, 0.025f), new Vector3(1.45f, 0.95f, 0.01f), TownPalette.Paper, false); // 종이
            TownKit.Box(map, "Land", new Vector3(-0.2f, 0.05f, 0.032f), new Vector3(0.8f, 0.5f, 0.005f), TownPalette.PlasterGreen, false, new Vector3(0f, 0f, 12f)); // 땅
            TownKit.Box(map, "River", new Vector3(0.25f, -0.1f, 0.034f), new Vector3(0.9f, 0.05f, 0.005f), TownPalette.ClothBlue, false, new Vector3(0f, 0f, -25f)); // 강
            TownKit.Sphere(map, "Mark_A", new Vector3(-0.3f, 0.12f, 0.04f), Vector3.one * 0.05f, TownPalette.ClothRed); // 표시
            TownKit.Sphere(map, "Mark_B", new Vector3(0.35f, -0.25f, 0.04f), Vector3.one * 0.05f, TownPalette.ClothRed); // 표시
        }

        public static void Scale(Transform parent, Vector3 position) // 저울 (장식)
        {
            Transform scale = TownKit.Group(parent, "Scale", position); // 묶음
            TownKit.Cylinder(scale, "Foot", new Vector3(0f, 0.02f, 0f), 0.2f, 0.04f, TownPalette.Brass); // 받침
            TownKit.Cylinder(scale, "Stem", new Vector3(0f, 0.22f, 0f), 0.03f, 0.4f, TownPalette.Brass); // 기둥
            TownKit.Box(scale, "Beam", new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.02f, 0.02f), TownPalette.Brass, false); // 저울대
            TownKit.Cylinder(scale, "Pan_L", new Vector3(-0.23f, 0.3f, 0f), 0.16f, 0.02f, TownPalette.Brass); // 접시
            TownKit.Cylinder(scale, "Pan_R", new Vector3(0.23f, 0.3f, 0f), 0.16f, 0.02f, TownPalette.Brass); // 접시
        }

        public static void Pot(Transform parent, Vector3 position, float scale) // 화분
        {
            TownKit.Cylinder(parent, "Pot", position + new Vector3(0f, 0.2f * scale, 0f), 0.4f * scale, 0.4f * scale, TownPalette.Brick, true); // 화분
            TownKit.Sphere(parent, "Plant", position + new Vector3(0f, 0.6f * scale, 0f), new Vector3(0.6f, 0.7f, 0.6f) * scale, TownPalette.Leaf); // 식물
        }

        public static void Rack(Transform parent, Vector3 position, float yaw, float width) // 창고 선반 랙 (앞면 +z)
        {
            Transform rack = TownKit.Group(parent, "StorageRack", position, yaw); // 묶음
            foreach (float x in new[] { -(width * 0.5f) + 0.04f, (width * 0.5f) - 0.04f }) // 기둥
            {
                TownKit.Box(rack, "Upright", new Vector3(x, 1.1f, 0.25f), new Vector3(0.07f, 2.2f, 0.07f), TownPalette.Iron, false); // 앞 기둥
                TownKit.Box(rack, "Upright", new Vector3(x, 1.1f, -0.25f), new Vector3(0.07f, 2.2f, 0.07f), TownPalette.Iron, false); // 뒤 기둥
            }

            for (int level = 0; level < 3; level++) // 단
            {
                float y = 0.15f + (level * 0.75f); // 높이
                TownKit.Box(rack, "Deck", new Vector3(0f, y, 0f), new Vector3(width, 0.05f, 0.6f), TownPalette.WoodLight, level == 0); // 판
                Crate(rack, new Vector3(-(width * 0.25f), y + 0.025f, 0f), 0.45f, level * 12f); // 상자
                Sack(rack, new Vector3(width * 0.2f, y + 0.025f, 0f)); // 자루
            }
        }
    }
}
