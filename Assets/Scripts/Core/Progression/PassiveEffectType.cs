/// <summary>
/// 패시브가 줄 수 있는 것. (docs/Blob_Passive_System.md 5절)
///
/// 【절대 규칙 — 패시브는 전투 수치를 주지 않는다.】
/// 방어도 · 방어 관통 · 피해 · 최대 체력 · 이동 속도 · 시야 · 감지 · 대시는
/// 전부 【장비】의 몫이다.
///
/// 덕코프는 이것을 지키지 않는다 — 「생존 본능 I = 최대 생명력 +5」,
/// 「팔굽혀펴기 I = 근접데미지 +5%」가 실제로 있다. [확인됨]
/// 따르지 않는 이유는 덕코프의 장비 옵션이 단순해 겹쳐도 장비의 존재 이유가 남기 때문이다.
/// Blob의 장비는 옵션이 25종이라, 패시브가 같은 축을 건드리면
/// 장비를 고르는 결정이 통째로 사라진다.
///
/// 새 효과를 추가할 때 먼저 물을 것 — 【이것이 전투 중에 적용되는가?】
/// 그렇다면 여기가 아니라 EquipmentStatType이다.
/// </summary>
public enum PassiveEffectType
{
    None = 0,

    // ── 적응 : 휴대 ───────────────────────────────────────────────────
    /// <summary>가방 적재 칸 +n. 장비(가방)와 겹치므로 총합에 상한을 둔다.</summary>
    CarrySlots = 1,

    /// <summary>최대 소지 중량 +n kg. 같은 이유로 상한을 둔다.</summary>
    CarryWeight = 2,

    // ── 대사 : 수집 ───────────────────────────────────────────────────
    /// <summary>경험치 획득량 +n%.</summary>
    AbsorbAmount = 10,

    /// <summary>흡수 감지 범위 +n m.</summary>
    AbsorbRange = 11,

    /// <summary>시체 전리품 추첨 횟수 +n.</summary>
    LootRolls = 12,

    /// <summary>희귀 드랍 확률 +n%.</summary>
    RareDropRate = 13,

    // ── 회수 : 죽어도 남는 것 ─────────────────────────────────────────
    /// <summary>
    /// 사망해도 지키는 가방 칸 +n.
    /// 【사망 규칙의 유일한 예외다.】 예외를 늘리지 않기 위해 이 계열 밖에 두지 않는다.
    /// </summary>
    SafeSlots = 20,

    /// <summary>【해금】 추출 실패 시 내 시체에서 1회 회수.</summary>
    CorpseRecovery = 21,

    /// <summary>【해금】 지도에 추출 지점 상시 표시.</summary>
    ExtractMark = 22,

    // ── 중개 : 벙커 경제 ──────────────────────────────────────────────
    /// <summary>판매가 +n%.</summary>
    SellPrice = 30,

    /// <summary>창고 칸 +n.</summary>
    StashSlots = 31,

    /// <summary>상점 갱신 쿨타임 −n%.</summary>
    ShopRefresh = 32,

    /// <summary>상점 갱신 횟수 +n.</summary>
    ShopSlots = 33,

    // ── 역행 : 숨겨진 것 (전부 해금형) ────────────────────────────────
    /// <summary>【해금】 제작대.</summary>
    CraftBench = 40,

    /// <summary>【해금】 처치만 해도 도감에 등록. 변이 샘플 흡수가 불필요해진다.</summary>
    CodexAuto = 41,

    /// <summary>【해금】 인자를 분해해 재료로 되돌린다.</summary>
    GemSalvage = 42,

    /// <summary>【해금】 지도에 전리품 위치 표시.</summary>
    MapLoot = 43
}

/// <summary>패시브 효과의 표시 이름과 성질.</summary>
public static class PassiveEffectInfo
{
    /// <summary>값이 아니라 켜고 끄는 해금인지.</summary>
    public static bool IsUnlockFlag(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.CorpseRecovery:
            case PassiveEffectType.ExtractMark:
            case PassiveEffectType.CraftBench:
            case PassiveEffectType.CodexAuto:
            case PassiveEffectType.GemSalvage:
            case PassiveEffectType.MapLoot:
                return true;

            default:
                return false;
        }
    }

    /// <summary>백분율로 표시하는 효과인지.</summary>
    public static bool IsPercent(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.AbsorbAmount:
            case PassiveEffectType.RareDropRate:
            case PassiveEffectType.SellPrice:
            case PassiveEffectType.ShopRefresh:
                return true;

            default:
                return false;
        }
    }

    /// <summary>수치 뒤에 붙는 단위.</summary>
    public static string Unit(PassiveEffectType type)
    {
        if (IsPercent(type))
            return "%";

        switch (type)
        {
            case PassiveEffectType.CarryWeight:  return " kg";
            case PassiveEffectType.AbsorbRange:  return " m";
            case PassiveEffectType.CarrySlots:
            case PassiveEffectType.SafeSlots:
            case PassiveEffectType.StashSlots:
            case PassiveEffectType.ShopSlots:    return "칸";
            case PassiveEffectType.LootRolls:    return "회";
            default:                             return string.Empty;
        }
    }

    public static string Describe(PassiveEffectType type, float value)
    {
        if (IsUnlockFlag(type))
            return Name(type);

        // 쿨타임 감소는 값이 양수여도 「−」로 보여야 뜻이 맞는다.
        string sign = type == PassiveEffectType.ShopRefresh ? "−" : "+";

        return $"{Name(type)} {sign}{value:0.#}{Unit(type)}";
    }

    public static string Name(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.CarrySlots:     return "가방 공간";
            case PassiveEffectType.CarryWeight:    return "소지 중량";
            case PassiveEffectType.AbsorbAmount:   return "경험치 획득";
            case PassiveEffectType.AbsorbRange:    return "흡수 범위";
            case PassiveEffectType.LootRolls:      return "전리품 추첨";
            case PassiveEffectType.RareDropRate:   return "희귀 드랍";
            case PassiveEffectType.SafeSlots:      return "보존 칸";
            case PassiveEffectType.CorpseRecovery: return "시체 회수";
            case PassiveEffectType.ExtractMark:    return "추출 지점 표시";
            case PassiveEffectType.SellPrice:      return "판매가";
            case PassiveEffectType.StashSlots:     return "창고 칸";
            case PassiveEffectType.ShopRefresh:    return "상점 갱신 쿨타임";
            case PassiveEffectType.ShopSlots:      return "상점 갱신 횟수";
            case PassiveEffectType.CraftBench:     return "제작대";
            case PassiveEffectType.CodexAuto:      return "도감 자동 등록";
            case PassiveEffectType.GemSalvage:     return "인자 분해";
            case PassiveEffectType.MapLoot:        return "전리품 표시";
            default:                               return "없음";
        }
    }
}
