using System.Text; // 문자열 조립 기능 참조
using ProjectI.Diagnostics; // F1 디버그 페이지 기반 참조
using ProjectI.Generation; // 생성 그래프 참조
using ProjectI.Items; // 플레이어 위치 조회용 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [RequireComponent(typeof(ProceduralInteriorGenerator))] // 생성기와 함께 사용
    public sealed class ProceduralInteriorDebugPage : DebugPageProvider // F1 Dungeon 페이지 (시드·방·서브문 연결·지하 깊이)
    {
        private ProceduralInteriorGenerator generator; // 생성기

        public override string PageName => "Dungeon"; // 페이지 이름
        public override int SortOrder => 120; // Trap 뒤

        public override string BuildDebugText() // 페이지 내용
        {
            generator = generator != null ? generator : GetComponent<ProceduralInteriorGenerator>(); // 생성기 조회
            StringBuilder builder = new StringBuilder(2400); // 문자열 버퍼
            builder.AppendLine("PROCEDURAL INTERIOR"); // 제목
            builder.AppendLine("────────────────────────────"); // 구분선

            if (generator == null || !generator.IsGenerated || generator.Layout == null) // 생성 확인
            {
                builder.AppendLine($"생성 안 됨 {generator?.FailureReason}"); // 실패
                return builder.ToString(); // 반환
            }

            DungeonLayout layout = generator.Layout; // 그래프
            int corridors = layout.Rooms.FindAll(room => room.Kind == RoomKind.Corridor).Count; // 복도 수
            int loops = layout.Doors.FindAll(door => door.IsLoop).Count; // 순환로 수
            builder.AppendLine($"Seed       : {generator.Seed} (시도 {layout.Attempt + 1}){(generator.SeedOverride != 0 ? " [고정 시드]" : string.Empty)}"); // 시드

            if (generator.ForcedVerticalKind != VerticalKind.None) // 테스트 모드 표시
            {
                builder.AppendLine($"Test Mode  : 세로형 방을 {VerticalLabel(generator.ForcedVerticalKind)}로 고정"); // 고정 종류
            }

            builder.AppendLine($"Signature  : {layout.Signature()}"); // 서명
            builder.AppendLine($"Rooms      : {layout.Rooms.Count} (복도 {corridors}) / 문 {layout.Doors.Count} (순환 {loops})"); // 방
            builder.AppendLine($"Floors     : {GridPoint.FloorName(layout.MinFloor)} ~ {GridPoint.FloorName(layout.MaxFloor)} ({layout.FloorCount}개) / 층 높이 {generator.FloorStep:F1}m"); // 층
            builder.AppendLine($"Max Depth  : {layout.MaxDepth} / 최심부 방 {layout.DeepestRoomId}"); // 깊이
            builder.AppendLine($"Locked     : {(layout.LockedDoorIndex < 0 ? "없음" : $"문 {layout.LockedDoorIndex} / 열쇠 방 {layout.KeyRoomId} {layout.Room(layout.KeyRoomId).Cell}")}"); // 잠긴 문
            builder.AppendLine($"Loot       : {generator.SpawnedLoot.Count}개"); // 회수품
            builder.AppendLine($"Depth      : 생성물 최고 {generator.GeneratedMaxY:F1} / 한계 {generator.InteriorTopY:F1} / 외부 최저 {generator.ExteriorBottomY:F1}"); // 지하 깊이
            builder.AppendLine(); // 여백
            builder.AppendLine($"정문        : 외부 정문 ↔ 시작 방 {layout.Room(layout.StartRoomId).Cell} {layout.MainDoorWall}벽"); // 정문

            for (int floor = layout.MaxFloor; floor >= layout.MinFloor; floor--) // 층별 방 수
            {
                int normal = 0; // 일반 방
                int vertical = 0; // 세로형 방

                foreach (RoomNode room in layout.Rooms) // 방 순회
                {
                    normal += !room.IsVertical && room.Cell.Floor == floor ? 1 : 0; // 집계
                    vertical += room.IsVertical && room.OccupiesFloor(floor) ? 1 : 0; // 집계
                }

                builder.AppendLine($"{GridPoint.FloorName(floor),-4}       : 방 {normal} / 세로형 {vertical} / 바닥 높이 {generator.FloorWorldY(floor):F1}"); // 층 정보
            }

            RoomNode startRoom = layout.Room(layout.StartRoomId); // 시작 방

            foreach (RoomNode room in layout.VerticalRooms) // 세로형 방 연결
            {
                Vector3 offset = generator.RoomCenterWorld(room) - generator.RoomCenterWorld(startRoom); // 시작 방 기준 위치 차이
                builder.AppendLine($"세로형 {room.Id,-4} : {VerticalLabel(room.Vertical)} {room.Cell} {GridPoint.FloorName(room.LowerFloor)}({room.LowerWall}) ↔ {GridPoint.FloorName(room.UpperFloor)}({room.UpperWall}) / 문 {room.Depth}개 · 시작 방에서 X {offset.x:+0.0;-0.0;0} Z {offset.z:+0.0;-0.0;0}"); // 연결·위치
            }

            builder.AppendLine(); // 여백

            foreach (SubDoorPlacement sub in layout.SubDoors) // 서브문
            {
                builder.AppendLine($"서브문 {sub.Index + 1}    : 외부 서브문 {sub.Index + 1} ↔ 방 {sub.RoomId} {layout.Room(sub.RoomId).Cell} {sub.Wall}벽 (깊이 {layout.Room(sub.RoomId).Depth})"); // 연결
            }

            PlayerInventory player = Object.FindFirstObjectByType<PlayerInventory>(); // 플레이어
            RoomNode here = player == null ? null : generator.FindRoomAt(player.transform.position); // 현재 방
            builder.AppendLine(); // 여백
            ProjectI.Player.PlayerMovement movement = player == null ? null : player.GetComponent<ProjectI.Player.PlayerMovement>(); // 이동 컴포넌트
            builder.AppendLine($"Player     : {(here == null ? "실내 밖" : $"방 {here.Id} {here.Cell} 깊이 {here.Depth} {here.Kind} {here.Content}")}"); // 위치
            builder.AppendLine($"Climb      : {(movement == null ? "없음" : movement.IsClimbing ? $"사다리 이용 중 {movement.ClimbProgress * 100f:F0}%" : "지상")}"); // 사다리 상태
            return builder.ToString(); // 반환
        }

        private static string VerticalLabel(VerticalKind kind) // 세로형 방 이름
        {
            switch (kind) // 종류별
            {
                case VerticalKind.Stairwell: return "계단통"; // 계단
                case VerticalKind.Ladder: return "사다리 방"; // 사다리
                case VerticalKind.Shaft: return "수직 통로"; // 통로
                default: return "없음"; // 기타
            }
        }
    }
}
