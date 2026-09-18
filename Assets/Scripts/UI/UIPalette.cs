using UnityEngine;

/// <summary>
/// 화면 전체가 공유하는 색. 창마다 색을 직접 적으면 화면이 따로 논다.
///
/// 덕코프 스크린샷의 색 구성을 기준으로 잡았다 —
/// 어두운 남색 반투명 패널 / 한 단계 밝은 칸 / 분류별 테두리 색.
/// </summary>
public static class UIPalette
{
    /// <summary>화면 전체를 덮는 어두운 막.</summary>
    public static readonly Color Dim = new(0f, 0f, 0f, 0.62f);

    /// <summary>패널 배경.</summary>
    public static readonly Color Panel = new(0.09f, 0.12f, 0.16f, 0.94f);

    /// <summary>패널 안 머리글 띠.</summary>
    public static readonly Color Header = new(0.13f, 0.17f, 0.23f, 0.96f);

    /// <summary>빈 칸.</summary>
    public static readonly Color Slot = new(0.16f, 0.20f, 0.26f, 0.92f);

    /// <summary>내용이 있는 칸.</summary>
    public static readonly Color SlotFilled = new(0.21f, 0.27f, 0.34f, 0.95f);

    /// <summary>선택된 칸.</summary>
    public static readonly Color SlotSelected = new(0.36f, 0.56f, 0.82f, 0.98f);

    /// <summary>잠긴 칸.</summary>
    public static readonly Color SlotLocked = new(0.10f, 0.11f, 0.13f, 0.85f);

    /// <summary>주 동작 버튼.</summary>
    public static readonly Color Action = new(0.22f, 0.45f, 0.72f, 0.96f);

    /// <summary>보조 동작 버튼.</summary>
    public static readonly Color Subtle = new(0.24f, 0.27f, 0.32f, 0.96f);

    /// <summary>본문 글자.</summary>
    public static readonly Color Text = new(0.93f, 0.95f, 0.97f, 1f);

    /// <summary>보조 설명 글자.</summary>
    public static readonly Color TextDim = new(0.66f, 0.71f, 0.78f, 1f);

    /// <summary>수치·강조 글자.</summary>
    public static readonly Color TextAccent = new(1f, 0.85f, 0.45f, 1f);

    /// <summary>경고(과중량·실패).</summary>
    public static readonly Color Warning = new(0.92f, 0.45f, 0.38f, 1f);

    /// <summary>스킬 분류별 색. Core / Support / Meta / Persistent 순.</summary>
    public static readonly Color[] SkillCategory =
    {
        new(0.72f, 0.26f, 0.22f, 0.96f),
        new(0.20f, 0.45f, 0.80f, 0.96f),
        new(0.60f, 0.42f, 0.14f, 0.96f),
        new(0.26f, 0.52f, 0.34f, 0.96f)
    };

    /// <summary>아이템 종류별 테두리 색. ItemKind 순서와 맞춘다.</summary>
    public static Color ForItem(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Weapon:     return new Color(0.62f, 0.34f, 0.24f, 0.95f);
            case ItemKind.Armour:     return new Color(0.28f, 0.44f, 0.60f, 0.95f);
            case ItemKind.Backpack:   return new Color(0.34f, 0.46f, 0.30f, 0.95f);
            case ItemKind.Imprint:    return new Color(0.52f, 0.34f, 0.62f, 0.95f);
            case ItemKind.SkillGem:   return new Color(0.24f, 0.52f, 0.58f, 0.95f);
            case ItemKind.Consumable: return new Color(0.58f, 0.30f, 0.34f, 0.95f);
            case ItemKind.Key:        return new Color(0.60f, 0.52f, 0.22f, 0.95f);
            default:                  return SlotFilled;
        }
    }
}
