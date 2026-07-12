using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectI.EditorTools
{
    /// <summary>
    /// 보물.csv → TreasureData(ScriptableObject) 자동 임포트. (기획서 PART 13.2.2)
    /// 메뉴: ProjectI → Import → Treasures from CSV
    /// 정본: Assets/Data/보물.csv, 출력: Assets/ScriptableObjects/Treasures/
    /// </summary>
    public static class TreasureDataImporter
    {
        const string CsvRelative = "Data/보물.csv";
        const string OutFolder = "Assets/ScriptableObjects/Treasures";

        [MenuItem("ProjectI/Import/Treasures from CSV")]
        public static void Import()
        {
            string full = Path.Combine(Application.dataPath, CsvRelative);
            if (!File.Exists(full)) { Debug.LogError($"[TreasureImporter] CSV 없음: {full}"); return; }

            EnsureFolder(OutFolder);
            string[] lines = File.ReadAllLines(full, Encoding.UTF8);
            if (lines.Length < 2) { Debug.LogError("[TreasureImporter] 데이터 행 없음"); return; }

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

                string path = $"{OutFolder}/Treasure_{name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<TreasureData>(path);
                bool isNew = data == null;
                if (isNew) data = ScriptableObject.CreateInstance<TreasureData>();

                data.displayName = name;
                data.minValue = ToInt(Get(c, col, "최소가"));
                data.maxValue = ToInt(Get(c, col, "최대가"));
                data.weightKg = ToFloat(Get(c, col, "무게(kg)"));
                data.inventorySlots = Mathf.Max(1, ToInt(Get(c, col, "인벤토리칸")));
                data.twoHanded = Get(c, col, "두손운반").Trim() == "예";
                data.noise = Mathf.Clamp(ToInt(Get(c, col, "소음")), 0, 3);

                if (isNew) { AssetDatabase.CreateAsset(data, path); created++; }
                else { EditorUtility.SetDirty(data); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TreasureImporter] 완료 — 생성 {created}, 갱신 {updated} (총 {created + updated})");
        }

        static string Get(string[] cells, Dictionary<string, int> col, string key)
            => (col.TryGetValue(key, out int i) && i < cells.Length) ? cells[i] : "";
        static int ToInt(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        static float ToFloat(string s) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

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
