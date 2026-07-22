using UnityEngine; // Unity GameObject와 컴포넌트 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class Room : MonoBehaviour // 던전의 수평 및 수직 격자 방 관리
    {
        public enum Dir // 방 연결 방향 종류
        {
            N = 0, // 북쪽 +Z 연결
            E = 1, // 동쪽 +X 연결
            S = 2, // 남쪽 -Z 연결
            W = 3, // 서쪽 -X 연결
            Up = 4, // 위층 +Y 연결
            Down = 5 // 아래층 -Y 연결
        }

        [Header("수평 벽")] // 수평 연결을 막는 벽 오브젝트 구분
        [Tooltip("북쪽 벽")] [SerializeField] GameObject wallN; // 북쪽 벽
        [Tooltip("동쪽 벽")] [SerializeField] GameObject wallE; // 동쪽 벽
        [Tooltip("남쪽 벽")] [SerializeField] GameObject wallS; // 남쪽 벽
        [Tooltip("서쪽 벽")] [SerializeField] GameObject wallW; // 서쪽 벽

        [Header("수직 연결 차단물")] // 층간 연결을 막는 오브젝트 구분
        [Tooltip("위층 연결부를 막는 천장 또는 해치")] [SerializeField] GameObject ceilingBlock; // 위층 연결부를 막는 천장 또는 해치
        [Tooltip("아래층 연결부를 막는 바닥 또는 해치")] [SerializeField] GameObject floorBlock; // 아래층 연결부를 막는 바닥 또는 해치

        [Header("수직 연결 외형")] // 연결될 때 표시할 계단과 난간 구분
        [Tooltip("위층으로 올라가는 계단 외형")] [SerializeField] GameObject stairsUpVisual; // 위층으로 올라가는 계단 외형
        [Tooltip("아래층으로 내려가는 입구 외형")] [SerializeField] GameObject stairsDownVisual; // 아래층으로 내려가는 입구 외형

        [Header("자동 스폰")] // 방 내부 자동 생성 허용 설정
        [Tooltip("몬스터와 보물 및 함정 생성 허용 여부")] [SerializeField] bool allowAutomaticSpawning = true; // 몬스터와 보물 및 함정 생성 허용 여부

        public bool AllowAutomaticSpawning => allowAutomaticSpawning; // 외부 스폰 매니저에 생성 허용 상태 반환

        public void ResetConnections() // 재생성 전 모든 연결 차단물 초기화
        {
            SetBlockerActive(wallN, true); // 북쪽 벽 복구
            SetBlockerActive(wallE, true); // 동쪽 벽 복구
            SetBlockerActive(wallS, true); // 남쪽 벽 복구
            SetBlockerActive(wallW, true); // 서쪽 벽 복구
            SetBlockerActive(ceilingBlock, true); // 천장 차단물 복구
            SetBlockerActive(floorBlock, true); // 바닥 차단물 복구
            SetBlockerActive(stairsUpVisual, false); // 위층 계단 외형 숨김
            SetBlockerActive(stairsDownVisual, false); // 아래층 계단 외형 숨김
        }

        public bool TryGetClosedWall(Dir direction, out Transform wallTransform) // 닫힌 수평 벽 Transform 검색
        {
            wallTransform = null; // 실패 대비 결과 초기화

            if (direction == Dir.Up || direction == Dir.Down) // 수직 방향 여부 확인
            {
                return false; // 수직 차단물 제외
            }

            GameObject wallObject = GetBlocker(direction); // 방향에 연결된 벽 오브젝트 검색

            if (wallObject == null || !wallObject.activeInHierarchy) // 벽 존재와 활성 상태 확인
            {
                return false; // 열렸거나 누락된 벽 제외
            }

            wallTransform = wallObject.transform; // 안전한 닫힌 벽 Transform 저장
            return true; // 닫힌 벽 검색 성공
        }

        public void OpenSide(Dir direction) // 지정된 수평 벽 또는 수직 해치를 열고 연결 외형 활성화
        {
            GameObject blocker = GetBlocker(direction); // 현재 방향을 막는 오브젝트 검색

            if (blocker != null) // 차단 오브젝트가 연결되어 있는지 확인
            {
                blocker.SetActive(false); // 연결 방향의 벽 또는 해치 비활성화
            }

            GameObject verticalVisual = GetVerticalVisual(direction); // 현재 방향의 계단 외형 검색

            if (verticalVisual != null) // 계단 또는 난간 외형이 연결되어 있는지 확인
            {
                verticalVisual.SetActive(true); // 연결된 수직 이동 외형 활성화
            }
        }

        void SetBlockerActive(GameObject target, bool active) // 선택 오브젝트 활성 상태 설정
        {
            if (target != null) // 대상 존재 여부 확인
            {
                target.SetActive(active); // 지정 활성 상태 적용
            }
        }

        GameObject GetBlocker(Dir direction) // 방향에 맞는 수평 벽 또는 수직 해치 반환
        {
            switch (direction) // 전달된 연결 방향 확인
            {
                case Dir.N: // 북쪽 방향인지 확인
                    return wallN; // 북쪽 벽 반환

                case Dir.E: // 동쪽 방향인지 확인
                    return wallE; // 동쪽 벽 반환

                case Dir.S: // 남쪽 방향인지 확인
                    return wallS; // 남쪽 벽 반환

                case Dir.W: // 서쪽 방향인지 확인
                    return wallW; // 서쪽 벽 반환

                case Dir.Up: // 위층 방향인지 확인
                    return ceilingBlock; // 천장 또는 위층 해치 반환

                case Dir.Down: // 아래층 방향인지 확인
                    return floorBlock; // 바닥 또는 아래층 해치 반환

                default: // 정의되지 않은 방향 처리
                    return null; // 차단 오브젝트 없음 반환
            }
        }

        GameObject GetVerticalVisual(Dir direction) // 방향에 맞는 계단 또는 난간 외형 반환
        {
            switch (direction) // 전달된 수직 방향 확인
            {
                case Dir.Up: // 위층 연결인지 확인
                    return stairsUpVisual; // 올라가는 계단 외형 반환

                case Dir.Down: // 아래층 연결인지 확인
                    return stairsDownVisual; // 내려가는 입구 외형 반환

                default: // 수평 연결 방향 처리
                    return null; // 수직 외형 없음 반환
            }
        }

        public static Dir Opposite(Dir direction) // 전달된 연결 방향의 반대 방향 반환
        {
            switch (direction) // 현재 방향 확인
            {
                case Dir.N: // 북쪽 방향인지 확인
                    return Dir.S; // 남쪽 반환

                case Dir.E: // 동쪽 방향인지 확인
                    return Dir.W; // 서쪽 반환

                case Dir.S: // 남쪽 방향인지 확인
                    return Dir.N; // 북쪽 반환

                case Dir.W: // 서쪽 방향인지 확인
                    return Dir.E; // 동쪽 반환

                case Dir.Up: // 위층 방향인지 확인
                    return Dir.Down; // 아래층 반환

                default: // 아래층 또는 예외 방향 처리
                    return Dir.Up; // 위층 반환
            }
        }
    }
}