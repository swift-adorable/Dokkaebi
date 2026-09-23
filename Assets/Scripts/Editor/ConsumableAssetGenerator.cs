using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 소모품 에셋 생성. (로드맵 8-A · docs/Blob_Consumable_System.md)
///
/// 【이번에 만드는 것은 셋뿐이다 — 회복 · 해제 · 음료와 음식.】
/// 강화와 방호는 「이동 +25%, 120초」처럼 이로운 상태를 걸어야 하는데
/// StatusEffectType에 그것을 담을 자리가 아직 없다. 빈 껍데기 에셋을
/// 미리 만들어 두면 가방에 들어가고 무게를 먹으면서 아무 일도 하지
/// 않는 물건이 된다 — 그쪽이 없는 것보다 나쁘다.
///
/// 수치 근거 —
///   · 체력은 100 고정이므로 회복량을 그 자릿수에 맞춘다. (마스터 프롬프트)
///   · 무게와 스택은 문서 2절 표 그대로. **가벼운 것은 여럿, 무거운 것은 하나.**
///   · 아스피린의 「수분 −15」는 덕코프 실제 값이다. [확인됨 — research 07]
///   · 에너지 바의 「배고픔은 해결되나 갈증을 유발한다」도 같은 출처다.
/// </summary>
public static class ConsumableAssetGenerator
{
    private const string Root = "Assets/Data/ScriptableObjects/Items/Consumables";

    private struct Row
    {
        public string id;
        public string name;
        public string desc;
        public ConsumableCategory category;
        public int heal;
        public float water;
        public float energy;
        public float waterCost;
        public float energyCost;
        public StatusEffectType cure;
        public float weight;
        public int stackMax;
        public int value;
    }

    /// <summary>
    /// 표.
    ///
    /// 【회복은 무게가 곧 회복량이다.】 아스피린은 9개를 한 칸에 겹칠 수
    /// 있고 대형 구급상자는 한 칸을 통째로 먹는다. 그래야 「가볍게 여러 번
    /// 조금씩」과 「한 번에 크게」가 서로 다른 선택이 된다.
    ///
    /// 【해제는 값이 같다.】 어떤 상태가 더 위험한지는 상황이 정하지
    /// 도구가 정하지 않는다. 해독제가 해빙제보다 비싸면 플레이어는
    /// 「중독이 더 무서운 것」이라고 잘못 배운다.
    /// </summary>
    private static List<Row> Table()
    {
        var list = new List<Row>(16);

        // ── 회복 ──────────────────────────────────────────────────────

        list.Add(Heal("con_aspirin", "아스피린",
            "머리가 덜 아프다. 대신 목이 마른다.", 10, 0.03f, 9, 40, waterCost: 15f));

        list.Add(Heal("con_medkit_small", "소형 구급상자",
            "한 번의 실수를 덮을 만큼.", 30, 0.5f, 1, 180));

        list.Add(Heal("con_medkit", "구급상자",
            "제대로 된 것. 자리를 차지하는 만큼 값을 한다.", 60, 0.75f, 1, 420));

        list.Add(Heal("con_medkit_large", "대형 구급상자",
            "이걸 들고 나왔다면 무언가를 두고 온 것이다.", 100, 1.2f, 1, 900));

        // ── 해제 — 상태 하나에 도구 하나 ──────────────────────────────

        // 지혈 붕대만 회복을 겸한다. 출혈이 가장 흔하기 때문이다. (문서 3절)
        list.Add(Cure("con_bandage", "지혈 붕대", "감고 나면 피는 멎는다.",
            StatusEffectType.Bleed, heal: 8, weight: 0.05f, stackMax: 3, value: 60));

        list.Add(Cure("con_antidote", "해독제", "속을 게워 내는 맛이 난다.",
            StatusEffectType.Poison, 0, 0.1f, 3, 90));

        list.Add(Cure("con_antacid", "소화제", "안에서 타는 것을 끈다.",
            StatusEffectType.Ignite, 0, 0.1f, 3, 90));

        list.Add(Cure("con_relaxant", "이완제", "경련이 멎는다.",
            StatusEffectType.Shock, 0, 0.1f, 3, 90));

        list.Add(Cure("con_defroster", "해빙제", "안쪽부터 녹인다.",
            StatusEffectType.Freeze, 0, 0.1f, 3, 90));

        // ── 음료 · 음식 ───────────────────────────────────────────────
        //
        // 【체력을 채우지 않는다.】 먹어서 상처가 낫기 시작하면
        // 수분·에너지 두 축이 체력에 흡수된다. (결정 2-32)

        list.Add(Food("con_water", "정수 물통", "맛은 없지만 안전하다.",
            water: 40f, energy: 0f, weight: 0.5f, stackMax: 3, value: 30));

        list.Add(Food("con_soda", "미지근한 탄산", "김이 빠졌다. 그래도 물이다.",
            25f, 5f, 0.4f, 3, 45));

        list.Add(Food("con_canned", "통조림", "국물까지 마시면 물도 조금 는다.",
            10f, 30f, 0.5f, 2, 55));

        list.Add(Food("con_ration", "압축 식량", "씹는 데 시간이 걸린다.",
            0f, 40f, 0.3f, 3, 60));

        // 배고픔은 해결되나 갈증을 유발한다. [확인됨 — 덕코프]
        list.Add(Food("con_energy_bar", "에너지 바", "삼키고 나면 물을 찾게 된다.",
            0f, 25f, 0.15f, 5, 40, waterCost: 10f));

        list.Add(Food("con_whisky", "위스키", "몸이 데워지는 대신 물이 마른다.",
            0f, 10f, 0.6f, 2, 70, waterCost: 15f));

        return list;
    }

    [MenuItem("Blob/Items/소모품 에셋 생성")]
    public static void Generate()
    {
        EnsureFolder(Root);

        List<Row> rows = Table();

        foreach (Row row in rows)
            CreateOrUpdate(row);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ConsumableAssetGenerator] 소모품 {rows.Count}종 생성 완료 — "
                  + "「Blob/Playtest/검증 카탈로그 생성」도 다시 실행하십시오.");
    }

    private static Row Heal(string id, string name, string desc, int heal,
                            float weight, int stackMax, int value, float waterCost = 0f)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Restore,
            heal = heal, waterCost = waterCost,
            cure = StatusEffectType.None,
            weight = weight, stackMax = stackMax, value = value
        };
    }

    private static Row Cure(string id, string name, string desc, StatusEffectType cure,
                            int heal, float weight, int stackMax, int value)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Cure,
            heal = heal, cure = cure,
            weight = weight, stackMax = stackMax, value = value
        };
    }

    private static Row Food(string id, string name, string desc,
                            float water, float energy,
                            float weight, int stackMax, int value,
                            float waterCost = 0f)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Sustenance,
            water = water, energy = energy, waterCost = waterCost,
            cure = StatusEffectType.None,
            weight = weight, stackMax = stackMax, value = value
        };
    }

    private static void CreateOrUpdate(Row row)
    {
        string path = $"{Root}/{row.id}.asset";

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
        so.FindProperty("kind").intValue = (int)ItemKind.Consumable;
        so.FindProperty("tier").intValue = 0;
        so.FindProperty("weight").floatValue = row.weight;
        so.FindProperty("slotSize").intValue = 1;
        so.FindProperty("stackMax").intValue = row.stackMax;
        so.FindProperty("maxDurability").intValue = 0;
        so.FindProperty("baseValue").intValue = row.value;

        SerializedProperty effect = so.FindProperty("consumable");

        effect.FindPropertyRelative("category").intValue = (int)row.category;
        effect.FindPropertyRelative("heal").intValue = row.heal;
        effect.FindPropertyRelative("water").floatValue = row.water;
        effect.FindPropertyRelative("energy").floatValue = row.energy;
        effect.FindPropertyRelative("waterCost").floatValue = row.waterCost;
        effect.FindPropertyRelative("energyCost").floatValue = row.energyCost;
        effect.FindPropertyRelative("cure").intValue = (int)row.cure;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(item);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');

        EnsureFolder(path.Substring(0, split));

        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
