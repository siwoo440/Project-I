using System.Collections.Generic;
using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 마차 — 던전 입구의 안전지대 겸 보물 보관소. (기획서 PART 3.2)
    /// 보물/아이템을 실으면 확보(안전), '떠나기'로 던전 종료(익스트랙션).
    /// ※ 마을 정산 연결은 Phase 3에서. 지금은 종료 결과만 표시.
    /// </summary>
    public class Wagon : MonoBehaviour
    {
        readonly List<ICarryable> secured = new List<ICarryable>();
        Transform cargoRoot;
        bool left;
        public int SecuredCount => secured.Count;
        public int SecuredValue
        {
            get { int v = 0; foreach (var c in secured) if (c is Treasure t) v += t.Value; return v; }
        }
        public bool HasLeft => left;

        void Awake()
        {
            Time.timeScale = 1f; // 이전 플레이에서 0으로 남았을 수 있으니 초기화
            var c = new GameObject("Cargo").transform;
            c.SetParent(transform);
            c.localPosition = new Vector3(0f, 1f, 0f);
            cargoRoot = c;
        }

        /// <summary>아이템/보물을 마차에 실어 확보(보관).</summary>
        public void Deposit(ICarryable c)
        {
            if (c == null) return;
            c.EnterInventory(cargoRoot); // 마차에 숨겨 보관(모델 숨김·물리 off)
            secured.Add(c);
            Debug.Log($"[Wagon] 적재: {c.DisplayName}  (총 {SecuredCount}개, {SecuredValue}골드)");
        }

        /// <summary>떠나기 — 던전 종료. 살아있는 플레이어가 들고 있던 것도 함께 확보.</summary>
        public void Leave(InventorySystem playerInventory = null)
        {
            if (left) return;

            // 살아서 탈출 → 인벤토리에 들고 있던 것도 마차로 확보
            if (playerInventory != null)
                foreach (var c in playerInventory.TakeAll()) Deposit(c);

            left = true;
            Debug.Log($"[Wagon] 떠나기 — 확보 보물 {SecuredCount}개, 총 {SecuredValue}골드");
            Time.timeScale = 0f; // 임시: 던전 종료. (마을 정산 연결은 이후)
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 150, 640, 20), $"마차 적재: {SecuredCount}개  |  총 가치: {SecuredValue}골드");

            if (left)
            {
                var title = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                var sub = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 40), "던전 종료 — 탈출 성공!", title);
                GUI.Label(new Rect(0, Screen.height / 2f + 4, Screen.width, 30), $"확보 보물 {SecuredCount}개 · 총 {SecuredValue}골드", sub);
                GUI.Label(new Rect(0, Screen.height / 2f + 34, Screen.width, 30), "(플레이 정지 후 다시 시작하세요 — 마을 정산은 이후 단계)", sub);
            }
        }
    }
}
