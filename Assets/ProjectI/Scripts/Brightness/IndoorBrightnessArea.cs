using System.Collections.Generic; // 활성 방 영역 목록 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    [RequireComponent(typeof(BoxCollider))] // 방 영역 판정을 위한 BoxCollider 필수 지정
    public sealed class IndoorBrightnessArea : MonoBehaviour // 내부 방 하나의 밝기 계산 영역
    {
        private static readonly HashSet<IndoorBrightnessArea> ActiveAreas = new HashSet<IndoorBrightnessArea>(); // 현재 활성화된 내부 방 영역 목록
        [SerializeField] private string areaName = "Indoor Room"; // 디버그 UI에 표시할 방 이름
        [SerializeField] private BoxCollider volume; // 플레이어가 내부인지 판정할 공간 Collider

        public string AreaName => areaName; // 현재 방 표시 이름 공개
        public BoxCollider Volume => volume; // 검증용 영역 Collider 공개

        private void Awake() // 방 영역 초기화
        {
            EnsureVolume(); // BoxCollider 참조와 Trigger 상태 확보
        }

        private void OnEnable() // 방 영역 활성화 처리
        {
            EnsureVolume(); // BoxCollider 참조와 Trigger 상태 확보
            ActiveAreas.Add(this); // 현재 방을 활성 영역 목록에 등록
        }

        private void OnDisable() // 방 영역 비활성화 처리
        {
            ActiveAreas.Remove(this); // 활성 영역 목록에서 현재 방 제거
        }

        public void Configure(string displayName, Vector3 size, Vector3 center) // 에디터 자동 설정용 방 영역 값 지정
        {
            EnsureVolume(); // BoxCollider 참조 확보
            areaName = displayName; // 디버그용 방 이름 저장
            volume.size = size; // 방 내부 판정 크기 지정
            volume.center = center; // 방 내부 판정 중심 지정
            volume.isTrigger = true; // 물리 충돌 없이 영역 판정용 Trigger로 지정
        }

        public bool Contains(Vector3 worldPosition) // 월드 위치가 현재 방 영역 내부인지 판정
        {
            EnsureVolume(); // BoxCollider 참조 확보
            Vector3 localPosition = volume.transform.InverseTransformPoint(worldPosition) - volume.center; // 월드 위치를 Collider 중심 기준 로컬 좌표로 변환
            Vector3 halfSize = volume.size * 0.5f; // 방 영역 반크기 계산
            bool insideX = Mathf.Abs(localPosition.x) <= halfSize.x; // X축 내부 여부 판정
            bool insideY = Mathf.Abs(localPosition.y) <= halfSize.y; // Y축 내부 여부 판정
            bool insideZ = Mathf.Abs(localPosition.z) <= halfSize.z; // Z축 내부 여부 판정
            return insideX && insideY && insideZ; // 세 축 모두 내부일 때 현재 방으로 판정
        }

        public static IndoorBrightnessArea FindContaining(Vector3 worldPosition) // 지정 위치를 포함하는 현재 방 영역 검색
        {
            foreach (IndoorBrightnessArea area in ActiveAreas) // 활성 방 영역 순회
            {
                if (area == null || !area.isActiveAndEnabled) // 유효하지 않거나 비활성화된 방 확인
                {
                    continue; // 다음 방 영역 검사
                }

                if (area.Contains(worldPosition)) // 현재 위치가 해당 방 내부인지 확인
                {
                    return area; // 처음 포함된 방 영역 반환
                }
            }

            return null; // 포함되는 내부 방이 없으면 외부로 판정
        }

        private void EnsureVolume() // BoxCollider 참조와 영역 설정 확보
        {
            if (volume == null) // Collider 참조 누락 확인
            {
                volume = GetComponent<BoxCollider>(); // 같은 오브젝트의 BoxCollider 조회
            }

            if (volume != null) // Collider 존재 여부 확인
            {
                volume.isTrigger = true; // 방 영역은 항상 Trigger로 유지
            }
        }
    }
}
