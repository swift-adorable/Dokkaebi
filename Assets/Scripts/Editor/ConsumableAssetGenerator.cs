using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 소모품 에셋 생성. (로드맵 8-A · docs/Dokkaebi_Consumable_System.md)
///
/// 【이번에 만드는 것은 셋뿐이다 — 회복 · 해제 · 음료와 음식.】
/// 강화와 저항은 「이동 +25%, 120초」처럼 이로운 상태를 걸어야 하는데
/// StatusEffectType에 그것을 담을 자리가 아직 없다. 빈 껍데기 에셋을
/// 미리 만들어 두면 가방에 들어가고 무게를 먹으면서 아무 일도 하지
/// 않는 물건이 된다 — 그쪽이 없는 것보다 나쁘다.
///
/// 수치 근거 —
///   · 체력은 100 고정이므로 회복량을 그 자릿수에 맞춘다. (마스터 프롬프트)
///   · 무게와 스택은 문서 2절 표 그대로. **가벼운 것은 여럿, 무거운 것은 하나.**
///   · 아스피린의 「수분 −15」는 덕코프 실제 값이다. [확인됨 — research 07]
///   · 곶감의 「배고픔은 해결되나 갈증을 유발한다」도 같은 출처다.
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
        public int cureStacks;
        public int useCost;
        public int maxDurability;
        public float castSeconds;
        public StatusEffectType grant;
        public float weight;
        public int stackMax;
        public int value;
    }

    /// <summary>
    /// 표.
    ///
    /// 【회복은 무게가 곧 회복량이다.】 쌍화탕은 9개를 한 칸에 겹칠 수
    /// 있고 대환단은 한 칸을 통째로 먹는다. 그래야 「가볍게 여러 번
    /// 조금씩」과 「한 번에 크게」가 서로 다른 선택이 된다.
    ///
    /// 【해제는 값이 같다.】 어떤 상태가 더 위험한지는 상황이 정하지
    /// 도구가 정하지 않는다. 감두탕이 생강보다 비싸면 플레이어는
    /// 「중독이 더 무서운 것」이라고 잘못 배운다.
    /// </summary>
    private static List<Row> Table()
    {
        var list = new List<Row>(16);

        // 【시전 시간】 환단은 클수록 길다 — 2.0 / 3.0 / 4.5초.
        // 나무위키가 「이 등급부터 사용 시간이 길어져 전투 중에 쓰기 상당히
        // 어렵다」고 적는 것을 따른 것이다 [확인됨]. 다만 **정확한 초는
        // 어디에도 없으므로 아래 값은 우리가 정한 것이다** [불확실].
        // 붕대 1.5 · 해제약 1.0 · 쌍화탕 0.8 · 음식 1.2 —
        // 급할 때 쓰는 것일수록 짧다.
        //
        // ── 회복 — 【내구도(충전)형이다.】 ────────────────────────────
        //
        // 덕코프의 구급상자는 한 번 쓰고 사라지는 물건이 아니다.
        // 소형 125/25 = 5회 · 환단 175/25 = 7회 · 대형 400/40 = 10회.
        // [확인됨 — 아이템 #15 · #16 · #17]
        //
        // 회복량도 그대로 가져온다. Dokkaebi의 체력은 100 고정이고 덕코프
        // 생명력의 기준값은 위키에 없지만(확인 불가), 12 / 20 / 35라는
        // 값 자체가 「한 방에 다 채우지 못한다」를 뜻하므로 그 비율이
        // 옮겨야 할 것이다. 한 번에 다 차면 「지금 쓸까 아꼈다 쓸까」가 없다.
        //
        // 【전부 출혈을 함께 지운다.】 그래서 체력이 가득해도 쓸 이유가 남는다.

        list.Add(Kit("con_medkit_small", "소환단",
            "한 번에 조금씩. 다섯 번 쓸 수 있다.",
            heal: 12, useCost: 25, maxDurability: 125,
            cure: StatusEffectType.Bleed, cureStacks: 1,
            weight: 0.5f, value: 206, castSeconds: 2.0f));

        list.Add(Kit("con_medkit", "환단",
            "제대로 된 것. 출혈도 두 겹까지 잡는다.",
            heal: 20, useCost: 25, maxDurability: 175,
            cure: StatusEffectType.Bleed, cureStacks: 2,
            weight: 0.75f, value: 807, castSeconds: 3.0f));

        list.Add(Kit("con_medkit_large", "대환단",
            "이걸 들고 나왔다면 무언가를 두고 온 것이다.",
            heal: 35, useCost: 40, maxDurability: 400,
            cure: StatusEffectType.Bleed, cureStacks: 99,
            weight: 1.2f, value: 1322, castSeconds: 4.5f));

        // ── 한 번 쓰고 사라지는 것 ────────────────────────────────────
        //
        // 가벼워서 겹쳐 들고 다닌다. 【싼 도구가 비싼 도구와 같은 일을
        // 하면 안 된다】 — 붕대는 출혈 2중첩까지만, 환단은 회복을 겸한다.

        list.Add(Cure("con_bandage", "쑥", "감고 나면 피는 멎는다.",
            StatusEffectType.Bleed, cureStacks: 2,
            heal: 5, weight: 0.05f, stackMax: 3, value: 240, castSeconds: 1.5f));

        // 아스피린의 「수분 −15」는 덕코프 실제 값이다. [확인됨 — 아이템 #20]
        list.Add(Heal("con_aspirin", "쌍화탕",
            "머리가 덜 아프다. 대신 목이 마른다.", 10, 0.03f, 9, 96,
            waterCost: 15f, castSeconds: 0.8f));

        list.Add(Cure("con_antidote", "감두탕", "속을 게워 내는 맛이 난다.",
            StatusEffectType.Poison, 99, 0, 0.1f, 3, 90, castSeconds: 1.0f));

        list.Add(Cure("con_antacid", "황련", "안에서 타는 것을 끈다.",
            StatusEffectType.Ignite, 99, 0, 0.1f, 3, 90, castSeconds: 1.0f));

        list.Add(Cure("con_relaxant", "우황청심원", "경련이 멎는다.",
            StatusEffectType.Shock, 99, 0, 0.1f, 3, 90, castSeconds: 1.0f));

        list.Add(Cure("con_defroster", "생강", "안쪽부터 녹인다.",
            StatusEffectType.Freeze, 99, 0, 0.1f, 3, 90, castSeconds: 1.0f));

        // ── 강화 — 【전부 대가가 붙는다】 (문서 4절) ──────────────────
        //
        // 대가를 별도 필드로 두지 않는다. 축지환의 「끝나면 −15%」는
        // 상태이상 표의 AftermathOf(가속) = 쇠약이 알아서 건다.
        //
        // 주사약은 수분을 태운다 — 「버프를 쓸수록 물이 급해진다」가
        // 소모품을 무한히 쓰지 못하게 하는 장치다. [확인됨 — 덕코프]
        // 축지환(이동 +25% 120초)이 에너지 −5 · 수분 −15다.

        list.Add(Shot("con_stim", "축지환",
            "빨라진다. 끝나면 그만큼 느려진다.",
            StatusEffectType.Haste, waterCost: 15f, energyCost: 5f,
            weight: 0.2f, value: 629));

        list.Add(Shot("con_coagulant", "녹용",
            "굳은 만큼 약이 안 듣는다.",
            StatusEffectType.Bolster, waterCost: 5f, energyCost: 5f,
            weight: 0.2f, value: 648));

        list.Add(Shot("con_regen", "경옥고",
            "천천히 아문다. 급할 때 쓰는 것이 아니다.",
            StatusEffectType.Regen, waterCost: 7f, energyCost: 0f,
            weight: 0.2f, value: 875));

        list.Add(Shot("con_expander", "공진단",
            "더 들고 갈 수 있다. 끝나면 그대로 주저앉는다.",
            StatusEffectType.Overload, waterCost: 10f, energyCost: 0f,
            weight: 0.21f, value: 648));

        // ── 부자탕 — 보스 약품 (문서 4-2) ─────────────────────────────
        //
        // 덕코프 「타길라의 약품」#1400 그대로다 — 에너지 +15 · 수분 −50 ·
        // 무게 0.2 · 가치 2280 · 스택 3 → 「타길라의 힘」 90초. [확인됨]
        //
        // 【90초를 버티면 180을 잃는다.】 최대 체력 110보다 많다.
        // 회복 없이 끝까지 가면 죽는다. 살아 나오려고 쓰는 약이다.
        //
        // 미노타우로스 약품(에너지 +25 · 수분 −75)은 넣지 않는다. 위키의
        // 「미노타우로스의 힘」 buff 수치가 타길라와 한 글자도 다르지 않게
        // 적혀 있어, 설명(「극대」·「대량의 생명력」)과 어긋난다. [불확실]
        // 그대로 넣으면 수분만 더 먹는 하위 호환이 된다.
        list.Add(new Row
        {
            id = "con_absorbent", name = "부자탕",
            desc = "몸이 제 것이 아닌 것처럼 움직인다. 그 대가로 안에서부터 삭는다.",
            category = ConsumableCategory.Boost,
            grant = StatusEffectType.Frenzy,
            energy = 15f, waterCost = 50f,
            cure = StatusEffectType.None, cureStacks = 99,
            weight = 0.2f, stackMax = 3, value = 2280
        });

        // ── 저항 — 장비를 대체하지 못한다 (문서 5절) ──────────────────
        //
        // 덕코프 저항 주사약 4종과 같다 — 「받는 해당 속성 피해 −25%」·120초·
        // 수분 −20·무게 0.19·가치 648·스택 3. [확인됨 — 아이템 #408 · #1070~#1072]
        //
        // 【넷을 동시에 걸 수 있다.】 덕코프도 비배타다. 다만 넷을 다 쓰면
        // 수분이 −80이라 그것이 곧 대가가 된다.
        //
        // 저항에 곱하므로 면역이 되지 않는다 — 0.75는 0을 만들지 못한다.
        // 「소모품만으로 환경을 통과하게 두지 않는다」가 수식으로 지켜진다.

        list.Add(Ward("con_ward_fire", "석고환", "타는 것이 덜 아프다.",
            StatusEffectType.WardFire));

        list.Add(Ward("con_ward_cold", "계피환", "언 것이 덜 아프다.",
            StatusEffectType.WardCold));

        list.Add(Ward("con_ward_shock", "벽조환", "저리는 것이 덜하다.",
            StatusEffectType.WardLightning));

        list.Add(Ward("con_ward_toxin", "녹두환", "삭는 것이 덜하다.",
            StatusEffectType.WardChaos));

        // ── 음료 · 음식 ───────────────────────────────────────────────
        //
        // 【체력을 채우지 않는다.】 먹어서 상처가 낫기 시작하면
        // 수분·에너지 두 축이 체력에 흡수된다. (결정 2-32)

        list.Add(Food("con_water", "샘물", "맛은 없지만 안전하다.",
            water: 40f, energy: 0f, weight: 0.5f, stackMax: 3, value: 30));

        list.Add(Food("con_soda", "식혜", "김이 빠졌다. 그래도 물이다.",
            25f, 5f, 0.4f, 3, 45));

        // 잭 오 랜턴 「에너지 25 · 수분 10」과 같은 결. [확인됨 — 아이템 #1261]
        list.Add(Food("con_canned", "누룽지", "국물까지 마시면 물도 조금 는다.",
            10f, 30f, 0.5f, 2, 55));

        list.Add(Food("con_ration", "미숫가루", "씹는 데 시간이 걸린다.",
            0f, 40f, 0.3f, 3, 60));

        // 배고픔은 해결되나 갈증을 유발한다. [확인됨 — 덕코프]
        list.Add(Food("con_energy_bar", "곶감", "삼키고 나면 물을 찾게 된다.",
            0f, 25f, 0.15f, 5, 40, waterCost: 10f));

        list.Add(Food("con_whisky", "막걸리", "몸이 데워지는 대신 물이 마른다.",
            0f, 10f, 0.6f, 2, 70, waterCost: 15f));

        return list;
    }

    [MenuItem("Dokkaebi/Items/소모품 에셋 생성")]
    public static void Generate()
    {
        EnsureFolder(Root);

        List<Row> rows = Table();

        foreach (Row row in rows)
            CreateOrUpdate(row);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ConsumableAssetGenerator] 소모품 {rows.Count}종 생성 완료 — "
                  + "「Dokkaebi/Playtest/검증 카탈로그 생성」도 다시 실행하십시오.");
    }

    /// <summary>내구도를 깎아 여러 번 쓰는 환단.</summary>
    private static Row Kit(string id, string name, string desc,
                           int heal, int useCost, int maxDurability,
                           StatusEffectType cure, int cureStacks,
                           float weight, int value, float castSeconds)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Restore,
            heal = heal, cure = cure, cureStacks = cureStacks,
            useCost = useCost, maxDurability = maxDurability,
            castSeconds = castSeconds,
            weight = weight, stackMax = 1, value = value
        };
    }

    /// <summary>
    /// 주사약. 【즉시 발동한다.】
    ///
    /// 회복은 시전 시간으로 묶지만 강화는 묶지 않는다. 문서 6절이
    /// 「강화·저항은 즉시」로 정해 둔 것이며, 이유는 쓰임이 다르기
    /// 때문이다 — 회복은 빠져서 쓰는 것이고 강화는 들어가면서 쓰는 것이다.
    /// 강화에도 시전 시간을 붙이면 둘 다 「빠져서 쓰는 것」이 된다.
    /// </summary>
    private static Row Shot(string id, string name, string desc,
                            StatusEffectType grant,
                            float waterCost, float energyCost,
                            float weight, int value)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Boost,
            grant = grant, waterCost = waterCost, energyCost = energyCost,
            cure = StatusEffectType.None, cureStacks = 99,
            weight = weight, stackMax = 3, value = value
        };
    }

    /// <summary>저항 주사약. 수치는 덕코프 저항 주사약 4종 그대로다.</summary>
    private static Row Ward(string id, string name, string desc, StatusEffectType grant)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Ward,
            grant = grant, waterCost = 20f,
            cure = StatusEffectType.None, cureStacks = 99,
            weight = 0.19f, stackMax = 3, value = 648
        };
    }

    private static Row Heal(string id, string name, string desc, int heal,
                            float weight, int stackMax, int value,
                            float waterCost = 0f, float castSeconds = 0f)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Restore,
            heal = heal, waterCost = waterCost, castSeconds = castSeconds,
            cure = StatusEffectType.None, cureStacks = 99,
            weight = weight, stackMax = stackMax, value = value
        };
    }

    private static Row Cure(string id, string name, string desc, StatusEffectType cure,
                            int cureStacks, int heal, float weight, int stackMax, int value,
                            float castSeconds = 0f)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Cure,
            heal = heal, cure = cure, cureStacks = cureStacks,
            castSeconds = castSeconds,
            weight = weight, stackMax = stackMax, value = value
        };
    }

    private static Row Food(string id, string name, string desc,
                            float water, float energy,
                            float weight, int stackMax, int value,
                            float waterCost = 0f, float castSeconds = 1.2f)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            category = ConsumableCategory.Sustenance,
            water = water, energy = energy, waterCost = waterCost,
            castSeconds = castSeconds,
            cure = StatusEffectType.None, cureStacks = 99,
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
        so.FindProperty("maxDurability").intValue = row.maxDurability;
        so.FindProperty("baseValue").intValue = row.value;

        SerializedProperty effect = so.FindProperty("consumable");

        effect.FindPropertyRelative("category").intValue = (int)row.category;
        effect.FindPropertyRelative("heal").intValue = row.heal;
        effect.FindPropertyRelative("water").floatValue = row.water;
        effect.FindPropertyRelative("energy").floatValue = row.energy;
        effect.FindPropertyRelative("waterCost").floatValue = row.waterCost;
        effect.FindPropertyRelative("energyCost").floatValue = row.energyCost;
        effect.FindPropertyRelative("cure").intValue = (int)row.cure;
        effect.FindPropertyRelative("cureStacks").intValue = Mathf.Max(1, row.cureStacks);
        effect.FindPropertyRelative("useCost").intValue = row.useCost;
        effect.FindPropertyRelative("castSeconds").floatValue = row.castSeconds;
        effect.FindPropertyRelative("grant").intValue = (int)row.grant;

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
