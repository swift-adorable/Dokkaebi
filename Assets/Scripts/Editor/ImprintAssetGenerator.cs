using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static EquipmentAssetWriter;

/// <summary>
/// 각인 8계열 × 3단계 = 24종 에셋 생성. (로드맵 6-C)
///
/// 【절대 규칙 — 각인은 순수 증가를 주지 않는다】
/// 각인은 철수에 실패해도 잃지 않는 유일한 장착품이다.
/// 순수 증가를 주면 "죽어도 사라지지 않는 전투력"이 되어
/// 「죽으면 들고 있던 것 전부」라는 이 게임의 뼈대가 물러진다.
/// 그래서 전부 「A를 깎아 B를 얻는다」이며 예외가 없다.
/// (docs/Dokkaebi_Imprint_System.md 0절)
///
/// 대가는 전부 【음수 스탯】으로 표현한다. 별도 필드를 만들지 않는 이유 —
/// 합산 경로가 하나여야 "왜 이 수치가 이렇게 나왔는가"를 추적할 수 있다.
///
/// ※ 「정밀 Ⅲ — 탄창당 1발」은 탄창 시스템이 아직 없어
///   발사 간격 +150%로 대체했다. 탄창이 들어오면 교체한다. (문서 7절 TBD)
/// </summary>
public static class ImprintAssetGenerator
{
    private const string Folder = Root + "/Imprints";

    /// <summary>각인 하나. 이득과 대가가 같은 stats 배열에 들어간다.</summary>
    public struct Spec
    {
        public string family;        // 계열 키 (영문) — 중복 장착 판정에 쓴다
        public string familyLabel;   // 계열 이름 (한국어)
        public int tier;             // 1 / 2 / 3
        public string desc;
        public StatusEffectType immunity;
        public StatusEffectType[] extraImmunities;
        public List<EquipmentStat> stats;
    }

    /// <summary>단계별 가치. (docs/Dokkaebi_Imprint_System.md 2절)</summary>
    private static readonly int[] TierValue = { 350, 1000, 3500 };

    /// <summary>각인의 무게는 고정이다. 각인은 무게 결정을 만들지 않는다 — 슬롯이 결정이다.</summary>
    private const float FixedWeight = 0.3f;

    private static readonly string[] TierMark = { "Ⅰ", "Ⅱ", "Ⅲ" };

    public static List<Spec> Table()
    {
        var list = new List<Spec>(24);

        // ── 진격 — 피해를 얻고 사거리를 잃는다 ─────────────────────────────
        list.Add(New("charge", "진격", 1, "붙어서 쏘라고 새긴 것이다.",
            S(EquipmentStatType.DamageIncrease, 0.12f),
            S(EquipmentStatType.WeaponRangeIncrease, -0.15f)));

        list.Add(New("charge", "진격", 2, "더 세게, 더 가까이.",
            S(EquipmentStatType.DamageIncrease, 0.25f),
            S(EquipmentStatType.WeaponRangeIncrease, -0.30f)));

        // Ⅲ의 대가는 같은 축이 아니라 다른 축을 깎는다.
        // 그래야 Ⅲ 두 개를 끼는 선택이 실제로 위험해진다.
        list.Add(New("charge", "진격", 3, "몸이 계속 녹는다. 그만큼 세게 나간다.",
            S(EquipmentStatType.DamageIncrease, 0.45f),
            S(EquipmentStatType.HealthRegen, -1.5f)));

        // ── 관망 — 사거리를 얻고 발을 잃는다 ───────────────────────────────
        list.Add(New("watch", "관망", 1, "멀리서 보고 멀리서 쏜다.",
            S(EquipmentStatType.WeaponRangeIncrease, 0.15f),
            S(EquipmentStatType.MoveAbility, -0.06f)));

        list.Add(New("watch", "관망", 2, "시야가 트이는 대신 발이 무거워진다.",
            S(EquipmentStatType.WeaponRangeIncrease, 0.30f),
            S(EquipmentStatType.ViewDistance, 3f),
            S(EquipmentStatType.MoveAbility, -0.12f)));

        list.Add(New("watch", "관망", 3, "먼 것은 다 보인다. 붙으면 죽는다.",
            S(EquipmentStatType.WeaponRangeIncrease, 0.55f),
            S(EquipmentStatType.ViewDistance, 5f),
            S(EquipmentStatType.BodyArmour, -4f)));

        // ── 경량 — 속도를 얻고 몸을 잃는다 ─────────────────────────────────
        list.Add(New("feather", "경량", 1, "가볍게 새겼다.",
            S(EquipmentStatType.MoveAbility, 0.10f),
            S(EquipmentStatType.MaxHealth, -8f)));

        list.Add(New("feather", "경량", 2, "구르기가 빨라지고 몸이 얇아진다.",
            S(EquipmentStatType.MoveAbility, 0.20f),
            S(EquipmentStatType.DashCooldown, -0.25f),
            S(EquipmentStatType.MaxHealth, -18f)));

        // 「방어도 0 취급」 — 큰 음수를 주면 합산 후 0에서 잘린다.
        // 새 규칙을 만들지 않고 기존 합산 경로로 표현한다.
        list.Add(New("feather", "경량", 3, "아무것도 막지 못한다. 대신 아무것도 못 맞힌다.",
            S(EquipmentStatType.MoveAbility, 0.35f),
            S(EquipmentStatType.HeadArmour, -12f),
            S(EquipmentStatType.BodyArmour, -12f)));

        // ── 중장 — 방어를 얻고 기동을 잃는다 ───────────────────────────────
        list.Add(New("bulwark", "중장", 1, "두껍게 새겼다.",
            S(EquipmentStatType.HeadArmour, 0.8f),
            S(EquipmentStatType.BodyArmour, 0.8f),
            S(EquipmentStatType.MoveAbility, -0.07f)));

        list.Add(New("bulwark", "중장", 2, "버티는 쪽으로 완전히 기울었다.",
            S(EquipmentStatType.HeadArmour, 1.6f),
            S(EquipmentStatType.BodyArmour, 1.6f),
            S(EquipmentStatType.MoveAbility, -0.15f)));

        // 「대시 불가」 — 쿨다운을 크게 늘려 표현한다.
        list.Add(New("bulwark", "중장", 3, "움직이지 않는다. 그 자리에서 버틴다.",
            S(EquipmentStatType.HeadArmour, 2.6f),
            S(EquipmentStatType.BodyArmour, 2.6f),
            S(EquipmentStatType.DashCooldown, 99f)));

        // ── 연소 — 상태이상을 얻고 직접 피해를 잃는다 ───────────────────────
        list.Add(New("smoulder", "연소", 1, "태우는 쪽에 무게를 옮겼다.",
            S(EquipmentStatType.AilmentPower, 0.20f),
            S(EquipmentStatType.DamageIncrease, -0.10f)));

        list.Add(New("smoulder", "연소", 2, "직접 때리는 것은 포기했다.",
            S(EquipmentStatType.AilmentPower, 0.45f),
            S(EquipmentStatType.DamageIncrease, -0.25f)));

        list.Add(New("smoulder", "연소", 3, "탄은 불씨일 뿐이다.",
            S(EquipmentStatType.AilmentPower, 0.80f),
            S(EquipmentStatType.DamageIncrease, -0.50f)));

        // ── 정밀 — 치명을 얻고 연사를 잃는다 ───────────────────────────────
        list.Add(New("precision", "정밀", 1, "한 발을 아껴 쏜다.",
            S(EquipmentStatType.CriticalChance, 0.08f),
            S(EquipmentStatType.FireIntervalIncrease, 0.10f)));

        list.Add(New("precision", "정밀", 2, "느리지만 한 발이 무겁다.",
            S(EquipmentStatType.CriticalChance, 0.18f),
            S(EquipmentStatType.CriticalMultiplier, 0.3f),
            S(EquipmentStatType.FireIntervalIncrease, 0.25f)));

        list.Add(New("precision", "정밀", 3, "한 발에 전부 건다.",
            S(EquipmentStatType.CriticalChance, 0.35f),
            S(EquipmentStatType.CriticalMultiplier, 0.8f),
            S(EquipmentStatType.FireIntervalIncrease, 1.50f)));

        // ── 먹성 — 성장을 얻고 몸을 잃는다 ─────────────────────────────────
        list.Add(New("devour", "먹성", 1, "많이 먹는 쪽으로 새겼다.",
            S(EquipmentStatType.XpAbsorbAmount, 0.15f),
            S(EquipmentStatType.MaxHealth, -8f)));

        list.Add(New("devour", "먹성", 2, "멀리 있는 것까지 끌어와 삼킨다.",
            S(EquipmentStatType.XpAbsorbAmount, 0.35f),
            S(EquipmentStatType.XpAbsorbRange, 1.5f),
            S(EquipmentStatType.MaxHealth, -18f)));

        // 「회복량 0」 — 받는 회복량 −100%.
        list.Add(New("devour", "먹성", 3, "삼키는 것으로만 살아간다. 약은 듣지 않는다.",
            S(EquipmentStatType.XpAbsorbAmount, 0.60f),
            S(EquipmentStatType.XpAbsorbRange, 3f),
            S(EquipmentStatType.HealingReceived, -1.0f)));

        // ── 역치 — 상태는 막고 피해는 받는다 ───────────────────────────────
        // 덕코프 토템의 저항 계열이 수치가 아니라 【면역】을 파는 것을 그대로 가져왔다.
        // 다만 면역의 대가는 그 속성에 더 약해지는 것이다.
        // 이 구분이 DamageResolver(피해)와 StatusEffect(상태)의 분리를 드러낸다.
        list.Add(NewThreshold(1, "불이 붙지 않는다. 다만 더 뜨겁다.",
            StatusEffectType.Ignite, null,
            S(EquipmentStatType.ResistFire, -0.5f)));

        list.Add(NewThreshold(2, "두 가지를 막는다. 그 두 가지가 더 아프다.",
            StatusEffectType.Ignite, new[] { StatusEffectType.Chill },
            S(EquipmentStatType.ResistFire, -0.5f),
            S(EquipmentStatType.ResistCold, -0.5f)));

        list.Add(NewThreshold(3, "상태는 전부 막는다. 피해는 두 배로 받는다.",
            StatusEffectType.Ignite, new[] { StatusEffectType.Chill, StatusEffectType.Shock },
            S(EquipmentStatType.ResistFire, -1.0f),
            S(EquipmentStatType.ResistCold, -1.0f),
            S(EquipmentStatType.ResistLightning, -1.0f)));

        return list;
    }

    [MenuItem("Dokkaebi/Equipment/각인 에셋 생성")]
    public static void Generate()
    {
        List<Spec> table = Table();

        foreach (Spec spec in table)
            WriteOne(spec);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ImprintAssetGenerator] 각인 {table.Count}종 생성 완료");
    }

    public static void WriteOne(Spec spec)
    {
        int index = Mathf.Clamp(spec.tier - 1, 0, 2);

        Write(new Row
        {
            id = $"imp_{spec.family}_t{spec.tier}",
            name = $"{spec.familyLabel} {TierMark[index]}",
            desc = spec.desc,
            kind = ItemKind.Imprint,
            slot = EquipmentSlot.ImprintA,   // 각인은 두 슬롯 어디에나 들어간다
            tier = spec.tier,
            weight = FixedWeight,
            durability = 0,                  // 각인은 내구도가 없다
            value = TierValue[index],
            imprintFamily = spec.family,
            immunity = spec.immunity,
            extraImmunities = spec.extraImmunities,
            stats = spec.stats
        }, Folder, isWeapon: false);
    }

    private static Spec New(string family, string label, int tier, string desc,
                            params EquipmentStat[] stats)
    {
        return new Spec
        {
            family = family,
            familyLabel = label,
            tier = tier,
            desc = desc,
            immunity = StatusEffectType.None,
            extraImmunities = null,
            stats = new List<EquipmentStat>(stats)
        };
    }

    private static Spec NewThreshold(int tier, string desc,
                                     StatusEffectType immunity,
                                     StatusEffectType[] extra,
                                     params EquipmentStat[] stats)
    {
        return new Spec
        {
            family = "threshold",
            familyLabel = "역치",
            tier = tier,
            desc = desc,
            immunity = immunity,
            extraImmunities = extra,
            stats = new List<EquipmentStat>(stats)
        };
    }
}
