using System;
using System.Collections.Generic;

public enum CraftError
{
    None = 0,
    /// <summary>작업대를 짓지 않았다.</summary>
    NoWorkbench = 1,
    /// <summary>작업대 단계가 낮다.</summary>
    StageLocked = 2,
    /// <summary>아직 바쳐 열지 않았다.</summary>
    NotUnlocked = 3,
    /// <summary>이미 열었다.</summary>
    AlreadyUnlocked = 4,
    /// <summary>바칠 것(견본 · 도면 · 장별 재료)이 없다.</summary>
    MissingOffering = 5,
    MissingMaterial = 6,
    NotEnoughGold = 7,
    /// <summary>창고 · 가방 어디에도 자리가 없다.</summary>
    NoRoom = 8,
    /// <summary>더 올릴 단계가 없다.</summary>
    MaxStage = 9,
    /// <summary>바칠 필요 없는 제작법이다.</summary>
    NoUnlockNeeded = 10,
    /// <summary>고칠 것이 없다.</summary>
    NothingToRepair = 11,
    /// <summary>분해할 수 없다 (장비만 · 입은 것은 벗어야 한다).</summary>
    CannotDismantle = 12,
}

/// <summary>
/// 【작업대 상태】 (결정 2-95) — 단계 · 바쳐 연 제작법. 세이브 판 17 `workbenchStage` · `craftUnlocks`.
/// 재료 · 엽전 · 놓을 자리는 바깥에서 함수로 받는다 — MonoBehaviour 의존 없음, EditMode 테스트 대상.
/// </summary>
public class WorkbenchState
{
    private readonly HashSet<string> unlocked = new();

    /// <summary>작업대 단계 (짓기 전에도 1로 둔다 — 지었는지는 따로 묻는다).</summary>
    public int Stage { get; private set; } = 1;

    public IReadOnlyCollection<string> Unlocked => unlocked;

    public bool IsUnlocked(CraftRecipe recipe) => recipe != null && (!recipe.NeedsUnlock || unlocked.Contains(recipe.OutputId));

    // ── 단계 올리기 ──────────────────────────────────────────────────

    public CraftError CanUpgrade(bool built, int gold, Func<string, int> countOf)
    {
        if (!built)
            return CraftError.NoWorkbench;

        if (Stage >= WorkbenchTable.MaxStage)
            return CraftError.MaxStage;

        StageCost cost = WorkbenchTable.CostToReach(Stage + 1);

        if (gold < cost.Gold)
            return CraftError.NotEnoughGold;

        return Has(cost.Materials, countOf) ? CraftError.None : CraftError.MissingMaterial;
    }

    public CraftError Upgrade(bool built, ref int gold, Func<string, int> countOf, Func<string, int, int> remove)
    {
        CraftError error = CanUpgrade(built, gold, countOf);
        if (error != CraftError.None)
            return error;

        StageCost cost = WorkbenchTable.CostToReach(Stage + 1);
        Pay(cost.Materials, remove);
        gold -= cost.Gold;
        Stage++;
        return CraftError.None;
    }

    // ── 바쳐 열기 ────────────────────────────────────────────────────

    public CraftError CanUnlock(CraftRecipe recipe, bool built, Func<string, int> countOf)
    {
        if (!built)
            return CraftError.NoWorkbench;

        if (recipe == null || !recipe.NeedsUnlock)
            return CraftError.NoUnlockNeeded;

        if (Stage < recipe.Stage)
            return CraftError.StageLocked;

        if (unlocked.Contains(recipe.OutputId))
            return CraftError.AlreadyUnlocked;

        return Has(recipe.UnlockCost, countOf) ? CraftError.None : CraftError.MissingOffering;
    }

    public CraftError Unlock(CraftRecipe recipe, bool built, Func<string, int> countOf, Func<string, int, int> remove)
    {
        CraftError error = CanUnlock(recipe, built, countOf);
        if (error != CraftError.None)
            return error;

        Pay(recipe.UnlockCost, remove);
        unlocked.Add(recipe.OutputId);
        return CraftError.None;
    }

    // ── 만들기 ───────────────────────────────────────────────────────

    public CraftError CanCraft(CraftRecipe recipe, bool built, int gold, Func<string, int> countOf)
    {
        if (!built)
            return CraftError.NoWorkbench;

        if (recipe == null)
            return CraftError.NotUnlocked;

        if (Stage < recipe.Stage)
            return CraftError.StageLocked;

        if (!IsUnlocked(recipe))
            return CraftError.NotUnlocked;

        if (gold < recipe.Gold)
            return CraftError.NotEnoughGold;

        return Has(recipe.Inputs, countOf) ? CraftError.None : CraftError.MissingMaterial;
    }

    /// <param name="give">만든 것을 넣는다 — 자리가 없으면 false (아무것도 쓰지 않는다).</param>
    public CraftError Craft(CraftRecipe recipe, bool built, ref int gold, Func<string, int> countOf,
                            Func<string, int, int> remove, Func<string, int, bool> canGive, Action<string, int> give)
    {
        CraftError error = CanCraft(recipe, built, gold, countOf);
        if (error != CraftError.None)
            return error;

        if (canGive != null && !canGive(recipe.OutputId, recipe.OutputCount))
            return CraftError.NoRoom;

        Pay(recipe.Inputs, remove);
        gold -= recipe.Gold;
        give?.Invoke(recipe.OutputId, recipe.OutputCount);
        return CraftError.None;
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    public void Restore(int stage, IEnumerable<string> ids)
    {
        Stage = Math.Clamp(stage, 1, WorkbenchTable.MaxStage);
        unlocked.Clear();

        if (ids == null)
            return;

        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id))
                unlocked.Add(id);
    }

    public void Reset() => Restore(1, null);

    private static bool Has(MaterialCost[] costs, Func<string, int> countOf)
    {
        foreach (MaterialCost c in costs)
            if (countOf == null || countOf(c.ItemId) < c.Count)
                return false;

        return true;
    }

    private static void Pay(MaterialCost[] costs, Func<string, int, int> remove)
    {
        foreach (MaterialCost c in costs)
            remove?.Invoke(c.ItemId, c.Count);
    }

    public static string Explain(CraftError error) => error switch
    {
        CraftError.NoWorkbench => "작업대를 먼저 지어야 한다.",
        CraftError.StageLocked => "작업대 단계가 모자란다.",
        CraftError.NotUnlocked => "아직 바쳐 열지 않았다.",
        CraftError.AlreadyUnlocked => "이미 열었다.",
        CraftError.MissingOffering => "바칠 것이 모자란다.",
        CraftError.MissingMaterial => "재료가 모자란다.",
        CraftError.NotEnoughGold => "엽전이 모자란다.",
        CraftError.NoRoom => "창고에도 가방에도 자리가 없다.",
        CraftError.MaxStage => "더 올릴 단계가 없다.",
        CraftError.NothingToRepair => "고칠 데가 없다.",
        CraftError.CannotDismantle => "분해할 수 없다 — 가방 · 창고의 장비만 (입은 것은 벗어야 한다).",
        _ => string.Empty,
    };
}
