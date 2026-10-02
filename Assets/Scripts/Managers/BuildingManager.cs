using System;
using System.Collections.Generic;

/// <summary>
/// 벙커 건물의 상태를 들고 있는다. (로드맵 8-K · docs/Dokkaebi_Bunker_System.md 3-4절)
///
/// MonoBehaviour가 아니다 — 무엇을 지었는지는 세이브에 속한다. 벙커 씬의
/// BunkerBuildings가 이 상태를 보고 건물을 세우고, 바뀌면 다시 세운다.
/// </summary>
public static class BuildingManager
{
    private static BuildingState state;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        state = null;
        OnChanged = null;
    }

    public static BuildingState State => state ??= new BuildingState();

    /// <summary>짓거나 · 놓거나 · 재활용했을 때.</summary>
    public static event Action OnChanged;

    /// <summary>지을 수 있는지. 골드는 PassiveManager, 재료는 창고 + 가방.</summary>
    public static BuildError CanBuild(BuildingDefinition definition)
    {
        int gold = PassiveManager.EnsureInstance().Gold;
        PlayerInventory inventory = PlayerInventory.EnsureInstance();

        return State.CanBuild(definition, gold,
            id => BuildingState.CountIn(inventory.Stash, inventory.Bag, id),
            StoryManager.Progress.IsBlueprintOpen);
    }

    /// <summary>값을 치르고 가진다. 놓는 것은 배치 모드가 따로 한다.</summary>
    public static BuildError Build(BuildingDefinition definition)
    {
        PassiveManager passive = PassiveManager.EnsureInstance();
        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        int gold = passive.Gold;

        BuildError error = State.Build(definition, ref gold, inventory.Stash, inventory.Bag,
            StoryManager.Progress.IsBlueprintOpen);

        if (error != BuildError.None)
            return error;

        passive.Gold = gold;
        inventory.RefreshCapacity();

        GameLogger.Log($"[Building] 지음 — {definition.Name}");

        // 가게 세우기는 상인의 부탁이다 — 지어 주면 보상을 준다.
        StoryManager.GrantBuildingReward(definition.Id);

        OnChanged?.Invoke();

        return BuildError.None;
    }

    public static bool Place(string id, BuildingPose pose)
    {
        if (!State.Place(id, pose))
            return false;

        OnChanged?.Invoke();
        return true;
    }

    public static bool Recycle(string id)
    {
        if (!State.Recycle(id))
            return false;

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>놓인 건물이 여는 자리가 있는가. 상점이 건물이 된 뒤로는 이것이 문이다.</summary>
    public static bool IsOpen(string id) => State.IsPlaced(id);

    // ── 세이브 ───────────────────────────────────────────────────────

    public static List<SavedBuilding> Capture()
    {
        var rows = new List<SavedBuilding>();

        foreach (string id in State.OwnedIds)
        {
            var row = new SavedBuilding { id = id };

            if (State.TryGetPose(id, out BuildingPose pose))
            {
                row.placed = true;
                row.x = pose.X;
                row.z = pose.Z;
                row.turns = pose.Turns;
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// 세이브에서 되살린다. 【표에 없는 건물은 버린다】 — 건물을 지우거나 id를 바꾸면 생긴다.
    /// </summary>
    public static void Restore(List<SavedBuilding> saved)
    {
        State.Clear();

        if (saved != null)
        {
            foreach (SavedBuilding row in saved)
            {
                if (row == null || BuildingTable.Find(row.id) == null)
                    continue;

                State.Own(row.id);

                if (row.placed)
                    State.Place(row.id, new BuildingPose(row.x, row.z, row.turns));
            }
        }

        OnChanged?.Invoke();
    }
}
