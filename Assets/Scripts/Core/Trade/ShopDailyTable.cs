using System;
using System.Collections.Generic;

/// <summary>
/// 【오늘의 물건】 (결정 2-98) — 가게마다 평소 표 말고 3칸이 더 있다. 판이 끝날 때마다 풀에서 새로 뽑힌다.
/// 풀은 【평소엔 안 파는 것】이고 값은 ×1.5 — 주워 오거나 만드는 것보다 비싸야 한다. 값은 전부 [임시].
///
///   · 잡화 가게 — 요리 재료(공용 · 산열매 제외) + 열린 장의 장 재료
///   · 약탕간   — 약재 + 달이는 약 넷(생강 · 감두탕 · 황련 · 우황청심원)
///   · 대장간   — 덕코프 무기 상점처럼 진행으로 열린다: 3장이 열리면 방어 관통탄 7, 5장이 열리면 무기 도면 7
///
/// 장부방에서 엽전을 내면 ① 그 가게 재고를 지금 가득 채우고 ② 오늘의 물건을 다시 뽑는다.
/// MonoBehaviour 의존이 없다 — EditMode 테스트 대상.
/// </summary>
public static class ShopDailyTable
{
    public const int Slots = 3;
    public const float PriceMultiplier = 1.5f;

    /// <summary>방어 관통탄이 풀에 드는 장 — 시체에서 나오기 시작하는 때와 맞춘다.</summary>
    public const int PiercingChapter = 3;

    /// <summary>무기 도면이 풀에 드는 장.</summary>
    public const int BlueprintChapter = 5;

    private const int IngredientStock = 3;
    private const int HerbStock = 5;
    private const int MedicineStock = 2;
    private const int PiercingStock = 30;

    private static readonly string[] Medicines = { "con_defroster", "con_antidote", "con_antacid", "con_relaxant" };

    /// <summary>지금 진행에서 그 가게의 풀.</summary>
    public static List<ShopEntry> Pool(ShopKind kind, Func<int, bool> isChapterOpen)
    {
        var pool = new List<ShopEntry>();
        bool Open(int c) => isChapterOpen != null && isChapterOpen(c);

        switch (kind)
        {
            case ShopKind.Apothecary:
                pool.Add(Entry(ApothecaryTable.Herb, HerbStock));
                foreach (string id in Medicines)
                    pool.Add(Entry(id, MedicineStock));
                break;

            case ShopKind.Smithy:
                if (Open(PiercingChapter))
                    foreach (AmmoInfo a in AmmoTable.All)
                        if (a.PenetrationBonus > 0)
                            pool.Add(Entry(a.Id, PiercingStock));

                if (Open(BlueprintChapter))
                    foreach (WeaponKind w in WorkbenchTable.BlueprintKinds)
                        pool.Add(Entry(WorkbenchTable.BlueprintOf(w), 1));
                break;

            default:
                foreach (Ingredient i in IngredientTable.All)
                    if (i.IsCommon && i.Id != IngredientTable.Berry)
                        pool.Add(Entry(i.Id, IngredientStock));

                foreach (string id in ApothecaryTable.ChapterPool(Open))
                    if (!Contains(pool, id))
                        pool.Add(Entry(id, IngredientStock));
                break;
        }

        // 평소 표에 있는 것은 빼낸다 — 「평소엔 안 파는 것」
        pool.RemoveAll(e => ShopTable.TryFind(kind, e.ItemId, out _));
        return pool;
    }

    /// <summary>진행과 상관없이 그 가게 풀의 한 줄 — 세이브에서 되살릴 때 쓴다. 없으면 false.</summary>
    public static bool TryFind(ShopKind kind, string itemId, out ShopEntry entry)
    {
        foreach (ShopEntry e in Pool(kind, _ => true))
        {
            if (e.ItemId != itemId)
                continue;

            entry = e;
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// 풀에서 count개를 겹치지 않게 뽑는다. avoid에 든 것은 풀이 넉넉하면 피한다(다시 뽑기가 같은 것을 내지 않게).
    /// </summary>
    public static List<string> Roll(IReadOnlyList<ShopEntry> pool, int count, Random random, ICollection<string> avoid = null)
    {
        random ??= new Random(Environment.TickCount);
        var picks = new List<string>();

        if (pool == null || pool.Count == 0 || count <= 0)
            return picks;

        var candidates = new List<string>();
        foreach (ShopEntry e in pool)
            if (avoid == null || !avoid.Contains(e.ItemId))
                candidates.Add(e.ItemId);

        // 피할 것을 빼고 모자라면 피할 것도 다시 넣는다
        if (candidates.Count < count)
            foreach (ShopEntry e in pool)
                if (!candidates.Contains(e.ItemId))
                    candidates.Add(e.ItemId);

        while (picks.Count < count && candidates.Count > 0)
        {
            int i = random.Next(candidates.Count);
            picks.Add(candidates[i]);
            candidates.RemoveAt(i);
        }

        return picks;
    }

    /// <summary>장부방 「지금 채우기」 값.</summary>
    public static int RefillPrice(ShopKind kind) => kind switch
    {
        ShopKind.Smithy => 200,
        ShopKind.Apothecary => 150,
        _ => 100,
    };

    /// <summary>장부방 「오늘의 물건 다시 뽑기」 값.</summary>
    public const int RerollPrice = 50;

    private static ShopEntry Entry(string id, int stock) => new(id, stock, PriceMultiplier);

    private static bool Contains(List<ShopEntry> list, string id)
    {
        foreach (ShopEntry e in list)
            if (e.ItemId == id) return true;
        return false;
    }
}
