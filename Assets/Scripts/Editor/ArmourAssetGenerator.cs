using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static EquipmentAssetWriter;

/// <summary>
/// 방어구 4부위 + 가방 에셋 생성. (로드맵 6-C)
///
/// 티어 대역은 docs/Blob_Equipment_System.md 3절 표를 따른다.
///
/// 이 생성기가 지켜야 하는 세 가지
///  1. 【최고 티어일수록 적재가 준다】 — "방어 최대화 = 파밍량 최소화"
///  2. 【성능 상단에는 음수 옵션이 하나 이상 붙는다】 (티어 4 이상)
///  3. 【얼굴은 속성 대응 전담】 — 방어도를 거의 주지 않고 내성·면역만 준다
/// </summary>
public static class ArmourAssetGenerator
{
    private const string ArmourFolder = Root + "/Armour";
    private const string BackpackFolder = Root + "/Backpacks";

    /// <summary>몸통 변형. 같은 티어에서 "파밍하러 간다 / 버티러 간다"를 가른다.</summary>
    public enum BodyVariant { Normal, Light, Heavy }

    // ── 머리 ──────────────────────────────────────────────────────────────
    // 원거리·투사체 피격에 적용된다. 대가는 감각이다 — 쓰면 잘 안 보이고 잘 안 들린다.
    private static readonly (float armour, float weight, int dur, int value,
                             float viewAngle, float hearing)[] HeadTable =
    {
        (1.0f, 0.4f,  25,   120,   0f,    0f),      // 티어 1 — 대가 거의 없음
        (2.0f, 1.0f,  50,   420,  -3f,   -0.05f),   // 티어 2 — 약함
        (3.0f, 1.6f,  80,  1200,  -6f,   -0.10f),   // 티어 3 — 보통
        (4.0f, 2.4f,  95,  3200, -10f,   -0.18f),   // 티어 4 — 뚜렷
        (5.0f, 3.2f, 130,  7500, -14f,   -0.25f),   // 티어 5 — 강함
        (6.0f, 4.4f, 150, 16000, -20f,   -0.35f)    // 티어 6 — 극단
    };

    private static readonly string[] HeadNames =
    {
        "천 두건", "작업 헬멧", "방탄 헬멧", "격리 헬멧", "복합 장갑 투구", "진압용 폐쇄 투구"
    };

    // ── 몸통 ──────────────────────────────────────────────────────────────
    // 근접·접촉·폭발 피격에 적용된다. 적재 공간을 겸하므로 티어가 오르면 적재가 준다.
    // 적재는 티어가 오르면 줄지만 0까지는 가지 않는다.
    // 덕코프도 최상위(Lv.5 방탄복)에서 +2를 남긴다 — 0으로 만들면
    // 「최고 방어구를 입으면 아예 못 줍는다」가 되어 선택이 아니라 금지가 된다.
    private static readonly (float armour, float weight, int dur, int value, int slots)[] BodyTable =
    {
        (1.5f, 1.5f,  40,   200,  3),
        (2.5f, 2.5f,  60,   700,  3),
        (3.5f, 3.5f, 120,  1900,  3),
        (4.5f, 4.5f, 150,  4800,  2),
        (5.5f, 6.0f, 180, 11000,  2),
        (6.5f, 8.0f, 150, 24000,  1)
    };

    private static readonly string[] BodyNames =
    {
        "누더기 조끼", "작업복", "방탄복", "격리 방호복", "복합 장갑복", "진압용 중장갑"
    };

    // ── 청각 ──────────────────────────────────────────────────────────────
    // 내구도가 없는 슬롯이다. 방어도를 주지 않고 정보만 준다.
    private static readonly (float hearing, float locate, float weight, int value)[] EarsTable =
    {
        (0.10f, 0.0f,  0.1f,   90),
        (0.20f, 0.1f,  0.15f,  300),
        (0.30f, 0.2f,  0.2f,   900),
        (0.40f, 0.35f, 0.25f, 2400),
        (0.55f, 0.5f,  0.3f,  6000),
        (0.70f, 0.7f,  0.4f, 13000)
    };

    private static readonly string[] EarsNames =
    {
        "귀덮개", "청음기", "지향성 집음기", "전술 헤드셋", "음향 해석기", "전역 청음 배열"
    };

    // ── 얼굴 ──────────────────────────────────────────────────────────────
    // 속성 대응 전담. 티어 2 = 내성 / 티어 4 = 면역 / 티어 6 = 면역 + 내성.
    // 티어 3~4에서 수치가 아니라 면역으로 넘어가는 것이 의도된 질적 도약이다.
    private struct FaceElement
    {
        public string key;
        public string label;
        public EquipmentStatType resist;
        public StatusEffectType status;
    }

    private static readonly FaceElement[] FaceElements =
    {
        new() { key = "fire",      label = "내화",  resist = EquipmentStatType.ResistFire,      status = StatusEffectType.Ignite },
        new() { key = "cold",      label = "내한",  resist = EquipmentStatType.ResistCold,      status = StatusEffectType.Chill },
        new() { key = "lightning", label = "절연",  resist = EquipmentStatType.ResistLightning, status = StatusEffectType.Shock  },
        new() { key = "chaos",     label = "방독",  resist = EquipmentStatType.ResistChaos,     status = StatusEffectType.Poison },
        new() { key = "physical",  label = "방탄",  resist = EquipmentStatType.ResistPhysical,  status = StatusEffectType.Bleed  }
    };

    [MenuItem("Blob/Equipment/방어구 · 가방 에셋 생성")]
    public static void Generate()
    {
        int count = 0;

        count += GenerateHead();
        count += GenerateBody();
        count += GenerateEars();
        count += GenerateFace();
        count += GenerateBackpacks();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ArmourAssetGenerator] 방어구 · 가방 {count}종 생성 완료");
    }

    private static int GenerateHead()
    {
        for (int i = 0; i < HeadTable.Length; i++)
        {
            int tier = i + 1;
            var t = HeadTable[i];

            var stats = new List<EquipmentStat> { S(EquipmentStatType.HeadArmour, t.armour) };

            if (t.viewAngle != 0f) stats.Add(S(EquipmentStatType.ViewAngle, t.viewAngle));
            if (t.hearing != 0f) stats.Add(S(EquipmentStatType.Hearing, t.hearing));

            Write(new Row
            {
                id = $"arm_head_t{tier}",
                name = $"{HeadNames[i]}",
                desc = tier <= 2
                    ? "머리를 가린다. 시야는 아직 트여 있다."
                    : "머리를 확실히 가린다. 대신 보이는 것과 들리는 것이 줄어든다.",
                kind = ItemKind.Armour,
                slot = EquipmentSlot.Head,
                tier = tier,
                weight = t.weight,
                durability = t.dur,
                value = t.value,
                stats = stats
            }, ArmourFolder, isWeapon: false);
        }

        return HeadTable.Length;
    }

    private static int GenerateBody()
    {
        int made = 0;

        for (int i = 0; i < BodyTable.Length; i++)
        {
            int tier = i + 1;

            foreach (BodyVariant variant in new[] { BodyVariant.Normal, BodyVariant.Light, BodyVariant.Heavy })
            {
                WriteBody(tier, i, variant);
                made++;
            }
        }

        return made;
    }

    private static void WriteBody(int tier, int index, BodyVariant variant)
    {
        var t = BodyTable[index];

        float armour = t.armour;
        float weight = t.weight;
        int dur = t.dur;
        int slots = t.slots;

        string suffix = string.Empty;
        string label = string.Empty;
        string desc;

        switch (variant)
        {
            case BodyVariant.Light:
                // 방어를 조금 내주고 적재를 크게 얻는다. 파밍용이다.
                armour -= 0.1f;
                weight *= 0.85f;
                dur = Mathf.RoundToInt(dur * 0.85f);
                slots += 4;
                suffix = "_light";
                label = " (경량)";
                desc = "주머니를 늘리고 판을 덜어냈다. 털러 갈 때 입는다.";
                break;

            case BodyVariant.Heavy:
                // 적재를 전부 내주고 방어와 내구를 얻는다. 버티러 갈 때다.
                // 일반 변형이 최소 1칸을 남기므로, 「0칸」은 중갑을 고른 대가다.
                armour += 0.3f;
                weight *= 1.5f;
                dur = Mathf.RoundToInt(dur * 1.4f);
                slots = 0;
                suffix = "_heavy";
                label = " (중갑)";
                desc = "판을 겹쳐 덧댔다. 아무것도 못 들고 오지만 오래 버틴다.";
                break;

            default:
                desc = "몸통을 가린다. 주머니가 몇 개 달려 있다.";
                break;
        }

        armour = Mathf.Min(armour, CombatConstants.MaxArmour);

        var stats = new List<EquipmentStat> { S(EquipmentStatType.BodyArmour, armour) };

        if (slots > 0)
            stats.Add(S(EquipmentStatType.SlotCapacity, slots));

        // 성능 상단(티어 4 이상)에는 음수 옵션을 하나 이상 붙인다.
        // 중갑은 적재 0이 이미 대가이므로 추가 페널티를 얹지 않는다.
        if (tier >= 4 && variant != BodyVariant.Heavy)
            stats.Add(S(EquipmentStatType.MoveAbility, tier == 6 ? -0.10f : -0.05f));

        if (variant == BodyVariant.Heavy && tier >= 4)
            stats.Add(S(EquipmentStatType.MoveAbility, tier == 6 ? -0.18f : -0.10f));

        Write(new Row
        {
            id = $"arm_body_t{tier}{suffix}",
            name = $"{BodyNames[index]}{label}",
            desc = desc,
            kind = ItemKind.Armour,
            slot = EquipmentSlot.Body,
            tier = tier,
            weight = Mathf.Round(weight * 10f) / 10f,
            durability = dur,
            value = variant == BodyVariant.Normal ? t.value : Mathf.RoundToInt(t.value * 1.15f),
            stats = stats
        }, ArmourFolder, isWeapon: false);
    }

    private static int GenerateEars()
    {
        for (int i = 0; i < EarsTable.Length; i++)
        {
            int tier = i + 1;
            var t = EarsTable[i];

            var stats = new List<EquipmentStat> { S(EquipmentStatType.Hearing, t.hearing) };

            if (t.locate > 0f)
                stats.Add(S(EquipmentStatType.SoundLocate, t.locate));

            Write(new Row
            {
                id = $"arm_ears_t{tier}",
                name = EarsNames[i],
                desc = "소리가 어디서 오는지 알려준다. 방어에는 아무 도움이 되지 않는다.",
                kind = ItemKind.Armour,
                slot = EquipmentSlot.Ears,
                tier = tier,
                weight = t.weight,
                durability = 0,          // 청각 슬롯은 내구도가 없다
                value = t.value,
                stats = stats
            }, ArmourFolder, isWeapon: false);
        }

        return EarsTable.Length;
    }

    private static int GenerateFace()
    {
        int made = 0;

        // 티어 1 — 속성을 고르기 전의 범용품. 전 속성 소폭.
        Write(new Row
        {
            id = "arm_face_t1",
            name = "천 마스크",
            desc = "먼지를 막는다. 그 이상은 기대할 수 없다.",
            kind = ItemKind.Armour,
            slot = EquipmentSlot.Face,
            tier = 1,
            weight = 0.2f,
            durability = 25,
            value = 100,
            stats = new List<EquipmentStat>
            {
                S(EquipmentStatType.ResistFire, 0.04f),
                S(EquipmentStatType.ResistCold, 0.04f),
                S(EquipmentStatType.ResistLightning, 0.04f),
                S(EquipmentStatType.ResistChaos, 0.04f),
                S(EquipmentStatType.ResistPhysical, 0.04f)
            }
        }, ArmourFolder, isWeapon: false);

        made++;

        foreach (FaceElement e in FaceElements)
        {
            // 티어 2 — 수치 대응
            WriteFace(e, tier: 2, resist: 0.15f, immunity: StatusEffectType.None,
                weight: 0.3f, dur: 55, value: 500,
                name: $"{e.label} 마스크",
                desc: "해당 속성 피해를 조금 덜 받는다.");

            // 티어 4 — 질적 도약. 상태이상 자체를 막는다.
            WriteFace(e, tier: 4, resist: 0f, immunity: e.status,
                weight: 0.5f, dur: 100, value: 3600,
                name: $"{e.label} 차단 마스크",
                desc: "상태이상 자체를 막는다. 다만 피해는 그대로 들어온다.");

            // 티어 6 — 면역 + 내성. 시야를 대가로 받는다.
            WriteFace(e, tier: 6, resist: 0.30f, immunity: e.status,
                weight: 0.8f, dur: 150, value: 15000,
                name: $"{e.label} 완전 차단면",
                desc: "막고 덜 받는다. 대신 앞이 잘 보이지 않는다.");

            made += 3;
        }

        return made;
    }

    private static void WriteFace(FaceElement e, int tier, float resist,
                                  StatusEffectType immunity, float weight, int dur, int value,
                                  string name, string desc)
    {
        var stats = new List<EquipmentStat>();

        if (resist > 0f)
            stats.Add(S(e.resist, resist));

        // 얼굴은 시야·감지를 겸한다. 티어가 오르면 대가로 시야가 준다.
        if (tier >= 4)
            stats.Add(S(EquipmentStatType.ViewAngle, tier == 6 ? -12f : -6f));

        if (tier == 6)
            stats.Add(S(EquipmentStatType.ViewDistance, -1.5f));

        Write(new Row
        {
            id = $"arm_face_{e.key}_t{tier}",
            name = name,
            desc = desc,
            kind = ItemKind.Armour,
            slot = EquipmentSlot.Face,
            tier = tier,
            weight = weight,
            durability = dur,
            value = value,
            immunity = immunity,
            stats = stats
        }, ArmourFolder, isWeapon: false);
    }

    private static int GenerateBackpacks()
    {
        // 「최대 소지 중량」(kg)과 「적재 공간」(칸)은 별도 자원이다.
        // 가방마다 배분이 달라야 "초반 병목은 칸, 후반 병목은 무게"가 선택으로 나타난다.
        // 덕코프 가방 5티어의 곡선을 그대로 가져왔다 —
        // 일반 티어는 중량과 칸이 거의 1:1로 같이 오르고,
        // 배분을 가르는 것은 【특수 가방】이다. (research/duckov/08 6절)
        //
        // 그래서 티어 1~3과 6은 균형 곡선, 티어 4·5만 배분을 극단으로 둔다.
        // 전부 배분이 다르면 "이번엔 뭘 들지"가 아니라 매번 계산이 된다.
        (string id, string name, int tier, float carryWeight, int slots,
         float weight, int value, string desc)[] table =
        {
            ("bag_t1_sack",   "낡은 자루",        1,  5f,  8, 0.45f,   150,
                "구멍이 몇 개 나 있지만 담긴다."),
            ("bag_t2_vest",   "수납 조끼",        2, 10f, 12, 1.1f,    480,
                "가슴팍에 주머니가 여럿 달려 있다."),
            ("bag_t3_pack",   "작업 배낭",        3, 15f, 17, 2.2f,   1500,
                "균형이 잡혀 있다. 무엇을 담아도 무리가 없다."),
            ("bag_t4_frame",  "운반 프레임",      4, 30f, 10, 2.6f,   4000,
                "무거운 것을 지고 나르는 용도다. 칸은 적다."),
            ("bag_t5_module", "구획 배낭",        5, 12f, 24, 2.0f,   9500,
                "칸이 많다. 작고 값비싼 것을 긁어올 때 쓴다."),
            ("bag_t6_haul",   "반출용 대형 배낭",  6, 30f, 30, 3.9f,  22000,
                "한 번에 다 가져갈 수 있다. 다만 뛸 수 없다.")
        };

        foreach (var b in table)
        {
            var stats = new List<EquipmentStat>
            {
                S(EquipmentStatType.MaxCarryWeight, b.carryWeight),
                S(EquipmentStatType.SlotCapacity, b.slots)
            };

            // 성능 상단에는 대가가 붙는다.
            if (b.tier >= 5)
                stats.Add(S(EquipmentStatType.MoveAbility, b.tier == 6 ? -0.08f : -0.04f));

            Write(new Row
            {
                id = b.id,
                name = b.name,
                desc = b.desc,
                kind = ItemKind.Backpack,
                slot = EquipmentSlot.Backpack,
                tier = b.tier,
                weight = b.weight,
                durability = 0,          // 가방은 내구도가 없다
                value = b.value,
                stats = stats
            }, BackpackFolder, isWeapon: false);
        }

        return table.Length;
    }
}
