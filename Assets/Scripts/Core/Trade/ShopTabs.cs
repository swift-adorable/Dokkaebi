using System.Collections.Generic;

/// <summary>
/// 【가게 위쪽 탭】 (결정 2-120) — 그 가게가 파는 물건의 종류로 저절로 나뉜다. 맨 앞은 「전체」.
/// 종류가 하나뿐인 가게(약탕간)는 탭 줄을 띄우지 않는다. 순서는 늘 같다 — 무기 · 탄 · 방어구 · 가방 · 소모품 · 재료.
/// </summary>
public static class ShopTabs
{
    private static readonly ItemKind[] Order =
    {
        ItemKind.Weapon, ItemKind.Ammo, ItemKind.Armour, ItemKind.Backpack,
        ItemKind.Consumable, ItemKind.Material, ItemKind.Imprint, ItemKind.SkillGem, ItemKind.Key,
    };

    /// <summary>들어온 종류들을 정해진 순서로, 겹치지 않게.</summary>
    public static List<ItemKind> KindsOf(IEnumerable<ItemKind> kinds)
    {
        var present = new HashSet<ItemKind>();
        if (kinds != null)
            foreach (ItemKind k in kinds)
                present.Add(k);

        var result = new List<ItemKind>();
        foreach (ItemKind k in Order)
            if (present.Contains(k))
                result.Add(k);

        return result;
    }

    /// <summary>탭 줄을 띄우는가 — 종류가 둘 이상일 때만.</summary>
    public static bool Shows(IReadOnlyCollection<ItemKind> kinds) => kinds != null && kinds.Count >= 2;

    public static string Name(ItemKind kind) => kind switch
    {
        ItemKind.Weapon => "무기",
        ItemKind.Ammo => "탄",
        ItemKind.Armour => "방어구",
        ItemKind.Backpack => "가방",
        ItemKind.Consumable => "소모품",
        ItemKind.Material => "재료",
        ItemKind.Imprint => "새김패",
        ItemKind.SkillGem => "구슬",
        ItemKind.Key => "열쇠",
        _ => kind.ToString(),
    };
}
