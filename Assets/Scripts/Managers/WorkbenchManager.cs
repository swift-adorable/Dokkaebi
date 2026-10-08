using System.Linq;
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

    // ── 수리 · 분해 (결정 2-96) ──────────────────────────────────────

    /// <summary>고칠 것들 — 입은 것 · 가방 · 창고.</summary>
    public static List<ItemStack> Repairable()
    {
        var list = new List<ItemStack>();
        PlayerInventory inv = PlayerInventory.EnsureInstance();

        foreach (ItemStack s in inv.Loadout.All())
            if (WearTable.NeedsRepair(s)) list.Add(s);
        foreach (ItemStack s in inv.Bag.Stacks)
            if (WearTable.NeedsRepair(s)) list.Add(s);
        foreach (ItemStack s in inv.Stash.Stacks)
            if (WearTable.NeedsRepair(s)) list.Add(s);

        return list;
    }

    /// <summary>분해할 수 있는 것들 — 가방 · 창고 (입은 것은 벗어야 한다).</summary>
    public static List<ItemStack> Dismantlable()
    {
        var list = new List<ItemStack>();
        PlayerInventory inv = PlayerInventory.EnsureInstance();

        foreach (ItemStack s in inv.Bag.Stacks)
            if (WearTable.CanDismantle(s?.Definition)) list.Add(s);
        foreach (ItemStack s in inv.Stash.Stacks)
            if (WearTable.CanDismantle(s?.Definition)) list.Add(s);

        return list;
    }

    public static CraftError Repair(ItemStack stack)
    {
        if (!Built)
            return CraftError.NoWorkbench;

        if (!WearTable.NeedsRepair(stack))
            return CraftError.NothingToRepair;

        int cost = WearTable.RepairCost(stack);
        if (Gold < cost)
            return CraftError.NotEnoughGold;

        stack.Repair(stack.MaxDurability);
        PlayerInventory.EnsureInstance().Loadout.MarkDirty();
        Finish(CraftError.None, Gold - cost, "작업대 수리");
        return CraftError.None;
    }

    public static CraftError Dismantle(ItemStack stack)
    {
        if (!Built)
            return CraftError.NoWorkbench;

        if (stack?.Definition == null || !WearTable.CanDismantle(stack.Definition))
            return CraftError.CannotDismantle;

        PlayerInventory inv = PlayerInventory.EnsureInstance();
        Inventory from = inv.Bag.Stacks.Contains(stack) ? inv.Bag : inv.Stash.Stacks.Contains(stack) ? inv.Stash : null;
        if (from == null)
            return CraftError.CannotDismantle;

        List<MaterialCost> yield = WearTable.DismantleYield(stack);
        from.RemoveStack(stack);

        foreach (MaterialCost c in yield)
            if (!CanGive(c.ItemId, c.Count))
            {
                from.TryAddStack(stack);   // 놓을 자리가 없으면 되돌린다
                return CraftError.NoRoom;
            }

        foreach (MaterialCost c in yield)
            Give(c.ItemId, c.Count);

        Finish(CraftError.None, Gold, "작업대 분해");
        return CraftError.None;
    }

    /// <summary>창고 먼저, 모자라면 가방에서 뺀다. 뺀 수를 돌려준다. (약탕간도 쓴다 — 결정 2-97)</summary>
    public static int Remove(string itemId, int count)
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

    public static bool CanGive(string itemId, int count)
    {
        ItemDefinition definition = ItemCatalog.Load()?.Find(itemId);
        if (definition == null)
            return false;

        PlayerInventory inv = PlayerInventory.EnsureInstance();
        return inv.Stash.CanAdd(definition, count) || inv.Bag.CanAdd(definition, count);
    }

    /// <summary>창고로, 자리가 없으면 가방으로.</summary>
    public static void Give(string itemId, int count)
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
