using UnityEngine;

/// <summary>거래가 막힌 이유. 화면이 그대로 알려 준다.</summary>
public enum TradeError
{
    None = 0,

    /// <summary>거래 불가 아이템 (각인 등).</summary>
    NoTrade,

    /// <summary>가치가 0이라 팔아도 받을 것이 없다.</summary>
    Worthless,

    /// <summary>이 상점이 팔지 않는 물건.</summary>
    NotForSale,

    /// <summary>재고가 없다.</summary>
    OutOfStock,

    /// <summary>크레딧이 모자라다.</summary>
    NotEnoughCredits,

    /// <summary>가방에 자리가 없다.</summary>
    NoSpace
}

/// <summary>
/// 【사고파는 값과 규칙.】 MonoBehaviour 없는 순수 클래스다. (docs/Blob_Bunker_System.md 3절)
///
/// 【전부 아니면 전혀】 — 크레딧만 빠지고 물건이 안 들어오거나, 물건만 빠지고
/// 크레딧이 안 들어오는 경우를 만들지 않는다. 모든 검사를 먼저 하고 옮긴다.
/// </summary>
public static class TradeRules
{
    /// <summary>
    /// 파는 값 = 가치의 이 비율. 벙커 문서 3절의 「매입가 = 가치의 50%」다.
    /// 덕코프의 판매 비율은 위키에 없다 [확인 불가].
    /// </summary>
    public const float SellRatio = 0.5f;

    // ── 값 ───────────────────────────────────────────────────────────

    /// <summary>하나를 사는 값. 올림 — 1 크레딧이라도 싸게 팔지 않는다.</summary>
    public static int BuyPrice(ItemDefinition definition, ShopEntry entry)
    {
        if (definition == null)
            return 0;

        return Mathf.Max(1, Mathf.CeilToInt(definition.BaseValue * entry.PriceMultiplier));
    }

    /// <summary>
    /// 이 칸을 통째로 파는 값. 내림.
    ///
    /// 【닳은 만큼 깎는다】 — 반쯤 쓴 구급상자가 새것과 같은 값이면 쓰고 파는
    /// 것이 늘 이득이다. 덕코프가 어떻게 하는지는 위키에 없다 [확인 불가].
    /// </summary>
    /// <param name="sellBonusPercent">패시브 「흥정」의 합. 8이면 +8%.</param>
    public static int SellPrice(ItemStack stack, float sellBonusPercent)
    {
        if (stack == null || stack.IsEmpty)
            return 0;

        ItemDefinition definition = stack.Definition;

        float condition = 1f;

        if (definition.HasDurability && stack.MaxDurability > 0)
            condition = Mathf.Clamp01(stack.Durability / (float)stack.MaxDurability);

        float bonus = 1f + Mathf.Max(0f, sellBonusPercent) * 0.01f;

        int unit = Mathf.FloorToInt(definition.BaseValue * SellRatio * bonus * condition);

        return Mathf.Max(0, unit) * stack.Count;
    }

    // ── 검사 ─────────────────────────────────────────────────────────

    public static TradeError CanSell(ItemStack stack, float sellBonusPercent)
    {
        if (stack == null || stack.IsEmpty)
            return TradeError.Worthless;

        if (stack.Definition.NoTrade)
            return TradeError.NoTrade;

        return SellPrice(stack, sellBonusPercent) <= 0 ? TradeError.Worthless : TradeError.None;
    }

    public static TradeError CanBuy(
        ItemDefinition definition, ShopEntry entry, int remaining, int credits, Inventory bag)
    {
        if (definition == null || definition.Id != entry.ItemId)
            return TradeError.NotForSale;

        if (remaining <= 0)
            return TradeError.OutOfStock;

        if (credits < BuyPrice(definition, entry))
            return TradeError.NotEnoughCredits;

        if (bag == null || !bag.CanAdd(definition))
            return TradeError.NoSpace;

        return TradeError.None;
    }

    // ── 실행 ─────────────────────────────────────────────────────────

    /// <summary>하나를 산다. 성공하면 크레딧이 줄고 재고가 하나 빠진다.</summary>
    public static TradeError Buy(
        ShopState shop, ItemDefinition definition, ShopEntry entry, Inventory bag, ref int credits)
    {
        if (shop == null)
            return TradeError.NotForSale;

        TradeError error = CanBuy(definition, entry, shop.Remaining(entry.ItemId), credits, bag);

        if (error != TradeError.None)
            return error;

        // CanAdd가 자리를 보장했다. 그래도 어긋나면 아무것도 빼지 않는다.
        if (bag.TryAdd(definition) != 1)
            return TradeError.NoSpace;

        shop.TryConsume(entry.ItemId);
        credits -= BuyPrice(definition, entry);

        return TradeError.None;
    }

    /// <summary>칸을 통째로 판다. 성공하면 가방에서 빠지고 크레딧이 는다.</summary>
    public static TradeError Sell(
        ItemStack stack, Inventory bag, float sellBonusPercent, ref int credits, out int earned)
    {
        earned = 0;

        TradeError error = CanSell(stack, sellBonusPercent);

        if (error != TradeError.None)
            return error;

        // 값은 빼기 전에 잰다 — 빼고 나면 개수가 남아 있지 않을 수 있다.
        int price = SellPrice(stack, sellBonusPercent);

        if (bag == null || !bag.RemoveStack(stack))
            return TradeError.NotForSale;

        earned = price;
        credits += price;

        return TradeError.None;
    }

    /// <summary>화면에 띄울 한 줄.</summary>
    public static string Explain(TradeError error)
    {
        switch (error)
        {
            case TradeError.NoTrade:          return "거래할 수 없는 물건입니다.";
            case TradeError.Worthless:        return "값이 나가지 않는 물건입니다.";
            case TradeError.NotForSale:       return "이 상점은 팔지 않습니다.";
            case TradeError.OutOfStock:       return "재고가 없습니다. 다음 파밍이 끝나면 채워집니다.";
            case TradeError.NotEnoughCredits: return "크레딧이 모자랍니다.";
            case TradeError.NoSpace:          return "가방에 자리가 없습니다.";
            default:                          return string.Empty;
        }
    }
}
