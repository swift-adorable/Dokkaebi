using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 【어디서 얻나 · 어디에 쓰나】 (결정 2-120) — 아이템 상세의 두 줄. 표를 거꾸로 훑어 모은다.
///
///   얻는 곳 — 가게 평소 표 · 오늘의 물건 · 부뚜막 · 약탕 · 샘가 · 작업대 · 샘 · 열매 나무 · 시체(전리품 표) · 무기 도면 · 보스 고유 장비
///   쓰는 곳 — 부뚜막 · 약탕 재료 · 작업대 재료 · 견본 · 단계 올리기 · 건물 짓기
///
/// 새 글을 지어내지 않는다 — 이름은 전부 표 · 카탈로그에 이미 있는 것이다. MonoBehaviour 없는 순수 클래스다.
/// </summary>
public static class ItemSources
{
    /// <summary>한 칸에 이름을 몇 개까지 적나 — 넘치면 「외 n」.</summary>
    public const int MaxNames = 3;

    public const string BlueprintSource = "큰 요괴 · 보스 시체 (드물게)";

    /// <summary>얻는 곳.</summary>
    /// <param name="inCorpseLoot">시체 전리품 표에 드는가 (카탈로그가 안다).</param>
    public static List<string> Sources(string id, Func<string, bool> inCorpseLoot = null)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(id))
            return list;

        // 가게 — 평소 표, 그다음 오늘의 물건(진행과 상관없이 풀 전부)
        foreach (ShopKind kind in ShopTable.All)
            if (ShopTable.TryFind(kind, id, out _))
                Add(list, ShopTable.NameOf(kind));

        foreach (ShopKind kind in ShopTable.All)
            if (!ShopTable.TryFind(kind, id, out _) && InDailyPool(kind, id))
                Add(list, $"{ShopTable.NameOf(kind)} 오늘의 물건");

        // 만들기
        if (CookingTable.ForOutput(id) != null)
            Add(list, $"{CookingTable.HearthName} (요리)");

        foreach (CraftRecipe r in ApothecaryTable.Brews)
            if (r.OutputId == id)
                Add(list, $"{ApothecaryTable.PotName} (달이기)");

        CraftRecipe made = WorkbenchTable.Find(id);
        if (made != null)
            Add(list, $"작업대 {WorkbenchTable.StageName(made.Stage)}");

        // 저절로 모이는 것
        if (id == ApothecaryTable.Herb || ApothecaryTable.ChapterPool(_ => true).Contains(id))
            Add(list, $"{ApothecaryTable.SpringsideName} (약탕간)");

        if (id == SpringTable.WaterId)
            Add(list, SpringTable.Name);

        if (id == IngredientTable.Berry)
            Add(list, BerryTree.Name);

        // 시체
        if (inCorpseLoot != null && inCorpseLoot(id))
            Add(list, CorpseWhere(id));

        if (id.StartsWith(WorkbenchTable.BlueprintPrefix, StringComparison.Ordinal))
            Add(list, BlueprintSource);

        foreach (KeyValuePair<string, string> pair in BossUniqueTable.Items)
        {
            if (pair.Value != id)
                continue;

            BossDefinition boss = StoryTable.Boss(pair.Key);
            Add(list, boss != null ? $"{boss.Name} — 다시 나왔을 때" : "중간 보스 — 다시 나왔을 때");
        }

        return list;
    }

    /// <summary>쓰는 곳. nameOf는 만든 것 · 건물의 이름을 찾는다(없으면 id).</summary>
    public static List<string> Uses(string id, Func<string, string> nameOf = null)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(id))
            return list;

        string Name(string x) => nameOf?.Invoke(x) ?? x;

        var cooked = new List<string>();
        foreach (Recipe r in CookingTable.All)
            if (Uses(r.Inputs, id))
                cooked.Add(Name(r.OutputId));
        AddGroup(list, CookingTable.HearthName, cooked);

        var brewed = new List<string>();
        foreach (CraftRecipe r in ApothecaryTable.Brews)
            if (Uses(r.Inputs, id))
                brewed.Add(Name(r.OutputId));
        AddGroup(list, ApothecaryTable.PotName, brewed);

        var crafted = new List<string>();
        var samples = new List<string>();
        foreach (CraftRecipe r in WorkbenchTable.All)
        {
            if (Uses(r.Inputs, id) && !crafted.Contains(Name(r.OutputId)))
                crafted.Add(Name(r.OutputId));

            if (Uses(r.UnlockCost, id) && !samples.Contains(Name(r.OutputId)))
                samples.Add(Name(r.OutputId));
        }
        AddGroup(list, "작업대", crafted);
        AddGroup(list, "작업대 견본", samples);

        var stages = new List<string>();
        for (int stage = 2; stage <= WorkbenchTable.MaxStage; stage++)
            if (Uses(WorkbenchTable.CostToReach(stage).Materials, id))
                stages.Add(WorkbenchTable.StageName(stage));
        if (stages.Count > 0)
            Add(list, $"작업대 단계 올리기 {string.Join(" · ", stages)}");

        var buildings = new List<string>();
        foreach (BuildingDefinition b in BuildingTable.All)
            if (Uses(b.Materials, id))
                buildings.Add(b.Name);
        AddGroup(list, "짓기", buildings);

        return list;
    }

    /// <summary>상세에 붙일 글 — 「얻는 곳 — …」 · 「쓰는 곳 — …」. 둘 다 없으면 빈 글.</summary>
    public static string Describe(string id, Func<string, bool> inCorpseLoot = null, Func<string, string> nameOf = null)
    {
        List<string> sources = Sources(id, inCorpseLoot);
        List<string> uses = Uses(id, nameOf);

        var sb = new StringBuilder();
        if (sources.Count > 0)
            sb.Append("얻는 곳 — ").Append(string.Join(" · ", sources));

        if (uses.Count > 0)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append("쓰는 곳 — ").Append(string.Join(" · ", uses));
        }

        return sb.ToString();
    }

    /// <summary>「이름(가 · 나 · 다 외 n)」 — 이름이 많으면 줄인다.</summary>
    public static string Group(string where, IReadOnlyList<string> names)
    {
        if (names == null || names.Count == 0)
            return where;

        int shown = Math.Min(names.Count, MaxNames);
        var head = new List<string>(shown);
        for (int i = 0; i < shown; i++)
            head.Add(names[i]);

        string inner = string.Join(" · ", head);
        if (names.Count > shown)
            inner += $" 외 {names.Count - shown}";

        return $"{where}({inner})";
    }

    /// <summary>시체에서 — 장 재료 · 탄은 나오기 시작하는 장을 붙인다.</summary>
    private static string CorpseWhere(string id)
    {
        Ingredient ingredient = IngredientTable.Find(id);
        if (ingredient != null && !ingredient.IsCommon)
            return $"{ingredient.Chapter}장 시체";

        AmmoInfo? ammo = AmmoTable.Find(id);
        if (ammo != null && ammo.Value.FirstTier > 1)
            return $"{ammo.Value.FirstTier}장부터 시체";

        return "시체";
    }

    private static bool InDailyPool(ShopKind kind, string id)
    {
        foreach (ShopEntry e in ShopDailyTable.Pool(kind, _ => true))
            if (e.ItemId == id)
                return true;

        return false;
    }

    /// <summary>재료 줄에 이 아이템이 드는가 — 묶음(산적의 고기)은 안에 든 것까지.</summary>
    private static bool Uses(MaterialCost[] costs, string id)
    {
        if (costs == null)
            return false;

        foreach (MaterialCost c in costs)
        {
            if (c.ItemId == id)
                return true;

            if (IngredientTable.IsTag(c.ItemId))
                foreach (string member in IngredientTable.Members(c.ItemId))
                    if (member == id)
                        return true;
        }

        return false;
    }

    private static void AddGroup(List<string> list, string where, List<string> names)
    {
        if (names.Count > 0)
            Add(list, Group(where, names));
    }

    private static void Add(List<string> list, string text)
    {
        if (!string.IsNullOrEmpty(text) && !list.Contains(text))
            list.Add(text);
    }
}
