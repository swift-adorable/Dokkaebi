using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 작업대 (결정 2-95) — WorkbenchState를 들고 창고 · 가방 · 엽전에 잇는다. 세이브 판 17.
///   · 재료 · 바칠 것은 【창고 먼저】, 모자라면 가방에서 (건설 · 요리와 같다)
///   · 만든 것은 창고로, 창고에 자리가 없으면 가방으로
/// </summary>
public static class WorkbenchManager
{
    private static WorkbenchState state;

    public static WorkbenchState State => state ??= new WorkbenchState();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => state = null;

    /// <summary>작업대를 지어 놓았는가.</summary>
    public static bool Built => BuildingManager.IsOpen(BuildingTable.Workbench);

    public static int Gold => PassiveManager.HasInstance ? PassiveManager.Instance.Gold : 0;

    public static int CountOf(string itemId)
    {
        PlayerInventory inv = PlayerInventory.EnsureInstance();
        return BuildingState.CountIn(inv.Stash, inv.Bag, itemId);
    }

    public static CraftError Upgrade()
    {
        int gold = Gold;
        CraftError error = State.Upgrade(Built, ref gold, CountOf, Remove);
        Finish(error, gold, $"작업대 {WorkbenchTable.StageName(State.Stage)}");
        return error;
    }

    public static CraftError Unlock(CraftRecipe recipe)
    {
        CraftError error = State.Unlock(recipe, Built, CountOf, Remove);
        Finish(error, Gold, "작업대 바치기");
        return error;
    }

    public static CraftError Craft(CraftRecipe recipe)
    {
        int gold = Gold;
        CraftError error = State.Craft(recipe, Built, ref gold, CountOf, Remove, CanGive, Give);
        Finish(error, gold, "작업대 제작");
        return error;
    }

    private static void Finish(CraftError error, int gold, string reason)
    {
        if (error != CraftError.None)
            return;

        if (PassiveManager.HasInstance)
            PassiveManager.Instance.Gold = gold;

        PlayerInventory.EnsureInstance().RefreshCapacity();
        ExchangeWindowUI.RefreshIfOpen();
        SaveManager.Commit(reason);
    }

    /// <summary>창고 먼저, 모자라면 가방에서 뺀다. 뺀 수를 돌려준다.</summary>
    private static int Remove(string itemId, int count)
    {
        PlayerInventory inv = PlayerInventory.EnsureInstance();
        int removed = RemoveAll(inv.Stash, itemId, count);
        removed += RemoveAll(inv.Bag, itemId, count - removed);
        return removed;
    }

    private static int RemoveAll(Inventory inventory, string itemId, int count)
    {
        int removed = 0;

        while (removed < count)
        {
            int r = BuildingState.RemoveById(inventory, itemId, count - removed);
            if (r <= 0)
                break;
            removed += r;
        }

        return removed;
    }

    private static bool CanGive(string itemId, int count)
    {
        ItemDefinition definition = ItemCatalog.Load()?.Find(itemId);
        if (definition == null)
            return false;

        PlayerInventory inv = PlayerInventory.EnsureInstance();
        return inv.Stash.CanAdd(definition, count) || inv.Bag.CanAdd(definition, count);
    }

    private static void Give(string itemId, int count)
    {
        ItemDefinition definition = ItemCatalog.Load()?.Find(itemId);
        if (definition == null)
            return;

        PlayerInventory inv = PlayerInventory.EnsureInstance();
        Inventory target = inv.Stash.CanAdd(definition, count) ? inv.Stash : inv.Bag;
        target.TryAdd(definition, count);
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    public static void Capture(SaveData data)
    {
        data.workbenchStage = State.Stage;
        data.craftUnlocks = new List<string>(State.Unlocked);
    }

    public static void Restore(SaveData data) => State.Restore(data.workbenchStage, data.craftUnlocks);

    public static void Reset() => State.Reset();
}
