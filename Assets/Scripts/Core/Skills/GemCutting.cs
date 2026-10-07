using System.Collections.Generic;

/// <summary>아직 고르지 않은 구슬의 종류 (결정 2-75).</summary>
public enum BlankGemKind
{
    None = 0,

    /// <summary>핵심 구슬 — 핵심(스킬) 8종 중 하나로.</summary>
    Core = 1,

    /// <summary>보조 구슬 — 보조 34종 중 하나로.</summary>
    Support = 2,

    /// <summary>정신력 구슬 — 발동 5 · 전령 5 중 하나로. 단계가 없다.</summary>
    Spirit = 3
}

public enum GemCutError
{
    None = 0,
    NotBlank = 1,

    /// <summary>이 종류 · 단계로는 고를 수 없는 구슬이다.</summary>
    NotAllowed = 2,

    BagFull = 3
}

/// <summary>
/// 【구슬 고르기】 (결정 2-75 — 도감을 없애고 PoE2의 젬처럼).
///
///   · 구슬은 세 종류로 떨어진다 — 핵심 25 · 보조 60 · 정신력 15 [임시값]
///   · 핵심 · 보조에는 단계가 있다 = 떨어진 장(0장은 1단계). 단계가 고를 수 있는 요구 레벨을 정한다
///     1단계 Lv1 · 2단계 Lv3 · 3단계 Lv5 · 4단계 Lv7 · 5단계 Lv9 · 6단계 전부
///   · 정신력 구슬은 단계 없이 어디서나 — 발동 · 전령 열 가지 중 하나로
///   · 가방에서 언제든 고른다. 한 번 고르면 바꿀 수 없다 — 강화 · 분해는 없다 (결정 2-74)
///   · 끼우는 것은 지금처럼 레벨이 막는다(요구 레벨 · 소켓)
/// 순수 정적 클래스 — EditMode 테스트 대상.
/// </summary>
public static class GemCutting
{
    public const int MaxTier = 6;

    public const int CoreWeight = 25;
    public const int SupportWeight = 60;
    public const int SpiritWeight = 15;

    /// <summary>이 단계로 고를 수 있는 요구 레벨의 상한.</summary>
    public static int LevelCap(int tier)
    {
        if (tier >= MaxTier)
            return int.MaxValue;

        return tier <= 1 ? 1 : tier * 2 - 1;
    }

    /// <summary>장 → 단계. 0장은 1단계, 6장 넘게는 6단계.</summary>
    public static int TierOf(int chapter) => chapter < 1 ? 1 : chapter > MaxTier ? MaxTier : chapter;

    /// <summary>떨어질 종류를 뽑는다. roll은 [0, 100).</summary>
    public static BlankGemKind RollKind(int roll)
    {
        if (roll < CoreWeight) return BlankGemKind.Core;
        if (roll < CoreWeight + SupportWeight) return BlankGemKind.Support;
        return BlankGemKind.Spirit;
    }

    /// <summary>빈 구슬 아이템 id. 정신력 구슬은 단계가 없다.</summary>
    public static string BlankId(BlankGemKind kind, int tier)
    {
        switch (kind)
        {
            case BlankGemKind.Core:    return $"gem_blank_core_{TierOf(tier)}";
            case BlankGemKind.Support: return $"gem_blank_support_{TierOf(tier)}";
            case BlankGemKind.Spirit:  return "gem_blank_spirit";
            default:                   return null;
        }
    }

    public static string NameOf(BlankGemKind kind)
    {
        switch (kind)
        {
            case BlankGemKind.Core:    return "핵심 구슬";
            case BlankGemKind.Support: return "보조 구슬";
            case BlankGemKind.Spirit:  return "정신력 구슬";
            default:                   return string.Empty;
        }
    }

    /// <summary>이 빈 구슬로 고를 수 있는가.</summary>
    public static bool Allows(BlankGemKind kind, int tier, SkillDefinition skill)
    {
        if (skill == null)
            return false;

        switch (kind)
        {
            case BlankGemKind.Core:
                return skill.Category == SkillCategory.Core && skill.RequiredLevel <= LevelCap(tier);
            case BlankGemKind.Support:
                return skill.Category == SkillCategory.Support && skill.RequiredLevel <= LevelCap(tier);
            case BlankGemKind.Spirit:
                return skill.Category == SkillCategory.Meta || skill.Category == SkillCategory.Persistent;
            default:
                return false;
        }
    }

    /// <summary>고를 수 있는 구슬 — 요구 레벨 · 이름 순.</summary>
    public static List<SkillDefinition> Candidates(BlankGemKind kind, int tier, IReadOnlyList<SkillDefinition> all)
    {
        var list = new List<SkillDefinition>();

        if (all == null)
            return list;

        foreach (SkillDefinition s in all)
            if (Allows(kind, tier, s))
                list.Add(s);

        list.Sort((a, b) => a.RequiredLevel != b.RequiredLevel
            ? a.RequiredLevel.CompareTo(b.RequiredLevel)
            : string.CompareOrdinal(a.Id, b.Id));

        return list;
    }

    /// <summary>
    /// 고른다 — 가방의 빈 구슬 하나를 빼고 고른 구슬을 넣는다. 구슬은 칸을 먹지 않으므로 자리는 거의 늘 있다.
    /// </summary>
    public static GemCutError Cut(Inventory bag, ItemDefinition blank, SkillDefinition choice, ItemDefinition result)
    {
        if (blank == null || !blank.IsBlankGem)
            return GemCutError.NotBlank;

        if (!Allows(blank.BlankGem, blank.Tier, choice) || result == null || result.Skill != choice)
            return GemCutError.NotAllowed;

        if (bag == null || bag.CountOf(blank) <= 0)
            return GemCutError.NotBlank;

        if (bag.Remove(blank, 1) != 1)
            return GemCutError.NotBlank;

        if (bag.TryAdd(result, 1) != 1)
        {
            bag.TryAdd(blank, 1); // 되돌린다
            return GemCutError.BagFull;
        }

        return GemCutError.None;
    }

    public static string Explain(GemCutError error)
    {
        switch (error)
        {
            case GemCutError.NotBlank:   return "고를 구슬이 없다.";
            case GemCutError.NotAllowed: return "이 구슬로는 고를 수 없다.";
            case GemCutError.BagFull:    return "가방에 자리가 없다.";
            default:                     return string.Empty;
        }
    }
}
