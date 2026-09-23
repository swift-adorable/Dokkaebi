/// <summary>
/// 잡화 상점의 재고를 들고 있는다. (로드맵 8-I · docs/Blob_Bunker_System.md 3절)
///
/// MonoBehaviour가 아니다 — 재고는 화면이 아니라 세이브에 속한다.
/// 사고파는 규칙은 TradeRules가, 무엇을 파는지는 ShopTable이 정한다.
/// 여기는 둘을 이어 주고 크레딧을 PassiveManager에서 가져올 뿐이다.
/// </summary>
public static class ShopManager
{
    private static ShopState general;

    /// <summary>
    /// 플레이 모드를 다시 들어갈 때 재고를 비운다. 도메인 리로드를 끈 설정에서는
    /// static이 이전 실행의 값을 들고 온다 — 세이브가 아닌 것이 섞이면 안 된다.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => general = null;

    /// <summary>잡화 상점의 재고. 처음 부를 때 가득 채운다.</summary>
    public static ShopState General
    {
        get
        {
            if (general != null)
                return general;

            general = new ShopState();
            general.Restock(ShopTable.General);

            return general;
        }
    }

    /// <summary>
    /// 【파밍이 끝났다】 — 재고를 채운다. (결정 2-35)
    /// 사망 저장 바로 앞에서 부른다. 철수가 생기면(9단계) 그 자리에서도 부른다.
    /// </summary>
    public static void RestockAfterRun()
    {
        General.Restock(ShopTable.General);
        GameLogger.Log("[Shop] 파밍이 끝나 잡화 상점 재고를 채웠습니다.");
    }

    /// <summary>패시브 「흥정」의 합(%).</summary>
    public static float SellBonusPercent
        => PassiveManager.HasInstance ? PassiveManager.Instance.Total(PassiveEffectType.SellPrice) : 0f;

    /// <summary>하나를 산다. 가방으로 들어온다.</summary>
    public static TradeError Buy(ItemDefinition definition)
    {
        if (definition == null || !ShopTable.TryFind(definition.Id, out ShopEntry entry))
            return TradeError.NotForSale;

        PassiveManager passive = PassiveManager.EnsureInstance();
        int credits = passive.Credits;

        TradeError error = TradeRules.Buy(General, definition, entry,
            PlayerInventory.EnsureInstance().Bag, ref credits);

        if (error == TradeError.None)
        {
            passive.Credits = credits;
            PlayerInventory.Instance.RefreshCapacity();
        }

        return error;
    }

    /// <summary>가방의 한 칸을 통째로 판다.</summary>
    public static TradeError Sell(ItemStack stack, out int earned)
    {
        PassiveManager passive = PassiveManager.EnsureInstance();
        int credits = passive.Credits;

        TradeError error = TradeRules.Sell(stack, PlayerInventory.EnsureInstance().Bag,
            SellBonusPercent, ref credits, out earned);

        if (error == TradeError.None)
        {
            passive.Credits = credits;
            PlayerInventory.Instance.RefreshCapacity();
        }

        return error;
    }

    /// <summary>세이브에서 되살린다.</summary>
    public static void Restore(System.Collections.Generic.List<SavedStock> saved)
        => General.Restore(ShopTable.General, saved);

    /// <summary>세이브를 지운 뒤 처음 상태로.</summary>
    public static void Reset() => general = null;
}
