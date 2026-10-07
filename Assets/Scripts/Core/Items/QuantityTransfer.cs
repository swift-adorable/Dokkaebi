/// <summary>
/// 【수량을 골라 옮긴다】 (결정 2-77) — 창고 · 가방 · 전리품 · 상점에서 겹치는 물건은 몇 개를 옮길지 고른다.
/// 전부를 고르면 예전처럼 칸째 옮긴다. MonoBehaviour 없는 순수 정적 클래스 — EditMode 테스트 대상.
/// </summary>
public static class QuantityTransfer
{
    /// <summary>인벤토리 → 인벤토리로 n개. 자리가 없으면 아무것도 옮기지 않는다.</summary>
    public static bool Move(Inventory from, Inventory to, ItemStack stack, int count)
    {
        if (from == null || to == null || stack == null || stack.IsEmpty || count <= 0)
            return false;

        if (count >= stack.Count)
            return Inventory.MoveStack(from, to, stack);

        if (!to.CanAdd(stack.Definition, count))
            return false;

        stack.Take(count);
        return to.TryAdd(stack.Definition, count) == count;
    }

    /// <summary>전리품 칸 → 가방으로 n개.</summary>
    public static bool Take(LootContainer box, int index, Inventory bag, int count)
    {
        ItemStack stack = box?.Get(index);

        if (stack == null || bag == null || count <= 0)
            return false;

        if (count >= stack.Count)
            return box.TryTakeTo(index, bag);

        if (!bag.CanAdd(stack.Definition, count))
            return false;

        stack.Take(count);
        return bag.TryAdd(stack.Definition, count) == count;
    }

    /// <summary>n개를 팔면 받는 값 — 한 개 값 × n (닳은 물건은 겹치지 않으므로 칸째 판다).</summary>
    public static int SellPrice(ItemStack stack, int count, float sellBonusPercent)
    {
        if (stack == null || stack.IsEmpty || count <= 0)
            return 0;

        if (count >= stack.Count)
            return TradeRules.SellPrice(stack, sellBonusPercent);

        return TradeRules.SellPrice(new ItemStack(stack.Definition, 1, stack.Durability), sellBonusPercent) * count;
    }

    /// <summary>가방의 n개를 판다.</summary>
    public static TradeError Sell(ItemStack stack, int count, Inventory bag, float sellBonusPercent,
                                  ref int gold, out int earned)
    {
        earned = 0;

        if (count <= 0 || stack == null)
            return TradeError.Worthless;

        if (count >= stack.Count)
            return TradeRules.Sell(stack, bag, sellBonusPercent, ref gold, out earned);

        TradeError error = TradeRules.CanSell(stack, sellBonusPercent);
        if (error != TradeError.None)
            return error;

        earned = SellPrice(stack, count, sellBonusPercent);
        stack.Take(count);
        gold += earned;
        return TradeError.None;
    }
}
