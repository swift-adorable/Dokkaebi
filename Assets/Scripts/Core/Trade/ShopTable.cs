using System.Collections.Generic;

/// <summary>어느 상점인가. 【덕코프처럼 종류별로 나뉜다】 [확인됨 — 덕코프 건물 목록]</summary>
public enum ShopKind
{
    /// <summary>잡화 상점 — 약품 · 잡화.</summary>
    General = 0,

    /// <summary>무기 상점 — 무기.</summary>
    Weapon = 1,

    /// <summary>방어구 상점 — 헬멧 · 방어구 · 가방.</summary>
    Armour = 2
}

/// <summary>상점 한 줄 — 무엇을, 몇 개까지, 가치의 몇 배로 파는가.</summary>
public readonly struct ShopEntry
{
    public readonly string ItemId;

    /// <summary>재고가 다 차 있을 때의 개수. 덕코프 상점 표의 「최대 재고」.</summary>
    public readonly int MaxStock;

    /// <summary>구매가 = 아이템 가치 × 이 값. 덕코프 상점 표의 「가격 계수」.</summary>
    public readonly float PriceMultiplier;

    public ShopEntry(string itemId, int maxStock, float priceMultiplier)
    {
        ItemId = itemId;
        MaxStock = maxStock < 1 ? 1 : maxStock;
        PriceMultiplier = priceMultiplier <= 0f ? 1f : priceMultiplier;
    }
}

/// <summary>
/// 【상점 재고표.】 (docs/Blob_Bunker_System.md 3절)
///
/// 덕코프의 거래 건물은 종류별로 나뉜다 [확인됨] —
///   잡화 상점  「각종 약품과 잡화를 구매하거나 물건을 팔아 돈으로 바꾼다」
///   무기 상점  「무기, 탄약 그리고 살인과 약탈에 사용하는 물건들이 있다」
///   방어구 상점 「각종 헬멧과 방어구를 판매한다」 (가방도 판다)
/// 【세 상점 모두 플레이어의 물건을 사 준다】 (결정 2-37, 사용자 결정 2026-09-29).
/// 덕코프는 설명에 「팔아 돈으로 바꾼다」가 적힌 건물이 잡화 상점뿐이고, 다른 상점에서도
/// 팔 수 있는지는 위키에 없다 [확인 불가]. 판매가는 어느 상점이든 같다 (TradeRules.SellPrice).
///
/// 【잡화 상점】 재고와 가격 계수는 덕코프 아이템 페이지의 상점 표 그대로다 [확인됨].
/// 주사약은 덕코프에서 머드(헬스장을 지으면 입주)가 판다. Blob에는 헬스장이 없어
/// 잡화 상점에 둔다. 해제약 넷과 음식은 팔지 않는다 — 상점 표에 없거나 수집하지 못했다.
///
/// 【무기 상점 · 방어구 상점】 Blob의 무기 · 방어구는 덕코프에 같은 물건이 없어 줄마다의
/// 값을 옮길 수 없다. 덕코프 방어구 상점의 오렌지 헤드폰이 「최대 재고 1 · 1.00×」인 것을
/// 따라 전부 1개 · 1.00×로 둔다 [불확실]. 【티어 1~3만 판다】 — 벙커 문서 7절 3번
/// 「상점에서 최상위를 팔지 않는다」.
/// </summary>
public static class ShopTable
{
    public const string GeneralStoreName = "잡화 상점";
    public const string WeaponShopName = "무기 상점";
    public const string ArmourShopName = "방어구 상점";

    private static readonly ShopEntry[] general =
    {
        // 잡화 상점 [확인됨 — 덕코프 소형 구급상자 · 구급상자 · 대형 구급상자 · 지혈 붕대 · 아스피린]
        new("con_medkit_small", 3, 1.00f),
        new("con_medkit",       2, 1.00f),
        new("con_medkit_large", 1, 1.00f),
        new("con_bandage",      3, 1.00f),
        new("con_aspirin",      2, 1.00f),

        // 머드 [확인됨 — 덕코프 노란·강화·회복·중량·화염 저항·전기 저항·독 저항 주사약, 타길라의 약품]
        new("con_stim",        3, 1.00f),
        new("con_coagulant",   3, 1.00f),
        new("con_regen",       3, 1.00f),
        new("con_expander",    3, 1.00f),
        new("con_ward_fire",   3, 1.00f),
        new("con_ward_shock",  3, 1.00f),
        new("con_ward_toxin",  3, 1.00f),
        new("con_ward_cold",   3, 1.00f),   // 덕코프에 없음 — 공간 저항 자리
        new("con_absorbent",   1, 1.00f),   // 타길라의 약품 자리 (최대 재고 1)
    };

    private static readonly ShopEntry[] weapon =
    {
        new("wpn_t1_pipe", 1, 1.00f),
        new("wpn_t2_coil", 1, 1.00f),
        new("wpn_t3_acid", 1, 1.00f),
    };

    private static readonly ShopEntry[] armour =
    {
        new("arm_head_t1", 1, 1.00f),
        new("arm_head_t2", 1, 1.00f),
        new("arm_head_t3", 1, 1.00f),
        new("arm_body_t1", 1, 1.00f),
        new("arm_body_t1_light", 1, 1.00f),
        new("arm_body_t1_heavy", 1, 1.00f),
        new("arm_body_t2", 1, 1.00f),
        new("arm_body_t2_light", 1, 1.00f),
        new("arm_body_t2_heavy", 1, 1.00f),
        new("arm_body_t3", 1, 1.00f),
        new("arm_body_t3_light", 1, 1.00f),
        new("arm_body_t3_heavy", 1, 1.00f),
        new("arm_face_t1", 1, 1.00f),
        new("arm_face_physical_t2", 1, 1.00f),
        new("arm_face_fire_t2", 1, 1.00f),
        new("arm_face_cold_t2", 1, 1.00f),
        new("arm_face_lightning_t2", 1, 1.00f),
        new("arm_face_chaos_t2", 1, 1.00f),
        new("arm_ears_t1", 1, 1.00f),
        new("arm_ears_t2", 1, 1.00f),
        new("arm_ears_t3", 1, 1.00f),
        new("bag_t1_sack", 1, 1.00f),
        new("bag_t2_vest", 1, 1.00f),
        new("bag_t3_pack", 1, 1.00f),
    };

    public static readonly ShopKind[] All = { ShopKind.General, ShopKind.Weapon, ShopKind.Armour };

    /// <summary>잡화 상점의 줄들.</summary>
    public static IReadOnlyList<ShopEntry> General => general;

    /// <summary>그 상점의 줄들.</summary>
    public static IReadOnlyList<ShopEntry> For(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Weapon: return weapon;
            case ShopKind.Armour: return armour;
            default:              return general;
        }
    }

    public static string NameOf(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Weapon: return WeaponShopName;
            case ShopKind.Armour: return ArmourShopName;
            default:              return GeneralStoreName;
        }
    }

    /// <summary>플레이어의 물건을 사 주는가. 【셋 다 사 준다】 (결정 2-37).</summary>
    public static bool BuysFromPlayer(ShopKind kind) => true;

    /// <summary>이 상점의 이 아이템 줄. 팔지 않는 것이면 false.</summary>
    public static bool TryFind(ShopKind kind, string itemId, out ShopEntry entry)
    {
        IReadOnlyList<ShopEntry> table = For(kind);

        for (int i = 0; i < table.Count; i++)
        {
            if (table[i].ItemId != itemId)
                continue;

            entry = table[i];
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>잡화 상점에서 찾는다.</summary>
    public static bool TryFind(string itemId, out ShopEntry entry)
        => TryFind(ShopKind.General, itemId, out entry);
}
