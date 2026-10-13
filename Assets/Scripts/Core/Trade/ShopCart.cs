using System;
using System.Collections.Generic;

/// <summary>담아 둔 한 줄 — 무엇을 몇 개.</summary>
public readonly struct CartLine
{
    public readonly string ItemId;
    public readonly int Count;

    public CartLine(string itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }
}

/// <summary>
/// 【가게의 담아 두기】 (결정 2-120) — 여러 물건을 담아 두고 합계를 본 뒤 한 번에 산다.
/// 가게 창이 떠 있는 동안만 산다 — 창을 닫으면 비운다. 세이브에 남지 않는다.
/// 같은 물건은 한 줄로 합친다. MonoBehaviour 없는 순수 클래스다.
/// </summary>
public sealed class ShopCart
{
    private readonly List<CartLine> lines = new();

    public IReadOnlyList<CartLine> Lines => lines;

    public bool IsEmpty => lines.Count == 0;

    /// <summary>담은 개수. 없으면 0.</summary>
    public int CountOf(string itemId)
    {
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].ItemId == itemId)
                return lines[i].Count;

        return 0;
    }

    /// <summary>개수를 정한다. 0 이하면 줄을 뺀다. 있던 줄은 자리를 지킨다.</summary>
    public void Set(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].ItemId != itemId)
                continue;

            if (count <= 0)
                lines.RemoveAt(i);
            else
                lines[i] = new CartLine(itemId, count);

            return;
        }

        if (count > 0)
            lines.Add(new CartLine(itemId, count));
    }

    public void Add(string itemId, int count) => Set(itemId, CountOf(itemId) + Math.Max(0, count));

    public void Remove(string itemId) => Set(itemId, 0);

    public void Clear() => lines.Clear();
}

/// <summary>값을 매길 수 있게 풀어 둔 한 줄 — 정의 · 가게 줄 · 개수.</summary>
public readonly struct CartItem
{
    public readonly ItemDefinition Definition;
    public readonly ShopEntry Entry;
    public readonly int Count;

    public CartItem(ItemDefinition definition, ShopEntry entry, int count)
    {
        Definition = definition;
        Entry = entry;
        Count = count;
    }
}

/// <summary>
/// 【한꺼번에 사기의 규칙】 (결정 2-120) — TradeRules와 같은 「전부 아니면 전혀」.
/// 재고 · 엽전 · 가방 칸을 줄 전부에 대해 먼저 재고, 하나라도 막히면 아무것도 사지 않는다.
/// </summary>
public static class CartRules
{
    /// <summary>합계 — 줄마다 하나 값 × 개수.</summary>
    public static int Total(IReadOnlyList<CartItem> items, float discountPercent = 0f)
    {
        int total = 0;

        if (items == null)
            return 0;

        for (int i = 0; i < items.Count; i++)
            total += TradeRules.BuyPrice(items[i].Definition, items[i].Entry, discountPercent) * Math.Max(0, items[i].Count);

        return total;
    }

    /// <summary>줄 전부를 담으려면 새로 드는 가방 칸.</summary>
    public static int SlotsNeeded(IReadOnlyList<CartItem> items, Inventory bag)
    {
        int slots = 0;

        if (items == null || bag == null)
            return 0;

        for (int i = 0; i < items.Count; i++)
            slots += bag.SlotsNeeded(items[i].Definition, items[i].Count);

        return slots;
    }

    /// <summary>
    /// 살 수 있는가. 막히면 그 이유와, 줄 탓이면(재고 · 팔지 않음) 그 줄 번호를 준다(아니면 −1).
    /// </summary>
    public static TradeError CanBuy(IReadOnlyList<CartItem> items, Func<string, int> remaining, int gold,
                                    Inventory bag, float discountPercent, out int failedIndex)
    {
        failedIndex = -1;

        if (items == null || items.Count == 0)
            return TradeError.EmptyCart;

        for (int i = 0; i < items.Count; i++)
        {
            CartItem item = items[i];

            if (item.Definition == null || item.Definition.Id != item.Entry.ItemId || item.Count <= 0)
            {
                failedIndex = i;
                return TradeError.NotForSale;
            }

            if (remaining == null || item.Count > remaining(item.Entry.ItemId))
            {
                failedIndex = i;
                return TradeError.OutOfStock;
            }
        }

        if (Total(items, discountPercent) > gold)
            return TradeError.NotEnoughGold;

        if (bag == null || SlotsNeeded(items, bag) > bag.FreeSlots)
            return TradeError.NoSpace;

        return TradeError.None;
    }

    /// <summary>산다. 성공하면 엽전이 합계만큼 줄고 줄마다 재고가 빠진다.</summary>
    public static TradeError Buy(ShopState shop, IReadOnlyList<CartItem> items, Inventory bag, ref int gold,
                                 float discountPercent, out int failedIndex)
    {
        failedIndex = -1;

        if (shop == null)
            return TradeError.NotForSale;

        TradeError error = CanBuy(items, shop.Remaining, gold, bag, discountPercent, out failedIndex);
        if (error != TradeError.None)
            return error;

        // 위에서 전부 쟀다 — 하나씩 사도 막히지 않는다.
        for (int i = 0; i < items.Count; i++)
        {
            for (int n = 0; n < items[i].Count; n++)
            {
                error = TradeRules.Buy(shop, items[i].Definition, items[i].Entry, bag, ref gold, discountPercent);
                if (error != TradeError.None)
                {
                    failedIndex = i;
                    return error;
                }
            }
        }

        return TradeError.None;
    }

    /// <summary>
    /// 단추 아래 이유 한 줄 — 얼마나 모자란지까지 적는다.
    /// </summary>
    /// <param name="itemName">줄 탓일 때 그 물건 이름.</param>
    public static string Explain(TradeError error, int goldShort, int slotsShort, string itemName)
    {
        switch (error)
        {
            case TradeError.None:
                return string.Empty;
            case TradeError.NotEnoughGold:
                return goldShort > 0 ? $"엽전이 {goldShort:N0} 모자랍니다." : TradeRules.Explain(error);
            case TradeError.NoSpace:
                return slotsShort > 0 ? $"가방 자리가 {slotsShort}칸 모자랍니다." : TradeRules.Explain(error);
            case TradeError.OutOfStock:
                return string.IsNullOrEmpty(itemName) ? TradeRules.Explain(error) : $"「{itemName}」 재고가 모자랍니다.";
            case TradeError.NotForSale:
                return string.IsNullOrEmpty(itemName) ? TradeRules.Explain(error) : $"「{itemName}」 — 이 가게는 팔지 않습니다.";
            default:
                return TradeRules.Explain(error);
        }
    }
}
