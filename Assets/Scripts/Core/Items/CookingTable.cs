using System;
using System.Collections.Generic;

/// <summary>음식의 효과 크기 (결정 2-73). 작음 = 공용 재료만 · 보통 = 장 재료 하나 · 큼 = 장 재료 둘 이상.</summary>
public enum CookTier
{
    Small = 0,
    Medium = 1,
    Large = 2
}

/// <summary>요리 한 가지 — 재료를 넣으면 이미 있는 음식이 나온다 (결정 2-64).</summary>
public sealed class Recipe
{
    public readonly string OutputId;
    public readonly int OutputCount;
    public readonly CookTier Tier;
    public readonly MaterialCost[] Inputs;

    public Recipe(string outputId, int outputCount, MaterialCost[] inputs, CookTier tier = CookTier.Small)
    {
        OutputId = outputId;
        OutputCount = outputCount < 1 ? 1 : outputCount;
        Tier = tier;
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
/// 【Cooking Data.】 잡화 가게 옆 부뚜막에서 재료를 조합해 음식을 만든다 (결정 2-64 · 2-71 · docs/Dokkaebi_Cooking_System.md).
///
/// 만드는 것은 **이미 있는 음식**뿐이다 — 값(수분 · 에너지)은 Consumable(생성기)이 원본이다.
/// 포만도 · 버프 · 실패 확률 · 조리 시간은 두지 않는다. 메밀묵은 이야기 음식이라 레시피가 아니다.
/// 재료 이름 · 개수는 [임시값]. 재료와 나오는 장은 IngredientTable, 물은 고목 뿌리 샘가(SpringTable) — 결정 2-73.
/// </summary>
public static class CookingTable
{
    /// <summary>요리하는 자리 — 영감의 잡화 가게 옆에 저절로 생긴다 (결정 2-71). 따로 짓지 않는다.</summary>
    public const string HearthName = "부뚜막";

    // 재료 id는 IngredientTable이 원본이다. 물은 고목 뿌리 샘가에서 떠 오는 호리병 물(SpringTable).
    private const string Rice = IngredientTable.Rice;
    private const string Malt = IngredientTable.Malt;
    private const string Water = SpringTable.WaterId;

    private static Recipe R(CookTier tier, string output, params (string id, int count)[] inputs)
    {
        var costs = new MaterialCost[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
            costs[i] = new MaterialCost(inputs[i].id, inputs[i].count);
        return new Recipe(output, 1, costs, tier);
    }

    private const CookTier S = CookTier.Small;
    private const CookTier M = CookTier.Medium;
    private const CookTier L = CookTier.Large;

    /// <summary>스물두 가지 (결정 2-73). 수치(수분 · 에너지 · 값)는 ConsumableAssetGenerator가 원본이다.</summary>
    private static readonly Recipe[] all =
    {
        // ── 작음 — 공용 재료만. 어느 장에서든 만든다.
        R(S, "con_canned",          (Rice, 2)),                                     // 누룽지
        R(S, "con_yeot",            (Rice, 1), (Malt, 1)),                          // 엿
        R(S, "con_energy_bar",      (IngredientTable.Persimmon, 1)),                // 곶감
        R(S, "con_roast_chestnut",  (IngredientTable.Chestnut, 1)),                 // 군밤
        R(S, "con_roast_potato",    (IngredientTable.Potato, 1)),                   // 군감자
        R(S, "con_roast_sweet_potato", (IngredientTable.SweetPotato, 1)),           // 군고구마
        R(S, "con_soda",            (Rice, 1), (Malt, 1), (Water, 1)),              // 식혜
        R(S, "con_berry_juice",     (IngredientTable.Berry, 1), (Water, 1)),        // 열매즙

        // ── 보통 — 장 재료 하나 + 공용
        R(M, "con_sujeonggwa",      (IngredientTable.Persimmon, 1), (IngredientTable.Ginger, 1), (Water, 1)), // 수정과 · 1장
        R(M, "con_ration",          (IngredientTable.Barley, 1), (Rice, 1)),        // 미숫가루 · 2장
        R(M, "con_yugwa",           (IngredientTable.GlutinousRice, 1), (Malt, 1)), // 유과 · 2장
        R(M, "con_songpyeon",       (Rice, 1), (IngredientTable.Bean, 1)),          // 송편 · 4장
        R(M, "con_acorn_jelly",     (IngredientTable.Acorn, 2)),                    // 도토리묵 · 4장
        R(M, "con_jujube_tea",      (IngredientTable.Jujube, 1), (Water, 1)),       // 대추차 · 4장
        R(M, "con_yakgwa",          (IngredientTable.Wheat, 1), (IngredientTable.Sesame, 1), (Malt, 1)), // 약과 · 5장
        R(M, "con_hwajeon",         (IngredientTable.GlutinousRice, 1), (IngredientTable.Petal, 1)),     // 화전 · 6장
        R(M, "con_honey_water",     (IngredientTable.Honey, 1), (Water, 1)),        // 꿀물 · 6장

        // ── 큼 — 장 재료 둘 이상 (앞 장 재료는 창고에 모아 둔 것)
        R(L, "con_tteokguk",        (IngredientTable.RiceCake, 1), (IngredientTable.Radish, 1), (Water, 1)),  // 떡국 · 1장
        R(L, "con_samgyetang",      (IngredientTable.Chicken, 1), (IngredientTable.Ginseng, 1), (IngredientTable.GlutinousRice, 1)), // 삼계탕 · 3장
        R(L, "con_pumpkin_porridge", (IngredientTable.Pumpkin, 1), (IngredientTable.GlutinousRice, 1), (Rice, 1)), // 호박죽 · 4장
        R(L, "con_sanjeok",         (IngredientTable.MeatTag, 2)),                  // 산적 · 4 ~ 5장 — 고기 아무거나
        R(L, "con_yaksik",          (IngredientTable.GlutinousRice, 1), (IngredientTable.Jujube, 1),
                                    (IngredientTable.Chestnut, 1), (IngredientTable.Honey, 1)),        // 약식 · 6장
    };

    public static IReadOnlyList<Recipe> All => all;

    public static Recipe ForOutput(string outputId)
    {
        foreach (Recipe r in all)
            if (r.OutputId == outputId)
                return r;

        return null;
    }

    /// <summary>요리가 열렸는가 — 영감의 잡화 가게를 놓았다 = 그 옆에 부뚜막이 생겼다 (서브 퀘스트 「영감의 잡화 가게」).</summary>
    public static bool IsUnlocked(Func<string, bool> isPlaced)
        => isPlaced != null && isPlaced(BuildingTable.GeneralStore);

    /// <summary>가진 개수 — 묶음(산적의 고기)은 안에 든 재료를 모두 더한다.</summary>
    public static int CountOf(string id, Func<string, int> countOf)
    {
        if (countOf == null)
            return 0;

        int total = 0;
        foreach (string member in IngredientTable.Members(id))
            total += countOf(member);

        return total;
    }

    public static CookError CanCook(Recipe recipe, Func<string, int> countOf, bool unlocked)
    {
        if (!unlocked)
            return CookError.Locked;

        foreach (MaterialCost c in recipe.Inputs)
            if (CountOf(c.ItemId, countOf) < c.Count)
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

            // 묶음은 앞에 적힌 재료부터 쓴다. 어느 쪽이든 창고 먼저.
            foreach (string member in IngredientTable.Members(c.ItemId))
            {
                if (remaining <= 0)
                    break;

                remaining -= BuildingState.RemoveById(stash, member, remaining);
                remaining -= BuildingState.RemoveById(bag, member, remaining);
            }
        }

        bag.TryAdd(output, recipe.OutputCount);
        return CookError.None;
    }

    public static string Explain(CookError error)
    {
        switch (error)
        {
            case CookError.Locked:          return "영감의 잡화 가게를 세워야 부뚜막이 생긴다.";
            case CookError.MissingMaterial: return "재료가 모자란다.";
            case CookError.BagFull:         return "가방에 자리가 없다.";
            default:                        return string.Empty;
        }
    }
}
