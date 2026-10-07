using System.Collections.Generic;
using UnityEngine;

/// <summary>건물을 놓은 자리. 회전은 90도 단위(0~3)다.</summary>
public struct BuildingPose
{
    public float X;
    public float Z;
    public int Turns;

    public BuildingPose(float x, float z, int turns)
    {
        X = x;
        Z = z;
        Turns = ((turns % 4) + 4) % 4;
    }
}

/// <summary>건설이 막힌 이유.</summary>
public enum BuildError
{
    None = 0,
    Unknown,
    AlreadyOwned,
    MissingBuilding,
    NotEnoughGold,
    NotEnoughMaterials,

    /// <summary>이 가게를 부탁할 상인이 아직 오지 않았다 — 설계도가 닫혀 있다 (3단계).</summary>
    MissingMerchant
}

/// <summary>
/// 무엇을 지었고 어디에 놓았는가. MonoBehaviour 없는 순수 클래스다.
///
/// 【지은 것과 놓은 것을 나눈다.】 덕코프는 건물 모드에서 건물을 재활용하면
/// 「손실 없이 건물 메뉴 목록으로 돌아간다」 [확인됨 — 위키 가이드].
/// 한 번 값을 치른 건물은 계속 가지고 있고, 놓지 않았을 뿐이다.
/// 놓지 않은 건물은 쓸 수 없지만, 다른 건물의 선행 조건은 채운다.
/// </summary>
public class BuildingState
{
    private readonly HashSet<string> owned = new();
    private readonly Dictionary<string, BuildingPose> placed = new();

    public bool Owns(string id) => id != null && owned.Contains(id);

    public bool IsPlaced(string id) => id != null && placed.ContainsKey(id);

    public bool TryGetPose(string id, out BuildingPose pose) => placed.TryGetValue(id, out pose);

    public IEnumerable<string> OwnedIds => owned;

    public IEnumerable<KeyValuePair<string, BuildingPose>> Placed => placed;

    /// <summary>값을 치르고 가진다. 놓지는 않는다.</summary>
    public void Own(string id)
    {
        if (!string.IsNullOrEmpty(id))
            owned.Add(id);
    }

    /// <summary>놓는다. 가지고 있지 않으면 false.</summary>
    public bool Place(string id, BuildingPose pose)
    {
        if (!Owns(id))
            return false;

        placed[id] = pose;
        return true;
    }

    /// <summary>재활용한다. 【손실이 없다】 — 가진 채로 목록에 돌아간다.</summary>
    public bool Recycle(string id) => placed.Remove(id);

    public void Clear()
    {
        owned.Clear();
        placed.Clear();
    }

    // ── 검사 · 값 치르기 ─────────────────────────────────────────────

    /// <summary>
    /// 지을 수 있는지. 재료는 【창고와 가방을 합쳐】 센다 —
    /// 덕코프가 어느 쪽 재료를 쓰는지는 위키에 없다 [확인 불가]. 벙커에서는
    /// 둘 다 손 닿는 곳에 있으니 나눌 이유가 없다.
    /// </summary>
    public BuildError CanBuild(BuildingDefinition definition, int gold, System.Func<string, int> countOf,
        System.Func<string, bool> blueprintOpen = null)
    {
        if (definition == null)
            return BuildError.Unknown;

        if (Owns(definition.Id))
            return BuildError.AlreadyOwned;

        // 【이야기에서 상인이 와야 설계도가 열린다】 (docs/Dokkaebi_Story.md 1절).
        // 판정을 넘겨받지 않으면 따지지 않는다 — 건물 규칙만 보는 검사 · 테스트용.
        if (blueprintOpen != null && !blueprintOpen(definition.Id))
            return BuildError.MissingMerchant;

        foreach (string required in definition.RequiredBuildings)
        {
            if (!Owns(required))
                return BuildError.MissingBuilding;
        }

        if (gold < definition.Gold)
            return BuildError.NotEnoughGold;

        foreach (MaterialCost cost in definition.Materials)
        {
            if (countOf == null || countOf(cost.ItemId) < cost.Count)
                return BuildError.NotEnoughMaterials;
        }

        return BuildError.None;
    }

    /// <summary>
    /// 짓는다 — 골드와 재료를 치르고 가진다. 【전부 아니면 전혀】: 검사를 먼저 하고,
    /// 통과했을 때만 뺀다. 재료는 창고에서 먼저, 모자라면 가방에서 뺀다 —
    /// 가방은 다음 파밍에 들고 갈 것이다.
    /// </summary>
    public BuildError Build(BuildingDefinition definition, ref int gold, Inventory stash, Inventory bag,
        System.Func<string, bool> blueprintOpen = null)
    {
        BuildError error = CanBuild(definition, gold, id => CountIn(stash, bag, id), blueprintOpen);

        if (error != BuildError.None)
            return error;

        foreach (MaterialCost cost in definition.Materials)
        {
            int remaining = cost.Count;
            remaining -= RemoveById(stash, cost.ItemId, remaining);
            RemoveById(bag, cost.ItemId, remaining);
        }

        gold -= definition.Gold;
        Own(definition.Id);

        return BuildError.None;
    }

    public static int CountIn(Inventory stash, Inventory bag, string itemId)
        => CountById(stash, itemId) + CountById(bag, itemId);

    private static int CountById(Inventory inventory, string itemId)
    {
        if (inventory == null)
            return 0;

        int total = 0;

        foreach (ItemStack stack in inventory.Stacks)
        {
            if (stack?.Definition != null && stack.Definition.Id == itemId)
                total += stack.Count;
        }

        return total;
    }

    /// <summary>그 아이템을 count개까지 뺀다. 뺀 수를 돌려준다. 요리(CookingTable)도 쓴다.</summary>
    public static int RemoveById(Inventory inventory, string itemId, int count)
    {
        if (inventory == null || count <= 0)
            return 0;

        foreach (ItemStack stack in inventory.Stacks)
        {
            if (stack?.Definition != null && stack.Definition.Id == itemId)
                return inventory.Remove(stack.Definition, count);
        }

        return 0;
    }

    public static string Explain(BuildError error)
    {
        switch (error)
        {
            case BuildError.AlreadyOwned:       return "이미 지었습니다.";
            case BuildError.MissingBuilding:    return "먼저 지어야 하는 건물이 있습니다.";
            case BuildError.NotEnoughGold:      return "엽전이 모자랍니다.";
            case BuildError.NotEnoughMaterials: return "재료가 모자랍니다.";
            case BuildError.Unknown:            return "알 수 없는 건물입니다.";
            case BuildError.MissingMerchant:    return "이 가게를 부탁할 상인이 아직 오지 않았습니다.";
            default:                            return string.Empty;
        }
    }
}

/// <summary>
/// 벙커 바닥 — 방 크기와 「여기에 놓을 수 있는가」. MonoBehaviour 없는 순수 클래스다.
/// 에디터 생성기(BunkerSceneGenerator)와 배치 모드가 같은 값을 쓴다.
/// </summary>
public static class BunkerLayout
{
    /// <summary>방 크기 (m). 가로 22 · 세로 14. 벙커 체류는 3분 이내다(Progression 2절).</summary>
    public const float RoomWidth = 22f;
    public const float RoomDepth = 14f;

    /// <summary>벽에서 띄우는 거리. 벽에 붙이면 뒤로 돌아갈 수 없다.</summary>
    public const float WallMargin = 0.75f;

    /// <summary>배치 격자 (m).</summary>
    public const float Snap = 0.5f;

    /// <summary>플레이어 시작 자리.</summary>
    public static readonly Vector3 Spawn = Vector3.zero;

    /// <summary>고정 자리 — 창고 · 파밍 출발 · 설계도 테이블.</summary>
    public static readonly Vector3 StashPosition = new(-6f, 0f, RoomDepth * 0.5f - 2f);
    public static readonly Vector3 DeparturePosition = new(0f, 0f, -RoomDepth * 0.5f + 2f);

    /// <summary>덕코프처럼 「플레이어 스폰 지점 오른쪽」 [확인됨 — 위키 가이드].</summary>
    public static readonly Vector3 BlueprintPosition = new(3f, 0f, 0f);

    /// <summary>고정 자리가 바닥에서 차지하는 크기 (m).</summary>
    public const float FixedSize = 2f;

    public static float SnapValue(float value) => Mathf.Round(value / Snap) * Snap;

    /// <summary>회전을 반영한 바닥 사각형.</summary>
    public static Rect Footprint(BuildingDefinition definition, BuildingPose pose)
    {
        bool sideways = (pose.Turns & 1) == 1;
        float w = sideways ? definition.Depth : definition.Width;
        float d = sideways ? definition.Width : definition.Depth;

        return new Rect(pose.X - w * 0.5f, pose.Z - d * 0.5f, w, d);
    }

    public static Rect FixedRect(Vector3 position)
        => new(position.x - FixedSize * 0.5f, position.z - FixedSize * 0.5f, FixedSize, FixedSize);

    /// <summary>방 안에 들어가고, 다른 것과 겹치지 않는가.</summary>
    public static bool CanPlace(Rect footprint, IEnumerable<Rect> occupied)
    {
        float halfW = RoomWidth * 0.5f - WallMargin;
        float halfD = RoomDepth * 0.5f - WallMargin;

        if (footprint.xMin < -halfW || footprint.xMax > halfW
            || footprint.yMin < -halfD || footprint.yMax > halfD)
            return false;

        if (occupied == null)
            return true;

        foreach (Rect other in occupied)
        {
            if (footprint.Overlaps(other))
                return false;
        }

        return true;
    }
}
