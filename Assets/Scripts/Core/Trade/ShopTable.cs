using System.Collections.Generic;

/// <summary>
/// 어느 가게인가 — 상인 넷이 나눠 맡는다 (결정 2-52 · 2-57). 장부방(길달)은 사고팔지 않는다.
/// 【세이브에는 이름으로 적힌다】 옛 판의 「Weapon」 · 「Armour」는 ShopManager가 대장간으로 읽는다.
/// </summary>
public enum ShopKind
{
    /// <summary>잡화 가게 — 영감. 잡화 · 음식 · 재료 · 매입 (요리는 Cooking — 미구현).</summary>
    General = 0,

    /// <summary>대장간 — 빚쟁이. 무기 + 방어구 + 가방 (옛 무기 상점 + 방어구 상점).</summary>
    Smithy = 1,

    /// <summary>약탕간 — 참봉. 약품 · 주사약 (덕코프 머드 · 의료소 자리).</summary>
    Apothecary = 2
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
/// 【가게 재고표.】 (docs/Dokkaebi_Bunker_System.md 1 · 3절 · 결정 2-52 · 2-57)
///
/// 【잡화 가게 — 영감】 잡화 · 음식 · 재료. **약은 팔지 않는다** (결정 2-57). 음식 · 재료의 재고는 [임시값].
/// 【약탕간 — 참봉】 옛 잡화 상점의 약 줄 그대로 — 재고와 가격 계수는 덕코프 상점 표 [확인됨].
///   주사약은 덕코프에서 머드가 판다 — 약은 전부 참봉이다. 해제약 넷은 팔지 않는다 (상점 표에 없거나 수집하지 못했다).
/// 【대장간 — 빚쟁이】 옛 무기 상점 + 방어구 상점을 합쳤다 (결정 2-52). 덕코프에 같은 물건이 없어
///   전부 1개 · 1.00× [불확실]. 【티어 1~3만 판다】 — 「상점에서 최상위를 팔지 않는다」. 각인 Ⅰ · Ⅱ 판매는 [미구현].
/// 【세 가게 모두 플레이어의 물건을 사 준다】 (결정 2-37). 판매가는 어디서 팔든 같다 (TradeRules.SellPrice).
/// </summary>
public static class ShopTable
{
    public const string GeneralStoreName = "잡화 가게";
    public const string SmithyName = "대장간";
    public const string ApothecaryName = "약탕간";
    public const string LedgerRoomName = "장부방";

    private static readonly ShopEntry[] general =
    {
        // 음식 · 음료 [임시값 — 재고]
        new("con_water",      3, 1.00f),
        new("con_soda",       3, 1.00f),
        new("con_canned",     2, 1.00f),
        new("con_ration",     3, 1.00f),
        new("con_energy_bar", 3, 1.00f),
        new("con_whisky",     2, 1.00f),

        // 공용 재료 — 쇠붙이 · 숯 · 새끼 뭉치 [임시값 — 재고 · 가격 계수 1.5×: 주워 오는 것보다 비싸야 한다]
        new("scrap_metal",  5, 1.50f),
        new("cell_battery", 3, 1.50f),
        new("wire_bundle",  3, 1.50f),

        // 탄 (결정 2-80) — 1티어 무기(환목궁 · 세총통)의 탄만. 대장간이 열리기 전(0장 · 1장)에도 살 수 있게 [임시값 — 재고]
        new(AmmoTable.Arrow, 300, 1.00f),
        new(AmmoTable.Shot,  300, 1.00f),
    };

    private static readonly ShopEntry[] apothecary =
    {
        // [확인됨 — 덕코프 소형 구급상자 · 구급상자 · 대형 구급상자 · 지혈 붕대 · 아스피린]
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

        // 막이 소모품 【임시】 (결정 2-91)
        new("con_guard_warmth", 3, 1.00f),
        new("con_guard_shield", 3, 1.00f),
        new("con_guard_cool",   3, 1.00f),
        new("con_guard_light",  3, 1.00f),
    };

    private static readonly ShopEntry[] smithy =
    {
        new("wpn_t1_pipe", 1, 1.00f),
        new("wpn_t2_coil", 1, 1.00f),
        new("wpn_t3_acid", 1, 1.00f),
        new("wpn_t1_sechongtong", 1, 1.00f),
        new("wpn_t2_gwoljangno",  1, 1.00f),
        new("wpn_t2_seungja",     1, 1.00f),
        new("wpn_t3_pyeonjeon",   1, 1.00f),
        new("wpn_t3_sunogi",      1, 1.00f),
        new("wpn_t3_soseungja",   1, 1.00f),

        // 탄 7종 (결정 2-80) [임시값 — 재고]
        new(AmmoTable.Arrow,     300, 1.00f),
        new(AmmoTable.Pyeonjeon, 200, 1.00f),
        new(AmmoTable.Bolt,      200, 1.00f),
        new(AmmoTable.Dart,      300, 1.00f),
        new(AmmoTable.Shot,      300, 1.00f),
        new(AmmoTable.Scatter,   200, 1.00f),
        new(AmmoTable.Rocket,     20, 1.00f),

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
        new("arm_guard_warmth", 1, 1.00f),   // 막이 머리 【임시】 (결정 2-91)
        new("arm_guard_shield", 1, 1.00f),
        new("arm_guard_cool",   1, 1.00f),
        new("arm_guard_light",  1, 1.00f),
        new("bag_t1_sack", 1, 1.00f),
        new("bag_t2_vest", 1, 1.00f),
        new("bag_t3_pack", 1, 1.00f),
    };

    public static readonly ShopKind[] All = { ShopKind.General, ShopKind.Smithy, ShopKind.Apothecary };

    /// <summary>잡화 가게의 줄들.</summary>
    public static IReadOnlyList<ShopEntry> General => general;

    /// <summary>그 상점의 줄들.</summary>
    public static IReadOnlyList<ShopEntry> For(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Smithy:     return smithy;
            case ShopKind.Apothecary: return apothecary;
            default:                  return general;
        }
    }

    public static string NameOf(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Smithy:     return SmithyName;
            case ShopKind.Apothecary: return ApothecaryName;
            default:                  return GeneralStoreName;
        }
    }

    /// <summary>플레이어의 물건을 사 주는가. 【세 가게 다 사 준다】 (결정 2-37).</summary>
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

    /// <summary>잡화 가게에서 찾는다.</summary>
    public static bool TryFind(string itemId, out ShopEntry entry)
        => TryFind(ShopKind.General, itemId, out entry);
}
