using System;
using System.Collections.Generic;

/// <summary>
/// 약탕간의 두 일 (결정 2-97) — 【약 달이기】와 【샘가 재료】. 값은 전부 [임시].
///
///   · 달이기 — 약재(bio_sample)를 주재료로 약을 달인다. 가게에 없는 약 + 기본 약 (사용자 결정 2026-10-08).
///     재료는 창고 먼저, 모자라면 가방에서. 만든 것은 창고로, 자리가 없으면 가방으로 (작업대와 같다).
///   · 샘가 — 약탕간을 지어 두면 【판마다(밤마다)】 샘가에 약재가 모인다. 가끔 열린 장의 장 재료 하나.
///     약탕간 화면에서 거둔다. 쌓이는 데 끝이 있다 — 오래 비워 두면 더 모이지 않는다.
/// </summary>
public static class ApothecaryTable
{
    public const string PotName = "약탕";
    public const string SpringsideName = "샘가";

    public const string Herb = "bio_sample";

    // ── 달이기 ───────────────────────────────────────────────────────

    private static readonly CraftRecipe[] brews =
    {
        // 기본 약 — 가게에도 있다
        new("con_bandage",      2, 0, new[] { new MaterialCost(Herb, 1) }, 0),
        new("con_medkit_small", 1, 0, new[] { new MaterialCost(Herb, 2), new MaterialCost("food_rice", 1) }, 0),
        new("con_aspirin",      1, 0, new[] { new MaterialCost(Herb, 2), new MaterialCost("food_jujube", 1), new MaterialCost("con_water", 1) }, 0),

        // 가게에 없는 약
        new("con_defroster",    2, 0, new[] { new MaterialCost(Herb, 1), new MaterialCost("cell_battery", 1) }, 0),
        new("con_antidote",     1, 0, new[] { new MaterialCost(Herb, 1), new MaterialCost("food_bean", 1), new MaterialCost("con_water", 1) }, 0),
        new("con_antacid",      1, 0, new[] { new MaterialCost(Herb, 2) }, 0),
        new("con_relaxant",     1, 0, new[] { new MaterialCost(Herb, 2), new MaterialCost("food_ginseng", 1) }, 0),
    };

    public static IReadOnlyList<CraftRecipe> Brews => brews;

    /// <summary>
    /// 달인다. 약탕간이 없으면 NoWorkbench(→ 「약탕간을 먼저 지어야 한다」).
    /// 재료가 모자라면 MissingMaterial, 놓을 자리가 없으면 NoRoom — 그때는 아무것도 빼지 않는다.
    /// </summary>
    public static CraftError Brew(CraftRecipe recipe, bool built, Func<string, int> countOf,
                                  Func<string, int, int> remove, Func<string, int, bool> canGive, Action<string, int> give)
    {
        CraftError error = CanBrew(recipe, built, countOf);
        if (error != CraftError.None)
            return error;

        if (canGive != null && !canGive(recipe.OutputId, recipe.OutputCount))
            return CraftError.NoRoom;

        foreach (MaterialCost c in recipe.Inputs)
            remove?.Invoke(c.ItemId, c.Count);

        give?.Invoke(recipe.OutputId, recipe.OutputCount);
        return CraftError.None;
    }

    public static CraftError CanBrew(CraftRecipe recipe, bool built, Func<string, int> countOf)
    {
        if (!built)
            return CraftError.NoWorkbench;

        if (recipe == null)
            return CraftError.MissingMaterial;

        foreach (MaterialCost c in recipe.Inputs)
            if (countOf == null || countOf(c.ItemId) < c.Count)
                return CraftError.MissingMaterial;

        return CraftError.None;
    }

    public static string Explain(CraftError error)
        => error == CraftError.NoWorkbench ? "약탕간을 먼저 지어야 한다." : WorkbenchState.Explain(error);

    // ── 샘가 ─────────────────────────────────────────────────────────

    /// <summary>한 밤에 모이는 약재.</summary>
    public const int HerbsPerNight = 2;

    /// <summary>약재가 쌓이는 끝.</summary>
    public const int HerbCap = 8;

    /// <summary>한 밤에 장 재료 하나가 모일 확률.</summary>
    public const double ChapterChance = 0.25;

    /// <summary>장 재료가 쌓이는 끝(종류 합).</summary>
    public const int ChapterCap = 4;

    /// <summary>열린 장(1장부터)의 장 재료 id 전부 — 샘가에 모일 수 있는 것.</summary>
    public static List<string> ChapterPool(Func<int, bool> isChapterOpen)
    {
        var pool = new List<string>();

        for (int chapter = 1; chapter <= StoryTable.LastChapter; chapter++)
        {
            if (isChapterOpen == null || !isChapterOpen(chapter))
                continue;

            foreach (Ingredient i in IngredientTable.ChapterOnly(chapter))
                if (!pool.Contains(i.Id))
                    pool.Add(i.Id);
        }

        return pool;
    }
}

/// <summary>샘가에 모인 것 (결정 2-97). MonoBehaviour 의존이 없다 — EditMode 테스트 대상.</summary>
public sealed class SpringsideState
{
    private readonly List<MaterialCost> pending = new();

    public IReadOnlyList<MaterialCost> Pending => pending;

    public bool IsEmpty => pending.Count == 0;

    public int CountOf(string id)
    {
        int total = 0;
        foreach (MaterialCost c in pending)
            if (c.ItemId == id) total += c.Count;
        return total;
    }

    private int ChapterTotal
    {
        get
        {
            int total = 0;
            foreach (MaterialCost c in pending)
                if (c.ItemId != ApothecaryTable.Herb) total += c.Count;
            return total;
        }
    }

    /// <summary>한 밤이 지났다. 약탕간이 없으면 아무것도 모이지 않는다.</summary>
    public void AccrueNight(bool built, IReadOnlyList<string> chapterPool, Random random)
    {
        if (!built)
            return;

        random ??= new Random(Environment.TickCount);

        int herbs = Math.Min(ApothecaryTable.HerbsPerNight, ApothecaryTable.HerbCap - CountOf(ApothecaryTable.Herb));
        if (herbs > 0)
            Add(ApothecaryTable.Herb, herbs);

        if (chapterPool != null && chapterPool.Count > 0 && ChapterTotal < ApothecaryTable.ChapterCap
            && random.NextDouble() < ApothecaryTable.ChapterChance)
            Add(chapterPool[random.Next(chapterPool.Count)], 1);
    }

    /// <summary>다 거둔다 — 거둔 것을 돌려주고 비운다.</summary>
    public List<MaterialCost> TakeAll()
    {
        var taken = new List<MaterialCost>(pending);
        pending.Clear();
        return taken;
    }

    private void Add(string id, int count)
    {
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].ItemId != id)
                continue;

            pending[i] = new MaterialCost(id, pending[i].Count + count);
            return;
        }

        pending.Add(new MaterialCost(id, count));
    }

    public List<SavedItem> Capture()
    {
        var list = new List<SavedItem>();
        foreach (MaterialCost c in pending)
            list.Add(new SavedItem { id = c.ItemId, count = c.Count });
        return list;
    }

    public void Restore(List<SavedItem> saved)
    {
        pending.Clear();

        if (saved == null)
            return;

        foreach (SavedItem s in saved)
            if (s != null && !s.IsEmpty)
                Add(s.id, s.count);
    }

    public void Reset() => pending.Clear();
}
