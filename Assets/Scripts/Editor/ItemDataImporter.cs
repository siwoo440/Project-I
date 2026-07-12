using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectI.EditorTools
{
    /// <summary>
    /// 아이템.csv → ItemData(ScriptableObject) 자동 임포트. (기획서 PART 13.2.2)
    /// 메뉴: ProjectI → Import → Items from CSV
    /// CSV 정본: Assets/Data/아이템.csv (프로젝트 내). 출력: Assets/ScriptableObjects/Items/
    /// 헤더 이름으로 컬럼을 찾으므로 컬럼 순서가 바뀌어도 동작.
    /// </summary>
    public static class ItemDataImporter
    {
        const string CsvRelative = "Data/아이템.csv";              // Application.dataPath 기준
        const string OutFolder = "Assets/ScriptableObjects/Items"; // 생성 폴더

        [MenuItem("ProjectI/Import/Items from CSV")]
        public static void Import()
        {
            string full = Path.Combine(Application.dataPath, CsvRelative);
            if (!File.Exists(full))
            {
                Debug.LogError($"[ItemImporter] CSV를 찾을 수 없음: {full}");
                return;
            }

            EnsureFolder(OutFolder);

            string[] lines = File.ReadAllLines(full, Encoding.UTF8);
            if (lines.Length < 2) { Debug.LogError("[ItemImporter] 데이터 행이 없습니다."); return; }

            // 헤더 → 컬럼 인덱스 맵
            string[] header = lines[0].Split(',');
            var col = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++) col[header[i].Trim()] = i;

            int created = 0, updated = 0;
            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                string[] c = lines[r].Split(',');

                string name = Get(c, col, "이름").Trim();
                if (string.IsNullOrEmpty(name)) continue;

                string path = $"{OutFolder}/Item_{name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                bool isNew = data == null;
                if (isNew) data = ScriptableObject.CreateInstance<ItemData>();

                data.displayName    = name;
                data.category       = Get(c, col, "분류");
                data.description    = Get(c, col, "설명");
                data.buyPrice       = ToInt(Get(c, col, "구매가"));
                data.sellPrice      = ToInt(Get(c, col, "판매가"));
                data.inventorySlots = Mathf.Max(1, ToInt(Get(c, col, "인벤토리칸")));
                data.bonusSlots     = ToInt(Get(c, col, "보너스칸"));
                data.weightKg       = ToFloat(Get(c, col, "무게(kg)"));
                data.noise          = Mathf.Clamp(ToInt(Get(c, col, "소음")), 0, 3);
                data.usesBattery    = Get(c, col, "배터리").Trim().ToUpperInvariant() == "Y";
                data.interaction    = Get(c, col, "상호작용");

                if (isNew) { AssetDatabase.CreateAsset(data, path); created++; }
                else { EditorUtility.SetDirty(data); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ItemImporter] 완료 — 생성 {created}, 갱신 {updated} (총 {created + updated})");
        }

        static string Get(string[] cells, Dictionary<string, int> col, string key)
            => (col.TryGetValue(key, out int i) && i < cells.Length) ? cells[i] : "";

        static int ToInt(string s)
            => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        static float ToFloat(string s)
            => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
