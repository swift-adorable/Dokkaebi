using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 약탕간의 달이기 · 샘가 (결정 2-97) — ApothecaryTable · SpringsideState를 창고 · 가방 · 밤 시계에 잇는다. 세이브 판 19.
/// </summary>
public static class ApothecaryManager
{
    private static SpringsideState springside;

    public static SpringsideState Springside => springside ??= new SpringsideState();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => springside = null;

    /// <summary>약탕간을 지어 놓았는가.</summary>
    public static bool Built => BuildingManager.IsOpen(BuildingTable.Apothecary);

    public static CraftError Brew(CraftRecipe recipe)
    {
        CraftError error = ApothecaryTable.Brew(recipe, Built, WorkbenchManager.CountOf,
            WorkbenchManager.Remove, WorkbenchManager.CanGive, WorkbenchManager.Give);

        if (error == CraftError.None)
            Finish("약 달이기");

        return error;
    }

    /// <summary>
    /// 한 판이 끝났다 — 철수 · 쓰러짐의 NightClock.PassNight 바로 옆에서 부른다.
    /// 출발 화면의 「하룻밤 쉬기」는 부르지 않는다 — 쉬기만 거듭해 약재를 거저 얻지 않게.
    /// 저장은 부르는 쪽이 한다.
    /// </summary>
    public static void OnNightPassed()
    {
        StoryProgress progress = StoryManager.Progress;
        List<string> pool = ApothecaryTable.ChapterPool(c => progress != null && progress.IsChapterOpen(c));
        Springside.AccrueNight(Built, pool, null);
    }

    /// <summary>샘가의 것을 거둔다. 놓을 자리가 없는 것은 샘가에 남긴다. 거둔 묶음 수를 돌려준다.</summary>
    public static int Collect(out bool leftSome)
    {
        leftSome = false;
        int taken = 0;
        var left = new List<SavedItem>();

        foreach (MaterialCost c in Springside.TakeAll())
        {
            if (WorkbenchManager.CanGive(c.ItemId, c.Count))
            {
                WorkbenchManager.Give(c.ItemId, c.Count);
                taken++;
            }
            else
            {
                left.Add(new SavedItem { id = c.ItemId, count = c.Count });
                leftSome = true;
            }
        }

        if (left.Count > 0)
            Springside.Restore(left);

        if (taken > 0)
            Finish("샘가 거두기");

        return taken;
    }

    private static void Finish(string reason)
    {
        PlayerInventory.EnsureInstance().RefreshCapacity();
        ExchangeWindowUI.RefreshIfOpen();
        SaveManager.Commit(reason);
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    public static void Capture(SaveData data) => data.springside = Springside.Capture();

    public static void Restore(SaveData data) => Springside.Restore(data.springside);

    public static void Reset() => Springside.Reset();
}
