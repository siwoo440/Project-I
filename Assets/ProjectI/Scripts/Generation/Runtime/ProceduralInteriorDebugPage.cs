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
            builder.AppendLine($"Seed       : {generator.Seed} (시도 {layout.Attempt + 1})"); // 시드
            builder.AppendLine($"Signature  : {layout.Signature()}"); // 서명
            builder.AppendLine($"Rooms      : {layout.Rooms.Count} (복도 {corridors}) / 문 {layout.Doors.Count} (순환 {loops})"); // 방
            builder.AppendLine($"Max Depth  : {layout.MaxDepth} / 최심부 방 {layout.DeepestRoomId}"); // 깊이
            builder.AppendLine($"Locked     : {(layout.LockedDoorIndex < 0 ? "없음" : $"문 {layout.LockedDoorIndex} / 열쇠 방 {layout.KeyRoomId} {layout.Room(layout.KeyRoomId).Cell}")}"); // 잠긴 문
            builder.AppendLine($"Loot       : {generator.SpawnedLoot.Count}개"); // 회수품
            builder.AppendLine($"Depth      : 생성물 최고 {generator.GeneratedMaxY:F1} / 한계 {generator.InteriorTopY:F1} / 외부 최저 {generator.ExteriorBottomY:F1}"); // 지하 깊이
            builder.AppendLine(); // 여백
            builder.AppendLine($"정문        : 외부 정문 ↔ 시작 방 {layout.Room(layout.StartRoomId).Cell} {layout.MainDoorWall}벽"); // 정문

            foreach (SubDoorPlacement sub in layout.SubDoors) // 서브문
            {
                builder.AppendLine($"서브문 {sub.Index + 1}    : 외부 서브문 {sub.Index + 1} ↔ 방 {sub.RoomId} {layout.Room(sub.RoomId).Cell} {sub.Wall}벽 (깊이 {layout.Room(sub.RoomId).Depth})"); // 연결
            }

            PlayerInventory player = Object.FindFirstObjectByType<PlayerInventory>(); // 플레이어
            RoomNode here = player == null ? null : generator.FindRoomAt(player.transform.position); // 현재 방
            builder.AppendLine(); // 여백
            builder.AppendLine($"Player     : {(here == null ? "실내 밖" : $"방 {here.Id} {here.Cell} 깊이 {here.Depth} {here.Kind} {here.Content}")}"); // 위치
            return builder.ToString(); // 반환
        }
    }
}
