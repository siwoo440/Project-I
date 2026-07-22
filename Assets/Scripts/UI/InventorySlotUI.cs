using TMPro; // TextMeshPro 글자 기능 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.UI; // Image와 Outline UI 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class InventorySlotUI : MonoBehaviour // Canvas 인벤토리의 개별 슬롯 표시 컴포넌트
    {
        [Header("UI 참조")] // 슬롯 UI 참조 구분
        [Tooltip("슬롯 배경 이미지")] [SerializeField] Image backgroundImage; // 슬롯 배경 이미지
        [Tooltip("현재 선택 슬롯 외곽선")] [SerializeField] Outline selectionOutline; // 현재 선택 슬롯 외곽선
        [Tooltip("실제 슬롯 번호 글자")] [SerializeField] TMP_Text slotNumberText; // 실제 슬롯 번호 글자
        [Tooltip("아이템 이름 또는 연결 슬롯 표시 글자")] [SerializeField] TMP_Text itemNameText; // 아이템 이름 또는 연결 슬롯 표시 글자

        [Header("색상")] // 슬롯 색상 설정 구분
        [Tooltip("빈 슬롯 배경 색상")] [SerializeField] Color emptyColor = new Color(0.09f, 0.09f, 0.1f, 0.82f); // 빈 슬롯 배경 색상
        [Tooltip("아이템이 들어 있는 슬롯 색상")] [SerializeField] Color occupiedColor = new Color(0.19f, 0.17f, 0.14f, 0.94f); // 아이템이 들어 있는 슬롯 색상
        [Tooltip("현재 선택 슬롯 배경 색상")] [SerializeField] Color selectedColor = new Color(0.32f, 0.26f, 0.12f, 1f); // 현재 선택 슬롯 배경 색상

        [Header("글자 크기")] // 슬롯 글자 크기 설정 구분
        [Tooltip("일반 아이템 이름 글자 크기")] [SerializeField] float itemNameFontSize = 16f; // 일반 아이템 이름 글자 크기
        [Tooltip("다중 슬롯 연결 기호 글자 크기")] [SerializeField] float continuationFontSize = 26f; // 다중 슬롯 연결 기호 글자 크기

        public void SetSlot(int slotNumber, string itemLabel, bool occupied, bool selected, bool continuation) // 전달받은 상태로 슬롯 화면 갱신
        {
            if (backgroundImage != null) // 슬롯 배경 이미지 존재 여부 확인
            {
                backgroundImage.color = selected ? selectedColor : occupied ? occupiedColor : emptyColor; // 선택과 점유 상태에 맞는 배경색 적용
            }

            if (selectionOutline != null) // 선택 외곽선 존재 여부 확인
            {
                selectionOutline.enabled = selected; // 현재 선택 슬롯에만 외곽선 표시
            }

            if (slotNumberText != null) // 슬롯 번호 글자 존재 여부 확인
            {
                slotNumberText.text = slotNumber.ToString(); // 1부터 시작하는 슬롯 번호 표시
            }

            if (itemNameText != null) // 아이템 이름 글자 존재 여부 확인
            {
                itemNameText.text = occupied ? itemLabel : string.Empty; // 점유 슬롯에만 아이템 문구 표시
                itemNameText.fontSize = continuation ? continuationFontSize : itemNameFontSize; // 연결 슬롯과 일반 슬롯의 글자 크기 분리
            }
        }
    }
}