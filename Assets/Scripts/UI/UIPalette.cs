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
    public static readonly Color Dim = new(0.01f, 0.02f, 0.04f, 0.72f);

    /// <summary>패널 배경.</summary>
    public static readonly Color Panel = new(0.14f, 0.19f, 0.27f, 0.42f);

    /// <summary>패널 안 머리글 띠.</summary>
    public static readonly Color Header = new(0.20f, 0.27f, 0.38f, 0.50f);

    /// <summary>빈 칸.</summary>
    public static readonly Color Slot = new(0.22f, 0.28f, 0.38f, 0.34f);

    /// <summary>내용이 있는 칸.</summary>
    public static readonly Color SlotFilled = new(0.26f, 0.34f, 0.45f, 0.44f);

    /// <summary>선택된 칸.</summary>
    public static readonly Color SlotSelected = new(0.38f, 0.62f, 0.95f, 0.62f);

    /// <summary>
    /// 고른 장비가 들어갈 수 있는 자리. 선택색(파랑)과 확실히 달라야 한다 —
    /// 「지금 고른 것」과 「여기 넣을 수 있다」는 다른 뜻이다.
    /// </summary>
    public static readonly Color SlotEquippable = new(0.26f, 0.72f, 0.50f, 0.52f);

    /// <summary>잠긴 칸.</summary>
    public static readonly Color SlotLocked = new(0.06f, 0.08f, 0.11f, 0.42f);

    /// <summary>주 동작 버튼.</summary>
    public static readonly Color Action = new(0.26f, 0.54f, 0.90f, 0.66f);

    /// <summary>보조 동작 버튼.</summary>
    public static readonly Color Subtle = new(0.36f, 0.42f, 0.52f, 0.40f);

    /// <summary>본문 글자.</summary>
    public static readonly Color Text = new(0.93f, 0.95f, 0.97f, 1f);

    /// <summary>보조 설명 글자.</summary>
    public static readonly Color TextDim = new(0.66f, 0.71f, 0.78f, 1f);

    /// <summary>수치·강조 글자.</summary>
    public static readonly Color TextAccent = new(1f, 0.85f, 0.45f, 1f);

    /// <summary>경고(과중량·실패).</summary>
    public static readonly Color Warning = new(0.92f, 0.45f, 0.38f, 1f);

    /// <summary>
    /// 숫자 배지의 알약 바탕. 칸 색이 아이템마다 달라서
    /// 글자만 얹으면 밝은 칸에서 숫자가 사라진다.
    /// </summary>
    public static readonly Color Badge = new(0.02f, 0.03f, 0.05f, 0.80f);

    /// <summary>패널 윤곽선. 면과 면을 갈라 준다.</summary>
    public static readonly Color Edge = new(0.62f, 0.72f, 0.88f, 0.34f);

    /// <summary>칸 윤곽선. 패널보다 약하다.</summary>
    public static readonly Color EdgeSoft = new(0.60f, 0.70f, 0.86f, 0.20f);

    /// <summary>패널 안쪽을 한 단계 눌러 깊이를 만든다.</summary>
    public static readonly Color Inset = new(0.04f, 0.06f, 0.10f, 0.34f);

    /// <summary>표 형태의 줄. 홀짝으로 번갈아 깐다.</summary>
    public static readonly Color Row = new(0.42f, 0.52f, 0.68f, 0.16f);

    public static readonly Color RowAlt = new(0.42f, 0.52f, 0.68f, 0.07f);

    /// <summary>이득 수치(초록) / 대가 수치(주황).</summary>
    public static readonly Color Gain = new(0.48f, 0.82f, 0.56f, 1f);

    public static readonly Color Cost = new(0.95f, 0.62f, 0.40f, 1f);

    /// <summary>
    /// 유리판의 테두리. 안쪽보다 밝아야 「모서리에 빛이 맺혔다」로 읽힌다.
    /// </summary>
    public static readonly Color Rim = new(0.78f, 0.86f, 1f, 0.30f);

    /// <summary>위쪽 절반을 덮는 넓고 옅은 광택. 판에 두께를 준다.</summary>
    public static readonly Color SheenWide = new(1f, 1f, 1f, 0.045f);

    /// <summary>맨 윗변의 얇고 밝은 띠. 모서리 하이라이트다.</summary>
    public static readonly Color SheenEdge = new(1f, 1f, 1f, 0.10f);

    /// <summary>
    /// 반투명 위에 올릴 때 쓰는 글자색. 배경이 비쳐도 읽히도록
    /// 기본 글자보다 한 단계 밝다.
    /// </summary>
    public static readonly Color TextOnGlass = new(0.97f, 0.98f, 1f, 1f);

    /// <summary>
    /// 아이템 칸의 바탕. 종류 색을 유리처럼 묽힌다 —
    /// 칸마다 불투명한 색판이면 격자가 색종이처럼 보인다.
    /// </summary>
    public static Color Glassify(Color source, float alpha = 0.42f)
    {
        return new Color(source.r, source.g, source.b, alpha);
    }

    /// <summary>같은 색을 밝혀 테두리로 쓴다. 종류별 색을 한 번 더 정의하지 않는다.</summary>
    public static Color Brighten(Color source, float amount = 0.28f)
    {
        return new Color(
            Mathf.Clamp01(source.r + amount),
            Mathf.Clamp01(source.g + amount),
            Mathf.Clamp01(source.b + amount),
            Mathf.Clamp01(source.a + 0.05f));
    }

    /// <summary>같은 색을 어둡게. 칸 바탕은 테두리보다 가라앉아야 글자가 읽힌다.</summary>
    public static Color Darken(Color source, float amount = 0.34f)
    {
        return new Color(
            Mathf.Max(0f, source.r - amount),
            Mathf.Max(0f, source.g - amount),
            Mathf.Max(0f, source.b - amount),
            source.a);
    }

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
