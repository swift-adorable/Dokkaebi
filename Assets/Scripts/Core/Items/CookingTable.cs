using System;
using System.Collections.Generic;

/// <summary>요리 한 가지 — 재료를 넣으면 이미 있는 음식이 나온다 (결정 2-64).</summary>
public sealed class Recipe
{
    public readonly string OutputId;
    public readonly int OutputCount;
    public readonly MaterialCost[] Inputs;

    public Recipe(string outputId, int outputCount, MaterialCost[] inputs)
    {
        OutputId = outputId;
        OutputCount = outputCount < 1 ? 1 : outputCount;
        Inputs = inputs ?? Array.Empty<MaterialCost>();
    }
}

public enum CookError
{
    None = 0,

    /// <summary>영감의 잡화 가게를 아직 놓지 않았다.</summary>
    Locked = 1,

    MissingMaterial = 2,

    /// <summary>만든 음식이 가방에 들어가지 않는다.</summary>
    BagFull = 3
}

/// <summary>
/// 【Cooking Data.】 영감의 잡화 가게에서 재료를 조합해 음식을 만든다 (결정 2-64 · docs/Dokkaebi_Cooking_System.md).
///
/// 만드는 것은 **이미 있는 음식**뿐이다 — 값(수분 · 에너지)은 Consumable(생성기)이 원본이다.
/// 포만도 · 버프 · 실패 확률 · 조리 시간은 두지 않는다. 메밀묵은 이야기 음식이라 레시피가 아니다.
/// 재료 이름 · 개수는 [임시값].
/// </summary>
public static class CookingTable
{
    public const string Rice = "food_rice";
    public const string Barley = "food_barley";
    public const string Malt = "food_malt";
    public const string Persimmon = "food_persimmon";

    private static Recipe R(string output, params (string id, int count)[] inputs)
    {
        var costs = new MaterialCost[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
            costs[i] = new MaterialCost(inputs[i].id, inputs[i].count);
        return new Recipe(output, 1, costs);
    }

    private static readonly Recipe[] all =
    {
        R("con_canned",     (Rice, 2)),                 // 누룽지 — 에너지 위주
        R("con_ration",     (Barley, 1), (Rice, 1)),    // 미숫가루 — 에너지 위주
        R("con_soda",       (Rice, 1), (Malt, 1)),      // 식혜 — 수분 위주
        R("con_energy_bar", (Persimmon, 1)),            // 곶감 — 가볍다
    };

    public static IReadOnlyList<Recipe> All => all;

    public static Recipe ForOutput(string outputId)
    {
        foreach (Recipe r in all)
            if (r.OutputId == outputId)
                return r;

        return null;
    }

    /// <summary>요리가 열렸는가 — 영감의 잡화 가게를 놓았다 (서브 퀘스트 「영감의 잡화 가게」).</summary>
    public static bool IsUnlocked(Func<string, bool> isPlaced)
        => isPlaced != null && isPlaced(BuildingTable.GeneralStore);

    public static CookError CanCook(Recipe recipe, Func<string, int> countOf, bool unlocked)
    {
        if (!unlocked)
            return CookError.Locked;

        foreach (MaterialCost c in recipe.Inputs)
            if (countOf == null || countOf(c.ItemId) < c.Count)
                return CookError.MissingMaterial;

        return CookError.None;
    }

    /// <summary>만든다 — 재료는 **창고에서 먼저** 빼고(건설과 같다), 음식은 가방으로 들어온다(가게에서 산 것과 같다).</summary>
    public static CookError Cook(Recipe recipe, Inventory stash, Inventory bag, ItemDefinition output, bool unlocked)
    {
        CookError error = CanCook(recipe, id => BuildingState.CountIn(stash, bag, id), unlocked);

        if (error != CookError.None)
            return error;

        if (bag == null || output == null || !bag.CanAdd(output, recipe.OutputCount))
            return CookError.BagFull;

        foreach (MaterialCost c in recipe.Inputs)
        {
            int remaining = c.Count;
            remaining -= BuildingState.RemoveById(stash, c.ItemId, remaining);
            BuildingState.RemoveById(bag, c.ItemId, remaining);
        }

        bag.TryAdd(output, recipe.OutputCount);
        return CookError.None;
    }

    public static string Explain(CookError error)
    {
        switch (error)
        {
            case CookError.Locked:          return "영감의 잡화 가게를 세워야 요리할 수 있다.";
            case CookError.MissingMaterial: return "재료가 모자란다.";
            case CookError.BagFull:         return "가방에 자리가 없다.";
            default:                        return string.Empty;
        }
    }
}
