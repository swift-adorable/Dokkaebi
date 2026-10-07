using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기본 전리품 아이템과 표를 만드는 에디터 도구. (로드맵 6-F)
///
/// 무기·방어구는 6-C에서 따로 만든다. 여기 있는 것은
/// 「줍고 팔고 제작에 쓰는 것」과 소모품뿐이다.
///
/// 이 표가 없으면 전리품 창을 열어도 늘 비어 있어 파밍이 성립하지 않는다.
/// </summary>
public static class LootAssetGenerator
{
    private const string ItemRoot = "Assets/Data/ScriptableObjects/Items/Loot";
    private const string TableRoot = "Assets/Data/ScriptableObjects/Loot";

    private struct Row
    {
        public string id;
        public string name;
        public string desc;
        public ItemKind kind;
        public float weight;
        public int stackMax;
        public int value;
        public int weightInTable;
        public int minCount;
        public int maxCount;
    }

    /// <summary>
    /// 표. 가치와 무게의 비(価/kg)가 곧 「들고 갈 가치가 있는가」다.
    /// 쇠붙이는 무겁고 싸다 — 가방이 넉넉할 때만 담는다.
    /// 노리개는 가볍고 비싸다 — 보이면 무조건 담는다.
    /// 이 대비가 없으면 전리품 창은 「전부 줍기」 버튼 하나로 끝난다.
    /// </summary>
    private static List<Row> BuildTable()
    {
        var rows = new List<Row>
        {
            //     id              이름            설명
            New("scrap_metal",   "쇠붙이",         "어디에나 굴러다닌다. 무겁고 싸다.",
                ItemKind.Material, weight: 1.2f, stackMax: 20, value: 12, tableWeight: 30, 1, 4),

            New("wire_bundle",   "새끼 뭉치",     "제작에 쓰인다. 부피에 비해 쓸모가 있다.",
                ItemKind.Material, weight: 0.4f, stackMax: 20, value: 30, tableWeight: 22, 1, 3),

            New("cell_battery",  "숯",           "아직 불기가 남아 있다. 가마에서 쓰인다.",
                ItemKind.Material, weight: 0.6f, stackMax: 10, value: 70, tableWeight: 14, 1, 2),

            New("bio_sample",    "약재",     "말린 약재. 약방골에서 값이 오른다.",
                ItemKind.Material, weight: 0.3f, stackMax: 10, value: 110, tableWeight: 10, 1, 2),

            New("memory_core",   "노리개",   "가볍고 비싸다. 보이면 담는다.",
                ItemKind.Material, weight: 0.2f, stackMax: 5,  value: 420, tableWeight: 4,  1, 1),

            // 가벼운 것은 여러 개, 무거운 것은 한 개. 스택 수가 곧 무게의 반대다.
            New("med_bandage",   "쑥",     "출혈을 멈춘다. 잠시 출혈에 걸리지 않는다.",
                ItemKind.Consumable, weight: 0.05f, stackMax: 3, value: 45, tableWeight: 12, 1, 2),

            New("med_stim",      "현호색",       "잠시 아픔을 잊는다.",
                ItemKind.Consumable, weight: 0.1f, stackMax: 5, value: 60, tableWeight: 8, 1, 2),

            // 들판의 「맑은 물」(water_bottle)은 호리병 물(con_water)로 합쳤다 (결정 2-73) — 아래 ExistingRows.
        };

        // 요리 재료 (결정 2-73) — IngredientTable이 원본이다. 장이 맞지 않는 재료는 그 판에서 빠진다
        // (CorpseController → IngredientTable.DropsIn). 이미 있는 아이템(생강)은 ExistingRows가 맡는다.
        foreach (Ingredient i in IngredientTable.All)
        {
            if (i.Existing)
                continue;

            // 전리품 표에 올리지 않는 재료(산열매 — 나무에서 딴다)도 아이템 에셋은 만든다.
            if (i.DropWeight <= 0)
            {
                CreateOrUpdate(New(i.Id, i.Name, i.Description, ItemKind.Material,
                    i.Weight, i.StackMax, i.Value, 0, i.MinCount, i.MaxCount));
                continue;
            }

            rows.Add(New(i.Id, i.Name, i.Description, ItemKind.Material,
                i.Weight, i.StackMax, i.Value, i.DropWeight, i.MinCount, i.MaxCount));
        }

        return rows;
    }

    private const string ConsumableRoot = "Assets/Data/ScriptableObjects/Items/Consumables";

    /// <summary>
    /// 소모품 생성기가 만든 것을 표에만 올린다 — 호리병 물(들판에서도 나온다) · 생강(1장 재료이자 동결 해제약).
    /// </summary>
    private static IEnumerable<(string id, int weight, int min, int max)> ExistingRows()
    {
        yield return (SpringTable.WaterId, 10, 1, 1);

        foreach (Ingredient i in IngredientTable.All)
            if (i.Existing)
                yield return (i.Id, i.DropWeight, i.MinCount, i.MaxCount);
    }

    private static Row New(string id, string name, string desc, ItemKind kind,
                           float weight, int stackMax, int value,
                           int tableWeight, int minCount, int maxCount)
    {
        return new Row
        {
            id = id, name = name, desc = desc, kind = kind,
            weight = weight, stackMax = stackMax, value = value,
            weightInTable = tableWeight, minCount = minCount, maxCount = maxCount
        };
    }

    [MenuItem("Dokkaebi/Loot/전리품 에셋 생성")]
    public static void Generate()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/ScriptableObjects");
        EnsureFolder("Assets/Data/ScriptableObjects/Items");
        EnsureFolder(ItemRoot);
        EnsureFolder(TableRoot);

        List<Row> rows = BuildTable();
        var entries = new List<LootEntry>(rows.Count + 1);

        foreach (Row row in rows)
        {
            ItemDefinition item = CreateOrUpdate(row);

            entries.Add(new LootEntry(item, row.weightInTable, row.minCount, row.maxCount));
        }

        foreach ((string id, int weight, int min, int max) in ExistingRows())
        {
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ConsumableRoot}/{id}.asset");

            if (existing != null)
                entries.Add(new LootEntry(existing, weight, min, max));
            else
                Debug.LogWarning($"[LootAssetGenerator] {id} 에셋이 없습니다 — 「Dokkaebi/Items/음식 에셋 생성」을 먼저 돌리십시오.");
        }

        // 빈손 줄. 시체마다 뭔가 나오면 파밍이 지루해진다.
        entries.Add(new LootEntry(null, 40));

        CreateTable("LootTable_Common", entries, minRolls: 1, maxRolls: 3);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LootAssetGenerator] 아이템 {rows.Count}종 · 표 1종 생성 완료");
    }

    private static ItemDefinition CreateOrUpdate(Row row)
    {
        string path = $"{ItemRoot}/{row.id}.asset";

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        var so = new SerializedObject(item);

        so.FindProperty("id").stringValue = row.id;
        so.FindProperty("displayName").stringValue = row.name;
        so.FindProperty("description").stringValue = row.desc;
        so.FindProperty("kind").intValue = (int)row.kind;
        so.FindProperty("tier").intValue = 0;
        so.FindProperty("weight").floatValue = row.weight;
        so.FindProperty("slotSize").intValue = 1;
        so.FindProperty("stackMax").intValue = row.stackMax;
        so.FindProperty("maxDurability").intValue = 0;
        so.FindProperty("baseValue").intValue = row.value;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(item);

        return item;
    }

    private static void CreateTable(string name, List<LootEntry> entries, int minRolls, int maxRolls)
    {
        string path = $"{TableRoot}/{name}.asset";

        var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);

        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LootTable>();
            AssetDatabase.CreateAsset(table, path);
        }

        var so = new SerializedObject(table);
        so.FindProperty("minRolls").intValue = minRolls;
        so.FindProperty("maxRolls").intValue = maxRolls;
        so.ApplyModifiedPropertiesWithoutUndo();

        table.EditorSetEntries(entries);

        EditorUtility.SetDirty(table);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');

        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
