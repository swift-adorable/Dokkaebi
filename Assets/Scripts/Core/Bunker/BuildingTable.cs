using System.Collections.Generic;

/// <summary>재료 한 줄 — 아이템 id와 개수.</summary>
public readonly struct MaterialCost
{
    public readonly string ItemId;
    public readonly int Count;

    public MaterialCost(string itemId, int count)
    {
        ItemId = itemId;
        Count = count < 1 ? 1 : count;
    }
}

/// <summary>벙커 건물 하나의 정의. MonoBehaviour 없는 순수 데이터다.</summary>
public sealed class BuildingDefinition
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Description;

    /// <summary>짓는 데 드는 골드.</summary>
    public readonly int Gold;

    /// <summary>짓는 데 드는 재료.</summary>
    public readonly MaterialCost[] Materials;

    /// <summary>먼저 지어져 있어야 하는 건물. 덕코프 건물 페이지의 「필요한 건물」.</summary>
    public readonly string[] RequiredBuildings;

    /// <summary>바닥에서 차지하는 크기(m). 회전하면 가로 · 세로가 바뀐다.</summary>
    public readonly float Width;
    public readonly float Depth;

    /// <summary>지으면 여는 자리. 없으면 BunkerStation.Kind.None.</summary>
    public readonly BunkerStation.Kind Opens;

    public BuildingDefinition(string id, string name, string description, int gold,
        MaterialCost[] materials, string[] requiredBuildings, float width, float depth,
        BunkerStation.Kind opens)
    {
        Id = id;
        Name = name;
        Description = description;
        Gold = gold < 0 ? 0 : gold;
        Materials = materials ?? System.Array.Empty<MaterialCost>();
        RequiredBuildings = requiredBuildings ?? System.Array.Empty<string>();
        Width = width;
        Depth = depth;
        Opens = opens;
    }
}

/// <summary>
/// 【벙커 건물표.】 상인 넷이 가게를 나눠 맡는다 (docs/Dokkaebi_Bunker_System.md 1절 · 결정 2-52 · 2-57).
///
///   작업대   — 상인 없이 처음부터 (제작 · 수리 · 분해 — 제작은 다음 단계)
///   잡화 가게 — 영감 · 처음부터
///   약탕간   — 참봉 · 첫 번째 밤
///   대장간   — 빚쟁이 · 첫 번째 밤 (옛 무기 상점 + 방어구 상점)
///   장부방   — 길달 · 두 번째 밤 (등록 · 관리 — 기능은 아직 없다)
///
/// 비용 — 작업대 · 잡화 가게는 덕코프 건물 페이지를 옮긴 그대로 [확인됨 → 재료는 Dokkaebi 재료로 바꿈 · 불확실].
/// 대장간은 옛 무기 · 방어구 상점의 합, 약탕간 · 장부방은 본문의 재료(쇠붙이 · 숯 · 새끼 뭉치)로 정한 [임시값].
/// 최대 수량은 전부 1이다.
/// </summary>
public static class BuildingTable
{
    public const string Workbench = "workbench";
    public const string GeneralStore = "general_store";
    public const string Smithy = "smithy";
    public const string Apothecary = "apothecary";
    public const string LedgerRoom = "ledger_room";

    /// <summary>판 9까지의 무기 상점 · 방어구 상점 — 대장간으로 읽는다 (결정 2-52).</summary>
    public const string LegacyWeaponShop = "weapon_shop";
    public const string LegacyArmourShop = "armour_shop";

    private const string Scrap = "scrap_metal";
    private const string Battery = "cell_battery";
    private const string Wire = "wire_bundle";

    private static readonly BuildingDefinition[] all =
    {
        // 덕코프 작업대: 목재 ×2
        new(Workbench, "작업대", "아이템을 제조하거나 분해하며 수리한다. (제작은 다음 단계)",
            gold: 0,
            materials: new[] { new MaterialCost(Scrap, 2) },
            requiredBuildings: null, width: 2f, depth: 1.2f,
            opens: BunkerStation.Kind.None),

        // 덕코프 잡화 상점: 100 · 볼트 ×2 · 너트 ×2 · 목재 ×2 · 절전형 전구 ×1
        new(GeneralStore, ShopTable.GeneralStoreName, "영감의 좌판. 잡화와 음식, 재료를 사고 물건을 팔아 엽전으로 바꾼다. 옆에 부뚜막이 붙어 요리할 수 있다.",
            gold: 100,
            materials: new[] { new MaterialCost(Scrap, 6), new MaterialCost(Battery, 1) },
            requiredBuildings: null, width: 3.2f, depth: 1.4f,   // 오른쪽 1.2m는 부뚜막 (결정 2-71)
            opens: BunkerStation.Kind.GeneralStore),

        // 본문 「쇠붙이 · 숯 · 새끼 뭉치를 모아 와 샘가에 약탕간을 세운다」 [임시값]
        new(Apothecary, ShopTable.ApothecaryName, "참봉의 약탕간. 약품과 주사약을 사고판다.",
            gold: 100,
            materials: new[] { new MaterialCost(Scrap, 3), new MaterialCost(Battery, 2), new MaterialCost(Wire, 1) },
            requiredBuildings: null, width: 2f, depth: 1.4f,
            opens: BunkerStation.Kind.Apothecary),

        // 옛 무기 상점(쇠붙이 4 · 숯 2) + 방어구 상점(쇠붙이 4 · 숯 1 · 새끼 뭉치 1) · 작업대 [임시값]
        new(Smithy, ShopTable.SmithyName, "빚쟁이의 대장간. 무기 · 방어구 · 가방을 사고판다.",
            gold: 150,
            materials: new[] { new MaterialCost(Scrap, 6), new MaterialCost(Battery, 2), new MaterialCost(Wire, 1) },
            requiredBuildings: new[] { Workbench }, width: 2.4f, depth: 1.4f,
            opens: BunkerStation.Kind.Smithy),

        // 본문 「재료를 모아 와 장부방을 세운다」 — 등록 · 관리 기능은 아직 없다 [임시값]
        new(LedgerRoom, ShopTable.LedgerRoomName, "길달의 장부방. 도감 · 열쇠 등록 · 기록을 맡는다. (기능은 다음 단계)",
            gold: 150,
            materials: new[] { new MaterialCost(Scrap, 4), new MaterialCost(Wire, 2) },
            requiredBuildings: null, width: 2f, depth: 1.4f,
            opens: BunkerStation.Kind.None),
    };

    /// <summary>옛 id를 지금 id로. 대장간으로 합친 두 상점만 바뀐다.</summary>
    public static string Canonical(string id)
        => id == LegacyWeaponShop || id == LegacyArmourShop ? Smithy : id;

    public static IReadOnlyList<BuildingDefinition> All => all;

    public static BuildingDefinition Find(string id)
    {
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].Id == id)
                return all[i];
        }

        return null;
    }
}
