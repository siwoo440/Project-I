using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 모듈 부품 참조
using ProjectI.Generation; // 출입구 규격 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class DungeonPowerModuleBaker // 발전실·배전반 방 모듈을 만듭니다 (31일차 전력 계통)
    {
        public static GameObject BuildPowerPlant(ModuleMaterials materials) // 발전실 — 발전기 1대
        {
            DungeonModuleShape shape = new DungeonModuleShape("Room_PowerPlant_9x7", DungeonModuleCatalog.PowerFolder, ModuleRole.PowerPlant, 1,
                new[] { new ShapeRect(0, 0, 9, 7) },
                new[]
                {
                    new ShapeDoor(4, 0, GridDirection.South), // 출입구
                }); // 형태

            GameObject root = DungeonModuleBaker.Build(shape, materials); // 껍데기
            Transform structure = root.transform.Find("Structure"); // 구조물
            Transform machine = DungeonModuleBaker.CreateChild(structure, "PowerPlantMachine"); // 발전기
            machine.localPosition = new Vector3(4.5f, 0f, 5f); // 방 안쪽
            DungeonModuleBaker.CreateBox(machine, "Base", new Vector3(0f, 0.35f, 0f), new Vector3(2.8f, 0.7f, 1.8f), materials.Trim); // 받침
            DungeonModuleBaker.CreateBox(machine, "Body", new Vector3(0f, 1.35f, 0f), new Vector3(2.4f, 1.4f, 1.4f), materials.Wall); // 본체
            DungeonModuleBaker.CreateBox(machine, "Exhaust", new Vector3(0.9f, 2.5f, 0f), new Vector3(0.4f, 1.2f, 0.4f), materials.Trim); // 배기관
            Transform wheelPivot = DungeonModuleBaker.CreateChild(machine, "Flywheel"); // 회전 부품
            wheelPivot.localPosition = new Vector3(-1.4f, 1.35f, 0f); // 본체 옆
            wheelPivot.localRotation = Quaternion.Euler(0f, 90f, 0f); // 옆면을 향함
            DungeonModuleBaker.CreateBox(wheelPivot, "Spoke_A", new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.16f, 0.18f), materials.Accent); // 살
            DungeonModuleBaker.CreateBox(wheelPivot, "Spoke_B", new Vector3(0f, 0f, 0f), new Vector3(0.16f, 1.1f, 0.18f), materials.Accent); // 살
            GameObject indicator = DungeonModuleBaker.CreateBox(machine, "Indicator", new Vector3(0f, 2.15f, -0.75f), new Vector3(0.24f, 0.24f, 0.1f), materials.Accent); // 표시등
            DungeonPowerPlant plant = machine.gameObject.AddComponent<DungeonPowerPlant>(); // 발전기 기능
            plant.Configure(wheelPivot, indicator.GetComponent<Renderer>()); // 구성
            BoxCollider trigger = machine.gameObject.AddComponent<BoxCollider>(); // 시선 상호작용용
            trigger.center = new Vector3(0f, 1.3f, 0f); // 중심
            trigger.size = new Vector3(3f, 2.6f, 2f); // 크기
            return root; // 반환
        }

        public static GameObject BuildBreakerRoom(ModuleMaterials materials) // 배전반 방 — 구역 차단기 1개
        {
            DungeonModuleShape shape = new DungeonModuleShape("Room_Breaker_5x5", DungeonModuleCatalog.PowerFolder, ModuleRole.Breaker, 1,
                new[] { new ShapeRect(0, 0, 5, 5) },
                new[]
                {
                    new ShapeDoor(2, 0, GridDirection.South), // 출입구
                }); // 형태

            GameObject root = DungeonModuleBaker.Build(shape, materials); // 껍데기
            Transform structure = root.transform.Find("Structure"); // 구조물
            Transform panel = DungeonModuleBaker.CreateChild(structure, "BreakerPanel"); // 배전반
            panel.localPosition = new Vector3(2.5f, 0f, 4.4f); // 안쪽 벽 앞
            DungeonModuleBaker.CreateBox(panel, "Cabinet", new Vector3(0f, 1.4f, 0f), new Vector3(1.6f, 2.2f, 0.4f), materials.Wall); // 함
            DungeonModuleBaker.CreateBox(panel, "Frame", new Vector3(0f, 1.4f, -0.24f), new Vector3(1.4f, 2f, 0.08f), materials.Trim); // 전면판
            Transform leverPivot = DungeonModuleBaker.CreateChild(panel, "Lever"); // 손잡이 축
            leverPivot.localPosition = new Vector3(0f, 1.5f, -0.3f); // 전면
            DungeonModuleBaker.CreateBox(leverPivot, "Handle", new Vector3(0f, 0.28f, 0f), new Vector3(0.14f, 0.56f, 0.14f), materials.Accent); // 손잡이
            GameObject indicator = DungeonModuleBaker.CreateBox(panel, "Indicator", new Vector3(0.45f, 2.1f, -0.3f), new Vector3(0.22f, 0.22f, 0.1f), materials.Accent); // 표시등
            DungeonBreaker breaker = panel.gameObject.AddComponent<DungeonBreaker>(); // 차단기 기능
            breaker.Configure(leverPivot, indicator.GetComponent<Renderer>()); // 구성
            BoxCollider trigger = panel.gameObject.AddComponent<BoxCollider>(); // 시선 상호작용용
            trigger.center = new Vector3(0f, 1.4f, -0.1f); // 중심
            trigger.size = new Vector3(1.8f, 2.4f, 0.9f); // 크기
            return root; // 반환
        }
    }
}
