using System.Collections.Generic;

/// <summary>상점 한 줄 — 무엇을, 몇 개까지, 가치의 몇 배로 파는가.</summary>
public readonly struct ShopEntry
{
    public readonly string ItemId;

    /// <summary>재고가 다 차 있을 때의 개수. 덕코프 상점 표의 「최대 재고」.</summary>
    public readonly int MaxStock;

    /// <summary>구매가 = 아이템 가치 × 이 값. 덕코프 상점 표의 「가격 계수」.</summary>
    public readonly float PriceMultiplier;

    public ShopEntry(string itemId, int maxStock, float priceMultiplier)
    {
        ItemId = itemId;
        MaxStock = maxStock < 1 ? 1 : maxStock;
        PriceMultiplier = priceMultiplier <= 0f ? 1f : priceMultiplier;
    }
}

/// <summary>
/// 【상점 재고표.】 (docs/Blob_Bunker_System.md 3절)
///
/// 덕코프 위키의 아이템 페이지에는 「상점 이름 · 위치 · 확률 · 최대 재고 · 가격 계수」
/// 표가 붙어 있다 [확인됨]. 재고와 가격 계수는 그 표를 그대로 옮겼다.
///
/// 【파는 곳을 하나로 모았다.】 덕코프는 구급상자·붕대·아스피린을 잡화 상점에서,
/// 주사약을 머드(헬스장을 지으면 입주하는 NPC)에게서 판다. Blob의 벙커 문서는
/// 거래 건물을 무기 상점 · 방어구 상점 · 잡화 상점 셋으로 정했고 소모품은
/// 잡화 상점 몫이다 — 그래서 주사약도 여기에 둔다. 머드에 해당하는 건물이
/// 생기면 옮긴다.
///
/// 【덕코프 상점에 없는 것은 팔지 않는다】 — 해제약 넷(해독제·소화제·이완제·해빙제)과
/// 음식은 주워야 한다. 음식의 상점 표는 수집하지 못했다 [확인 불가].
/// 냉기 저항 주사약만 예외다 — 덕코프의 넷째는 공간 저항이고 Blob은 그 자리를
/// 냉기로 바꿨다. 같은 줄의 나머지 셋과 같은 값을 준다.
/// </summary>
public static class ShopTable
{
    public const string GeneralStoreName = "잡화 상점";

    private static readonly ShopEntry[] general =
    {
        // 잡화 상점 [확인됨 — 덕코프 소형 구급상자 · 구급상자 · 대형 구급상자 · 지혈 붕대 · 아스피린]
        new("con_medkit_small", 3, 1.00f),
        new("con_medkit",       2, 1.00f),
        new("con_medkit_large", 1, 1.00f),
        new("con_bandage",      3, 1.00f),
        new("con_aspirin",      2, 1.00f),

        // 머드 [확인됨 — 덕코프 노란·강화·회복·중량·화염 저항·전기 저항·독 저항 주사약, 타길라의 약품]
        new("con_stim",        3, 1.00f),
        new("con_coagulant",   3, 1.00f),
        new("con_regen",       3, 1.00f),
        new("con_expander",    3, 1.00f),
        new("con_ward_fire",   3, 1.00f),
        new("con_ward_shock",  3, 1.00f),
        new("con_ward_toxin",  3, 1.00f),
        new("con_ward_cold",   3, 1.00f),   // 덕코프에 없음 — 공간 저항 자리
        new("con_absorbent",   1, 1.00f),   // 타길라의 약품 자리 (최대 재고 1)
    };

    /// <summary>잡화 상점의 줄들.</summary>
    public static IReadOnlyList<ShopEntry> General => general;

    /// <summary>이 아이템의 줄. 팔지 않는 것이면 false.</summary>
    public static bool TryFind(string itemId, out ShopEntry entry)
    {
        for (int i = 0; i < general.Length; i++)
        {
            if (general[i].ItemId != itemId)
                continue;

            entry = general[i];
            return true;
        }

        entry = default;
        return false;
    }
}
