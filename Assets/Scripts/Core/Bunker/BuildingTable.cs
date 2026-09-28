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
/// 【벙커 건물표.】 (docs/Blob_Bunker_System.md 1 · 3-4절)
///
/// 덕코프 건물 페이지의 「비용 · 필요한 건물 · 최대 수량」을 옮겼다 [확인됨].
/// 모든 건물의 최대 수량은 1이다 — 덕코프도 이 넷은 전부 1이다.
///
/// 【재료는 Blob 재료로 바꿨다.】 덕코프의 목재 · 볼트 · 너트 · 못 · 금속 조각은
/// Blob에 없다. 쇠붙이는 고철, 전구 · MD40은 전지, 로프는 전선 뭉치로 옮기고
/// 개수는 그대로 더했다 [불확실 — 대응은 Blob의 결정].
///
/// 【퀘스트 조건은 뺐다.】 덕코프의 무기 상점은 퀘스트 「신호탑」, 방어구 상점은
/// 「옛 친구의 편지」가 필요하다. 퀘스트는 10단계라 지금은 건물 조건만 둔다.
/// </summary>
public static class BuildingTable
{
    public const string Workbench = "workbench";
    public const string GeneralStore = "general_store";
    public const string WeaponShop = "weapon_shop";
    public const string ArmourShop = "armour_shop";

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
        new(GeneralStore, ShopTable.GeneralStoreName, "각종 약품과 잡화를 사거나 물건을 팔아 골드로 바꾼다.",
            gold: 100,
            materials: new[] { new MaterialCost(Scrap, 6), new MaterialCost(Battery, 1) },
            requiredBuildings: null, width: 2f, depth: 1.4f,
            opens: BunkerStation.Kind.GeneralStore),

        // 덕코프 무기 상점: 100 · 볼트 ×2 · 못 ×2 · 절전형 전구 ×1 · MD40 ×1 · 작업대
        new(WeaponShop, ShopTable.WeaponShopName, "무기를 사고판다.",
            gold: 100,
            materials: new[] { new MaterialCost(Scrap, 4), new MaterialCost(Battery, 2) },
            requiredBuildings: new[] { Workbench }, width: 2f, depth: 1.4f,
            opens: BunkerStation.Kind.WeaponShop),

        // 덕코프 방어구 상점: 100 · 너트 ×2 · 금속 조각 ×2 · 전구 ×1 · 로프 ×1 · 작업대
        new(ArmourShop, ShopTable.ArmourShopName, "헬멧 · 방어구 · 가방을 사고판다.",
            gold: 100,
            materials: new[] { new MaterialCost(Scrap, 4), new MaterialCost(Battery, 1), new MaterialCost(Wire, 1) },
            requiredBuildings: new[] { Workbench }, width: 2f, depth: 1.4f,
            opens: BunkerStation.Kind.ArmourShop),
    };

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
