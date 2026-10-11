using System.Collections.Generic;

/// <summary>
/// 상점들의 재고를 들고 있는다. (로드맵 8-I · 8-K · docs/Dokkaebi_Bunker_System.md 3절)
///
/// MonoBehaviour가 아니다 — 재고는 화면이 아니라 세이브에 속한다.
/// 사고파는 규칙은 TradeRules가, 무엇을 파는지는 ShopTable이 정한다.
/// 여기는 둘을 이어 주고 골드를 PassiveManager에서 가져올 뿐이다.
/// </summary>
public static class ShopManager
{
    private static Dictionary<ShopKind, ShopState> states;

    /// <summary>가게마다 오늘의 물건 (결정 2-98).</summary>
    private static Dictionary<ShopKind, List<string>> dailies;

    /// <summary>
    /// 플레이 모드를 다시 들어갈 때 재고를 비운다. 도메인 리로드를 끈 설정에서는
    /// static이 이전 실행의 값을 들고 온다 — 세이브가 아닌 것이 섞이면 안 된다.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        states = null;
        dailies = null;
    }

    /// <summary>그 상점의 재고. 처음 부를 때 가득 채운다.</summary>
    public static ShopState Of(ShopKind kind)
    {
        states ??= new Dictionary<ShopKind, ShopState>();

        if (states.TryGetValue(kind, out ShopState state))
            return state;

        state = new ShopState();
        states[kind] = state;
        state.Restock(EntriesOf(kind), ExtraStock);

        return state;
    }

    /// <summary>잡화 가게의 재고.</summary>
    public static ShopState General => Of(ShopKind.General);

    /// <summary>
    /// 【파밍이 끝났다】 — 모든 상점의 재고를 채운다. (결정 2-35)
    /// 사망 저장 · 철수 저장 바로 앞에서 부른다.
    /// </summary>
    public static void RestockAfterRun()
    {
        foreach (ShopKind kind in ShopTable.All)
        {
            RollDaily(kind, null);
            Of(kind).Restock(EntriesOf(kind), ExtraStock);
        }

        GameLogger.Log("[Shop] 파밍이 끝나 상점 재고를 채웠습니다.");
    }

    /// <summary>패시브 「재고 +n」의 합 — 가게 물건마다 n개씩 더 채운다 (Audit A9 · 결정 2-74 — 옛 「상점 갱신 횟수」).</summary>
    public static int ExtraStock
        => PassiveManager.HasInstance ? UnityEngine.Mathf.RoundToInt(PassiveManager.Instance.Total(PassiveEffectType.ShopSlots)) : 0;

    /// <summary>패시브 「값 깎기 −n%」의 합 — 가게에서 사는 값 (Audit A9 · 결정 2-74 — 옛 「상점 갱신 쿨다운」).</summary>
    public static float BuyDiscountPercent
        => PassiveManager.HasInstance ? PassiveManager.Instance.Total(PassiveEffectType.ShopRefresh) : 0f;

    /// <summary>패시브 「흥정」의 합(%).</summary>
    public static float SellBonusPercent
        => PassiveManager.HasInstance ? PassiveManager.Instance.Total(PassiveEffectType.SellPrice) : 0f;

    /// <summary>하나를 산다. 가방으로 들어온다.</summary>
    public static TradeError Buy(ShopKind kind, ItemDefinition definition)
    {
        if (definition == null || !TryFindEntry(kind, definition.Id, out ShopEntry entry))
            return TradeError.NotForSale;

        PassiveManager passive = PassiveManager.EnsureInstance();
        int gold = passive.Gold;

        TradeError error = TradeRules.Buy(Of(kind), definition, entry,
            PlayerInventory.EnsureInstance().Bag, ref gold, BuyDiscountPercent);

        if (error == TradeError.None)
        {
            passive.Gold = gold;
            PlayerInventory.Instance.RefreshCapacity();
        }

        return error;
    }

    /// <summary>가방의 한 칸을 통째로 판다. 어느 상점에서 팔든 값은 같다.</summary>
    public static TradeError Sell(ItemStack stack, out int earned)
        => Sell(stack, stack != null ? stack.Count : 0, out earned);

    /// <summary>가방 칸에서 n개를 판다 (결정 2-77 — 수량 고르기).</summary>
    public static TradeError Sell(ItemStack stack, int count, out int earned)
    {
        PassiveManager passive = PassiveManager.EnsureInstance();
        int gold = passive.Gold;

        TradeError error = QuantityTransfer.Sell(stack, count, PlayerInventory.EnsureInstance().Bag,
            SellBonusPercent, ref gold, out earned);

        if (error == TradeError.None)
        {
            passive.Gold = gold;
            PlayerInventory.Instance.RefreshCapacity();
        }

        return error;
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    /// <summary>모든 상점의 재고. 줄마다 어느 상점인지 적는다.</summary>
    public static List<SavedStock> Capture()
    {
        var rows = new List<SavedStock>();

        foreach (ShopKind kind in ShopTable.All)
        {
            foreach (SavedStock row in Of(kind).Capture(EntriesOf(kind)))
            {
                row.shop = kind.ToString();
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>
    /// 세이브에서 되살린다. 【상점 이름이 비어 있는 줄은 잡화 가게다】 — 판 5~7에는 잡화 상점 하나뿐이었다.
    /// 판 8~9의 「Weapon」 · 「Armour」는 대장간으로 읽는다 (결정 2-52). 그 판의 잡화 상점 약 줄은
    /// 잡화 가게 표에 없어 버려지고 약탕간은 가득 찬 재고로 시작한다.
    /// </summary>
    public static void Restore(List<SavedStock> saved)
    {
        foreach (ShopKind kind in ShopTable.All)
        {
            var rows = new List<SavedStock>();

            if (saved != null)
            {
                foreach (SavedStock row in saved)
                {
                    if (row != null && KindOf(row) == kind)
                        rows.Add(row);
                }
            }

            Of(kind).Restore(EntriesOf(kind), rows, ExtraStock);
        }
    }

    private static ShopKind KindOf(SavedStock row)
    {
        if (string.IsNullOrEmpty(row.shop))
            return ShopKind.General;

        if (row.shop == "Weapon" || row.shop == "Armour")
            return ShopKind.Smithy;

        return System.Enum.TryParse(row.shop, out ShopKind kind) ? kind : ShopKind.General;
    }

    /// <summary>세이브를 지운 뒤 처음 상태로.</summary>
    public static void Reset()
    {
        states = null;
        dailies = null;
    }

    // ── 오늘의 물건 (결정 2-98) ──────────────────────────────────────

    /// <summary>그 가게가 지금 파는 줄 — 평소 표 + 오늘의 물건.</summary>
    public static List<ShopEntry> EntriesOf(ShopKind kind)
    {
        var list = new List<ShopEntry>(ShopTable.For(kind));

        foreach (string id in DailyOf(kind))
            if (ShopDailyTable.TryFind(kind, id, out ShopEntry entry))
                list.Add(entry);

        return list;
    }

    /// <summary>그 가게의 오늘의 물건 id. 처음 부를 때 뽑는다.</summary>
    public static IReadOnlyList<string> DailyOf(ShopKind kind)
    {
        dailies ??= new Dictionary<ShopKind, List<string>>();

        if (!dailies.TryGetValue(kind, out List<string> list))
        {
            list = RollFor(kind, null);
            dailies[kind] = list;
        }

        return list;
    }

    public static bool IsDaily(ShopKind kind, string itemId)
    {
        foreach (string id in DailyOf(kind))
            if (id == itemId) return true;
        return false;
    }

    public static bool TryFindEntry(ShopKind kind, string itemId, out ShopEntry entry)
    {
        if (ShopTable.TryFind(kind, itemId, out entry))
            return true;

        if (IsDaily(kind, itemId) && ShopDailyTable.TryFind(kind, itemId, out entry))
            return true;

        entry = default;
        return false;
    }

    /// <summary>지금 재고를 가득 채운다 (장부방 — 값은 LedgerManager가 받는다).</summary>
    public static void RefillNow(ShopKind kind) => Of(kind).Restock(EntriesOf(kind), ExtraStock);

    /// <summary>오늘의 물건을 다시 뽑는다 — 새 물건은 가득, 평소 표의 남은 수는 그대로 (장부방).</summary>
    public static void RerollDaily(ShopKind kind)
    {
        var old = new List<string>(DailyOf(kind));
        RollDaily(kind, old);
        Of(kind).Refit(EntriesOf(kind), ExtraStock, new List<string>(DailyOf(kind)));
    }

    private static void RollDaily(ShopKind kind, ICollection<string> avoid)
    {
        dailies ??= new Dictionary<ShopKind, List<string>>();
        dailies[kind] = RollFor(kind, avoid);
    }

    private static List<string> RollFor(ShopKind kind, ICollection<string> avoid)
    {
        StoryProgress progress = StoryManager.Progress;
        List<ShopEntry> pool = ShopDailyTable.Pool(kind, c => progress != null && progress.IsChapterOpen(c));
        return ShopDailyTable.Roll(pool, ShopDailyTable.Slots, null, avoid);
    }

    /// <summary>오늘의 물건 — 세이브 줄 (shop · id).</summary>
    public static List<SavedStock> CaptureDaily()
    {
        var rows = new List<SavedStock>();

        foreach (ShopKind kind in ShopTable.All)
            foreach (string id in DailyOf(kind))
                rows.Add(new SavedStock { shop = kind.ToString(), id = id });

        return rows;
    }

    /// <summary>
    /// 오늘의 물건을 되살린다 — 재고(Restore)보다 먼저 부른다. 19판 이하(빈 목록)는 새로 뽑는다.
    /// (JsonUtility는 없는 목록을 null이 아니라 빈 목록으로 읽는다 — 셋 다 비어 있어도 다시 뽑아 같은 결과다.)
    /// 풀에 없는 id는 버린다.
    /// </summary>
    public static void RestoreDaily(List<SavedStock> saved)
    {
        dailies = new Dictionary<ShopKind, List<string>>();

        if (saved == null || saved.Count == 0)
            return;   // 처음 부를 때 뽑는다

        foreach (ShopKind kind in ShopTable.All)
            dailies[kind] = new List<string>();

        foreach (SavedStock row in saved)
        {
            if (row == null || string.IsNullOrEmpty(row.id) || !System.Enum.TryParse(row.shop, out ShopKind kind))
                continue;

            if (dailies.TryGetValue(kind, out List<string> list) && !list.Contains(row.id)
                && ShopDailyTable.TryFind(kind, row.id, out _))
                list.Add(row.id);
        }
    }
}
